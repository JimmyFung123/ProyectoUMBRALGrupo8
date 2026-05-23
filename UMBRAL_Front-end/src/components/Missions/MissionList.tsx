import { useEffect, useState } from 'react';
import { missionService } from '../../services/missionService';
import { stageService } from '../../services/stageService';
import {
  DIFFICULTY_LABELS,
  type ApiError,
  type CreateMissionPayload,
  type DifficultyLevel,
  type Mission,
  type UpdateMissionPayload,
} from '../../types/mission';
import type { Stage } from '../../types/stage';
import { StageManager } from './StageManager';

const DIFFICULTY_OPTIONS: DifficultyLevel[] = ['Easy', 'Medium', 'Hard'];

const STATUS_LABELS: Record<string, string> = {
  Active: 'Activa',
  Inactive: 'Inactiva',
};

const initialCreateForm: CreateMissionPayload = {
  name: '',
  description: '',
  difficulty: 'Easy',
  maxDuration: 30,
};

// ── MissionList ───────────────────────────────────────────────────────────────

export function MissionList() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createForm, setCreateForm] = useState<CreateMissionPayload>(initialCreateForm);
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [statusError, setStatusError] = useState<Record<string, string>>({});

  // Qué paneles de misión están expandidos
  const [expandedId, setExpandedId] = useState<string | null>(null);
  // Etapas por ID de misión (cargadas desde StageService)
  const [stagesByMission, setStagesByMission] = useState<Record<string, Stage[]>>({});
  const [stagesLoading, setStagesLoading] = useState<Record<string, boolean>>({});

  // Qué misión se está editando
  const [editingMissionId, setEditingMissionId] = useState<string | null>(null);

  useEffect(() => { loadMissions(); }, []);

  async function loadMissions() {
    setLoading(true);
    setError(null);
    try {
      setMissions(await missionService.getAll());
    } catch {
      setError('No se pudieron cargar las misiones. Intentá de nuevo.');
    } finally {
      setLoading(false);
    }
  }

  async function loadStages(missionId: string) {
    setStagesLoading(prev => ({ ...prev, [missionId]: true }));
    try {
      const stages = await stageService.getByMission(missionId);
      setStagesByMission(prev => ({ ...prev, [missionId]: stages }));
    } finally {
      setStagesLoading(prev => ({ ...prev, [missionId]: false }));
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setCreateError(null);
    setCreating(true);
    try {
      await missionService.create(createForm);
      setCreateForm(initialCreateForm);
      await loadMissions();
    } catch (err) {
      setCreateError((err as ApiError)?.message ?? 'No se pudo crear la misión.');
    } finally {
      setCreating(false);
    }
  }

  async function handleToggleStatus(mission: Mission) {
    const activate = mission.status === 'Inactive';
    setStatusError(prev => ({ ...prev, [mission.id]: '' }));
    try {
      await missionService.changeStatus(mission.id, activate);
      await loadMissions();
      // Refresh detail if this mission is expanded
      if (expandedId === mission.id) await loadStages(mission.id);
    } catch (err) {
      setStatusError(prev => ({
        ...prev,
        [mission.id]: (err as ApiError)?.message ?? 'No se pudo cambiar el estado.',
      }));
    }
  }

  function toggleExpand(missionId: string) {
    if (expandedId === missionId) {
      setExpandedId(null);
    } else {
      setExpandedId(missionId);
      loadStages(missionId);
    }
  }

  async function handleStageChanged(missionId: string) {
    await Promise.all([loadMissions(), loadStages(missionId)]);
  }

  // ── Renderizado ───────────────────────────────────────────────────────────

  return (
    <div style={{ maxWidth: 860, margin: '0 auto', padding: '2rem', fontFamily: 'sans-serif' }}>
      <h1>Misiones</h1>

      {/* ── Formulario de creación ─────────────────────────────── */}
      <section style={{ marginBottom: '2rem', padding: '1rem', border: '1px solid #ddd', borderRadius: 8 }}>
        <h2 style={{ marginTop: 0 }}>Nueva misión</h2>
        <form onSubmit={handleCreate}>
          <div style={{ marginBottom: '0.75rem' }}>
            <label>Nombre</label>
            <input value={createForm.name} onChange={e => setCreateForm(f => ({ ...f, name: e.target.value }))} required
              style={{ display: 'block', width: '100%', padding: '0.4rem' }} />
          </div>
          <div style={{ marginBottom: '0.75rem' }}>
            <label>Descripción</label>
            <textarea value={createForm.description} onChange={e => setCreateForm(f => ({ ...f, description: e.target.value }))} rows={2}
              style={{ display: 'block', width: '100%', padding: '0.4rem' }} />
          </div>
          <div style={{ display: 'flex', gap: '1rem', marginBottom: '0.75rem' }}>
            <div>
              <label>Dificultad</label>
              <select value={createForm.difficulty} onChange={e => setCreateForm(f => ({ ...f, difficulty: e.target.value as DifficultyLevel }))}
                style={{ display: 'block', padding: '0.4rem' }}>
                {DIFFICULTY_OPTIONS.map(d => <option key={d} value={d}>{DIFFICULTY_LABELS[d]}</option>)}
              </select>
            </div>
            <div>
              <label>Duración máxima (min)</label>
              <input type="number" min={1} value={createForm.maxDuration}
                onChange={e => setCreateForm(f => ({ ...f, maxDuration: Number(e.target.value) }))} required
                style={{ display: 'block', padding: '0.4rem', width: '90px' }} />
            </div>
          </div>
          {createError && <p style={{ color: 'red', margin: '0.4rem 0' }}>{createError}</p>}
          <button type="submit" disabled={creating}>{creating ? 'Creando…' : 'Crear misión'}</button>
        </form>
      </section>

      {/* ── Lista ─────────────────────────────────────────────── */}
      {loading && <p>Cargando misiones…</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loading && !error && missions.length === 0 && <p>Todavía no hay misiones.</p>}

      <ul style={{ listStyle: 'none', padding: 0 }}>
        {missions.map(mission => (
          <li key={mission.id} style={{ marginBottom: '0.75rem', border: '1px solid #ddd', borderRadius: 8, overflow: 'hidden' }}>

            {/* Cabecera */}
            <div style={{ padding: '0.9rem 1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div style={{ flex: 1 }}>
                <strong>{mission.name}</strong>
                <span style={{
                  marginLeft: '0.5rem', padding: '0.1rem 0.5rem', borderRadius: 4, fontSize: '0.75rem',
                  background: mission.status === 'Active' ? '#d4edda' : '#f8d7da',
                  color: mission.status === 'Active' ? '#155724' : '#721c24',
                }}>
                  {STATUS_LABELS[mission.status] ?? mission.status}
                </span>
                <p style={{ margin: '0.2rem 0', color: '#555', fontSize: '0.9rem' }}>{mission.description}</p>
                <small style={{ color: '#888' }}>
                  {DIFFICULTY_LABELS[mission.difficulty]} · {mission.maxDuration} min · {mission.stageCount} etapa{mission.stageCount !== 1 ? 's' : ''}
                </small>
                {statusError[mission.id] && (
                  <p style={{ color: 'red', margin: '0.2rem 0', fontSize: '0.85rem' }}>{statusError[mission.id]}</p>
                )}
              </div>

              {/* Botones de acción */}
              <div style={{ display: 'flex', gap: '0.4rem', marginLeft: '1rem', flexShrink: 0, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                <button onClick={() => toggleExpand(mission.id)} style={{ fontSize: '0.85rem' }}>
                  {expandedId === mission.id ? '▲ Etapas' : '▼ Etapas'}
                </button>
                <button
                  onClick={() => setEditingMissionId(editingMissionId === mission.id ? null : mission.id)}
                  disabled={mission.status === 'Active'}
                  title={mission.status === 'Active' ? 'Desactivá la misión para editarla' : undefined}
                  style={{ fontSize: '0.85rem', opacity: mission.status === 'Active' ? 0.45 : 1, cursor: mission.status === 'Active' ? 'not-allowed' : 'pointer' }}
                >
                  {editingMissionId === mission.id ? 'Cancelar edición' : '✏️ Editar'}
                </button>
                <button onClick={() => handleToggleStatus(mission)}>
                  {mission.status === 'Active' ? 'Desactivar' : 'Activar'}
                </button>
              </div>
            </div>

            {/* Formulario de edición de misión */}
            {editingMissionId === mission.id && (
              <div style={{ borderTop: '1px solid #eee', padding: '0.75rem 1rem', background: '#fffde7' }}>
                <MissionEditForm
                  mission={mission}
                  onSaved={async () => { setEditingMissionId(null); await loadMissions(); }}
                  onCancel={() => setEditingMissionId(null)}
                />
              </div>
            )}

            {/* Panel de etapas */}
            {expandedId === mission.id && (
              <div style={{ borderTop: '1px solid #eee', padding: '0.75rem 1rem', background: '#fafafa' }}>
                <strong style={{ fontSize: '0.9rem' }}>Etapas</strong>

                {stagesLoading[mission.id] ? (
                  <p style={{ color: '#888', fontSize: '0.85rem' }}>Cargando etapas…</p>
                ) : (
                  <StageManager
                    mission={mission}
                    stages={stagesByMission[mission.id] ?? []}
                    onChanged={() => handleStageChanged(mission.id)}
                  />
                )}
              </div>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

// ── MissionEditForm ───────────────────────────────────────────────────────────

function MissionEditForm({ mission, onSaved, onCancel }: {
  mission: Mission;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [form, setForm] = useState<UpdateMissionPayload>({
    name: mission.name,
    description: mission.description,
    difficulty: mission.difficulty,
    maxDuration: mission.maxDuration,
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      await missionService.update(mission.id, form);
      onSaved();
    } catch (err) {
      setError((err as ApiError)?.message ?? 'No se pudo actualizar la misión.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit}>
      <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#f57f17' }}>✏️ Editar misión</strong>

      <div style={{ marginBottom: '0.5rem' }}>
        <label style={{ fontSize: '0.85rem' }}>Nombre</label>
        <input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} required
          style={{ display: 'block', width: '100%', padding: '0.4rem' }} />
      </div>
      <div style={{ marginBottom: '0.5rem' }}>
        <label style={{ fontSize: '0.85rem' }}>Descripción</label>
        <textarea value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))} rows={2}
          style={{ display: 'block', width: '100%', padding: '0.4rem' }} />
      </div>
      <div style={{ display: 'flex', gap: '1rem', marginBottom: '0.5rem' }}>
        <div>
          <label style={{ fontSize: '0.85rem' }}>Dificultad</label>
          <select value={form.difficulty} onChange={e => setForm(f => ({ ...f, difficulty: e.target.value as DifficultyLevel }))}
            style={{ display: 'block', padding: '0.4rem' }}>
            {DIFFICULTY_OPTIONS.map(d => <option key={d} value={d}>{DIFFICULTY_LABELS[d]}</option>)}
          </select>
        </div>
        <div>
          <label style={{ fontSize: '0.85rem' }}>Duración máxima (min)</label>
          <input type="number" min={1} value={form.maxDuration}
            onChange={e => setForm(f => ({ ...f, maxDuration: Number(e.target.value) }))} required
            style={{ display: 'block', padding: '0.4rem', width: '90px' }} />
        </div>
      </div>

      {error && <p style={{ color: 'red', fontSize: '0.85rem', margin: '0.3rem 0' }}>{error}</p>}

      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.5rem' }}>
        <button type="submit" disabled={saving}>{saving ? 'Guardando…' : '💾 Guardar'}</button>
        <button type="button" onClick={onCancel} style={{ background: '#eee', border: '1px solid #ccc' }}>Cancelar</button>
      </div>
    </form>
  );
}

