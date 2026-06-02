import { useEffect, useCallback } from 'react';

export interface KeyboardNavigationConfig {
  onNext?: () => void;
  onPrevious?: () => void;
  onTogglePause?: () => void;
  onIncreaseDuration?: () => void;
  onDecreaseDuration?: () => void;
  onShowInfo?: () => void;
  onShowHelp?: () => void;
  onExit?: () => void;
  enabled?: boolean;
}

export function useKeyboardNavigation({
  onNext,
  onPrevious,
  onTogglePause,
  onIncreaseDuration,
  onDecreaseDuration,
  onShowInfo,
  onShowHelp,
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
      case 'ArrowRight':
        event.preventDefault();
        onNext?.();
        break;
      case 'ArrowLeft':
        event.preventDefault();
        onPrevious?.();
        break;
      case ' ':
      case 'p':
      case 'P':
        event.preventDefault();
        onTogglePause?.();
        break;
      case '+':
      case '=':
        event.preventDefault();
        onIncreaseDuration?.();
        break;
      case '-':
      case '_':
        event.preventDefault();
        onDecreaseDuration?.();
        break;
      case 'i':
      case 'I':
        event.preventDefault();
        onShowInfo?.();
        break;
      case '?':
      case '/':
        event.preventDefault();
        onShowHelp?.();
        break;
      case 'Escape':
        event.preventDefault();
        onExit?.();
        break;
    }
  }, [enabled, onNext, onPrevious, onTogglePause, onIncreaseDuration, onDecreaseDuration, onShowInfo, onShowHelp, onExit]);

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [handleKeyDown]);
}
