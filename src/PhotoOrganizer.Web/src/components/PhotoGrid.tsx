import type { PhotoDto } from '../api/types';
import PhotoCard from './PhotoCard';

interface Props {
  photos: PhotoDto[];
}

export default function PhotoGrid({ photos }: Props) {
  return (
    <div className="photo-grid">
      {photos.map(photo => (
        <PhotoCard key={photo.id} photo={photo} />
      ))}
    </div>
  );
}
