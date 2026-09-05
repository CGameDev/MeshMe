param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools/dotnet-home'
    $env:NUGET_PACKAGES = Join-Path $taskRoot '.tools/nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    python build/prepare-web.py
    if ($LASTEXITCODE -ne 0) { throw 'Web preparation failed' }
    python tests/engine-web/test_offline_assets.py
    if ($LASTEXITCODE -ne 0) { throw 'Offline asset tests failed' }
    node --test tests/engine-web/smoke.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Engine smoke tests failed' }
    dotnet build MeshMe360.sln -c Debug -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Publish) {
        dotnet publish src/MeshMe360.Desktop/MeshMe360.Desktop.csproj -c Debug -r win-x64 --self-contained true -p:RestoreLockedMode=true -o artifacts/m1-windows
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        python build/verify-published.py
        if ($LASTEXITCODE -ne 0) { throw 'Published content verification failed' }
    }
    Write-Host 'Automated M1 checks passed. M1 is NOT complete until the manual checkpoint passes.'
}
finally { Pop-Location }
