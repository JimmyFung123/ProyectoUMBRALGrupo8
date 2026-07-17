import type { ReactNode } from 'react';

interface Props {
  icon?: ReactNode;
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
}

/**
 * Friendly "nothing here yet" panel. Use it inside Cards for empty lists,
 * empty filters, "still loading initial data" etc.
 */
export function EmptyState({ icon = '✨', title, description, action }: Props) {
  return (
    <div className="flex flex-col items-center text-center py-10 px-4 gap-2">
      <div className="text-3xl" aria-hidden>{icon}</div>
      <h4 className="text-base font-semibold text-ink">{title}</h4>
      {description && (
        <p className="text-sm text-ink-muted max-w-md leading-snug">{description}</p>
      )}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
