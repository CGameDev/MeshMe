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

Imported unchanged on 2026-09-05 into `src/MeshMe360.EngineWeb/upstream/`.
All 117 original blobs and the complete root `LICENSE` are verified by
`build/verify-upstream.py`. Original notices, 24 textures and thumbnails are retained.
Combined Mesh Me360 source uses AGPL-3.0-only.

## Dependencies identified by the pinned upstream project

The pinned upstream project documentation/code must be treated as the authoritative source when confirming exact dependency files and versions during Milestone 0/1.

### Three.js

- Project: Three.js
- Upstream baseline documented version: `0.170.0`
- Source: `https://threejs.org/` / official Three.js repository/package
- License: MIT
- Role: WebGL rendering, scene management, materials, loaders, controls, line rendering helpers
- Vendored: `src/MeshMe360.EngineWeb/vendor/three-0.170.0/`; actual npm LICENSE
  inspected and included. Copyright 2010–2024 three.js authors. Runtime core and
  the recursive closure of required addons are included unchanged.

Required local release modules include only the pieces actually used by the pinned engine, such as core Three.js plus relevant addons/loaders/controls/line modules.

### fflate

- Project: fflate
- Upstream baseline documented version: `0.8.2`
- Source: `https://github.com/101arrowz/fflate`
- License: MIT
- Role: ZIP compression/decompression for 3MF/project-related workflows
- Vendored: `src/MeshMe360.EngineWeb/vendor/fflate-0.8.2/`; actual npm LICENSE
  inspected and included. Copyright 2023 Arjun Barrett. Browser ESM unchanged.

### meshStep

- Project: meshStep
- Upstream source: `https://github.com/CNCKitchen/meshStep`
- Upstream BumpMesh baseline references meshStep for STEP/STP tessellation
- Version/source revision: `meshstep@0.1.0`, confirmed in pinned `js/stepWorker.js`
- License: AGPL-3.0-only, confirmed from npm package.json, SPDX source headers
  and the complete included LICENSE (not MIT).
- Role: STEP/STP browser-side tessellation/worker path
- Vendored: `src/MeshMe360.EngineWeb/vendor/meshstep-0.1.0/`; unmodified compiled
  JavaScript and corresponding TypeScript source, README, package metadata and license.
- Zero runtime dependencies and no WASM payload in this pinned package.

Do not write an assumed version/license into release metadata. Inspect the pinned upstream source first.

## Desktop dependencies planned for Mesh Me360

The following are architecture selections. Codex must record the exact installed package/runtime versions and licenses after project creation.

### Microsoft Edge WebView2

- Components: Microsoft WebView2 .NET SDK + WebView2 Fixed Version Runtime x64 for releases
- Source: Microsoft
- Role: embedded Chromium/WebGL runtime for the Mesh Me360 web engine
- SDK version: `Microsoft.Web.WebView2` 1.0.4191.47 (NuGet lock file included)
- SDK license: BSD-style three-clause Microsoft license inspected in the actual
  package. Complete LICENSE and NOTICE included in `build/licenses/` and output
  `Licenses/`. SDK assemblies and native loader are redistributed in the M1 test build.
- Fixed Runtime: not bundled at M1; pinning/packaging is the M8 requirement.
  M1 uses a local `WebView2FixedRuntime/` folder when supplied, otherwise an already
  installed runtime. The host never downloads/bootstrap-installs a runtime.
- Redistribution terms: confirm and comply with Microsoft WebView2 redistribution terms when packaging the fixed runtime

### Velopack

- Project: Velopack
- Role: Windows packaging and update orchestration
- Version: pin during implementation
- License: inspect actual package/repository license before first release and record it here

## Future .NET / test / logging dependencies

### .NET 8 build targeting packs

- Names: Microsoft.NETCore.App.Ref, Microsoft.WindowsDesktop.App.Ref,
  Microsoft.AspNetCore.App.Ref, Microsoft.NETCore.App.Host.win-x64
- Version in M0 verification: 8.0.30, resolved by SDK 10.0.400
- Source: official Microsoft packages on https://api.nuget.org/v3/index.json
- License: MIT, .NET Foundation and contributors; inspected package license text
- Role: compile the .NET 8 WPF/Core projects and provide the Windows app host
- Reference packs are development-only; the application host is output with the build.
- Runtime distribution and its complete notices are part of M8 release engineering.

### .NET 8 self-contained M1 test build

- Microsoft.NETCore.App.Runtime.win-x64 and Microsoft.WindowsDesktop.App.Runtime.win-x64
- Version: 8.0.30 in this verified handoff; source: official Microsoft NuGet packages.
- MIT licenses inspected; .NET Foundation and contributors.
- Runtime binaries are in ignored test output only. Complete .NET license and
  runtime third-party notices are copied from the packages into `build/licenses/`
  and shipped in the test payload. No machine-wide .NET installation was performed.

### Vendored package integrity

`src/MeshMe360.EngineWeb/vendor/manifest.json` records exact npm archive URLs,
registry SHA-512 integrity values, versions, licenses and SHA-256 for every
vendored file. `build/vendor-dependencies.py` checks archive integrity on import;
every ordinary build checks the committed file inventory and hashes offline.
All three browser dependencies are redistributed locally with their notices.

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
