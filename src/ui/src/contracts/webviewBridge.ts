import type { Bridge } from './api';

/** The part of window.chrome.webview we use. */
export interface WebViewLike {
  postMessage(message: unknown): void;
  addEventListener(type: 'message', listener: (event: { data: unknown }) => void): void;
}

type Incoming =
  | { id: number; result?: unknown; error?: { code: string; message: string } }
  | { event: string; data?: unknown };

export class BridgeError extends Error {
  constructor(
    public code: string,
    message: string,
  ) {
    super(message);
  }
}

/** JSON-RPC over WebView2 postMessage: requests carry an id, host events carry an event name. */
export function createWebViewBridge(webview: WebViewLike): Bridge {
  let nextId = 1;
  const pending = new Map<number, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
  const handlers = new Map<string, Set<(data: unknown) => void>>();

  webview.addEventListener('message', ({ data }) => {
    const message = data as Incoming;
    if ('event' in message) {
      handlers.get(message.event)?.forEach((handler) => handler(message.data));
      return;
    }
    const request = pending.get(message.id);
    if (!request) return;
    pending.delete(message.id);
    if (message.error) request.reject(new BridgeError(message.error.code, message.error.message));
    else request.resolve(message.result);
  });

  return {
    call: (method: string, params?: unknown) =>
      new Promise<unknown>((resolve, reject) => {
        const id = nextId++;
        pending.set(id, { resolve, reject });
        webview.postMessage({ id, method, params });
      }),
    on: (event: string, handler: (data: never) => void) => {
      const set = handlers.get(event) ?? new Set();
      handlers.set(event, set);
      set.add(handler as (data: unknown) => void);
      return () => set.delete(handler as (data: unknown) => void);
    },
  } as Bridge;
}
