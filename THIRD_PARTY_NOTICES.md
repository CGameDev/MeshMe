# Mesh Me360 — Third-Party Notices

This file is a living dependency/provenance manifest. Contributors must update it whenever a third-party dependency or vendored asset is added, removed, or version-changed.

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

### Manifold solid geometry backend

- Manifold 3.5.1, source commit `cc8a7f66d7d5a560da94346258c5b546af27811e`
  from https://github.com/elalish/manifold; Copyright The Manifold Authors;
  Apache-2.0. No Manifold geometry source was edited.
- Clipper2 commit `46f639177fe418f9689e8ddb74f08a870c71f5b4`, selected by
  that Manifold revision, from https://github.com/AngusJohnson/Clipper2;
  Copyright Angus Johnson; Boost Software License 1.0. Manifold's own carried
  `CLIPPER2_NO_IOSTREAM` patch is applied by its original CMake configuration.
- Built with Emscripten 5.0.2, SDK commit
  `c817c0ca4ba889ee24a185fd954cff7de1bd8afa`, CMake 3.31.6, Ninja 1.11.1.4.
  `DYNAMIC_EXECUTION=0`, `ENVIRONMENT=web,worker`, serial Manifold,
  MinSizeRel. Rebuild instructions: `docs/implementation-notes/MANIFOLD_BUILD.md`.
- Role: offline worker Boolean, plane split and morphological erosion/hollow
  operations. These extend the application; BumpMesh algorithms remain intact.
- Redistributed: the generated JS/WASM under
  `src/MeshMe360.EngineWeb/vendor/manifold-3.5.1-csp/`, with exact SHA-256 inventory.
  The npm loader is not used. No runtime download or compiler is required.
- Included notices: Manifold Apache license, Clipper2 Boost license, Emscripten
  MIT/NCSA license and authors, musl MIT/copyright exceptions, LLVM libc++,
  libc++abi and compiler-rt Apache/LLVM exception and legacy notices. These
  accompany the compiled runtime in the same vendor directory.
- Manifold acknowledges Sterling Orsten's public-domain linalg and Antti Kuukka's
  public-domain quickhull; its derivative source retains those acknowledgements.
  Emscripten's dlmalloc is public domain (Doug Lea), with Emscripten modifications.

## Desktop dependencies planned for Mesh Me360

The following are architecture selections. Contributors must record the exact installed package/runtime versions and licenses after project creation.

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

### Repair adapter and PMP

- Name: Polygon Mesh Processing Library (PMP); version: source revision
  `215975ca1efafab97477b18187a4a23b87529036`; source:
  https://github.com/pmp-library/pmp-library; license: MIT at this exact revision.
  Copyright 2011–2026 the PMP developers and 2001–2005 Computer Graphics Group,
  RWTH Aachen. Role: bounded repair hole filling and halfedge validation.
  Redistributed in release: yes, compiled into the repair adapter. Exact source
  closure and full license: `vendor/pmp-215975c/src` and `LICENSE.txt`.
- Name: Mesh Me360 repair adapter; version 1; source `build/geometry/repair.cpp`;
  license AGPL-3.0-only. Runtime is `vendor/meshme-repair-1/repair.js` and `.wasm`,
  using the same pinned MPL-2.0 libigl/Eigen and Emscripten dependencies described
  below. Runtime/compiler notices are retained beside it. Full rebuild/source
  instructions: `docs/implementation-notes/REPAIR_BUILD.md`. Release corresponding
  source archives must include the unmodified vendor closures and their notices.

### Solidification adapter and header dependencies

- Name: libigl; version 2.6.0; source https://github.com/libigl/libigl at
  `40e7900ccbd767f1f360e0eb10f0f1a6432e0993`; license MPL-2.0 with retained
  permissive helper notices. Role: signed-distance queries for approximate
  Make Solid. Redistributed in release: yes, compiled into the local WASM adapter.
  Exact unmodified header closure and full MPL text are in
  `vendor/libigl-2.6.0/src` and `vendor/libigl-2.6.0/LICENSE.MPL2`.
- Name: Eigen; version 3.4.0; source https://gitlab.com/libeigen/eigen at
  `3147391d946bb4b6c68edd901f2add6ac1f31f8c`; license MPL-2.0 with permissive
  exceptions, built with EIGEN_MPL2_ONLY. Role: libigl matrix dependency.
  Redistributed in release: yes, compiled. Exact header closure and COPYING
  notices are in `vendor/eigen-3.4.0`. The included BFloat16 header is
  Copyright 2017 The TensorFlow Authors, Apache-2.0.
- Name: Mesh Me360 solidification adapter; version 1; source
  `build/geometry/solidify.cpp`; license AGPL-3.0-only. Redistributed in release:
  yes, `vendor/meshme-solidify-1/solidify.js` and `.wasm`. Uses the already pinned
  Manifold and Emscripten toolchain. Full runtime/system notices, Apache text,
  and Side Effects Software Inc. 2018 MIT notice are included in that directory.
  libigl's Tomas Moller ray-triangle helper retains its upstream public-domain
  acknowledgment. Source and rebuild instructions are in this repository at
  `docs/implementation-notes/SOLIDIFY_BUILD.md`; release source archives must
  retain the vendor source closure and original notices.

- Do not copy Autodesk/Fusion 360 icons, screenshots, logos, or proprietary UI assets.
- Mesh Me360 SVG mockups in `docs/ui/` are original project reference assets.
- Built-in texture assets imported from BumpMesh remain subject to the upstream repository's licensing/notices; preserve provenance.
- Do not add third-party texture/icon packs without source and license documentation.

## Release review requirement

Before v1.0, this file must contain exact versions/licenses for every redistributed dependency. Placeholder wording such as `confirm later`, `latest`, or `unknown` is not acceptable in a release candidate.

## Packaged fixed WebView2 runtime (MSI preview)

Microsoft Edge WebView2 Fixed Version Runtime 152.0.4191.62, Windows x64,
is redistributed unmodified in `WebView2FixedRuntime/`. Microsoft Corporation
retains its copyrights. Its separate Microsoft Software License Terms are in
`Licenses/WebView2-Fixed-Runtime-LICENSE.html`; the runtime also retains its
bundled third-party notices and license viewer. Official license source:
https://developer.microsoft.com/microsoft-edge/api/eula/webview2?fixed=true
Download URL, version and SHA256 are pinned in `build/installer/webview2-runtime.json`.
The fixed runtime is updated by publishing a new application package; it does
not receive Evergreen updates. Review its security updates before distribution.
WiX Toolset 4.0.6 and its UI extension 4.0.6 build the MSI; WiX is a build-time
.NET Foundation tool, licensed under MS-RL, not an application runtime.
