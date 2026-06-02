import { renderHook } from '@testing-library/react';
import { fireEvent } from '@testing-library/react';
import { useKeyboardNavigation } from '../hooks/useKeyboardNavigation';
import type { KeyboardNavigationConfig } from '../hooks/useKeyboardNavigation';

function press(key: string, target: Element | Window = window) {
  fireEvent.keyDown(target, { key });
}

function renderNav(config: KeyboardNavigationConfig) {
  return renderHook(() => useKeyboardNavigation(config));
}

describe('useKeyboardNavigation', () => {
  it('ArrowRight calls onNext', () => {
    const onNext = vi.fn();
    renderNav({ onNext });
    press('ArrowRight');
    expect(onNext).toHaveBeenCalledTimes(1);
  });

  it('ArrowLeft calls onPrevious', () => {
    const onPrevious = vi.fn();
    renderNav({ onPrevious });
    press('ArrowLeft');
    expect(onPrevious).toHaveBeenCalledTimes(1);
  });

  it('Space calls onTogglePause (not onNext)', () => {
    const onNext = vi.fn();
    const onTogglePause = vi.fn();
    renderNav({ onNext, onTogglePause });
    press(' ');
    expect(onTogglePause).toHaveBeenCalledTimes(1);
    expect(onNext).not.toHaveBeenCalled();
  });

  it('p and P call onTogglePause', () => {
    const onTogglePause = vi.fn();
    renderNav({ onTogglePause });
    press('p');
    press('P');
    expect(onTogglePause).toHaveBeenCalledTimes(2);
  });

  it('+ and = call onIncreaseDuration', () => {
    const onIncreaseDuration = vi.fn();
    renderNav({ onIncreaseDuration });
    press('+');
    press('=');
    expect(onIncreaseDuration).toHaveBeenCalledTimes(2);
  });

  it('- and _ call onDecreaseDuration', () => {
    const onDecreaseDuration = vi.fn();
    renderNav({ onDecreaseDuration });
    press('-');
    press('_');
    expect(onDecreaseDuration).toHaveBeenCalledTimes(2);
  });

  it('i and I call onShowInfo', () => {
    const onShowInfo = vi.fn();
    renderNav({ onShowInfo });
    press('i');
    press('I');
    expect(onShowInfo).toHaveBeenCalledTimes(2);
  });

  it('? and / call onShowHelp', () => {
    const onShowHelp = vi.fn();
    renderNav({ onShowHelp });
    press('?');
    press('/');
    expect(onShowHelp).toHaveBeenCalledTimes(2);
  });

  it('Escape calls onExit', () => {
    const onExit = vi.fn();
    renderNav({ onExit });
    press('Escape');
    expect(onExit).toHaveBeenCalledTimes(1);
  });

  it('ignores all keys when disabled', () => {
    const callbacks = {
      onNext: vi.fn(),
      onPrevious: vi.fn(),
      onTogglePause: vi.fn(),
      onIncreaseDuration: vi.fn(),
      onDecreaseDuration: vi.fn(),
      onShowInfo: vi.fn(),
      onShowHelp: vi.fn(),
      onExit: vi.fn(),
      enabled: false,
    };
    renderNav(callbacks);
    ['ArrowRight', 'ArrowLeft', ' ', '+', '-', 'i', '?', 'Escape'].forEach(k => press(k));
    Object.values(callbacks).forEach(v => {
      if (typeof v === 'function') expect(v).not.toHaveBeenCalled();
    });
  });

  it('ignores events from input elements', () => {
    const onNext = vi.fn();
    renderNav({ onNext });
    const input = document.createElement('input');
    document.body.appendChild(input);
    press('ArrowRight', input);
    expect(onNext).not.toHaveBeenCalled();
    document.body.removeChild(input);
  });

  it('ignores events from textarea elements', () => {
    const onTogglePause = vi.fn();
    renderNav({ onTogglePause });
    const textarea = document.createElement('textarea');
    document.body.appendChild(textarea);
    press(' ', textarea);
    expect(onTogglePause).not.toHaveBeenCalled();
    document.body.removeChild(textarea);
  });
});
