/**
 * HU-28 — haptic feedback helpers.
 *
 * Wraps `navigator.vibrate` with three concerns:
 *   1. Some browsers (notably iOS Safari) have no Vibration API — calls
 *      become silent no-ops automatically.
 *   2. The participant can mute haptics via the in-app toggle; the choice is
 *      persisted in sessionStorage so it survives reloads on the same tab.
 *   3. A small named pattern catalog avoids each call site inventing its
 *      own ms numbers.
 */

const STORAGE_KEY = 'umbral.participant.haptics.v1';

type Pattern = 'tap' | 'success' | 'error' | 'message' | 'celebrate';

const PATTERNS: Record<Pattern, number | number[]> = {
  tap:       30,
  success:   [60, 40, 80],
  error:     [40, 30, 40, 30, 40],
  message:   [40, 60, 40],
  celebrate: [80, 60, 80, 60, 160],
};

export function isHapticsSupported(): boolean {
  return typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function';
}

export function isHapticsEnabled(): boolean {
  if (!isHapticsSupported()) return false;
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    // Default = enabled. Only "false" disables it.
    return raw !== 'false';
  } catch {
    return true;
  }
}

export function setHapticsEnabled(enabled: boolean) {
  try {
    sessionStorage.setItem(STORAGE_KEY, enabled ? 'true' : 'false');
  } catch {
    /* sessionStorage might be full / disabled — ignore */
  }
}

export function vibrate(pattern: Pattern) {
  if (!isHapticsSupported() || !isHapticsEnabled()) return;
  try {
    navigator.vibrate(PATTERNS[pattern]);
  } catch {
    /* swallow */
  }
}
