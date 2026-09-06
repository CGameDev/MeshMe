# Mesh Me360 — Milestone 03: Locked UI Reference + Functional Toolset

**Status:** REQUIRED  
**Date:** 2026-09-05  
**Repository:** `CGameDev/MeshMe`  
**Product owner/publisher credit:** **Computer Universal Technology Systems**  
**Reference image:** `../ui/MeshMe360_UI_LOCKED_REFERENCE.png`

---

# 1. Goal

Replace the previous “Fusion-inspired mockup” interpretation with the exact Mesh Me360 interface shown in the locked reference PNG and make every visible command/control a genuine product feature.

This milestone intentionally expands the earlier scope.

The UI shown in the PNG is no longer a loose concept. It is the desired production application layout.

---

# 2. Non-negotiable directive to Codex

**The locked PNG is the visual source of truth.**

Codex must not treat it as:
- a mockup;
- an example;
- inspiration;
- a rough direction;
- a temporary skin.

At the 1536×1024 reference client size, implement the client UI to visually match it as closely as technically practical.

Do not make unsolicited layout improvements.
Do not convert it back into the older DESIGN/TEXTURE/MESH/INSPECT ribbon.
Do not remove the lower preset shelf.
Do not move the right Quick Actions panel.
Do not replace the Project tree with the previous Browser specification.
Do not omit the before/after viewport divider.
Do not hide visible toolbar tools in menus as a substitute for implementing them.

If a real technical conflict exists, keep the visual placement and document the conflict rather than silently redesigning.

---

# 3. Reference dimensions and structural layout

Reference image resolution: **1536 × 1024**.

Treat coordinates as client-area references, not absolute requirements at other DPI scales.

Approximate locked structural regions:

| Region | Reference geometry |
|---|---|
| Application/menu row | y ≈ 0–55 |
| Primary tool ribbon | y ≈ 56–151 |
| Main working area | y ≈ 152–985 |
| Global status bar | y ≈ 986–1023 |
| Left Project panel | x ≈ 7–295 |
| Main viewport / lower shelf column | x ≈ 302–1149 |
| Right inspector / Quick Actions | x ≈ 1156–1528 |
| Lower preset/library shelf | y ≈ 789–984, x ≈ 7–1149 |
| Right Quick Actions | lower-right, beneath inspector |

Primary intent:
- left Project panel about 19% of width;
- right inspector about 24% of width;
- viewport gets the largest single region;
- lower library shelf spans the left+center area but not the right inspector;
- top commands remain two fixed horizontal rows.

At larger resolutions, preserve proportions and let the viewport gain most additional space.
At smaller supported resolutions, use responsive compression/scroll/overflow without changing the conceptual placement.

Minimum supported usable client: 1280×720.
Test 1366×768, 1536×1024, 1600×900, 1920×1080 and common Windows DPI scaling.

---

# 4. Visual fidelity requirements

Match:
- dark charcoal/navy surfaces;
- slightly lighter panel surfaces;
- thin low-contrast borders;
- blue active/selected states;
- compact Segoe UI-style typography;
- icon-over-label toolbar controls;
- panel corner radii;
- toolbar separators;
- active blue outlines;
- dark viewport;
- material thumbnail shapes;
- thin sliders and numeric fields;
- inspector tab treatment;
- subtle green status indicator.

Use original/open/system icons, not Autodesk icon files.

Do not embed the reference screenshot as UI.

## Visual regression gate

Create an automated or repeatable screenshot test at exactly 1536×1024.

Compare:
- major region boundaries;
- toolbar order;
- control positions;
- panel sizing;
- selected-state styling;
- tab positions;
- inspector controls;
- lower shelf;
- status bar.

Mask only dynamic 3D model pixels and inherently variable OS/window-frame pixels.

Target:
- major edges within ±3 px;
- control centers within ±4 px;
- no missing/reordered reference controls;
- no materially different panel proportions.

A human visual review against the PNG is still required before marking this checkpoint complete.

---

# 5. Product identity change

Replace user-facing author/publisher references with:

**Done by Computer Universal Technology Systems**

Use the company as:
- About-screen product credit;
- installer/product publisher metadata where appropriate;
- executable assembly company metadata;
- documentation product-author credit;
- project/template metadata created by Mesh Me360 where an authoring application is recorded.

Do **not** replace legally required upstream copyright notices.
Do **not** claim Computer Universal Technology Systems wrote original stlTexturizer code.

Keep GNU AGPLv3 source/license obligations and upstream attribution intact.

---

# 6. Native file identity change

