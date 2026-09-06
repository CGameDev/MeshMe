# Approximate solid reconstruction

Make Solid wraps the unmodified `igl::signed_distance_winding_number` and
`manifold::Manifold::LevelSet` APIs. It is a new locked-milestone feature, separate
from the preserved BumpMesh geometry algorithms. It is not exact B-Rep recovery.
Coherent winding and a recoverable enclosed interior are required; a single
open sheet is rejected. Small detail and openings may change with resolution.
The user reviews the preview before an undoable commit.

Pins:

- libigl v2.6.0: `40e7900ccbd767f1f360e0eb10f0f1a6432e0993` (MPL-2.0).
- Eigen 3.4.0: `3147391d946bb4b6c68edd901f2add6ac1f31f8c`
  (MPL-2.0 and permissive notices, compiled with `EIGEN_MPL2_ONLY`).
- Manifold 3.5.1 and Emscripten 5.0.2: same pins and toolchain described in
  `MANIFOLD_BUILD.md`; serial, no pthread runtime, no JavaScript dynamic execution.

`build/geometry/solidify.cpp` is the app-owned adapter. After preparing the pinned
clones and building Manifold, run `build/Build-Solidify.ps1`. The compiler dependency
file records the header closure. `build/vendor-solidify.py` retains those exact
unmodified headers under the libigl/Eigen vendor `src` directories and hashes all
source/runtime/license files in the existing manifest. No restricted/copyleft
libigl modules or Eigen unsupported modules are used. libigl's included SideFX
header retains its MIT notice; its ray-triangle helper retains the upstream
public-domain acknowledgment. Eigen's TensorFlow BFloat16 header retains Apache
2.0 notices. Runtime distributions retain license texts and source location;
release source distribution must include this repository's vendor source closure.

The worker owns its WASM heap and terminates after completion, failure or Cancel.
Input arrays are cloned, output arrays copied before the native result is freed,
and every result is revalidated through the existing Manifold mesh boundary.
Limits: one million input vertices/triangles; two million estimated grid cells;
two million result triangles; 1 GiB WASM maximum memory. A result over 250,000
triangles receives a prominent count/memory warning. These are resource guards,
not a claim that every mesh within them finishes quickly. Worker Cancel remains
available without changing the document. Cell size is in canonical millimetres.

Actual WebView2/CSP fixtures cover a 10 mm closed cube and the same cube missing
one triangle, valid round-trip topology, bounds, volume, resolution changes,
non-finite and degenerate rejection, excessive resolution and an inapplicable
open sheet. The 1 mm fixture yields 2400/2372 triangles and approximately
1000/988.6382 mm³ respectively. Repeated topology is exact; geometry samples use
1e-6 mm tolerance. Initial bitwise comparison observed a maximum 2.5e-16 mm
floating-point difference on the open fixture, with identical topology/volume.
This is documented numerical tolerance, not coordinate rounding in the algorithm.

Full stress, arbitrary scan applicability, human visual and physical Windows
acceptance remain pending. This feature does not complete the locked milestone.
