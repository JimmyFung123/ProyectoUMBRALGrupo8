import type { HTMLAttributes, ReactNode } from 'react';
import { cn } from './cn';

/**
 * Surface primitive used everywhere — sessions list, dashboard panels, modals.
 * `padded` controls whether the card adds its own breathing room; set it to
 * false when children manage their own padding (e.g. a table that wants to
 * flush against the edges).
 */
interface CardProps extends HTMLAttributes<HTMLDivElement> {
  padded?: boolean;
  elevated?: boolean;
  /** Accent stripe on the left edge, used by status cards. */
  accent?: 'brand' | 'success' | 'warning' | 'danger' | 'info' | null;
}

const ACCENT: Record<string, string> = {
  brand:   'border-l-4 border-l-brand-500',
  success: 'border-l-4 border-l-success-600',
  warning: 'border-l-4 border-l-warning-600',
  danger:  'border-l-4 border-l-danger-600',
  info:    'border-l-4 border-l-info-600',
};

export function Card({
  padded = true,
  elevated = false,
  accent = null,
  className,
  children,
  ...rest
}: CardProps) {
  return (
    <div
      className={cn(
        'bg-surface-card border border-slate-200 rounded-md',
        elevated ? 'shadow-elevated' : 'shadow-card',
        padded && 'p-4 md:p-5',
        accent && ACCENT[accent],
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}

interface CardHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  /** Slot for buttons / badges on the right side of the header row. */
  actions?: ReactNode;
  className?: string;
}

export function CardHeader({ title, description, actions, className }: CardHeaderProps) {
  return (
    <div className={cn('flex items-start justify-between gap-4 mb-3', className)}>
      <div className="min-w-0">
        <h3 className="text-base font-semibold text-ink leading-tight">{title}</h3>
        {description && (
          <p className="text-sm text-ink-muted mt-1 leading-snug">{description}</p>
        )}
      </div>
      {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
    </div>
  );
}

interface CardSectionProps extends HTMLAttributes<HTMLDivElement> {
  divided?: boolean;
}

/** Vertical sub-section inside a card. `divided` adds the top border separator. */
export function CardSection({ divided, className, children, ...rest }: CardSectionProps) {
  return (
    <div
      className={cn(divided && 'border-t border-slate-200 pt-4 mt-4', className)}
      {...rest}
    >
      {children}
    </div>
  );
}
