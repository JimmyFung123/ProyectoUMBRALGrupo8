import { useState } from 'react';
import { stageService } from '../../services/stageService';
import { autoReleaseService } from '../../services/autoReleaseService';
import type { AddStagePayload, ApiError, StageType, UpdateStagePayload } from '../../types/stage';
import type { Mission, StageDetail } from '../../types/mission';
import { TreasureHuntConfig, type TreasureHuntData } from './TreasureHuntConfig';
import { ClueManager } from './ClueManager';

interface Props {
  mission: Mission;
  stages: StageDetail[];
  onChanged: () => void;
}

// ── Utilidades compartidas ────────────────────────────────────────────────────

const STAGE_TYPE_LABELS: Record<string, string> = {
  Trivia: 'Trivia',
  TreasureHunt: 'Búsqueda del Tesoro',
};

const emptyAdd: AddStagePayload = {
  title: '',
  order: 1,
  type: 'Trivia',
  baseScore: 100,
  question: '',
  options: [
    { text: '', isCorrect: false },
    { text: '', isCorrect: false },
  ],
};

// ── StageManager ──────────────────────────────────────────────────────────────

export function StageManager({ mission, stages, onChanged }: Props) {
  const isLocked = mission.status === 'Active';

  const [editingId, setEditingId] = useState<string | null>(null);
  const [addForm, setAddForm] = useState<AddStagePayload>({ ...emptyAdd, order: stages.length + 1 });
  const [addError, setAddError] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [deleteError, setDeleteError] = useState<Record<string, string>>({});

  // ── Manejadores del formulario de agregar ─────────────────────────────────

  function setAddField<K extends keyof AddStagePayload>(key: K, value: AddStagePayload[K]) {
    setAddForm(f => ({ ...f, [key]: value }));
  }

  function handleAddTypeChange(type: StageType) {
    setAddForm(f => ({ ...f, type, latitude: undefined, longitude: undefined, qrCode: '' }));
  }

  function handleAddTreasureChange(data: TreasureHuntData | null) {
    if (data) setAddForm(f => ({ ...f, latitude: data.latitude, longitude: data.longitude, qrCode: data.qrCode }));
    else setAddForm(f => ({ ...f, latitude: undefined, longitude: undefined, qrCode: '' }));
  }

  function addOption() {
    setAddForm(f => ({ ...f, options: [...(f.options ?? []), { text: '', isCorrect: false }] }));
  }

  function removeOption(i: number) {
    setAddForm(f => ({ ...f, options: f.options?.filter((_, idx) => idx !== i) }));
  }

  function setOptionText(i: number, text: string) {
    setAddForm(f => {
      const options = [...(f.options ?? [])];
      options[i] = { ...options[i], text };
      return { ...f, options };
    });
  }

  function setCorrectOption(i: number) {
    setAddForm(f => ({ ...f, options: f.options?.map((o, idx) => ({ ...o, isCorrect: idx === i })) }));
  }

  async function handleAdd(e: React.FormEvent) {
    e.preventDefault();
    setAddError(null);
    setAdding(true);
    const payload: AddStagePayload = {
      title: addForm.title,
      order: addForm.order,
      type: addForm.type,
      baseScore: addForm.baseScore,
      ...(addForm.type === 'Trivia'
        ? { question: addForm.question, options: addForm.options }
        : { latitude: addForm.latitude, longitude: addForm.longitude, qrCode: addForm.qrCode }),
    };
    try {
      await stageService.addStage(mission.id, payload);
      setAddForm({ ...emptyAdd, order: stages.length + 2 });
      onChanged();
    } catch (err) {
      setAddError((err as ApiError)?.message ?? 'No se pudo agregar la etapa.');
    } finally {
      setAdding(false);
    }
  }

  // ── Manejador de eliminación ───────────────────────────────────────────────

  async function handleDelete(stage: StageDetail) {
    if (!confirm(`¿Eliminar la etapa "${stage.title}"?`)) return;
    setDeleteError(prev => ({ ...prev, [stage.id]: '' }));
    try {
      await stageService.removeStage(mission.id, stage.id);
      onChanged();
    } catch (err) {
      setDeleteError(prev => ({ ...prev, [stage.id]: (err as ApiError)?.message ?? 'Error al eliminar.' }));
    }
  }

  // ── Estado bloqueado ───────────────────────────────────────────────────────

  if (isLocked) {
    return (
      <>
        {stages.length > 0 && <StageList stages={stages} missionId={mission.id} isLocked onChanged={onChanged} />}
        <p style={{ color: '#856404', background: '#fff3cd', padding: '0.5rem 0.75rem', borderRadius: 4, fontSize: '0.85rem', marginTop: '0.5rem' }}>
          Esta misión está <strong>Activa</strong>. Desactivala para modificar las etapas.
        </p>
      </>
    );
  }

  const addTreasureReady =
    addForm.type === 'TreasureHunt' &&
    addForm.latitude !== undefined &&
    addForm.longitude !== undefined &&
    !!addForm.qrCode;

  const canAdd = addForm.type === 'Trivia' || addTreasureReady;

  return (
    <div>
      {/* ── Etapas existentes ───────────────────────────────────── */}
      {stages.length > 0 && (
        <StageList
          stages={stages}
          missionId={mission.id}
          isLocked={false}
          editingId={editingId}
          onEditStart={setEditingId}
          onEditDone={() => { setEditingId(null); onChanged(); }}
          onDelete={handleDelete}
          deleteError={deleteError}
          onChanged={onChanged}
        />
      )}

      {/* ── Formulario para agregar etapa ──────────────────────── */}
      <form onSubmit={handleAdd} style={{ marginTop: '0.75rem', padding: '0.75rem', background: '#f0f4ff', borderRadius: 6, border: '1px dashed #c5cae9' }}>
        <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#3949ab' }}>+ Agregar etapa</strong>

        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
          <div style={{ flex: 3 }}>
            <label style={{ fontSize: '0.8rem' }}>Título</label>
            <input value={addForm.title} onChange={e => setAddField('title', e.target.value)} required
              style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
          </div>
          <div style={{ flex: 1 }}>
            <label style={{ fontSize: '0.8rem' }}>Orden</label>
            <input type="number" min={1} value={addForm.order} onChange={e => setAddField('order', Number(e.target.value))} required
              style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
          </div>
          <div style={{ flex: 1 }}>
            <label style={{ fontSize: '0.8rem' }}>Puntaje</label>
            <input type="number" min={0} value={addForm.baseScore} onChange={e => setAddField('baseScore', Number(e.target.value))} required
              style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
          </div>
        </div>

        <div style={{ marginBottom: '0.5rem' }}>
          <label style={{ fontSize: '0.8rem' }}>Tipo de etapa</label>
          <select value={addForm.type} onChange={e => handleAddTypeChange(e.target.value as StageType)}
            style={{ display: 'block', padding: '0.35rem' }}>
            <option value="Trivia">Trivia</option>
            <option value="TreasureHunt">Búsqueda del Tesoro</option>
          </select>
        </div>

        {addForm.type === 'Trivia' && (
          <>
            <div style={{ marginBottom: '0.5rem' }}>
              <label style={{ fontSize: '0.8rem' }}>Pregunta</label>
              <input value={addForm.question ?? ''} onChange={e => setAddField('question', e.target.value)}
                style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
            </div>
            <div style={{ marginBottom: '0.5rem' }}>
              <label style={{ fontSize: '0.8rem' }}>Opciones <span style={{ color: '#888' }}>(marcá la correcta)</span></label>
              {addForm.options?.map((opt, i) => (
                <div key={i} style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', marginTop: '0.3rem' }}>
                  <input type="radio" name="addCorrect" checked={opt.isCorrect} onChange={() => setCorrectOption(i)} title="Correcta" />
                  <input value={opt.text} onChange={e => setOptionText(i, e.target.value)}
                    placeholder={`Opción ${i + 1}`} style={{ flex: 1, padding: '0.3rem' }} />
                  {(addForm.options?.length ?? 0) > 2 && (
                    <button type="button" onClick={() => removeOption(i)}
                      style={{ color: 'red', background: 'none', border: 'none', cursor: 'pointer' }}>✕</button>
                  )}
                </div>
              ))}
              <button type="button" onClick={addOption} style={{ marginTop: '0.4rem', fontSize: '0.8rem' }}>
                + Agregar opción
              </button>
            </div>
          </>
        )}

        {addForm.type === 'TreasureHunt' && (
          <TreasureHuntConfig onChange={handleAddTreasureChange} />
        )}

        {addError && <p style={{ color: 'red', fontSize: '0.85rem', margin: '0.4rem 0' }}>{addError}</p>}

        <button type="submit" disabled={adding || !canAdd} style={{ marginTop: '0.75rem' }}>
          {adding ? 'Agregando…' : 'Agregar etapa'}
        </button>
      </form>
    </div>
  );
}

