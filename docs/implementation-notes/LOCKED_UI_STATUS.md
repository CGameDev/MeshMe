# Locked UI milestone — NOT COMPLETE

The owner supplied `MeshMe360_UI_Locked_Milestone_v1.0.zip` and explicitly requested
its locked milestone. `CODEX_START_HERE.md` was read directly from the archive
before extraction and the remaining instructions were read in their specified order.
The original PNG was inspected at 1536×1024. No screenshot pixels are embedded in
the application; icons and procedural textures are generated independently.

The package's UI/functional scope supersedes older UI documents. It does not
constitute confirmation of the previous M2 manual checkpoint. That remains pending.

## Implementation state

| Phase | State | Actual work / remaining requirements |
|---|---|---|
| A — shell | IN PROGRESS | Locked region layout, toolbar order/centers, tabs, shelf, existing viewport, camera modes/navigation and comparison presentation hook; typography, icon fidelity, source thumbnails, full inspector layout, responsiveness and human/pixel review remain. |
| B — project | IN PROGRESS | Versioned ZIP `.mm360` data model, multiple sequential imports, scene selection/visibility, numeric transforms/center, history and actual native save/reopen tested. Multi-file chooser workflow, full gizmo qualification, full legacy/texture stress qualification, safe close, autosave/recovery and broader tests remain. |
| C — engine relocation | IN PROGRESS | Existing scale/depth/invert/projection controls relocated with handlers intact; original advanced controls remain accessible. Emboss/Deboss/Apply Texture use the preserved worker with preview/commit/undo and signed geometry tests. Complete masking/refinement/decimation relocation, source-strength/blending, Pattern and broader integration remain. |
| D — new tools | IN PROGRESS | CSP-compatible Manifold build adopted with pinned source/licenses/hashes. Boolean, plane Split and inward Hollow previews/commits/undo pass initial real WebView2 tests. Drain placement, minimum-wall qualification, repair/solidification, remaining tools and broad geometry tests remain. |
| E — export/profiles | INCOMPLETE | Existing STL/3MF preserved and native `.mm360` save added; full multi-object export, OBJ/material packaging, profile execution and warnings remain. |
| F — verification | INCOMPLETE | Build, baseline geometry, initial scene tests and real WebView captures exist. Full functional matrix, stress/DPI/offline tests and human visual acceptance have not passed. |

**Do not mark this milestone or any visible tool complete based on registry
`implemented` flags.** Those flags indicate a handler exists; they are not proof
that all subrequirements pass. Move/Rotate/Scale now use pinned Three.js viewport
gizmos; an actual Move drag/undo passes, while full rotation/scale/multi-object/DPI
qualification remains. Material transactions, reset and project persistence exist;
export-capability integration remains.
The checker always fails the overall gate while these requirements remain open.

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
