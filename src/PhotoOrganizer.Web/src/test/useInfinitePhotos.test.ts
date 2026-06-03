import { columnsForWidth, filterKey } from '../hooks/useInfinitePhotos';
import type { InfinitePhotosFilters } from '../hooks/useInfinitePhotos';

// ─── filterKey ────────────────────────────────────────────────────────────────
// Every field in InfinitePhotosFilters must appear in filterKey so that a change
// to any filter triggers a hook reset/refetch. This test is the regression guard
// for that invariant.

const BASE: InfinitePhotosFilters = {
  folder: '',
  type: 'all',
  deduplicated: true,
  fileName: '',
  dateFrom: '',
  dateTo: '',
};

describe('filterKey', () => {
  test('same filters produce the same key', () => {
    expect(filterKey(BASE)).toBe(filterKey({ ...BASE }));
  });

  test('changing folder produces a different key', () => {
    expect(filterKey({ ...BASE, folder: '/photos/2024' })).not.toBe(filterKey(BASE));
  });

  test('changing type produces a different key', () => {
    expect(filterKey({ ...BASE, type: 'Originals' })).not.toBe(filterKey(BASE));
  });

  test('changing deduplicated produces a different key', () => {
    expect(filterKey({ ...BASE, deduplicated: false })).not.toBe(filterKey(BASE));
  });

  test('changing fileName produces a different key', () => {
    expect(filterKey({ ...BASE, fileName: 'IMG' })).not.toBe(filterKey(BASE));
  });

  test('changing dateFrom produces a different key', () => {
    expect(filterKey({ ...BASE, dateFrom: '2024-01-01' })).not.toBe(filterKey(BASE));
  });

  test('changing dateTo produces a different key', () => {
    expect(filterKey({ ...BASE, dateTo: '2024-12-31' })).not.toBe(filterKey(BASE));
  });
});

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
