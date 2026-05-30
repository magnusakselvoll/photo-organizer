import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getPhoto, imageUrl } from '../api/client';
import type { PhotoDto } from '../api/types';

export default function PhotoDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [photo, setPhoto] = useState<PhotoDto | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    getPhoto(id)
      .then(setPhoto)
      .catch(e => setError(String(e)));
  }, [id]);

  if (error) return <p className="error">{error}</p>;

  if (photo === undefined) return <p className="status">Loading…</p>;

  if (photo === null) {
    return (
      <div className="detail-not-found">
        <p>Photo not found.</p>
        <Link to="/">Back to browse</Link>
      </div>
    );
  }

  const capturedAt = photo.capturedAt
    ? new Date(photo.capturedAt).toLocaleString()
    : null;

  return (
    <div className="photo-detail">
      <Link to="/" className="back-link">← Back to browse</Link>
      <img
        src={imageUrl(photo.id)}
        alt={photo.fileName}
        className="detail-image"
      />
      <dl className="detail-meta">
        <dt>File name</dt>
        <dd>{photo.fileName}</dd>
        <dt>File path</dt>
        <dd>{photo.filePath}</dd>
        <dt>Captured</dt>
        <dd>{capturedAt ?? '—'}</dd>
        <dt>Folder type</dt>
        <dd>{photo.folderType}</dd>
        {photo.tags.length > 0 && (
          <>
            <dt>Tags</dt>
            <dd>{photo.tags.join(', ')}</dd>
          </>
        )}
        {photo.duplicateGroupId && (
          <>
            <dt>Duplicate group</dt>
            <dd>
              {photo.duplicateGroupId}
              {' '}
              {photo.isPreferred ? '(preferred)' : '(duplicate)'}
            </dd>
          </>
        )}
      </dl>
    </div>
  );
}
