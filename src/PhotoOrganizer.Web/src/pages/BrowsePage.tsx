import { useEffect, useRef, useState } from 'react';
import { getFolders, getIndexStatus, getPhotos } from '../api/client';
import type { FolderDto } from '../api/types';
import PhotoGrid from '../components/PhotoGrid';
import { useInfinitePhotos } from '../hooks/useInfinitePhotos';

/** How many photos to fetch when polling for new arrivals. */
const LIVE_FETCH_LIMIT = 50;
/** Polling interval in ms. Stops when the index reports complete. */
const POLL_INTERVAL_MS = 4000;

export default function BrowsePage() {
  const [folders, setFolders] = useState<FolderDto[]>([]);

  const [folder, setFolder] = useState('');
  const [type, setType] = useState('all');
  const [deduplicatedOnly, setDeduplicatedOnly] = useState(true);

  const { items, hasMore, isLoading, error, totalCount, loadMore, mergeNewest } =
    useInfinitePhotos({ folder, type, deduplicated: deduplicatedOnly });

  // Track the last-seen index count so we can detect growth.
  const lastIndexCount = useRef<number | null>(null);
  const indexComplete = useRef(false);

  useEffect(() => {
    getFolders().then(setFolders).catch(() => {});
  }, []);

  // Live-update poller: polls /api/index/status every 4 s; when the count
  // grows, fetches the newest page and merges any new arrivals at the top.
  useEffect(() => {
    if (indexComplete.current) return;

    const id = setInterval(async () => {
      try {
        const status = await getIndexStatus();
        const prev = lastIndexCount.current;
        lastIndexCount.current = status.count;

        if (status.complete) {
          indexComplete.current = true;
          clearInterval(id);
        }

        if (prev !== null && status.count > prev) {
          // New photos indexed — fetch the freshest page and prepend unknowns.
          const page = await getPhotos({
            folder: folder || undefined,
            type: type !== 'all' ? type : undefined,
            deduplicated: deduplicatedOnly,
            limit: LIVE_FETCH_LIMIT,
          });
          mergeNewest(page.items);
        }
      } catch {
        // Ignore transient network errors; poller will retry.
      }
    }, POLL_INTERVAL_MS);

    return () => clearInterval(id);
  // Re-run the poller whenever filters change so we fetch against the correct set.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [folder, type, deduplicatedOnly]);

  function handleFolderChange(value: string) {
    setFolder(value);
    lastIndexCount.current = null;
  }

  function handleTypeChange(value: string) {
    setType(value);
    lastIndexCount.current = null;
  }

  function handleDeduplicatedChange(value: boolean) {
    setDeduplicatedOnly(value);
    lastIndexCount.current = null;
  }

  const showEmpty = !isLoading && !error && items.length === 0;

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
        {totalCount > 0 && (
          <span className="photo-count">{totalCount.toLocaleString()} photos</span>
        )}
      </div>

      {error && <p className="error">{error}</p>}
      {showEmpty && <p className="status">No photos found.</p>}

      {items.length > 0 && (
        <PhotoGrid
          photos={items}
          hasMore={hasMore}
          isLoading={isLoading}
          onLoadMore={loadMore}
        />
      )}

      {isLoading && items.length === 0 && <p className="status">Loading…</p>}
    </div>
  );
}
