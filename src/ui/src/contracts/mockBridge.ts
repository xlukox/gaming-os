import type { Bridge, GameDto } from './api';

// Fictional titles on purpose: no real game names or artwork in the repository.
const games: GameDto[] = [
  { id: 1, title: 'Neon Drift', platform: 'steam', installed: true, playtimeMinutes: 2530, accentColor: '#2fe6de' },
  { id: 2, title: 'Hollow Tides', platform: 'epic', installed: true, playtimeMinutes: null, accentColor: '#3d7bff' },
  { id: 3, title: 'Iron Orchard', platform: 'gog', installed: true, playtimeMinutes: 410, accentColor: '#9b5cff' },
  { id: 4, title: 'Starfall Protocol', platform: 'steam', installed: false, playtimeMinutes: 6120, accentColor: '#ff5ca8' },
  { id: 5, title: 'Paper Kingdoms', platform: 'gog', installed: true, playtimeMinutes: 95, accentColor: '#5cc8ff' },
  { id: 6, title: 'Deep Signal', platform: 'steam', installed: true, playtimeMinutes: 1280, accentColor: '#7a6bff' },
  { id: 7, title: 'Ashen Circuit', platform: 'epic', installed: false, playtimeMinutes: null, accentColor: '#22d3a6' },
  { id: 8, title: 'Lantern Road', platform: 'manual', installed: true, playtimeMinutes: 30, accentColor: '#c05cff' },
];

/** Stands in for the host so the UI runs in a plain browser (`npm run dev`). */
export const mockBridge = {
  call: async (method: string) => (method === 'library.list' ? games : undefined),
  on: () => () => {},
} as Bridge;
