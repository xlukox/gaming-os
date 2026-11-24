import { expect, test } from 'vitest';
import { playtimeLabel } from './playtime';

test('picks the wording by how much was played', () => {
  expect(playtimeLabel(null)).toEqual(['library.playtimeUnknown']);
  expect(playtimeLabel(0)).toEqual(['library.neverPlayed']);
  expect(playtimeLabel(59)).toEqual(['library.playtimeMinutes', { minutes: 59 }]);
  expect(playtimeLabel(60)).toEqual(['library.playtime', { hours: 1 }]);
  expect(playtimeLabel(6123)).toEqual(['library.playtime', { hours: 102 }]);
});
