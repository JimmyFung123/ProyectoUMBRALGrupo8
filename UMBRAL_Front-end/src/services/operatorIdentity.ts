// HU-22: identidad del operador.
// Mientras el proyecto no tenga autenticación real, el front captura el
// nombre del operador la primera vez que entra y lo guarda en localStorage.
// Ese nombre viaja en cada llamada modificadora vía el header X-Operator-Name
// para que el back-end pueda atribuir cada SessionEvent al operador correcto.

const STORAGE_KEY = 'umbral.operator.name';
const OPERATOR_HEADER = 'X-Operator-Name';

export function getOperatorName(): string | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const trimmed = raw?.trim() ?? '';
    return trimmed.length > 0 ? trimmed : null;
  } catch {
    return null;
  }
}

export function setOperatorName(name: string): void {
  const trimmed = name.trim();
  if (trimmed.length === 0) return;
  try {
    localStorage.setItem(STORAGE_KEY, trimmed);
  } catch {
    /* private mode / quota — ignore */
  }
}

export function clearOperatorName(): void {
  try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
}

/**
 * Returns a headers object pre-filled with X-Operator-Name (when an operator
 * identity is set) plus any extra headers the caller wants to merge in.
 * Use this in every fetch that calls a state-changing endpoint so the
 * audit log (HU-22) captures who triggered the action.
 */
export function withOperator(extra: Record<string, string> = {}): Record<string, string> {
  const name = getOperatorName();
  return name ? { ...extra, [OPERATOR_HEADER]: name } : { ...extra };
}
