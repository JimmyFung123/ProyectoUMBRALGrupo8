import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { Stage } from '../../types/stage';
import type { Clue } from '../../types/clue';
import type { TeamProgressDto } from '../../types/team';

interface Props {
  teams: TeamProgressDto[];
  sessionId: string;
  sessionStatus: string;
  /** Stages for the session's mission, sorted by order. */
  stages: Stage[];
  /** Clues grouped by stageId, sorted by order. */
  cluesByStage: Record<string, Clue[]>;
  /** Called after a successful clue release so the parent can reload data. */
  onClueReleased: () => void;
}

const RANK_COLORS: Record<number, string> = {
  1: '#f9a825',
  2: '#90a4ae',
  3: '#a1887f',
};

function RankBadge({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: '0.25rem',
      fontWeight: 'bold',
      color: RANK_COLORS[rank] ?? '#555',
      minWidth: 28,
    }}>
      {medal ?? `#${rank}`}
    </span>
  );
}

function ConnectionDot({ connected }: { connected: boolean }) {
  return (
    <span
      title={connected ? 'Conectado' : 'Desconectado'}
      style={{
        display: 'inline-block',
        width: 10,
        height: 10,
        borderRadius: '50%',
        background: connected ? '#27ae60' : '#bdc3c7',
        marginRight: '0.4rem',
      }}
    />
  );
}

// ── TeamProgressPanel ─────────────────────────────────────────────────────────

