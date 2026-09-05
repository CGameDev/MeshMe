# M1 manual Windows checkpoint

Status: **NOT RUN — M1 remains IN PROGRESS.**

This is the first required physical/manual Windows gate in Issue #1.
Do not start M2 until the M1 checkpoint and relevant TEST_PLAN rows pass.

## Prepared build

From the repository root:

```powershell
./build/Test-M1.ps1 -Publish
./artifacts/m1-windows/MeshMe360.exe
```

Build prerequisites: a .NET SDK capable of targeting .NET 8 (verified with
10.0.400), Python 3 and Node 22.15+ for build/test tools. Initial NuGet restore
needs network access; repeat builds use the local cache. There is no npm install,
server, Python or Node requirement for the running application.

Copy the **whole** `artifacts/m1-windows/` directory and `tests/fixtures/` to the
test machine. The self-contained build includes .NET 8/WPF. It still requires an
already-installed WebView2 runtime, or an existing compatible x64 fixed runtime
placed in `WebView2FixedRuntime/` beside MeshMe360.exe. This M1 build does not
download a runtime. Pinning and distributing that runtime in an installer is M8.

Use Windows 10 22H2 x64 or supported Windows 11 x64 with a working GPU driver.
The development host reported Windows 19044, so it does not establish supported-OS
acceptance. Keep a known valid, locally available STEP/STP model for the STEP check.

## Required acceptance

Record Windows version, GPU/driver, WebView2 version, date, tester and build/commit
with the results. Logs are in `%LOCALAPPDATA%\MeshMe360\Logs\` and include the
WebView2 version. No geometry/texture binary data is logged.

1. Disable networking **before launch** on the test machine. Launch MeshMe360.exe.
   Confirm the WPF window contains the upstream UI and a working Three.js viewport.
   The M1 UI intentionally has not received the M3 Mesh Me360 layout.
2. Dismiss the upstream welcome overlay if shown. Use its file chooser to open
   `cube_binary.stl`; also check the preserved drag-and-drop path. Expect a closed
   20 × 20 × 20 mm cube with 12 triangles before refinement.
3. Orbit, pan and zoom. Select a built-in texture and enable 3D Preview; confirm
   visible texture displacement and responsive viewport interaction.
4. Export STL with the upstream controls. Confirm a nonempty binary STL is written
   through the current browser download path, then reopen it and inspect the mesh.
   Native host-controlled save destinations are the next milestone, M2.
5. Import a local STEP/STP model using the upstream coarse/standard/fine options.
   Confirm tessellation finishes using the local worker and diagnostics appear.
6. In DevTools (F12 or Ctrl+Shift+I), inspect Console and Network after repeating
   load/preview/export. Require no CSP, module, worker, texture, WebGL or CDN/network
   dependency errors. Local app requests use `https://app.meshme360.local/`.
   Explicitly opened external attribution/source links are not offline dependencies.
7. Close and relaunch while still offline. Confirm normal startup again.
8. In a **copy of the test output**, temporarily rename `EngineWeb/index.html`.
   Launch and confirm a visible startup failure, Copy Details and Open Log Folder.
   Restore the file in that copy. Also verify missing/unusable WebView2 runtime
   errors are visible on a test machine where that scenario can be exercised.

Do not disable networking on a machine needed for remote access. A disconnected
test VM is suitable. This instruction does not claim the clean-machine M8 installer
or complete section 20 offline feature suite has passed.

## Results to fill after testing

| Check | Result | Evidence |
|---|---|---|
| Supported OS + offline launch + upstream UI | NOT RUN | |
| Model chooser + drag/drop + cube dimensions | NOT RUN | |
| Viewport + built-in texture + GPU preview | NOT RUN | |
| STL download/export + re-import | NOT RUN | |
| STEP/STP worker + quality options | NOT RUN | |
| Console/network dependency review | NOT RUN | |
| Offline relaunch | NOT RUN | |
| Fatal-startup screen and details/log actions | NOT RUN | |

If a check fails, retain the console error and relevant local log, fix the M1
problem and rerun the affected checks. Keep M2 gated. Do not mark Issue #1 complete.
