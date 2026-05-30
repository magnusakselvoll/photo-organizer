import type { CSSProperties } from 'react';
import { imageUrl } from '../api/client';
import type { KenBurnsConfig } from '../utils/kenBurns';

interface SlideshowImageProps {
  photoId: string;
  photoName: string;
  kenBurns: KenBurnsConfig;
  fadingOut?: boolean;
}

export function SlideshowImage({ photoId, photoName, kenBurns, fadingOut = false }: SlideshowImageProps) {
  const style: CSSProperties = {
    '--kb-scale-from': kenBurns.scaleFrom,
    '--kb-scale-to': kenBurns.scaleTo,
    '--kb-x-from': kenBurns.xFrom,
    '--kb-y-from': kenBurns.yFrom,
    '--kb-x-to': kenBurns.xTo,
    '--kb-y-to': kenBurns.yTo,
    '--kb-duration': kenBurns.duration,
  } as CSSProperties;

  return (
    <div className={`slideshow-image-wrapper${fadingOut ? ' fading-out' : ''}`}>
      <img
        src={imageUrl(photoId)}
        alt={photoName}
        className="slideshow-image ken-burns"
        style={style}
      />
    </div>
  );
}
