import type { ReactNode } from 'react';
import { cn } from './cn';

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

interface Props {
  tone?: AlertTone;
  title?: ReactNode;
  children?: ReactNode;
  onDismiss?: () => void;
  className?: string;
  icon?: ReactNode;
}

const TONES: Record<AlertTone, { bg: string; border: string; text: string; icon: string }> = {
  info:    { bg: 'bg-info-50',    border: 'border-info-500/40',    text: 'text-info-700',    icon: 'ℹ️' },
  success: { bg: 'bg-success-50', border: 'border-success-500/40', text: 'text-success-700', icon: '✅' },
  warning: { bg: 'bg-warning-50', border: 'border-warning-500/40', text: 'text-warning-700', icon: '⚠️' },
  danger:  { bg: 'bg-danger-50',  border: 'border-danger-500/40',  text: 'text-danger-700',  icon: '⛔' },
};

/**
 * Inline notification used for forms, banner-style feedback after an action,
 * and empty/error states inside cards. Keep it concise — for full-screen
 * blocking messages use a Modal instead.
 */
export function Alert({ tone = 'info', title, children, onDismiss, className, icon }: Props) {
  const t = TONES[tone];
  return (
    <div
      role={tone === 'danger' || tone === 'warning' ? 'alert' : 'status'}
      className={cn(
        'flex items-start gap-3 rounded border px-3 py-2.5 text-sm',
        t.bg, t.border, t.text,
        className,
      )}
    >
      <span aria-hidden className="text-base leading-none mt-0.5">{icon ?? t.icon}</span>
      <div className="flex-1 min-w-0">
        {title && <div className="font-semibold leading-snug">{title}</div>}
        {children && <div className={cn('leading-snug', !!title && 'mt-0.5')}>{children}</div>}
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Cerrar"
          className="text-ink-muted hover:text-ink shrink-0 -mr-1 -mt-1 p-1 leading-none"
        >
          ✕
        </button>
      )}
    </div>
  );
}
