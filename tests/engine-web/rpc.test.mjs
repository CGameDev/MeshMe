// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import { createRpc } from '../../src/MeshMe360.EngineWeb/app/rpc.js';
function transport() {
  const listeners = new Set();
  return { sent: [], addEventListener(_, callback) { listeners.add(callback); }, removeEventListener(_, callback) { listeners.delete(callback); },
    postMessage(message) { this.sent.push(message); }, receive(message) { for (const listener of listeners) listener({data:message}); } };
}
test('RPC matches type and id, and supports out-of-order replies', async () => {
  const channel = transport(); const rpc = createRpc(channel);
  const a = rpc.request('settings.get'); const b = rpc.request('recent.list');
  channel.receive({type:'recent.list.result',requestId:channel.sent[1].requestId,success:true,payload:['recent']});
  channel.receive({type:'wrong.result',requestId:channel.sent[0].requestId,success:true,payload:'wrong'});
  channel.receive({type:'settings.get.result',requestId:channel.sent[0].requestId,success:true,payload:{theme:'System'}});
  assert.deepEqual(await a,{theme:'System'}); assert.deepEqual(await b,['recent']); rpc.dispose();
});
test('RPC propagates host rejection and times out unanswered operations', async () => {
  const channel = transport(); const rpc = createRpc(channel,{timeoutMs:20});
  const rejected = rpc.request('unknown');
  channel.receive({type:'unknown.result',requestId:channel.sent[0].requestId,success:false,error:{message:'Unsupported'}});
  await assert.rejects(rejected,/Unsupported/);
  await assert.rejects(rpc.request('settings.get'),/timed out/); rpc.dispose();
});
test('RPC disposal rejects pending calls and detaches the transport listener', async () => {
  const channel = transport(); const rpc = createRpc(channel); const request = rpc.request('settings.get');
  rpc.dispose(); await assert.rejects(request,/closed/);
});
