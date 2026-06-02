import type { PhotoDto } from '../api/types';

interface PhotoInfoPanelProps {
  photo: PhotoDto;
  intervalSeconds: number;
}

function formatDate(value: string | null): string {
  if (!value) return '—';
  try {
    return new Date(value).toLocaleString(undefined, {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return value;
  }
}

export function PhotoInfoPanel({ photo, intervalSeconds }: PhotoInfoPanelProps) {
  return (
    <div className="slideshow-info-panel">
      <dl className="slideshow-info-dl">
        <dt>File</dt>
        <dd>{photo.fileName}</dd>

        <dt>Captured</dt>
        <dd>{formatDate(photo.capturedAt)}</dd>

        <dt>Folder type</dt>
        <dd>{photo.folderType}</dd>

        {photo.tags.length > 0 && (
          <>
            <dt>Tags</dt>
            <dd>{photo.tags.join(', ')}</dd>
          </>
        )}

        <dt>Path</dt>
        <dd className="slideshow-info-path">{photo.filePath}</dd>

        {(photo.versions ?? []).length > 1 && (
          <>
            <dt>Versions</dt>
            <dd>
              <ul className="slideshow-info-versions">
                {(photo.versions ?? []).map(v => (
                  <li key={v.id} className={v.isPreferred ? 'version-preferred' : undefined}>
                    {v.fileName} <span className="version-folder-type">({v.folderType})</span>
                    {v.isPreferred && <span className="version-badge"> ★</span>}
                  </li>
                ))}
              </ul>
            </dd>
          </>
        )}

        <dt>Display time</dt>
        <dd>{intervalSeconds}s</dd>
      </dl>
    </div>
  );
}
