import type { Bridge } from './api';
import { mockBridge } from './mockBridge';
import { createWebViewBridge, type WebViewLike } from './webviewBridge';

const webview = (window as { chrome?: { webview?: WebViewLike } }).chrome?.webview;

/** Real host when running inside WebView2, mock data in a plain browser. */
export const bridge: Bridge = webview ? createWebViewBridge(webview) : mockBridge;
