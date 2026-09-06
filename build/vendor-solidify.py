"""Adopt a qualified, locally built solidification candidate and exact source closure."""
from pathlib import Path
import hashlib
import json
import shutil

root = Path(__file__).resolve().parents[1]
vendor = root / 'src/MeshMe360.EngineWeb/vendor'
candidate = root / '.tools/solidify-candidate'
manifest_path = vendor / 'manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
dependencies = [Path(line.strip().removesuffix('\\').strip().replace('\\ ', ' ')).resolve()
                for line in (candidate / 'dependencies.d').read_text().splitlines()[1:]]

def copy(source, target):
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)

def record(key, directory, **metadata):
    manifest[key] = dict(directory=directory.name, **metadata, files={
        path.relative_to(directory).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(directory.rglob('*')) if path.is_file()})

for name, revision, url, licenses in [
    ('libigl-2.6.0', '40e7900ccbd767f1f360e0eb10f0f1a6432e0993', 'https://github.com/libigl/libigl', ['LICENSE.MPL2']),
    ('eigen-3.4.0', '3147391d946bb4b6c68edd901f2add6ac1f31f8c', 'https://gitlab.com/libeigen/eigen', ['COPYING.MPL2', 'COPYING.BSD', 'COPYING.README'])]:
    source = root / '.tools' / name
    target = vendor / name
    for path in sorted(set(dependencies)):
        if path.is_relative_to(source):
            relative = path.relative_to(source)
            assert not any(part in ['copyleft', 'restricted', 'unsupported'] for part in relative.parts)
            copy(path, target / 'src' / relative)
    for name in licenses:
        copy(source / name, target / name)
    record(target.name, target, source=url, revision=revision, license='MPL-2.0 with permissive source exceptions')

target = vendor / 'meshme-solidify-1'
for name in ['solidify.js', 'solidify.wasm']:
    copy(candidate / name, target / name)
# Full runtime/system notices already reviewed for the identical compiler and Manifold.
for path in (vendor / 'manifold-3.5.1-csp').iterdir():
    if path.is_file() and path.suffix not in ['.js', '.wasm']:
        copy(path, target / path.name)
copy(root / 'LICENSE', target / 'MESHME-LICENSE')
sidefx = (root / '.tools/libigl-2.6.0/include/igl/FastWindingNumberForSoups.h').read_text()
(target / 'SIDEFX-LICENSE').write_text('\n'.join(line.removeprefix('// ') for line in sidefx.split('// # Fast Winding Numbers for Soups')[0].splitlines()[1:])+'\n', encoding='utf-8')
(target / 'SOURCE.md').write_text('The app-owned adapter is build/geometry/solidify.cpp (AGPL-3.0-only). '
    'Rebuild with build/Build-Solidify.ps1. Exact unmodified libigl/Eigen header closures and MPL notices '
    'are distributed in sibling vendor directories. Source pins and limitations are documented in '
    'docs/implementation-notes/SOLIDIFY_BUILD.md. Manifold and compiler notices are included here.\n', encoding='utf-8')
record('meshme-solidify', target, version='1', source='build/geometry/solidify.cpp', license='AGPL-3.0-only; dependencies retain their licenses')
manifest_path.write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
print('Recorded solidification runtime, source closure, licenses and SHA-256 inventory')
