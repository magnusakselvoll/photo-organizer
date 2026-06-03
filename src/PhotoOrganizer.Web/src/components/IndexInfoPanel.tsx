import { useState, useEffect } from 'react';
import { getIndexStats } from '../api/client';
import type { IndexStatsDto } from '../api/types';

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function IndexInfoPanel() {
  const [stats, setStats] = useState<IndexStatsDto | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    getIndexStats()
      .then(setStats)
      .catch(() => setError(true));
  }, []);

  if (error) {
    return <div className="slideshow-index-panel">Failed to load index info.</div>;
  }

  if (!stats) {
    return <div className="slideshow-index-panel slideshow-index-loading">Loading…</div>;
  }

  return (
    <div className="slideshow-index-panel">
      <h3 className="slideshow-shortcut-title">Index {stats.complete ? '' : '(indexing…)'}</h3>

      <dl className="slideshow-info-dl slideshow-index-summary">
        <dt>Photos</dt>
        <dd>{stats.totalPhotoCount.toLocaleString()}</dd>
        <dt>Sidecar size</dt>
        <dd>{formatBytes(stats.sidecarSizeBytes)}</dd>
        <dt>Folders</dt>
        <dd>{stats.folders.length.toLocaleString()}</dd>
      </dl>

      {stats.folders.length > 0 && (
        <table className="slideshow-index-table">
          <thead>
            <tr>
              <th>Folder</th>
              <th>Type</th>
              <th className="slideshow-index-count">Photos</th>
            </tr>
          </thead>
          <tbody>
            {stats.folders.map(f => (
              <tr key={f.path}>
                <td className="slideshow-index-label" title={f.path}>{f.label}</td>
                <td className="slideshow-index-type">{f.type}</td>
                <td className="slideshow-index-count">{f.photoCount.toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
