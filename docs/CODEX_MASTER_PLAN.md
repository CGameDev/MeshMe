# Mesh Me360 — Codex Master Plan

This document defines the implementation order. Codex must complete milestones sequentially and satisfy each checkpoint before beginning the next major milestone.

## Global execution policy

- Read `AGENTS.md` first.
- Do not reinterpret architecture or scope.
- Do not remove upstream capability to simplify the desktop conversion.
- Keep the repository buildable at every milestone boundary.
- Prefer preserving upstream code first, then layering desktop behavior around it.
- Do not begin broad visual redesign until the pinned upstream engine runs locally inside WebView2.

---

## Milestone 0 — Repository and provenance baseline

### Goal
Establish a reproducible, correctly licensed base before feature work.

### Tasks
1. Add the complete GNU AGPL v3 license text to `LICENSE`.
2. Create `THIRD_PARTY_NOTICES.md` from the requirements in `docs/UPSTREAM_AND_LICENSING.md`.
3. Import the exact upstream baseline commit:
   - repo: `CNCKitchen/stlTexturizer`
   - SHA: `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1`
4. Place the imported upstream web source under `src/MeshMe360.EngineWeb/upstream/`.
5. Preserve upstream license/copyright/SPDX notices.
6. Add `src/MeshMe360.EngineWeb/UPSTREAM_BASELINE.md` containing source URL, SHA, import date, and a statement that modifications must remain traceable.
7. Create the .NET solution and source tree defined in `AGENTS.md`.

### Checkpoint
- Upstream source is present at the exact pinned revision.
- Provenance is documented.
- License/notice files are present.
- Solution opens/builds even if the desktop shell is still minimal.

---

## Milestone 1 — WPF shell + offline WebView2 boot

### Goal
Launch the unmodified/preserved BumpMesh app inside Mesh Me360 without a network connection.

### Tasks
1. Create `MeshMe360.sln`.
2. Create .NET 8 WPF executable `MeshMe360.Desktop` targeting `win-x64`.
3. Add WebView2.
4. Package the upstream engine as application content.
5. Remove CDN runtime dependency by vendoring/pinning required JavaScript modules locally.
6. Use WebView2 virtual-host folder mapping, not a local HTTP server:
   - host name: `app.meshme360.local`
   - map to packaged engine/application web root
   - navigate to `https://app.meshme360.local/index.html`
7. Confirm ES modules, textures, workers, STEP support, and WebGL all load from local packaged assets.
8. Add a strict Content Security Policy that permits only the resources the app actually uses.
9. Add startup logging and a visible fatal-startup error screen if WebView2/engine initialization fails.

### Checkpoint
With networking disabled:
- app launches from Visual Studio/build output;
- upstream UI renders;
- a model can be loaded;
- viewport works;
- one built-in texture can preview;
- STL export completes;
- DevTools console contains no CDN/network dependency errors.

---

## Milestone 2 — Desktop host bridge and Windows services

### Goal
Create a narrow, documented bridge between WPF and the web app.

### Tasks
1. Add `IHostBridge`/message envelope contracts.
2. Use JSON messages with:
   - `type`
   - `requestId`
   - `payload`
   - `success`
   - `error`
3. Implement only named message types documented in `docs/ARCHITECTURE.md`.
4. Add host services for:
   - application settings
   - theme/OS information
   - recent-file list
   - startup file path
   - logging
   - update status
   - session/crash marker
5. Implement efficient host-to-web file transfer for Explorer/recent-file launch using WebView2 shared buffers or another documented zero/low-copy WebView2 mechanism. Do not base64 multi-hundred-MB meshes.
6. Intercept WebView2 downloads for Save Project / Export STL / Export 3MF so destination paths are controlled by native Windows save dialogs.
7. Preserve existing drag-and-drop and web `<input type=file>` workflows.

### Checkpoint
- launching `MeshMe360.exe "C:\path\model.stl"` opens the model;
- saving/exporting uses a Windows save dialog;
- native host and web logs show request correlation IDs;
- large-file transfer does not duplicate data through base64 JSON.

