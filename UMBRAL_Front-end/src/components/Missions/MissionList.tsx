import { useEffect, useState } from 'react';
import { missionService } from '../../services/missionService';
import {
  DIFFICULTY_LABELS,
  type ApiError,
  type CreateMissionPayload,
  type DifficultyLevel,
  type Mission,
} from '../../types/mission';

const DIFFICULTY_OPTIONS: DifficultyLevel[] = ['Easy', 'Medium', 'Hard'];

const initialForm: CreateMissionPayload = {
  name: '',
  description: '',
  difficulty: 'Easy',
  maxDuration: 30,
};

export function MissionList() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState<CreateMissionPayload>(initialForm);
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [statusError, setStatusError] = useState<Record<string, string>>({});

  useEffect(() => {
    loadMissions();
  }, []);

  async function loadMissions() {
    setLoading(true);
    setError(null);
    try {
      const data = await missionService.getAll();
      setMissions(data);
    } catch {
      setError('Failed to load missions. Please try again.');
    } finally {
      setLoading(false);
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setFormError(null);
    setSubmitting(true);
    try {
      await missionService.create(form);
      setForm(initialForm);
      await loadMissions();
    } catch (err) {
      const apiErr = err as ApiError;
      setFormError(apiErr?.message ?? 'Could not create mission.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleToggleStatus(mission: Mission) {
    const activate = mission.status === 'Inactive';
    setStatusError(prev => ({ ...prev, [mission.id]: '' }));
    try {
      await missionService.changeStatus(mission.id, activate);
      await loadMissions();
    } catch (err) {
      const apiErr = err as ApiError;
      setStatusError(prev => ({
        ...prev,
        [mission.id]: apiErr?.message ?? 'Could not change status.',
      }));
    }
  }

  return (
    <div style={{ maxWidth: 800, margin: '0 auto', padding: '2rem', fontFamily: 'sans-serif' }}>
      <h1>Missions</h1>

      {/* Create form */}
      <section style={{ marginBottom: '2rem', padding: '1rem', border: '1px solid #ddd', borderRadius: 8 }}>
        <h2>New Mission</h2>
        <form onSubmit={handleCreate}>
          <div style={{ marginBottom: '0.75rem' }}>
            <label>Name</label>
            <input
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              required
              style={{ display: 'block', width: '100%', padding: '0.4rem' }}
            />
          </div>

          <div style={{ marginBottom: '0.75rem' }}>
            <label>Description</label>
            <textarea
              value={form.description}
              onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
              rows={3}
              style={{ display: 'block', width: '100%', padding: '0.4rem' }}
            />
          </div>

          <div style={{ marginBottom: '0.75rem' }}>
            <label>Difficulty level</label>
            <select
              value={form.difficulty}
              onChange={e => setForm(f => ({ ...f, difficulty: e.target.value as DifficultyLevel }))}
              style={{ display: 'block', padding: '0.4rem' }}
            >
              {DIFFICULTY_OPTIONS.map(d => (
                <option key={d} value={d}>{DIFFICULTY_LABELS[d]}</option>
              ))}
            </select>
          </div>

          <div style={{ marginBottom: '0.75rem' }}>
            <label>Max duration (minutes)</label>
            <input
              type="number"
              min={1}
              value={form.maxDuration}
              onChange={e => setForm(f => ({ ...f, maxDuration: Number(e.target.value) }))}
              required
              style={{ display: 'block', padding: '0.4rem' }}
            />
          </div>

          {formError && <p style={{ color: 'red' }}>{formError}</p>}
          <button type="submit" disabled={submitting}>
            {submitting ? 'Creating…' : 'Create Mission'}
          </button>
        </form>
      </section>

      {/* Mission list */}
      {loading && <p>Loading missions…</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}

      {!loading && !error && missions.length === 0 && (
        <p>No missions yet. Create one above.</p>
      )}

      <ul style={{ listStyle: 'none', padding: 0 }}>
        {missions.map(mission => (
          <li
            key={mission.id}
            style={{
              padding: '1rem',
              marginBottom: '0.75rem',
              border: '1px solid #ddd',
              borderRadius: 8,
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'flex-start',
            }}
          >
            <div>
              <strong>{mission.name}</strong>
              <span
                style={{
                  marginLeft: '0.5rem',
                  padding: '0.1rem 0.5rem',
                  borderRadius: 4,
                  fontSize: '0.75rem',
                  background: mission.status === 'Active' ? '#d4edda' : '#f8d7da',
                  color: mission.status === 'Active' ? '#155724' : '#721c24',
                }}
              >
                {mission.status}
              </span>
              <p style={{ margin: '0.25rem 0', color: '#555' }}>{mission.description}</p>
              <small>
                Difficulty: {DIFFICULTY_LABELS[mission.difficulty]} ·{' '}
                Max duration: {mission.maxDuration} min ·{' '}
                Stages: {mission.stageCount}
              </small>
              {statusError[mission.id] && (
                <p style={{ color: 'red', margin: '0.25rem 0', fontSize: '0.85rem' }}>
                  {statusError[mission.id]}
                </p>
              )}
            </div>
            <button
              onClick={() => handleToggleStatus(mission)}
              style={{ whiteSpace: 'nowrap', marginLeft: '1rem' }}
            >
              {mission.status === 'Active' ? 'Deactivate' : 'Activate'}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
