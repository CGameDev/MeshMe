"""Import inspected npm packages from .tools/downloads; verify registry integrity first.

Development maintenance command only. Normal builds use committed vendor files.
"""
from pathlib import Path
import base64
import hashlib
import json
import re
import shutil

root = Path(__file__).resolve().parents[1]
downloads = root / '.tools/downloads'
vendor = root / 'src/MeshMe360.EngineWeb/vendor'
packages = {}
for name, version in [('three', '0.170.0'), ('fflate', '0.8.2'), ('meshstep', '0.1.0')]:
    metadata = json.loads((downloads / f'{name}-metadata.json').read_text(encoding='utf-8-sig'))
    assert metadata['name'] == name and metadata['version'] == version
    archive = (downloads / f'{name}.tgz').read_bytes()
    integrity = 'sha512-' + base64.b64encode(hashlib.sha512(archive).digest()).decode()
    assert integrity == metadata['dist']['integrity'], f'{name}: registry integrity mismatch'
    source = downloads / name / 'package'
    target = vendor / f'{name}-{version}'
    target.mkdir(parents=True, exist_ok=True)
    def copy(relative):
        dest = target / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source / relative, dest)
    for filename in ['LICENSE', 'package.json', 'README.md']:
        copy(filename)
    if name == 'three':
        pending = ['build/three.module.js', 'examples/jsm/controls/OrbitControls.js',
                   'examples/jsm/lines/LineSegments2.js', 'examples/jsm/lines/LineSegmentsGeometry.js',
                   'examples/jsm/lines/LineMaterial.js', 'examples/jsm/loaders/STLLoader.js',
                   'examples/jsm/loaders/OBJLoader.js']
        seen = set()
        while pending:
            relative = pending.pop()
            if relative in seen:
                continue
            seen.add(relative)
            copy(relative)
            for spec in re.findall(r'from\s+[\'\"]([^\'\"]+)', (source / relative).read_text(encoding='utf-8')):
                if spec.startswith('.'):
                    pending.append((source / relative).parent.joinpath(spec).resolve().relative_to(source.resolve()).as_posix())
                else:
                    assert spec == 'three', f'Unexpected Three.js dependency: {spec}'
    elif name == 'fflate':
        copy('esm/browser.js')
    else:
        # Keep TypeScript corresponding source alongside the unmodified compiled modules.
        for folder in ['dist', 'src']:
            for path in (source / folder).rglob('*'):
                if path.is_file():
                    copy(path.relative_to(source))
    files = {p.relative_to(target).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
             for p in sorted(target.rglob('*')) if p.is_file()}
    packages[name] = {'version': version, 'source': metadata['dist']['tarball'],
                      'integrity': integrity, 'license': metadata['license'],
                      'directory': target.name, 'files': files}
(vendor / 'manifest.json').write_text(json.dumps(packages, indent=2) + '\n', encoding='utf-8')
print('Vendored and hashed:', ', '.join(packages))
