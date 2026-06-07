import { useState, useCallback, useEffect, useRef } from 'react';
import type { ReactNode } from 'react';

const DEFAULT_DURATION_MS = 3_000;

export interface OverlayMessageState {
  message: ReactNode | null;
  showMessage: (content: ReactNode, durationMs?: number) => void;
  hide: () => void;
  toggleMessage: (content: ReactNode, durationMs: number, key: string) => void;
  hideKey: (key: string) => void;
}

/**
 * Manages a transient on-screen overlay message that fades out automatically.
 * Suitable for keyboard-shortcut feedback and action confirmations.
 *
 * Panels that should toggle (i.e. show / hide with the same key press) should use
 * `toggleMessage` with a stable string key, then call `hideKey(key)` from any effect
 * that should dismiss the panel (e.g. when the photo advances). Keyless `showMessage`
 * calls (toast feedback) are unaffected by the key mechanism.
 */
export function useOverlayMessage(): OverlayMessageState {
  const [message, setMessage] = useState<ReactNode | null>(null);
  const timerRef = useRef<number | null>(null);
  // Tracks which keyed panel is currently open; null for keyless toasts.
  const keyRef = useRef<string | null>(null);

  const hide = useCallback(() => {
    if (timerRef.current !== null) {
      clearTimeout(timerRef.current);
      timerRef.current = null;
    }
    keyRef.current = null;
    setMessage(null);
  }, []);

  const showMessage = useCallback((content: ReactNode, durationMs = DEFAULT_DURATION_MS, key: string | null = null) => {
    if (timerRef.current !== null) clearTimeout(timerRef.current);
    keyRef.current = key;
    setMessage(content);
    timerRef.current = window.setTimeout(() => {
      setMessage(null);
      keyRef.current = null;
      timerRef.current = null;
    }, durationMs);
  }, []);

  const toggleMessage = useCallback((content: ReactNode, durationMs: number, key: string) => {
    if (keyRef.current === key) {
      // Same panel already open — hide it (toggle off).
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
      keyRef.current = null;
      setMessage(null);
    } else {
      // Different panel or nothing open — show the new panel.
      if (timerRef.current !== null) clearTimeout(timerRef.current);
      keyRef.current = key;
      setMessage(content);
      timerRef.current = window.setTimeout(() => {
        setMessage(null);
        keyRef.current = null;
        timerRef.current = null;
      }, durationMs);
    }
  }, []);

  const hideKey = useCallback((key: string) => {
    if (keyRef.current === key) {
      if (timerRef.current !== null) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
      keyRef.current = null;
      setMessage(null);
    }
  }, []);

  // Clear timer on unmount
  useEffect(() => {
    return () => {
      if (timerRef.current !== null) clearTimeout(timerRef.current);
    };
  }, []);

  return { message, showMessage, hide, toggleMessage, hideKey };
}
