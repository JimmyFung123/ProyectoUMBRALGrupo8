import { useEffect, useRef, useState } from 'react';
import { MapContainer, TileLayer, Marker, Popup, useMap } from 'react-leaflet';
import L from 'leaflet';
import { Html5Qrcode } from 'html5-qrcode';
import type { ParticipantStage, QrValidationResult } from '../types';

// Fix Leaflet default icon paths (the bundler does not serve them automatically)
delete (L.Icon.Default.prototype as unknown as { _getIconUrl?: unknown })._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

interface Props {
  stage: ParticipantStage;
  sessionId: string;
  teamId: string;
  onResolved: (result: QrValidationResult) => void;
  onError: (msg: string) => void;
  validateQr: (
    sessionId: string,
    teamId: string,
    stageId: string,
    scannedCode: string,
  ) => Promise<QrValidationResult>;
}

const SCANNER_ELEMENT_ID = 'umbral-qr-scanner';

export function TreasureHuntScreen({
  stage, sessionId, teamId, onResolved, onError, validateQr,
}: Props) {
  const [scannerOpen, setScannerOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [scanError, setScanError] = useState<string | null>(null);
  const [manualOpen, setManualOpen] = useState(false);
  const [manualCode, setManualCode] = useState('');
  const scannerRef = useRef<Html5Qrcode | null>(null);

  const hasCoordinates =
    typeof stage.latitude === 'number' && typeof stage.longitude === 'number';

  useEffect(() => {
    return () => {
      stopScanner();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function startScanner() {
    setScanError(null);
    setScannerOpen(true);

    // The element must be in the DOM before Html5Qrcode is instantiated
    setTimeout(async () => {
      try {
        const scanner = new Html5Qrcode(SCANNER_ELEMENT_ID, /* verbose */ false);
        scannerRef.current = scanner;

        // Use a relative qrbox so the scanning region scales with the viewport
        // and grabs a much bigger area than the previous fixed 240x240.
        const qrboxFn = (viewfinderWidth: number, viewfinderHeight: number) => {
          const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
          const size = Math.floor(minEdge * 0.75);
          return { width: size, height: size };
        };

        await scanner.start(
          { facingMode: 'environment' },
          {
            fps: 15,
            qrbox: qrboxFn,
            aspectRatio: 1.0,
            disableFlip: false,
          },
          (decodedText) => { void handleDecoded(decodedText); },
          () => { /* per-frame failure — ignore */ },
        );
      } catch (err) {
        console.error('[TreasureHunt] scanner start failed:', err);
        setScanError(
          'No se pudo acceder a la cámara. Verifica los permisos del navegador y que estés en HTTPS.',
        );
      }
    }, 50);
  }

  async function stopScanner() {
    const scanner = scannerRef.current;
    scannerRef.current = null;
    if (scanner) {
      try {
        if (scanner.isScanning) await scanner.stop();
        scanner.clear();
      } catch { /* swallow */ }
    }
  }

  async function handleDecoded(scannedCode: string) {
    if (submitting) return;
    setSubmitting(true);
    await stopScanner();
    setScannerOpen(false);

    try {
      const result = await validateQr(sessionId, teamId, stage.stageId, scannedCode);
      if (!result.isCorrect) {
        setScanError('QR incorrecto. Verifica que escaneaste el código del lugar correcto.');
        setSubmitting(false);
        return;
      }
      onResolved(result);
    } catch {
      onError('No se pudo validar el código QR. Intentá de nuevo.');
      setSubmitting(false);
    }
  }

  async function handleCloseScanner() {
    await stopScanner();
    setScannerOpen(false);
  }

  async function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    const code = manualCode.trim();
    if (!code || submitting) return;
    setManualOpen(false);
    setManualCode('');
    await handleDecoded(code);
  }

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <div style={styles.header}>
          <span style={styles.stageBadge}>Etapa {stage.order}</span>
          {stage.isLastStage && <span style={styles.lastBadge}>Última etapa</span>}
          <span style={styles.modeBadge}>🗺️ Búsqueda del tesoro</span>
        </div>

        <h2 style={styles.title}>{stage.title}</h2>
        <p style={styles.subtitle}>
          Sigue las pistas en el mapa, encuentra el lugar y escanea el código QR del tesoro.
        </p>

        {hasCoordinates ? (
          <div style={styles.mapWrapper}>
            <MapContainer
              center={[stage.latitude as number, stage.longitude as number]}
              zoom={16}
              style={styles.map}
              scrollWheelZoom={false}
            >
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              <Marker position={[stage.latitude as number, stage.longitude as number]}>
                <Popup>Zona aproximada del tesoro</Popup>
              </Marker>
              <RecenterMap lat={stage.latitude as number} lng={stage.longitude as number} />
            </MapContainer>
          </div>
        ) : (
          <div style={styles.mapPlaceholder}>
            <p style={styles.placeholderText}>
              Esperando la primera pista geográfica…
            </p>
          </div>
        )}

        {scanError && (
          <div style={styles.errorBox}>
            {scanError}
          </div>
        )}

        <button
          onClick={startScanner}
          disabled={submitting}
          style={{
            ...styles.scanBtn,
            opacity: submitting ? 0.5 : 1,
            cursor: submitting ? 'not-allowed' : 'pointer',
          }}
        >
          {submitting ? 'Validando…' : '📷 Escanear código QR'}
        </button>

        <button
          onClick={() => !submitting && setManualOpen(true)}
          disabled={submitting}
          style={{
            ...styles.manualBtn,
            opacity: submitting ? 0.5 : 1,
            cursor: submitting ? 'not-allowed' : 'pointer',
          }}
        >
          Ingresar código manualmente
        </button>

        <p style={styles.hint}>
          El escáner usa la cámara de tu dispositivo. Solo el QR correcto avanza la etapa.
        </p>
      </div>

      {scannerOpen && (
        <div style={styles.scannerOverlay}>
          <div style={styles.scannerCard}>
            <div style={styles.scannerHeader}>
              <h3 style={styles.scannerTitle}>Escanear QR</h3>
              <button onClick={handleCloseScanner} style={styles.closeBtn}>✕</button>
            </div>
            <div id={SCANNER_ELEMENT_ID} style={styles.scannerArea} />
            <p style={styles.scannerHint}>
              Apunta la cámara al QR del tesoro. Manténelo enfocado dentro del recuadro y con buena luz.
            </p>
          </div>
        </div>
      )}

      {manualOpen && (
        <div style={styles.scannerOverlay}>
          <form onSubmit={handleManualSubmit} style={styles.scannerCard}>
            <div style={styles.scannerHeader}>
              <h3 style={styles.scannerTitle}>Ingresar código del tesoro</h3>
              <button
                type="button"
                onClick={() => { setManualOpen(false); setManualCode(''); }}
                style={styles.closeBtn}
              >✕</button>
            </div>
            <p style={styles.scannerHint}>
              Pedile al profesor el código del QR (lo ve en la configuración de la etapa) y escribilo acá.
            </p>
            <input
              type="text"
              value={manualCode}
              onChange={(e) => setManualCode(e.target.value)}
              placeholder="Código QR"
              autoFocus
              style={styles.manualInput}
            />
            <button
              type="submit"
              disabled={!manualCode.trim() || submitting}
              style={{
                ...styles.scanBtn,
                marginTop: '0.75rem',
                opacity: !manualCode.trim() || submitting ? 0.5 : 1,
              }}
            >
              {submitting ? 'Validando…' : 'Validar código'}
            </button>
          </form>
        </div>
      )}
    </div>
  );
}

function RecenterMap({ lat, lng }: { lat: number; lng: number }) {
  const map = useMap();
  useEffect(() => {
    map.setView([lat, lng], map.getZoom(), { animate: true });
  }, [lat, lng, map]);
  return null;
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100dvh', display: 'flex', alignItems: 'center',
    justifyContent: 'center', padding: '1rem', background: '#0f172a',
  },
  card: {
    width: '100%', maxWidth: 480, padding: '1.5rem 1.25rem',
    background: '#1e293b', borderRadius: 16,
    boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
  },
  header: {
    display: 'flex', alignItems: 'center', gap: '0.5rem',
    flexWrap: 'wrap', marginBottom: '0.75rem',
  },
  stageBadge: {
    background: '#334155', color: '#94a3b8',
    fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.6rem',
    borderRadius: 9999, letterSpacing: '0.05em',
  },
  lastBadge: {
    background: '#6366f1', color: '#fff',
    fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.6rem',
    borderRadius: 9999,
  },
  modeBadge: {
    background: '#0f172a', color: '#a5b4fc',
    fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.6rem',
    borderRadius: 9999, border: '1px solid #4338ca',
  },
  title: {
    color: '#f8fafc', fontSize: '1.4rem', fontWeight: 800,
    margin: '0 0 0.5rem',
  },
  subtitle: {
    color: '#cbd5e1', fontSize: '0.95rem', lineHeight: 1.5,
    margin: '0 0 1rem',
  },
  mapWrapper: {
    width: '100%', height: 260, borderRadius: 12,
    overflow: 'hidden', marginBottom: '1rem',
    border: '1px solid #334155',
  },
  map: { width: '100%', height: '100%' },
  mapPlaceholder: {
    width: '100%', height: 200, borderRadius: 12,
    background: '#0f172a', border: '1px dashed #334155',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    marginBottom: '1rem',
  },
  placeholderText: { color: '#64748b', fontSize: '0.9rem' },
  errorBox: {
    background: '#7f1d1d', color: '#fecaca',
    padding: '0.75rem 1rem', borderRadius: 10,
    fontSize: '0.9rem', marginBottom: '0.75rem',
    border: '1px solid #b91c1c',
  },
  scanBtn: {
    width: '100%', padding: '0.95rem',
    background: '#6366f1', color: '#fff',
    border: 'none', borderRadius: 12,
    fontSize: '1rem', fontWeight: 700,
    transition: 'opacity 0.15s',
  },
  manualBtn: {
    width: '100%', padding: '0.7rem',
    background: 'transparent', color: '#94a3b8',
    border: '1px solid #334155', borderRadius: 12,
    fontSize: '0.85rem', fontWeight: 600,
    marginTop: '0.5rem',
  },
  manualInput: {
    width: '100%', padding: '0.85rem 1rem',
    background: '#1e293b', color: '#f8fafc',
    border: '1px solid #334155', borderRadius: 10,
    fontSize: '0.95rem', boxSizing: 'border-box',
    marginTop: '0.5rem',
    fontFamily: 'monospace',
  },
  hint: {
    color: '#64748b', fontSize: '0.8rem',
    textAlign: 'center', marginTop: '0.75rem',
  },
  scannerOverlay: {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.85)',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    zIndex: 1100, padding: '1rem',
  },
  scannerCard: {
    width: '100%', maxWidth: 420, background: '#0f172a',
    borderRadius: 16, padding: '1rem',
    border: '1px solid #334155',
  },
  scannerHeader: {
    display: 'flex', justifyContent: 'space-between', alignItems: 'center',
    marginBottom: '0.5rem',
  },
  scannerTitle: { color: '#f8fafc', margin: 0, fontSize: '1.1rem' },
  closeBtn: {
    background: 'none', border: 'none', color: '#94a3b8',
    fontSize: '1.25rem', cursor: 'pointer',
  },
  scannerArea: {
    width: '100%', minHeight: 280, background: '#000',
    borderRadius: 10, overflow: 'hidden',
  },
  scannerHint: {
    color: '#94a3b8', fontSize: '0.85rem',
    textAlign: 'center', marginTop: '0.75rem',
  },
};
