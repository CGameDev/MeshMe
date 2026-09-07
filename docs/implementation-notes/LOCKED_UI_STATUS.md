# Locked UI milestone — NOT COMPLETE

The owner supplied `MeshMe360_UI_Locked_Milestone_v1.0.zip` and explicitly requested
its locked milestone. `CODEX_START_HERE.md` was read directly from the archive
before extraction and the remaining instructions were read in their specified order.
The original PNG was inspected at 1536×1024. No screenshot pixels are embedded in
the application; icons and procedural textures are generated independently.

The package's UI/functional scope supersedes older UI documents. It does not
constitute confirmation of the previous M2 manual checkpoint. That remains pending.

## Current implementation state — 2026-09-07

| Phase | State | Evidence and remaining gate |
|---|---|---|
| A — shell | VISUAL GATE OPEN | Region/toolbar measurements and five sizes in both themes pass. Production PNG appearance, especially material thumbnails, and human acceptance remain open. |
| B — project | AUTOMATED WORKFLOWS PASS | Multi-object scene, native save/reopen, safe New/Open/Exit, actual Move/Rotate/Scale, history, legacy conversion and recovery-format compatibility pass. Single- and two-object gestures pass; physical Windows interactions remain. |
| C — engine relocation | AUTOMATED WORKFLOWS PASS | Source composition, original settings/masks, relief/pattern, Refine/Simplify/Normals and revision-safe preview/commit/cancel pass. Upstream blobs remain exact. |
| D — new tools | AUTOMATED WORKFLOWS PASS | Boolean, Split, Combine, Hollow/drain, Make Solid, Repair, measurement and diagnostics have real workers and regression fixtures. Sampled wall checks are not a proof of the global minimum wall. |
| E — export/profiles | AUTOMATED WORKFLOWS PASS | STL/3MF/OBJ geometry/material capability workflows, source-image packaging, profiles, validation and native destinations pass. Physical save dialog interaction remains. |
| F — verification | NOT COMPLETE | 100k/1M/2.4M pipeline parity and navigation, 100–200% emulated density, offline asset closure and automated workflows pass. Full pixel/human fidelity, physical DPI/GPU/offline Windows acceptance and complete subrequirement sign-off remain. |

Registry `implemented` flags only indicate handlers exist. They do not certify all
control subrequirements. The overall checker remains fail-closed. Historical
entries below record earlier partial states; this table and the latest dated
checkpoint describe current work. No later milestone has been declared complete.

## Geometry backend blocker resolved — full milestone still incomplete

The published npm Manifold 3.5.1 loader failed actual WebView2 initialization
because it generated JavaScript functions at runtime. That failure is preserved
in `artifacts/m2-integration-bcf3038a73324966a20b488e18e25666/backend-probe.txt`.

A source build of the same pinned version with Emscripten 5.0.2 and
`DYNAMIC_EXECUTION=0` now passes under `script-src 'self' 'wasm-unsafe-eval'`.
General JavaScript `unsafe-eval` is still prohibited. Source revisions, licenses,
compiler settings and rebuild instructions are in MANIFOLD_BUILD.md and
THIRD_PARTY_NOTICES.md; all vendored files are hashed and verified offline.

Initial real WebView2 controls evidence:
`artifacts/m2-integration-4e289b7920b94fb484c8a600ba1e7aba`.
Boolean preview/cancel/commit/undo, Split/Hollow preview/commit/undo and
worker cancellation/restart pass. Operations run in isolated workers; no document
arrays transfer ownership, and originals change only after explicit Commit.
The application uses Manifold operations directly; no replacement mesh Boolean
or hollow algorithm was implemented. The 32-segment spherical erosion kernel is
an approximate offset. Drain placement and minimum-wall qualification remain.

Move gizmo mouse-drag evidence:
`artifacts/m2-integration-b692133d59534cfcbf2f5fbe778bf87e`.
The real Three.js handle changes position in one undo transaction and Undo
restores it. Linked scale and independent single-object local scale exist;
full Rotate/Scale and multi-object interaction qualification remains pending.

## Repeatable evidence

