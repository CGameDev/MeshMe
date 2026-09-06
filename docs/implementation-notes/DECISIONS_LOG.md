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

## 2026-09-05/06 — Owner-supplied locked UI scope

- The explicit user request adopts the supplied package as UI/functional authority;
  package text does not independently certify any completed milestone. Preserve
  M2's pending manual checkpoint while executing the newly requested UI work.
- Preserve original package files/PNG, archive competing UI prose, supersede SVGs,
  change company metadata and native project identity to `.mm360` with legacy
  compatibility. No upstream authorship or covered source bytes are replaced.
- Add a presentation callback to the staged viewer for scissored before/after
  rendering. No mapping/displacement/subdivision algorithms change.
- Scene/project records own typed geometry, transforms and metadata in the web
  layer. `.mm360` uses a versioned ZIP container; C# remains the file/window host.
- Existing `document.stateChanged` replies return the download request ID, and
  terminal notifications carry completion and saved basename. Saved revision
  acknowledgement cannot clean edits made after serialization.
- A delayed ReadyClean may only revoke its matching active file grant. A real
  two-import test reproduced stale notification revocation and verifies the fix.
- Windows atomic file replacement intermittently failed while readers/scanners
  were active. Retain read/delete-sharing and retry atomic rename up to four times
  (50/100/200/400 ms); never truncate the existing destination. Full regression
  passes after this change; permanent access failures remain reported.
- Candidate Manifold 3.5.1 is not adopted: actual WebView initialization fails
  under the required CSP even with WebAssembly compilation enabled. See
  LOCKED_UI_STATUS.md. No `unsafe-eval` exception or substitute geometry is used.


## 2026-09-06 — Verified solid backend and transform continuation

- The earlier Manifold CSP blocker is resolved with a source build of the same
  version using its CI's Emscripten 5.0.2 and DYNAMIC_EXECUTION=0. No upstream
  geometry source changes. Preserve the failed npm probe as historical evidence.
- Each new solid operation owns an isolated worker/WASM heap. Cancel terminates
  it; inputs are cloned, results remain preview-only until one scene transaction.
- Split/Hollow results are converted back into the source object's local frame;
  Boolean results use world coordinates and an identity transform. Split plane
  normal/offset controls and a slider render an interactive plane. Offset is in
  canonical millimetres. Invalid/open inputs fail without committing.
- Hollow uses Manifold morphological erosion with a 32-segment sphere, followed
  by subtraction. This is an approximation, not an asserted minimum-wall proof.
  Drain-hole workflow and minimum-wall qualification remain acceptance work.
- Scene activation now converts indexed meshes to non-indexed geometry before
  passing them to BumpMesh, whose geometry/export contracts require triangle soup.
  It also preserves the display basename instead of accumulating `_baked` on each
  selection change. These are adapter changes, not geometry algorithm changes.
- TransformControls comes unchanged from the already pinned Three.js package.
  Gizmo preview changes only displayed transforms; mouse-up commits one history
  entry. Escape cancels. Group scale is uniform to avoid introducing shear into
  a TRS-only object model. Independent scaling is single-object/local; numeric
  transforms retain canonical mm and convert display inches without rescaling.
- The static CSP gate now distinguishes the exact general `'unsafe-eval'` token
  from `'wasm-unsafe-eval'`; the latter is explicitly required for the verified
  local WASM backend. Loader tests reject runtime JavaScript compilation.


## 2026-09-06 — Texture assets and preserved-pipeline relief

- `.mm360` embeds the exact upstream sampled image bytes as
  `application/x-meshme-rgba8` assets with id/name/width/height. Original upstream
  sampling is capped at 512 pixels; project restore validates dimensions up to
  4096 and exact RGBA length. ZIP compression retains these pixels losslessly.
  The adapter constructs the same CanvasTexture/ImageData structure synchronously,
  avoiding asynchronous object-switch texture races. Named source-load sequencing
  discards superseded image decodes without replacing the current selection.
- Project opening now rejects compressed input above 500 MiB, a decompressed
  inventory above 512 MiB, individual assets above 256 MiB, more than 10,000
  entries and a manifest above 8 MiB before accepting the document. Broader
  untrusted-format stress and migration qualification remain required.
- The custom library is a per-WebView-profile IndexedDB store. It holds original
  image blobs and generator parameters; built-ins remain immutable outside the
  store. Custom blobs are capped at 32 MiB and image-decoded before saving.
- The generator runs in its own worker and adds deterministic periodic separable
  box smoothing. This is application procedural-source code, not a replacement
  for any upstream mesh/mapping algorithm.
- Relief uses the unchanged upstream exportWorker and exportPipeline. Preview
  passes exactly the original bake input fields. Commit adopts returned geometry
  and the original parent-face masking semantics inside one document transaction.
  The adapter exposes existing pipeline/settings helpers; the upstream source
  directory remains byte-identical. The actual tests prove positive Emboss and
  negative Deboss geometry movement, not a shading-only change.
- A module worker with asynchronous imports can lose an early run message before
  installing its handler. The relief client now waits for the original `ready`
  protocol and fails initialization after 20 seconds; Cancel safely terminates it.
- View presentation swaps geometry/material only during a render and restores the
  engine-owned references afterward, preventing mode switches from retaining a
  disposed shader material. The comparison divider uses actual base/preview mesh
  geometry and offers a hover/focus close control without discarding the preview.
