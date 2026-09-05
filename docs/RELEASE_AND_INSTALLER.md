# Mesh Me360 — Release, Installer, and Update Specification

## 1. Release target

Primary artifact:

`MeshMe360-Setup-x64.exe`

Secondary artifact for testing/portable validation:

`MeshMe360-Portable-x64.zip`

Target runtime identifier: `win-x64`.

Do not ship development-only assets, source maps containing local machine paths, test fixtures, or debug symbols in the normal installer unless explicitly intended.

## 2. Packaging framework

Use **Velopack** for v1.0 Windows packaging and application update orchestration unless a proven blocker is documented and approved.

Reasons this is fixed in the project plan:
- one tool for Windows install/update lifecycle;
- supports packaged desktop application updates;
- avoids Codex independently choosing multiple conflicting installer/update frameworks.

If Velopack cannot satisfy an explicit requirement, document the exact blocker before substituting another system.

## 3. Per-user installation

Default to per-user installation so normal installation/update does not require administrator rights where technically possible.

Application display name: `Mesh Me360`.

Executable: `MeshMe360.exe`.

Publisher/project attribution shown in app metadata: `CGameDev / Brian Hinds`.

Do not use Autodesk, Fusion, or CNC Kitchen as the publisher identity.

## 4. WebView2 offline runtime

The end-user release must not require an Internet download on first launch.

Preferred strategy:

1. Pin a WebView2 Fixed Version Runtime x64 version during release engineering.
2. Build/release pipeline downloads the official Microsoft fixed runtime package.
3. Unpack/package it within the release payload under a deterministic directory such as:
   `WebView2FixedRuntime\`
4. Mesh Me360 creates its `CoreWebView2Environment` using that fixed runtime path.
5. The app continues to use its own `%LOCALAPPDATA%\MeshMe360\WebView2\UserData\` user-data folder.

Do **not** commit the large fixed-runtime binary tree to normal source history.

The exact pinned runtime version belongs in a build configuration file created during implementation, for example:

`build/dependencies.props` or `build/runtime-versions.json`.

Do not use `latest` in a release build. CI may have a dependency-update job that proposes a version bump separately.

## 5. Local web dependencies

Release package must contain local pinned copies/build outputs for browser dependencies required by the pinned BumpMesh engine, including:

- Three.js core and required addons
- STLLoader/OBJLoader and other required Three.js modules
- line rendering modules used by upstream
- fflate
- meshStep and required STEP worker/runtime files

No production import map may reference a CDN.

## 6. Installer behavior

Installer must:
- install Mesh Me360 application files;
- install/package the selected offline WebView2 runtime strategy;
- create Start menu shortcut;
- register uninstall entry;
- register `.bumpmesh` association;
- register Open With entries for shared model formats;
- preserve user data on update;
- include or link locally to license/third-party notices from installed application;
- provide an uninstall path.

Do not force desktop shortcut creation in v1.0 unless a user-facing installer option is intentionally implemented.

## 7. File registration

### `.bumpmesh`
ProgID: `MeshMe360.Project`

Suggested registration under current-user classes:

```text
HKCU\Software\Classes\.bumpmesh
  (Default) = MeshMe360.Project

HKCU\Software\Classes\MeshMe360.Project
  (Default) = Mesh Me360 Project
  DefaultIcon\(Default) = "<exe-or-icon-path>",0
  shell\open\command\(Default) = "<MeshMe360.exe>" "%1"
```

Implementation may use the installer/updater framework's equivalent registration API. Behavior must remain equivalent.

### Shared formats
For `.stl`, `.obj`, `.3mf`, `.step`, `.stp`, register Mesh Me360 as an Open With capable application/verb without replacing the user's existing default association.

Uninstall must remove only registrations created by Mesh Me360.

## 8. Startup argument contract

Supported:

```text
MeshMe360.exe
MeshMe360.exe "C:\path\model.stl"
MeshMe360.exe "C:\path\project.bumpmesh"
```

Exactly one startup file is required for v1.0.

If more than one file argument is supplied, open the first supported file and log/show that v1.0 supports one document at a time rather than guessing an import merge behavior.

## 9. Auto-update behavior

Stable feed/source: GitHub Releases for `CGameDev/MeshMe`.

Rules:
- automatic checking enabled by default;
- check at most once per 24 hours;
- do not perform automatic check until main UI is initialized and usable;
- manual Help > Check for Updates bypasses throttle;
- update-check errors are logged and non-blocking;
- no mandatory network connection;
- do not automatically install without user approval in v1.0;
- preserve `%APPDATA%\MeshMe360` and `%LOCALAPPDATA%\MeshMe360` user state across update.

## 10. Update UI

When current:
`Mesh Me360 is up to date.`

When update exists, show:
- current version;
- available version;
- release title/short notes;
- View Release Notes;
- Install Update;
- Later.

During update download:
- show progress;
- allow normal editing only if the updater framework safely supports it;
- otherwise defer install until user closes/restarts;
- never terminate an unsaved document without the normal dirty-document confirmation flow.

## 11. Development/test update channel

Codex must create a way to test updates without offering development packages to stable users.

Acceptable fixed implementation:
- release channels `stable` and `test`;
- production builds default to `stable`;
- Debug/local builds may use `test` through compile-time/build configuration;
- the normal user Preferences UI does not need a public channel selector in v1.0.

## 12. Versioning

Use semantic versions:

`MAJOR.MINOR.PATCH`

Examples:
- `0.1.0` development preview
- `0.9.0` release candidate period
- `1.0.0` first completed release

File/product version metadata must match the release version.

Upstream BumpMesh version/commit is separate metadata and must not be substituted as Mesh Me360 version.

## 13. GitHub Actions release pipeline

Codex should create workflows only after local build scripts work.

Required release pipeline steps:

1. checkout full source;
2. restore .NET dependencies;
3. validate vendored web dependencies / provenance manifest;
4. build web-engine tests;
5. build .NET tests;
6. run tests;
7. publish `win-x64` app;
8. obtain the pinned WebView2 Fixed Version Runtime from the official source;
9. stage all offline assets;
10. package with Velopack;
11. create checksums;
12. upload release artifacts when triggered by an approved release tag/workflow.

Do not release automatically on every push to `main`.

## 14. Reproducibility

The repository must contain enough information to rebuild a release from source:
- exact .NET target;
- exact/pinned upstream BumpMesh SHA;
- third-party web dependency versions;
- WebView2 fixed-runtime version;
- packaging version/configuration;
- build scripts;
- license/notices.

Do not rely on an undocumented developer workstation directory.

## 15. Code signing

The build/release architecture must support Authenticode signing when a signing certificate becomes available.

Do not hard-code a certificate path/password in source.

If no certificate is configured, development releases may be unsigned. The app must not falsely claim it is signed.

## 16. Clean-machine release test

Before v1.0:

1. create clean supported Windows x64 VM;
2. disconnect/disable network;
3. install from `MeshMe360-Setup-x64.exe`;
4. launch app;
5. load local STL;
6. apply built-in texture;
7. enable 3D preview;
8. save project;
9. export STL;
10. close/reopen project;
11. verify `.bumpmesh` association;
12. verify no runtime download prompt/network dependency occurs.

Then reconnect network separately to test updater behavior.
