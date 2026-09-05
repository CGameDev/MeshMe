# AGENTS.md — Mesh Me360 Codex Rules

This file is the primary instruction contract for Codex. Read it completely before changing code.

## 1. Product identity

- Product display name: **Mesh Me360**
- Internal namespace / assembly prefix: `MeshMe360`
- Repository: `CGameDev/MeshMe`
- Target: **Windows 10/11 x64 desktop**
- `360` is branding only. **Do not target Xbox 360, XDK, UWP, WinUI Xbox, or console APIs.**

## 2. Fixed technical architecture

Do not replace this architecture unless the repository owner explicitly changes this file.

- Host: **C# .NET 8 WPF**
- Embedded renderer/app: **Microsoft Edge WebView2**
- Mesh engine: vendored/adapted BumpMesh web engine from the pinned upstream commit in `docs/UPSTREAM_AND_LICENSING.md`
- Rendering: Three.js/WebGL, preserved from upstream
- Runtime must be offline-capable
- End users must not need Node.js, Python, PHP, Docker, a local HTTP server, or a browser tab
- The desktop app must not depend on CDN resources at runtime

## 3. Golden rule: preserve the mesh engine

**Do not rewrite, replace, simplify, or “improve” upstream geometry algorithms merely to make them more idiomatic.**

This includes, but is not limited to:

- mesh import/parsing behavior
- projection mapping
- UV transform logic
- displacement
- symmetric displacement
- adaptive subdivision
- crease handling
- QEM decimation
- boundary/link-condition checks
- normal-flip rejection
- mask painting
- bucket fill
- angle masking
- mask falloff curves
- diagnostics
- mesh export

Allowed changes to upstream engine code are limited to:

1. offline dependency conversion,
2. desktop host integration,
3. UI refactoring needed to implement `docs/UI_SPEC.md`,
4. verified bug fixes with regression tests,
5. performance fixes that preserve output behavior,
6. explicit features defined in this repository.

If an algorithmic change appears necessary, stop that change and document the reason in a new issue or `docs/implementation-notes/`. Do not silently substitute another algorithm.

## 4. No guessing rule

If implementation details are already specified in this repository, follow them exactly.

If a required detail is missing:

1. inspect the pinned upstream implementation,
2. inspect existing tests/documentation,
3. choose the smallest behavior-preserving implementation,
4. document the decision in `docs/implementation-notes/DECISIONS_LOG.md`,
5. do not invent a product feature, file format, algorithm, server dependency, or UX flow that contradicts these documents.

Do not use “best guess” as a substitute for inspecting upstream code.

## 5. UI rules

- Follow `docs/UI_SPEC.md` and the SVG mockups under `docs/ui/`.
- Use Fusion 360 only as a familiarity/layout reference.
- Do **not** copy Autodesk/Fusion trademarks, logos, proprietary icons, screenshots, or exact branded assets.
- Use original Mesh Me360 branding and open-source/system icons.
- Preserve both light and dark themes.
- The central viewport always receives the most screen area.
- Tool parameters belong in the right Properties panel; model/document hierarchy belongs in the left Browser.
- Do not move core commands to hidden menus if the UI spec places them in the top command bar.

## 6. Required feature completeness

Nothing in `docs/FEATURE_MATRIX.md` is optional for v1.0, including items that were previously described as optional ideas:

- recent files
- native Windows file behavior
- file associations
- fully offline runtime
- installer
- auto updater
- autosave
- crash/session recovery
- Texture Library Manager

A feature may be disabled by a user preference where specified, but it must exist and be tested.

## 7. Source tree contract

Create and keep this structure unless a file-level implementation detail requires a subfolder:

