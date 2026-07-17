import type { CSSProperties } from 'react';

/**
 * Full-screen overlay that blocks gameplay when the operator pauses,
 * finalizes or cancels the session.
 *
 * Backed by the `sessionStatus` field that the participant-stage polling
 * already returns (HU-11 / HU-18 reject any answer when the session is not
 * InProgress, so blocking the UI here is purely defensive — but it gives the
 * participant immediate, immersive feedback instead of a confusing API error).
 *
 * The overlay sits above the game canvas (zIndex 3000) so it stays on top
 * of the map, the ranking FAB and the error banner.
 */
export type BlockingSessionStatus = 'Paused' | 'Completed' | 'Cancelled';

interface Props {
  status: BlockingSessionStatus;
  /** Called when the participant clicks "Volver al inicio". Only rendered for
   *  terminal statuses (Completed / Cancelled). For Paused there is no exit —
   *  the operator can resume at any moment and we want the team to stay. */
  onLeaveSession?: () => void;
  /** Optional: lets the user open the ranking from the Completed overlay,
   *  reusing the same handler the game screen already wires up. */
  onViewRanking?: () => void;
}

interface OverlayCopy {
  icon: string;
  title: string;
  message: string;
  accent: string;
  /** Whether the user can leave from this screen. */
  showLeaveButton: boolean;
}

const COPY: Record<BlockingSessionStatus, OverlayCopy> = {
  Paused: {
    icon: '⏸️',
    title: 'Sesión pausada',
    message: 'El operador puso la sesión en pausa. No se aceptarán respuestas ni escaneos hasta que se reanude. Quedate atento.',
    accent: '#fbbf24',
    showLeaveButton: false,
  },
  Completed: {
    icon: '🏁',
    title: 'Sesión finalizada',
    message: 'El operador cerró la sesión. El ranking definitivo ya quedó publicado.',
    accent: '#34d399',
    showLeaveButton: true,
  },
  Cancelled: {
    icon: '🚫',
    title: 'Sesión cancelada',
    message: 'El operador canceló la sesión. Volvé a la pantalla de inicio para unirte a otra cuando esté disponible.',
    accent: '#f87171',
    showLeaveButton: true,
  },
};

export function SessionStatusOverlay({ status, onLeaveSession, onViewRanking }: Props) {
  const copy = COPY[status];

  return (
    <div style={styles.overlay} role="dialog" aria-modal="true">
      <div style={{ ...styles.card, borderTopColor: copy.accent }}>
        <div style={{ ...styles.icon, color: copy.accent }}>{copy.icon}</div>
        <h2 style={{ ...styles.title, color: copy.accent }}>{copy.title}</h2>
        <p style={styles.message}>{copy.message}</p>

        {status === 'Paused' && (
          <div style={styles.pulse}>
            <span style={styles.pulseDot} />
            <span style={styles.pulseText}>Esperando que se reanude…</span>
          </div>
        )}

        {status === 'Completed' && onViewRanking && (
          <button onClick={onViewRanking} style={styles.primaryBtn}>
            🏆 Ver ranking final
          </button>
        )}

        {copy.showLeaveButton && onLeaveSession && (
          <button onClick={onLeaveSession} style={styles.secondaryBtn}>
            ↩ Volver al inicio
          </button>
        )}
      </div>
    </div>
  );
}

const styles: Record<string, CSSProperties> = {
  overlay: {
    position: 'fixed',
    inset: 0,
    background: 'rgba(15, 23, 42, 0.92)',
    backdropFilter: 'blur(6px)',
    WebkitBackdropFilter: 'blur(6px)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '1rem',
    // Above the in-game ranking overlay (zIndex 2000) and the ranking FAB
    // (zIndex 1500) — this is the highest layer on the participant screen.
    zIndex: 3000,
  },
  card: {
    width: '100%',
    maxWidth: 420,
    padding: '2rem 1.5rem',
    background: '#1e293b',
    borderRadius: 16,
    borderTop: '4px solid transparent',
    textAlign: 'center',
    boxShadow: '0 12px 40px rgba(0,0,0,0.55)',
  },
  icon: { fontSize: '3.5rem', marginBottom: '0.5rem' },
  title: { fontSize: '1.5rem', fontWeight: 800, margin: '0 0 0.75rem' },
  message: { color: '#cbd5e1', fontSize: '0.95rem', lineHeight: 1.5, margin: '0 0 1.5rem' },
  pulse: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '0.5rem',
    padding: '0.6rem 1rem',
    background: '#0f172a',
    borderRadius: 999,
  },
  pulseDot: {
    width: 10,
    height: 10,
    borderRadius: '50%',
    background: '#fbbf24',
    boxShadow: '0 0 0 0 rgba(251, 191, 36, 0.6)',
    animation: 'umbral-pulse 1.4s ease-out infinite',
  },
  pulseText: { color: '#94a3b8', fontSize: '0.85rem' },
  primaryBtn: {
    display: 'block',
    width: '100%',
    marginBottom: '0.6rem',
    padding: '0.75rem 1.25rem',
    background: '#6366f1',
    color: '#fff',
    border: 'none',
    borderRadius: 10,
    fontSize: '0.95rem',
    fontWeight: 700,
    cursor: 'pointer',
  },
  secondaryBtn: {
    display: 'block',
    width: '100%',
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
