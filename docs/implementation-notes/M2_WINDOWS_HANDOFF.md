# M2 Windows checkpoint — awaiting manual acceptance

M1 passed with the user's offline confirmation on 2026-09-05. M2 automated
tests pass; M2 is not complete until the checks below pass. M3 has not begun.

Run `./build/Test-M2.ps1 -Publish` from the repository on Windows with a .NET SDK,
Python, Node and installed WebView2. Tests use isolated settings and profiles.
Restricted sandboxes may prevent WebView2 from initializing; run this test in
a normal desktop environment. The entire `artifacts/m2-windows` folder is needed
to run its self-contained .NET 8 executable. This is a development handoff;
fixed runtime distribution, installer and release qualification remain M8.

## Manual checkpoint

1. Close prior Mesh Me360 test windows, then launch from PowerShell:
   `& .\artifacts\m2-windows\MeshMe360.exe "$((Get-Location).Path)\tests\fixtures\cube_binary.stl"`
   Confirm the cube appears without choosing a file. Repeat with a copied fixture
   whose path includes spaces and Unicode characters. Missing/unsupported paths
   must report a useful warning. Multiple paths must warn and open the first
   supported one. No file associations are installed by this milestone.
2. Export STL, then export 3MF in the same session. Each must show the native
   Windows save dialog with the correct extension/filter. Choose a destination
   containing spaces. Reopen the exported models and verify geometry. Repeat an
   export and cancel; the previous destination must remain unchanged.
3. Save a project with the model included, using the existing upstream project
   controls. Confirm the native dialog's `.bumpmesh` filter and overwrite prompt.
   Close normally and launch the saved project by command line. Accept the
   preserved upstream load-content dialog and verify the model/settings.
4. Confirm existing file chooser and single-file drag/drop still open models.
   Switch theme and language, close normally, relaunch and verify persistence.
   On a fresh profile, System theme must follow Windows light/dark preference.
5. Review `%LOCALAPPDATA%\MeshMe360\Logs\` and WebView2 DevTools console:
   matching request IDs must appear for bridge operations, with no binary
   payloads in logs or messages. Native startup transfer uses one-use internal
   GET tokens and streamed bytes; the automated 128 MB check verifies exact data
   without a file-sized host allocation. No base64 JSON transport is used.
6. Verify normal close writes `clean: true` in
   `%LOCALAPPDATA%\MeshMe360\Session\active-session.json` and exits promptly.
   Recovery snapshots/UI remain M6; the marker alone is not recovery acceptance.

Record Windows edition/build, WebView2 version, pass/fail for each check and any
error text. Do not proceed to M3 while this checkpoint has an unresolved failure.

## Automated evidence and limits

- 36 Core checks: settings, recent retention/missing paths, startup parsing,
  token scope/expiry/reuse, 128 MB exact stream, session retention and save filters.
- 5 Python asset checks and 6 Node engine/RPC checks; upstream 117 blobs verified.
- Actual WPF/WebView2: handshake, native activation/internal GET, invalid-token
  rejection, settings, repeated STL/3MF downloads, real project serialization,
  cancellation and language persistence. Only native modal decisions are
  substituted in this test. STL/3MF transport payloads are synthetic; deterministic
  STL geometry parity is separately tested, and 3MF geometry needs manual review.
- The repeated-download check initially failed because Chromium blocked subsequent
  downloads. Scoped app-origin permission handling fixed it; the full test now
  passes. Each download still requires an approved URL and native save decision.
- No claim of physical GPU/DPI, full STEP parity, disabled-network requalification,
  installer, updater installation or v1.0 acceptance follows from these tests.
