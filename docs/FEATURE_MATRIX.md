# Mesh Me360 — v1.0 Feature Matrix

Every row marked `Required` must be implemented for v1.0. Codex must not reinterpret previously described “optional” desktop ideas as optional; they are included in this matrix and therefore required.

Status values during implementation: `NOT STARTED`, `IN PROGRESS`, `BLOCKED`, `PASS`, `FAIL`.

| Area | Feature | Requirement | Acceptance summary | Status |
|---|---|---:|---|---|
| Import | Binary STL | Required | Loads through preserved upstream loader and renders correctly | NOT STARTED |
| Import | ASCII STL | Required | Loads through preserved upstream loader and renders correctly | NOT STARTED |
| Import | OBJ | Required | Loads and renders supported upstream OBJ geometry | NOT STARTED |
| Import | 3MF | Required | Loads supported upstream 3MF and preserves validation behavior | NOT STARTED |
| Import | STEP/STP | Required | Uses pinned upstream meshStep integration and quality presets | NOT STARTED |
| Project | `.bumpmesh` load | Required | Opens upstream-compatible project and restores model/settings/texture as available | NOT STARTED |
| Project | `.bumpmesh` save | Required | Saves reusable project and reloads without material state loss | NOT STARTED |
| Project | Undo/Redo | Required | Existing upstream edit history remains functional in new UI | NOT STARTED |
| Project | Dirty-state tracking | Required | User is warned before destructive close/open/new | NOT STARTED |
| Project | Recent files | Required | Max 20, deduplicated, persisted, missing-file behavior per requirements | IN PROGRESS |
| Textures | Upstream built-in textures | Required | All pinned upstream built-ins remain available | NOT STARTED |
| Textures | Custom texture loading | Required | Existing upstream custom texture path works | NOT STARTED |
| Textures | Texture smoothing | Required | Upstream blur/smoothing behavior preserved | NOT STARTED |
| Textures | Texture Library Manager | Required | Persistent custom library, thumbnails, search, tags, favorites, delete rules | NOT STARTED |
| Projection | Triplanar | Required | Same mapping behavior as upstream baseline | NOT STARTED |
| Projection | Cubic | Required | Same mapping and seam behavior as upstream baseline | NOT STARTED |
| Projection | Cylindrical | Required | Same mapping, seam, cap-angle behavior as upstream baseline | NOT STARTED |
| Projection | Spherical | Required | Same mapping behavior as upstream baseline | NOT STARTED |
| Projection | Planar XY | Required | Same mapping behavior as upstream baseline | NOT STARTED |
| Projection | Planar XZ | Required | Same mapping behavior as upstream baseline | NOT STARTED |
| Projection | Planar YZ | Required | Same mapping behavior as upstream baseline | NOT STARTED |
| Transform | U/V texture scale | Required | Upstream ranges/defaults/lock behavior preserved | NOT STARTED |
| Transform | U/V offset | Required | Upstream behavior preserved | NOT STARTED |
| Transform | Texture rotation | Required | Upstream behavior preserved | NOT STARTED |
| Transform | Part rotation gizmo | Required | Upstream rotation remains usable in redesigned UI | NOT STARTED |
| Displacement | Amplitude | Required | Upstream range and depth behavior preserved | NOT STARTED |
| Displacement | Symmetric displacement | Required | Neutral gray/inward/outward behavior preserved | NOT STARTED |
| Displacement | 3D GPU preview | Required | Real-time preview works through local Three.js/WebGL | NOT STARTED |
| Displacement | Amplitude overlap warning | Required | Existing warning threshold/behavior preserved | NOT STARTED |
| Mask | Top angle mask | Required | Upstream behavior preserved | NOT STARTED |
| Mask | Bottom angle mask | Required | Upstream behavior preserved | NOT STARTED |
| Mask | Paint Exclude | Required | Brush/single-face behavior preserved | NOT STARTED |
| Mask | Paint Include | Required | Exclusive include behavior preserved | NOT STARTED |
| Mask | Brush radius | Required | Adjustable upstream behavior preserved | NOT STARTED |
| Mask | Bucket fill | Required | Uses upstream adjacency/dihedral threshold behavior | NOT STARTED |
| Mask | Erase / Shift erase | Required | Existing interaction preserved | NOT STARTED |
| Mask | Clear mask | Required | Clears intentional mask state only | NOT STARTED |
| Mask | Smooth transition curves | Required | Linear/S-curve/ease-in baseline behavior available | NOT STARTED |
| Mesh | Adaptive subdivision | Required | Existing algorithm/crease handling preserved | NOT STARTED |
| Mesh | 10M triangle safety cap | Required | Existing safety behavior retained unless owner-approved change | NOT STARTED |
| Mesh | QEM decimation | Required | Existing QEM implementation and protections preserved | NOT STARTED |
| Diagnostics | Open-edge detection | Required | Existing diagnostic result and overlay remain available | NOT STARTED |
| Diagnostics | Shell count | Required | Existing shell diagnostic remains available | NOT STARTED |
| Diagnostics | Advanced overlay | Required | Existing upstream highlights remain usable | NOT STARTED |
| Viewer | Orbit | Required | Same practical mouse interaction as upstream | NOT STARTED |
| Viewer | Pan | Required | Same practical mouse interaction as upstream | NOT STARTED |
| Viewer | Zoom | Required | Same practical mouse interaction as upstream | NOT STARTED |
| Viewer | Fit/Home view | Required | Accessible from toolbar/view cube/navigation strip | NOT STARTED |
| Viewer | Place on Face | Required | Existing upstream function remains available | NOT STARTED |
| Viewer | Wireframe | Required | Toggle works and state is visible | NOT STARTED |
| Viewer | Grid | Required | Toggle works | NOT STARTED |
| Viewer | XYZ axes | Required | Toggle/indicator works | NOT STARTED |
| Viewer | View cube | Required | Original Mesh Me360 CAD-style orientation control changes view | NOT STARTED |
| Info | Triangle count | Required | Live current value shown without blocking viewport | NOT STARTED |
| Info | Bounding dimensions | Required | Live dimensions shown | NOT STARTED |
| Export | Binary STL | Required | Produces valid upstream-equivalent displaced STL | NOT STARTED |
| Export | 3MF | Required | Produces valid supported 3MF output | NOT STARTED |
| Export | Processing progress | Required | Subdivision→displacement→decimation→write stages visible | NOT STARTED |
| Desktop | WPF host | Required | .NET 8 WPF executable launches and owns OS integration | IN PROGRESS |
| Desktop | WebView2 embedded app | Required | Engine runs in application window, not external browser | IN PROGRESS |
| Desktop | Fully offline runtime | Required | Core app works with NIC/network disabled | IN PROGRESS |
| Desktop | No local server | Required | No Kestrel/HttpListener/Python/Node/PHP local service | IN PROGRESS |
| Desktop | No CDN runtime | Required | Release makes zero required CDN requests | IN PROGRESS |
| Desktop | Native save destinations | Required | Windows save dialogs used for save/export | IN PROGRESS |
| Desktop | Explorer startup file | Required | `MeshMe360.exe "path"` opens supported model/project | IN PROGRESS |
| Desktop | Drag and drop | Required | Single supported model/project opens; multiple drop handled explicitly | NOT STARTED |
| Windows | `.bumpmesh` association | Required | Explorer recognizes Mesh Me360 Project and opens app | NOT STARTED |
| Windows | STL Open With | Required | Mesh Me360 appears without hijacking default | NOT STARTED |
| Windows | OBJ Open With | Required | Mesh Me360 appears without hijacking default | NOT STARTED |
| Windows | 3MF Open With | Required | Mesh Me360 appears without hijacking default | NOT STARTED |
| Windows | STEP/STP Open With | Required | Mesh Me360 appears without hijacking default | NOT STARTED |
| Windows | Installer | Required | Clean supported Windows installation works | NOT STARTED |
| Windows | Uninstaller | Required | Removes app/owned registry entries, preserves user projects | NOT STARTED |
| Windows | Start menu shortcut | Required | Installed shortcut launches app | NOT STARTED |
| Windows | High-DPI | Required | 100–200% scaling without essential clipping | NOT STARTED |
| Windows | Multi-monitor | Required | Moving between DPI monitors remains usable | NOT STARTED |
| Reliability | Autosave | Required | Dirty doc snapshots every 120 sec under policy | NOT STARTED |
| Reliability | Crash/session recovery | Required | Forced process termination yields recoverable next-launch state | NOT STARTED |
| Reliability | Atomic recovery writes | Required | Interrupted autosave cannot replace last valid snapshot with partial file | NOT STARTED |
| Reliability | Structured logs | Required | Rolling logs capture host/engine errors without model payloads | IN PROGRESS |
| Reliability | Diagnostics screen | Required | Required host/WebGL/model metadata view/copy works | NOT STARTED |
| Updates | Automatic check | Required | Default once/24h, user-disableable, non-blocking offline | NOT STARTED |
| Updates | Manual check | Required | Help menu action checks immediately | NOT STARTED |
| Updates | Install update | Required | User-approved update installs and preserves AppData/user data | NOT STARTED |
| Updates | Dev/test update channel | Required | Development testing does not target stable users | NOT STARTED |
| UI | Fusion-inspired main layout | Required | Matches UI spec/mockups, without Autodesk assets | NOT STARTED |
| UI | DESIGN workspace | Required | Defined commands accessible | NOT STARTED |
| UI | TEXTURE workspace | Required | Defined texture/projection/mask commands accessible | NOT STARTED |
| UI | MESH workspace | Required | Subdivision/displacement/decimation/output accessible | NOT STARTED |
| UI | INSPECT workspace | Required | Diagnostics/display commands accessible | NOT STARTED |
| UI | Browser panel | Required | Defined logical tree; no fake CAD component semantics | NOT STARTED |
| UI | Properties panel | Required | Context controls use upstream ranges/defaults | NOT STARTED |
| UI | Bottom status/history | Required | Processing state/progress and real history info visible | NOT STARTED |
| UI | Light theme | Required | Functional and persistent | IN PROGRESS |
| UI | Dark theme | Required | Functional and persistent | IN PROGRESS |
| UI | System theme | Required | Default follows OS until overridden | IN PROGRESS |
| UI | Empty state | Required | Drop/Open affordance with supported formats | NOT STARTED |
| UI | Recovery dialog | Required | Matches flow and safety behavior in UI spec | NOT STARTED |
| UI | Preferences | Required | Required settings available and persisted | IN PROGRESS |
| UI | Keyboard shortcuts | Required | Minimum shortcut set works without breaking viewport controls | NOT STARTED |
| i18n | Upstream languages | Required | Existing translations remain accessible after UI refactor | NOT STARTED |
| Licensing | Upstream attribution | Required | Visible in About/notice files/source | NOT STARTED |
| Licensing | AGPL license/source access | Required | License/source links included with release | NOT STARTED |
| Licensing | Third-party notices | Required | Three.js, fflate, meshStep and added deps documented | NOT STARTED |

