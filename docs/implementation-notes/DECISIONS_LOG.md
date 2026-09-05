# Implementation decisions

## 2026-09-05 — M0

- Imported the entire pinned upstream tree so source, benchmark scripts, textures,
  notices and provenance remain inspectable. Deployment will select runtime assets
  only; upstream Docker/server instructions do not apply to the desktop application.
- Preserve upstream bytes with Git attributes and a recorded blob inventory.
- Create only Desktop and Core .NET projects now. Architecture section 2 makes a
  separate Updater project conditional; no empty second executable is needed.
- Reserve the specified test/app/vendor directories; add actual tests with their
  milestone behavior, rather than placeholder passing unit tests.
- The host has SDK 10.0.400; the project still targets .NET 8 and Windows x64.
  Build targeting packs are development dependencies, not an end-user requirement.

## 2026-09-05 — M1

- Inspecting the pinned page and `threeCompat.js`/`stepWorker.js` identified three
  dependencies: Three.js 0.170.0, fflate 0.8.2, meshstep 0.1.0. The latter is
  AGPL-3.0-only and pure JS. Use its original npm ESM tree instead of the CDN
  rebundle, retaining its TypeScript corresponding source in Git.
- Build-only `prepare-web.py` transforms three upstream files: index.html
  (local map/CSP/bootstrap/title/metadata), threeCompat.js (local worker fallback),
  stepWorker.js (local STEP import). Mesh algorithm bytes remain unchanged.
- CSP allows the exact import-map SHA-256, self-hosted scripts and upstream inline
  styles. No script unsafe-inline/unsafe-eval, CDN, wildcard or external connect
  permission is used. M2's bridge origin will be added only with that milestone.
- The M1 adapter uses upstream's existing `stlt-no-sponsor` preference and hides
  the upstream promotional banner; original attribution and legal notices remain.
  M3 will replace the layout after the M1 checkpoint. No geometry settings changed.
- M1 lifecycle messages use documented `web.ready`/`engine.error` types, with
  source checking and correlation IDs. This is startup reporting, not the M2
  operational bridge. A 60-second engine readiness timeout produces a visible
  fatal screen; it does not time-limit model loading or mesh processing.
- WebView2 uses DenyCors virtual-host mapping and its own LocalAppData profile.
  Explicit user-clicked HTTPS links open through the OS browser. Embedded
  navigation cannot leave the application origin. No local listener is created.
- M1 can use an installed WebView2 runtime, with an existing fixed-runtime folder
  preferred. M8 retains responsibility for a pinned offline runtime in releases.
- SDK 10.0.400 built the .NET 8 targets; a self-contained 8.0.30 Debug handoff
  avoids requiring .NET 8 installation. This is not an M8 installer or release.
- Node tests use a local import-map resolver and a worker `self` shim. These verify
  module resolution and deterministic engine execution, not Chromium/WebGL behavior.
- Publish verification caught MSBuild metadata referring to existing Content
  items. A dedicated `_OfflineAsset` item now supplies qualified target paths.
