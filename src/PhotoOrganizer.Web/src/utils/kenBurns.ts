export interface KenBurnsConfig {
  scaleFrom: number;
  scaleTo: number;
  xFrom: string;
  yFrom: string;
  xTo: string;
  yTo: string;
  duration: string;
}

function randomInRange(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

export function generateKenBurnsConfig(intervalMs: number): KenBurnsConfig {
  // Random zoom direction: in or out
  const zoomIn = Math.random() > 0.5;

  // Scale range: 1.0-1.05 to 1.22-1.30 (~22-30% total zoom, clearly perceptible)
  const scaleSmall = randomInRange(1.0, 1.05);
  const scaleLarge = randomInRange(1.22, 1.30);

  // Random pan direction with randomized amount (8-14%)
  const panAmount = randomInRange(8, 14);
  const panDirections = [
    { x: panAmount, y: 0 },                           // right
    { x: -panAmount, y: 0 },                          // left
    { x: 0, y: panAmount },                           // down
    { x: 0, y: -panAmount },                          // up
    { x: panAmount * 0.7, y: panAmount * 0.7 },       // diagonal down-right
    { x: -panAmount * 0.7, y: panAmount * 0.7 },      // diagonal down-left
    { x: panAmount * 0.7, y: -panAmount * 0.7 },      // diagonal up-right
    { x: -panAmount * 0.7, y: -panAmount * 0.7 },     // diagonal up-left
  ];
  const pan = panDirections[Math.floor(Math.random() * panDirections.length)];

  // Duration spans the full slide interval so the drift continuously fills the display time
  const duration = intervalMs / 1000;

  if (zoomIn) {
    return {
      scaleFrom: scaleSmall,
      scaleTo: scaleLarge,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  } else {
    return {
      scaleFrom: scaleLarge,
      scaleTo: scaleSmall,
      xFrom: '0%',
      yFrom: '0%',
      xTo: `${pan.x}%`,
      yTo: `${pan.y}%`,
      duration: `${duration.toFixed(1)}s`,
    };
  }
}
