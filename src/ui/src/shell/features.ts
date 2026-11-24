import type { ComponentType } from 'react';

export interface Feature {
  id: string;
  /** Position in the navigation, lowest first. */
  order: number;
  /** Translation key of the navigation label. */
  titleKey: string;
  /** SVG path data for the navigation icon, drawn as a stroke on a 24x24 grid. */
  icon: string;
  Screen: ComponentType;
}

// Every features/<name>/feature.ts registers itself: adding a feature touches no shared file.
export const features = Object.values(
  import.meta.glob<Feature>('../features/*/feature.ts', { eager: true, import: 'default' }),
).sort((a, b) => a.order - b.order);
