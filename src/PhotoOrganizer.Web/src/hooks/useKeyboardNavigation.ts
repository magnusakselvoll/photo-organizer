import { useEffect, useCallback } from 'react';

export interface KeyboardNavigationConfig {
  onNext?: () => void;
  onPrevious?: () => void;
  onTogglePause?: () => void;
  onExit?: () => void;
  enabled?: boolean;
}

export function useKeyboardNavigation({
  onNext,
  onPrevious,
  onTogglePause,
  onExit,
  enabled = true,
}: KeyboardNavigationConfig): void {
  const handleKeyDown = useCallback((event: KeyboardEvent) => {
    if (!enabled) return;

    // Ignore if focus is on an input element
    if (
      event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement
    ) {
      return;
    }

    switch (event.key) {
      case ' ':
      case 'ArrowRight':
        event.preventDefault();
        onNext?.();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        onPrevious?.();
        break;
      case 'p':
      case 'P':
        event.preventDefault();
        onTogglePause?.();
        break;
      case 'Escape':
        event.preventDefault();
        onExit?.();
        break;
    }
  }, [enabled, onNext, onPrevious, onTogglePause, onExit]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [handleKeyDown]);
}
