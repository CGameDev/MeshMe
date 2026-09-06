# Mesh Me360 — Locked UI Control & Functional Contract

This file defines the behavior of the controls visibly represented in
`docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png`.

The purpose is to prevent decorative or ambiguous UI implementation.

---

## A. Top application row

Visible structure, left to right:

- Mesh Me360 glyph
- `Mesh Me360`
- tagline: `Create. Texture. Print.`
- `File`
- `Edit`
- `Mesh`
- `Texture`
- `Modify`
- `View`
- `Tools`
- `Help`
- Undo
- Redo
- centered/current document name and dirty marker
- primary `Export` button
- Export dropdown arrow

### File
Implement:
- New Project
- Open Project…
- Open Model…
- Open Recent
- Import Model…
- Save
- Save As…
- Export…
- Project Settings
- Exit

### Edit
Implement:
- Undo
- Redo
- Duplicate Selected
- Delete Selected
- Select All applicable objects
- Preferences

### Mesh
Expose the real mesh operations provided by this milestone:
- Repair
- Make Solid
- Hollow
- Subdivide/Refine
- Simplify/Decimate
- Recalculate Normals
- Boolean
- Split
- Combine
- Diagnostics

### Texture
Implement:
- Texture panel
- Procedural source
- Image source
- Texture Library
- Apply Texture
- Emboss
- Deboss
- Pattern
- Generate Texture
- Remove/Clear active texture or displacement

### Modify
Implement:
- Move
- Rotate
- Scale
- Center Model
- Place on Bed / ground plane
- Reset Transform

### View
Implement:
- Shaded
- Wireframe
- Textures
- X-Ray
- Grid toggle
- Axes toggle
- Home view
- Fit selected/all
- Perspective/Orthographic when renderer supports both
- Browser panel toggle
- right inspector panel toggle
- lower library shelf toggle

### Tools
Implement:
- Measure
- Prepare for Print
- Mesh Diagnostics
- Application Diagnostics
- Preferences

### Help
Implement:
- Documentation
- Keyboard Shortcuts
- Source Code / License
- Third-Party Notices
- About Mesh Me360

---

## B. Primary command toolbar

Control order must match the PNG:

`Select | Move | Rotate | Scale | Texture | Emboss | Deboss | Pattern | Repair | Hollow | Measure | Boolean | Split | Combine | STL | 3MF | OBJ | Import`

The primary Export button remains at top-right.

### Select
- object selection by click;
- face/region selection when the active tool supports it;
- Ctrl multi-select for objects;
- Esc clears/cancels active selection/tool;
- selected object has a clear but restrained outline/highlight;
- selection synchronizes with the Project tree.

### Move
- translate selected object(s) with viewport gizmo;
- numeric X/Y/Z entry in Properties;
- units follow current display unit;
- action is one undoable transaction.

### Rotate
- rotate selected object(s) with gizmo;
- numeric X/Y/Z degrees;
- one undoable transaction.

### Scale
- uniform scale by default;
- optional independent X/Y/Z scale;
- lock/link control for uniform scaling;
- numeric entry;
- one undoable transaction.

### Texture
- focuses the right `Texture` tab;
- live preview from the selected source;
- does not permanently mutate geometry until Apply/Commit where geometry displacement is involved.

### Emboss
Create positive surface detail from the active grayscale/procedural source:
- region/object targeting;
- depth control;
- scale/UV mapping control;
- live preview;
- commit/cancel;
- undo/redo;
- use the existing displacement/subdivision engine where possible.

### Deboss
Same workflow as Emboss but produces inward/negative surface displacement. Do not merely invert viewport shading.

### Pattern
Provide real repeat placement for texture/relief detail:
- Linear;
- Grid;
- Radial;
- count;
- spacing;
- orientation;
- preview;
- commit/cancel.

If the operation is implemented through repeated masks/displacement, keep it deterministic and undoable.

### Repair
Run a documented repair pipeline and show before/after diagnostics. At minimum:
- remove degenerate/zero-area triangles;
- remove invalid/non-finite vertices;
- merge genuinely duplicate vertices within a safe tolerance;
- correct inconsistent winding/normals where unambiguous;
- identify/fix small boundary holes where safe;
- retain a report of anything not automatically fixed;
- never silently destroy large intentional openings.

### Hollow
Create a printable internal shell:
- user-defined wall thickness;
- inward shell by default;
- preview;
- optional drain-hole placement workflow;
- watertightness check after commit;
- minimum-wall warning;
- undoable.

Do not fake hollowing by merely hiding faces.

### Measure
The Measure tool must support:
- point-to-point distance;
- angle using three picked points;
- selected-object bounding dimensions;
- local wall/thickness estimate using opposite-surface ray tests where valid.

Measurements appear under the Project tree `Measurements` node and can be renamed/deleted.

### Boolean
For two or more applicable closed meshes:
- Union;
- Subtract;
- Intersect.

Requirements:
- robust manifold validation before execution;
- preview/result diagnostics;
- preserve originals until commit;
- clear error if input cannot be safely processed;
- undoable.

