# Mesh Me360 — UI Specification

This is the canonical UI contract for v1.0. The SVGs in `docs/ui/` visualize this specification. When the mockup and prose differ, this document wins unless the repository owner explicitly changes it.

## 1. Design intent

Mesh Me360 should feel immediately understandable to users familiar with Fusion 360 because it uses the same **general spatial model** of:

- application commands at top,
- grouped workspace tools below,
- document/model hierarchy on the left,
- large 3D viewport in the center,
- view orientation control in the viewport corner,
- tool/properties editing on the right,
- status/history information at the bottom.

It must **not** copy Autodesk logos, Fusion trademarks, screenshots, proprietary icons, exact icon drawings, or branded assets.

## 2. Reference size

Primary reference: **1600 × 900**.

Minimum supported usable window: **1280 × 720**.

Panels may collapse at smaller widths but required commands must remain reachable.

## 3. Global layout

At 1600×900 use these reference regions:

```text
┌────────────────────────────────────────────────────────────────────────────┐
│ A. Menu / title row                                         36 px          │
├────────────────────────────────────────────────────────────────────────────┤
│ B. Workspace + command bar                                  88 px          │
├───────────────┬───────────────────────────────────────┬────────────────────┤
│ C. Browser    │ D. 3D Viewport                        │ E. Properties      │
│ 250 px        │ flexible                              │ 320 px             │
│               │                                       │                    │
│               │                                       │                    │
│               │                                       │                    │
├───────────────┴───────────────────────────────────────┴────────────────────┤
│ F. History / status strip                                   54 px          │
└────────────────────────────────────────────────────────────────────────────┘
```

The viewport always receives remaining width and height.

## 4. A — Menu/title row

Height: 36 px reference.

Left to right:

1. Mesh Me360 application icon
2. application menus:
   - File
   - Edit
   - View
   - Help
3. document title in available center area
4. dirty marker `●` or `*` next to document title when unsaved
5. right-side quick controls:
   - Undo
   - Redo
   - theme indicator/menu optional shortcut
   - standard Windows minimize/maximize/close handled by host chrome

### File menu
- New
- Open Model…
- Open Project…
- separator
- Save Project
- Save Project As…
- separator
- Export STL…
- Export 3MF…
- separator
- Recent Files >
- separator
- Exit

### Edit menu
- Undo
- Redo
- separator
- Preferences…

### View menu
- Fit Model
- Home View
- Front
- Back
- Left
- Right
- Top
- Bottom
- separator
- Perspective / Orthographic toggle if upstream supports both; otherwise omit rather than invent
- Wireframe
- Grid
- Axes
- Browser Panel
- Properties Panel
- Fullscreen

### Help menu
- Documentation
- Keyboard Shortcuts
- Diagnostics
- Check for Updates
- separator
- About Mesh Me360

## 5. B — Workspace and command bar

Height: 88 px reference.

### Workspace tabs
At the upper portion of this region:
- **DESIGN**
- **TEXTURE**
- **MESH**
- **INSPECT**

Active tab uses a strong accent underline and slightly bolder text.

The tabs change command groups and the default right-side panel content; they do not create separate documents.

### DESIGN command groups

**FILE / SETUP**
- Open Model
- Place on Face
- Reset Transform

**TRANSFORM**
- Rotate
- Fit View
- Home View

**DISPLAY**
- Wireframe
- Grid
- Axes

No new CAD solid-modeling tools are implied by the DESIGN name.

### TEXTURE command groups

**LIBRARY**
- Texture Library
- Import Texture
- Favorites

**PROJECTION**
- Triplanar
- Cubic
- Cylindrical
- Spherical
- Planar

Projection commands may be a dropdown/split-button but current projection must be visible without opening a menu.

**MASK**
- Paint Exclude
- Paint Include
- Bucket Fill
- Erase
- Clear Mask

**PREVIEW**
- 3D Preview toggle
- texture visibility/preview control if upstream exposes one

### MESH command groups

**REFINE**
- Subdivision settings/tool

**DISPLACE**
- Apply/preview displacement controls

**SIMPLIFY**
- Decimation settings/tool

**OUTPUT**
- Export STL
- Export 3MF

### INSPECT command groups

**ANALYZE**
- Mesh Diagnostics
- Open Edges overlay
- Shells overlay
- Mesh Info

**DISPLAY**
- Wireframe
- Grid
- Axes

**DIAGNOSTICS**
- Application Diagnostics shortcut

Do not invent CAD measurements that do not exist in the engine. A measurement tool can be a future feature only after explicit scope approval.

