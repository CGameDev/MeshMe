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
| B — project | IN PROGRESS | Versioned ZIP `.mm360` data model, multiple sequential imports, scene selection/visibility, numeric transforms/center, history and actual native save/reopen tested. Multi-file chooser workflow, gizmos, full embedded texture restoration, legacy fidelity, safe close, autosave/recovery and broader tests remain. |
| C — engine relocation | IN PROGRESS | Existing scale/depth/invert/projection controls relocated with handlers intact; original advanced controls remain accessible. Complete masking/refinement/decimation relocation, source-strength/blending, commit/undo integration remain. |
| D — new tools | BACKEND BLOCKER + INCOMPLETE | Procedural generation/data tests exist. Boolean/hollow/solidification backend cannot yet be adopted; remaining tool workflows and geometry tests are incomplete. |
| E — export/profiles | INCOMPLETE | Existing STL/3MF preserved and native `.mm360` save added; full multi-object export, OBJ/material packaging, profile execution and warnings remain. |
| F — verification | INCOMPLETE | Build, baseline geometry, initial scene tests and real WebView captures exist. Full functional matrix, stress/DPI/offline tests and human visual acceptance have not passed. |

**Do not mark this milestone or any visible tool complete based on registry
`implemented` flags.** Those flags indicate a handler exists; they are not proof
that all subrequirements pass. Numeric Move/Rotate/Scale still lack viewport
gizmos. Material editing still needs scene transactions and complete persistence.
The checker always fails the overall gate while these requirements remain open.

## Verified technical blocker: candidate geometry backend

Candidate: npm `manifold-3d` **3.5.1**, package license **Apache-2.0**. The actual
package/API was inspected in `.tools/manifold-3.5.1`; it has not been added to the
runtime vendor manifest or distributed product. Its full transitive license/source
and build audit remains a prerequisite for adoption.

The published Emscripten loader uses `new Function` in `createJsInvoker` and
`craftInvokerFunction`. An actual WebView2 probe with:

`script-src 'self' 'wasm-unsafe-eval'`

fails during initialization with:

> EvalError: Evaluating a string as JavaScript violates the following Content
> Security Policy directive because 'unsafe-eval' is not an allowed source of script.

Stack: `createJsInvoker` → `craftInvokerFunction` →
`__embind_register_class_constructor` in `manifold.js`.

Evidence: `artifacts/m2-integration-bcf3038a73324966a20b488e18e25666/backend-probe.txt`.
The application CSP was not weakened. No naïve replacement Boolean/hollow
algorithm was written. The installed environment has no `emcc`, `emcmake`,
`cmake`, `ninja`, or `clang` on PATH.

Resolution requires a reproducible, licensed CSP-compatible build (for example,
verify an Emscripten build with dynamic JavaScript execution disabled), or another
verified backend. Validate actual initialization and deterministic geometry before
adoption; do not assume a compiler option is sufficient without testing it.

Primary sources inspected: [Manifold API](https://manifoldcad.org/docs/jsapi/classes/manifold.Manifold.html)
and the actual versioned npm package, downloaded only to the development cache.

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
