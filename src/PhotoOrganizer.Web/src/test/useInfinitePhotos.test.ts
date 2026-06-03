import { columnsForWidth } from '../hooks/useInfinitePhotos';

// ─── columnsForWidth ──────────────────────────────────────────────────────────

describe('columnsForWidth', () => {
  test('returns 1 for a very narrow container', () => {
    expect(columnsForWidth(100)).toBe(1);
  });

  test('returns 1 for zero width', () => {
    expect(columnsForWidth(0)).toBe(1);
  });

  test('returns 1 for a container exactly as wide as one column', () => {
    // 180px column, no room for a second one (180 + 12 = 192 needed for 2)
    expect(columnsForWidth(180)).toBe(1);
  });

  test('returns 2 when the container fits two columns with gap', () => {
    // 2 columns = 2*180 + 1*12 = 372
    expect(columnsForWidth(372)).toBe(2);
  });

  test('returns 3 when the container fits three columns', () => {
    // 3 columns = 3*180 + 2*12 = 564
    expect(columnsForWidth(564)).toBe(3);
  });

  test('rounds down (does not exceed available space)', () => {
    // Just under the threshold for 4 columns (4*180 + 3*12 = 756)
    expect(columnsForWidth(755)).toBe(3);
  });

  test('respects custom minColWidth', () => {
    // With minColWidth=100, gap=12: 2 cols need 212px
    expect(columnsForWidth(212, 100, 12)).toBe(2);
  });

  test('respects custom gap', () => {
    // With minColWidth=180, gap=20: 2 cols need 380px
    expect(columnsForWidth(380, 180, 20)).toBe(2);
  });

  test('handles a realistic 1280px viewport with defaults', () => {
    // floor((1280 + 12) / (180 + 12)) = floor(1292 / 192) = 6
    expect(columnsForWidth(1280)).toBe(6);
  });
});
