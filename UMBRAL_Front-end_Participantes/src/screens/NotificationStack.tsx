import { useCallback, useEffect, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';

/**
 * HU-28 — themed live notifications shown to participants.
 *
 * Variants:
 *   - clue       💡  blue   — automatic / operator clue delivered
 *   - rank       🏆  amber  — team climbed the leaderboard
 *   - stage      ✅  green  — stage completed
 *   - wrong     ❌  rose   — wrong answer / wrong QR
 *   - message    📩  violet — operator-authored broadcast
 *
 * The stack auto-dismisses each toast after its lifetime and supports manual
 * dismissal. Vibration is fired by the caller (vibrate.ts helper) so this
 * stays purely visual.
 */

export type ToastVariant = 'clue' | 'rank' | 'stage' | 'wrong' | 'message' | 'penalty';

export interface Toast {
  id: string;
  variant: ToastVariant;
  title: string;
  body?: string;
  /** Milliseconds before auto-dismiss; defaults to 4500. */
  durationMs?: number;
}

interface Props {
  toasts: Toast[];
  onDismiss: (id: string) => void;
}

const STYLE: Record<ToastVariant, { icon: string; accent: string; bg: string }> = {
  clue:    { icon: '💡', accent: '#38bdf8', bg: '#0c4a6e' },
  rank:    { icon: '🏆', accent: '#fbbf24', bg: '#78350f' },
  stage:   { icon: '✅', accent: '#34d399', bg: '#064e3b' },
  wrong:   { icon: '❌', accent: '#fb7185', bg: '#7f1d1d' },
  message: { icon: '📩', accent: '#a78bfa', bg: '#3b0764' },
  penalty: { icon: '🚨', accent: '#f97316', bg: '#7c2d12' },
};

export function NotificationStack({ toasts, onDismiss }: Props) {
  return (
    <div
      style={{
        position: 'fixed',
        top: '4.5rem',
        left: '50%',
        transform: 'translateX(-50%)',
        zIndex: 2500,
        width: '100%',
        maxWidth: 360,
        display: 'flex',
        flexDirection: 'column',
        gap: '0.5rem',
        pointerEvents: 'none',
        padding: '0 0.75rem',
      }}
    >
      <AnimatePresence initial={false}>
        {toasts.map(toast => (
          <ToastCard key={toast.id} toast={toast} onDismiss={onDismiss} />
        ))}
      </AnimatePresence>
    </div>
  );
}

function ToastCard({ toast, onDismiss }: { toast: Toast; onDismiss: (id: string) => void }) {
  const style = STYLE[toast.variant];
  const dismiss = useCallback(() => onDismiss(toast.id), [onDismiss, toast.id]);

  useEffect(() => {
    const timer = window.setTimeout(dismiss, toast.durationMs ?? 4500);
    return () => window.clearTimeout(timer);
  }, [dismiss, toast.durationMs]);

  return (
    <motion.div
      role="status"
      initial={{ opacity: 0, y: -16, scale: 0.96 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={{ opacity: 0, y: -12, scale: 0.96 }}
      transition={{ type: 'spring', stiffness: 360, damping: 28 }}
      onClick={dismiss}
      style={{
        background: style.bg,
        borderLeft: `4px solid ${style.accent}`,
        color: '#f8fafc',
        borderRadius: 12,
        padding: '0.8rem 1rem',
        boxShadow: '0 12px 28px rgba(0,0,0,0.45)',
        display: 'flex',
        gap: '0.75rem',
        alignItems: 'flex-start',
        pointerEvents: 'auto',
        cursor: 'pointer',
        backdropFilter: 'blur(4px)',
        WebkitBackdropFilter: 'blur(4px)',
      }}
    >
      <span style={{ fontSize: '1.4rem', lineHeight: 1, marginTop: 1 }} aria-hidden>
        {style.icon}
      </span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontWeight: 700, fontSize: '0.95rem', color: style.accent, lineHeight: 1.2 }}>
          {toast.title}
        </div>
        {toast.body && (
          <div style={{ fontSize: '0.82rem', color: '#e2e8f0', marginTop: 2, lineHeight: 1.35 }}>
            {toast.body}
          </div>
        )}
      </div>
    </motion.div>
  );
}

/**
 * Tiny hook so screens don't have to manage the toast list manually.
 * Returns a `push` function and the props for `<NotificationStack />`.
 */
export function useToastStack() {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const push = useCallback((toast: Omit<Toast, 'id'>) => {
    const id = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    setToasts(prev => [...prev, { ...toast, id }]);
  }, []);

  const dismiss = useCallback((id: string) => {
    setToasts(prev => prev.filter(t => t.id !== id));
  }, []);

  return { toasts, push, dismiss };
}
