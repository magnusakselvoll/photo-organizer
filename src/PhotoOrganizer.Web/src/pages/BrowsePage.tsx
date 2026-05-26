import { useState, useEffect, useReducer } from 'react';
import { getFolders, getPhotos } from '../api/client';
import type { FolderDto, PhotoPageDto } from '../api/types';
import PhotoGrid from '../components/PhotoGrid';
import Pagination from '../components/Pagination';

type FetchState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: PhotoPageDto }
  | { status: 'error'; message: string };

type FetchAction =
  | { type: 'start' }
  | { type: 'success'; data: PhotoPageDto }
  | { type: 'error'; message: string };

function fetchReducer(_: FetchState, action: FetchAction): FetchState {
  switch (action.type) {
    case 'start': return { status: 'loading' };
    case 'success': return { status: 'success', data: action.data };
    case 'error': return { status: 'error', message: action.message };
  }
}

export default function BrowsePage() {
  const [folders, setFolders] = useState<FolderDto[]>([]);
  const [fetchState, dispatch] = useReducer(fetchReducer, { status: 'idle' });

  const [folder, setFolder] = useState('');
  const [type, setType] = useState('all');
  const [deduplicatedOnly, setDeduplicatedOnly] = useState(true);
  const [currentPage, setCurrentPage] = useState(1);

  useEffect(() => {
    getFolders().then(setFolders).catch(() => {});
  }, []);

  useEffect(() => {
    dispatch({ type: 'start' });
    getPhotos({
      folder: folder || undefined,
      type: type !== 'all' ? type : undefined,
      deduplicated: deduplicatedOnly,
      page: currentPage,
    })
      .then(data => dispatch({ type: 'success', data }))
      .catch(e => dispatch({ type: 'error', message: String(e) }));
  }, [folder, type, deduplicatedOnly, currentPage]);

  function handleFolderChange(value: string) {
    setFolder(value);
    setCurrentPage(1);
  }

  function handleTypeChange(value: string) {
    setType(value);
    setCurrentPage(1);
  }

  function handleDeduplicatedChange(value: boolean) {
    setDeduplicatedOnly(value);
    setCurrentPage(1);
  }

  return (
    <div className="browse-page">
      <div className="filter-bar">
        <label>
          Folder
          <select value={folder} onChange={e => handleFolderChange(e.target.value)}>
            <option value="">All folders</option>
            {folders.map(f => (
              <option key={f.path} value={f.path}>{f.label}</option>
            ))}
          </select>
        </label>
        <label>
          Type
          <select value={type} onChange={e => handleTypeChange(e.target.value)}>
            <option value="all">All types</option>
            <option value="Originals">Originals</option>
            <option value="Edits">Edits</option>
            <option value="Mixed">Mixed</option>
          </select>
        </label>
        <label>
          <input
            type="checkbox"
            checked={deduplicatedOnly}
            onChange={e => handleDeduplicatedChange(e.target.checked)}
          />
          Deduplicated only
        </label>
      </div>

      {fetchState.status === 'error' && (
        <p className="error">{fetchState.message}</p>
      )}
      {fetchState.status === 'loading' && <p className="status">Loading…</p>}
      {fetchState.status === 'success' && fetchState.data.items.length === 0 && (
        <p className="status">No photos found.</p>
      )}
      {fetchState.status === 'success' && fetchState.data.items.length > 0 && (
        <PhotoGrid photos={fetchState.data.items} />
      )}
      {fetchState.status === 'success' && (
        <Pagination
          page={currentPage}
          pageSize={fetchState.data.pageSize}
          totalCount={fetchState.data.totalCount}
          onPageChange={setCurrentPage}
        />
      )}
    </div>
  );
}