Use a mature, license-compatible geometry implementation when the upstream engine does not provide robust Boolean operations. Document the dependency and license. Do not invent a fragile triangle-only Boolean algorithm just to satisfy the button.

### Split
Support:
- split by an interactive plane;
- keep both sides by default;
- optional keep-positive / keep-negative;
- create separate scene objects;
- preserve transforms;
- undoable.

An object-as-cutter split may be added if it uses the same verified Boolean backend.

### Combine
Combine selected scene objects into one scene object:
- merge geometries;
- optional weld coincident vertices;
- preserve/material-group information where format/backend allows;
- this is distinct from Boolean Union;
- originals remain recoverable through Undo.

### STL / 3MF / OBJ
These are format-specific export shortcuts:
- open the normal export workflow with format preselected;
- validate the selected/project geometry;
- use native Windows destination dialog;
- show format capability warnings.

STL:
- geometry only; do not pretend it preserves PBR material/texture metadata.

3MF:
- preserve supported unit/material/project metadata where implementation supports it.

OBJ:
- export geometry and MTL/texture references or package them predictably when applicable.

### Import
Import one or more supported models:
- STL
- OBJ
- 3MF
- STEP
- STP

Do not block the UI while parsing large files. Imported models become independent Project-tree objects.

### Export
Primary action uses the last chosen/default export profile.
Dropdown must expose at minimum:
- Export STL
- Export 3MF
- Export OBJ
- Export Selected
- Export Project/Prepared Model using active profile

---

## C. Project / scene panel

Match the reference panel visually:

- header `Project`
- search field
- project root, e.g. `dragon_statue.mm360`
- `Models`
- object nodes
- child mesh/group nodes where meaningful
- `Textures`
- `Modifiers`
- `Measurements`
- `Export Profiles`

Required behavior:
- tree selection syncs to viewport;
- eye icon toggles visibility;
- search filters tree without deleting nodes;
- F2/context action renames user-owned objects;
- Delete removes selected user-owned object after normal undoable edit rules;
- multiple imported objects are supported;
- drag/drop reordering may be added but is not required if order has no rendering meaning.

### Modifiers
Represent committed/current model operations such as displacement and smoothing in a truthful way. Do not show fake parametric history if the engine cannot re-evaluate it.

### Export Profiles
Ship at least:
- `Bambu Lab (0.20mm)` preparation/export preset
- `Generic STL`

These are preparation/export presets, **not a slicer and not G-code generation**.

---

## D. Central 3D viewport

Match:
- large dark viewport;
- top-left mode pills: `Shaded`, `Wireframe`, `Textures`, `X-Ray`;
- left vertical navigation strip;
- top-right orientation cube;
- grid/axes;
- bottom-left geometry statistics;
- bottom-right physical scale ruler;
- draggable vertical before/after comparison divider shown during applicable texture/relief preview.

### View modes

Shaded:
- neutral model material with lighting.

Wireframe:
- clear edge display; user can still select objects.

Textures:
- texture/material preview.

X-Ray:
- transparent/ghosted visualization useful for internal/hollow inspection.

### Before/after comparison divider
When texture, emboss, deboss, material or applicable modifier preview is active:
- show a vertical draggable comparison divider;
- one side shows base/original appearance;
- the other shows previewed result;
- double-click resets divider to 50%;
- allow user to turn comparison off without disabling the preview.

This is a real feature represented by the reference image, not decorative art.

### Navigation
- Orbit
- Pan
- Zoom
- Fit/Home
- standard mouse-wheel zoom
- predictable CAD-style modifier keys documented in Keyboard Shortcuts

### Orientation cube
Original Mesh Me360 cube:
- Front / Back / Left / Right / Top / Bottom;
- click a face to align;
- click/drag may orbit;
- `Z` up convention as shown.

### Statistics
Show live:
- Vertices
- Faces/Triangles
- Size X × Y × Z

### Scale ruler
Adapt to camera zoom and display a meaningful world-space length such as `50 mm`.

---

## E. Right inspector panel

Tabs must be in this order:

`Texture | Material | Properties`

### Texture tab

Header area:
`Texture Source`

Sub-tabs:
`Procedural | Image | Library`

Quick source cards in the reference:
- Stone
- Rock
- Bricks
- Scales
- Wood
- Metal
- Fabric
- Custom

Controls:
- Scale
- Depth
- Strength
- Invert
- Advanced expander
- Direction
- Blend Mode
- UV Mapping
- `Apply Texture`

#### Scale
Controls texture frequency/size. Link icon locks U/V or relevant axes.

#### Depth
Controls physical displacement depth when geometric displacement is enabled.

#### Strength
Controls visual/source influence before physical displacement.

#### Invert
Inverts the source height/intensity relationship.

#### Direction
At minimum:
- Surface Normal
- available projection axis directions where meaningful

#### Blend Mode
At minimum:
- Replace/Normal
- Multiply
- Add
- Subtract/Min where technically sensible

Do not expose a mode that has no implemented shader/height behavior.

#### UV Mapping
At minimum preserve/implement supported:
- Triplanar
- Planar
- Cubic
- Cylindrical
- Spherical

Use upstream projection algorithms where they exist.