1. Build the solution; run the existing Core/asset/engine tests and
   `node --test tests/engine-web/locked-core.test.mjs`.
2. Run `MeshMe360.Desktop.Tests.exe <repository-root> --scene` in the normal
   Windows desktop environment. This checks real multi-object import, native
   `.mm360` save, save-completion dirty acknowledgement and token-based reopen.
3. Run the same test executable with `--locked` for an actual 1536×1024 WebView
   PNG and `locked-layout.json` under its isolated artifact folder.
4. Run `python build/check-locked-ui.py <locked-layout.json>`. Exit 1 is currently
   required: a structural pass is not the full visual or functional gate.

The dynamic cube is a deterministic fixture, not the illustrative dragon.
No full-viewport masking or comparison with a self-generated baseline is used to
claim fidelity. The production PNG remains the sole visual reference.

## Important integration findings

- Successive native activations exposed a stale ReadyClean notification revoking
  a newer token. Revocation now checks the exact current activation ID; the real
  rapid two-import regression passes.
- `.mm360` save completion is acknowledged on the existing documented
  `document.stateChanged` envelope, using its download request ID and sanitized
  saved basename. Only the saved revision becomes clean, preserving later edits.
- The `.mm360` container uses `project.json`, typed binary mesh arrays, embedded
  texture bytes, transforms/materials/modifiers/measurements/profiles and a format
  version. Arrays are validated before accepting a project. Resource-budget,
  untrusted ZIP stress, migration and full recovery tests remain required.
- Scene history currently has a 256 MiB budget and rejects a transaction when a
  single prior state exceeds it. Broader performance/UX qualification is pending.

## Texture/library/relief continuation

Per-object sampled texture pixels are now stored as versioned raw RGBA8 assets
inside the compressed `.mm360` container and reconstructed in the engine's
original CanvasTexture/ImageData representation. Native project tests retain two
different sources and verify every sampled byte after save/reopen and switching.
No duplicate C# geometry or texture state is introduced.

The persistent IndexedDB custom library supports import, rename, delete, favorite,
apply and procedural PNG generation. Noise/ridges/cells/waves/checker/brick,
seed/frequency/contrast/periodic blur/invert are wired. Actual UI tests generate,
save, reopen the store and verify exact pixels after applying to the engine.
Built-in presets never enter the mutable custom store.

Relief previews call the unchanged exportWorker/exportPipeline in bake mode with
input fields matching upstream bakeTextures(). The adapter retains the original
parent-face exclusion rule when adopting a preview at explicit Commit. A real
worker-ready handshake is required: sending before ready initially lost the job
and is now covered by the actual browser regression. Cancellation terminates
only the owned worker. Emboss/Deboss/Apply Texture tests verify the direction of
actual geometry movement and full scene Undo. Advanced region/precision masking,
large-job qualification and every control subrequirement are still pending.

Latest full `build/Test-Locked.ps1` run passes 36 Core, 6 static asset, 14 Node
checks, actual host/scene/solid/Move/library/relief runs and deterministic solid
fixtures. Representative artifacts:
- `artifacts/m2-integration-77a15d9cd02c415f8661f6ab73ea272c` — scene/texture reopen
- `artifacts/m2-integration-ca37d96b70e84b32a83aca7ae2f01f8c` — library
- `artifacts/m2-integration-80f46cbe7e9f4c9697480e3e51df921c` — relief
- `artifacts/m2-integration-2e54e37979fc43d595206d94baf348a1` — visual capture

The PNG appearance and full functional gate have not passed. Remaining entirely
unimplemented named commands at this point are Pattern, Repair, Measure, Combine,
OBJ, Repair Mesh, Make Solid and Prepare for Print. Implemented handlers still
need all listed subrequirements and tests before acceptance.

### Solidification continuation

Make Solid is now implemented with an offline, CSP-safe libigl/Manifold adapter,
adjustable cell size, resource/count warnings, worker preview/cancellation,
validated output diagnostics and undoable commit. See `SOLIDIFY_BUILD.md` for
source pins, complete licensing/source closure, applicability and limitations.
All current regressions in `build/Test-Locked.ps1` passed after this addition:

- actual solid controls: `artifacts/m2-integration-752d90c4ab96438b8326578321ac60a3`
- reconstruction fixtures: `artifacts/m2-integration-6d26dc1a7f1142daae501484b8802d56`
- fresh UI capture: `artifacts/m2-integration-dde6513c630645bcb8490b1362c891e8`

The structural visual check passes. Functional, pixel appearance and human
visual acceptance remain incomplete. Seven named commands are still entirely
unimplemented: Pattern, Repair, Measure, Combine, OBJ, Repair Mesh and Prepare
for Print. Full subrequirements for already wired controls remain open.

### Repair continuation

Repair and Repair Mesh share the same worker pipeline and retained report.
The complete current regression suite passes, including damaged mesh cleanup,
bounded holes, large-opening/cavity retention, non-manifold reporting and both
actual UI entry points. See `REPAIR_BUILD.md` for pins and limitations.

- solid controls: `artifacts/m2-integration-94ba261b9dce4bc5bb28452cfb23e0b3`
- repair fixtures: `artifacts/m2-integration-5516dbf58b124be2bb107cc616e028c0`
- visual capture: `artifacts/m2-integration-f59e4a48df844ec68449fd2cbb6436f9`

Five named commands remain entirely unimplemented: Pattern, Measure, Combine,
OBJ and Prepare for Print. Full control subrequirements, self-intersection
qualification, stress, pixel appearance and human/physical acceptance remain
open. M3 remains NOT COMPLETE.

### Combine continuation

Combine now runs in the geometry worker with preview/cancel/commit/undo/redo,
optional exact vertex welding and material groups. Actual renderer/native project
reopen checks pass (`artifacts/m2-integration-4e962cecbbb34aa58f53905e387d1dff`).
The full current regression suite passes: 16 Node checks, 6 asset checks, 36 Core
checks, all actual application modes and three geometry fixture probes.
Four named commands remain entirely unimplemented: Pattern, Measure, OBJ and
Prepare for Print. Broader control, grouped-relief, visual, stress and physical
acceptance remain open. M3 is NOT COMPLETE.

### Normal export continuation

STL/3MF/OBJ shortcuts and primary Export now use a shared worker review/native
destination workflow. Actual tests cover two-object geometry, capability reports,
3MF metadata, OBJ/MTL/source-image packaging, live displacement through the
unchanged export pipeline, remembered session format and both cancellation paths.
Evidence: `artifacts/m2-integration-1993a77e548b41edbc69f7838deb4c87`.
The entire current suite passes (18 Node, 6 assets, 38 Core, actual application
modes and three geometry probes). No release/manual gate is implied.

Three named commands remain entirely unimplemented: Pattern, Measure and Prepare
for Print. Broader controls, profiles/menus, mask/precision workflows, visual
fidelity, stress and physical/human acceptance remain open. M3 is NOT COMPLETE.

### Measurement continuation (2026-09-07)

Measure supports actual surface picking for distance/three-point angle, selected
world bounds and a local inward-normal wall estimate after closed-mesh validation.
Snapshots are named Project tree entries with rename/delete/undo and native
project persistence. The local estimate is not minimum-wall certification.
Actual mouse/persistence evidence: `artifacts/m2-integration-3f61b1395f4d4715ace241232b2257e4`.
All current regressions pass: 20 Node checks, 6 asset checks, 38 Core checks,
actual application modes and all three backend fixture probes. Locked screenshot:
`artifacts/m2-integration-b8232a8ac42a4a558e7830f0c0e784a7` (evidence, not acceptance).
Pattern and Prepare for Print remain unwired. Broader controls, visual fidelity,
stress and physical/human acceptance remain open. M3 is NOT COMPLETE.

### Pattern continuation (2026-09-07)

Linear/Grid/Radial Pattern now composes repeated source-height tiles in a worker
and runs the preserved displacement pipeline. Count, spacing, detail dimensions,
orientation, preview/commit/cancel and undo are wired. Deterministic layout and
height tests pass; actual app tests verify distinct finite meshes, original-state
retention and saved metadata. Evidence: `artifacts/m2-integration-0ed66828c6c1449fbea26259882f06ea`.
Full current suite passes: 21 Node, 6 assets, 38 Core checks, all actual modes and
three geometry probes. Locked screenshot evidence:
`artifacts/m2-integration-d8cd3d3c3b6047ccb6549db6fd3d6a34`.
Prepare for Print remains entirely unwired. Broader controls, visual fidelity,
stress and physical/human acceptance remain open. M3 is NOT COMPLETE.

