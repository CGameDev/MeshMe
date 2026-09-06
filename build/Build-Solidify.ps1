# Development candidate; qualification is required before runtime adoption.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pins = @{
    'libigl-2.6.0' = '40e7900ccbd767f1f360e0eb10f0f1a6432e0993'
    'eigen-3.4.0' = '3147391d946bb4b6c68edd901f2add6ac1f31f8c'
    'manifold-source-3.5.1' = 'cc8a7f66d7d5a560da94346258c5b546af27811e'
    'emsdk' = 'c817c0ca4ba889ee24a185fd954cff7de1bd8afa'
}
foreach ($name in $pins.Keys) {
    if ((git -C (Join-Path $repo ".tools/$name") rev-parse HEAD) -ne $pins[$name]) { throw "Unexpected $name revision" }
}
$env:EMSDK_QUIET = '1'
. (Join-Path $repo '.tools/emsdk/emsdk_env.ps1')
$output = Join-Path $repo '.tools/solidify-candidate'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @(
    (Join-Path $PSScriptRoot 'geometry/solidify.cpp'),
    "-I$(Join-Path $repo '.tools/libigl-2.6.0/include')",
    "-I$(Join-Path $repo '.tools/eigen-3.4.0')",
    "-I$(Join-Path $repo '.tools/manifold-source-3.5.1/include')",
    "-I$(Join-Path $repo '.tools/manifold-csp-build/include')",
    (Join-Path $repo '.tools/manifold-csp-build/src/libmanifold.a'),
    '-std=c++17', '-Oz', '-DNDEBUG', '-DEIGEN_MPL2_ONLY', '-DMANIFOLD_PAR=-1',
    '-fexceptions', '--bind', '-sDYNAMIC_EXECUTION=0', '-sENVIRONMENT=web,worker',
    '-sMODULARIZE=1', '-sEXPORT_ES6=1', '-sALLOW_MEMORY_GROWTH=1', '-sMAXIMUM_MEMORY=1073741824',
    '-MD', '-MF', (Join-Path $output 'dependencies.d'), '-o', (Join-Path $output 'solidify.js')
)
em++ @arguments
if ($LASTEXITCODE -ne 0) { throw 'Solidification candidate compilation failed' }
Write-Output "Candidate built in $output. Not adopted until geometry and licensing gates pass."
