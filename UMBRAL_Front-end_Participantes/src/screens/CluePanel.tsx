import type { ClueStreamStatus, ReleasedClue } from '../types';

interface Props {
  clues: ReleasedClue[];
  status: ClueStreamStatus;
  /** "trivia" → stacked text cards · "treasure" → map-overlay banners */
  variant: 'trivia' | 'treasure';
}

const STATUS_LABEL: Record<ClueStreamStatus, string> = {
  connecting: 'Conectando…',
  connected: 'En vivo',
  reconnecting: 'Reconectando…',
  disconnected: 'Desconectado',
};

const STATUS_DOT_COLOR: Record<ClueStreamStatus, string> = {
  connecting: '#fbbf24',
  connected: '#34d399',
  reconnecting: '#fbbf24',
  disconnected: '#f87171',
};

export function ConnectionBadge({ status }: { status: ClueStreamStatus }) {
  return (
    <span style={styles.badge} title={`Estado de la conexión en vivo: ${STATUS_LABEL[status]}`}>
      <span style={{ ...styles.dot, background: STATUS_DOT_COLOR[status] }} />
      {STATUS_LABEL[status]}
    </span>
  );
}

export function CluePanel({ clues, status, variant }: Props) {
  if (clues.length === 0) {
    return (
      <div style={styles.empty}>
        <ConnectionBadge status={status} />
        <p style={styles.emptyText}>
          {variant === 'trivia'
            ? 'Aún no recibiste pistas. Si te quedas estancado, llegarán automáticamente.'
            : 'Aún no recibiste pistas. Las próximas aparecerán acá.'}
        </p>
      </div>
    );
  }

  return (
    <div style={styles.panel}>
      <div style={styles.header}>
        <span style={styles.title}>💡 Pistas recibidas ({clues.length})</span>
        <ConnectionBadge status={status} />
      </div>
      <div style={styles.list}>
        {clues.map((clue) => {
          const isGeo = clue.content == null && clue.latitude != null && clue.longitude != null;
          return (
            <div key={clue.id} style={styles.card}>
              <span style={styles.cardNumber}>#{clue.order}</span>
              <p style={styles.cardText}>
                {clue.content
                  ? clue.content
                  : isGeo
                    ? `📍 Nueva zona en el mapa — radio ${clue.radiusMeters ?? '?'} m`
                    : '(sin contenido)'}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  panel: {
    background: '#0f172a',
    borderRadius: 12,
    border: '1px solid #334155',
    padding: '0.75rem',
    marginTop: '1rem',
  },
  header: {
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: '0.5rem',
  },
  title: {
    color: '#f8fafc',
    fontSize: '0.85rem',
    fontWeight: 700,
  },
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: '0.5rem',
    maxHeight: 200,
    overflowY: 'auto',
  },
  card: {
    background: '#1e293b',
    borderRadius: 10,
    padding: '0.6rem 0.75rem',
    border: '1px solid #334155',
    display: 'flex',
    gap: '0.6rem',
    alignItems: 'flex-start',
  },
  cardNumber: {
    background: '#6366f1',
    color: '#fff',
    fontSize: '0.7rem',
    fontWeight: 800,
    padding: '0.15rem 0.4rem',
    borderRadius: 6,
    flexShrink: 0,
    marginTop: '0.1rem',
  },
  cardText: {
    color: '#cbd5e1',
    fontSize: '0.85rem',
    lineHeight: 1.45,
    margin: 0,
    whiteSpace: 'pre-wrap',
  },
  empty: {
    background: '#0f172a',
    borderRadius: 12,
    border: '1px dashed #334155',
    padding: '0.75rem',
    marginTop: '1rem',
    display: 'flex',
    flexDirection: 'column',
    gap: '0.5rem',
    alignItems: 'flex-start',
  },
  emptyText: {
    color: '#64748b',
    fontSize: '0.8rem',
    margin: 0,
  },
  badge: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '0.35rem',
    background: '#1e293b',
    color: '#cbd5e1',
    fontSize: '0.7rem',
    fontWeight: 700,
    padding: '0.2rem 0.55rem',
    borderRadius: 9999,
    border: '1px solid #334155',
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: '50%',
    display: 'inline-block',
  },
};