#### Apply Texture
Commits the current texture/displacement settings as one undoable operation.

### Material tab
Material is viewport/export-capability aware.

Implement:
- Base Color
- Metallic
- Roughness
- Opacity
- Reset Material

Material changes affect viewport PBR preview.

When exporting:
- STL warns that material is not preserved;
- OBJ/3MF preserve material/texture only to the extent the chosen exporter actually supports them.

### Properties tab
For selected object show:
- name;
- visibility/lock;
- source format;
- position X/Y/Z;
- rotation X/Y/Z;
- scale X/Y/Z;
- dimensions X/Y/Z;
- vertex/face count;
- manifold/watertight state;
- normals state;
- applied modifiers summary.

---

## F. Lower texture/material shelf

Match the reference:

Tabs:
`Library | Presets | Recent | Favorites`

Reference preset cards:
- Stone
- Rock
- Scales
- Bricks
- Wood
- Metal
- Fabric
- Carbon
- Leather
- Concrete
- Sand
- Custom / `+`

Required:
- horizontal shelf;
- thumbnail/swatch;
- label;
- selected item blue outline;
- recent list persists;
- favorites persist;
- custom import persists in the library;
- built-in items cannot be accidentally deleted.

---

## G. Quick Actions

Right lower panel, exact visible actions:

Top row:
- `Center Model`
- `Repair Mesh`
- `Make Solid`

Bottom row:
- `Hollow Model`
- `Generate Texture`
- `Prepare for Print`

### Center Model
Center selected model on X/Y and place its lowest Z on the build/grid plane. Preserve geometry scale/orientation.

### Repair Mesh
Same verified repair backend as toolbar Repair, with sensible default options and a review/report before destructive commit.

### Make Solid
Convert applicable non-solid/open geometry into a watertight manifold solid using a robust, documented backend.

Requirements:
- user-adjustable quality/resolution when the method is approximation-based;
- preview;
- triangle-count warning;
- diagnostics after operation;
- undo.

Do not claim exact B-Rep reconstruction if the implementation is mesh/voxel/manifold based.

### Generate Texture
Open a procedural texture generator integrated with the Texture Source system.

At minimum support:
- noise;
- ridges;
- cells/Voronoi-like pattern;
- waves;
- checker/brick-like pattern;
- seed;
- frequency;
- contrast;
- blur/smoothing;
- invert;
- save to library.

Generated textures must work offline.

### Prepare for Print
This is a guided mesh-preparation workflow, not a slicer.

Run/check:
1. model dimensions and units;
2. place/center on build plane;
3. non-manifold/open edges;
4. degenerate geometry;
5. normals;
6. watertight/solid state;
7. minimum wall/thickness warnings where calculable;
8. active export profile;
9. final STL/3MF/OBJ export choice.

Never claim to generate G-code.
Do not silently modify the model without showing the proposed action/result.

---

## H. Status bar

Match the reference placement.

Left:
- Mesh Me360/home glyph
- state text such as `Ready`

Right:
- issue indicator, e.g. green `No issues`, warning count, or error state
- unit selector, default `Metric (mm)`

Units:
- internal canonical geometry units remain millimetres unless the imported format explicitly supplies units and conversion is required;
- display selector can support Metric (mm) and Imperial (in);
- changing display units must not silently rescale geometry.

---

## I. Multi-object scene requirement

Boolean, Split, Combine and the reference Project tree require a project capable of more than one model object.

Implement a Mesh Me360 scene/document layer that can own multiple mesh objects without rewriting the upstream geometry algorithms.

Recommended principle:
- scene/document management wraps the preserved engine;
- each selected object can be passed through the existing upstream texture/displacement/refinement code;
- C# host does not become a duplicate geometry engine;
- web engine remains authoritative for geometry and viewport state.

All operations that materially change geometry must participate in Undo/Redo.

---

## J. `.mm360` project format

Primary save format: `.mm360`.

Use a versioned, self-contained project container capable of preserving:
- scene objects;
- object transforms;
- embedded/reference texture assets;
- active material settings;
- modifier state needed to restore the project;
- measurements;
- export profiles;
- unit/display settings;
- application/project format version.

A ZIP-style documented container is acceptable if consistent with architecture.

Keep a migration/open path for `.bumpmesh` projects. Do not obscure upstream provenance.

---

## K. Responsiveness and processing

Long operations (repair, boolean, make solid, hollow, heavy displacement, subdivision, export):
- must not freeze the visible application for extended periods;
- show operation status/progress;
- use a safe worker/Web Worker path where applicable;
- provide Cancel only if cancellation leaves valid state;
- disable conflicting commands while a transaction is running;
- preserve the last valid model if an operation fails.

---

## L. No-placeholder rule

The milestone FAILS if any visible feature:
- has no handler;
- only displays “coming soon”;
- only writes to log/console;
- opens an empty panel;
- returns a hard-coded fake result;
- uses a screenshot as the actual UI;
- permanently remains disabled because implementation was skipped.

State-based disabled controls are correct. Examples:
- Boolean disabled until 2 suitable objects are selected.
- Hollow disabled with no model selected.
- Apply Texture disabled with no source/model.
