# M1 fixture

`cube_binary.stl` is original simple 20 mm cube geometry generated for Mesh Me360
using Three.js BoxGeometry and the unchanged BumpMesh binary STL exporter.
12 outward-facing triangles, bounds [-10,-10,-10] to [10,10,10] mm.
Licensed AGPL-3.0-only with this repository. No commercial model assets are used.

The M1 Node smoke test checks its bytes, finite geometry, diagnostics, STL parse
validity and a zero-amplitude pipeline against the pinned upstream implementation.
Additional fixtures in TEST_PLAN remain required at their later acceptance gates.
