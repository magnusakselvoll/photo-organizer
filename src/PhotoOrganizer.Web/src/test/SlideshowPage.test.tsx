import { render, screen, act, fireEvent } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import SlideshowPage from '../pages/SlideshowPage';
import * as client from '../api/client';

const mockPhoto1 = {
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

const mockPhoto2 = {
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
  getConfig: vi.fn(),
  getSlideshowNext: vi.fn(),
  imageUrl: (id: string) => `/api/photos/${id}/image`,
}));

function renderSlideshow() {
  return render(
    <MemoryRouter initialEntries={['/slideshow']}>
      <Routes>
        <Route path="/slideshow" element={<SlideshowPage />} />
        <Route path="/" element={<div>Browse page</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

// Flush microtask queue without relying on setTimeout (fake-timer-safe).
// Only fakes setTimeout/Date, not queueMicrotask, so this is safe.
async function flushPromises() {
  await act(async () => {
    for (let i = 0; i < 20; i++) {
      await new Promise<void>(resolve => queueMicrotask(resolve));
    }
  });
}

// Fire a keyboard key on window (where our listener is attached)
function pressKey(key: string) {
  fireEvent.keyDown(window, { key });
}

beforeEach(() => {
  // Only fake setTimeout/clearTimeout/Date — leave setImmediate, queueMicrotask,
  // and nextTick real so React 18's scheduler and act() work correctly.
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'Date'] });
  vi.mocked(client.getConfig).mockResolvedValue({
    scanRoots: [],
    slideshow: { intervalSeconds: 8, transitionMs: 500 },
  });
});

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

test('renders an image after the first photo loads', async () => {
  vi.mocked(client.getSlideshowNext).mockResolvedValue(mockPhoto1);

  renderSlideshow();
  await flushPromises();

  const img = screen.getByRole('img', { name: 'a.jpg' });
  expect(img).toHaveAttribute('src', '/api/photos/photo-1/image');
});

test('shows empty state when no photos exist (404)', async () => {
  vi.mocked(client.getSlideshowNext).mockResolvedValue(null);

  renderSlideshow();
  await flushPromises();

  expect(screen.getByText(/no photos to show/i)).toBeInTheDocument();
});

test('pressing ArrowRight shows the prefetched photo instantly', async () => {
  // photo1 (initial) + photo2 (prefetch while photo1 shows) + photo1 (prefetch while photo2 shows)
  vi.mocked(client.getSlideshowNext)
    .mockResolvedValueOnce(mockPhoto1)
    .mockResolvedValueOnce(mockPhoto2)
    .mockResolvedValue(mockPhoto1);

  renderSlideshow();
  await flushPromises();

  // After initial load: getSlideshowNext called twice — once for photo1, once for the prefetch
  expect(vi.mocked(client.getSlideshowNext)).toHaveBeenCalledTimes(2);
  expect(screen.getByRole('img', { name: 'a.jpg' })).toHaveAttribute('src', '/api/photos/photo-1/image');

  // ArrowRight: uses the already-prefetched photo2, then starts prefetching the next one
  await act(async () => {
    pressKey('ArrowRight');
  });
  await flushPromises();

  // One more call for the prefetch after photo2 is shown
  expect(vi.mocked(client.getSlideshowNext)).toHaveBeenCalledTimes(3);
  expect(screen.getByRole('img', { name: 'b.jpg' })).toHaveAttribute('src', '/api/photos/photo-2/image');
});

test('pressing Escape navigates back to browse page', async () => {
  vi.mocked(client.getSlideshowNext).mockResolvedValue(mockPhoto1);

  renderSlideshow();
  await flushPromises();

  await act(async () => {
    pressKey('Escape');
    // Flush any async work triggered by navigation
    for (let i = 0; i < 10; i++) {
      await new Promise<void>(resolve => queueMicrotask(resolve));
    }
  });

  expect(screen.getByText('Browse page')).toBeInTheDocument();
});

test('auto-advances after the configured interval using the prefetched photo', async () => {
  // photo1 (initial) + photo2 (prefetch while photo1 shows) + any (prefetch while photo2 shows)
  vi.mocked(client.getSlideshowNext)
    .mockResolvedValueOnce(mockPhoto1)
    .mockResolvedValueOnce(mockPhoto2)
    .mockResolvedValue(mockPhoto1);

  renderSlideshow();
  await flushPromises();

  // After initial load: 2 calls — photo1 shown + photo2 prefetched
  expect(vi.mocked(client.getSlideshowNext)).toHaveBeenCalledTimes(2);
  expect(screen.getByRole('img', { name: 'a.jpg' })).toHaveAttribute('src', '/api/photos/photo-1/image');

  // Timer fires: consumes prefetched photo2 (no extra fetch), starts prefetch for next
  await act(async () => {
    await vi.advanceTimersByTimeAsync(8_000);
  });
  await flushPromises();

  // One more call for the prefetch after photo2 is shown
  expect(vi.mocked(client.getSlideshowNext)).toHaveBeenCalledTimes(3);
  expect(screen.getByRole('img', { name: 'b.jpg' })).toHaveAttribute('src', '/api/photos/photo-2/image');
});
