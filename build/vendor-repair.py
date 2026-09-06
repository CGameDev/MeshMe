"""Adopt the tested repair adapter with exact compiler source closure and notices."""
from pathlib import Path
import hashlib
import json
import shutil

root=Path(__file__).resolve().parents[1]
vendor=root/'src/MeshMe360.EngineWeb/vendor'
candidate=root/'.tools/repair-candidate'
manifest_path=vendor/'manifest.json'
manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
dependencies=set()
for depfile in candidate.glob('*.d'):
    dependencies.update(Path(line.strip().removesuffix('\\').strip().replace('\\ ', ' ')).resolve()
                        for line in depfile.read_text().splitlines()[1:])
def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source,target)
def record(key,target,**metadata):
    manifest[key]=dict(directory=target.name,**metadata,files={p.relative_to(target).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(target.rglob('*')) if p.is_file()})
for source_name,target_name in [('libigl-2.6.0','libigl-2.6.0'),('eigen-3.4.0','eigen-3.4.0'),('pmp-3.0.0','pmp-215975c')]:
    source=root/'.tools'/source_name
    target=vendor/target_name
    for path in sorted(dependencies):
        if path.is_relative_to(source):
            relative=path.relative_to(source)
            assert not any(part in ['copyleft','restricted','unsupported'] for part in relative.parts)
            copy(path,target/'src'/relative)
    if source_name.startswith('pmp'):
        copy(source/'LICENSE.txt',target/'LICENSE.txt')
        record(target_name,target,source='https://github.com/pmp-library/pmp-library',revision='215975ca1efafab97477b18187a4a23b87529036',license='MIT')
    else:
        metadata={k:v for k,v in manifest[target_name].items() if k not in ['directory','files']}
        record(target_name,target,**metadata)
target=vendor/'meshme-repair-1'
for name in ['repair.js','repair.wasm']:
    copy(candidate/name,target/name)
for path in (vendor/'meshme-solidify-1').iterdir():
    if path.is_file() and path.name!='SOURCE.md' and path.suffix not in ['.js','.wasm']:
        copy(path,target/path.name)
(target/'SOURCE.md').write_text('App-owned adapter: build/geometry/repair.cpp, AGPL-3.0-only. '
    'Rebuild using build/Build-Repair.ps1. Exact libigl, Eigen and PMP source closures '
    'and licenses are provided in sibling vendor directories. '
    'See docs/implementation-notes/REPAIR_BUILD.md for pins and limitations.\n',encoding='utf-8')
record('meshme-repair',target,version='1',source='build/geometry/repair.cpp',license='AGPL-3.0-only with MPL-2.0/MIT dependencies')
manifest_path.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('Repair runtime, source closure, notices and hashes recorded')
