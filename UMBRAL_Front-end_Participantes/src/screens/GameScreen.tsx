import { useEffect, useRef, useState } from 'react';
import type { ParticipantStage, QrValidationResult, SessionInfo, TriviaAnswerResult } from '../types';
import { getParticipantStage, submitTriviaAnswer, validateQrCode } from '../services/sessionService';
import { useClueStream } from '../services/clueStream';
import { TriviaScreen } from './TriviaScreen';
import { TreasureHuntScreen } from './TreasureHuntScreen';

type TeamProp = { teamId: string; isLeader: boolean };

interface Props {
  session: SessionInfo;
  team: TeamProp;
  nickname: string;
  initialStage: ParticipantStage;
}

type StageResult = TriviaAnswerResult | QrValidationResult;

type AnswerState =
  | { phase: 'answering' }
  | { phase: 'result'; result: StageResult };

const POLL_INTERVAL_MS = 8_000;
const RESULT_DISPLAY_MS = 3_000;

export function GameScreen({ session, team, nickname, initialStage }: Props) {
  const [stage, setStage] = useState<ParticipantStage>(initialStage);
  const [answerState, setAnswerState] = useState<AnswerState>({ phase: 'answering' });
  const [error, setError] = useState<string | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Real-time clue stream (HU-20): SignalR + API resync after reconnect.
  const { clues, status: clueStatus, resetClues } = useClueStream({
    sessionId: session.id,
    teamId: team.teamId,
  });

  // Poll for stage updates (handles operator force-advance, session pause, etc.)
  useEffect(() => {
    function startPolling() {
      intervalRef.current = setInterval(async () => {
        try {
          const updated = await getParticipantStage(session.id, team.teamId);
          // Only update if still in answering phase to avoid overwriting result card
          setStage((prev) => {
            if (updated.currentStageOrder !== prev.currentStageOrder) {
              setAnswerState({ phase: 'answering' });
              // Clues belong to a specific stage — clear them on advance.
              resetClues();
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
    setAnswerState({ phase: 'result', result });

    // After 3s, advance to next stage view via polling refresh
    setTimeout(async () => {
      try {
        const updated = await getParticipantStage(session.id, team.teamId);
        setStage(updated);
        setAnswerState({ phase: 'answering' });
        resetClues();
      } catch {
        setAnswerState({ phase: 'answering' });
      }
    }, RESULT_DISPLAY_MS);
  }

  // Completed: team has gone past the last stage
  if (stage.type === 'Completed' || (stage.isLastStage && answerState.phase === 'result' && answerState.result.nextStageOrder > stage.order)) {
    return (
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
        </div>
      </div>
    );
  }

  // Result card (correct / incorrect feedback)
  if (answerState.phase === 'result') {
    const { result } = answerState;
    return (
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
    );
  }

  // Waiting for session to start / stage assigned
  if (stage.type === 'Waiting') {
    return (
      <div style={styles.container}>
        <div style={styles.card}>
          <div style={styles.icon}>⏳</div>
          <h2 style={styles.title}>Esperando inicio…</h2>
          <p style={styles.subtitle}>La actividad comenzará en breve.</p>
        </div>
      </div>
    );
  }

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
        onAnswered={handleAnswered}
        onError={setError}
        submitAnswer={submitTriviaAnswer}
        clues={clues}
        clueStatus={clueStatus}
      />
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
};
