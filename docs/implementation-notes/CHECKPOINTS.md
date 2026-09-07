# Issue #1 checkpoint evidence

## M0 — PASS (2026-09-05)

- Exact `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1` tree imported.
- `python build/verify-upstream.py`: PASS, all 117 Git blob identities,
  exact file inventory and complete root AGPL license verified.
- `git check-attr text`: upstream source and LICENSE have text conversion disabled.
- Provenance, attribution, dependencies and traceability recorded in
  `UPSTREAM_BASELINE.md`, `build/upstream-tree.txt`, and `THIRD_PARTY_NOTICES.md`.
- `dotnet build MeshMe360.sln -c Debug`: PASS, 0 warnings, 0 errors;
  .NET 8 WPF executable and Core library built on Windows x64 using SDK 10.0.400.
- No geometry modifications; no algorithm regression gate is triggered by the
  byte-identical import. M0 introduces no host service behavior requiring unit tests.
- Initial archive inherited Windows Git CRLF conversion. Re-extracted with
  `git -c core.autocrlf=false archive`; the identity check now passes.

## M1 — PASS (user-confirmed offline checkpoint, 2026-09-05)

The user confirmed "yes the test passed" in response to the explicit checkpoint
question covering networking disabled, model loading, viewport movement, texture
preview, STL export and no console errors. Combined with the automated evidence
below, this satisfies the M1 master-plan gate. This is user-reported UI acceptance;
do not interpret it as agent-observed screenshots, full STEP parity, or M8 release
qualification. Earlier entries below retain the history before that confirmation.

Implemented the WPF/WebView2 host, virtual-host mapping, offline page and worker
dependencies, restricted CSP, startup/error logging, and fatal-startup screen.
The upstream UI remains the M1 target; the M3 redesign has not begun.

Automated evidence from `./build/Test-M1.ps1 -Publish`:

- Upstream provenance: PASS (117 exact blobs and complete AGPL license).
- Vendored dependency inventory/SHA-256 and npm archive SHA-512 checks: PASS.
- Python asset checks: 5/5 PASS (CSP/import map, module closure, both worker paths,
  all 24 textures/24 thumbnails, 14 language modules, unchanged algorithm bytes,
  no packaged source maps/TypeScript/server scripts).
- Node engine checks: 3/3 PASS. Both actual worker entry modules load using their
  local dependencies without a page import map. Deterministic 20 mm cube export/
  parse and zero-amplitude pipeline match the pinned baseline: 12 triangles,
  bounds [-10,-10,-10] to [10,10,10], 0 invalid vertices, 0 open edges,
  0 non-manifold edges, 1 shell. Saved manifest:
  `tests/baselines/bumpmesh-a6ac179/m1-zero-amplitude-cube.json`.
- .NET Debug build: PASS, 0 warnings, 0 errors (.NET 8 targets, SDK 10.0.400).
- Self-contained Windows x64 Debug publish: PASS (.NET/WPF runtime 8.0.30).
- Published-content check: PASS, all 141 web files match staging byte-for-byte;
  .NET/WPF/WebView2 SDK/native loader and license/notice files are present.
- Fresh committed-source archive: PASS for provenance, web preparation, all
  5 asset tests and all 3 Node tests. This also verifies that ignored local
  dependency caches are not needed to reproduce the web build.
- Test output: `artifacts/m1-windows/MeshMe360.exe` (entire containing directory
  required). This is a development handoff, not an installer or release artifact.

The agent did not directly observe M1's physical/offline interaction; the user
confirmed it as described above. Full STEP parity and release-platform eligibility
remain separate qualification work. No network adapters or associations were changed.

### Launch verification — 2026-09-05 23:01 UTC

- Started the prepared executable at the user's request.
- Startup log confirms .NET 8.0.30, installed WebView2 152.0.4191.62, and a
  correlated `web.ready` event for the pinned upstream baseline. No startup
  error was recorded in that session's log.
- Actual OS identification is Windows 10 IoT Enterprise LTSC 2021, 21H2,
  build 19044.7663. Supported-servicing-equivalent eligibility still needs
  assessment; the build number alone does not establish release qualification.
- Computer Use initialization and window listing succeeded, but the app was
  not listed as a targetable window. Its explicit launch request timed out
  waiting for Computer Use app approval. No viewport/file/export UI acceptance
  is inferred from `web.ready`.
- At that time, the manual gate was still pending. The subsequent user confirmation
  recorded above satisfied M1 and allowed M2 to start.

## M2 — AUTOMATED PASS; MANUAL CHECKPOINT PENDING

Started after the user's M1 confirmation. `build/Test-M2.ps1 -Publish` passes:

