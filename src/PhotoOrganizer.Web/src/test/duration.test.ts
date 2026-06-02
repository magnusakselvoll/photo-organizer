import { nextDuration, formatDuration, MIN_INTERVAL_SECONDS, MAX_INTERVAL_SECONDS } from '../utils/duration';

describe('nextDuration', () => {
  // ── Increasing ──

  it('steps by +10s below 60s', () => {
    expect(nextDuration(10, 1)).toBe(20);
    expect(nextDuration(20, 1)).toBe(30);
    expect(nextDuration(50, 1)).toBe(60);
  });

  it('crosses the 60s boundary with a +10s step from 50s', () => {
    expect(nextDuration(50, 1)).toBe(60);
  });

  it('steps by +60s at 60s', () => {
    expect(nextDuration(60, 1)).toBe(120);
  });

  it('steps by +60s above 60s', () => {
    expect(nextDuration(120, 1)).toBe(180);
    expect(nextDuration(300, 1)).toBe(360);
  });

  it('clamps at MAX_INTERVAL_SECONDS when increasing', () => {
    expect(nextDuration(MAX_INTERVAL_SECONDS, 1)).toBe(MAX_INTERVAL_SECONDS);
    expect(nextDuration(MAX_INTERVAL_SECONDS - 10, 1)).toBe(MAX_INTERVAL_SECONDS);
  });

  // ── Decreasing ──

  it('steps by -10s below 120s', () => {
    expect(nextDuration(30, -1)).toBe(20);
    expect(nextDuration(60, -1)).toBe(50);
    expect(nextDuration(110, -1)).toBe(100);
  });

  it('crosses the 120s boundary with a -60s step from 120s', () => {
    expect(nextDuration(120, -1)).toBe(60);
  });

  it('steps by -60s above 120s', () => {
    expect(nextDuration(180, -1)).toBe(120);
    expect(nextDuration(360, -1)).toBe(300);
  });

  it('clamps at MIN_INTERVAL_SECONDS when decreasing', () => {
    expect(nextDuration(MIN_INTERVAL_SECONDS, -1)).toBe(MIN_INTERVAL_SECONDS);
    expect(nextDuration(MIN_INTERVAL_SECONDS + 5, -1)).toBe(MIN_INTERVAL_SECONDS);
  });

  // ── Symmetry around the 60/120 boundary ──

  it('60 +60 → 120', () => expect(nextDuration(60, 1)).toBe(120));
  it('120 -60 → 60', () => expect(nextDuration(120, -1)).toBe(60));
  it('60 -10 → 50', () => expect(nextDuration(60, -1)).toBe(50));
});

describe('formatDuration', () => {
  it('formats seconds under a minute', () => {
    expect(formatDuration(10)).toBe('10s');
    expect(formatDuration(30)).toBe('30s');
    expect(formatDuration(59)).toBe('59s');
  });

  it('formats exact minutes', () => {
    expect(formatDuration(60)).toBe('1m');
    expect(formatDuration(120)).toBe('2m');
    expect(formatDuration(600)).toBe('10m');
  });

  it('formats minutes and seconds', () => {
    expect(formatDuration(90)).toBe('1m 30s');
    expect(formatDuration(150)).toBe('2m 30s');
  });
});
