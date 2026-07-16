import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { Stage } from '../../types/stage';
import type { Clue } from '../../types/clue';
import type { TeamProgressDto } from '../../types/team';
import {
  Alert,
  Badge,
  Button,
  EmptyState,
  FormField,
  Modal,
  Stack,
  Textarea,
  TextInput,
} from '../ui';

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
  /** false = modo consulta (Administrador, RB-10): oculta la columna de acciones. */
  canManage: boolean;
}

const RANK_COLOR: Record<number, string> = {
  1: '#f59e0b',
  2: '#94a3b8',
  3: '#b45309',
};

function RankBadge({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
  return (
    <span
      className="font-bold inline-block min-w-[28px]"
      style={{ color: RANK_COLOR[rank] ?? '#475569' }}
    >
      {medal ?? `#${rank}`}
    </span>
  );
}

function ConnectionDot({ connected }: { connected: boolean }) {
  return (
    <span
      title={connected ? 'Conectado' : 'Desconectado'}
      className="inline-block w-2.5 h-2.5 rounded-full mr-2 align-middle"
      style={{ background: connected ? '#16a34a' : '#cbd5e1' }}
    />
  );
}

export function TeamProgressPanel({
  teams,
  sessionId,
  sessionStatus,
  stages,
  cluesByStage,
  onClueReleased,
  canManage,
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
  const [advancing, setAdvancing] = useState<string | null>(null);
  const [advanceError, setAdvanceError] = useState<string | null>(null);

  if (teams.length === 0) {
    return (
      <EmptyState
        icon="👥"
        title="Aún no hay equipos inscritos"
        description="Los equipos aparecerán aquí cuando los participantes ingresen el código de acceso."
      />
    );
  }

  function stageForTeam(team: TeamProgressDto): Stage | undefined {
    return stages.find(s => s.order === team.currentStageOrder);
  }
  function cluesForTeam(team: TeamProgressDto): Clue[] {
    const stage = stageForTeam(team);
    if (!stage) return [];
    return (cluesByStage[stage.id] ?? []).slice().sort((a, b) => a.order - b.order);
  }

  async function handleReleaseClue(team: TeamProgressDto) {
    const clues = cluesForTeam(team);
    const nextClue = clues[team.cluesReceivedCurrentStage];
    if (!nextClue) return;

    setReleasing(team.id);
    setReleaseError(null);
    try {
      await sessionService.releaseClue(sessionId, team.id, clues.length, {
        clueContent: nextClue.content,
        clueLatitude: nextClue.latitude,
        clueLongitude: nextClue.longitude,
        clueRadiusMeters: nextClue.radiusMeters,
      });
      const summary = nextClue.content ?? `Zona geográfica (radio ${nextClue.radiusMeters ?? '?'}m)`;
      setLastReleased({ teamName: team.name, content: summary });
      onClueReleased();
    } catch {
      setReleaseError(`No se pudo liberar la pista para ${team.name}`);
    } finally {
      setReleasing(null);
    }
  }

  async function handleForceAdvance(team: TeamProgressDto) {
    const confirmed = window.confirm(
      `¿Forzar avance de "${team.name}" a la siguiente etapa?\n\nEl equipo recibirá 0 puntos por la etapa actual.`,
    );
    if (!confirmed) return;
    setAdvancing(team.id);
    setAdvanceError(null);
    try {
      await sessionService.forceAdvanceTeam(sessionId, team.id);
      onClueReleased();
    } catch {
      setAdvanceError(`No se pudo forzar el avance de ${team.name}`);
    } finally {
      setAdvancing(null);
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
      onClueReleased();
    } catch {
      setPenaltyError('No se pudo aplicar la penalización. Verifique los datos.');
    } finally {
      setPenaltyLoading(false);
    }
  }

  const canAct = sessionStatus === 'InProgress';
  const maxStageOrder = stages.length > 0 ? Math.max(...stages.map(s => s.order)) : 0;

  return (
    <div>
      <Stack gap={2} className="mb-3">
        {lastReleased && (
          <Alert tone="success" onDismiss={() => setLastReleased(null)}>
            💡 Pista enviada a <strong>{lastReleased.teamName}</strong>: «{lastReleased.content}»
          </Alert>
        )}
        {penaltySuccess && (
          <Alert tone="danger" onDismiss={() => setPenaltySuccess(null)}>
            🚨 Penalización aplicada a <strong>{penaltySuccess.teamName}</strong>: -{penaltySuccess.points} pts
          </Alert>
        )}
        {releaseError && <Alert tone="danger">{releaseError}</Alert>}
        {advanceError && <Alert tone="warning">{advanceError}</Alert>}
      </Stack>

      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm">
          <thead className="bg-surface-subtle">
            <tr>
              <Th>Pos.</Th>
              <Th>Equipo</Th>
              <Th align="center">Etapa</Th>
              <Th align="center">Pistas</Th>
              <Th align="right">Puntos</Th>
              {canManage && <Th align="center">Acciones</Th>}
            </tr>
          </thead>
          <tbody>
            {teams.map(team => {
              const clues = cluesForTeam(team);
              const totalClues = clues.length;
              const received = team.cluesReceivedCurrentStage;
              const exhausted = totalClues > 0 && received >= totalClues;
              const isFinished = maxStageOrder > 0 && team.currentStageOrder > maxStageOrder;
              const noStage = team.currentStageOrder === 0 || clues.length === 0;
              const isReleasing = releasing === team.id;
              const isAdvancing = advancing === team.id;

              const releaseDisabled = isReleasing || !canAct || exhausted || noStage;
              const releaseTitle = !canAct
                ? 'Solo se pueden liberar pistas con la sesión en progreso'
                : noStage
                  ? 'El equipo no está en ninguna etapa con pistas configuradas'
                  : exhausted
                    ? 'Todas las pistas de esta etapa ya fueron enviadas'
                    : `Enviar pista ${received + 1} de ${totalClues} a ${team.name}`;

              return (
                <tr
                  key={team.id}
                  className={`border-t border-slate-100 ${team.rank === 1 ? 'bg-warning-50/40' : ''}`}
                >
                  <Td><RankBadge rank={team.rank} /></Td>
                  <Td>
                    <ConnectionDot connected={team.isConnected} />
                    <strong className="text-ink">{team.name}</strong>
                    {team.lastClueWasAutomatic && (
                      <span
                        title="La última pista fue liberada automáticamente por el sistema"
                        className="ml-2 text-xs text-brand-700 font-semibold"
                      >
                        ⚡ Auto
                      </span>
                    )}
                  </Td>
                  <Td align="center">
                    {isFinished
                      ? <Badge tone="success">✓ Finalizado</Badge>
                      : team.currentStageOrder === 0
                        ? <span className="text-ink-subtle">—</span>
                        : <span className="text-ink-soft">Etapa {team.currentStageOrder}</span>}
                  </Td>
                  <Td align="center">
                    {noStage ? (
                      <span className="text-ink-subtle">—</span>
                    ) : (
                      <Badge tone={exhausted ? 'neutral' : 'brand'}>
                        {received}/{totalClues}{exhausted ? ' ✓' : ''}
                      </Badge>
                    )}
                  </Td>
                  <Td align="right">
                    <span className="font-bold tabular-nums text-ink">
                      {team.score.toLocaleString('es-VE')}
                    </span>
                  </Td>
                  {canManage && (
                  <Td align="center">
                    <div className="flex items-center gap-1 justify-center flex-wrap">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => handleReleaseClue(team)}
                        disabled={releaseDisabled}
                        title={releaseTitle}
                      >
                        {isReleasing ? '…' : exhausted ? '✓ Agotadas' : '💡 Liberar pista'}
                      </Button>
                      <Button
                        variant="danger"
                        size="sm"
                        onClick={() => {
                          setPenaltyTarget(team);
                          setPenaltyPoints(1);
                          setPenaltyReason('');
                          setPenaltyError(null);
                        }}
                        disabled={!canAct}
                        title={!canAct ? 'Solo se puede penalizar con la sesión en progreso' : `Aplicar penalización a ${team.name}`}
                      >
                        🚨 Penalizar
                      </Button>
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => handleForceAdvance(team)}
                        disabled={isAdvancing || !canAct}
                        title={!canAct ? 'Solo se puede forzar el avance con la sesión en progreso' : `Forzar avance de ${team.name} a la siguiente etapa (0 puntos)`}
                      >
                        {isAdvancing ? '…' : '⏭ Forzar'}
                      </Button>
                    </div>
                  </Td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <Modal
        open={!!penaltyTarget}
        onClose={() => { setPenaltyTarget(null); setPenaltyError(null); }}
        title={`🚨 Penalizar equipo: ${penaltyTarget?.name ?? ''}`}
        description="La sanción restará puntos al equipo y quedará registrada en la auditoría con el motivo que indiques."
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => { setPenaltyTarget(null); setPenaltyError(null); }}
              disabled={penaltyLoading}
            >
              Cancelar
            </Button>
            <Button
              variant="danger"
              onClick={handlePenalize}
              disabled={penaltyLoading || !penaltyReason.trim() || penaltyPoints < 1}
            >
              {penaltyLoading ? 'Aplicando…' : 'Aplicar penalización'}
            </Button>
          </>
        }
      >
        <Stack gap={3}>
          <FormField label="Puntos a restar" htmlFor="penalty-points" required>
            <TextInput
              id="penalty-points"
              type="number"
              min={1}
              value={penaltyPoints}
              onChange={e => setPenaltyPoints(Math.max(1, Number(e.target.value)))}
            />
          </FormField>
          <FormField label="Motivo" htmlFor="penalty-reason" required>
            <Textarea
              id="penalty-reason"
              rows={3}
              placeholder="Describe el motivo de la penalización…"
              value={penaltyReason}
              onChange={e => setPenaltyReason(e.target.value)}
            />
          </FormField>
          {penaltyError && <Alert tone="danger">{penaltyError}</Alert>}
        </Stack>
      </Modal>
    </div>
  );
}

function Th({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <th className={`px-3 py-2 text-xs font-semibold uppercase tracking-wider text-ink-muted text-${align}`}>
      {children}
    </th>
  );
}

function Td({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <td className={`px-3 py-2.5 align-middle text-${align}`}>{children}</td>
  );
}
