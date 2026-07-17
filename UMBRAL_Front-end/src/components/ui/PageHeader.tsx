import type { ReactNode } from 'react';
import { cn } from './cn';

interface Props {
  title: ReactNode;
  description?: ReactNode;
  /** Right-hand side: buttons / filters / status badge. */
  actions?: ReactNode;
  /** Pre-title eyebrow (e.g. "📊 Dashboard"). */
  eyebrow?: ReactNode;
  className?: string;
}

/**
 * Top-of-page header used by every top-level screen (sessions, missions,
 * statistics, sync health, users, audit). Establishes a consistent breathing
 * room and title hierarchy across the operator app.
 */
export function PageHeader({ title, description, actions, eyebrow, className }: Props) {
  return (
    <header className={cn('flex items-start justify-between gap-4 mb-5', className)}>
      <div className="min-w-0">
        {eyebrow && (
          <div className="text-xs uppercase tracking-wider text-ink-muted font-semibold mb-1">
            {eyebrow}
          </div>
        )}
        <h1 className="text-xl md:text-2xl font-bold text-ink leading-tight">{title}</h1>
        {description && (
          <p className="text-sm text-ink-muted mt-1.5 leading-snug max-w-2xl">{description}</p>
        )}
      </div>
      {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
    </header>
  );
}