// ── StageList ─────────────────────────────────────────────────────────────────

interface StageListProps {
  stages: StageDetail[];
  missionId: string;
  isLocked: boolean;
  editingId?: string | null;
  onEditStart?: (id: string) => void;
  onEditDone?: () => void;
  onDelete?: (stage: StageDetail) => void;
  deleteError?: Record<string, string>;
  onChanged: () => void;
}

function StageList({ stages, missionId, isLocked, editingId, onEditStart, onEditDone, onDelete, deleteError, onChanged }: StageListProps) {
  return (
    <ul style={{ listStyle: 'none', padding: 0, margin: '0.5rem 0 0' }}>
      {stages.map(stage => (
        <li key={stage.id} style={{ marginBottom: '0.5rem', border: '1px solid #e0e0e0', borderRadius: 6, overflow: 'hidden' }}>
          {editingId === stage.id ? (
            <StageEditForm stage={stage} missionId={missionId} onDone={onEditDone!} />
          ) : (
            <StageRow
              stage={stage}
              missionId={missionId}
              isLocked={isLocked}
              onEdit={() => onEditStart?.(stage.id)}
              onDelete={() => onDelete?.(stage)}
              deleteError={deleteError?.[stage.id]}
              onChanged={onChanged}
            />
          )}
        </li>
      ))}
    </ul>
  );
}