## 6. C — Browser panel

Reference width: 250 px.
Minimum practical expanded width: 220 px.
Collapsible.

Header:
- `BROWSER`
- collapse button

Tree structure:

```text
Document Settings
Named Views
  Home
  Front
  Back
  Left
  Right
  Top
  Bottom
Origin
  X Axis
  Y Axis
  Z Axis
Model
  <loaded model filename>
Textures
  Active: <texture name>
Masks
  Excluded Faces
  Included Faces
Diagnostics
  Open Edges
  Shells
History
```

Rules:
- hide/disable nodes that are meaningless before a model loads;
- `Textures` opens/focuses texture properties, not a second viewport;
- diagnostic nodes toggle existing overlays;
- `History` is a logical presentation of existing undo/redo state, not a parametric CAD timeline.

Do not falsely imply editable CAD bodies/components that the mesh engine does not actually support.

## 7. D — 3D viewport

The existing Three.js viewport remains the central interaction surface.

Required viewport overlays:

### Top-left
- current tool name when a modal editing tool is active
- concise instruction, e.g. `Paint faces to exclude • Shift = erase • Esc = finish`

### Top-right
- original Mesh Me360 orientation cube inspired by common CAD view cubes
- cube faces: TOP, FRONT, RIGHT; adjacent clicks/drag may reach other views
- small Home icon below cube
- do not copy Autodesk ViewCube graphics

### Bottom-center navigation strip
Small icon-only navigation controls with tooltips:
- Orbit
- Pan
- Zoom/Fit
- Home View

Keep mouse controls compatible with upstream orbit/pan/zoom.

### Bottom-left viewport info
Compact live model statistics:
- `Triangles: 124,532`
- `Size: 120.0 × 82.5 × 35.0 mm`

Do not cover the model with a large floating HUD.

## 8. E — Properties / Tool panel

Reference width: 320 px.
Minimum expanded width: 280 px.
Collapsible.

Header text changes by context:
- `PROPERTIES`
- `TEXTURE`
- `MASK`
- `SUBDIVISION`
- `DECIMATION`
- `DIAGNOSTICS`
- `TEXTURE LIBRARY`

The panel is the canonical home of numerical settings. Avoid duplicating sliders in multiple permanent locations.

### No active model
Show:
- short `Open or drop a 3D model to begin.` message
- Open Model button
- supported format list

### Base model properties
Show:
- filename
- format
- dimensions
- triangle count
- shell count when known
- diagnostics summary

### Texture panel order
1. texture thumbnail/name
2. Change Texture / Library
3. Projection mode
4. Scale U
5. Scale V
6. Lock U/V
7. Offset U
8. Offset V
9. Rotation
10. projection-specific controls
11. Texture Smoothing
12. Amplitude
13. Symmetric displacement
14. 3D Preview toggle
15. amplitude overlap warning when triggered

Use the upstream ranges/defaults; do not substitute new numeric ranges.

### Mask panel order
1. Mode: Exclude / Include
2. Tool: Brush / Bucket
3. Brush radius when Brush
4. fill angle threshold when Bucket
5. Top angle mask
6. Bottom angle mask
7. transition curve
8. Clear Mask
9. face counts summary

### Subdivision panel
Expose the exact upstream target edge length/quality controls and warnings.

### Decimation panel
Expose the exact upstream target triangle/count behavior and warnings.

### Diagnostics panel
Show upstream diagnostic results and overlay toggles.

## 9. F — History / status strip

Reference height: 54 px.

### Left
Current state:
- Ready
- Loading…
- Subdividing…
- Applying displacement…
- Decimating…
- Exporting…
- Autosaving…

### Center
Compact history chips for recent meaningful operations where upstream undo history exposes them, e.g.:
- Load
- Texture
- Mask
- Rotate

Do not fabricate fully parametric CAD history.

### Right
- active model triangle count
- memory/performance warning only if an existing/upstream-safe signal exists
- zoom percentage if available

Long-running operation progress may replace the center history area with a progress bar and stage name.

## 10. Texture Library Manager layout

The manager opens in the right panel at normal sizes and may expand to a modal/panel overlay if more width is needed.

Header:
`TEXTURE LIBRARY`

Top controls:
- search field
- filter segmented buttons: All / Built-in / Custom / Favorites
- `Import Texture` button

Texture grid/card:
- square thumbnail
- display name
- favorite star
- Built-in or Custom badge

Context menu for custom item:
- Apply
- Rename
- Add/Edit Tags
- Favorite/Unfavorite
- Delete

