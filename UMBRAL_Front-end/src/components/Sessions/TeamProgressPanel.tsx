import type { TeamProgressDto } from '../../types/team';

interface Props {
  teams: TeamProgressDto[];
  /** Called when the operator requests to send a hint to a specific team. */
  onSendHint?: (teamId: string, teamName: string) => void;
}

const RANK_COLORS: Record<number, string> = {
  1: '#f9a825',
  2: '#90a4ae',
  3: '#a1887f',
};

function RankBadge({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
  return (
    <span style={{
      display: 'inline-flex',
      alignItems: 'center',
      gap: '0.25rem',
      fontWeight: 'bold',
      color: RANK_COLORS[rank] ?? '#555',
      minWidth: 28,
    }}>
      {medal ?? `#${rank}`}
    </span>
  );
}

function ConnectionDot({ connected }: { connected: boolean }) {
  return (
    <span
      title={connected ? 'Conectado' : 'Desconectado'}
      style={{
        display: 'inline-block',
        width: 10,
        height: 10,
        borderRadius: '50%',
        background: connected ? '#27ae60' : '#bdc3c7',
        marginRight: '0.4rem',
      }}
    />
  );
}

// ── TeamProgressPanel ─────────────────────────────────────────────────────────

export function TeamProgressPanel({ teams, onSendHint }: Props) {
  if (teams.length === 0) {
    return (
      <p style={{ color: '#999', fontSize: '0.9rem', margin: 0 }}>
        Aún no hay equipos inscritos en esta sesión.
      </p>
    );
  }

  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
        <thead>
          <tr style={{ background: '#f5f5f5', textAlign: 'left' }}>
            <th style={thStyle}>Pos.</th>
            <th style={thStyle}>Equipo</th>
            <th style={{ ...thStyle, textAlign: 'center' }}>Etapa</th>
            <th style={{ ...thStyle, textAlign: 'right' }}>Puntos</th>
            <th style={{ ...thStyle, textAlign: 'center' }}>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {teams.map(team => (
            <tr
              key={team.id}
              style={{
                borderBottom: '1px solid #eee',
                background: team.rank === 1 ? '#fffde7' : undefined,
              }}
            >
              <td style={tdStyle}>
                <RankBadge rank={team.rank} />
              </td>
              <td style={tdStyle}>
                <ConnectionDot connected={team.isConnected} />
                <strong>{team.name}</strong>
              </td>
              <td style={{ ...tdStyle, textAlign: 'center' }}>
                {team.currentStageOrder === 0
                  ? <span style={{ color: '#aaa' }}>—</span>
                  : `Etapa ${team.currentStageOrder}`}
              </td>
              <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 'bold', color: '#2c3e50' }}>
                {team.score.toLocaleString('es-VE')}
              </td>
              <td style={{ ...tdStyle, textAlign: 'center' }}>
                <button
                  onClick={() => onSendHint?.(team.id, team.name)}
                  disabled={!onSendHint}
                  title={onSendHint ? `Enviar pista a ${team.name}` : 'Disponible en una próxima versión'}
                  style={{
                    padding: '0.2rem 0.6rem',
                    fontSize: '0.8rem',
                    cursor: onSendHint ? 'pointer' : 'not-allowed',
                    opacity: onSendHint ? 1 : 0.45,
                  }}
                >
                  💡 Enviar pista
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const thStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem',
  fontWeight: '600',
  fontSize: '0.8rem',
  color: '#555',
  borderBottom: '2px solid #ddd',
};

const tdStyle: React.CSSProperties = {
  padding: '0.6rem 0.75rem',
  verticalAlign: 'middle',
};
