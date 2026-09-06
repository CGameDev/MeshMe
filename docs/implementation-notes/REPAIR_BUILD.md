# Repair pipeline and qualification

Toolbar Repair and Quick Action Repair Mesh share one preview/commit workflow.
`build/geometry/repair.cpp` orchestrates unmodified libigl and PMP calls in an
isolated worker. It does not modify the preserved BumpMesh algorithms.

Pipeline:

1. Filter non-finite vertices and invalid incident triangles, retaining counts.
2. Use libigl `remove_duplicate_vertices` with epsilon zero: exact duplicates
   only, so nearby intentional gaps are not welded. Drop zero-area triangles.
3. Use libigl `bfs_orient` only without non-manifold edges, and accept its result
   only when paired edges have consistent winding. Report ambiguous cases.
4. Remove unreferenced vertices. Use PMP's halfedge mesh and `fill_hole` on a
   separate candidate copy for each eligible loop. Default maximum bounding
   diameter is 1 mm; zero disables filling. At most 64 loop edges and 32 patches
   are processed. Larger openings remain intact and are reported. The user can
   adjust the threshold and inspect the preview before any document change.
5. Reject failed, non-finite, degenerate or zero-enclosed-volume closing patches.
   A failed patch does not replace the preceding valid mesh. For one closed
   connected patch, libigl's enclosed-volume integral identifies globally
   reversed winding. Multiple components retain their orientation, preserving
   possible nested cavities. Self-intersections remain explicitly unverified.
6. Attempt the existing Manifold validation boundary. Open or non-manifold
   cleanup results are allowed, with unavailable volume and an explicit status;
   the report never equates consistent winding with guaranteed printability.

Before/after edge and triangle counts, cleanup counts, hole results and unresolved
issues are shown before Commit and retained in the baked modifier in `.mm360`.
One Commit is one undoable transaction. Worker failure/Cancel keeps the document.

PMP source is pinned to `215975ca1efafab97477b18187a4a23b87529036`, from
https://github.com/pmp-library/pmp-library, under plain MIT. This is a specific
source revision, not the 3.0.0 release: the older release's additional employer
terms were not adopted. The development checkout directory retains its historical
`.tools/pmp-3.0.0` name; the build checks the full revision. PMP requires C++20.
Exact compiled PMP source files are retained at `vendor/pmp-215975c/src`, with its
full unmodified license. libigl/Eigen/Emscripten pins match `SOLIDIFY_BUILD.md`.
`build/Build-Repair.ps1` rebuilds; `build/vendor-repair.py` records each compiler
dependency closure and runtime hash. Release source archives must include those
unmodified sources and the app-owned adapter. No runtime compiler/network needed.

Actual WebView2 fixtures cover invalid data, degenerate triangles, duplicate
vertices, reversed winding, preserved cube volume/bounds, small-hole closure,
large-opening retention, zero-volume closure rejection, nested cavity retention,
non-manifold reporting and repeated output. Actual controls exercise both entry
points and preview/Cancel/Commit/Undo. Arbitrary scans, self-intersection safety,
large workloads and physical/human acceptance still need qualification. This
implementation does not complete the locked milestone.
