# Mesh Me360

**Mesh Me360** is a Windows 10/11 x64 desktop application for applying displacement textures to 3D meshes, previewing the result in real time, masking surfaces, diagnosing mesh problems, and exporting printable textured models.

The application is intentionally based on the proven **BumpMesh / stlTexturizer** mesh-processing engine by CNC Kitchen (Stefan Hermann), but is being adapted into a complete offline-first Windows desktop experience with a Fusion 360-inspired workflow and layout.

> **Important:** “360” is part of the Mesh Me360 product name. This is a Windows desktop application, **not an Xbox 360 application**.

## Upstream baseline

- Upstream repository: `https://github.com/CNCKitchen/stlTexturizer`
- Product name upstream: BumpMesh
- Pinned baseline commit: `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1`
- Baseline release represented by that commit: `v1.2.0`
- Upstream license: GNU AGPL v3.0

Codex must preserve upstream copyright/license notices in any imported or modified upstream source files and must follow `docs/UPSTREAM_AND_LICENSING.md` before importing code.

## Target platform

- Windows 10/11 x64
- .NET 8
- WPF host
- Microsoft Edge WebView2
- Local/offline HTML/CSS/JavaScript/Three.js engine
- No Node.js, Python, local web server, or Internet connection required by end users
- Release installer must include everything required to run offline

## Product scope

Mesh Me360 must include **all** functionality already present in the pinned BumpMesh baseline plus the complete Windows feature set below.

| Capability | Mesh Me360 requirement |
|---|---|
| STL import | Required |
| OBJ import | Required |
| 3MF import | Required |
| STEP/STP import | Required |
| Built-in displacement textures | Required; preserve upstream library |
| Custom displacement textures | Required |
| Triplanar / Cubic / Cylindrical / Spherical / Planar projections | Required; preserve upstream behavior |
| Adaptive subdivision | Required; preserve upstream algorithm |
| QEM decimation | Required; preserve upstream algorithm |
| Surface mask painting / bucket fill / erase | Required |
| Angle masking and smooth mask falloff | Required |
| GPU 3D displacement preview | Required |
| Mesh diagnostics | Required |
| Place on Face / rotation controls | Required |
| Binary STL export | Required |
| 3MF export | Required |
| `.bumpmesh` project save/load compatibility | Required |
| Drag and drop | Required |
| Light / Dark theme | Required |
| Multilingual UI | Required; preserve upstream languages |
| Native Windows open/save behavior | Required |
| Recent files/projects | Required |
| Windows file associations | Required |
| Fully offline runtime | Required |
| Windows installer | Required |
| Automatic update system | Required |
| Autosave | Required |
| Crash/session recovery | Required |
| Texture Library Manager | Required |
| Structured logs and diagnostics | Required |
| Keyboard shortcuts | Required |
| High-DPI / multi-monitor support | Required |

## UI direction

The user interface must be **Fusion 360-inspired**, not a copy of Autodesk branding, icons, logos, or proprietary assets. The goal is familiarity of layout and workflow:

- Application/menu row at the top
- Workspace tabs/tool groups directly below
- Left-side model/document Browser tree
- Large central 3D viewport
- View cube in the upper-right of the viewport
- Context-sensitive Properties/Tool panel on the right
- Bottom history/status region with mesh statistics
- Clear grouped commands for Design, Texture, Mesh, Inspect, and Export workflows

Canonical visual references for implementation are stored in `docs/ui/` and the binding rules are defined in `docs/UI_SPEC.md`.

## Architecture principle

**Do not rewrite the BumpMesh geometry algorithms.**

The desktop application must preserve the upstream JavaScript/Three.js mesh engine and place it inside a Windows WPF/WebView2 shell. Desktop-specific capabilities belong in the .NET host and must communicate with the web engine through a small, documented bridge.

See:

- `AGENTS.md` — mandatory Codex execution rules
- `docs/CODEX_MASTER_PLAN.md` — implementation order and checkpoints
- `docs/PRODUCT_REQUIREMENTS.md` — exact product requirements
- `docs/ARCHITECTURE.md` — fixed architecture and directory layout
- `docs/UI_SPEC.md` — exact desktop UI layout and behavior
- `docs/FEATURE_MATRIX.md` — feature-by-feature acceptance matrix
- `docs/TEST_PLAN.md` — validation requirements
- `docs/RELEASE_AND_INSTALLER.md` — packaging, updates, associations, offline runtime
- `docs/UPSTREAM_AND_LICENSING.md` — upstream import and AGPL requirements
- `docs/DECISIONS_AND_NON_GOALS.md` — decisions Codex must not reinterpret

## Definition of done for v1.0

Mesh Me360 v1.0 is complete only when:

1. Every feature in `docs/FEATURE_MATRIX.md` is implemented and verified.
2. Existing upstream mesh-processing results remain functionally equivalent for the same source model/settings.
3. The application starts and performs core editing/export tasks with the network disabled.
4. A clean Windows machine can install and launch the packaged application without separately installing development tools.
5. `.bumpmesh` files open in Mesh Me360 from Explorer.
6. Autosave and unclean-exit recovery are verified.
7. Automatic update logic is implemented and can be tested against a non-production feed/release.
8. UI matches the provided Mesh Me360 mockups and layout specification.
9. AGPL and third-party notices are visible from **Help > About > Licenses / Source Code**.
10. Source, build instructions, and release process are reproducible from this repository.

## Developer attribution

Mesh Me360 project direction: **Brian Hinds / CGameDev**.

Upstream BumpMesh / stlTexturizer engine: **CNC Kitchen (Stefan Hermann) and contributors**. See the upstream repository and licensing documentation for details.