The visual reference uses `.mm360`, so make that the native Mesh Me360 project type.

Required:
- `.mm360`
- ProgID `MeshMe360.Project`
- `Mesh Me360 Project`

Continue to open/import `.bumpmesh` projects where practical for compatibility.

A converted legacy project saves as `.mm360` by default and does not overwrite the source `.bumpmesh` silently.

---

# 7. Required visible UI

The following exact top-level visual elements are mandatory:

## Application row
`Mesh Me360`
`Create. Texture. Print.`
`File`
`Edit`
`Mesh`
`Texture`
`Modify`
`View`
`Tools`
`Help`
Undo
Redo
document filename + dirty marker
Export
Export dropdown

## Command toolbar
`Select`
`Move`
`Rotate`
`Scale`
`Texture`
`Emboss`
`Deboss`
`Pattern`
`Repair`
`Hollow`
`Measure`
`Boolean`
`Split`
`Combine`
`STL`
`3MF`
`OBJ`
`Import`

## Viewport display controls
`Shaded`
`Wireframe`
`Textures`
`X-Ray`

## Project panel
Search
Models
Textures
Modifiers
Measurements
Export Profiles

## Inspector tabs
`Texture`
`Material`
`Properties`

## Texture source
`Procedural`
`Image`
`Library`

Reference quick sources:
Stone
Rock
Bricks
Scales
Wood
Metal
Fabric
Custom

Reference controls:
Scale
Depth
Strength
Invert
Advanced
Direction
Blend Mode
UV Mapping
Apply Texture

## Lower shelf
`Library`
`Presets`
`Recent`
`Favorites`

Reference presets:
Stone
Rock
Scales
Bricks
Wood
Metal
Fabric
Carbon
Leather
Concrete
Sand
Custom

## Quick Actions
`Center Model`
`Repair Mesh`
`Make Solid`
`Hollow Model`
`Generate Texture`
`Prepare for Print`

## Status
Ready/operation state
issue state
unit selector

Detailed behavior is normative in `docs/UI_CONTROL_CONTRACT.md`.

---

# 8. Scene/document model expansion

The old single-model assumption is insufficient for the locked UI.

Add a web-engine scene/document abstraction supporting multiple model objects so that:
- multi-import works;
- Project tree is truthful;
- Boolean can operate on multiple meshes;
- Split can create multiple objects;
- Combine can consume multiple objects.

Do not move geometry authority into C#.
Do not rewrite upstream displacement/projection/subdivision algorithms.

Adapter architecture is preferred:
- scene object owns transform/metadata;
- geometry operations call the appropriate preserved or vetted geometry backend;
- texture/displacement features reuse pinned upstream behavior when possible;
- host owns Windows shell/file/lifecycle services.

---

# 9. Geometry feature backend rule

Some locked-reference controls go beyond the original upstream feature set.

For missing complex geometry operations (especially Boolean, robust solidification and shell/hollow operations):

1. inspect upstream first;
2. do not fake the operation;
3. do not implement a naïve fragile algorithm solely to light up the button;
4. select a mature dependency or proven algorithm compatible with AGPLv3 distribution;
5. pin its version;
6. document its license in `THIRD_PARTY_NOTICES.md`;
7. add deterministic regression fixtures;
8. wrap it behind a Mesh Me360 interface so it can be replaced/tested independently.

Candidate technology must be verified by Codex before adoption. Do not assume a dependency API from memory.

---

# 10. Undo/redo contract

Every user-visible geometry/scene change must be represented as an undoable transaction when technically reasonable:

- move;
- rotate;
- scale;
- import/remove;
- texture apply;
- emboss;
- deboss;
- pattern;
- repair;
- hollow;
- make solid;
- boolean;
- split;
- combine;
- material change;
- measurement creation/removal.

For expensive operations, storing a checkpoint/result snapshot is acceptable if memory limits are managed.

---

# 11. Processing UX

Heavy operations must:
- keep the app responsive;
- show current operation in status area;
- disable conflicting commands;
- preserve previous valid state on failure;
- report actionable failures;
- only show Cancel if cancellation is safe.

No modal “please wait” loop that freezes the viewport for long jobs.

---

# 12. Functional implementation sequence

This is one milestone with internal phases. Do not declare completion between phases.

## Phase A — Reference-locked shell
- implement exact client layout;
- replace old SVG-driven layout;
- implement responsive rules;
- set new product credit;
- wire tabs/panels/tree;
- implement screenshot regression baseline.