---

## Milestone 3 — Fusion 360-inspired Mesh Me360 UI shell

### Goal
Replace the upstream page layout with the Mesh Me360 desktop workspace while preserving engine behavior.

### Required references
- `docs/UI_SPEC.md`
- `docs/ui/MeshMe360_Main_Workspace.svg`
- `docs/ui/MeshMe360_Texture_Workspace.svg`
- `docs/ui/MeshMe360_Recovery_Dialog.svg`

### Tasks
1. Implement the defined top menu/application row.
2. Implement workspace tabs:
   - DESIGN
   - TEXTURE
   - MESH
   - INSPECT
3. Implement top command groups and command enable/disable states.
4. Implement left Browser tree.
5. Keep the central Three.js viewport dominant.
6. Implement view cube and view/navigation controls without copying Autodesk artwork.
7. Implement right context-sensitive Properties/Tool panel.
8. Implement bottom status/history strip.
9. Wire all existing upstream controls into the new locations; do not leave hidden duplicate controls as the permanent implementation.
10. Preserve keyboard navigation, focus states, tooltips, and accessibility labels.
11. Implement light/dark themes and OS-theme default.

### Checkpoint
- mockup layout is recognizable at 1600x900;
- usable at 1366x768 without blocking the viewport;
- high-DPI scaling at 125%, 150%, and 200% does not clip essential controls;
- every upstream control has an intentional new location;
- no upstream feature is lost because of UI redesign.

---

## Milestone 4 — Windows document workflow

### Goal
Make Mesh Me360 behave like a normal Windows creation tool.

### Tasks
1. Implement File menu:
   - New
   - Open Model…
   - Open Project…
   - Save Project
   - Save Project As…
   - Export STL…
   - Export 3MF…
   - Recent Files
   - Exit
2. Implement Edit menu:
   - Undo
   - Redo
   - Preferences
3. Implement View menu and Help menu per UI spec.
4. Implement recent files/projects with exact retention rules.
5. Add dirty-document state and title-bar marker.
6. Add safe-close prompt for unsaved changes.
7. Keep `.bumpmesh` compatibility with upstream projects.

### Checkpoint
All common open/save/reopen/close workflows work from UI, drag-and-drop, recent list, and command-line/Explorer activation.

---

## Milestone 5 — Texture Library Manager

### Goal
Turn custom textures into a persistent reusable library.

