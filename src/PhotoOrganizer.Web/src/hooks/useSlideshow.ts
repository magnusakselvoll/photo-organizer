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

const MAX_BACKOFF_MS = 30_000;

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
  const backoffRef = useRef<number>(1_000);
  const mountedRef = useRef(true);

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

  // Fetch the next photo, preload it, push to history and advance.
  const fetchAndAdvance = useCallback(async () => {
    try {
      const photo = await getSlideshowNext();
      if (!mountedRef.current) return;

      if (photo === null) {
        // 404 — library is empty
        setStatus('empty');
        setCurrentPhoto(null);
        return;
      }

      // Fire-and-forget preload to warm the browser cache ahead of the crossfade.
      // We don't await so tests and server-side environments aren't blocked if
      // image load events don't fire (jsdom, etc.).
      void preloadImage(imageUrl(photo.id));

      historyRef.current.push(photo);
      pointerRef.current = historyRef.current.length - 1;
      backoffRef.current = 1_000; // reset backoff on success
      applyPointer();
    } catch {
      if (!mountedRef.current) return;
      setHasError(true);
      // Keep the last photo on screen; status stays 'ready' if we had one
      if (status === 'loading') setStatus('error');
    }
  }, [applyPointer, status]);

  // Schedule the next auto-advance (rescheduling setTimeout pattern)
  const scheduleNext = useCallback(() => {
    clearTimer();
    timerRef.current = window.setTimeout(() => {
      if (!mountedRef.current) return;
      // Only auto-advance when at the head of history (not browsing backwards)
      if (pointerRef.current === historyRef.current.length - 1) {
        fetchAndAdvance().then(() => {
          if (mountedRef.current && !paused) scheduleNext();
        });
      } else {
        scheduleNext();
      }
    }, intervalMs);
  }, [clearTimer, fetchAndAdvance, intervalMs, paused]);

  // Error retry with exponential backoff
  const scheduleRetry = useCallback(() => {
    clearTimer();
    timerRef.current = window.setTimeout(() => {
      if (!mountedRef.current) return;
      fetchAndAdvance().then(() => {
        if (mountedRef.current) {
          if (hasError) {
            backoffRef.current = Math.min(backoffRef.current * 2, MAX_BACKOFF_MS);
            scheduleRetry();
          } else if (!paused) {
            scheduleNext();
          }
        }
      });
    }, backoffRef.current);
  }, [clearTimer, fetchAndAdvance, hasError, paused, scheduleNext]);

  // Initial load
  useEffect(() => {
    mountedRef.current = true;
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