export function TeamProgressPanel({
  teams,
  sessionId,
  sessionStatus,
  stages,
  cluesByStage,
  onClueReleased,
}: Props) {
  const [releasing, setReleasing] = useState<string | null>(null);
  const [releaseError, setReleaseError] = useState<string | null>(null);
  const [lastReleased, setLastReleased] = useState<{ teamName: string; content: string } | null>(null);
  const [penaltyTarget, setPenaltyTarget] = useState<TeamProgressDto | null>(null);
  const [penaltyPoints, setPenaltyPoints] = useState<number>(1);
  const [penaltyReason, setPenaltyReason] = useState<string>('');
  const [penaltyLoading, setPenaltyLoading] = useState(false);
  const [penaltyError, setPenaltyError] = useState<string | null>(null);
  const [penaltySuccess, setPenaltySuccess] = useState<{ teamName: string; points: number } | null>(null);

  if (teams.length === 0) {
    return (
      <p style={{ color: '#999', fontSize: '0.9rem', margin: 0 }}>
        Aún no hay equipos inscritos en esta sesión.
      </p>
    );
  }

  /** Finds the stage object for a team based on its CurrentStageOrder. */
  function stageForTeam(team: TeamProgressDto): Stage | undefined {
    return stages.find(s => s.order === team.currentStageOrder);
  }

  /** Clue array for the team's current stage, sorted by order. */
  function cluesForTeam(team: TeamProgressDto): Clue[] {
    const stage = stageForTeam(team);
    if (!stage) return [];
    return (cluesByStage[stage.id] ?? []).slice().sort((a, b) => a.order - b.order);
  }

  async function handleReleaseClue(team: TeamProgressDto) {
    const clues = cluesForTeam(team);
    const nextClue = clues[team.cluesReceivedCurrentStage]; // 0-based index
    if (!nextClue) return;

    setReleasing(team.id);
    setReleaseError(null);

    try {
      await sessionService.releaseClue(sessionId, team.id, clues.length, nextClue.content);
      setLastReleased({ teamName: team.name, content: nextClue.content });
      onClueReleased();
    } catch {
      setReleaseError(`No se pudo liberar la pista para ${team.name}`);
    } finally {
      setReleasing(null);
    }
  }

  async function handlePenalize() {
    if (!penaltyTarget || !penaltyReason.trim() || penaltyPoints < 1) return;
    setPenaltyLoading(true);
    setPenaltyError(null);
    try {
      await sessionService.penalizeTeam(sessionId, penaltyTarget.id, penaltyPoints, penaltyReason.trim());
      setPenaltySuccess({ teamName: penaltyTarget.name, points: penaltyPoints });
      setPenaltyTarget(null);
      setPenaltyPoints(1);
      setPenaltyReason('');
      onClueReleased(); // reloads ranking
    } catch {
      setPenaltyError('No se pudo aplicar la penalización. Verifique los datos.');
    } finally {
      setPenaltyLoading(false);
    }
  }

  const canReleaseClues = sessionStatus === 'InProgress';

  return (
    <div>
      {/* ── Toast: last released clue ──────────────────────────────── */}
      {lastReleased && (
        <div style={{
          marginBottom: '0.75rem',
          padding: '0.6rem 0.9rem',
          background: '#d4edda',
          border: '1px solid #c3e6cb',
          borderRadius: 6,
          fontSize: '0.85rem',
          color: '#155724',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}>
          <span>
            💡 Pista enviada a <strong>{lastReleased.teamName}</strong>: «{lastReleased.content}»
          </span>
          <button
            onClick={() => setLastReleased(null)}
            style={{ background: 'none', border: 'none', cursor: 'pointer', fontWeight: 'bold', color: '#155724' }}
          >
            ✕
          </button>
        </div>
      )}

      {penaltySuccess && (
        <div style={{
          marginBottom: '0.75rem',
          padding: '0.6rem 0.9rem',
          background: '#f8d7da',
          border: '1px solid #f5c6cb',
          borderRadius: 6,
          fontSize: '0.85rem',
          color: '#721c24',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}>
          <span>🚨 Penalización aplicada a <strong>{penaltySuccess.teamName}</strong>: -{penaltySuccess.points} pts</span>
          <button onClick={() => setPenaltySuccess(null)}
            style={{ background: 'none', border: 'none', cursor: 'pointer', fontWeight: 'bold', color: '#721c24' }}>✕</button>
        </div>
      )}

      {releaseError && (
        <div style={{
          marginBottom: '0.75rem',
          padding: '0.5rem 0.9rem',
          background: '#f8d7da',
          borderRadius: 6,
          fontSize: '0.85rem',
          color: '#721c24',
        }}>
          {releaseError}
        </div>
      )}

      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
          <thead>
            <tr style={{ background: '#f5f5f5', textAlign: 'left' }}>
              <th style={thStyle}>Pos.</th>
              <th style={thStyle}>Equipo</th>
              <th style={{ ...thStyle, textAlign: 'center' }}>Etapa</th>
              <th style={{ ...thStyle, textAlign: 'center' }}>Pistas</th>
              <th style={{ ...thStyle, textAlign: 'right' }}>Puntos</th>
              <th style={{ ...thStyle, textAlign: 'center' }}>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {teams.map(team => {
              const clues = cluesForTeam(team);
              const totalClues = clues.length;
              const received = team.cluesReceivedCurrentStage;
              const exhausted = totalClues > 0 && received >= totalClues;
              const noStage = team.currentStageOrder === 0 || clues.length === 0;
              const isReleasing = releasing === team.id;

              const buttonDisabled = isReleasing || !canReleaseClues || exhausted || noStage;
              const buttonTitle = !canReleaseClues
                ? 'Solo se pueden liberar pistas con la sesión en progreso'
                : noStage
                  ? 'El equipo no está en ninguna etapa con pistas configuradas'
                  : exhausted
                    ? 'Todas las pistas de esta etapa ya fueron enviadas'
                    : `Enviar pista ${received + 1} de ${totalClues} a ${team.name}`;

              return (
                <tr
                  key={team.id}
                  style={{
                    borderBottom: '1px solid #eee',
                    background: team.rank === 1 ? '#fffde7' : undefined,
                  }}
                >
                  <td style={tdStyle}>
                    <RankBadge rank={team.rank} />
                  </td>
                  <td style={tdStyle}>
                    <ConnectionDot connected={team.isConnected} />
                    <strong>{team.name}</strong>
                    {team.lastClueWasAutomatic && (
                      <span title="La última pista fue liberada automáticamente por el sistema"
                        style={{ marginLeft: '0.3rem', fontSize: '0.75rem', color: '#6f42c1' }}>
                        ⚡ Auto
                      </span>
                    )}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center' }}>
                    {team.currentStageOrder === 0
                      ? <span style={{ color: '#aaa' }}>—</span>
                      : `Etapa ${team.currentStageOrder}`}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center' }}>
                    {noStage
                      ? <span style={{ color: '#aaa' }}>—</span>
                      : (
                        <span style={{
                          fontSize: '0.8rem',
                          color: exhausted ? '#6c757d' : '#2c3e50',
                          fontWeight: exhausted ? 'normal' : 'bold',
                        }}>
                          {received}/{totalClues}
                          {exhausted && <span style={{ marginLeft: 4, color: '#6c757d' }}>✓</span>}
                        </span>
                      )}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 'bold', color: '#2c3e50' }}>
                    {team.score.toLocaleString('es-VE')}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center' }}>
                    <button
                      onClick={() => handleReleaseClue(team)}
                      disabled={buttonDisabled}
                      title={buttonTitle}
                      style={{
                        padding: '0.2rem 0.6rem',
                        fontSize: '0.8rem',
                        cursor: buttonDisabled ? 'not-allowed' : 'pointer',
                        opacity: buttonDisabled ? 0.45 : 1,
                        background: exhausted ? '#e9ecef' : '#fff3cd',
                        border: '1px solid #ffc107',
                        borderRadius: 4,
                      }}
                    >
                      {isReleasing ? '…' : exhausted ? '✓ Agotadas' : '💡 Liberar pista'}
                    </button>
                    <button
                      onClick={() => {
                        setPenaltyTarget(team);
                        setPenaltyPoints(1);
                        setPenaltyReason('');
                        setPenaltyError(null);
                      }}
                      disabled={!canReleaseClues}
                      title={!canReleaseClues ? 'Solo se puede penalizar con la sesión en progreso' : `Aplicar penalización a ${team.name}`}
                      style={{
                        marginLeft: '0.4rem',
                        padding: '0.2rem 0.6rem',
                        fontSize: '0.8rem',
                        cursor: !canReleaseClues ? 'not-allowed' : 'pointer',
                        opacity: !canReleaseClues ? 0.45 : 1,
                        background: '#f8d7da',
                        border: '1px solid #f5c6cb',
                        borderRadius: 4,
                      }}
                    >
                      🚨 Penalizar
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      {penaltyTarget && (
        <div style={{
          position: 'fixed', inset: 0,
          background: 'rgba(0,0,0,0.45)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          zIndex: 1000,
        }}>
          <div style={{
            background: '#fff', borderRadius: 8, padding: '1.5rem',
            minWidth: 360, maxWidth: 480, width: '90%',
            boxShadow: '0 4px 24px rgba(0,0,0,0.2)',
          }}>
            <h3 style={{ margin: '0 0 1rem', fontSize: '1rem' }}>
              🚨 Penalizar equipo: <strong>{penaltyTarget.name}</strong>
            </h3>

            <label style={{ display: 'block', marginBottom: '0.75rem' }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#555' }}>
                Puntos a restar <span style={{ color: '#c0392b' }}>*</span>
              </span>
              <input
                type="number"
                min={1}
                value={penaltyPoints}
                onChange={e => setPenaltyPoints(Math.max(1, Number(e.target.value)))}
                style={{
                  display: 'block', width: '100%', marginTop: '0.3rem',
                  padding: '0.4rem 0.6rem', border: '1px solid #ccc', borderRadius: 4,
                  fontSize: '0.9rem', boxSizing: 'border-box',
                }}
              />
            </label>

            <label style={{ display: 'block', marginBottom: '0.75rem' }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 600, color: '#555' }}>
                Motivo <span style={{ color: '#c0392b' }}>*</span>
              </span>
              <textarea
                rows={3}
                placeholder="Describe el motivo de la penalización…"
                value={penaltyReason}
                onChange={e => setPenaltyReason(e.target.value)}
                style={{
                  display: 'block', width: '100%', marginTop: '0.3rem',
                  padding: '0.4rem 0.6rem', border: '1px solid #ccc', borderRadius: 4,
                  fontSize: '0.9rem', resize: 'vertical', boxSizing: 'border-box',
                }}
              />
            </label>

            {penaltyError && (
              <p style={{ margin: '0 0 0.75rem', color: '#c0392b', fontSize: '0.85rem' }}>{penaltyError}</p>
            )}

            <div style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
              <button
                onClick={() => { setPenaltyTarget(null); setPenaltyError(null); }}
                disabled={penaltyLoading}
                style={{ padding: '0.4rem 0.9rem', cursor: 'pointer', borderRadius: 4, border: '1px solid #ccc' }}
              >
                Cancelar
              </button>
              <button
                onClick={handlePenalize}
                disabled={penaltyLoading || !penaltyReason.trim() || penaltyPoints < 1}
                style={{
                  padding: '0.4rem 0.9rem', cursor: penaltyLoading || !penaltyReason.trim() ? 'not-allowed' : 'pointer',
                  borderRadius: 4, border: '1px solid #c0392b',
                  background: penaltyLoading || !penaltyReason.trim() ? '#f8d7da' : '#c0392b',
                  color: '#fff', fontWeight: 600,
                  opacity: penaltyLoading || !penaltyReason.trim() ? 0.6 : 1,
                }}
              >
                {penaltyLoading ? 'Aplicando…' : 'Aplicar penalización'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

const thStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem',
  fontWeight: '600',
  fontSize: '0.8rem',
  color: '#555',
  borderBottom: '2px solid #ddd',
};

const tdStyle: React.CSSProperties = {
  padding: '0.6rem 0.75rem',
  verticalAlign: 'middle',
};
