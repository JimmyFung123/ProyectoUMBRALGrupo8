import 'leaflet/dist/leaflet.css';

import L from 'leaflet';
import { useEffect, useState } from 'react';
import { Circle, MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet';
import { clueService } from '../../services/clueService';
import type { AddCluePayload, Clue, UpdateCluePayload } from '../../types/clue';

// ── Fix Leaflet marker icons broken by bundlers ──────────────────────────────
delete (L.Icon.Default.prototype as unknown as Record<string, unknown>)['_getIconUrl'];
L.Icon.Default.mergeOptions({
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
});

// ── Default map center: UCAB Caracas ─────────────────────────────────────────
const DEFAULT_CENTER: [number, number] = [10.4866, -66.8543];

// ── Types ─────────────────────────────────────────────────────────────────────

interface Props {
  missionId: string;
  stageId: string;
  stageType: 'Trivia' | 'TreasureHunt';
  isLocked: boolean;
}

interface ClueGeoData {
  latitude: number;
  longitude: number;
  radiusMeters: number;
}

// ── ClueTreasureForm ──────────────────────────────────────────────────────────

function MapClickHandler({ onPick }: { onPick: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

function ClueTreasureForm({
  onChange,
  initialLatitude,
  initialLongitude,
  initialRadiusMeters,
}: {
  onChange: (data: ClueGeoData | null) => void;
  initialLatitude?: number;
  initialLongitude?: number;
  initialRadiusMeters?: number;
}) {
  const [lat, setLat] = useState<number | null>(initialLatitude ?? null);
  const [lng, setLng] = useState<number | null>(initialLongitude ?? null);
  const [radius, setRadius] = useState(initialRadiusMeters ?? 100);

  function handlePick(pickedLat: number, pickedLng: number) {
    setLat(pickedLat);
    setLng(pickedLng);
    onChange({ latitude: pickedLat, longitude: pickedLng, radiusMeters: radius });
  }

  function handleRadiusChange(newRadius: number) {
    setRadius(newRadius);
    if (lat !== null && lng !== null) {
      onChange({ latitude: lat, longitude: lng, radiusMeters: newRadius });
    }
  }

  return (
    <div style={{ marginBottom: '0.5rem' }}>
      <label style={{ fontSize: '0.8rem', display: 'block', marginBottom: '0.3rem' }}>
        Ubicación <span style={{ color: '#888', fontWeight: 400 }}>(hacé clic en el mapa)</span>
      </label>

      <div style={{ borderRadius: 6, overflow: 'hidden', border: '1px solid #ddd', position: 'relative' }}>
        {!lat && (
          <div style={{
            position: 'absolute', inset: 0, zIndex: 10, display: 'flex',
            alignItems: 'center', justifyContent: 'center', pointerEvents: 'none',
          }}>
            <div style={{
              background: 'rgba(0,0,0,0.5)', color: '#fff', fontSize: '0.75rem',
              borderRadius: 6, padding: '0.3rem 0.6rem',
            }}>
              📍 Clic para marcar la pista
            </div>
          </div>
        )}
        <MapContainer
          center={
            lat !== null && lng !== null ? [lat, lng] : DEFAULT_CENTER
          }
          zoom={15}
          style={{ height: '240px', width: '100%' }}
        >
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/">OpenStreetMap</a>'
          />
          <MapClickHandler onPick={handlePick} />
          {lat !== null && lng !== null && (
            <>
              <Marker position={[lat, lng]} />
              <Circle
                center={[lat, lng]}
                radius={radius}
                pathOptions={{ color: '#7c3aed', fillColor: '#a78bfa', fillOpacity: 0.25 }}
              />
            </>
          )}
        </MapContainer>
      </div>

      <div style={{ marginTop: '0.4rem', fontSize: '0.78rem', color: '#555', fontFamily: 'monospace' }}>
        {lat !== null && lng !== null
          ? <>Lat: <strong>{lat.toFixed(6)}</strong> &nbsp; Lon: <strong>{lng.toFixed(6)}</strong></>
          : <span style={{ color: '#aaa' }}>Sin ubicación seleccionada</span>
        }
      </div>

      <div style={{ marginTop: '0.5rem' }}>
        <label style={{ fontSize: '0.8rem' }}>
          Radio de detección: <strong>{radius} m</strong>
        </label>
        <input
          type="range"
          min={10}
          max={500}
          step={10}
          value={radius}
          onChange={e => handleRadiusChange(Number(e.target.value))}
          style={{ display: 'block', width: '100%', accentColor: '#7c3aed' }}
        />
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.7rem', color: '#aaa' }}>
          <span>10 m</span><span>500 m</span>
        </div>
      </div>
    </div>
  );
}

// ── ClueManager ───────────────────────────────────────────────────────────────

export function ClueManager({ missionId, stageId, stageType, isLocked }: Props) {
  const [clues, setClues] = useState<Clue[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [editingId, setEditingId] = useState<string | null>(null);

  // Add form state
  const [addOrder, setAddOrder] = useState(1);
  const [addContent, setAddContent] = useState('');
  const [addGeo, setAddGeo] = useState<ClueGeoData | null>(null);
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    clueService.getClues(missionId, stageId)
      .then(data => {
        setClues(data);
        setAddOrder(data.length + 1);
      })
      .catch(err => setLoadError(err?.message ?? 'Error al cargar las pistas.'))
      .finally(() => setLoading(false));
  }, [missionId, stageId]);

  function refreshClues() {
    clueService.getClues(missionId, stageId).then(data => {
      setClues(data);
      setAddOrder(data.length + 1);
    });
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setAddError(null);
    setAdding(true);

    const payload: AddCluePayload = { order: addOrder };
    if (stageType === 'Trivia') {
      payload.content = addContent;
    } else if (addGeo) {
      payload.latitude = addGeo.latitude;
      payload.longitude = addGeo.longitude;
      payload.radiusMeters = addGeo.radiusMeters;
    }

    try {
      await clueService.addClue(missionId, stageId, payload);
      setAddContent('');
      setAddGeo(null);
      refreshClues();
    } catch (err) {
      setAddError((err as { message?: string })?.message ?? 'No se pudo agregar la pista.');
    } finally {
      setAdding(false);
    }
  }

  async function handleDelete(clue: Clue) {
    if (!confirm(`¿Eliminar la pista ${clue.order}?`)) return;
    try {
      await clueService.deleteClue(missionId, stageId, clue.id);
      refreshClues();
    } catch (err) {
      alert((err as { message?: string })?.message ?? 'Error al eliminar.');
    }
  }

  const canAdd = stageType === 'Trivia'
    ? addContent.trim().length > 0
    : addGeo !== null;

  if (loading) {
    return <p style={{ fontSize: '0.82rem', color: '#888', padding: '0.5rem 0' }}>Cargando pistas…</p>;
  }

  if (loadError) {
    return <p style={{ fontSize: '0.82rem', color: 'red', padding: '0.5rem 0' }}>{loadError}</p>;
  }

  return (
    <div style={{ padding: '0.75rem', background: '#f5f3ff', borderRadius: 6, border: '1px solid #ddd6fe', marginTop: '0.5rem' }}>
      <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#7c3aed', fontSize: '0.9rem' }}>
        🗝️ Pistas
      </strong>

      {/* ── Clue list ────────────────────────────────────────── */}
      {clues.length === 0 && (
        <p style={{ fontSize: '0.82rem', color: '#a78bfa', marginBottom: '0.5rem' }}>Sin pistas aún.</p>
      )}

      <ul style={{ listStyle: 'none', padding: 0, margin: '0 0 0.75rem' }}>
        {clues.map(clue => (
          <li key={clue.id} style={{ marginBottom: '0.4rem', border: '1px solid #ddd6fe', borderRadius: 6, overflow: 'hidden' }}>
            {editingId === clue.id ? (
              <ClueEditForm
                clue={clue}
                stageType={stageType}
                missionId={missionId}
                stageId={stageId}
                onDone={() => { setEditingId(null); refreshClues(); }}
              />
            ) : (
              <ClueRow
                clue={clue}
                stageType={stageType}
                isLocked={isLocked}
                onEdit={() => setEditingId(clue.id)}
                onDelete={() => handleDelete(clue)}
              />
            )}
          </li>
        ))}
      </ul>

      {/* ── Add form ─────────────────────────────────────────── */}
      {!isLocked && (
        <form onSubmit={handleAdd} style={{ background: '#ede9fe', borderRadius: 6, padding: '0.75rem', border: '1px dashed #c4b5fd' }}>
          <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#6d28d9', fontSize: '0.85rem' }}>
            + Agregar pista
          </strong>

          <div style={{ marginBottom: '0.5rem' }}>
            <label style={{ fontSize: '0.8rem' }}>Orden</label>
            <input
              type="number"
              min={1}
              value={addOrder}
              onChange={e => setAddOrder(Number(e.target.value))}
              required
              style={{ display: 'block', width: '5rem', padding: '0.3rem' }}
            />
          </div>

          {stageType === 'Trivia' && (
            <div style={{ marginBottom: '0.5rem' }}>
              <label style={{ fontSize: '0.8rem' }}>Contenido de la pista</label>
              <textarea
                value={addContent}
                onChange={e => setAddContent(e.target.value)}
                required
                rows={3}
                style={{ display: 'block', width: '100%', padding: '0.3rem', resize: 'vertical', boxSizing: 'border-box' }}
              />
            </div>
          )}

          {stageType === 'TreasureHunt' && (
            <ClueTreasureForm onChange={setAddGeo} />
          )}

          {addError && (
            <p style={{ color: 'red', fontSize: '0.82rem', margin: '0.3rem 0' }}>{addError}</p>
          )}

          <button type="submit" disabled={adding || !canAdd} style={{ marginTop: '0.25rem' }}>
            {adding ? 'Agregando…' : 'Agregar pista'}
          </button>
        </form>
      )}
    </div>
  );
}

// ── ClueRow ───────────────────────────────────────────────────────────────────

function ClueRow({
  clue,
  stageType,
  isLocked,
  onEdit,
  onDelete,
}: {
  clue: Clue;
  stageType: 'Trivia' | 'TreasureHunt';
  isLocked: boolean;
  onEdit: () => void;
  onDelete: () => void;
}) {
  return (
    <div style={{
      padding: '0.5rem 0.75rem',
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'flex-start',
      background: '#faf5ff',
    }}>
      <div style={{ flex: 1 }}>
        <span style={{ fontWeight: 600, fontSize: '0.85rem', color: '#7c3aed' }}>
          Pista {clue.order}
        </span>
        <span style={{ marginLeft: '0.6rem', fontSize: '0.82rem', color: '#555' }}>
          {stageType === 'Trivia'
            ? (clue.content ?? <em style={{ color: '#aaa' }}>Sin contenido</em>)
            : (clue.latitude !== null
              ? `Lat: ${clue.latitude.toFixed(6)}, Lon: ${clue.longitude?.toFixed(6)}, Radio: ${clue.radiusMeters}m`
              : <em style={{ color: '#aaa' }}>Sin ubicación</em>)
          }
        </span>
      </div>
      {!isLocked && (
        <div style={{ display: 'flex', gap: '0.35rem', marginLeft: '0.5rem', flexShrink: 0 }}>
          <button type="button" onClick={onEdit}
            style={{ fontSize: '0.72rem', padding: '0.2rem 0.45rem' }}>
            ✏️ Editar
          </button>
          <button type="button" onClick={onDelete}
            style={{ fontSize: '0.72rem', padding: '0.2rem 0.45rem', color: '#c62828' }}>
            🗑️ Eliminar
          </button>
        </div>
      )}
    </div>
  );
}

// ── ClueEditForm ──────────────────────────────────────────────────────────────

function ClueEditForm({
  clue,
  stageType,
  missionId,
  stageId,
  onDone,
}: {
  clue: Clue;
  stageType: 'Trivia' | 'TreasureHunt';
  missionId: string;
  stageId: string;
  onDone: () => void;
}) {
  const [order, setOrder] = useState(clue.order);
  const [content, setContent] = useState(clue.content ?? '');
  const [geo, setGeo] = useState<ClueGeoData | null>(
    clue.latitude !== null && clue.longitude !== null && clue.radiusMeters !== null
      ? { latitude: clue.latitude, longitude: clue.longitude, radiusMeters: clue.radiusMeters }
      : null
  );
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSave = stageType === 'Trivia'
    ? content.trim().length > 0
    : geo !== null;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);

    const payload: UpdateCluePayload = { order };
    if (stageType === 'Trivia') {
      payload.content = content;
    } else if (geo) {
      payload.latitude = geo.latitude;
      payload.longitude = geo.longitude;
      payload.radiusMeters = geo.radiusMeters;
    }

    try {
      await clueService.updateClue(missionId, stageId, clue.id, payload);
      onDone();
    } catch (err) {
      setError((err as { message?: string })?.message ?? 'No se pudo guardar la pista.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ padding: '0.75rem', background: '#ede9fe' }}>
      <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#6d28d9', fontSize: '0.85rem' }}>
        ✏️ Editando pista {clue.order}
      </strong>

      <div style={{ marginBottom: '0.5rem' }}>
        <label style={{ fontSize: '0.8rem' }}>Orden</label>
        <input
          type="number"
          min={1}
          value={order}
          onChange={e => setOrder(Number(e.target.value))}
          required
          style={{ display: 'block', width: '5rem', padding: '0.3rem' }}
        />
      </div>

      {stageType === 'Trivia' && (
        <div style={{ marginBottom: '0.5rem' }}>
          <label style={{ fontSize: '0.8rem' }}>Contenido de la pista</label>
          <textarea
            value={content}
            onChange={e => setContent(e.target.value)}
            required
            rows={3}
            style={{ display: 'block', width: '100%', padding: '0.3rem', resize: 'vertical', boxSizing: 'border-box' }}
          />
        </div>
      )}

      {stageType === 'TreasureHunt' && (
        <ClueTreasureForm
          onChange={setGeo}
          initialLatitude={clue.latitude ?? undefined}
          initialLongitude={clue.longitude ?? undefined}
          initialRadiusMeters={clue.radiusMeters ?? undefined}
        />
      )}

      {error && <p style={{ color: 'red', fontSize: '0.82rem', margin: '0.3rem 0' }}>{error}</p>}

      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
        <button type="submit" disabled={saving || !canSave}>
          {saving ? 'Guardando…' : '💾 Guardar'}
        </button>
        <button type="button" onClick={onDone} style={{ background: '#eee', border: '1px solid #ccc' }}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