Context menu for built-in item:
- Apply
- Favorite/Unfavorite
- no Rename/Delete

Selecting a texture applies/loads it only after an intentional click/double-click or Apply action; hovering must not mutate the model.

## 11. Recovery dialog

Shown before normal document creation only after an unclean session with valid recovery data.

Title: `Recover Mesh Me360 Work`

Body:
`Mesh Me360 did not close normally. A recoverable autosave was found.`

List columns:
- Project / Model
- Last Autosave
- Original Location
- Size

Primary button: `Recover Selected`
Secondary: `Open Without Recovering`
Destructive tertiary: `Delete Selected Recovery`

Do not automatically overwrite the original project.

## 12. Preferences

Use a normal settings dialog/panel with sections:

### General
- Language
- Theme
- Automatically check for updates

### Files
- recent-file management
- default save behavior where needed
- show file association status, not a deceptive “make default” switch for shared formats

### Recovery
- Autosave toggle
- display fixed v1.0 interval: `Every 2 minutes`
- Open Recovery Folder

### Appearance
- Theme System/Light/Dark
- panel density only if implemented globally; do not add unimplemented placeholders

## 13. Color and visual tokens

### Light theme
- app background: `#F3F4F6`
- panel background: `#FFFFFF`
- viewport background: `#E7EAEE` or engine-compatible equivalent
- primary text: `#202124`
- secondary text: `#5F6368`
- borders: `#D6D9DD`
- accent: `#1473E6`
- warning: `#B26A00`
- error: `#B3261E`
- success: `#1B7F3A`

### Dark theme
- app background: `#1E1F22`
- panel background: `#26282C`
- viewport background: `#15171A`
- primary text: `#F1F3F4`
- secondary text: `#B7BBC0`
- borders: `#3A3D42`
- accent: `#4C9AFF`
- warning: `#F0B35A`
- error: `#FF7B72`
- success: `#5FD38D`

These are Mesh Me360 tokens, not Autodesk color assets.

## 14. Typography

Windows UI stack:
`Segoe UI, Arial, sans-serif`

Reference sizes:
- menus: 13 px
- workspace tabs: 12 px semibold uppercase
- command labels: 11–12 px
- panel headers: 11 px semibold uppercase
- standard controls: 13 px
- small metadata: 11 px

Do not require downloading fonts.

## 15. Command icons

Use one consistent icon family:
- Microsoft Fluent system icons where redistribution/use is appropriate, or
- original SVG icons created for Mesh Me360.

Requirements:
- 20–24 px primary toolbar icon
- 16–18 px panel/menu icon
- monochrome/adaptive to theme where possible
- tooltip for every icon-only control

Do not copy Fusion 360 icon files.

## 16. Responsive behavior

### Width 1450 px or greater
- Browser 250 px
- Properties 320 px
- all command groups visible

### Width 1280–1449 px
- Browser 220 px
- Properties 290 px
- toolbar group labels may shorten/collapse into overflow
- viewport remains at least ~650 px when possible

### Below 1280 px
Not a supported target, but app must remain recoverable:
- collapse Browser first
- collapse lower-priority toolbar labels into overflow
- never permanently hide File/Export/Properties access

### Height 720–799 px
- command bar remains fixed
- right panel scrolls internally
- bottom status stays visible

## 17. Processing state behavior

When a long operation is running:
- show stage/progress in bottom strip;
- disable commands that would corrupt current operation;
- keep viewport visible;
- do not display blocking message boxes for ordinary progress;
- show cancel only if the engine has a verified safe cancellation path.

## 18. Empty state

Initial viewport center:

```text
Mesh Me360
Drop a 3D model here
STL • OBJ • 3MF • STEP/STP
[ Open Model ]
```

Right panel repeats supported formats briefly.

Do not show marketing panels or network content in the editing workspace.

## 19. First-run behavior

No mandatory account/login/onboarding.

First launch may show a single dismissible welcome overlay with:
- Open Model
- Open Project
- 3-step explanation: Load → Texture → Export
- link to Help

Skip/close must be permanent unless user reopens Help > Welcome.

## 20. Exact naming

Use these strings consistently:

- `Mesh Me360`
- `Texture Library`
- `3D Preview`
- `Mesh Diagnostics`
- `Place on Face`
- `Subdivision`
- `Decimation`
- `Export STL`
- `Export 3MF`
- `Recover Mesh Me360 Work`

Do not rename the product to MeshMe, MeshMe 360, Mesh Me 360, or BumpMesh in user-visible chrome. Internal namespaces remain `MeshMe360`.