### Tasks
1. Preserve all upstream built-in textures.
2. Add built-in/custom sections.
3. Store user library under `%APPDATA%\MeshMe360\Textures\`.
4. Add metadata index and thumbnails.
5. Implement:
   - Import Texture…
   - rename custom texture
   - tags
   - search/filter
   - favorite/unfavorite
   - delete custom texture with confirmation
   - reveal file/location where appropriate
6. Never allow deletion/modification of packaged built-in textures through the UI.
7. Validate imported image format using actual upstream-supported formats; do not invent decoder support.
8. If a custom texture is embedded in a `.bumpmesh` project, allow adding a copy to the library without changing the project.

### Checkpoint
A texture imported in one session remains available after restart and can be used offline in another project.

---

## Milestone 6 — Autosave and crash recovery

### Goal
Protect user work without corrupting normal project files.

### Tasks
1. Implement dirty-state-driven autosave.
2. Serialize recovery data using the existing `.bumpmesh` project representation or a versioned recovery container that embeds equivalent project state.
3. Follow exact timing/retention rules in `docs/PRODUCT_REQUIREMENTS.md`.
4. Keep recovery data separate from the user's explicitly saved file.
5. Create session marker before document work begins.
6. Mark session clean only on normal shutdown.
7. On next start after an unclean session, display the specified recovery dialog.
8. Allow:
   - Recover
   - Open without recovering
   - Delete selected recovery
   - Reveal details/timestamp
9. Recovery failures must not prevent the app from opening.

### Checkpoint
Terminate the process during an edited unsaved project, relaunch, and successfully recover the latest valid snapshot.

---

## Milestone 7 — File associations and Explorer integration

### Goal
Integrate with Windows without hijacking user defaults.

### Tasks
1. Register `.bumpmesh` using ProgID `MeshMe360.Project`.
2. Register an icon and open command for `.bumpmesh`.
3. Register `Open with Mesh Me360` shell verbs for:
   - `.stl`
   - `.obj`
   - `.3mf`
   - `.step`
   - `.stp`
4. Do not force Mesh Me360 as the default handler for common mesh/CAD formats.
5. Support quoted paths and paths containing Unicode/spaces.
6. Uninstall must clean only entries created by Mesh Me360.

### Checkpoint
Double-clicking a `.bumpmesh` file launches/reuses Mesh Me360 correctly; common model types show Mesh Me360 as an available Open With choice.

---

## Milestone 8 — Installer + fully offline runtime

### Goal
Produce a distributable Windows installer that needs no runtime downloads at first launch.

### Tasks
1. Implement the packaging strategy in `docs/RELEASE_AND_INSTALLER.md`.
2. Bundle/pin a compatible WebView2 runtime strategy in the release package.
3. Build x64 installer and portable/test package as specified.
4. Install under per-user application location unless release documentation explicitly changes this.
5. Add Start menu shortcut and uninstall entry.
6. Include license/source links and notices.
7. Confirm no development runtime is required.

### Checkpoint
On a clean Windows test VM with networking disabled, install and use core Mesh Me360 functions successfully.

---

## Milestone 9 — Automatic updates

### Goal
Provide safe, non-blocking release updates.

### Tasks
1. Use the update framework selected in `docs/RELEASE_AND_INSTALLER.md`.
2. Stable update source: GitHub Releases for `CGameDev/MeshMe`.
3. Check no more than once per 24 hours by default.
4. Add Preferences toggle: `Automatically check for updates` (default on).
5. Add Help > Check for Updates.
6. Never block startup when offline or GitHub is unavailable.
7. Show version, release notes link, Download/Install action, and Later action.
8. Validate package integrity/signature mechanisms supported by the selected updater.
9. Support a test feed/channel for development without pointing dev builds at production users.

### Checkpoint
A test version detects a newer test release, downloads/applies it, relaunches, and preserves settings/recent files/library/recovery data.

---

## Milestone 10 — Reliability, diagnostics, accessibility, polish

### Goal
Reach desktop-quality v1.0 behavior.

### Tasks
1. Structured rolling logs.
2. Help > Diagnostics screen showing:
   - Mesh Me360 version
   - .NET runtime
   - WebView2 version
   - GPU/WebGL renderer string
   - current model triangle count/dimensions
   - last engine error
   - log folder shortcut
3. Harden exception handling in host and web engine.
4. Preserve upstream 10M-triangle safety cap unless upstream baseline says otherwise.
5. Verify keyboard shortcuts.
6. Verify screen scaling/multi-monitor behavior.
7. Verify theme persistence and OS-theme behavior.
8. Verify all translations still function after UI relocation.
9. Add source-code/license entry to About.

### Checkpoint
No P0/P1 bugs remain; diagnostics can capture actionable information without exposing user model contents by default.

---

## Milestone 11 — Regression and release qualification

### Goal
Prove that desktop conversion did not break BumpMesh functionality.

### Tasks
1. Execute `docs/TEST_PLAN.md` in full.
2. Test representative STL, OBJ, 3MF, STEP/STP fixtures.
3. Test each projection mode.
4. Test built-in and custom textures.
5. Test masks, subdivision, displacement, decimation, diagnostics, save/load, export.
6. Compare pinned-upstream results and Mesh Me360 results for deterministic cases.
7. Test offline install and offline runtime.
8. Test update flow separately with network enabled.
9. Test crash recovery through forced termination.
10. Generate release notes and final third-party notice review.

### Final v1.0 gate
Do not tag/release v1.0 until every mandatory row in `docs/FEATURE_MATRIX.md` is `PASS` or a repository-owner-approved documented exception exists.
