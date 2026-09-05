// SPDX-License-Identifier: AGPL-3.0-only
import { parentPort, workerData } from 'node:worker_threads';
// Deliberately no import-map shim: exercise the actual worker fallback resolution.
globalThis.self = { postMessage: message => parentPort.postMessage(message) };
await import(workerData);
parentPort.on('message', data => self.onmessage({ data }));
