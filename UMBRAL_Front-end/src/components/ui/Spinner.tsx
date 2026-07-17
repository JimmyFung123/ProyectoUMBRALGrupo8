import { cn } from './cn';

interface Props {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
  className?: string;
}

const SIZE = {
  sm: 'w-4 h-4 border-2',
  md: 'w-6 h-6 border-2',
  lg: 'w-10 h-10 border-[3px]',
} as const;

/** Simple CSS spinner. Pair it with a status label for accessibility. */
export function Spinner({ size = 'md', label, className }: Props) {
  return (
    <div className={cn('inline-flex items-center gap-2', className)}>
      <span
        role="status"
        aria-label={label ?? 'Cargando'}
        className={cn(
          'inline-block rounded-full border-slate-200 border-t-brand-500 animate-spin',
          SIZE[size],
        )}
      />
      {label && <span className="text-sm text-ink-muted">{label}</span>}
    </div>
  );
}
