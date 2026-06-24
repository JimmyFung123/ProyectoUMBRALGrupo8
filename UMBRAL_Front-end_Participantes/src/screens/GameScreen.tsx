import { useEffect, useRef, useState } from 'react';
import type { ParticipantStage, QrValidationResult, SessionInfo, TriviaAnswerResult, TriviaWrongAnswerPayload } from '../types';
import { getParticipantStage, submitTriviaAnswer, validateQrCode } from '../services/sessionService';
import { useGameConnection } from '../services/gameConnection';
import { vibrate, isHapticsEnabled, setHapticsEnabled } from '../services/vibrate';
import { TriviaScreen } from './TriviaScreen';
import { TreasureHuntScreen } from './TreasureHuntScreen';
import { RankingScreen } from './RankingScreen';
import { SessionStatusOverlay, type BlockingSessionStatus } from './SessionStatusOverlay';
import { NotificationStack, useToastStack } from './NotificationStack';
import { Confetti } from './Confetti';

type TeamProp = { teamId: string; isLeader: boolean };

interface Props {
  session: SessionInfo;
  team: TeamProp;
  nickname: string;
  initialStage: ParticipantStage;
  /** Called when the user wants to exit the session (e.g. after completing the mission). */
  onLeaveSession: () => void;
}

type StageResult = TriviaAnswerResult | QrValidationResult;

type AnswerState =
  | { phase: 'answering' }
  | { phase: 'result'; result: StageResult };

const POLL_INTERVAL_MS = 8_000;
const RESULT_DISPLAY_MS = 3_000;
const BLOCKING_STATUSES: ReadonlySet<string> = new Set(['Paused', 'Completed', 'Cancelled']);

