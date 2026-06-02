import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import PhotoDetailPage from '../pages/PhotoDetailPage';
import * as client from '../api/client';

const mockPhoto = {
  id: 'abc-123',
  filePath: '/photos/2024/test.jpg',
  fileName: 'test.jpg',
  capturedAt: '2024-06-15T10:30:00Z',
  folderType: 'Originals' as const,
  duplicateGroupId: null,
  isPreferred: true,
  tags: ['vacation', 'summer'],
  versions: [],
};

vi.mock('../api/client', () => ({
  getPhoto: vi.fn(),
  imageUrl: (id: string) => `/api/photos/${id}/image`,
}));

function renderDetail(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/photo/${id}`]}>
      <Routes>
        <Route path="/photo/:id" element={<PhotoDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

test('renders photo file name, folder type, and tags', async () => {
  vi.mocked(client.getPhoto).mockResolvedValue(mockPhoto);
  renderDetail('abc-123');

  expect(await screen.findByText('test.jpg')).toBeInTheDocument();
  expect(screen.getByText('Originals')).toBeInTheDocument();
  expect(screen.getByText('vacation, summer')).toBeInTheDocument();
});

test('renders the image with the correct src', async () => {
  vi.mocked(client.getPhoto).mockResolvedValue(mockPhoto);
  renderDetail('abc-123');

  const img = await screen.findByRole('img');
  expect(img).toHaveAttribute('src', '/api/photos/abc-123/image');
});

test('renders not-found state when getPhoto returns null', async () => {
  vi.mocked(client.getPhoto).mockResolvedValue(null);
  renderDetail('unknown-id');

  expect(await screen.findByText('Photo not found.')).toBeInTheDocument();
});

test('renders a back link to the browse page', async () => {
  vi.mocked(client.getPhoto).mockResolvedValue(mockPhoto);
  renderDetail('abc-123');

  const link = await screen.findByRole('link', { name: /back to browse/i });
  expect(link).toHaveAttribute('href', '/');
});