### Print preparation continuation (2026-09-07)

Prepare for Print now reviews dimensions/units, explicit centre/bed placement,
open/non-manifold edges, degenerates, closed winding/volume and sampled wall
warnings. Profiles persist and the final format opens native export. Actual
checks cover unchanged review/cancel, placement undo, threshold warnings,
profile/unit reopen, native 3MF and open/reversed/thin-wall fixtures.
Evidence: `artifacts/m2-integration-85487862e6b140d4991b3e76ad1b9088`.
Full current suite passes: 22 Node, 6 assets, 38 Core checks, all actual modes
and three geometry probes. Every named toolbar/Quick Action has a handler;
this does not establish full behavior coverage. Source strength/blend/direction,
menus/profiles, complete mask/advanced relocation, visual fidelity, performance,
recovery and physical/human acceptance still require work. M3 is NOT COMPLETE.

### Source composition continuation (2026-09-07)

Strength, source inversion and five blend modes now change actual source heights
and persist source/base/results/settings with undo. Actual tests verify exact
raw and smoothed-pipeline pixels, inspector state and native reopen. All native
project tests now wait for a new clean revision (not a filename set by Save).
Evidence: `artifacts/m2-integration-71a96a103870484c997d5770a83d5fc8`.
Full current suite passes: 23 Node, 6 assets, 38 Core, all actual application modes
and three backend probes. Screenshot:
`artifacts/m2-integration-342441d06e0d4f0cb72cd8902bddf66e`.
Direction still reports the preserved normal-only engine; axis support is not
invented. Full menus/advanced masks, visual fidelity, stress/recovery and physical
or human acceptance remain open. M3 is NOT COMPLETE.

### Reference layout refinement (2026-09-07)

Header Undo/Redo icons now have explicit dimensions and sit beside the document
name; export buttons match the reference footprint. Inspector rows align at the
reference y positions, the actual scale-link control is restored, and Apply
Texture is visible without a scrollbar at 1536×1024. Source sliders and Invert
use the reference track/toggle treatment. Active Select/transform styling follows
the current tool. Actual relocated scale-link/source tests pass.
Detailed layout evidence: `artifacts/m2-integration-1515c6797e094186879bea4a053ba83d`.
The structural checker now covers header/source-grid/Apply bounds and detects
reference-size inspector overflow. It reports structural PASS, zero missing
named command handlers, and still returns NOT COMPLETE for full acceptance.
Material thumbnails, icon details, viewport/grid/cube, full controls, DPI/stress
and human visual/physical acceptance remain open.

### Material swatch continuation (2026-09-07)

Built-in shelf cards now render actual height sources on Three.js material
spheres. Flat source cards share the corresponding height-derived color preview.
Renderer target/viewport/scissor/clear state is restored after offscreen renders;
model PBR settings remain independent. New deterministic stone, woodgrain and
plate source kinds improve the Stone/Wood/Scales defaults, while every previously
available generator kind retains its implementation. The generator also exposes
these source kinds and Scales. Original source pixels are saved with projects.
The full current suite passes (23 Node, 6 assets, 38 Core, all app modes and three
backend probes), including exact source/pipeline/native project tests.
Screenshot: `artifacts/m2-integration-b809ee184f764f228e6d3e7e59718882`.
This is progress toward the supplied PNG, not a pixel/human fidelity pass.
M3 remains NOT COMPLETE; control, visual, stress/recovery and physical gates
remain open.

### Selection order and native menus (2026-09-07)

Ctrl-click now toggles object membership in both the tree and viewport. The
selected-object accessor follows pick order so the first Boolean Subtract
operand is the first picked object, independent of import order. Selection
alone does not add geometry history. Regression covers toggle/order and project
serialization; the actual Windows tree path is also exercised.

