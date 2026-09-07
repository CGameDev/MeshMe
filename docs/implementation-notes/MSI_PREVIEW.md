# MSI preview and offline user guide

## Scope

Owner-requested addition, September 2026: generate an MSI with an application
license agreement, distribute a PDF guide, and expose Help > User Guide. This is
a 0.1.0 development preview, not completion of M3, M7, M8 or v1.0. The original
Velopack update milestone remains future work. No mesh algorithms changed.

## Build

- Windows x64, .NET SDK capable of building the locked .NET 8 solution; Python
  for existing offline staging and installer-source generation.
- Run `build/Build-Msi.ps1` from PowerShell. Use a fresh output directory for a
  fresh publish. It restores pinned WiX 4.0.6 and UI extension 4.0.6 if absent,
  verifies the fixed-runtime CAB SHA256, publishes self-contained .NET 8,
  harvests stable components and builds/validates the MSI.
- `-SkipPublish` only reuses an already reviewed payload for installer-authoring
  changes. It is not a fresh source build. Cabinet cache is under the output.
- The guide is committed at `output/pdf/MeshMe360-User-Guide.pdf`. Regenerate
  using `python build/create-user-guide.py` with ReportLab 4.4.9; inspect all
  rendered pages after editorial changes. End users need only a PDF reader.
- `build/Test-Msi.ps1` checks product identity, payload count, the full AGPL
  control, acceptance gating, HKCU-only registration, common-format default
  preservation, and app/guide shortcuts without installing the product.
- Generated executables, runtime CAB/tree, MSI and test captures are excluded
  from Git. Runtime pins and download/hash metadata are committed.

## Installation behavior

Current-user destination: `%LOCALAPPDATA%/Programs/MeshMe360`. Start menu entries
open the application and its guide. `.mm360` uses `MeshMe360.Project`. STL, OBJ,
3MF, STEP, STP and legacy `.bumpmesh` receive Open with registrations, without
replacing common-format defaults. User project files and application data are
outside installer-owned components and remain on uninstall.

The standard MSI welcome/license dialog shows the complete GNU AGPLv3, original
engine attribution and publisher. Acceptance enables installation. Microsoft
fixed-runtime terms and all dependency notices are included separately. There
is no new proprietary EULA. Source is https://github.com/CGameDev/MeshMe.

The MSI includes .NET 8 and WebView2 Fixed Version 152.0.4191.62 x64. The
Microsoft-signed runtime is included unmodified. Two deferred, current-user
installer actions grant read/execute on the runtime directory to application
package SIDs, as required for Windows 10 unpackaged WebView2 applications.
Neither action changes system-wide security settings or permissions on user data.
Reference: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution
WiX UI reference: https://docs.firegiant.com/wix/tools/wixext/wixui/

Help > User Guide requests `documentation.openUserGuide`. The native host opens
only the fixed `Documentation/MeshMe360-User-Guide.pdf` beside the app using the
Windows PDF association. No payload-supplied path is used. Missing guides or
reader launch failures appear through the existing Help error surface.

## Acceptance record

Build, automated source tests, guide rendering and packaged verification are
recorded below. Clean-machine Windows 10/11 install/repair/upgrade/uninstall,
license cancellation/acceptance in a normal installation, file-association
launches, physical GPU/DPI behavior and disabled-network acceptance remain
manual release gates. The MSI is unsigned; no signing certificate was supplied.
Do not infer that generating the installer has passed those gates.

### September 7 verification

- Debug and Release builds passed; Debug solution: zero warnings/errors.
- 28 Node tests, 38 Core tests and six offline-asset checks passed.
- Isolated native `--guide` and `--dropdown` tests passed, including both themes.
- All eight PDF pages rendered and were visually reviewed; selectable text and
  expected page count checked. PDF output contains no tooling attribution.
- WiX compilation/Windows Installer validation passed. Read-only MSI checks
  passed for 1,264 files, full AGPL control, acceptance gating, per-user HKCU,
  two Start menu shortcuts and shared-format default preservation.
- Administrative extraction returned 0; all 1,264 extracted files matched the
  SHA256 manifest exactly. Test harness files were added only after verification
  to the extracted review copy; they are not present in the MSI.
- The extracted release assemblies/web assets and bundled fixed WebView2 passed
  startup, real model import and Help > User Guide through `--guide --packaged`.
- Microsoft runtime signature validated as Microsoft Corporation.
- Normal installer UI accessibility confirmed the license checkbox and initial
  disabled Install button. Screenshot capture failed with `SetIsBorderRequired:
  No such interface supported (0x80004002)`; visual installation review and actual
  installation remain manual. The user's running model session was preserved.
- Final MSI SHA256:
  `cb8d6ed3e70fb19be367886931f51387ef5640e979b8c457dda8417f5752e788`.
