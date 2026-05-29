import { type ReactNode } from 'react';
import { cn } from './cn';

interface Tab {
  key: string;
  label: ReactNode;
  /** Optional icon string / emoji rendered before the label. */
  icon?: ReactNode;
  /** Disabled tabs are visible but not selectable (e.g. role-gated). */
  disabled?: boolean;
}

interface Props {
  tabs: Tab[];
  active: string;
  onChange: (key: string) => void;
  className?: string;
}

/**
 * Tab bar used at the top of the operator app. Pure presentational — the
 * caller renders the matching panel based on `active`.
 */
export function Tabs({ tabs, active, onChange, className }: Props) {
  return (
    <div
      role="tablist"
      className={cn(
        'flex items-center gap-1 border-b border-slate-200 bg-surface-card px-4',
        className,
      )}
    >
      {tabs.map(tab => {
        const isActive = tab.key === active;
        return (
          <button
            key={tab.key}
            role="tab"
            type="button"
            aria-selected={isActive}
            aria-disabled={tab.disabled}
            disabled={tab.disabled}
            onClick={() => !tab.disabled && onChange(tab.key)}
            className={cn(
              'relative flex items-center gap-2 px-3 py-3 text-sm font-medium transition-colors',
              'border-b-2 -mb-px',
              isActive
                ? 'border-brand-500 text-brand-700'
                : 'border-transparent text-ink-muted hover:text-ink hover:border-slate-300',
              tab.disabled && 'opacity-50 cursor-not-allowed',
            )}
          >
            {tab.icon && <span aria-hidden>{tab.icon}</span>}
            <span>{tab.label}</span>
          </button>
        );
      })}
    </div>
  );
}