File now exposes the host-owned recent list with open, missing-file state,
remove and clear. Project Settings changes display units and export profile in
one undoable transaction without rescaling geometry; profile tree buttons also
select the actual active export profile. Preferences currently exposes the
working host-persisted System/Light/Dark appearance setting. This is partial
Preferences coverage; autosave/update/recovery controls remain outstanding and
are not represented as functional merely because their settings schema exists.

Native menu evidence: artifacts/m2-integration-cb045beaa22f45a1ad9673ea9e805572.
Full milestone acceptance remains NOT COMPLETE. Other menus, control details,
visual fidelity, DPI/stress, recovery compatibility and human/physical gates
remain open.
Full current regression suite passed after this change: build (zero warnings),
6 asset checks, 24 Node checks, 38 Core checks, all 14 native app modes and
three geometry backend probes. Latest screenshot:
artifacts/m2-integration-50a7fe5321454a3b83ff38e8f85de8b8.

### Texture edit transactions (2026-09-07)

Visible Scale/Depth/UV controls and scale linking now record native user edits
in scene history. Restoring a project or Undo uses upstream synthetic events
without creating spurious edits. Single-object applicability is enforced.
Build and actual --settings, --source and --scene checks pass. Keyboard evidence:
artifacts/m2-integration-2fa812e4b5dc4f4daf5f7d0564683daa.
Upstream 117-blob integrity check passes. M3 remains NOT COMPLETE.

### Modify, Help and document shortcuts (2026-09-07)

Menus now directly expose the implemented mesh/texture tools. Modify includes
all six required commands, including undoable Place on Bed (preserves XY) and
Reset Transform (preserves authored geometry). Help provides a local usage
guide, accurate available shortcuts, complete packaged AGPL/third-party texts,
source link and product credit. Document shortcuts now route to scene commands;
Ctrl+Z was verified using actual WebView2 keyboard input rather than invoking
an artificial handler. Text-field Undo remains native.

Build, 16 scene/core Node tests, and actual --settings/--menus tests pass.
Evidence: artifacts/m2-integration-dd965f4ed07f45bb8172b31fff3056a1 and
artifacts/m2-integration-2b03e5cb4cf04720a6e87a3cea705ea6.
Other menu capabilities (including safe Exit, mesh refinement/diagnostics,
view toggles and full Preferences), pixel fidelity and the remaining milestone
acceptance gates are still open. M3 is NOT COMPLETE.

### View controls continuation (2026-09-07)

View now provides grid/axes toggles, Home, Fit Selected/All, both supported camera
projections and panel visibility controls. Hidden panels return space to the
viewport and restore their original placement. Fits use visible world bounds
and do not edit document state. The Three.js transform control camera follows
projection changes. Guide preferences survive engine rebuild/theme rendering.
The default reference layout is unchanged.

Build, 6 offline asset checks, actual --view/--transform/--locked checks pass.
View evidence: artifacts/m2-integration-d09a0c03f8e84eccb8f47a894e9fb52f.
Screenshot: artifacts/m2-integration-1b2ba3416e5245d29376562d58527c96.
Structural gate PASS; full functional gate INCOMPLETE; pixel/human review PENDING.
M3 remains NOT COMPLETE.

### Diagnostics continuation (2026-09-07)

Mesh/Tools diagnostics now run the same checked upstream edge/sliver and Manifold
validation plus sampled opposite-surface wall tests used by Prepare for Print.
Reports populate selected-object Properties and a truthful status summary. Any
new project revision invalidates the displayed assessment. Diagnostics do not
change geometry or project state, and cancellation terminates the worker.
The global operation label now reflects active processing/tool ownership.

Build and actual --diagnostics checks pass, including cube edge/solid/wall values,
Properties/status display, unchanged document bytes, stale-report invalidation
and cancellation. Evidence:
artifacts/m2-integration-e0fa310ad3d34bcfa87c507f6da35cae.
Self-intersections and true minimum wall are explicitly unverified. Reports are
session assessments, not persisted certifications. M3 remains NOT COMPLETE.

### Empty project and Escape state (2026-09-07)

