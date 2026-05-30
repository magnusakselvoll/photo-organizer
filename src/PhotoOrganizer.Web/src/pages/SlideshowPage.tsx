import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { getConfig } from '../api/client';
import type { SlideshowConfigDto } from '../api/types';
import { useSlideshow } from '../hooks/useSlideshow';
import { useKeyboardNavigation } from '../hooks/useKeyboardNavigation';
import { SlideshowImage } from '../components/SlideshowImage';
import { generateKenBurnsConfig } from '../utils/kenBurns';
import type { KenBurnsConfig } from '../utils/kenBurns';
import type { PhotoDto } from '../api/types';

const DEFAULT_CONFIG: SlideshowConfigDto = { intervalSeconds: 8, transitionMs: 500 };
const CONTROLS_HIDE_DELAY_MS = 3_000;

interface DisplayState {
  photo: PhotoDto;
  kenBurns: KenBurnsConfig;
  key: number;
}

export default function SlideshowPage() {
  const navigate = useNavigate();
  const [config, setConfig] = useState<SlideshowConfigDto>(DEFAULT_CONFIG);

  // Load config once on mount; fall back to defaults on error
  useEffect(() => {
    getConfig()
      .then(c => setConfig(c.slideshow))
      .catch(() => { /* use defaults */ });
  }, []);

  const intervalMs = config.intervalSeconds * 1000;
  const transitionMs = config.transitionMs;

  const slideshow = useSlideshow({ intervalMs });

  // Crossfade: maintain current + previous display state
  const [currentDisplay, setCurrentDisplay] = useState<DisplayState | null>(null);
  const [previousDisplay, setPreviousDisplay] = useState<DisplayState | null>(null);
  const displayKeyRef = useRef(0);
  const lastPhotoIdRef = useRef<string | null>(null);

  useEffect(() => {
    const photo = slideshow.currentPhoto;
    if (!photo || photo.id === lastPhotoIdRef.current) return;

    lastPhotoIdRef.current = photo.id;

    setCurrentDisplay(prev => {
      if (prev) {
        setPreviousDisplay(prev);
        setTimeout(() => setPreviousDisplay(null), transitionMs);
      }
      displayKeyRef.current += 1;
      return {
        photo,
        kenBurns: generateKenBurnsConfig(intervalMs),
        key: displayKeyRef.current,
      };
    });
  }, [slideshow.currentPhoto, intervalMs, transitionMs]);

  // Controls overlay — show on cursor movement, hide after inactivity
  const [controlsVisible, setControlsVisible] = useState(false);
  const hideTimerRef = useRef<number | null>(null);

  const showControls = useCallback(() => {
    setControlsVisible(true);
    if (hideTimerRef.current !== null) clearTimeout(hideTimerRef.current);
    hideTimerRef.current = window.setTimeout(() => {
      setControlsVisible(false);
    }, CONTROLS_HIDE_DELAY_MS);
  }, []);

  useEffect(() => {
    return () => {
      if (hideTimerRef.current !== null) clearTimeout(hideTimerRef.current);
    };
  }, []);

  const handleExit = useCallback(() => navigate('/'), [navigate]);

  useKeyboardNavigation({
    onNext: slideshow.next,
    onPrevious: slideshow.previous,
    onTogglePause: slideshow.togglePause,
    onExit: handleExit,
  });

  // ── Render ──

  if (slideshow.status === 'empty') {
    return (
      <div className="slideshow-empty-state">
        <p>No photos to show yet.</p>
        <p>Run the crawler to index your photo library.</p>
        <button className="slideshow-exit-btn" onClick={handleExit}>Back to browse</button>
      </div>
    );
  }

  return (
    <div className="slideshow-root" onMouseMove={showControls}>
      {/* Photo layers */}
      {previousDisplay && (
        <SlideshowImage
          key={previousDisplay.key}
          photoId={previousDisplay.photo.id}
          photoName={previousDisplay.photo.fileName}
          kenBurns={previousDisplay.kenBurns}
          fadingOut
        />
      )}
      {currentDisplay && (
        <SlideshowImage
          key={currentDisplay.key}
          photoId={currentDisplay.photo.id}
          photoName={currentDisplay.photo.fileName}
          kenBurns={currentDisplay.kenBurns}
        />
      )}

      {/* Subtle error indicator */}
      {slideshow.hasError && (
        <div className="slideshow-error-indicator" title="Network error — retrying">⚠</div>
      )}

      {/* Controls overlay */}
      <div className={`slideshow-controls${controlsVisible ? ' visible' : ''}`}>
        <div className="slideshow-controls-inner">
          <button className="slideshow-control-btn" onClick={slideshow.previous} title="Previous (←)">‹</button>
          <button className="slideshow-control-btn" onClick={slideshow.togglePause} title={slideshow.paused ? 'Resume (P)' : 'Pause (P)'}>
            {slideshow.paused ? '▶' : '⏸'}
          </button>
          <button className="slideshow-control-btn" onClick={slideshow.next} title="Next (→ or Space)">›</button>
          <button className="slideshow-control-btn slideshow-exit-ctrl" onClick={handleExit} title="Exit (Esc)">✕</button>
        </div>
      </div>
    </div>
  );
}
