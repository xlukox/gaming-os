import type { Bridge, GameDto } from './api';

// Fictional titles on purpose: no real game names or artwork in the repository.
const titles: Omit<GameDto, 'coverUrl' | 'heroUrl' | 'logoUrl'>[] = [
  { id: 1, title: 'Neon Drift', platform: 'steam', installed: true, playtimeMinutes: 2530, accentColor: '#2fe6de' },
  { id: 2, title: 'Hollow Tides', platform: 'epic', installed: true, playtimeMinutes: null, accentColor: '#3d7bff' },
  { id: 3, title: 'Iron Orchard', platform: 'gog', installed: true, playtimeMinutes: 410, accentColor: '#9b5cff' },
  { id: 4, title: 'Starfall Protocol', platform: 'steam', installed: false, playtimeMinutes: 6120, accentColor: '#ff5ca8' },
  { id: 5, title: 'Paper Kingdoms', platform: 'gog', installed: true, playtimeMinutes: 95, accentColor: '#5cc8ff' },
  { id: 6, title: 'Deep Signal', platform: 'steam', installed: true, playtimeMinutes: 1280, accentColor: '#7a6bff' },
  { id: 7, title: 'Ashen Circuit', platform: 'epic', installed: false, playtimeMinutes: null, accentColor: '#22d3a6' },
  { id: 8, title: 'Lantern Road', platform: 'manual', installed: true, playtimeMinutes: 30, accentColor: '#c05cff' },
];
// No artwork in the mock: the UI falls back to the generated visuals.
const games: GameDto[] = titles.map((game) => ({ ...game, coverUrl: null, heroUrl: null, logoUrl: null }));

/** Stands in for the host so the UI runs in a plain browser (`npm run dev`). */
export const mockBridge = {
  call: async (method: string, params?: { gameId: number }) => {
    if (method === 'library.list') return games;
    // Every second game has news, so both states of the UI can be seen.
    if (method === 'intel.news' && params && params.gameId % 2 === 1) {
      return [
        { title: 'Update 2.2: three new night tracks', url: 'https://example.com/news/1', publishedAt: '2026-11-21T10:00:00Z' },
        { title: 'Hotfix for crashes when loading the garage', url: 'https://example.com/news/2', publishedAt: '2026-11-12T16:30:00Z' },
      ];
    }
    return method === 'intel.news' ? [] : undefined;
  },
  on: () => () => {},
} as Bridge;
