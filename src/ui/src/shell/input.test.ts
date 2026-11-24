import { expect, test } from 'vitest';
import { heldActions, newlyPressed, type PadState } from './input';

function pad(pressed: number[] = [], axes: number[] = [0, 0]): PadState {
  return { buttons: Array.from({ length: 16 }, (_, i) => ({ pressed: pressed.includes(i) })), axes };
}

test('maps d-pad, A and B', () => {
  expect([...heldActions(pad([12, 0]))]).toEqual(['up', 'confirm']);
  expect([...heldActions(pad([15, 1]))]).toEqual(['right', 'back']);
});

test('left stick counts only past the threshold', () => {
  expect(heldActions(pad([], [0.3, -0.4])).size).toBe(0);
  expect([...heldActions(pad([], [-0.9, 0.6]))]).toEqual(['left', 'down']);
});

test('a pad with fewer buttons or axes does not throw', () => {
  expect(heldActions({ buttons: [{ pressed: true }], axes: [] })).toEqual(new Set(['confirm']));
});

test('a held action is reported once, not every frame', () => {
  const first = heldActions(pad([14]));
  expect(newlyPressed(new Set(), first)).toEqual(['left']);
  expect(newlyPressed(first, heldActions(pad([14])))).toEqual([]);
  expect(newlyPressed(first, heldActions(pad([14, 0])))).toEqual(['confirm']);
});
