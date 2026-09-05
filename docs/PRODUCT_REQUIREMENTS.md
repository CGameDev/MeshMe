# Mesh Me360 — Product Requirements

## 1. Product statement

Mesh Me360 is an offline-first Windows 10/11 x64 desktop application for loading 3D models, applying displacement textures, masking surfaces, previewing the result, diagnosing mesh quality, simplifying/refining the mesh, and exporting printable geometry.

The desktop product must retain the capability and mesh-processing behavior of the pinned BumpMesh/stlTexturizer baseline while presenting a Windows desktop workflow modeled after the familiar spatial organization of Fusion 360.

## 2. Supported operating environment

- Windows 10 22H2 x64 or newer supported Windows 10 servicing equivalent
- Windows 11 x64
- Per-user install is the default
- .NET 8 desktop application
- Hardware-accelerated WebGL/WebView2 where supported
- CPU fallback behavior is limited to what WebView2/Three.js already supports; do not create a second renderer

## 3. Required model formats

### Import
- STL: binary and ASCII
- OBJ
- 3MF
- STEP / STP through the pinned upstream meshStep path
- `.bumpmesh` project files

### Export
- binary STL
- 3MF
- `.bumpmesh` project files

Do not claim support for additional formats until they are implemented and tested.

## 4. Required upstream feature parity

Preserve all capabilities from the pinned baseline, including:

- 24 built-in seamless textures from upstream
- custom image displacement texture
- texture blur/smoothing
- projection modes: Triplanar, Cubic, Cylindrical, Spherical, Planar XY, Planar XZ, Planar YZ
- U/V scaling and lock behavior
- U/V offsets
- texture rotation
- cubic/cylindrical seam controls
- cylindrical cap angle
- amplitude control
- symmetric displacement
- real-time GPU displacement preview
- amplitude overlap warning
- top/bottom angle masking
- face include/exclude painting
- brush radius behavior
- bucket fill by dihedral-angle threshold
- erase behavior
- selectable smooth mask transition curves
- adaptive subdivision with sharp-crease handling
- QEM decimation with upstream protections/checks
- mesh diagnostics
- open-edge/shell diagnostics and overlays
- 10M-triangle subdivision safety cap unless explicitly changed by an upstream-compatible decision
- orbit/pan/zoom
- wireframe
- mesh information
- grid and XYZ axes
- Place on Face
- project save/load
- undo/redo
- part rotation gizmo
- multilingual UI from upstream baseline

## 5. Desktop document model

Only one active document is required for v1.0. Multiple independent application windows may be supported later but are not required.

### Document states
- `NoDocument`
- `Loading`
- `ReadyClean`
- `ReadyDirty`
- `Processing`
- `Saving`
- `Exporting`
- `ErrorRecoverable`

Commands must enable/disable according to state. Long processing must not allow conflicting destructive commands.

## 6. File menu requirements

Required commands:

- New
- Open Model…
- Open Project…
- Save Project
- Save Project As…
- Export STL…
- Export 3MF…
- Recent Files
- Exit

### New
If the current document is dirty, prompt Save / Don't Save / Cancel before clearing it.

### Open
If the current document is dirty, prompt Save / Don't Save / Cancel before replacing it.

### Save
If the document has no project path, behave as Save Project As.

### Export
Export is separate from project save and must not clear the document dirty flag.

## 7. Recent files

Store recent documents in `%APPDATA%\MeshMe360\recent.json`.

Rules:
- maximum 20 entries
- most recent first
- include both models and `.bumpmesh` projects
- de-duplicate by normalized full path, case-insensitive
- missing files remain visible but marked `Missing` until the user selects them or clears the list
- selecting a missing entry offers Remove From Recent
- File > Recent Files includes `Clear Recent Files`
- never upload recent-file data

## 8. Windows file association behavior

### Owned association
- `.bumpmesh`
- ProgID: `MeshMe360.Project`
- display text: `Mesh Me360 Project`

### Open-with registration only
- `.stl`
- `.obj`
- `.3mf`
- `.step`
- `.stp`

Mesh Me360 must not silently become the default application for the shared 3D model formats.

## 9. Drag and drop

Accept one file at a time in v1.0.

If multiple files are dropped:
- do not silently choose one;
- show a concise message that one model/project can be opened at a time.

Accepted drop types match the supported import/project formats.

## 10. Texture Library Manager

### Built-in library
- retain all packaged upstream textures
- built-ins are read-only
- built-ins may be searched, filtered, tagged by built-in metadata, and favorited

