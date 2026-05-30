import 'leaflet/dist/leaflet.css';

import L, { type LatLng } from 'leaflet';
import { useRef, useState } from 'react';
import { Circle, MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet';
import { QRCodeCanvas } from 'qrcode.react';

// ── Corrección de íconos de Leaflet rotos por el bundler ─────────────────────
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)['_getIconUrl'];
L.Icon.Default.mergeOptions({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// ── Tipos ────────────────────────────────────────────────────────────────────
export interface TreasureHuntData {
  latitude: number;
  longitude: number;
  qrCode: string;
}

interface Props {
  onChange: (data: TreasureHuntData | null) => void;
  disabled?: boolean;
  initialLatitude?: number;
  initialLongitude?: number;
  initialQrCode?: string;
}

// ── LocationPicker: vive dentro de <MapContainer> ────────────────────────────
function LocationPicker({ onPick, disabled }: { onPick: (ll: LatLng) => void; disabled: boolean }) {
  useMapEvents({ click: (e) => { if (!disabled) onPick(e.latlng); } });
  return null;
}

// ── Centro por defecto: UCAB, Caracas ────────────────────────────────────────
const DEFAULT_CENTER: [number, number] = [10.4866, -66.8543];

// ── Componente ───────────────────────────────────────────────────────────────
export function TreasureHuntConfig({
  onChange,
  disabled = false,
  initialLatitude,
  initialLongitude,
  initialQrCode,
}: Props) {
  const [position, setPosition] = useState<LatLng | null>(() =>
    initialLatitude != null && initialLongitude != null
      ? L.latLng(initialLatitude, initialLongitude)
      : null
  );
  const [radius, setRadius] = useState(50);

  // Si se edita una etapa existente, se conserva su código QR; de lo contrario se genera uno nuevo
  const [qrCode] = useState<string>(() => initialQrCode ?? crypto.randomUUID());

  const qrWrapperRef = useRef<HTMLDivElement>(null);
  // Canvas oculto en alta resolución, usado solo para exportar el QR a imprimir.
  const qrExportRef = useRef<HTMLDivElement>(null);

  // Tamaño de descarga del QR (px). El QR visible se mantiene pequeño (140px)
  // por layout, pero el archivo se exporta grande para que imprima nítido.
  const QR_EXPORT_SIZE = 2048;

  function handlePickLocation(ll: LatLng) {
    setPosition(ll);
    onChange({ latitude: ll.lat, longitude: ll.lng, qrCode });
  }

  function handleDownloadQR() {
    // Exportamos desde el canvas oculto de alta resolución, no del visible (140px),
    // para que el PNG descargado no salga pixelado al imprimirlo.
    const canvas = qrExportRef.current?.querySelector('canvas') as HTMLCanvasElement | null;
    if (!canvas) return;
    const link = document.createElement('a');
    link.href = canvas.toDataURL('image/png');
    link.download = `qr-${qrCode.slice(0, 8)}.png`;
    link.click();
  }

  return (
    <div className="rounded-xl border border-amber-200 bg-white overflow-hidden text-left mt-3">

      {/* Encabezado */}
      <div className="flex items-center gap-2 px-4 py-2 bg-amber-50 border-b border-amber-200">
        <span className="text-base">🗺️</span>
        <span className="font-semibold text-amber-900 text-sm">Configuración del Tesoro</span>
      </div>

      {/* Cuerpo: mapa + panel QR */}
      <div className="grid grid-cols-1 md:grid-cols-[1fr_280px]">

        {/* ── Mapa ───────────────────────────────────────────────── */}
        <div className="relative">
          {!position && (
            <div className="absolute inset-0 z-10 flex items-center justify-center pointer-events-none">
              <div className="bg-black/50 text-white text-xs rounded-lg px-3 py-2 flex items-center gap-2">
                <span>📍</span> Hacé clic en el mapa para marcar el tesoro
              </div>
            </div>
          )}

          <MapContainer
            center={DEFAULT_CENTER}
            zoom={14}
            style={{ height: '320px', width: '100%' }}
            className="z-0"
          >
            <TileLayer
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              attribution='&copy; <a href="https://www.openstreetmap.org/">OpenStreetMap</a>'
            />
            <LocationPicker onPick={handlePickLocation} disabled={disabled} />
            {position && (
              <>
                <Marker position={position} />
                <Circle
                  center={position}
                  radius={radius}
                  pathOptions={{ color: '#f59e0b', fillColor: '#fcd34d', fillOpacity: 0.25 }}
                />
              </>
            )}
          </MapContainer>

          {position ? (
            <div className="flex gap-4 px-3 py-1.5 bg-slate-50 border-t border-slate-100 text-xs font-mono text-slate-600">
              <span>📍 Lat: <strong className="text-slate-800">{position.lat.toFixed(6)}</strong></span>
              <span>Lon: <strong className="text-slate-800">{position.lng.toFixed(6)}</strong></span>
            </div>
          ) : (
            <div className="px-3 py-1.5 bg-slate-50 border-t border-slate-100 text-xs text-slate-400">
              Sin ubicación seleccionada
            </div>
          )}
        </div>

        {/* ── QR + controles ─────────────────────────────────────── */}
        <div className="flex flex-col gap-4 p-4 border-l border-slate-100 bg-slate-50">

          {/* Código QR */}
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-2">Código QR</p>
            <div className="flex flex-col items-center gap-2 bg-white rounded-xl border border-slate-200 p-3">
              <div ref={qrWrapperRef}>
                <QRCodeCanvas
                  value={qrCode}
                  size={140}
                  bgColor="#ffffff"
                  fgColor="#1e293b"
                  level="H"
                />
              </div>

              {/* Canvas oculto en alta resolución: solo se usa para exportar.
                  Se posiciona fuera de pantalla (no display:none, que impediría
                  pintar el canvas). includeMargin agrega el "quiet zone" que los
                  lectores necesitan para enfocar. */}
              <div
                ref={qrExportRef}
                aria-hidden="true"
                style={{ position: 'absolute', left: '-99999px', top: 0, pointerEvents: 'none' }}
              >
                <QRCodeCanvas
                  value={qrCode}
                  size={QR_EXPORT_SIZE}
                  bgColor="#ffffff"
                  fgColor="#1e293b"
                  level="H"
                  marginSize={4}
                />
              </div>
              <p className="text-[9px] font-mono text-slate-400 break-all text-center leading-relaxed">
                {qrCode}
              </p>
              <button
                type="button"
                onClick={handleDownloadQR}
                className="w-full flex items-center justify-center gap-1.5 text-xs bg-slate-800 hover:bg-slate-700 text-white py-1.5 rounded-lg transition-colors cursor-pointer"
              >
                ⬇ Descargar QR para imprimir
              </button>
            </div>
          </div>

          {/* Radio de búsqueda */}
          <div>
            <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-1">
              Radio de búsqueda: <span className="text-slate-800">{radius} m</span>
            </p>
            <input
              type="range"
              min={10}
              max={300}
              step={10}
              value={radius}
              onChange={e => setRadius(Number(e.target.value))}
              className="w-full accent-amber-500"
            />
            <div className="flex justify-between text-[9px] text-slate-400 mt-0.5">
              <span>10 m</span><span>300 m</span>
            </div>
          </div>

          {/* Checklist de validación */}
          <div className="text-xs space-y-1">
            <CheckItem done={position !== null} label="Ubicación marcada en el mapa" />
            <CheckItem done={true} label="Código QR generado" />
          </div>
        </div>
      </div>
    </div>
  );
}

// ── Ítem del checklist ────────────────────────────────────────────────────────
function CheckItem({ done, label }: { done: boolean; label: string }) {
  return (
    <div className={`flex items-center gap-2 ${done ? 'text-green-600' : 'text-slate-400'}`}>
      <span>{done ? '✅' : '⬜'}</span>
      <span>{label}</span>
    </div>
  );
}
