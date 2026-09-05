// SPDX-License-Identifier: AGPL-3.0-only
import { register } from 'node:module';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { Worker } from 'node:worker_threads';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';
import test from 'node:test';
register('./local-resolver.mjs', import.meta.url);
const web = new URL('../../artifacts/web/', import.meta.url);
const upstream = new URL('../../src/MeshMe360.EngineWeb/upstream/', import.meta.url);
const THREE = await import(new URL('vendor/three-0.170.0/build/three.module.js', web));
const { STLLoader } = await import(new URL('vendor/three-0.170.0/examples/jsm/loaders/STLLoader.js', web));
const loader = new STLLoader();
const fixture = new URL('../fixtures/cube_binary.stl', import.meta.url);

test('original 20 mm cube STL export/import and zero-amplitude pipeline match pinned upstream', async () => {
  // Original fixture built from simple cube geometry, not an external model.
  const cube = new THREE.BoxGeometry(20, 20, 20).toNonIndexed();
  let downloaded;
  const oldCreate = URL.createObjectURL;
  const oldTimeout = globalThis.setTimeout;
  URL.createObjectURL = blob => { downloaded = blob; return 'blob:test'; };
  globalThis.document = { createElement: () => ({ style: {}, click() {} }), body: { appendChild() {}, removeChild() {} } };
  // Avoid retaining the exporter's ten-second URL cleanup timer in a unit test.
  globalThis.setTimeout = (fn, delay, ...args) => { const timer = oldTimeout(fn, delay, ...args); if (delay === 10000) timer.unref(); return timer; };
  try {
    const exporter = await import(new URL('js/exporter.js', web));
    exporter.exportSTL(cube, 'cube_binary.stl');
    const bytes = Buffer.from(await downloaded.arrayBuffer());
    assert.equal(bytes.readUInt32LE(80), 12);
    assert.equal(bytes.length, 684);
    // Fixture identity is checked; tests never replace it.
    assert.deepEqual(await readFile(fixture), bytes);
    const parsed = loader.parse(bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength));
    parsed.computeBoundingBox();
    assert.deepEqual(parsed.boundingBox.min.toArray(), [-10, -10, -10]);
    assert.deepEqual(parsed.boundingBox.max.toArray(), [10, 10, 10]);
    const settings = { mappingMode: 5, scaleU: 25, scaleV: 25, amplitude: 0,
      textureHeight: 0.5, offsetU: 0, offsetV: 0, rotation: 0, refineLength: 100,
      maxTriangles: 750000, bottomAngleLimit: 0, topAngleLimit: 0,
      mappingBlend: 1, seamBandWidth: 0.5, capAngle: 20, boundaryFalloff: 0,
      blendNormalSmoothing: 0, regularizeEnabled: false, smoothBottom: false };
    const input = () => ({ positions: new Float32Array(parsed.attributes.position.array),
      bounds: { min: {x:-10,y:-10,z:-10}, max:{x:10,y:10,z:10}, center:{x:0,y:0,z:0}, size:{x:20,y:20,z:20} },
      imageData: { data: new Uint8ClampedArray([128,128,128,255]) }, imgWidth: 1, imgHeight: 1, settings });
    const baseline = await import(new URL('js/exportPipeline.js', upstream));
    const adapted = await import(new URL('js/exportPipeline.js', web));
    const expected = await baseline.runExportPipeline(input());
    const actual = await adapted.runExportPipeline(input());
    assert.deepEqual(actual.positions, expected.positions);
    assert.deepEqual(actual.positions, parsed.attributes.position.array);
    assert.ok(actual.positions.every(Number.isFinite));
    assert.equal(actual.positions.length / 9, 12);
    const { buildAdjacency } = await import(new URL('js/exclusion.js', web));
    const { runFastDiagnostics } = await import(new URL('js/meshValidation.js', web));
    const diagnostics = runFastDiagnostics(buildAdjacency(parsed), 12);
    assert.equal(diagnostics.openEdges, 0);
    assert.equal(diagnostics.shellCount, 1);
    const report = { baseline: 'a6ac179149b8a17c71a9469dd4cb6f866c0c01d1',
      fixtureSha256: createHash('sha256').update(bytes).digest('hex'), settings,
      triangles: 12, bounds: { min: [-10,-10,-10], max: [10,10,10] }, invalidVertices: 0,
      diagnostics, exportBytes: bytes.length,
      positionsSha256: createHash('sha256').update(Buffer.from(actual.positions.buffer)).digest('hex') };
    const recorded = JSON.parse(await readFile(new URL('../baselines/bumpmesh-a6ac179/m1-zero-amplitude-cube.json', import.meta.url), 'utf8'));
    assert.deepEqual(report, recorded);
    await mkdir(new URL('../../artifacts/test-results/', import.meta.url), { recursive: true });
    await writeFile(new URL('../../artifacts/test-results/m1-parity.json', import.meta.url), JSON.stringify(report, null, 2) + '\n');
  } finally { URL.createObjectURL = oldCreate; globalThis.setTimeout = oldTimeout; delete globalThis.document; cube.dispose(); }
});

for (const name of ['exportWorker.js', 'stepWorker.js']) {
  test(name + ' boots locally without page import map or network', async () => {
    const worker = new Worker(new URL('./worker-shim.mjs', import.meta.url), { workerData: new URL('js/' + name, web).href });
    try {
      await new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('Worker startup timed out')), 10000);
        worker.on('error', error => { clearTimeout(timer); reject(error); });
        worker.on('message', message => { clearTimeout(timer); assert.equal(message.type, 'ready'); resolve(); });
      });
    } finally { await worker.terminate(); }
  });
}
