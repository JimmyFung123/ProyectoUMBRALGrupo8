import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn } from './cn';

/**
 * Primary action surface across the entire operator app. Variants stay
 * intentionally narrow: anything outside the four below should be modelled
 * as a different component, not as a new button colour.
 */
export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md';

interface Props extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Optional icon rendered before the label. Pass any ReactNode (emoji / svg). */
  leadingIcon?: ReactNode;
  trailingIcon?: ReactNode;
  fullWidth?: boolean;
  children: ReactNode;
}

const BASE =
  'inline-flex items-center justify-center gap-2 font-medium rounded ' +
  'transition-colors transition-shadow disabled:opacity-50 disabled:cursor-not-allowed ' +
  'whitespace-nowrap select-none';

const VARIANTS: Record<ButtonVariant, string> = {
  primary:
    'bg-brand-500 text-white hover:bg-brand-600 active:bg-brand-700 shadow-card ' +
    'disabled:hover:bg-brand-500',
  secondary:
    'bg-white text-ink border border-slate-300 hover:bg-slate-50 active:bg-slate-100 ' +
    'shadow-card disabled:hover:bg-white',
  ghost:
    'bg-transparent text-ink-soft hover:bg-slate-100 active:bg-slate-200',
  danger:
    'bg-danger-600 text-white hover:bg-danger-700 active:bg-danger-700 shadow-card ' +
    'disabled:hover:bg-danger-600',
};

const SIZES: Record<ButtonSize, string> = {
  sm: 'h-8 px-3 text-sm',
  md: 'h-10 px-4 text-sm',
};

export function Button({
  variant = 'primary',
  size = 'md',
  leadingIcon,
  trailingIcon,
  fullWidth,
  className,
  children,
  type = 'button',
  ...rest
}: Props) {
  return (
    <button
      type={type}
      className={cn(
        BASE,
        VARIANTS[variant],
        SIZES[size],
        fullWidth && 'w-full',
        className,
      )}
      {...rest}
    >
      {leadingIcon && <span className="shrink-0">{leadingIcon}</span>}
      <span>{children}</span>
      {trailingIcon && <span className="shrink-0">{trailingIcon}</span>}
    </button>
  );
}