```text
MeshMe/
├─ AGENTS.md
├─ README.md
├─ LICENSE
├─ THIRD_PARTY_NOTICES.md
├─ docs/
├─ src/
│  ├─ MeshMe360.Desktop/            # WPF executable and host services
│  ├─ MeshMe360.EngineWeb/          # desktop web app and preserved upstream engine
│  │  ├─ upstream/                  # imported pinned BumpMesh source
│  │  ├─ app/                       # Mesh Me360 UI/adaptation layer
│  │  └─ vendor/                    # local runtime JS dependencies
│  ├─ MeshMe360.Core/               # host domain models/contracts/settings
│  └─ MeshMe360.Updater/            # update integration if separated from host
├─ tests/
│  ├─ MeshMe360.Desktop.Tests/
│  ├─ MeshMe360.Core.Tests/
│  └─ engine-web/                   # JS/browser regression fixtures/tests
├─ build/
└─ .github/workflows/
```

Do not place generated binaries, WebView2 runtime payloads, installers, or large test outputs in Git unless explicitly required.

## 8. Native/web separation

### .NET host owns

- window lifecycle
- startup arguments / Explorer file launches
- recent-file paths
- Windows file associations
- application settings storage
- logs
- OS theme/DPI integration
- update checks/install flow
- installer/release integration
- crash session marker
- opening local files from Explorer/recent-file list
- download/save destination integration

### Web engine owns

- 3D rendering
- model geometry
- texture mapping
- mask editing
- subdivision/decimation/displacement
- mesh diagnostics overlays
- project state serialization/deserialization
- undo/redo for model edits
- viewport interaction

### Shared responsibilities

- autosave/recovery: web engine serializes project state; desktop host coordinates session/recovery lifecycle as described in `docs/ARCHITECTURE.md`
- theme: host reports OS preference; user override is persisted by Mesh Me360

Do not duplicate geometry state in C#.

## 9. Upstream import rules

Before importing upstream source:

1. read `docs/UPSTREAM_AND_LICENSING.md`;
2. import exactly the pinned commit first;
3. record the baseline SHA;
4. preserve copyright/SPDX/license headers;
5. keep upstream code visibly separated under `src/MeshMe360.EngineWeb/upstream/`;
6. do not remove CNC Kitchen attribution;
7. do not rename upstream history internally in a way that obscures provenance.

## 10. Offline requirement

A release build must work with networking disabled.

Runtime code must not fetch from:

- jsDelivr
- unpkg
- npm CDNs
- Google Fonts
- GitHub raw content
- Autodesk services
- any API merely to render/edit/export a model

Vendored runtime dependencies must be pinned and documented in `THIRD_PARTY_NOTICES.md`.

Auto-update is the only feature expected to use the network. Update failure/offline state must be non-blocking.

## 11. File behavior

Required associations:

- `.bumpmesh` → Mesh Me360 project, owned association/ProgID

Required “Open with Mesh Me360” registration without taking default ownership:

- `.stl`
- `.obj`
- `.3mf`
- `.step`
- `.stp`

Do not silently replace the user's existing default app for common 3D formats.

## 12. Autosave and crash recovery

Follow the exact timing/retention rules in `docs/PRODUCT_REQUIREMENTS.md` and `docs/ARCHITECTURE.md`.

Do not mark a session as clean until shutdown completes normally.
Do not delete recovery data until the user explicitly discards it or a successful save/recovery resolution makes it obsolete.

## 13. Testing gate

Do not call a milestone complete merely because it compiles.

Each milestone must satisfy its checkpoint in `docs/CODEX_MASTER_PLAN.md` and corresponding rows in `docs/TEST_PLAN.md`.

For mesh-engine changes, add regression coverage using deterministic fixtures. Compare at minimum:

- triangle count
- bounding box
- vertex/triangle validity
- export success
- expected diagnostics
- representative geometry samples or hashes where stable

## 14. Commit behavior

Prefer milestone-sized commits with descriptive messages.

Before moving to the next milestone:

- build affected projects,
- run relevant tests,
- update checkpoint documentation,
- leave the repository runnable.

Do not stack multiple major architectural rewrites into one unverified commit.

## 15. Definition of “complete”

The project is not complete until the packaged Windows release—not merely a development build—passes the v1.0 acceptance requirements in `README.md` and `docs/TEST_PLAN.md`.
