import type { ReactNode } from 'react';
import { cn } from './cn';

export type BadgeTone = 'neutral' | 'brand' | 'success' | 'warning' | 'danger' | 'info';

interface Props {
  tone?: BadgeTone;
  children: ReactNode;
  className?: string;
  /** Subtle = pale background; solid = filled chip (for high-emphasis badges). */
  variant?: 'subtle' | 'solid';
}

const SUBTLE: Record<BadgeTone, string> = {
  neutral: 'bg-slate-100 text-ink-soft',
  brand:   'bg-brand-50    text-brand-700',
  success: 'bg-success-50  text-success-700',
  warning: 'bg-warning-50  text-warning-700',
  danger:  'bg-danger-50   text-danger-700',
  info:    'bg-info-50     text-info-700',
};

const SOLID: Record<BadgeTone, string> = {
  neutral: 'bg-slate-700  text-white',
  brand:   'bg-brand-500   text-white',
  success: 'bg-success-600 text-white',
  warning: 'bg-warning-600 text-white',
  danger:  'bg-danger-600  text-white',
  info:    'bg-info-600    text-white',
};

export function Badge({ tone = 'neutral', variant = 'subtle', className, children }: Props) {
  const palette = variant === 'solid' ? SOLID[tone] : SUBTLE[tone];
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded px-2 py-0.5 text-xs font-semibold leading-snug whitespace-nowrap',
        palette,
        className,
      )}
    >
      {children}
    </span>
  );
}
