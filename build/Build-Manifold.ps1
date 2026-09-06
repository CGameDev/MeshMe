# Development-only rebuild. The shipped application needs no compiler or Python.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repo '.tools/manifold-source-3.5.1'
$sdk = Join-Path $repo '.tools/emsdk'
if ((git -C $source rev-parse HEAD) -ne 'cc8a7f66d7d5a560da94346258c5b546af27811e') { throw 'Unexpected Manifold source revision' }
if ((git -C $sdk rev-parse HEAD) -ne 'c817c0ca4ba889ee24a185fd954cff7de1bd8afa') { throw 'Unexpected Emscripten SDK revision' }
$env:EMSDK_QUIET = '1'
. (Join-Path $sdk 'emsdk_env.ps1')
$env:PATH = "$(Join-Path $repo '.tools/build-python/cmake/data/bin');$(Join-Path $repo '.tools/build-python/bin');$env:PATH"
$output = Join-Path $repo '.tools/manifold-csp-build'
emcmake cmake -S $source -B $output -G Ninja -DCMAKE_BUILD_TYPE=MinSizeRel -DMANIFOLD_TEST=OFF -DMANIFOLD_CBIND=OFF -DMANIFOLD_PYBIND=OFF -DMANIFOLD_PAR=OFF '-DCMAKE_EXE_LINKER_FLAGS=-sDYNAMIC_EXECUTION=0 -sENVIRONMENT=web,worker'
if ($LASTEXITCODE -ne 0) { throw 'Manifold configuration failed' }
# Windows short-path normalization can invalidate the first-run SDK cache.
# Serial compilation avoids concurrent cache initialization/clearing.
cmake --build $output --target manifoldjs -j 1
if ($LASTEXITCODE -ne 0) { throw 'Manifold compilation failed' }
Write-Output "Built candidate in $output/bindings/wasm. Run the WebView2 geometry gate before adoption."
