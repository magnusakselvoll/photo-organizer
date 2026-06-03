import { useEffect, useRef, useState } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import type { PhotoDto } from '../api/types';
import { columnsForWidth } from '../hooks/useInfinitePhotos';
import PhotoCard from './PhotoCard';

const MIN_COL_WIDTH = 180;
const GAP = 12;
// How many rows from the bottom before we request more photos.
const LOAD_MORE_THRESHOLD = 3;

interface Props {
  photos: PhotoDto[];
  hasMore: boolean;
  isLoading: boolean;
  onLoadMore: () => void;
}

/**
 * Virtualized photo grid.
 *
 * Uses @tanstack/react-virtual for row-based windowing so that tens of
 * thousands of photos don't materialise in the DOM at once. Only rows
 * within (or close to) the visible viewport are rendered.
 *
 * Columns-per-row is derived from the container width via a ResizeObserver,
 * using the same minmax(180px, 1fr) / 12px-gap layout as the CSS grid.
 * Each row is square (height = column width + gap).
 *
 * When the user scrolls near the bottom, `onLoadMore` is called to fetch the
 * next cursor page.
 */
export default function PhotoGrid({ photos, hasMore, isLoading, onLoadMore }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [containerWidth, setContainerWidth] = useState(0);

  // Measure container width (and track resize).
  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const ro = new ResizeObserver(entries => {
      const w = entries[0]?.contentRect.width ?? 0;
      setContainerWidth(w);
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const cols = columnsForWidth(containerWidth, MIN_COL_WIDTH, GAP);
  const colWidth = containerWidth > 0
    ? Math.floor((containerWidth - GAP * (cols - 1)) / cols)
    : MIN_COL_WIDTH;
  const rowHeight = colWidth + GAP; // square cards + gap between rows

  const rowCount = Math.ceil(photos.length / cols);

  const virtualizer = useVirtualizer({
    count: rowCount,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => rowHeight,
    overscan: 5,
  });

  // Trigger loadMore when near the bottom.
  const virtualItems = virtualizer.getVirtualItems();
  useEffect(() => {
    if (virtualItems.length === 0) return;
    const lastRendered = virtualItems[virtualItems.length - 1].index;
    if (hasMore && !isLoading && rowCount - lastRendered <= LOAD_MORE_THRESHOLD) {
      onLoadMore();
    }
  }, [virtualItems, hasMore, isLoading, rowCount, onLoadMore]);

  return (
    <div
      ref={scrollRef}
      className="photo-grid-scroll"
    >
      <div
        style={{
          height: virtualizer.getTotalSize(),
          position: 'relative',
        }}
      >
        {virtualItems.map(virtualRow => {
          const startIdx = virtualRow.index * cols;
          const rowPhotos = photos.slice(startIdx, startIdx + cols);

          return (
            <div
              key={virtualRow.key}
              style={{
                position: 'absolute',
                top: 0,
                left: 0,
                width: '100%',
                transform: `translateY(${virtualRow.start}px)`,
                display: 'grid',
                gridTemplateColumns: `repeat(${cols}, 1fr)`,
                gap: `${GAP}px`,
                paddingBottom: `${GAP}px`,
              }}
            >
              {rowPhotos.map(photo => (
                <PhotoCard key={photo.id} photo={photo} />
              ))}
            </div>
          );
        })}
      </div>

      {isLoading && (
        <p className="status" style={{ textAlign: 'center', padding: '1rem' }}>
          Loading…
        </p>
      )}
    </div>
  );
}