// ── StageRow ─────────────────────────────────────────────────────────────────

function StageRow({ stage, missionId, isLocked, onEdit, onDelete, deleteError, onChanged }: {
  stage: StageDetail;
  missionId: string;
  isLocked: boolean;
  onEdit: () => void;
  onDelete: () => void;
  deleteError?: string;
  onChanged: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [showClues, setShowClues] = useState(false);
  const [showAutoRelease, setShowAutoRelease] = useState(false);

  const autoReleaseLabel = (() => {
    const parts: string[] = [];
    if (stage.autoReleaseTimeMinutes != null) parts.push(`cada ${stage.autoReleaseTimeMinutes} min`);
    if (stage.autoReleaseMaxAttempts != null) parts.push(`max ${stage.autoReleaseMaxAttempts} intentos`);
    return parts.length > 0 ? parts.join(' · ') : null;
  })();

  return (
    <div>
      <div style={{ padding: '0.6rem 0.75rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#fafafa' }}>
        <div style={{ flex: 1 }}>
          <span style={{ fontWeight: 600, fontSize: '0.9rem' }}>{stage.order}. {stage.title}</span>
          <span style={{
            marginLeft: '0.5rem', fontSize: '0.7rem', padding: '0.1rem 0.4rem', borderRadius: 4,
            background: stage.type === 'Trivia' ? '#e3f2fd' : '#fff8e1',
            color: stage.type === 'Trivia' ? '#1565c0' : '#f57f17',
          }}>
            {STAGE_TYPE_LABELS[stage.type]}
          </span>
          <span style={{ marginLeft: '0.4rem', fontSize: '0.75rem', color: '#888' }}>{stage.baseScore} ptos.</span>
        </div>
        <div style={{ display: 'flex', gap: '0.4rem' }}>
          <button type="button" onClick={() => setShowClues(s => !s)}
            style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}>
            🗝️ Pistas {showClues ? '▲' : ''}
          </button>
          <button type="button" onClick={() => setShowAutoRelease(s => !s)}
            style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}>
            ⚙️ Regla {showAutoRelease ? '▲' : ''}
          </button>
          <button type="button" onClick={() => setExpanded(e => !e)}
            style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}>
            {expanded ? '▲' : '▼'} Datos
          </button>
          {!isLocked && (
            <>
              <button type="button" onClick={onEdit}
                style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}>✏️ Editar</button>
              <button type="button" onClick={onDelete}
                style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', color: '#c62828' }}>🗑️ Eliminar</button>
            </>
          )}
        </div>
      </div>

      {deleteError && (
        <p style={{ color: 'red', fontSize: '0.8rem', margin: '0 0.75rem 0.4rem', padding: '0.3rem 0.5rem', background: '#ffebee', borderRadius: 4 }}>
          {deleteError}
        </p>
      )}

      {showClues && (
        <div style={{ padding: '0 0.75rem' }}>
          <ClueManager
            missionId={missionId}
            stageId={stage.id}
            stageType={stage.type}
            isLocked={isLocked}
          />
        </div>
      )}

      {showAutoRelease && (
        <div style={{ padding: '0 0.75rem 0.5rem' }}>
          <AutoRulePanel
            missionId={missionId}
            stageId={stage.id}
            stageTitle={stage.title}
            currentTimeMinutes={stage.autoReleaseTimeMinutes}
            currentMaxAttempts={stage.autoReleaseMaxAttempts}
            isLocked={isLocked}
            onChanged={onChanged}
          />
        </div>
      )}

      {expanded && (
        <div style={{ padding: '0.5rem 0.75rem', borderTop: '1px solid #eee', fontSize: '0.85rem', background: '#fff' }}>
          {stage.type === 'Trivia' && (
            <>
              <p><strong>Pregunta:</strong> {stage.question || <em style={{ color: '#aaa' }}>Sin pregunta</em>}</p>
              {stage.options.length > 0 && (
                <ul style={{ margin: '0.3rem 0 0 1rem', padding: 0 }}>
                  {stage.options.map(opt => (
                    <li key={opt.id} style={{ color: opt.isCorrect ? '#2e7d32' : '#555' }}>
                      {opt.isCorrect ? '✅ ' : '• '}{opt.text}
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
          {stage.type === 'TreasureHunt' && (
            <>
              <p><strong>Latitud:</strong> {stage.latitude?.toFixed(6)}</p>
              <p><strong>Longitud:</strong> {stage.longitude?.toFixed(6)}</p>
              <p style={{ wordBreak: 'break-all' }}><strong>QR:</strong> <code style={{ fontSize: '0.75rem' }}>{stage.qrCode}</code></p>
            </>
          )}
          {autoReleaseLabel && (
            <p style={{ marginTop: '0.4rem', color: '#5c35a8', fontSize: '0.82rem' }}>
              ⚙️ Auto-liberación: {autoReleaseLabel}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

// ── AutoRulePanel ─────────────────────────────────────────────────────────────

interface AutoRulePanelProps {
  missionId: string;
  stageId: string;
  stageTitle: string;
  currentTimeMinutes: number | null;
  currentMaxAttempts: number | null;
  isLocked: boolean;
  onChanged: () => void;
}

function AutoRulePanel({
  missionId,
  stageId,
  currentTimeMinutes,
  currentMaxAttempts,
  isLocked,
  onChanged,
}: AutoRulePanelProps) {
  const [timeMinutes, setTimeMinutes] = useState(currentTimeMinutes != null ? String(currentTimeMinutes) : '');
  const [maxAttempts, setMaxAttempts] = useState(currentMaxAttempts != null ? String(currentMaxAttempts) : '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const panelStyle: React.CSSProperties = {
    marginTop: '0.5rem',
    padding: '0.75rem',
    background: '#f3f0ff',
    borderRadius: 6,
    border: '1px solid #d1c4e9',
    fontSize: '0.85rem',
  };

  const labelStyle: React.CSSProperties = { display: 'block', fontSize: '0.78rem', marginBottom: '0.2rem', color: '#555' };
  const inputStyle: React.CSSProperties = { padding: '0.3rem 0.5rem', width: '100%', borderRadius: 4, border: '1px solid #ccc' };
  const rowStyle: React.CSSProperties = { display: 'flex', gap: '0.75rem', marginBottom: '0.5rem', flexWrap: 'wrap' };

  if (isLocked) {
    return (
      <div style={panelStyle}>
        <strong style={{ color: '#5c35a8' }}>⚙️ Liberación automática de pistas</strong>
        <hr style={{ margin: '0.4rem 0', borderColor: '#d1c4e9' }} />
        {currentTimeMinutes == null && currentMaxAttempts == null ? (
          <p style={{ color: '#888', margin: 0 }}>Sin regla automática configurada.</p>
        ) : (
          <p style={{ margin: 0 }}>
            {currentTimeMinutes != null && <span>Tiempo: <strong>{currentTimeMinutes} min</strong>&nbsp;&nbsp;</span>}
            {currentMaxAttempts != null && <span>Intentos: <strong>{currentMaxAttempts}</strong></span>}
          </p>
        )}
      </div>
    );
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const parsedTime = timeMinutes.trim() !== '' ? parseInt(timeMinutes, 10) : null;
    const parsedAttempts = maxAttempts.trim() !== '' ? parseInt(maxAttempts, 10) : null;
    try {
      await autoReleaseService.configure(missionId, stageId, {
        timeMinutes: parsedTime,
        maxAttempts: parsedAttempts,
      });
      setSuccess(true);
      setTimeout(() => {
        setSuccess(false);
        onChanged();
      }, 2000);
    } catch (err) {
      setError((err as { message?: string })?.message ?? 'No se pudo guardar la regla.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div style={panelStyle}>
      <strong style={{ color: '#5c35a8' }}>⚙️ Liberación automática de pistas</strong>
      <hr style={{ margin: '0.4rem 0', borderColor: '#d1c4e9' }} />
      <form onSubmit={handleSave}>
        <div style={rowStyle}>
          <div style={{ flex: 1, minWidth: 140 }}>
            <label style={labelStyle}>Tiempo (minutos):</label>
            <input
              type="number"
              min={1}
              value={timeMinutes}
              onChange={e => setTimeMinutes(e.target.value)}
              placeholder="Sin límite"
              style={inputStyle}
            />
          </div>
          <div style={{ flex: 1, minWidth: 140 }}>
            <label style={labelStyle}>Intentos fallidos:</label>
            <input
              type="number"
              min={1}
              value={maxAttempts}
              onChange={e => setMaxAttempts(e.target.value)}
              placeholder="Sin límite"
              style={inputStyle}
            />
          </div>
        </div>
        <p style={{ fontSize: '0.75rem', color: '#777', margin: '0 0 0.5rem' }}>
          Dejar vacío = sin regla automática
        </p>
        {error && <p style={{ color: 'red', margin: '0.3rem 0', fontSize: '0.82rem' }}>{error}</p>}
        {success && <p style={{ color: '#2e7d32', margin: '0.3rem 0', fontSize: '0.82rem' }}>✅ Regla guardada</p>}
        <button type="submit" disabled={saving} style={{ fontSize: '0.82rem', padding: '0.3rem 0.75rem' }}>
          {saving ? 'Guardando…' : 'Guardar regla'}
        </button>
      </form>
    </div>
  );
}

// ── StageEditForm ─────────────────────────────────────────────────────────────

function StageEditForm({ stage, missionId, onDone }: { stage: StageDetail; missionId: string; onDone: () => void }) {
  const [form, setForm] = useState<UpdateStagePayload>({
    title: stage.title,
    order: stage.order,
    baseScore: stage.baseScore,
    question: stage.question ?? '',
    options: stage.options.map(o => ({ text: o.text, isCorrect: o.isCorrect })),
    latitude: stage.latitude ?? undefined,
    longitude: stage.longitude ?? undefined,
    qrCode: stage.qrCode ?? '',
  });
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  function setField<K extends keyof UpdateStagePayload>(key: K, value: UpdateStagePayload[K]) {
    setForm(f => ({ ...f, [key]: value }));
  }

  function setOptionText(i: number, text: string) {
    setForm(f => {
      const options = [...(f.options ?? [])];
      options[i] = { ...options[i], text };
      return { ...f, options };
    });
  }

  function setCorrectOption(i: number) {
    setForm(f => ({ ...f, options: f.options?.map((o, idx) => ({ ...o, isCorrect: idx === i })) }));
  }

  function addOption() {
    setForm(f => ({ ...f, options: [...(f.options ?? []), { text: '', isCorrect: false }] }));
  }

  function removeOption(i: number) {
    setForm(f => ({ ...f, options: f.options?.filter((_, idx) => idx !== i) }));
  }

  function handleTreasureChange(data: TreasureHuntData | null) {
    if (data) setForm(f => ({ ...f, latitude: data.latitude, longitude: data.longitude, qrCode: data.qrCode }));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      await stageService.updateStage(missionId, stage.id, form);
      onDone();
    } catch (err) {
      setError((err as ApiError)?.message ?? 'No se pudo guardar la etapa.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} style={{ padding: '0.75rem', background: '#fffde7' }}>
      <strong style={{ display: 'block', marginBottom: '0.5rem', color: '#f57f17' }}>
        ✏️ Editando: {stage.title}
      </strong>

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
        <div style={{ flex: 3 }}>
          <label style={{ fontSize: '0.8rem' }}>Título</label>
          <input value={form.title} onChange={e => setField('title', e.target.value)} required
            style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
        </div>
        <div style={{ flex: 1 }}>
          <label style={{ fontSize: '0.8rem' }}>Orden</label>
          <input type="number" min={1} value={form.order} onChange={e => setField('order', Number(e.target.value))} required
            style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
        </div>
        <div style={{ flex: 1 }}>
          <label style={{ fontSize: '0.8rem' }}>Puntaje</label>
          <input type="number" min={0} value={form.baseScore} onChange={e => setField('baseScore', Number(e.target.value))} required
            style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
        </div>
      </div>

      {stage.type === 'Trivia' && (
        <>
          <div style={{ marginBottom: '0.5rem' }}>
            <label style={{ fontSize: '0.8rem' }}>Pregunta</label>
            <input value={form.question ?? ''} onChange={e => setField('question', e.target.value)}
              style={{ display: 'block', width: '100%', padding: '0.35rem' }} />
          </div>
          <div style={{ marginBottom: '0.5rem' }}>
            <label style={{ fontSize: '0.8rem' }}>Opciones <span style={{ color: '#888' }}>(marcá la correcta)</span></label>
            {form.options?.map((opt, i) => (
              <div key={i} style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', marginTop: '0.3rem' }}>
                <input type="radio" name={`editCorrect-${stage.id}`} checked={opt.isCorrect} onChange={() => setCorrectOption(i)} />
                <input value={opt.text} onChange={e => setOptionText(i, e.target.value)}
                  placeholder={`Opción ${i + 1}`} style={{ flex: 1, padding: '0.3rem' }} />
                {(form.options?.length ?? 0) > 2 && (
                  <button type="button" onClick={() => removeOption(i)}
                    style={{ color: 'red', background: 'none', border: 'none', cursor: 'pointer' }}>✕</button>
                )}
              </div>
            ))}
            <button type="button" onClick={addOption} style={{ marginTop: '0.4rem', fontSize: '0.8rem' }}>
              + Agregar opción
            </button>
          </div>
        </>
      )}

      {stage.type === 'TreasureHunt' && (
        <TreasureHuntConfig
          onChange={handleTreasureChange}
          initialLatitude={stage.latitude ?? undefined}
          initialLongitude={stage.longitude ?? undefined}
          initialQrCode={stage.qrCode ?? undefined}
        />
      )}

      {error && <p style={{ color: 'red', fontSize: '0.85rem', margin: '0.4rem 0' }}>{error}</p>}

      <div style={{ display: 'flex', gap: '0.5rem', marginTop: '0.75rem' }}>
        <button type="submit" disabled={saving}>{saving ? 'Guardando…' : '💾 Guardar cambios'}</button>
        <button type="button" onClick={onDone} style={{ background: '#eee', border: '1px solid #ccc' }}>Cancelar</button>
      </div>
    </form>
  );
}