export function GameScreen({ session, team, nickname, initialStage, onLeaveSession }: Props) {
  const [stage, setStage] = useState<ParticipantStage>(initialStage);
  const [answerState, setAnswerState] = useState<AnswerState>({ phase: 'answering' });
  const [error, setError] = useState<string | null>(null);
  const [showRanking, setShowRanking] = useState(false);
  // Cuando el operador pausa/finaliza/cancela la sesión, el polling detecta el
  // cambio de sessionStatus y bloqueamos toda interacción con un overlay.
  const [blockingStatus, setBlockingStatus] = useState<BlockingSessionStatus | null>(null);
  const [wrongAnswerEvent, setWrongAnswerEvent] = useState<TriviaWrongAnswerPayload | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // HU-28: toast stack — declared before useGameConnection so the
  // event callbacks can call pushToast without forward references.
  const { toasts, push: pushToast, dismiss: dismissToast } = useToastStack();

  const [hapticsOn, setHapticsOn] = useState(isHapticsEnabled());
  const [celebrate, setCelebrate] = useState(false);

  // Single SignalR connection (HU-20 + HU-28): live clues, stage results,
  // operator broadcasts and penalties all go through the same WebSocket.
  const { clues, status: clueStatus, resetClues } = useGameConnection({
    sessionId: session.id,
    teamId: team.teamId,
    onClue: (clue, isAutomatic) => {
      // HU-28: themed toast + haptic on every clue arrival.
      pushToast({
        variant: 'clue',
        title: isAutomatic ? 'Pista automática' : 'Nueva pista',
        body: clue.content
          ?? (clue.radiusMeters != null
            ? `Zona geográfica actualizada (radio ${clue.radiusMeters}m)`
            : 'Pista enviada por el operador'),
      });
      vibrate('tap');
    },
    onStageCompleted: payload => {
      if (payload.wasCorrect) {
        pushToast({
          variant: 'stage',
          title: '¡Etapa superada!',
          body: payload.pointsEarned > 0
            ? `+${payload.pointsEarned} pts · Nuevo puntaje ${payload.newScore}`
            : `Nuevo puntaje ${payload.newScore}`,
        });
        vibrate('success');
        if (payload.isLastStage) {
          setCelebrate(true);
          window.setTimeout(() => setCelebrate(false), 6000);
          vibrate('celebrate');
        }
      } else {
        pushToast({
          variant: 'wrong',
          title: 'Respuesta incorrecta',
          body: 'Sigue intentando — el equipo permanece en esta etapa.',
        });
        vibrate('error');
      }
    },
    onOperatorMessage: msg => {
      pushToast({
        variant: 'message',
        title: 'Mensaje del operador',
        body: msg.message,
        durationMs: 7000,
      });
      vibrate('message');
    },
    onTeamPenalized: payload => {
      // Visible y persistente: el equipo merece leer el motivo completo.
      pushToast({
        variant: 'penalty',
        title: `Penalización: -${payload.points} pts`,
        body: `${payload.reason} · Nuevo puntaje: ${payload.newScore}`,
        durationMs: 8000,
      });
      vibrate('error');
    },
    onTriviaWrongAnswer: payload => {
      setWrongAnswerEvent(payload);
      vibrate('error');
    },
  });

  function toggleHaptics() {
    const next = !hapticsOn;
    setHapticsOn(next);
    setHapticsEnabled(next);
    if (next) vibrate('tap'); // confirm with a tiny buzz
  }

  // Poll for stage updates (handles operator force-advance, session pause, etc.)
  useEffect(() => {
    function startPolling() {
      intervalRef.current = setInterval(async () => {
        try {
          const updated = await getParticipantStage(session.id, team.teamId);
          // Detect operator-driven state changes (HU-11) — pause/finalize/cancel
          // block the UI; resume clears the overlay automatically.
          if (BLOCKING_STATUSES.has(updated.sessionStatus)) {
            setBlockingStatus(updated.sessionStatus as BlockingSessionStatus);
          } else {
            setBlockingStatus(null);
          }
          // Only update if still in answering phase to avoid overwriting result card
          setStage((prev) => {
            if (updated.currentStageOrder !== prev.currentStageOrder) {
              setAnswerState({ phase: 'answering' });
              resetClues();
              setWrongAnswerEvent(null);
            }
            return updated;
          });
        } catch {
          // silently ignore
        }
      }, POLL_INTERVAL_MS);
    }

    startPolling();
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [session.id, team.teamId, resetClues]);

  function handleAnswered(result: StageResult) {
    // Wrong answer with attempts remaining — TriviaScreen handles its own state,
    // so GameScreen stays in 'answering' phase (no result card, no auto-advance).
    if ('shouldAdvance' in result && result.shouldAdvance === false) return;

    setAnswerState({ phase: 'result', result });

    // After 3s, advance to next stage view via polling refresh
    setTimeout(async () => {
      try {
        const updated = await getParticipantStage(session.id, team.teamId);
        setStage(updated);
        setAnswerState({ phase: 'answering' });
        resetClues();
        setWrongAnswerEvent(null);
      } catch {
        setAnswerState({ phase: 'answering' });
      }
    }, RESULT_DISPLAY_MS);
  }

  // HU-21: ranking renders as a full-screen overlay above the active game
  // screen — never replacing it, so TriviaScreen / TreasureHuntScreen stay
  // mounted and the participant's clue history is preserved when they go
  // back from the ranking view.
  const rankingOverlay = showRanking && (
    <RankingScreen
      sessionId={session.id}
      teamId={team.teamId}
      onBack={() => setShowRanking(false)}
    />
  );

  // Blocking overlay shared by every branch (Trivia, TreasureHunt, Waiting,
  // result, Completed). Declared up here so JSX in every branch can reference
  // it without tripping over TS's no-use-before-declaration rule.
  //
  // Fix del bug "Ver ranking final no abre": el SessionStatusOverlay vive en
  // zIndex 3000 y el RankingScreen en zIndex 2000, así que al pulsar "Ver
  // ranking" el ranking se renderizaba debajo del overlay y parecía que el
  // botón no hacía nada. Mientras el ranking esté visible escondemos el
  // overlay bloqueante; cuando el usuario lo cierre, el overlay vuelve a
  // mostrarse automáticamente porque `blockingStatus` sigue siendo "Completed".
  const blockingOverlay = blockingStatus && !showRanking && (
    <SessionStatusOverlay
      status={blockingStatus}
      onLeaveSession={blockingStatus === 'Paused' ? undefined : onLeaveSession}
      onViewRanking={blockingStatus === 'Completed' ? () => setShowRanking(true) : undefined}
    />
  );

  // HU-28: notification + confetti + haptics toggle. Single bundle so every
  // branch only has to drop `{liveLayer}` once.
  const liveLayer = (
    <>
      <NotificationStack toasts={toasts} onDismiss={dismissToast} />
      {celebrate && <Confetti durationSeconds={6} />}
    </>
  );

  // Completed: team has gone past the last stage
  if (stage.type === 'Completed' || (stage.isLastStage && answerState.phase === 'result' && answerState.result.nextStageOrder > stage.order)) {
    return (
      <>
        <div style={styles.container}>
          <div style={styles.card}>
            <div style={styles.icon}>🏆</div>
            <h2 style={styles.title}>¡Completaste la misión!</h2>
            <p style={styles.subtitle}>
              Bien jugado, <strong style={{ color: '#6366f1' }}>{nickname}</strong>.
            </p>
            {answerState.phase === 'result' && (
              <p style={styles.score}>Puntaje final: <strong>{answerState.result.newScore}</strong></p>
            )}
            <button onClick={() => setShowRanking(true)} style={styles.viewRankingButton}>
              🏆 Ver ranking final
            </button>
            <button onClick={onLeaveSession} style={styles.leaveButton}>
              ↩ Volver al inicio
            </button>
          </div>
        </div>
        {rankingOverlay}
        {blockingOverlay}
        {liveLayer}
      </>
    );
  }

  // Result card (correct / incorrect feedback)
  if (answerState.phase === 'result') {
    const { result } = answerState;
    return (
      <>
        <div style={styles.container}>
          <div style={styles.card}>
            <div style={styles.icon}>{result.isCorrect ? '✅' : '❌'}</div>
            <h2 style={{ ...styles.title, color: result.isCorrect ? '#34d399' : '#f87171' }}>
              {result.isCorrect ? '¡Correcto!' : 'Incorrecto'}
            </h2>
            <p style={styles.scoreLabel}>Puntaje actual</p>
            <p style={styles.scoreBig}>{result.newScore}</p>
            <p style={styles.subtitle}>
              {result.isLastStage ? 'Última etapa completada…' : 'Avanzando a la siguiente etapa…'}
            </p>
          </div>
        </div>
        {rankingOverlay}
        {blockingOverlay}
        {liveLayer}
      </>
    );
  }

  // Waiting for session to start / stage assigned
  if (stage.type === 'Waiting') {
    return (
      <>
        <div style={styles.container}>
          <div style={styles.card}>
            <div style={styles.icon}>⏳</div>
            <h2 style={styles.title}>Esperando inicio…</h2>
            <p style={styles.subtitle}>La actividad comenzará en breve.</p>
            <button onClick={() => setShowRanking(true)} style={styles.viewRankingButton}>
              🏆 Ver ranking
            </button>
            <button onClick={onLeaveSession} style={styles.leaveButton}>
              ↩ Salir de la sesión
            </button>
          </div>
        </div>
        {rankingOverlay}
        {blockingOverlay}
        {liveLayer}
      </>
    );
  }

  // HU-21: floating button to open the ranking from any in-game screen.
  const rankingFab = (
    <button
      onClick={() => setShowRanking(true)}
      style={styles.rankingFab}
      aria-label="Ver ranking"
      title="Ver ranking"
    >
      🏆
    </button>
  );

  // HU-28: discreet haptics toggle so participants who don't want vibration
  // (or are testing on desktop) can silence it. Pinned to the bottom-left so
  // it doesn't fight the ranking FAB on the right.
  const hapticsFab = (
    <button
      onClick={toggleHaptics}
      style={{
        ...styles.rankingFab,
        right: 'auto',
        left: '1rem',
        background: hapticsOn ? '#1e293b' : '#475569',
        fontSize: '1.2rem',
      }}
      aria-label={hapticsOn ? 'Silenciar vibración' : 'Activar vibración'}
      title={hapticsOn ? 'Vibración activa — toca para silenciar' : 'Vibración silenciada — toca para activar'}
    >
      {hapticsOn ? '📳' : '🔕'}
    </button>
  );

  // Exit FAB — always reachable from Trivia / TreasureHunt. Confirms before
  // leaving so a stray tap doesn't kick a teammate out mid-stage.
  const exitFab = (
    <button
      onClick={() => {
        if (window.confirm('¿Salir de la sesión? Volverás a la pantalla de inicio.')) {
          onLeaveSession();
        }
      }}
      style={styles.exitFab}
      aria-label="Salir de la sesión"
      title="Salir de la sesión"
    >
      ✕
    </button>
  );

  // TreasureHunt
  if (stage.type === 'TreasureHunt') {
    return (
      <>
        {error && (
          <div style={styles.errorBanner}>
            {error}
            <button onClick={() => setError(null)} style={styles.errorClose}>✕</button>
          </div>
        )}
        <TreasureHuntScreen
          stage={stage}
          sessionId={session.id}
          teamId={team.teamId}
          onResolved={handleAnswered}
          onError={setError}
          validateQr={validateQrCode}
          clues={clues}
          clueStatus={clueStatus}
        />
        {exitFab}
        {rankingFab}
        {hapticsFab}
        {rankingOverlay}
        {blockingOverlay}
        {liveLayer}
      </>
    );
  }

  // Trivia
  return (
    <>
      {error && (
        <div style={styles.errorBanner}>
          {error}
          <button onClick={() => setError(null)} style={styles.errorClose}>✕</button>
        </div>
      )}
      <TriviaScreen
        stage={stage}
        sessionId={session.id}
        teamId={team.teamId}
        nickname={nickname}
        onAnswered={handleAnswered}
        onError={setError}
        submitAnswer={submitTriviaAnswer}
        clues={clues}
        clueStatus={clueStatus}
        wrongAnswerEvent={wrongAnswerEvent}
      />
      {exitFab}
      {rankingFab}
      {hapticsFab}
      {rankingOverlay}
      {blockingOverlay}
      {liveLayer}
    </>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100dvh', display: 'flex', alignItems: 'center',
    justifyContent: 'center', padding: '1rem', background: '#0f172a',
  },
  card: {
    width: '100%', maxWidth: 420, padding: '2rem 1.5rem',
    background: '#1e293b', borderRadius: 16, textAlign: 'center',
    boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
  },
  icon: { fontSize: '3.5rem', marginBottom: '0.75rem' },
  title: { color: '#f8fafc', fontSize: '1.5rem', fontWeight: 800, margin: '0 0 0.5rem' },
  subtitle: { color: '#94a3b8', fontSize: '0.95rem', margin: '0.5rem 0 0' },
  hint: { color: '#64748b', fontSize: '0.85rem', marginTop: '0.5rem' },
  scoreLabel: { color: '#94a3b8', fontSize: '0.85rem', margin: '1rem 0 0.25rem' },
  scoreBig: {
    color: '#f8fafc', fontSize: '3rem', fontWeight: 900, margin: '0 0 0.5rem',
  },
  score: { color: '#94a3b8', fontSize: '1rem', marginTop: '1rem' },
  errorBanner: {
    position: 'fixed', top: 0, left: 0, right: 0,
    background: '#ef4444', color: '#fff',
    padding: '0.75rem 1rem', display: 'flex',
    justifyContent: 'space-between', alignItems: 'center',
    zIndex: 1000, fontSize: '0.9rem',
  },
  errorClose: {
    background: 'none', border: 'none', color: '#fff',
    fontSize: '1rem', cursor: 'pointer', padding: '0 0.25rem',
  },
  rankingFab: {
    position: 'fixed',
    bottom: '1rem',
    right: '1rem',
    width: 56,
    height: 56,
    borderRadius: '50%',
    background: '#6366f1',
    border: 'none',
    color: '#fff',
    fontSize: '1.5rem',
    cursor: 'pointer',
    boxShadow: '0 6px 18px rgba(99,102,241,0.5)',
    // Above Leaflet's panes/controls (max ~1000) so it stays visible on the
    // TreasureHunt map, but below the ranking overlay itself (zIndex 2000).
    zIndex: 1500,
  },
  exitFab: {
    position: 'fixed',
    top: '1rem',
    right: '1rem',
    padding: '0.4rem 0.85rem',
    borderRadius: 999,
    background: 'rgba(15, 23, 42, 0.85)',
    border: '1px solid #334155',
    color: '#cbd5e1',
    fontSize: '0.85rem',
    fontWeight: 600,
    cursor: 'pointer',
    boxShadow: '0 4px 12px rgba(0,0,0,0.35)',
    // Same layer as the ranking FAB so it always stays reachable on the map.
    zIndex: 1500,
    backdropFilter: 'blur(4px)',
    WebkitBackdropFilter: 'blur(4px)',
  },
  viewRankingButton: {
    marginTop: '1.5rem',
    padding: '0.75rem 1.25rem',
    background: '#6366f1',
    color: '#fff',
    border: 'none',
    borderRadius: 10,
    fontSize: '0.95rem',
    fontWeight: 700,
    cursor: 'pointer',
  },
  leaveButton: {
    display: 'block',
    width: '100%',
    marginTop: '0.6rem',
    padding: '0.7rem 1.25rem',
    background: 'transparent',
    color: '#94a3b8',
    border: '1px solid #334155',
    borderRadius: 10,
    fontSize: '0.9rem',
    fontWeight: 600,
    cursor: 'pointer',
  },
};
