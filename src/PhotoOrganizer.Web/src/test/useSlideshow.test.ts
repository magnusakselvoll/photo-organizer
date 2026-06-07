import { renderHook, act } from '@testing-library/react';
import { useSlideshow } from '../hooks/useSlideshow';
import * as client from '../api/client';

const INTERVAL_MS = 8_000;

const photo1 = {
  id: 'photo-1',
  filePath: '/photos/a.jpg',
  fileName: 'a.jpg',
  capturedAt: null,
  effectiveDate: null,
  folderType: 'Originals' as const,
  duplicateGroupId: null,
  isPreferred: true,
  tags: [],
  versions: [],
};

const photo2 = {
  id: 'photo-2',
  filePath: '/photos/b.jpg',
  fileName: 'b.jpg',
  capturedAt: null,
  effectiveDate: null,
  folderType: 'Originals' as const,
  duplicateGroupId: null,
  isPreferred: true,
  tags: [],
  versions: [],
};

vi.mock('../api/client', () => ({
  getSlideshowNext: vi.fn(),
  imageUrl: (id: string) => `/api/photos/${id}/image`,
}));

// Flush the microtask queue without relying on setTimeout (fake-timer-safe).
async function flushPromises() {
  await act(async () => {
    for (let i = 0; i < 20; i++) {
      await new Promise<void>(resolve => queueMicrotask(resolve));
    }
  });
}

beforeEach(() => {
  // Only fake timers — leave queueMicrotask/nextTick real so React's scheduler
  // and act() work correctly.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'] });
});

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe('useSlideshow', () => {
  test('shows the first photo after initial load', async () => {
    vi.mocked(client.getSlideshowNext).mockResolvedValue(photo1);

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    expect(result.current.status).toBe('ready');
    expect(result.current.currentPhoto?.id).toBe('photo-1');
  });

  test('shows empty status when no photos exist (404)', async () => {
    vi.mocked(client.getSlideshowNext).mockResolvedValue(null);

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    expect(result.current.status).toBe('empty');
    expect(result.current.currentPhoto).toBeNull();
  });

  test('auto-advances to the next photo after the interval', async () => {
    // photo1 (initial fetch) + photo2 (prefetch while photo1 shows) + photo1 (prefetch while photo2 shows)
    vi.mocked(client.getSlideshowNext)
      .mockResolvedValueOnce(photo1)
      .mockResolvedValueOnce(photo2)
      .mockResolvedValue(photo1);

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    expect(result.current.currentPhoto?.id).toBe('photo-1');

    // Advance past the interval; the hook should consume the prefetched photo2
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS);
    });
    await flushPromises();

    expect(result.current.currentPhoto?.id).toBe('photo-2');
  });

  test('pause stops auto-advance across an interval', async () => {
    vi.mocked(client.getSlideshowNext)
      .mockResolvedValueOnce(photo1)
      .mockResolvedValue(photo2);

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    expect(result.current.currentPhoto?.id).toBe('photo-1');

    // Pause
    act(() => { result.current.togglePause(); });
    expect(result.current.paused).toBe(true);

    // Advance past the interval — should NOT advance while paused
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS + 1_000);
    });
    await flushPromises();

    expect(result.current.currentPhoto?.id).toBe('photo-1');
  });

  test('resume after pause resumes auto-advance', async () => {
    vi.mocked(client.getSlideshowNext)
      .mockResolvedValueOnce(photo1)
      .mockResolvedValueOnce(photo2)
      .mockResolvedValue(photo1);

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    // Pause then immediately resume
    act(() => { result.current.togglePause(); });
    act(() => { result.current.togglePause(); });
    expect(result.current.paused).toBe(false);

    // Advance past interval — should advance now
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS);
    });
    await flushPromises();

    expect(result.current.currentPhoto?.id).toBe('photo-2');
  });

  test('unmount clears the timer and stops further fetches', async () => {
    vi.mocked(client.getSlideshowNext)
      .mockResolvedValueOnce(photo1)
      .mockResolvedValue(photo2);

    const { unmount } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    const callCountAfterLoad = vi.mocked(client.getSlideshowNext).mock.calls.length;

    unmount();

    // Advancing timers after unmount must not trigger further fetches
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS * 3);
    });

    expect(vi.mocked(client.getSlideshowNext).mock.calls.length).toBe(callCountAfterLoad);
  });

  test('a fetch failure sets hasError but the loop continues on the next interval', async () => {
    vi.mocked(client.getSlideshowNext)
      .mockResolvedValueOnce(photo1)   // initial load succeeds
      .mockRejectedValueOnce(new Error('network error'))  // prefetch fails (ignored)
      .mockRejectedValueOnce(new Error('network error'))  // next interval fetch fails
      .mockResolvedValueOnce(photo2)   // prefetch after next interval (ignored here)
      .mockResolvedValue(photo1);      // recovery fetch succeeds

    const { result } = renderHook(() => useSlideshow({ intervalMs: INTERVAL_MS }));
    await flushPromises();

    // First photo loaded fine
    expect(result.current.currentPhoto?.id).toBe('photo-1');
    expect(result.current.hasError).toBe(false);

    // Advance one interval — fetch fails, hasError should become true
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS);
    });
    await flushPromises();

    expect(result.current.hasError).toBe(true);
    // Still showing the last successful photo
    expect(result.current.currentPhoto?.id).toBe('photo-1');

    // Advance another interval — recovery fetch succeeds, hasError should clear
    await act(async () => {
      await vi.advanceTimersByTimeAsync(INTERVAL_MS);
    });
    await flushPromises();

    expect(result.current.hasError).toBe(false);
    expect(result.current.currentPhoto?.id).toBe('photo-2');
  });
});
