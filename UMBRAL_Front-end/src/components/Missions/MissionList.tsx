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
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  CardSection,
  EmptyState,
  FormField,
  PageHeader,
  Select,
  Spinner,
  Stack,
  Textarea,
  TextInput,
} from '../ui';

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

export function MissionList() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createForm, setCreateForm] = useState<CreateMissionPayload>(initialCreateForm);
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [statusError, setStatusError] = useState<Record<string, string>>({});

  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [stagesByMission, setStagesByMission] = useState<Record<string, Stage[]>>({});
  const [stagesLoading, setStagesLoading] = useState<Record<string, boolean>>({});

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

  return (
    <div>
      <PageHeader
        eyebrow="Catálogo"
        title="Misiones"
        description="Catálogo de experiencias educativas. Activá una misión para poder generar sesiones a partir de ella."
      />

      {/* ── Creación ─────────────────────────────────────────────────────── */}
      <Card className="mb-5">
        <CardHeader title="Nueva misión" />
        <form onSubmit={handleCreate}>
          <Stack gap={3}>
            <FormField label="Nombre" htmlFor="mission-name" required>
              <TextInput
                id="mission-name"
                required
                value={createForm.name}
                onChange={e => setCreateForm(f => ({ ...f, name: e.target.value }))}
              />
            </FormField>
            <FormField label="Descripción" htmlFor="mission-desc">
              <Textarea
                id="mission-desc"
                rows={2}
                value={createForm.description}
                onChange={e => setCreateForm(f => ({ ...f, description: e.target.value }))}
              />
            </FormField>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <FormField label="Dificultad" htmlFor="mission-diff">
                <Select
                  id="mission-diff"
                  value={createForm.difficulty}
                  onChange={e => setCreateForm(f => ({ ...f, difficulty: e.target.value as DifficultyLevel }))}
                >
                  {DIFFICULTY_OPTIONS.map(d => <option key={d} value={d}>{DIFFICULTY_LABELS[d]}</option>)}
                </Select>
              </FormField>
              <FormField label="Duración máxima (min)" htmlFor="mission-dur" required>
                <TextInput
                  id="mission-dur"
                  type="number"
                  min={1}
                  required
                  value={createForm.maxDuration}
                  onChange={e => setCreateForm(f => ({ ...f, maxDuration: Number(e.target.value) }))}
                />
              </FormField>
            </div>
            {createError && <Alert tone="danger">{createError}</Alert>}
            <div>
              <Button type="submit" disabled={creating}>
                {creating ? 'Creando…' : 'Crear misión'}
              </Button>
            </div>
          </Stack>
        </form>
      </Card>

      {/* ── Lista ─────────────────────────────────────────────────────────── */}
      {loading && <Card><Spinner label="Cargando misiones…" /></Card>}
      {error && <Alert tone="danger">{error}</Alert>}
      {!loading && !error && missions.length === 0 && (
        <Card>
          <EmptyState
            icon="🗺️"
            title="Todavía no hay misiones"
            description="Creá la primera completando el formulario de arriba."
          />
        </Card>
      )}

      {!loading && !error && missions.length > 0 && (
        <Stack gap={3}>
          {missions.map(mission => {
            const isActive = mission.status === 'Active';
            const isExpanded = expandedId === mission.id;
            const isEditing = editingMissionId === mission.id;
            return (
              <Card key={mission.id} padded={false}>
                <div className="p-4 md:p-5">
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="text-base font-semibold text-ink">{mission.name}</h3>
                        <Badge tone={isActive ? 'success' : 'neutral'}>
                          {STATUS_LABELS[mission.status] ?? mission.status}
                        </Badge>
                      </div>
                      {mission.description && (
                        <p className="text-sm text-ink-soft mt-1">{mission.description}</p>
                      )}
                      <p className="text-xs text-ink-muted mt-1.5">
                        {DIFFICULTY_LABELS[mission.difficulty]} · {mission.maxDuration} min · {mission.stageCount} etapa{mission.stageCount !== 1 ? 's' : ''}
                      </p>
                      {statusError[mission.id] && (
                        <p className="text-xs text-danger-600 mt-1">{statusError[mission.id]}</p>
                      )}
                    </div>
                    <div className="flex items-center gap-2 shrink-0 flex-wrap">
                      <Button variant="secondary" size="sm" onClick={() => toggleExpand(mission.id)}>
                        {isExpanded ? '▲ Etapas' : '▼ Etapas'}
                      </Button>
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => setEditingMissionId(isEditing ? null : mission.id)}
                        disabled={isActive}
                        title={isActive ? 'Desactivá la misión para editarla' : undefined}
                      >
                        {isEditing ? 'Cancelar edición' : '✏️ Editar'}
                      </Button>
                      <Button
                        variant={isActive ? 'danger' : 'primary'}
                        size="sm"
                        onClick={() => handleToggleStatus(mission)}
                      >
                        {isActive ? 'Desactivar' : 'Activar'}
                      </Button>
                    </div>
                  </div>
                </div>

                {isEditing && (
                  <CardSection divided className="p-4 md:p-5 bg-warning-50/40">
                    <MissionEditForm
                      mission={mission}
                      onSaved={async () => { setEditingMissionId(null); await loadMissions(); }}
                      onCancel={() => setEditingMissionId(null)}
                    />
                  </CardSection>
                )}

                {isExpanded && (
                  <CardSection divided className="p-4 md:p-5 bg-surface-subtle">
                    <h4 className="text-sm font-semibold text-ink mb-2">Etapas</h4>
                    {stagesLoading[mission.id] ? (
                      <Spinner size="sm" label="Cargando etapas…" />
                    ) : (
                      <StageManager
                        mission={mission}
                        stages={stagesByMission[mission.id] ?? []}
                        onChanged={() => handleStageChanged(mission.id)}
                      />
                    )}
                  </CardSection>
                )}
              </Card>
            );
          })}
        </Stack>
      )}
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
      <h4 className="text-sm font-semibold text-warning-700 mb-2">✏️ Editar misión</h4>
      <Stack gap={3}>
        <FormField label="Nombre" htmlFor={`edit-name-${mission.id}`} required>
          <TextInput
            id={`edit-name-${mission.id}`}
            required
            value={form.name}
            onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
          />
        </FormField>
        <FormField label="Descripción" htmlFor={`edit-desc-${mission.id}`}>
          <Textarea
            id={`edit-desc-${mission.id}`}
            rows={2}
            value={form.description}
            onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
          />
        </FormField>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <FormField label="Dificultad" htmlFor={`edit-diff-${mission.id}`}>
            <Select
              id={`edit-diff-${mission.id}`}
              value={form.difficulty}
              onChange={e => setForm(f => ({ ...f, difficulty: e.target.value as DifficultyLevel }))}
            >
              {DIFFICULTY_OPTIONS.map(d => <option key={d} value={d}>{DIFFICULTY_LABELS[d]}</option>)}
            </Select>
          </FormField>
          <FormField label="Duración máxima (min)" htmlFor={`edit-dur-${mission.id}`} required>
            <TextInput
              id={`edit-dur-${mission.id}`}
              type="number"
              min={1}
              required
              value={form.maxDuration}
              onChange={e => setForm(f => ({ ...f, maxDuration: Number(e.target.value) }))}
            />
          </FormField>
        </div>
        {error && <Alert tone="danger">{error}</Alert>}
        <div className="flex items-center gap-2">
          <Button type="submit" disabled={saving} size="sm">
            {saving ? 'Guardando…' : '💾 Guardar'}
          </Button>
          <Button type="button" variant="secondary" size="sm" onClick={onCancel}>
            Cancelar
          </Button>
        </div>
      </Stack>
    </form>
  );
}
