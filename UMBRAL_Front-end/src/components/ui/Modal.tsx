import { useEffect, type ReactNode } from 'react';
import { cn } from './cn';

interface Props {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  /** Footer slot. Typically holds a pair of Button components (Cancelar / Confirmar). */
  footer?: ReactNode;
  children?: ReactNode;
  /** sm = 24rem · md = 32rem · lg = 42rem. Defaults to md. */
  size?: 'sm' | 'md' | 'lg';
}

const SIZES = {
  sm: 'max-w-sm',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
} as const;

/**
 * Lightweight modal dialog. Uses a fixed full-screen overlay + a centred card.
 * No portal — relies on `position: fixed` + a high z-index, which is enough
 * for the operator app since no parent uses `transform` or `contain`.
 */
export function Modal({ open, onClose, title, description, footer, children, size = 'md' }: Props) {
  // ESC to close. Hooked on mount so it cleans up automatically when the
  // modal is unmounted.
  useEffect(() => {
    if (!open) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', handleKey);
    document.body.style.overflow = 'hidden';
    return () => {
      document.removeEventListener('keydown', handleKey);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-ink/40 backdrop-blur-[2px]"
      onClick={onClose}
      role="presentation"
    >
      <div
        role="dialog"
        aria-modal="true"
        className={cn(
          'w-full bg-surface-card rounded-lg shadow-floating border border-slate-200',
          'flex flex-col max-h-[90vh]',
          SIZES[size],
        )}
        onClick={e => e.stopPropagation()}
      >
        <header className="flex items-start justify-between gap-4 px-5 pt-5 pb-3 border-b border-slate-200">
          <div className="min-w-0">
            <h3 className="text-base font-semibold text-ink leading-tight">{title}</h3>
            {description && (
              <p className="text-sm text-ink-muted mt-1 leading-snug">{description}</p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar"
            className="text-ink-muted hover:text-ink shrink-0 -mr-1 p-1 leading-none text-lg"
          >
            ✕
          </button>
        </header>
        <div className="px-5 py-4 overflow-y-auto flex-1">
          {children}
        </div>
        {footer && (
          <footer className="flex items-center justify-end gap-2 px-5 py-3 border-t border-slate-200 bg-surface-subtle rounded-b-lg">
            {footer}
          </footer>
        )}
      </div>
    </div>
  );
}