## Completion rule

A release candidate may not be called v1.0-ready while any `Required` row remains `NOT STARTED`, `IN PROGRESS`, `BLOCKED`, or `FAIL`, unless the repository owner explicitly approves a documented exception.

## Locked UI scope amendment — all rows required

The production PNG and UI_CONTROL_CONTRACT.md supersede conflicting older UI
rows. No row below is PASS; partial handlers do not satisfy a full contract.

| Area | Required capability | Status |
|---|---|---|
| Visual | 1536×1024 PNG fidelity, exact order/positions, human review | IN PROGRESS |
| Project | .mm360 multi-object round-trip, assets/materials/measurements/profiles | IN PROGRESS |
| Project | Legacy .bumpmesh migration without overwriting original | IN PROGRESS |
| Scene | Multi-import, tree/viewport selection, visibility, rename, lock, search | IN PROGRESS |
| Transform | Move/Rotate/Scale gizmos, numeric controls and undo | IN PROGRESS |
| Relief | Emboss, Deboss, Linear/Grid/Radial Pattern, preview/commit/cancel | IN PROGRESS |
| Geometry | Repair pipeline, report, safe holes and diagnostics | IN PROGRESS — libigl/PMP cleanup and bounded holes, retained report; self-intersection/stress/manual qualification pending |
| Geometry | Hollow, thickness, watertightness, undo | IN PROGRESS |
| Geometry | Boolean Union/Subtract/Intersect with validated backend | IN PROGRESS |
| Geometry | Split by interactive plane, keep sides, undo | IN PROGRESS |
| Geometry | Combine, optional weld, group preservation, undo | NOT STARTED |
| Geometry | Make Solid, quality/preview/diagnostics/undo | IN PROGRESS — offline libigl/Manifold reconstruction; fixture and control qualification, stress/manual acceptance pending |
| Measure | Distance, angle, bounds, ray thickness, persistent tree entries | NOT STARTED |
| Texture | Procedural/Image/Library sources, strength/invert/blend/direction | IN PROGRESS |
| Texture | Noise/ridges/cells/waves/checker/brick, seed/frequency/contrast/blur | IN PROGRESS |
| Library | Twelve reference sources, persistent custom/recent/favorites | IN PROGRESS |
| Material | PBR controls, reset, undo, export-aware persistence | IN PROGRESS |
| View | Shaded/Wireframe/Textures/X-Ray, comparison, cube/nav/ruler/stats | IN PROGRESS |
| Print | Prepare for Print, Bambu Lab 0.20mm and Generic STL profiles | NOT STARTED |
| Export | Native STL/3MF/OBJ, selected/all, profile/capability warnings | IN PROGRESS |
| UX | Every menu, Quick Action, inspector control and keyboard flow | IN PROGRESS |
| Quality | Responsive/DPI/stress/offline/full functional-control gates | NOT STARTED |

See implementation-notes/LOCKED_UI_STATUS.md for the verified backend blocker
and explicit remaining requirements; older single-model exclusions are superseded.
