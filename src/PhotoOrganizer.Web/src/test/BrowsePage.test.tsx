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
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 50,
};

vi.mock('../api/client', () => ({
  getFolders: vi.fn(),
  getPhotos: vi.fn(),
  imageUrl: (id: string) => `/api/photos/${id}/image`,
}));

beforeEach(() => {
  vi.mocked(client.getFolders).mockResolvedValue(mockFolders);
  vi.mocked(client.getPhotos).mockResolvedValue(mockPage);
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

test('changing the folder filter resets to page 1 and refetches', async () => {
  renderBrowse();
  await screen.findByText('test.jpg');

  const selects = screen.getAllByRole('combobox');
  await userEvent.selectOptions(selects[0], '/photos/2024');

  await waitFor(() => {
    expect(client.getPhotos).toHaveBeenCalledWith(
      expect.objectContaining({ folder: '/photos/2024', page: 1 }),
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
      expect.objectContaining({ type: 'Originals', page: 1 }),
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
