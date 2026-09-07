// SPDX-License-Identifier: AGPL-3.0-only
/** Correlates native requests and replies with timeouts and deterministic disposal. */
/** Bind a transport; each request resolves only against its matching reply type and id. */
export function createRpc(transport, { timeoutMs = 30000, id = () => crypto.randomUUID() } = {}) {
  const pending = new Map();
  const listeners = new Set();
  const receive = event => {
    const message = event.data;
    if (!message || typeof message.type !== 'string' || typeof message.requestId !== 'string') return;
    const waiting = pending.get(message.requestId);
    if (waiting && message.type === waiting.type + '.result' && typeof message.success === 'boolean') {
      console.debug('[Mesh Me360 bridge]', message.requestId, message.type, message.success);
      clearTimeout(waiting.timer); pending.delete(message.requestId);
      if (message.success) waiting.resolve(message.payload);
      else waiting.reject(new Error(message.error?.message ?? 'Host operation failed.'));
      return;
    }
    for (const listener of listeners) listener(message);
  };
  transport.addEventListener('message', receive);
  return {
    /** Register correlation before sending so an immediate reply cannot be lost. */
    request(type, payload = {}) {
      const requestId = id();
      console.debug('[Mesh Me360 bridge]', requestId, type);
      return new Promise((resolve, reject) => {
        const timer = setTimeout(() => { pending.delete(requestId); reject(new Error(`Host request timed out: ${type}`)); }, timeoutMs);
        // Registration precedes postMessage because native transports may reply immediately.
        pending.set(requestId, { type, resolve, reject, timer });
        try { transport.postMessage({ type, requestId, payload, success: null, error: null }); }
        catch (error) { clearTimeout(timer); pending.delete(requestId); reject(error); }
      });
    },
    onMessage(listener) { listeners.add(listener); return () => listeners.delete(listener); },
    /** Detach the transport and reject every outstanding caller. */
    dispose() {
      transport.removeEventListener('message', receive);
      for (const waiting of pending.values()) { clearTimeout(waiting.timer); waiting.reject(new Error('Host bridge closed.')); }
      pending.clear(); listeners.clear();
    }
  };
}