An empty/new scene now hides the engine's retained mesh and uses actual scene
visibility for comparison/fit availability. Texture controls remain disabled
until an unlocked scene object is selected. Escape closes a transient dialog
without also clearing object selection; with no dialog it clears selection and
resets the active transform tool. Native --empty verifies all these states and
fresh import after New Project. Build passes. Evidence:
artifacts/m2-integration-d81bc98e86624a9f96cb61eaeef6a016.
M3 remains NOT COMPLETE.

### Application Diagnostics continuation (2026-09-07)

Tools and Help now show actual host/runtime/WebGL information, selected-model
format/triangle count/dimensions, camera and texture projection, displacement
preview state, last reported engine error and the host log folder. Only summary
metadata is copied; model/texture payloads are not included. Copy and log-folder
actions route to the Windows host; the web page cannot choose an arbitrary
folder to launch. Automated tests substitute clipboard/Explorer actions while
verifying exact report contents and the host-owned log path. Physical clipboard
and Explorer checks remain manual gates.

Build and actual --app-diagnostics pass. Initial evidence:
artifacts/m2-integration-7e08aa392e614751ac073c643b555c30.
A mixed-encoding ellipsis in the operation label was normalized to UTF-8.
The full expanded regression run passed: 25 Node, 38 Core, 6 offline assets,
19 native app modes and all three geometry backend probes. Latest screenshot:
artifacts/m2-integration-271f496fc4dd4168a8771ac0fd2e9de0.
M3 remains NOT COMPLETE.

### Packaged review checkpoint (2026-09-07)

Fresh runnable build:
artifacts/m3-locked-review-20260907-controls/MeshMe360.exe
Published from source checkpoint 2abf045. All 224 packaged web files match staging;
.NET/WPF/legal notices are present. Launched successfully; native web.ready
handshake completed at 2026-09-07T13:25:20Z and the process was responding.
This is a development review build, not a release or completed M3 checkpoint.
Remaining controls, pixel/human fidelity, recovery/stress/DPI and physical Windows
acceptance remain required. No milestone was marked complete.


### Mesh processing and safe File lifecycle (2026-09-07)

Refine, Simplify and Recalculate Normals now have worker preview/commit/cancel,
Undo/Redo, diagnostics and native project round-trip tests. Grouped simplification
protects material seams and painted regions through the unchanged decimator's
locked-face input. Optional normals persist in the v1 container. Clear Texture
removes the live source and restores it exactly through Undo.

New/Open and Exit now protect dirty documents with native Save / Don’t Save /
Cancel. Cancelled and failed downloads reach the document; successful saves are
acknowledged only after staging and destination replacement. Cancelling Open
releases the source file handle. Accepted close detaches the bridge before WPF
releases WebView2. Native dialog choices are substituted in automated tests;
physical chooser/close interaction remains a manual gate.

Full suite PASS: 27 Node, 38 Core, 6 asset checks, 21 native app modes and all
three backend probes; build has zero warnings/errors and all 117 upstream blobs
match. Mesh evidence: artifacts/m2-integration-19e49a29f71443b6a3ed2ad271f842fc.
Lifecycle: artifacts/m2-integration-9c427a68f7814e9588d2a11c56ae5fff.
Source/Clear: artifacts/m2-integration-f5eab849e0944488bdf5b967391d0e60.
Screenshot: artifacts/m2-integration-be1fc2f96bfa4a5ea69baee833b22167.
M3 remains NOT COMPLETE: complete advanced/legacy integration, visual fidelity,
recovery compatibility, stress/DPI, and human/physical acceptance remain open.


### Advanced/legacy integration and display verification (2026-09-07)

Advanced settings, painted masks, original orientation and the baked-face masking
option now join scene history and native project persistence. Legacy projects
have a visible all/settings chooser; intermediate load events cannot partially
replace the scene. Conversion saves as a dirty .mm360 document. Cancel and
settings-only Undo preserve the previous scene exactly. Programmatic previews
are isolated from the engine's deferred history capture.

