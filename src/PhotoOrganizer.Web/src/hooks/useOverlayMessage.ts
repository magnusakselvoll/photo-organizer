import { useState, useCallback, useEffect, useRef } from 'react';
import type { ReactNode } from 'react';

const DEFAULT_DURATION_MS = 3_000;

export interface OverlayMessageState {
  message: ReactNode | null;
  showMessage: (content: ReactNode, durationMs?: number) => void;
}

/**
 * Manages a transient on-screen overlay message that fades out automatically.
 * Suitable for keyboard-shortcut feedback and action confirmations.
 */
export function useOverlayMessage(): OverlayMessageState {
  const [message, setMessage] = useState<ReactNode | null>(null);
  const timerRef = useRef<number | null>(null);

  const showMessage = useCallback((content: ReactNode, durationMs = DEFAULT_DURATION_MS) => {
    if (timerRef.current !== null) clearTimeout(timerRef.current);
    setMessage(content);
    timerRef.current = window.setTimeout(() => {
      setMessage(null);
      timerRef.current = null;
    }, durationMs);
  }, []);

  // Clear timer on unmount
  useEffect(() => {
    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current);
    };
  }, []);

  return { message, showMessage };
}
