import 'leaflet/dist/leaflet.css';

import L, { type LatLng } from 'leaflet';
import { useState } from 'react';
import { Circle, MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet';
import { QrCodeDownload } from './QrCodeDownload';

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

  function handlePickLocation(ll: LatLng) {
    setPosition(ll);
    onChange({ latitude: ll.lat, longitude: ll.lng, qrCode });
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
            <QrCodeDownload value={qrCode} />
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