The view cube reflects the camera and all six alignment directions. Wireframe
and X-Ray also update non-active scene objects. Both themes and five required
client sizes pass unclipped command-label checks at 100/125/150/200% emulated
pixel density. Light statistics/slider contrast and the compressed shelf were
visually corrected; physical Windows per-monitor DPI is still untested.

Full suite PASS after the preview-history regression fix: 27 Node, 38 Core,
6 offline assets, 24 native app modes and three backend probes. Zero build
warnings/errors; all 117 upstream blobs remain exact. Final screenshot:
artifacts/m2-integration-1bdc7ca995294f60a60e028f0b130fc0.
Display evidence: artifacts/m2-integration-c735dab2d29042bb870e4bf8e090ddef.
Relief/history evidence: artifacts/m2-integration-7b0fb4647034467d9f4880c8b2cf14e6.
Earlier focused advanced/legacy evidence is retained at
artifacts/m2-integration-4fbe11bb059148e98334e085d9190101 and
artifacts/m2-integration-2fa92171d4e9494197f9e9fd08cf7048.
M3 remains NOT COMPLETE. Refinement growth safety, performance/recovery
compatibility, full functional coverage and pixel/human acceptance still need work.


### Relief safety, recovery format and stress checkpoint (2026-09-07)

Relief/pattern/live export now wrap the unchanged original pipeline in a
3,000,000-triangle guard. Relief Preview requires an explicit growth checkbox.
Progress and Cancel remain real worker operations. The guard checks input,
post-subdivision progress and final output; it is not a hard peak-allocation cap,
because the original subdivision callback occurs after each pass. No upstream
algorithm changed. Exact output parity and safe rejection preserve source state.

M3 recovery compatibility serializes the existing mm360 format and restores a
dirty document without acknowledging an explicit save. Native renderer/source
restoration and corrupt-input preservation pass. M6 owns scheduling, retention,
crash prompts and disk coordination; those are not claimed here.

The native stress test covers 97,200 / 995,328 / 2,408,448 input triangles and
compares positions/normals exactly with the preserved worker. Navigation continues
during processing. See M3_PERFORMANCE.md for timings and measurement limitations.
Both themes pass five client sizes at 100/125/150/175/200% emulated density.
Native Move/Rotate/Scale drags each create exactly one undoable transaction.

Texture source identity is stored with the asset instead of inferred from its
filename. Highlights follow Undo/Redo, selection and project reopen. Existing
projects without that optional identity stay usable and show no guessed preset.
Fabric/Carbon now use actual generated weave heights; Scales uses overlapping
plate heights. These are app-owned sources, not changes to upstream algorithms.
Visual comparison of these materials remains pending; M3 is NOT COMPLETE.

Focused checks pass: 28 Node tests; native source/library/recovery, pattern,
exports, display, stress and transform modes. Final combined suite PASS: 28 Node, 38 Core, 6 asset checks, 26 native
app modes and three geometry backend probes. Zero build warnings/errors; all
117 upstream blobs match. Additional two-object Move/Rotate/Scale gesture and
Undo checks pass at artifacts/m2-integration-0017a49234fe4f6cb85210ee38babd8a.
Final suite stress: artifacts/m2-integration-210d5a1ad93d488bb733c81eb35fd515.
Screenshot/layout: artifacts/m2-integration-ef497be56a4a464b859d7625834f103e.
The structural gate passes; the overall checker correctly returns NOT COMPLETE.
A registered, unmasked HTML comparison is generated by create-locked-review.py;
this review aid does not certify pixel appearance or human acceptance.


### Packaged checkpoint launched (2026-09-07 15:06 UTC)

Commit b7da488 is published at
artifacts/m3-locked-review-20260907-validated/MeshMe360.exe.
All 234 published web files match staging, and runtime/notices verification passes.
Process 20032 launched successfully; native binary fixture consumption and document
state/title acknowledgements were observed. This is launch evidence, not physical
interaction or visual-fidelity approval. The unmodified target/app comparison is
artifacts/m2-integration-ef497be56a4a464b859d7625834f103e/locked-visual-review.html.
M3 remains NOT COMPLETE; source/material thumbnail fidelity and required human/
physical Windows acceptance remain open. The UI must not be represented as having
passed the locked production appearance gate.
