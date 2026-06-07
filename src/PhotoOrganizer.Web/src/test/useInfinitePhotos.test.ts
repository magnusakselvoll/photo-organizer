import { renderHook, act, waitFor } from '@testing-library/react';
import { columnsForWidth, filterKey, useInfinitePhotos } from '../hooks/useInfinitePhotos';
import type { InfinitePhotosFilters } from '../hooks/useInfinitePhotos';
import * as client from '../api/client';
import type { PhotoPageDto } from '../api/types';

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

// ─── useInfinitePhotos hook behaviour ─────────────────────────────────────────

vi.mock('../api/client', () => ({
  getPhotos: vi.fn(),
}));

function makePhoto(id: string) {
  return {
    id,
    filePath: `/photos/${id}.jpg`,
    fileName: `${id}.jpg`,
    capturedAt: null,
    effectiveDate: null,
    folderType: 'Originals' as const,
    duplicateGroupId: null,
    isPreferred: true,
    tags: [],
    versions: [],
  };
}

function makePage(ids: string[], nextCursor: string | null = null): PhotoPageDto {
  return { items: ids.map(makePhoto), totalCount: ids.length, page: 1, pageSize: ids.length, nextCursor };
}

const BASE_FILTERS: InfinitePhotosFilters = {
  folder: '',
  type: 'all',
  deduplicated: false,
  fileName: '',
  dateFrom: '',
  dateTo: '',
};

afterEach(() => {
  vi.clearAllMocks();
});

describe('useInfinitePhotos hook', () => {
  test('loads the first page on mount', async () => {
    vi.mocked(client.getPhotos).mockResolvedValue(makePage(['a', 'b']));

    const { result } = renderHook(() => useInfinitePhotos(BASE_FILTERS));

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.items.map(p => p.id)).toEqual(['a', 'b']);
    expect(result.current.totalCount).toBe(2);
    expect(result.current.hasMore).toBe(false);
  });

  test('stale response is discarded when filter key changes mid-flight', async () => {
    // Use manual resolve handles so we control when each fetch resolves.
    let resolveFirst!: (v: PhotoPageDto) => void;
    let resolveSecond!: (v: PhotoPageDto) => void;

    vi.mocked(client.getPhotos)
      .mockReturnValueOnce(new Promise<PhotoPageDto>(res => { resolveFirst = res; }))
      .mockReturnValueOnce(new Promise<PhotoPageDto>(res => { resolveSecond = res; }));

    const filtersA: InfinitePhotosFilters = { ...BASE_FILTERS, folder: 'a' };
    const filtersB: InfinitePhotosFilters = { ...BASE_FILTERS, folder: 'b' };

    const { result, rerender } = renderHook(
      ({ filters }: { filters: InfinitePhotosFilters }) => useInfinitePhotos(filters),
      { initialProps: { filters: filtersA } },
    );

    // Both filter sets are now mounted; switch to B before A resolves.
    rerender({ filters: filtersB });

    // Resolve A (stale) — its result must be discarded.
    await act(async () => { resolveFirst(makePage(['stale-a'])); });

    // Items must remain empty because A's response was stale.
    expect(result.current.items).toHaveLength(0);

    // Now resolve B (fresh) — its result must be applied.
    await act(async () => { resolveSecond(makePage(['fresh-b'])); });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.items.map(p => p.id)).toEqual(['fresh-b']);
  });

  test('mergeNewest prepends only ids not already loaded', async () => {
    vi.mocked(client.getPhotos).mockResolvedValue(makePage(['a', 'b']));

    const { result } = renderHook(() => useInfinitePhotos(BASE_FILTERS));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // 'b' is already loaded; only 'c' should be prepended.
    act(() => { result.current.mergeNewest([makePhoto('b'), makePhoto('c')]); });

    expect(result.current.items.map(p => p.id)).toEqual(['c', 'a', 'b']);
    expect(result.current.totalCount).toBe(3);
  });

  test('mergeNewest with all-duplicate ids is a no-op', async () => {
    vi.mocked(client.getPhotos).mockResolvedValue(makePage(['a']));

    const { result } = renderHook(() => useInfinitePhotos(BASE_FILTERS));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const countBefore = result.current.totalCount;
    act(() => { result.current.mergeNewest([makePhoto('a')]); });

    expect(result.current.items).toHaveLength(1);
    expect(result.current.totalCount).toBe(countBefore);
  });

  test('loadMore appends the next page without duplicating the boundary item', async () => {
    vi.mocked(client.getPhotos)
      .mockResolvedValueOnce(makePage(['a', 'b'], 'cursor-2'))
      .mockResolvedValueOnce(makePage(['c', 'd'], null));

    const { result } = renderHook(() => useInfinitePhotos(BASE_FILTERS));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.hasMore).toBe(true);
    expect(result.current.items.map(p => p.id)).toEqual(['a', 'b']);

    act(() => { result.current.loadMore(); });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    // Second page appended; no duplicates.
    expect(result.current.items.map(p => p.id)).toEqual(['a', 'b', 'c', 'd']);
    expect(result.current.hasMore).toBe(false);
  });

  test('loadMore with an overlapping boundary item deduplicates', async () => {
    // If the server returns 'b' again on the second page (e.g. concurrent write),
    // the client-side Set dedup must exclude it.
    vi.mocked(client.getPhotos)
      .mockResolvedValueOnce(makePage(['a', 'b'], 'cursor-2'))
      .mockResolvedValueOnce(makePage(['b', 'c'], null));

    const { result } = renderHook(() => useInfinitePhotos(BASE_FILTERS));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => { result.current.loadMore(); });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.items.map(p => p.id)).toEqual(['a', 'b', 'c']);
  });
});
