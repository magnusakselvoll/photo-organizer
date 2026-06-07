import { useState, useEffect, useCallback, useRef } from 'react';
import { getSlideshowNext, imageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';

export type SlideshowStatus = 'loading' | 'empty' | 'error' | 'ready';

export interface SlideshowState {
  status: SlideshowStatus;
  currentPhoto: PhotoDto | null;
  hasError: boolean;
  paused: boolean;
  next: () => void;
  previous: () => void;
  togglePause: () => void;
}

interface UseSlideshowOptions {
  intervalMs: number;
}

function preloadImage(url: string): Promise<void> {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve();
    img.onerror = () => resolve(); // resolve anyway; the <img> will show the broken state
    img.src = url;
  });
}

export function useSlideshow({ intervalMs }: UseSlideshowOptions): SlideshowState {
  // History: array of photos we've shown, plus a pointer into it.
  // The current photo is history[pointer]. When moving forward past the end we fetch new.
  const historyRef = useRef<PhotoDto[]>([]);
  const pointerRef = useRef<number>(-1);

  const [status, setStatus] = useState<SlideshowStatus>('loading');
  const [currentPhoto, setCurrentPhoto] = useState<PhotoDto | null>(null);
  const [hasError, setHasError] = useState(false);
  const [paused, setPaused] = useState(false);

  // Timer refs — use setTimeout (rescheduled on each advance) so a late callback
  // doesn't drift and waking from sleep simply reschedules cleanly.
  const timerRef = useRef<number | null>(null);
  const mountedRef = useRef(true);

  // Stable ref to the latest scheduleNext so timer callbacks always invoke
  // the current version even after deps have changed.
  const scheduleNextRef = useRef<() => void>(() => {});

  // Cache-ahead: fetch + preload the next photo in the background while the
  // current one is being displayed, so transitions are instant.
  const prefetchedRef = useRef<PhotoDto | null>(null);
  // Generation counter lets old in-flight prefetches detect they've been superseded.
  const prefetchGenRef = useRef(0);

  const clearTimer = useCallback(() => {
    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  // Expose the current photo from history at pointer
  const applyPointer = useCallback(() => {
    const photo = historyRef.current[pointerRef.current] ?? null;
    setCurrentPhoto(photo);
    if (photo) {
      setStatus('ready');
      setHasError(false);
    }
  }, []);

  // Begin fetching and preloading the next photo in the background.
  // Silently discarded if a newer prefetch supersedes this one.
  const startPrefetch = useCallback(() => {
    prefetchedRef.current = null; // discard any stale prefetch
    const gen = ++prefetchGenRef.current;

    getSlideshowNext()
      .then(photo => {
        if (!mountedRef.current || gen !== prefetchGenRef.current || !photo) return;
        prefetchedRef.current = photo;
        void preloadImage(imageUrl(photo.id));
      })
      .catch(() => { /* prefetch failed; will fall back to live fetch on next advance */ });
  }, []);

  // Fetch the next photo (use prefetch if available), push to history and advance.
  const fetchAndAdvance = useCallback(async () => {
    try {
      // Consume prefetch if ready; invalidate any in-flight prefetch via gen bump
      // (startPrefetch will bump gen again when it starts the new prefetch).
      const prefetched = prefetchedRef.current;
      prefetchedRef.current = null;
      ++prefetchGenRef.current;

      const photo = prefetched ?? await getSlideshowNext();
      if (!mountedRef.current) return;

      if (photo === null) {
        // 404 — library is empty
        setStatus('empty');
        setCurrentPhoto(null);
        return;
      }

      // If we fetched live (no prefetch), fire-and-forget preload for the crossfade.
      // When using the prefetch the image is already loading/cached.
      if (!prefetched) void preloadImage(imageUrl(photo.id));

      historyRef.current.push(photo);
      pointerRef.current = historyRef.current.length - 1;
      applyPointer();

      // Begin fetching the photo after this one while the current one is displayed
      startPrefetch();
    } catch {
      if (!mountedRef.current) return;
      setHasError(true);
      // Keep the last photo on screen; status stays 'ready' if we had one
      if (status === 'loading') setStatus('error');
    }
  }, [applyPointer, startPrefetch, status]);

  // Schedule the next auto-advance (rescheduling setTimeout pattern).
  // Recursive calls go through scheduleNextRef so they always use the latest
  // version and avoid stale-closure issues when deps change.
  const scheduleNext = useCallback(() => {
    clearTimer();
    timerRef.current = window.setTimeout(() => {
      if (!mountedRef.current) return;
      // Only auto-advance when at the head of history (not browsing backwards)
      if (pointerRef.current === historyRef.current.length - 1) {
        fetchAndAdvance().then(() => {
          if (mountedRef.current && !paused) scheduleNextRef.current();
        });
      } else {
        scheduleNextRef.current();
      }
    }, intervalMs);
  }, [clearTimer, fetchAndAdvance, intervalMs, paused]);

  // Keep refs in sync so timer callbacks always call the latest version.
  // Must be done in an effect (not during render) per react-hooks/refs.
  useEffect(() => { scheduleNextRef.current = scheduleNext; }, [scheduleNext]);

  // Initial load
  useEffect(() => {
    mountedRef.current = true;
    // fetchAndAdvance sets state asynchronously (after the Promise resolves),
    // not synchronously — the rule fires a false positive here.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchAndAdvance().then(() => {
      if (mountedRef.current && !paused) scheduleNext();
    });
    return () => {
      mountedRef.current = false;
      clearTimer();
    };
    // Only run on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Reschedule when paused state or intervalMs changes
  useEffect(() => {
    if (!paused && status === 'ready') {
      scheduleNext();
    } else {
      clearTimer();
    }
    return clearTimer;
  }, [paused, status, intervalMs, scheduleNext, clearTimer]);

  // Resume cleanly after tab was hidden / device slept
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (!document.hidden && !paused && status === 'ready') {
        // Tab came back — reschedule from now rather than firing immediately
        scheduleNext();
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, [paused, status, scheduleNext]);

  const next = useCallback(() => {
    if (pointerRef.current < historyRef.current.length - 1) {
      // We're in history — step forward without fetching
      pointerRef.current += 1;
      applyPointer();
      if (!paused) scheduleNext();
    } else {
      // At the head — fetch a new photo
      clearTimer();
      fetchAndAdvance().then(() => {
        if (mountedRef.current && !paused) scheduleNext();
      });
    }
  }, [applyPointer, clearTimer, fetchAndAdvance, paused, scheduleNext]);

  const previous = useCallback(() => {
    if (pointerRef.current > 0) {
      pointerRef.current -= 1;
      applyPointer();
      if (!paused) scheduleNext();
    }
    // If already at start, do nothing
  }, [applyPointer, paused, scheduleNext]);

  const togglePause = useCallback(() => {
    setPaused(prev => !prev);
  }, []);

  return { status, currentPhoto, hasError, paused, next, previous, togglePause };
}
