import { useEffect, useState } from 'react';
import { clearOperatorName, getOperatorName, setOperatorName } from '../services/operatorIdentity';

/**
 * HU-22: barra superior que captura el nombre del operador la primera vez
 * que abre el dashboard y lo recuerda en localStorage. Cada acción modificadora
 * (start, pause, penalize, force-advance, etc.) viaja con ese nombre vía
 * X-Operator-Name para que el SessionEvent quede atribuido correctamente.
 *
 * No hay autenticación real — es un campo libre. Validar identidad queda
 * pendiente para una HU futura de gestión de personal operativo (HU-23).
 */
export function OperatorIdentityBar() {
  const [name, setName] = useState<string | null>(getOperatorName());
  const [editing, setEditing] = useState<boolean>(name === null);
  const [draft, setDraft] = useState<string>(name ?? '');

  // Si el nombre se limpia desde otra tab, refrescamos.
  useEffect(() => {
    function onStorage(e: StorageEvent) {
      if (e.key === 'umbral.operator.name') {
        setName(getOperatorName());
      }
    }
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  function handleSave() {
    const trimmed = draft.trim();
    if (trimmed.length === 0) return;
    setOperatorName(trimmed);
    setName(trimmed);
    setEditing(false);
  }

  function handleClear() {
    clearOperatorName();
    setName(null);
    setDraft('');
    setEditing(true);
  }

  if (editing) {
    return (
      <div style={{ ...styles.bar, background: '#fff3cd', borderBottom: '1px solid #ffd966' }}>
        <span style={styles.label}>👤 Identifícate como operador:</span>
        <input
          autoFocus
          type="text"
          placeholder="Tu nombre (ej. Prof. Ortega)"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') handleSave(); }}
          style={styles.input}
        />
        <button onClick={handleSave} disabled={draft.trim().length === 0} style={styles.primaryBtn}>
          Guardar
        </button>
        {name && (
          <button onClick={() => { setDraft(name); setEditing(false); }} style={styles.linkBtn}>
            Cancelar
          </button>
        )}
        <span style={styles.hint}>
          Se usa para atribuir tus acciones en el historial de auditoría.
        </span>
      </div>
    );
  }

  return (
    <div style={styles.bar}>
      <span style={styles.label}>
        👤 Operador: <strong style={{ color: '#3730a3' }}>{name}</strong>
      </span>
      <button onClick={() => { setDraft(name ?? ''); setEditing(true); }} style={styles.linkBtn}>
        Cambiar
      </button>
      <button onClick={handleClear} style={styles.linkBtn}>
        Salir
      </button>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  bar: {
    display: 'flex',
    alignItems: 'center',
    gap: '0.6rem',
    padding: '0.45rem 1rem',
    background: '#eef2ff',
    borderBottom: '1px solid #c7d2fe',
    fontSize: '0.85rem',
    flexWrap: 'wrap',
  },
  label: { color: '#4338ca' },
  input: {
    padding: '0.3rem 0.6rem',
    fontSize: '0.85rem',
    border: '1px solid #c0c0c0',
    borderRadius: 4,
    minWidth: 200,
  },
  primaryBtn: {
    padding: '0.3rem 0.8rem',
    fontSize: '0.85rem',
    background: '#6366f1',
    color: '#fff',
    border: 'none',
    borderRadius: 4,
    fontWeight: 600,
    cursor: 'pointer',
  },
  linkBtn: {
    background: 'transparent',
    border: 'none',
    color: '#6366f1',
    fontSize: '0.8rem',
    cursor: 'pointer',
    textDecoration: 'underline',
    padding: '0.15rem 0.4rem',
  },
  hint: {
    color: '#856404',
    fontSize: '0.75rem',
    marginLeft: 'auto',
  },
};
