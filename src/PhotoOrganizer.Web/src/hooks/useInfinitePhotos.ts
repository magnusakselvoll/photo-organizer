import { useCallback, useEffect, useReducer, useRef } from 'react';
import { getPhotos } from '../api/client';
import type { PhotoDto } from '../api/types';

export interface InfinitePhotosFilters {
  folder: string;
  type: string;
  deduplicated: boolean;
}

export interface UseInfinitePhotosResult {
  items: PhotoDto[];
  hasMore: boolean;
  isLoading: boolean;
  error: string | null;
  totalCount: number;
  loadMore: () => void;
  /** Prepend any items not already loaded (live-update ingestion). */
  mergeNewest: (incoming: PhotoDto[]) => void;
}

/** Number of photos fetched per page. */
const PAGE_SIZE = 50;

/**
 * Produces a stable string key from a filter set so we can detect changes.
 * The hook resets its state whenever the key changes.
 */
export function filterKey(f: InfinitePhotosFilters): string {
  return `${f.folder}|${f.type}|${f.deduplicated}`;
}

// ─── Reducer ─────────────────────────────────────────────────────────────────

interface State {
  items: PhotoDto[];
  hasMore: boolean;
  isLoading: boolean;
  error: string | null;
  totalCount: number;
}

const initialState: State = {
  items: [],
  hasMore: false,
  isLoading: true,
  error: null,
  totalCount: 0,
};

type Action =
  | { type: 'reset' }
  | { type: 'fetch-start' }
  | { type: 'fetch-success'; items: PhotoDto[]; totalCount: number; hasMore: boolean }
  | { type: 'fetch-error'; error: string }
  | { type: 'merge-newest'; fresh: PhotoDto[] };

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case 'reset':
      return { ...initialState };
    case 'fetch-start':
      return { ...state, isLoading: true, error: null };
    case 'fetch-success':
      return {
        ...state,
        items: [...state.items, ...action.items],
        totalCount: action.totalCount,
        hasMore: action.hasMore,
        isLoading: false,
        error: null,
      };
    case 'fetch-error':
      return { ...state, isLoading: false, error: action.error };
    case 'merge-newest':
      return {
        ...state,
        items: [...action.fresh, ...state.items],
        totalCount: state.totalCount + action.fresh.length,
      };
  }
}

// ─── Hook ─────────────────────────────────────────────────────────────────────

/**
 * Hand-rolled infinite-scroll hook with keyset cursor pagination.
 *
 * - Fetches the first page immediately on mount (and whenever filters change).
 * - `loadMore()` fetches the next page using the server's `nextCursor`; guards
 *   against concurrent fetches.
 * - `mergeNewest(incoming)` prepends photos whose ids aren't already loaded;
 *   used by the live-update poller to show newly indexed photos without
 *   disrupting already-loaded older pages.
 */
export function useInfinitePhotos(filters: InfinitePhotosFilters): UseInfinitePhotosResult {
  const [state, dispatch] = useReducer(reducer, initialState);

  // Cursor for the next page; null means either "start from the top" or "no more pages".
  const nextCursorRef = useRef<string | null>(null);
  // Whether there are more pages to load (mirror of state.hasMore for use in callbacks).
  const hasMoreRef = useRef(false);
  // Guard: don't start a fetch while one is already in flight.
  const loadingRef = useRef(false);
  // Key of the most recently mounted filter; lets async callbacks detect stale results.
  const activeKeyRef = useRef<string>('');
  // Set of loaded photo ids for O(1) dedup in mergeNewest.
  const loadedIdsRef = useRef<Set<string>>(new Set());

  const currentKey = filterKey(filters);

  // Hold filters in a ref so fetchPage closures can read the latest values.
  const filtersRef = useRef(filters);
  filtersRef.current = filters;

  // Reset state and fetch the first page whenever filters change.
  useEffect(() => {
    const key = filterKey(filtersRef.current);
    activeKeyRef.current = key;
    nextCursorRef.current = null;
    hasMoreRef.current = false;
    loadingRef.current = false;
    loadedIdsRef.current = new Set();

    dispatch({ type: 'reset' });
    fetchPage(key, null);
  }, [currentKey]);

  /**
   * Core fetch: loads one page starting from `cursor` (null = first page).
   * Appends results to `items`; ignores response if the filter key changed
   * since the request was started.
   */
  function fetchPage(key: string, cursor: string | null) {
    if (loadingRef.current) return;
    loadingRef.current = true;
    dispatch({ type: 'fetch-start' });

    const f = filtersRef.current;
    getPhotos({
      folder: f.folder || undefined,
      type: f.type !== 'all' ? f.type : undefined,
      deduplicated: f.deduplicated,
      cursor: cursor ?? undefined,
      limit: PAGE_SIZE,
    })
      .then(page => {
        if (activeKeyRef.current !== key) return; // stale response, discard

        const newItems = page.items.filter(p => !loadedIdsRef.current.has(p.id));
        newItems.forEach(p => loadedIdsRef.current.add(p.id));

        const more = page.nextCursor !== null;
        nextCursorRef.current = page.nextCursor;
        hasMoreRef.current = more;

        dispatch({ type: 'fetch-success', items: newItems, totalCount: page.totalCount, hasMore: more });
      })
      .catch(e => {
        if (activeKeyRef.current !== key) return;
        dispatch({ type: 'fetch-error', error: String(e) });
      })
      .finally(() => {
        if (activeKeyRef.current !== key) return;
        loadingRef.current = false;
      });
  }

  /** Triggered by the virtualizer when the user scrolls near the bottom. */
  const loadMore = useCallback(() => {
    if (!hasMoreRef.current || loadingRef.current) return;
    fetchPage(activeKeyRef.current, nextCursorRef.current);
  }, []);

  /**
   * Prepend `incoming` items that aren't already in the loaded set.
   * Called by the live-update poller when new photos appear in the index.
   * Because newer photos always sort above the current cursor position,
   * prepending them never corrupts the order of already-loaded older pages.
   */
  const mergeNewest = useCallback((incoming: PhotoDto[]) => {
    const fresh = incoming.filter(p => !loadedIdsRef.current.has(p.id));
    if (fresh.length === 0) return;
    fresh.forEach(p => loadedIdsRef.current.add(p.id));
    dispatch({ type: 'merge-newest', fresh });
  }, []);

  return {
    items: state.items,
    hasMore: state.hasMore,
    isLoading: state.isLoading,
    error: state.error,
    totalCount: state.totalCount,
    loadMore,
    mergeNewest,
  };
}

// ─── Pure helpers (exported for unit testing) ────────────────────────────────

/** Returns the columns-per-row count for a container of `containerWidth` px. */
export function columnsForWidth(containerWidth: number, minColWidth = 180, gap = 12): number {
  return Math.max(1, Math.floor((containerWidth + gap) / (minColWidth + gap)));
}
