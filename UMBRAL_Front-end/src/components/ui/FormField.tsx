import type { InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes, ReactNode } from 'react';
import { cn } from './cn';

// ── Shared label + helper text + error layout. Every form control wraps in this.

interface FieldShellProps {
  label?: ReactNode;
  hint?: ReactNode;
  error?: ReactNode;
  required?: boolean;
  htmlFor?: string;
  className?: string;
  children: ReactNode;
}

export function FormField({ label, hint, error, required, htmlFor, className, children }: FieldShellProps) {
  return (
    <div className={cn('flex flex-col gap-1', className)}>
      {label && (
        <label htmlFor={htmlFor} className="text-sm font-medium text-ink leading-snug">
          {label}
          {required && <span className="text-danger-600 ml-0.5">*</span>}
        </label>
      )}
      {children}
      {error
        ? <p className="text-xs text-danger-600 leading-snug">{error}</p>
        : hint && <p className="text-xs text-ink-muted leading-snug">{hint}</p>}
    </div>
  );
}

// ── Inputs ────────────────────────────────────────────────────────────────────

const CONTROL_BASE =
  'w-full rounded border bg-white px-3 py-2 text-sm text-ink ' +
  'placeholder:text-ink-subtle ' +
  'transition-colors transition-shadow ' +
  'disabled:bg-slate-50 disabled:text-ink-muted disabled:cursor-not-allowed';

const CONTROL_BORDER = 'border-slate-300 hover:border-slate-400';
const CONTROL_ERROR  = 'border-danger-500 focus:border-danger-500';

export interface TextInputProps extends InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
}

export function TextInput({ invalid, className, ...rest }: TextInputProps) {
  return (
    <input
      className={cn(CONTROL_BASE, invalid ? CONTROL_ERROR : CONTROL_BORDER, className)}
      {...rest}
    />
  );
}

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  invalid?: boolean;
}

export function Textarea({ invalid, className, rows = 4, ...rest }: TextareaProps) {
  return (
    <textarea
      rows={rows}
      className={cn(CONTROL_BASE, 'resize-y min-h-[5rem]', invalid ? CONTROL_ERROR : CONTROL_BORDER, className)}
      {...rest}
    />
  );
}

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  invalid?: boolean;
}

export function Select({ invalid, className, children, ...rest }: SelectProps) {
  return (
    <select
      className={cn(CONTROL_BASE, 'pr-8 bg-white', invalid ? CONTROL_ERROR : CONTROL_BORDER, className)}
      {...rest}
    >
      {children}
    </select>
  );
}
