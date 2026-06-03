import { useEffect, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { getFolders, getIndexStatus, getPhotos } from '../api/client';
import type { FolderDto } from '../api/types';
import PhotoGrid from '../components/PhotoGrid';
import { useInfinitePhotos } from '../hooks/useInfinitePhotos';

/** How many photos to fetch when polling for new arrivals. */
const LIVE_FETCH_LIMIT = 50;
/** Polling interval in ms. Stops when the index reports complete. */
const POLL_INTERVAL_MS = 4000;
/** Debounce delay for the filename search input in ms. */
const FILENAME_DEBOUNCE_MS = 300;

export default function BrowsePage() {
  const [folders, setFolders] = useState<FolderDto[]>([]);
  const [searchParams, setSearchParams] = useSearchParams();

  // All filter values read from URL params; defaults match the server defaults.
  // Absent params fall back to their default values so the URL stays clean.
  const folder = searchParams.get('folder') ?? '';
  const type = searchParams.get('type') ?? 'all';
  const deduplicatedOnly = searchParams.get('deduplicated') !== 'false'; // default true; only 'false' overrides
  const fileName = searchParams.get('fileName') ?? '';
  const dateFrom = searchParams.get('dateFrom') ?? '';
  const dateTo = searchParams.get('dateTo') ?? '';

  // Local state for the raw filename input so it responds instantly to keystrokes.
  // The debounced value is written to the URL after FILENAME_DEBOUNCE_MS, which then
  // drives the hook and the API call.
  const [fileNameInput, setFileNameInput] = useState(fileName);

  const { items, hasMore, isLoading, error, totalCount, loadMore, mergeNewest } =
    useInfinitePhotos({ folder, type, deduplicated: deduplicatedOnly, fileName, dateFrom, dateTo });

  // Track the last-seen index count so we can detect growth.
  const lastIndexCount = useRef<number | null>(null);
  const indexComplete = useRef(false);

  useEffect(() => {
    getFolders().then(setFolders).catch(() => {});
  }, []);

  // Reset live-update baseline whenever any filter changes.
  useEffect(() => {
    lastIndexCount.current = null;
  }, [folder, type, deduplicatedOnly, fileName, dateFrom, dateTo]);

  // Debounce filename input → URL.
  useEffect(() => {
    const id = setTimeout(() => {
      setSearchParams(
        prev => {
          const next = new URLSearchParams(prev);
          if (fileNameInput) {
            next.set('fileName', fileNameInput);
          } else {
            next.delete('fileName');
          }
          return next;
        },
        { replace: true },
      );
    }, FILENAME_DEBOUNCE_MS);
    return () => clearTimeout(id);
  }, [fileNameInput, setSearchParams]);

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
          // Pass the active filters so only in-filter photos are merged.
          const page = await getPhotos({
            folder: folder || undefined,
            type: type !== 'all' ? type : undefined,
            deduplicated: deduplicatedOnly,
            fileName: fileName || undefined,
            dateFrom: dateFrom || undefined,
            dateTo: dateTo || undefined,
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
  }, [folder, type, deduplicatedOnly, fileName, dateFrom, dateTo]);

  function handleFolderChange(value: string) {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev);
        if (value) next.set('folder', value); else next.delete('folder');
        return next;
      },
      { replace: true },
    );
  }

  function handleTypeChange(value: string) {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev);
        if (value !== 'all') next.set('type', value); else next.delete('type');
        return next;
      },
      { replace: true },
    );
  }

  function handleDeduplicatedChange(checked: boolean) {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev);
        if (!checked) next.set('deduplicated', 'false'); else next.delete('deduplicated');
        return next;
      },
      { replace: true },
    );
  }

  function handleDateFromChange(value: string) {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev);
        if (value) next.set('dateFrom', value); else next.delete('dateFrom');
        return next;
      },
      { replace: true },
    );
  }

  function handleDateToChange(value: string) {
    setSearchParams(
      prev => {
        const next = new URLSearchParams(prev);
        if (value) next.set('dateTo', value); else next.delete('dateTo');
        return next;
      },
      { replace: true },
    );
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
        <label>
          Filename
          <input
            className="filter-text"
            type="search"
            placeholder="Search by filename…"
            value={fileNameInput}
            onChange={e => setFileNameInput(e.target.value)}
          />
        </label>
        <label>
          From
          <input
            className="filter-date"
            type="date"
            value={dateFrom}
            onChange={e => handleDateFromChange(e.target.value)}
          />
        </label>
        <label>
          To
          <input
            className="filter-date"
            type="date"
            value={dateTo}
            onChange={e => handleDateToChange(e.target.value)}
          />
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
