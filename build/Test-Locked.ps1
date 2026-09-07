[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home'
    $env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget'
    dotnet build MeshMe360.sln -c Debug --no-restore
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    python tests/engine-web/test_offline_assets.py
    if($LASTEXITCODE -ne 0){throw 'Asset gate failed'}
    node --test tests/engine-web/smoke.test.mjs tests/engine-web/rpc.test.mjs tests/engine-web/locked-core.test.mjs tests/engine-web/export.test.mjs tests/engine-web/mesh-processing.test.mjs
    if($LASTEXITCODE -ne 0){throw 'Engine/scene tests failed'}
    & tests/MeshMe360.Core.Tests/bin/Debug/net8.0/win-x64/MeshMe360.Core.Tests.exe
    if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
    $runner=Join-Path $repo 'tests/MeshMe360.Desktop.Tests/bin/Debug/net8.0-windows/win-x64/MeshMe360.Desktop.Tests.exe'
    foreach($mode in @('', '--scene','--solid','--combine','--exports','--measure','--pattern','--print','--source','--menus','--settings','--view','--diagnostics','--app-diagnostics','--mesh-processing','--lifecycle','--advanced','--legacy','--display','--dropdown','--about','--recovery-compat','--stress','--empty','--transform','--library','--relief','--locked')){
        & $runner $repo $mode
        if($LASTEXITCODE -ne 0){throw "WebView2 test failed: $mode"}
    }
    New-Item -ItemType Directory -Force artifacts/web/candidate | Out-Null
    Copy-Item src/MeshMe360.EngineWeb/vendor/manifold-3.5.1-csp/manifold.js,src/MeshMe360.EngineWeb/vendor/manifold-3.5.1-csp/manifold.wasm artifacts/web/candidate
    Copy-Item tests/engine-web/solid-probe.html artifacts/web/candidate/probe.html
    Copy-Item tests/engine-web/solid-probe.js artifacts/web/candidate/probe.js
    & $runner $repo --backend-probe
    if($LASTEXITCODE -ne 0){throw 'Solid fixture gate failed'}
    Copy-Item src/MeshMe360.EngineWeb/vendor/meshme-solidify-1/solidify.js,src/MeshMe360.EngineWeb/vendor/meshme-solidify-1/solidify.wasm artifacts/web/candidate
    Copy-Item tests/engine-web/solidify-probe.js artifacts/web/candidate/probe.js
    & $runner $repo --backend-probe
    if($LASTEXITCODE -ne 0){throw 'Solidification fixture gate failed'}
    Copy-Item src/MeshMe360.EngineWeb/vendor/meshme-repair-1/repair.js,src/MeshMe360.EngineWeb/vendor/meshme-repair-1/repair.wasm artifacts/web/candidate
    Copy-Item tests/engine-web/repair-probe.js artifacts/web/candidate/probe.js
    & $runner $repo --backend-probe
    if($LASTEXITCODE -ne 0){throw 'Repair fixture gate failed'}
    Write-Output 'Current locked implementation regressions passed. Full control sign-off, pixel/human fidelity and physical Windows gates remain incomplete.'
} finally {Pop-Location}
