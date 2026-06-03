import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import BrowsePage from '../pages/BrowsePage';
import * as client from '../api/client';

const mockFolders = [
  { path: '/photos/2024', label: '2024', type: 'Originals' as const, enabled: true },
];

const mockPage = {
  items: [
    {
      id: 'abc-123',
      filePath: '/photos/2024/test.jpg',
      fileName: 'test.jpg',
      capturedAt: '2024-01-15T10:00:00Z',
      effectiveDate: '2024-01-15T10:00:00Z',
      folderType: 'Originals' as const,
      duplicateGroupId: null,
      isPreferred: true,
      tags: [],
      versions: [],
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 50,
  nextCursor: null,
};

const mockIndexStatus = { complete: true, count: 1 };

vi.mock('../api/client', () => ({
  getFolders: vi.fn(),
  getPhotos: vi.fn(),
  getIndexStatus: vi.fn(),
  imageUrl: (id: string) => `/api/photos/${id}/image`,
}));

// PhotoGrid uses ResizeObserver + @tanstack/react-virtual which don't work in
// jsdom. BrowsePage tests are concerned with filter/fetch behaviour, not grid
// layout, so we stub the grid to a simple list of filenames.
vi.mock('../components/PhotoGrid', () => ({
  default: ({ photos }: { photos: Array<{ id: string; fileName: string }> }) => (
    <ul>
      {photos.map(p => (
        <li key={p.id}>{p.fileName}</li>
      ))}
    </ul>
  ),
}));

beforeEach(() => {
  vi.mocked(client.getFolders).mockResolvedValue(mockFolders);
  vi.mocked(client.getPhotos).mockResolvedValue(mockPage);
  vi.mocked(client.getIndexStatus).mockResolvedValue(mockIndexStatus);
});

/** Render BrowsePage with an optional initial URL (e.g. '/?folder=x'). */
function renderBrowse(initialEntry = '/') {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <BrowsePage />
    </MemoryRouter>,
  );
}

test('renders photos returned by the API', async () => {
  renderBrowse();
  expect(await screen.findByText('test.jpg')).toBeInTheDocument();
});

test('folder options are populated from the API', async () => {
  renderBrowse();
  expect(await screen.findByRole('option', { name: '2024' })).toBeInTheDocument();
});

test('changing the folder filter refetches with the chosen folder', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  const selects = screen.getAllByRole('combobox');
  await userEvent.selectOptions(selects[0], '/photos/2024');

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ folder: '/photos/2024' }),
    );
  });
});

test('changing the type filter refetches with the chosen type', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  const selects = screen.getAllByRole('combobox');
  await userEvent.selectOptions(selects[1], 'Originals');

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'Originals' }),
    );
  });
});

test('unchecking deduplicated only refetches with deduplicated false', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  await userEvent.click(screen.getByRole('checkbox'));

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ deduplicated: false }),
    );
  });
});

test('typing a filename search refetches with the debounced fileName', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  // Clear previous getPhotos calls so we can assert on the new one only.
  vi.mocked(client.getPhotos).mockClear();

  const searchInput = screen.getByRole('searchbox');
  await userEvent.type(searchInput, 'IMG');

  // The filename input is debounced (300 ms). waitFor polls for up to 1000 ms
  // by default, which is enough time for the debounce to fire.
  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ fileName: 'IMG' }),
    );
  });
});

test('setting a date range refetches with dateFrom and dateTo', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  // Labels "From" and "To" implicitly associate with their nested date inputs.
  const fromInput = screen.getByLabelText('From');
  const toInput = screen.getByLabelText('To');

  fireEvent.change(fromInput, { target: { value: '2024-01-01' } });
  fireEvent.change(toInput, { target: { value: '2024-12-31' } });

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ dateFrom: '2024-01-01', dateTo: '2024-12-31' }),
    );
  });
});

test('filters are initialised from URL query params on mount', async () => {
  renderBrowse('/?type=Originals&deduplicated=false');
  await screen.findByText('test.jpg');

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'Originals', deduplicated: false }),
    );
  });
});
