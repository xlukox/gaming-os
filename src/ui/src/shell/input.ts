export type Action = 'up' | 'down' | 'left' | 'right' | 'confirm' | 'back';

/** The part of a Gamepad we read. */
export interface PadState {
  buttons: readonly { pressed: boolean }[];
  axes: readonly number[];
}

// Standard gamepad mapping: 0 = A, 1 = B, 12-15 = d-pad.
const BUTTONS: [number, Action][] = [
  [12, 'up'],
  [13, 'down'],
  [14, 'left'],
  [15, 'right'],
  [0, 'confirm'],
  [1, 'back'],
];
const STICK_THRESHOLD = 0.5;

/** Actions currently held on the pad (d-pad, left stick, A, B). */
export function heldActions(pad: PadState): Set<Action> {
  const held = new Set<Action>();
  for (const [index, action] of BUTTONS) if (pad.buttons[index]?.pressed) held.add(action);
  const [x = 0, y = 0] = pad.axes;
  if (x <= -STICK_THRESHOLD) held.add('left');
  if (x >= STICK_THRESHOLD) held.add('right');
  if (y <= -STICK_THRESHOLD) held.add('up');
  if (y >= STICK_THRESHOLD) held.add('down');
  return held;
}

/** Actions that were not held on the previous frame. */
export function newlyPressed(previous: Set<Action>, current: Set<Action>): Action[] {
  return [...current].filter((action) => !previous.has(action));
}

/**
 * Polls connected gamepads every frame and reports each new press once.
 * shortcut: holding a direction does not auto-repeat, add repeat when long lists make it tedious.
 */
export function startGamepad(onAction: (action: Action) => void): () => void {
  let previous = new Set<Action>();
  let frame = requestAnimationFrame(function poll() {
    const current = new Set<Action>();
    for (const pad of navigator.getGamepads()) if (pad) heldActions(pad).forEach((a) => current.add(a));
    newlyPressed(previous, current).forEach(onAction);
    previous = current;
    frame = requestAnimationFrame(poll);
  });
  return () => cancelAnimationFrame(frame);
}
