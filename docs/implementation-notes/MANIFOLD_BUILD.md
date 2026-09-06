# Pinned CSP-compatible Manifold build

The npm 3.5.1 loader failed actual WebView2 initialization because Embind used
`new Function`. The replacement is a source build, not a loader text patch.
Geometry code is unchanged. The narrowly scoped `wasm-unsafe-eval` CSP permission
allows WebAssembly compilation; general JavaScript `unsafe-eval` remains absent.

Development prerequisites (download into ignored `.tools`, never end-user setup):

```powershell
git clone --depth 1 --branch v3.5.1 https://github.com/elalish/manifold.git .tools/manifold-source-3.5.1
git clone --depth 1 --branch 5.0.2 https://github.com/emscripten-core/emsdk.git .tools/emsdk
python .tools/emsdk/emsdk.py install 5.0.2
python .tools/emsdk/emsdk.py activate 5.0.2
python -m pip install --target .tools/build-python cmake==3.31.6 ninja==1.11.1.4
./build/Build-Manifold.ps1
```

The script checks source and SDK commits. Manifold CMake pins Clipper2 to
`46f639177fe418f9689e8ddb74f08a870c71f5b4` and carries its own no-iostream patch.
No Python bindings, C bindings, TBB, test-only GTest, or experimental boolean2
backend is included. Upstream geometry tests are not claimed to have run;
application WebView2 regression fixtures are a separate gate.

The generated loader must contain no `new Function`, `eval(` or `node:module`.
Copy both generated files and notices only after passing the actual WebView2
probe. Recalculate the vendor manifest hashes for an intentional rebuild and
review them; ordinary application builds verify the recorded hashes offline.

Initial actual WebView2 evidence:
`artifacts/m2-integration-c59c41b51c5347c19bcf12875c484cb1/backend-probe.txt`.
Two overlapping 10 mm cubes give union 1500, subtract/intersect 500 mm³;
plane split gives two 500 mm³ halves; 1 mm inward hollow gives 488 mm³.
Output mesh round trips pass Manifold validation. Open input and excessive wall
thickness fail without changing the input arrays. Broader stress/mesh fixtures,
interactive controls and minimum-wall qualification remain required.
