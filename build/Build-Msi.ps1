# SPDX-License-Identifier: AGPL-3.0-only
# Build the owner-requested MSI preview with a pinned offline runtime and local PDF.
[CmdletBinding()]
param([string]$OutputDirectory='artifacts/msi-0.1.0', [switch]$SkipPublish)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    $env:DOTNET_CLI_HOME=Join-Path $repo '.tools/dotnet-home'
    $env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget'
    $output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
    if(-not $output.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must stay inside the repository'}
    $payload=Join-Path $output 'payload';$generated=Join-Path $output 'generated'
    New-Item -ItemType Directory -Force $payload,$generated | Out-Null
    $wix=Join-Path $repo '.tools/wix/wix.exe'
    if(-not(Test-Path $wix)){dotnet tool install wix --version 4.0.6 --tool-path .tools/wix;if($LASTEXITCODE){throw 'WiX restore failed'}}
    $extension=Join-Path $env:USERPROFILE '.wix/extensions/WixToolset.UI.wixext/4.0.6/wixext4/WixToolset.UI.wixext.dll'
    if(-not(Test-Path $extension)){& $wix extension add -g WixToolset.UI.wixext/4.0.6;if($LASTEXITCODE){throw 'WiX UI restore failed'}}
    $runtime=Get-Content build/installer/webview2-runtime.json -Raw | ConvertFrom-Json
    $cab=Join-Path $repo ".tools/downloads/webview2-$($runtime.version)-x64.cab"
    if(-not(Test-Path $cab)){New-Item -ItemType Directory -Force (Split-Path $cab) | Out-Null;Invoke-WebRequest $runtime.url -OutFile $cab}
    if((Get-FileHash $cab -Algorithm SHA256).Hash -ne $runtime.sha256){throw 'Runtime SHA256 mismatch'}
    $cache=Join-Path $repo ".tools/webview2-$($runtime.version)"
    $fixed=Join-Path $cache "Microsoft.WebView2.FixedVersionRuntime.$($runtime.version).x64"
    if(-not(Test-Path (Join-Path $fixed 'msedgewebview2.exe'))){New-Item -ItemType Directory -Force $cache | Out-Null;expand.exe $cab '-F:*' $cache | Out-Null;if($LASTEXITCODE){throw 'Runtime extraction failed'}}
    if(-not $SkipPublish){
        # The output directory must be new to prevent stale files entering a release.
        if(Get-ChildItem $payload){throw 'Use a new output directory for a fresh publish'}
        dotnet publish src/MeshMe360.Desktop/MeshMe360.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -p:DebugType=None -p:DebugSymbols=false -o $payload
        if($LASTEXITCODE){throw 'Publish failed'}
    }
    if(-not(Test-Path (Join-Path $payload 'Documentation/MeshMe360-User-Guide.pdf'))){throw 'User guide missing'}
    $runtimeTarget=Join-Path $payload 'WebView2FixedRuntime'
    New-Item -ItemType Directory -Force $runtimeTarget | Out-Null
    Get-ChildItem -LiteralPath $fixed | Copy-Item -Destination $runtimeTarget -Recurse -Force
    python build/create-msi-source.py $payload $generated
    if($LASTEXITCODE){throw 'MSI source generation failed'}
    & $wix build build/installer/Package.wxs (Join-Path $generated 'Payload.wxs') -arch x64 -ext WixToolset.UI.wixext/4.0.6 -d "Repo=$repo" -d "Generated=$generated" -cc (Join-Path $output 'cab-cache') -pdbtype none -o (Join-Path $output 'MeshMe360-0.1.0-x64.msi')
    if($LASTEXITCODE){throw 'MSI build or validation failed'}
    Get-FileHash (Join-Path $output 'MeshMe360-0.1.0-x64.msi') -Algorithm SHA256
} finally {Pop-Location}
