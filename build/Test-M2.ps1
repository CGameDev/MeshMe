param([switch]$Publish, [switch]$SkipWebView)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools/dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $taskRoot '.tools/nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    dotnet build MeshMe360.sln -c Debug -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    python tests/engine-web/test_offline_assets.py
    if ($LASTEXITCODE -ne 0) { throw 'Offline asset checks failed' }
    node --test tests/engine-web/smoke.test.mjs tests/engine-web/rpc.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Engine/bridge tests failed' }
    & tests/MeshMe360.Core.Tests/bin/Debug/net8.0/win-x64/MeshMe360.Core.Tests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
    if (!$SkipWebView) {
        & tests/MeshMe360.Desktop.Tests/bin/Debug/net8.0-windows/win-x64/MeshMe360.Desktop.Tests.exe $taskRoot
        if ($LASTEXITCODE -ne 0) { throw 'WebView2 integration tests failed' }
    }
    if ($Publish) {
        dotnet publish src/MeshMe360.Desktop/MeshMe360.Desktop.csproj -c Debug -r win-x64 --self-contained true --no-restore -o artifacts/m2-windows
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        python build/verify-published.py artifacts/m2-windows
        if ($LASTEXITCODE -ne 0) { throw 'Published content verification failed' }
    }
    if ($SkipWebView) { Write-Warning 'WebView2 tests skipped: this is NOT a full M2 automated pass.' }
    else { Write-Host 'M2 automated checks passed. Manual Windows checkpoint remains required.' }
}
finally { Pop-Location }
