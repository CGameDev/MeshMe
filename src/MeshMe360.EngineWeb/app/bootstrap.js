// Copyright (c) 2026 CGameDev / Brian Hinds
// SPDX-License-Identifier: AGPL-3.0-only
import { attachHost } from './host-bridge.js';
import { mountLockedUi } from './locked-ui.js';
const host = window.chrome?.webview ? attachHost(window.chrome.webview) : null;
window.meshMe360 = host;
function report(type, payload) {
  window.chrome?.webview?.postMessage({ type, requestId: crypto.randomUUID(), payload });
}
function failure(error, fatal = false) {
  report('engine.error', { message: String(error?.message ?? error), stack: error?.stack ?? '', fatal });
}
window.addEventListener('error', event => failure(event.error ?? event.message));
window.addEventListener('unhandledrejection', event => failure(event.reason));
window.addEventListener('securitypolicyviolation', event => {
  failure(new Error(`CSP blocked ${event.violatedDirective}: ${event.blockedURI}`));
});
try {
  // Upstream explicitly provides this session preference; suppress promotional
  // export overlays in the desktop workspace without changing the export path.
  sessionStorage.setItem('stlt-no-sponsor', '1');
  const engine = await import('../js/main.js');
  const canvas = document.querySelector('canvas');
  if (!canvas) throw new Error('The engine did not create its viewport.');
  mountLockedUi(host, engine);
  if (host) await host.start(engine);
} catch (error) {
  failure(error, true);
}
