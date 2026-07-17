import { useEffect, useState } from 'react';
import type {
  ClueStreamStatus,
  ParticipantStage,
  ReleasedClue,
  TriviaAnswerResult,
  TriviaWrongAnswerPayload,
} from '../types';
import { CluePanel } from './CluePanel';

interface Props {
  stage: ParticipantStage;
  sessionId: string;
  teamId: string;
  nickname: string;
  onAnswered: (result: TriviaAnswerResult) => void;
  onError: (msg: string) => void;
  submitAnswer: (
    sessionId: string,
    teamId: string,
    stageId: string,
    optionId: string,
    participantName?: string,
  ) => Promise<TriviaAnswerResult>;
  clues: ReleasedClue[];
  clueStatus: ClueStreamStatus;
  /** Incoming SignalR event for a wrong answer from any team member. */
  wrongAnswerEvent: TriviaWrongAnswerPayload | null;
}

export function TriviaScreen({
  stage, sessionId, teamId, nickname, onAnswered, onError,
  submitAnswer, clues, clueStatus, wrongAnswerEvent,
}: Props) {
  const [selectedOptionId, setSelectedOptionId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [blockedOptionIds, setBlockedOptionIds] = useState<Set<string>>(new Set());
  const [attemptsUsed, setAttemptsUsed] = useState(0);
  const [maxAttempts, setMaxAttempts] = useState(0);
  const [teammateMsg, setTeammateMsg] = useState<string | null>(null);

  // Reset all attempt state when the stage changes.
  useEffect(() => {
    setBlockedOptionIds(new Set());
    setAttemptsUsed(0);
    setMaxAttempts(0);
    setSelectedOptionId(null);
    setTeammateMsg(null);
  }, [stage.stageId]);

  // Apply incoming SignalR event (teammate or self via broadcast).
  useEffect(() => {
    if (!wrongAnswerEvent) return;
    setBlockedOptionIds(prev => new Set([...prev, wrongAnswerEvent.blockedOptionId]));
    setAttemptsUsed(wrongAnswerEvent.attemptsUsed);
    setMaxAttempts(wrongAnswerEvent.maxAttempts);

    const who = wrongAnswerEvent.participantName
      ? wrongAnswerEvent.participantName
      : 'Un integrante';
    setTeammateMsg(`${who} eligió una respuesta incorrecta. Esa opción quedó bloqueada.`);
    const t = window.setTimeout(() => setTeammateMsg(null), 4000);
    return () => window.clearTimeout(t);
  }, [wrongAnswerEvent]);

  async function handleConfirm() {
    if (!selectedOptionId || submitting) return;
    if (blockedOptionIds.has(selectedOptionId)) return;
    setSubmitting(true);
    try {
      const result = await submitAnswer(sessionId, teamId, stage.stageId, selectedOptionId, nickname);

      if (result.shouldAdvance === false) {
        // Wrong answer, attempts remaining — update local state from the HTTP response
        // (SignalR will also arrive, but this makes the UI instant for the submitter).
        if (result.blockedOptionId) {
          setBlockedOptionIds(prev => new Set([...prev, result.blockedOptionId!]));
        }
        if (result.attemptsUsed !== undefined) setAttemptsUsed(result.attemptsUsed);
        if (result.maxAttempts !== undefined)  setMaxAttempts(result.maxAttempts);
        setSelectedOptionId(null);
        setSubmitting(false);
      } else {
        onAnswered(result);
      }
    } catch {
      onError('No se pudo enviar la respuesta. Intentá de nuevo.');
      setSubmitting(false);
    }
  }

  const attemptsLeft = maxAttempts > 0 ? maxAttempts - attemptsUsed : null;

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        {/* Header */}
        <div style={styles.header}>
          <span style={styles.stageBadge}>Etapa {stage.order}</span>
          {stage.isLastStage && <span style={styles.lastBadge}>Última etapa</span>}
        </div>

        <h2 style={styles.title}>{stage.title}</h2>

        {stage.question && (
          <p style={styles.question}>{stage.question}</p>
        )}

        {/* Attempt counter */}
        {attemptsLeft !== null && (
          <div style={{
            ...styles.attemptsBadge,
            background: attemptsLeft <= 1 ? '#7f1d1d' : '#1e3a5f',
            borderColor: attemptsLeft <= 1 ? '#ef4444' : '#3b82f6',
          }}>
            {attemptsLeft > 0
              ? `Intentos restantes: ${attemptsLeft}`
              : 'Último intento'}
          </div>
        )}

        {/* Teammate wrong answer notification */}
        {teammateMsg && (
          <div style={styles.teammateAlert}>
            ⚠️ {teammateMsg}
          </div>
        )}

        {/* Options */}
        <div style={styles.optionList}>
          {stage.options.map((option) => {
            const isSelected  = selectedOptionId === option.id;
            const isBlocked   = blockedOptionIds.has(option.id);
            const isDisabled  = submitting || isBlocked;

            return (
              <button
                key={option.id}
                onClick={() => !isDisabled && setSelectedOptionId(option.id)}
                disabled={isDisabled}
                style={{
                  ...styles.optionBtn,
                  ...(isBlocked   ? styles.optionBtnBlocked   : {}),
                  ...(isSelected && !isBlocked ? styles.optionBtnSelected : {}),
                  cursor: isDisabled ? 'not-allowed' : 'pointer',
                  opacity: submitting && !isSelected ? 0.5 : 1,
                }}
              >
                {isBlocked && <span style={styles.blockedIcon}>✗ </span>}
                {option.text}
              </button>
            );
          })}
        </div>

        {/* Confirm button */}
        <button
          onClick={handleConfirm}
          disabled={!selectedOptionId || submitting || (selectedOptionId ? blockedOptionIds.has(selectedOptionId) : false)}
          style={{
            ...styles.confirmBtn,
            opacity: !selectedOptionId || submitting ? 0.4 : 1,
            cursor: !selectedOptionId || submitting ? 'not-allowed' : 'pointer',
          }}
        >
          {submitting ? 'Enviando…' : 'Confirmar respuesta'}
        </button>

        {/* Released clues — HU-20 */}
        <CluePanel clues={clues} status={clueStatus} variant="trivia" />
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100dvh', display: 'flex', alignItems: 'center',
    justifyContent: 'center', padding: '1rem', background: '#0f172a',
  },
  card: {
    width: '100%', maxWidth: 480, padding: '2rem 1.5rem',
    background: '#1e293b', borderRadius: 16,
    boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
  },
  header: {
    display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem',
  },
  stageBadge: {
    background: '#334155', color: '#94a3b8',
    fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.6rem',
    borderRadius: 9999, letterSpacing: '0.05em',
  },
  lastBadge: {
    background: '#6366f1', color: '#fff',
    fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.6rem',
    borderRadius: 9999,
  },
  title: {
    color: '#f8fafc', fontSize: '1.4rem', fontWeight: 800,
    margin: '0 0 0.75rem',
  },
  question: {
    color: '#cbd5e1', fontSize: '1rem', lineHeight: 1.6,
    margin: '0 0 1rem',
  },
  attemptsBadge: {
    border: '1px solid',
    borderRadius: 8,
    padding: '0.4rem 0.75rem',
    fontSize: '0.82rem',
    fontWeight: 700,
    color: '#f8fafc',
    marginBottom: '0.75rem',
    textAlign: 'center',
  },
  teammateAlert: {
    background: '#451a03',
    border: '1px solid #f97316',
    borderRadius: 8,
    color: '#fed7aa',
    fontSize: '0.85rem',
    fontWeight: 600,
    padding: '0.5rem 0.75rem',
    marginBottom: '0.75rem',
  },
  optionList: {
    display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '1.5rem',
  },
  optionBtn: {
    width: '100%', padding: '0.9rem 1rem',
    background: '#0f172a', color: '#f8fafc',
    border: '2px solid #334155', borderRadius: 12,
    fontSize: '0.95rem', fontWeight: 600, textAlign: 'left',
    transition: 'border-color 0.15s, background 0.15s',
  },
  optionBtnSelected: {
    borderColor: '#6366f1', background: '#1e1b4b',
    color: '#a5b4fc',
  },
  optionBtnBlocked: {
    borderColor: '#ef4444', background: '#1c0a0a',
    color: '#f87171', textDecoration: 'line-through',
  },
  blockedIcon: {
    fontWeight: 900, marginRight: '0.25rem',
  },
  confirmBtn: {
    width: '100%', padding: '0.9rem',
    background: '#6366f1', color: '#fff',
    border: 'none', borderRadius: 12,
    fontSize: '1rem', fontWeight: 700,
    transition: 'opacity 0.15s',
  },
};
