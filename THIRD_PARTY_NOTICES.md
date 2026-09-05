# Mesh Me360 — Third-Party Notices

This file is a living dependency/provenance manifest. Codex must update it whenever a third-party dependency or vendored asset is added, removed, or version-changed.

## Upstream application

### BumpMesh / stlTexturizer

- Copyright: CNC Kitchen (Stefan Hermann) and contributors
- Source: `https://github.com/CNCKitchen/stlTexturizer`
- Mesh Me360 pinned baseline: `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1`
- Baseline version: v1.2.0
- License: GNU Affero General Public License v3.0
- Role: upstream mesh-processing, 3D viewer, texture/mask/subdivision/displacement/decimation/diagnostics/project engine

Upstream source notices must remain intact when imported into `src/MeshMe360.EngineWeb/upstream/`.

## Dependencies identified by the pinned upstream project

The pinned upstream project documentation/code must be treated as the authoritative source when confirming exact dependency files and versions during Milestone 0/1.

### Three.js

- Project: Three.js
- Upstream baseline documented version: `0.170.0`
- Source: `https://threejs.org/` / official Three.js repository/package
- License: MIT
- Role: WebGL rendering, scene management, materials, loaders, controls, line rendering helpers

Required local release modules include only the pieces actually used by the pinned engine, such as core Three.js plus relevant addons/loaders/controls/line modules.

### fflate

- Project: fflate
- Upstream baseline documented version: `0.8.2`
- Source: `https://github.com/101arrowz/fflate`
- License: MIT
- Role: ZIP compression/decompression for 3MF/project-related workflows

### meshStep

- Project: meshStep
- Upstream source: `https://github.com/CNCKitchen/meshStep`
- Upstream BumpMesh baseline references meshStep for STEP/STP tessellation
- Version/source revision: **Codex must confirm from the pinned upstream code before vendoring**
- License: **Codex must inspect and record the actual license before redistribution**
- Role: STEP/STP browser-side tessellation/worker path

Do not write an assumed version/license into release metadata. Inspect the pinned upstream source first.

## Desktop dependencies planned for Mesh Me360

The following are architecture selections. Codex must record the exact installed package/runtime versions and licenses after project creation.

### Microsoft Edge WebView2

- Components: Microsoft WebView2 .NET SDK + WebView2 Fixed Version Runtime x64 for releases
- Source: Microsoft
- Role: embedded Chromium/WebGL runtime for the Mesh Me360 web engine
- Version: pinned during implementation/release engineering
- Redistribution terms: confirm and comply with Microsoft WebView2 redistribution terms when packaging the fixed runtime

### Velopack

- Project: Velopack
- Role: Windows packaging and update orchestration
- Version: pin during implementation
- License: inspect actual package/repository license before first release and record it here

## Future .NET / test / logging dependencies

For every new package, append an entry containing:

```text
Name:
Version:
Source:
License:
Role:
Redistributed in release? yes/no
License/notice file included where required:
```

## Asset rules

- Do not copy Autodesk/Fusion 360 icons, screenshots, logos, or proprietary UI assets.
- Mesh Me360 SVG mockups in `docs/ui/` are original project reference assets.
- Built-in texture assets imported from BumpMesh remain subject to the upstream repository's licensing/notices; preserve provenance.
- Do not add third-party texture/icon packs without source and license documentation.

## Release review requirement

Before v1.0, this file must contain exact versions/licenses for every redistributed dependency. Placeholder wording such as `confirm later`, `latest`, or `unknown` is not acceptable in a release candidate.
