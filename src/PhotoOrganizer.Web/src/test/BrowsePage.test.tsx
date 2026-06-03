import { render, screen, waitFor } from '@testing-library/react';
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

function renderBrowse() {
  return render(
    <MemoryRouter>
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