- .NET 8 self-contained Windows x64 build: zero warnings/errors.
- 36 Core checks: envelope/startup paths, scoped single-use tokens, expiry,
  invalidation, 128 MB byte-exact bounded-memory stream, settings defaults and
  persistence, recents cap/dedup/missing entries, session archives, save metadata.
- 5 Python asset checks and 6 Node engine/RPC checks, including unchanged pinned
  cube geometry/diagnostics and both local worker entry points.
- Actual WPF/WebView2 integration: startup, binary GET/native file activation,
  token rejection, settings correlation, repeated STL/3MF downloads, preserved
  project ZIP serialization and successful-save recents, cancellation preserving
  bytes, and language control persistence. Modal save decisions are substituted;
  synthetic STL/3MF payloads test transport, not complete export geometry parity.
- Self-contained publish and exact staged/published web inventory checks pass.
  Executable: `artifacts/m2-windows/MeshMe360.exe`.
- No upstream source or mesh algorithms rewritten: all 117 blobs still match.

The first sandboxed WebView test could not initialize. Running in the normal
Windows environment succeeds. Expanded tests exposed Chromium's second-download
block; scoped permission handling corrected it and the full suite passes.

Follow `M2_WINDOWS_HANDOFF.md` for physical/native-dialog/startup acceptance.
M2 is not marked complete. M3–M11 remain NOT STARTED.

### M2 launch evidence — 2026-09-05 23:56 UTC

Launched the published executable with `cube_binary.stl` as its quoted command-line
argument. Production logs confirm .NET 8.0.30, WebView2 152.0.4191.62, `web.ready`,
matching file-issued/file-consumed ID `2d97ef21-ec02-4954-89c9-918c5ab5f3e4`, and
successful recent/title/ReadyClean acknowledgements. The app remains open for
manual testing. This verifies the startup path through the actual executable;
visible geometry and native modal interactions still require user acceptance.

## M3 — Locked UI scope adopted; INCOMPLETE

The owner explicitly supplied and requested the locked PNG milestone, superseding
older UI/single-model scope. See LOCKED_UI_STATUS.md for actual phase status,
remaining controls and resolution of the candidate-backend CSP failure. M2 manual
acceptance is still pending; M4–M11 are not implicitly completed.

Current build and M2 regression suite pass (36 Core, 5 asset, 6 Node, actual WebView
integration). Five additional locked-core tests pass. Actual multi-object import,
native `.mm360` save, dirty acknowledgement and token-based reopen pass.

Capture `artifacts/m2-integration-77a9c7340a704a1ba45608607012dfc8/locked-layout.json`
passes measured region/toolbar positions. `locked-gate.json` deliberately reports
NOT COMPLETE: pixel appearance, human review and full controls are pending.
No skipped control or numeric-only transform is counted as full acceptance.

### Locked UI continuation — backend and scene tools

Manifold source build with dynamic JavaScript execution disabled is adopted and
passes real WebView2 deterministic fixtures. Boolean/Split/Hollow tool workflows
pass initial preview/commit/undo tests; worker cancel/restart preserves originals.
Three.js Move gizmo passes an actual mouse-drag/undo test. Material state has
validated transactions and round-trip tests. `build/Test-Locked.ps1` repeats these
checks and produces a fresh visual capture without claiming milestone acceptance.
Full functional, visual, stress, drain/minimum-wall and physical gates remain open.

### Locked UI continuation — Make Solid

The offline libigl/Manifold reconstruction adapter passes geometry fixtures and
actual Make Solid preview/cancel/commit/undo controls. The entire current
`build/Test-Locked.ps1` regression set passes. Fresh structural layout check passes
at `artifacts/m2-integration-dde6513c630645bcb8490b1362c891e8`.
M3 remains NOT COMPLETE: seven named commands, broader subrequirements, pixel
appearance, human visual review, stress and physical Windows gates are open.

### Locked UI continuation — Repair

Repair/Repair Mesh share the tested libigl/PMP worker backend. Current complete
regression suite passes, including cavity/opening retention and both actual
preview/cancel/commit/undo entry points. Evidence is recorded in LOCKED_UI_STATUS.
M3 remains NOT COMPLETE; five entirely missing commands and broader functional,
visual, stress, self-intersection and physical/human gates remain.

### Locked UI continuation — normal exports

Shared native STL/3MF/OBJ worker workflow passes actual geometry, package,
live-displacement and cancellation tests. The full current regression suite
passes. Pattern, Measure and Prepare for Print remain unwired; broad control,
visual, stress and physical/human acceptance remain open. M3 is NOT COMPLETE.
