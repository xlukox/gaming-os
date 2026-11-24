// Contract between the UI and the host.
// Hand-written until the host exists; then this file is generated from the C# interfaces in Core
// (docs/PLAN.md 1.4) and must not be edited by hand.

export type Platform = 'steam' | 'epic' | 'gog' | 'manual';

export interface GameDto {
  id: number;
  title: string;
  platform: Platform;
  installed: boolean;
  playtimeMinutes: number | null; // null = the platform does not provide it and we have not tracked any yet
  accentColor: string;
  // Artwork addresses the UI can load directly; null when the platform has none on disk.
  coverUrl: string | null; // portrait cover, 2:3
  heroUrl: string | null; // wide background image
  logoUrl: string | null; // game logo with transparent background
}

export interface Api {
  'library.list': { params: void; result: GameDto[] };
  'library.launch': { params: { gameId: number }; result: void };
}

export interface Events {
  'library.changed': void;
}

type Args<M extends keyof Api> = Api[M]['params'] extends void ? [] : [Api[M]['params']];

export interface Bridge {
  call<M extends keyof Api>(method: M, ...args: Args<M>): Promise<Api[M]['result']>;
  /** Returns a function that removes the handler. */
  on<E extends keyof Events>(event: E, handler: (data: Events[E]) => void): () => void;
}
