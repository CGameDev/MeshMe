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

**Not run:** actual WPF/WebView2 interaction, GPU/WebGL, native file selection,
preview, exported download, STEP quality controls in WebView2, DevTools review,
or launch with the network adapter disabled. Node worker tests do not prove these.

The current host reports Windows build 19044, below the specified Windows 10 22H2
target (19045); use a supported Windows 10 22H2/Windows 11 test machine for acceptance.
No Windows network adapters, installed applications, or file associations were changed.

Follow `M1_WINDOWS_HANDOFF.md` and record evidence before marking M1 PASS.
M2–M11 remain NOT STARTED. No milestone was skipped and no v1.0 feature is marked
PASS merely because its source was imported or the program compiled.

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
- Network adapters were not disabled. Offline import/preview/export/console
  acceptance remains unverified, so M1 is not marked PASS and M2 stays gated.

## M2 — IN PROGRESS

Started after the user's M1 confirmation. M3–M11 remain NOT STARTED.