## Phase B — Multi-object project model
- `.mm360` project container;
- multi-import;
- scene tree;
- visibility/selection;
- transforms;
- save/reopen;
- legacy `.bumpmesh` open path.

## Phase C — Existing engine feature relocation
- upstream texture mapping;
- displacement;
- projection modes;
- masking/refinement/decimation/diagnostics where retained;
- map them into the locked UI without losing capability.

## Phase D — Locked-reference tool expansion
- Emboss;
- Deboss;
- Pattern;
- Repair;
- Hollow;
- Measure;
- Boolean;
- Split;
- Combine;
- Make Solid;
- Generate Texture;
- Prepare for Print;
- material preview/export-aware behavior.

## Phase E — Export and profiles
- STL;
- 3MF;
- OBJ;
- active/default export profile;
- Bambu Lab (0.20mm) preparation preset;
- Generic STL profile;
- capability warnings.

## Phase F — Verification
- visual diff;
- functional matrix;
- project round-trip;
- multi-object operations;
- error handling;
- performance;
- high-DPI;
- offline operation;
- licensing notices.

---

# 13. Required tests

At minimum add deterministic tests for:

## UI
- exact control presence/order;
- Project tree selection sync;
- right-tab switching;
- bottom shelf tab switching;
- Quick Actions wiring;
- state-based disabled controls;
- reference screenshot structure.

## Project
- create/save/reopen `.mm360`;
- multi-object round trip;
- embedded/custom texture round trip;
- legacy `.bumpmesh` import/open;
- dirty state;
- autosave/recovery compatibility.

## Transforms
- Move;
- Rotate;
- uniform and non-uniform Scale;
- Center Model.

## Texture/relief
- procedural texture;
- image texture;
- apply;
- invert;
- projection modes;
- emboss;
- deboss;
- pattern;
- before/after preview divider.

## Geometry
- repair;
- hollow;
- make solid;
- boolean union/subtract/intersect;
- split;
- combine;
- diagnostics after each destructive operation.

Validate at least:
- finite vertices;
- valid triangle indices;
- triangle count;
- bounding box;
- manifold/watertight status where expected;
- normals;
- export success;
- no silent model loss.

## Measure
- known point distance;
- known angle;
- bounding dimensions;
- measurement persistence.

## Export
- STL round trip;
- OBJ round trip;
- 3MF round trip where exporter/parser test support exists;
- material capability warning behavior;
- export profile selection.

---

# 14. Performance guardrails

Use realistic fixtures:
- ~100k triangles;
- ~1M triangles;
- multi-million triangle stress case within the existing safety policy.

The UI must remain interactive during normal preview/navigation.

Do not add automatic subdivision/solidification that explodes triangle count without:
- prediction/warning;
- user confirmation;
- progress;
- safe failure.

---

# 15. Accessibility / Windows quality

Even though the appearance is locked:
- keyboard focus must be visible;
- icon-only controls get tooltips/accessibility names;
- menus are keyboard reachable;
- high contrast/focus must remain usable;
- text must not be permanently baked into raster assets;
- 125/150/200% DPI must not clip essential controls.

---

# 16. Definition of done

This milestone is complete only when all of the following are true:

- [ ] The app client visually matches `MeshMe360_UI_LOCKED_REFERENCE.png`.
- [ ] The old SVG mockups no longer override the locked reference.
- [ ] Product credit says `Done by Computer Universal Technology Systems`.
- [ ] `.mm360` is the primary project format.
- [ ] Multi-object project tree is real.
- [ ] Every visible toolbar command is functional when applicable.
- [ ] Every inspector control is functional.
- [ ] Every lower shelf tab works.
- [ ] Every Quick Action works.
- [ ] Shaded/Wireframe/Textures/X-Ray work.
- [ ] Before/after divider works.
- [ ] View cube/navigation/statistics/scale ruler work.
- [ ] Import and STL/3MF/OBJ export work.
- [ ] Heavy operations do not corrupt prior valid state.
- [ ] Undo/Redo covers the new editing operations.
- [ ] `.mm360` projects round-trip.
- [ ] Relevant `.bumpmesh` compatibility remains.
- [ ] Offline core operation remains intact.
- [ ] New third-party dependencies are pinned/noticed/licensed correctly.
- [ ] Automated tests pass.
- [ ] Human visual comparison at 1536×1024 passes.
- [ ] No “coming soon”, fake, demo-only or dead reference controls remain.

Do not mark this milestone complete merely because the UI looks right.  
Do not mark it complete merely because individual algorithms compile.  
Both the locked visual contract and the functional contract are required.
