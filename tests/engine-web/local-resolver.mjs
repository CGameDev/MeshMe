// SPDX-License-Identifier: AGPL-3.0-only
// Node test-only equivalent of the page import map. No npm install or HTTP server.
export async function resolve(specifier, context, nextResolve) {
  let path;
  if (specifier === 'three') path = 'three-0.170.0/build/three.module.js';
  if (specifier.startsWith('three/addons/')) path = 'three-0.170.0/examples/jsm/' + specifier.slice(13);
  if (specifier === 'fflate') path = 'fflate-0.8.2/esm/browser.js';
  if (path) return { url: new URL('../../artifacts/web/vendor/' + path, import.meta.url).href, shortCircuit: true };
  if (/^https?:/.test(specifier)) throw new Error('Network import attempted: ' + specifier);
  return nextResolve(specifier, context);
}

export async function load(url, context, nextLoad) {
  // fflate's browser ESM is published without a package type. Declare its known
  // module format in the test loader, preserving the vendored package metadata.
  if (url.endsWith('/fflate-0.8.2/esm/browser.js')) return nextLoad(url, { ...context, format: 'module' });
  return nextLoad(url, context);
}
