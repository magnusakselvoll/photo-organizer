import { generateKenBurnsConfig } from '../utils/kenBurns';

describe('generateKenBurnsConfig', () => {
  const INTERVAL_MS = 8_000;

  it('returns a config with scale values in documented ranges', () => {
    // Run many times to cover both random branches
    for (let i = 0; i < 200; i++) {
      const cfg = generateKenBurnsConfig(INTERVAL_MS);
      const { scaleFrom, scaleTo } = cfg;
      const small = Math.min(scaleFrom, scaleTo);
      const large = Math.max(scaleFrom, scaleTo);
      expect(small).toBeGreaterThanOrEqual(1.0);
      expect(small).toBeLessThanOrEqual(1.05);
      expect(large).toBeGreaterThanOrEqual(1.22);
      expect(large).toBeLessThanOrEqual(1.30);
    }
  });

  it('sets duration based on the interval', () => {
    const cfg = generateKenBurnsConfig(10_000);
    expect(cfg.duration).toBe('10.0s');
  });

  it('produces both zoom-in and zoom-out directions across many calls', () => {
    const results = Array.from({ length: 100 }, () => generateKenBurnsConfig(INTERVAL_MS));
    // zoom-in: scaleFrom < scaleTo; zoom-out: scaleFrom > scaleTo
    const hasZoomIn = results.some(c => c.scaleFrom < c.scaleTo);
    const hasZoomOut = results.some(c => c.scaleFrom > c.scaleTo);
    expect(hasZoomIn).toBe(true);
    expect(hasZoomOut).toBe(true);
  });

  it('xFrom and yFrom are always 0%', () => {
    for (let i = 0; i < 50; i++) {
      const cfg = generateKenBurnsConfig(INTERVAL_MS);
      expect(cfg.xFrom).toBe('0%');
      expect(cfg.yFrom).toBe('0%');
    }
  });

  it('pan destinations cover multiple directions across many calls', () => {
    const results = Array.from({ length: 200 }, () => generateKenBurnsConfig(INTERVAL_MS));
    const xPositive = results.some(c => parseFloat(c.xTo) > 0);
    const xNegative = results.some(c => parseFloat(c.xTo) < 0);
    const yPositive = results.some(c => parseFloat(c.yTo) > 0);
    const yNegative = results.some(c => parseFloat(c.yTo) < 0);
    expect(xPositive).toBe(true);
    expect(xNegative).toBe(true);
    expect(yPositive).toBe(true);
    expect(yNegative).toBe(true);
  });
});
