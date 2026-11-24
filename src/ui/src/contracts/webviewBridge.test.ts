import { expect, test } from 'vitest';
import { BridgeError, createWebViewBridge, type WebViewLike } from './webviewBridge';

function fakeWebView() {
  const sent: any[] = [];
  let listener: (event: { data: unknown }) => void = () => {};
  const webview: WebViewLike = {
    postMessage: (message) => sent.push(message),
    addEventListener: (_type, l) => (listener = l),
  };
  return { webview, sent, receive: (data: unknown) => listener({ data }) };
}

test('call resolves with the result matching its id, even out of order', async () => {
  const { webview, sent, receive } = fakeWebView();
  const bridge = createWebViewBridge(webview);

  const list = bridge.call('library.list');
  const launch = bridge.call('library.launch', { gameId: 7 });
  expect(sent).toEqual([
    { id: 1, method: 'library.list', params: undefined },
    { id: 2, method: 'library.launch', params: { gameId: 7 } },
  ]);

  receive({ id: 2 });
  receive({ id: 1, result: [] });
  await expect(launch).resolves.toBeUndefined();
  await expect(list).resolves.toEqual([]);
});

test('call rejects with the host error code', async () => {
  const { webview, receive } = fakeWebView();
  const call = createWebViewBridge(webview).call('library.list');
  receive({ id: 1, error: { code: 'platform_unavailable', message: 'Steam not found' } });
  await expect(call).rejects.toMatchObject({ code: 'platform_unavailable' });
  await expect(call).rejects.toBeInstanceOf(BridgeError);
});

test('events reach handlers until unsubscribed', () => {
  const { webview, receive } = fakeWebView();
  const bridge = createWebViewBridge(webview);
  let count = 0;
  const off = bridge.on('library.changed', () => count++);
  receive({ event: 'library.changed' });
  off();
  receive({ event: 'library.changed' });
  expect(count).toBe(1);
});
