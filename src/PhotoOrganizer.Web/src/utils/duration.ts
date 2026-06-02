/**
 * Minimum and maximum display time per photo (seconds).
 */
export const MIN_INTERVAL_SECONDS = 10;
export const MAX_INTERVAL_SECONDS = 600; // 10 minutes

/**
 * Compute the next display-time value in the stepped sequence:
 *   - Increasing: +10s while current < 60s; +60s at 60s or above.
 *   - Decreasing: -10s while current < 120s; -60s at 120s or above.
 * Result is clamped to [MIN_INTERVAL_SECONDS, MAX_INTERVAL_SECONDS].
 */
export function nextDuration(current: number, direction: 1 | -1): number {
  const step = direction > 0
    ? (current >= 60 ? 60 : 10)
    : (current >= 120 ? 60 : 10);
  return Math.max(MIN_INTERVAL_SECONDS, Math.min(MAX_INTERVAL_SECONDS, current + direction * step));
}

/**
 * Format a duration in seconds to a human-readable string.
 * Under a minute: "30s". At or over a minute: "2m", "1m 30s".
 */
export function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return s === 0 ? `${m}m` : `${m}m ${s}s`;
}