### User library
Storage root: `%APPDATA%\MeshMe360\Textures\`

Index: `%APPDATA%\MeshMe360\Textures\library.json`

Each custom entry must contain:
- stable GUID id
- display name
- source filename
- managed filename
- import timestamp UTC
- favorite boolean
- user tags array
- image width/height when available
- thumbnail filename

Required operations:
- Import Texture…
- Rename
- Add/remove tags
- Favorite/unfavorite
- Search
- Filter Built-in / Custom / Favorites
- Delete custom texture
- Add current project texture to library

Delete removes only the managed library copy and index entry. It must not modify source files outside the Mesh Me360 library.

## 11. Autosave

Autosave is required and enabled by default.

Rules:
- interval: every 120 seconds
- autosave only if document is dirty
- do not start a new autosave while one is already running
- do not autosave during active export or destructive mesh processing
- schedule the next autosave after processing completes if the document remains dirty
- autosave must not alter the explicit project path or clear dirty state
- autosave failures are logged and shown non-modally; they must not close the document

Retention:
- maximum 5 recovery snapshots per active document identity
- maximum 10 document recovery sets globally
- recovery sets older than 30 days may be pruned after confirming they are not the current unclean session

Storage root:
`%LOCALAPPDATA%\MeshMe360\Recovery\`

## 12. Crash/session recovery

Session marker:
`%LOCALAPPDATA%\MeshMe360\Session\active-session.json`

Create/update it during application startup before the user begins editing.

A clean shutdown must mark/remove the active session only after normal close logic completes.

On next startup, if an unclean session is detected and valid recovery data exists, display the recovery dialog before creating a new blank document.

Recovery dialog actions:
- Recover Selected
- Open Mesh Me360 Without Recovering
- Delete Selected Recovery

Each recovery row shows:
- project/model display name
- original path if known
- autosave timestamp
- snapshot size
- Mesh Me360 version that created it

Recovering creates a dirty document. It must not overwrite the original project until the user explicitly saves.

## 13. Settings

Settings file:
`%APPDATA%\MeshMe360\settings.json`

Required settings:
- Theme: System / Light / Dark
- Automatically check for updates: On by default
- Autosave: On by default
- Autosave interval UI may display 2 minutes in v1.0 but the supported v1.0 interval is fixed at 120 seconds
- Confirm before deleting custom textures: On by default
- Restore last viewport preferences where upstream state supports it
- Language: Auto plus all upstream-supported languages

Use a versioned settings schema and tolerate unknown future keys.

## 14. Themes

- System is default on first launch
- user selection persists
- changing theme must not require restart
- both host chrome and web UI must switch together
- high contrast should remain usable even if a dedicated high-contrast theme is not implemented

## 15. Keyboard shortcuts

Minimum required shortcuts:

- Ctrl+N — New
- Ctrl+O — Open Model/Project chooser
- Ctrl+S — Save Project
- Ctrl+Shift+S — Save Project As
- Ctrl+Z — Undo
- Ctrl+Y — Redo
- Ctrl+Shift+Z — Redo alternative where upstream uses it
- Ctrl+, — Preferences
- F1 — Help/Documentation
- F11 — Toggle application fullscreen
- Esc — cancel/exit active tool or close transient panel when safe

Do not break upstream viewport mouse controls.

## 16. Application update behavior

- stable update source: GitHub Releases for `CGameDev/MeshMe`
- default automatic check: once per 24 hours maximum
- no background update check before the main window is usable
- manual Help > Check for Updates ignores the 24-hour throttle
- offline/network/update service failure is non-blocking
- never auto-install without a visible user action in v1.0
- update UI offers Install / Later
- preserve settings, recent files, textures, recovery data, and user projects across update

## 17. Logs

Storage:
`%LOCALAPPDATA%\MeshMe360\Logs\`

Requirements:
- structured text or JSON lines
- UTC timestamps plus local-readable display where shown in UI
- rolling log files
- retain 10 most recent log files or 30 days, whichever is less aggressive
- do not log raw model geometry or texture binary contents
- path names may appear when necessary for troubleshooting
- log host/web bridge errors with request id

## 18. Diagnostics

Help > Diagnostics must show/copy:

- Mesh Me360 version
- commit/build identifier when available
- Windows version
- .NET version
- WebView2 runtime version
- WebGL renderer/vendor strings where available
- active model format
- triangle count
- bounding dimensions
- current projection mode
- whether 3D preview is enabled
- last engine error summary
- log folder path

Provide buttons:
- Copy Diagnostics
- Open Log Folder

Do not include model contents in copied diagnostics.

## 19. Performance and responsiveness

- viewport interaction must remain on the existing Three.js/WebGL path
- expensive mesh operations must keep their existing worker/async behavior where upstream provides it
- the WPF UI thread must not synchronously process mesh data
- progress stages must remain visible for subdivision, displacement, decimation, and writing/export
- cancellation is required only where upstream already supports a safe cancellation path; do not add unsafe mid-algorithm cancellation by guessing

## 20. Accessibility and scaling

- keyboard focus indicators on interactive controls
- tooltips for icon-only commands
- semantic labels for screen readers where practical in the web UI
- no essential status conveyed by color alone
- support 100%, 125%, 150%, 175%, and 200% Windows scaling
- minimum supported working window: 1280x720
- target reference layout: 1600x900

## 21. About screen

Help > About must include:

- Mesh Me360 name/version
- project direction attribution: Brian Hinds / CGameDev
- upstream attribution: BumpMesh/stlTexturizer by CNC Kitchen (Stefan Hermann) and contributors
- GNU AGPL v3 notice
- View License
- View Source Code
- Third-Party Notices
- repository link

## 22. Explicit non-cloud behavior

Mesh editing, previewing, project save/load, texture library use, diagnostics, and export must work without an Internet connection.

The application must not upload user models, textures, project files, diagnostics, or recent-file information.

The only planned network feature is update checking/downloading and explicit user-opened documentation/source links.
