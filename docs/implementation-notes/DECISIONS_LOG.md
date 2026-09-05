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
- A clean Git-archive rebuild found ignore rules omitting meshStep's required
  `dist/` files and two upstream files (`.claude/settings.local.json` and
  `assets/logo_big.png`). Explicitly included the pinned files without altering
  upstream's own .gitignore. Build verification now also checks Git membership
  when building a checkout, preventing working-directory-only provenance claims.

## 2026-09-05 — M2

- Begin only after the user's explicit M1 offline checkpoint confirmation.
- Implement the documented internal GET origin with a FileStream response,
  256-bit opaque single-use tokens, five-minute expiry, exact app-origin check,
  no-store headers and revocation on document replacement/disposal. Capture
  WebView2 request properties on its UI thread before background file opening.
  Browser Blob/File memory remains necessary for the preserved engine loaders;
  this is low-copy streaming, not a claim of zero total browser allocations.
- Envelope responses use `<type>.result` and the initiating GUID. Reject unknown
  operations; 64 KiB control messages carry metadata only. Web and native logs
  record matching IDs, types and outcomes, without model contents.
- Existing `document.stateChanged` metadata registers `{state:'Exporting',
  downloadUrl, filename}` before the preserved download helper clicks its Blob
  anchor. The host approves at most four pending URLs for five minutes, consumes
  each once, and shows Save Project/Export STL/Export 3MF native dialogs. Only
  completed project saves enter recents. No new message name or endpoint added.
- Chromium's repeated-download permission is allowed only for the exact app
  origin while a host-approved download is pending, and is not persisted.
  DownloadStarting independently checks each exact Blob URL and native decision.
  This fixes a real second-export integration-test failure.
- Preserve upstream algorithms and source bytes. `adapt-host.py` applies
  asserted staging-only loader completion/cancellation hooks, named exports,
  and download delegation. Preserve existing STEP quality and project-content
  dialogs. File chooser/drop stay upstream-owned; opaque browser Files do not
  expose native paths, so only host-approved activations populate native recents.
- Settings use the documented defaults, atomic JSON replacement and extension
  data for unknown keys. Invalid known values fail without overwriting settings.
  JSON read handles allow delete-sharing so another reader cannot block an atomic
  replacement on Windows; integration polling exposed and verified this fix.
  Theme override and the existing language control persist through the host;
  unsupported automatic browser language falls back to upstream English.
- Recents normalize full paths, deduplicate case-insensitively and retain twenty.
  Missing entries remain marked/removable. `recent.add` requires a successfully
  loaded host activation ID; `recent.open` resolves only a saved recent entry.
- Session marker is written before loading the editor. An exclusive session
  lease prevents competing marker writers; previous unclean markers are archived,
  never silently deleted. Final normal exit marks clean off the WPF synchronization
  context to avoid deadlock. Snapshot policy and recovery UI remain M6.
- `update.status` honestly reports NotConfigured; network checks/install remain
  M9. Dirty-close/save-document behavior remains M4, diagnostics UI/rolling logs
  remain M10. No stub is counted as those later milestones passing.
- Desktop builds now default to self-contained .NET 8, including test executables,
  so the installed .NET 10 runtime does not substitute for the specified .NET 8.
  Tests retain isolated artifacts; native modal choices are substituted only in
  the integration executable. Manual dialogs and physical Windows testing remain
  explicit gates in `M2_WINDOWS_HANDOFF.md`.
