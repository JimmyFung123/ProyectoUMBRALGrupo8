/**
 * Tiny classnames concatenator. We don't want a runtime dependency for what
 * is essentially `[a, b, c].filter(Boolean).join(' ')` so this lives inline.
 */
export function cn(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}
