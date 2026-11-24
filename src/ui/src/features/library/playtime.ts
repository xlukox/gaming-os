/** Translation key and parameters for a game's playtime. */
export function playtimeLabel(minutes: number | null): [key: string, params?: Record<string, number>] {
  if (minutes === null) return ['library.playtimeUnknown'];
  if (minutes === 0) return ['library.neverPlayed'];
  if (minutes < 60) return ['library.playtimeMinutes', { minutes }];
  return ['library.playtime', { hours: Math.round(minutes / 60) }];
}
