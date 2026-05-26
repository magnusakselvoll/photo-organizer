import { Link } from 'react-router-dom';
import type { PhotoDto } from '../api/types';
import { imageUrl } from '../api/client';

interface Props {
  photo: PhotoDto;
}

export default function PhotoCard({ photo }: Props) {
  const date = photo.capturedAt
    ? new Date(photo.capturedAt).toLocaleDateString()
    : null;

  return (
    <Link to={`/photo/${photo.id}`} className="photo-card">
      <img src={imageUrl(photo.id)} alt={photo.fileName} loading="lazy" />
      <div className="photo-card-caption">
        <span className="photo-card-name">{photo.fileName}</span>
        {date && <span className="photo-card-date">{date}</span>}
      </div>
    </Link>
  );
}
