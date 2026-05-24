import { useState } from 'react';
import type { ParticipantStage, TriviaAnswerResult } from '../types';

interface Props {
  stage: ParticipantStage;
  sessionId: string;
  teamId: string;
  onAnswered: (result: TriviaAnswerResult) => void;
  onError: (msg: string) => void;
  submitAnswer: (
    sessionId: string,
    teamId: string,
    stageId: string,
    optionId: string,
  ) => Promise<TriviaAnswerResult>;
}

export function TriviaScreen({ stage, sessionId, teamId, onAnswered, onError, submitAnswer }: Props) {
  const [selectedOptionId, setSelectedOptionId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleConfirm() {
    if (!selectedOptionId || submitting) return;
    setSubmitting(true);
    try {
      const result = await submitAnswer(sessionId, teamId, stage.stageId, selectedOptionId);
      onAnswered(result);
    } catch {
      onError('No se pudo enviar la respuesta. Intentá de nuevo.');
      setSubmitting(false);
    }
  }

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

        {/* Options */}
        <div style={styles.optionList}>
          {stage.options.map((option) => {
            const isSelected = selectedOptionId === option.id;
            return (
              <button
                key={option.id}
                onClick={() => !submitting && setSelectedOptionId(option.id)}
                style={{
                  ...styles.optionBtn,
                  ...(isSelected ? styles.optionBtnSelected : {}),
                  cursor: submitting ? 'not-allowed' : 'pointer',
                  opacity: submitting && !isSelected ? 0.5 : 1,
                }}
                disabled={submitting}
              >
                {option.text}
              </button>
            );
          })}
        </div>

        {/* Confirm button */}
        <button
          onClick={handleConfirm}
          disabled={!selectedOptionId || submitting}
          style={{
            ...styles.confirmBtn,
            opacity: !selectedOptionId || submitting ? 0.4 : 1,
            cursor: !selectedOptionId || submitting ? 'not-allowed' : 'pointer',
          }}
        >
          {submitting ? 'Enviando…' : 'Confirmar respuesta'}
        </button>
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
    margin: '0 0 1.5rem',
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
  confirmBtn: {
    width: '100%', padding: '0.9rem',
    background: '#6366f1', color: '#fff',
    border: 'none', borderRadius: 12,
    fontSize: '1rem', fontWeight: 700,
    transition: 'opacity 0.15s',
  },
};
