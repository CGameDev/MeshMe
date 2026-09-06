# Development-only rebuild. Normal application builds use the qualified vendor binary.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$pmp=Join-Path $repo '.tools/pmp-3.0.0'
if((git -C $pmp rev-parse HEAD) -ne '215975ca1efafab97477b18187a4a23b87529036'){throw 'Unexpected PMP candidate revision'}
foreach($pin in @(@('libigl-2.6.0','40e7900ccbd767f1f360e0eb10f0f1a6432e0993'),@('eigen-3.4.0','3147391d946bb4b6c68edd901f2add6ac1f31f8c'),@('emsdk','c817c0ca4ba889ee24a185fd954cff7de1bd8afa'))){
    if((git -C (Join-Path $repo ".tools/$($pin[0])") rev-parse HEAD) -ne $pin[1]){throw "Unexpected $($pin[0]) revision"}
}
$env:EMSDK_QUIET='1'
. (Join-Path $repo '.tools/emsdk/emsdk_env.ps1')
$output=Join-Path $repo '.tools/repair-candidate'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$sources=@((Join-Path $PSScriptRoot 'geometry/repair.cpp'))
foreach($relative in @('surface_mesh.cpp','algorithms/hole_filling.cpp','algorithms/fairing.cpp','algorithms/normals.cpp','algorithms/laplace.cpp','algorithms/numerics.cpp')){
    $sources+=Join-Path $pmp "src/pmp/$relative"
}
$objects=@()
foreach($source in $sources){
    $name=[IO.Path]::GetFileNameWithoutExtension($source)
    $object=Join-Path $output "$name.o"
    em++ $source '-std=c++20' '-Oz' '-DNDEBUG' '-DEIGEN_MPL2_ONLY' '-DPMP_SCALAR_TYPE_64' '-fexceptions' "-I$(Join-Path $repo '.tools/libigl-2.6.0/include')" "-I$(Join-Path $repo '.tools/eigen-3.4.0')" "-I$(Join-Path $pmp 'src')" '-MD' '-MF' (Join-Path $output "$name.d") '-c' '-o' $object
    if($LASTEXITCODE -ne 0){throw "Repair candidate compilation failed: $name"}
    $objects+=$object
}
em++ @objects '-Oz' '-fexceptions' '--bind' '-sDYNAMIC_EXECUTION=0' '-sENVIRONMENT=web,worker' '-sMODULARIZE=1' '-sEXPORT_ES6=1' '-sALLOW_MEMORY_GROWTH=1' '-sMAXIMUM_MEMORY=1073741824' '-o' (Join-Path $output 'repair.js')
if($LASTEXITCODE -ne 0){throw 'Repair candidate link failed'}
Write-Output "Built in $output. Run repair geometry fixtures before updating the vendored binary."
