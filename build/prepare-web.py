"""Stage offline web assets; only explicit dependency/bootstrap transformations.

No mesh algorithm source is transformed. This runs at build time, never at runtime.
"""
from pathlib import Path
import base64
import hashlib
import json
import re
import shutil
import subprocess
import sys
import runpy

root = Path(__file__).resolve().parents[1]
subprocess.run([sys.executable, str(root / 'build/verify-upstream.py')], check=True)
engine = root / 'src/MeshMe360.EngineWeb'
upstream = engine / 'upstream'
output = root / 'artifacts/web'
manifest = json.loads((engine / 'vendor/manifest.json').read_text(encoding='utf-8'))
tracked = (set(subprocess.check_output(['git', '-C', str(root), 'ls-files'], text=True).splitlines())
           if (root / '.git').exists() else None)
for package in manifest.values():
    directory = engine / 'vendor' / package['directory']
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob('*') if p.is_file()}
    assert actual == package['files'].keys(), f'Vendor inventory mismatch: {directory}'
    for relative, digest in package['files'].items():
        assert hashlib.sha256((directory / relative).read_bytes()).hexdigest() == digest, relative
        if tracked is not None:
            assert (directory / relative).relative_to(root).as_posix() in tracked, f'Vendored file omitted from Git: {relative}'

# Remove only this fixed, resolved generated directory, never a caller-selected path.
assert output.resolve() == root.resolve() / 'artifacts' / 'web'
if output.exists():
    shutil.rmtree(output)
output.mkdir(parents=True)
for folder in ['js', 'textures']:
    shutil.copytree(upstream / folder, output / folder)
for filename in ['index.html', 'style.css', 'logo.png', 'LICENSE']:
    shutil.copyfile(upstream / filename, output / filename)
shutil.copytree(engine / 'vendor', output / 'vendor')
shutil.copytree(engine / 'app', output / 'app')
# Corresponding source remains in vendor in Git; release web root needs runtime JS/licenses only.
for path in (output / 'vendor').rglob('*'):
    if path.is_file() and (path.name == '.gitkeep' or path.suffix in ['.ts', '.map'] or 'src' in path.relative_to(output / 'vendor').parts):
        path.unlink()
for path in (output / 'app').glob('.gitkeep'):
    path.unlink()

notice = 'Modified for Mesh Me360 by Computer Universal Technology Systems, 2026. Original work: BumpMesh by CNC Kitchen and contributors. SPDX-License-Identifier: AGPL-3.0-only'
def replace_exact(filename, old, new, modification_notice=notice):
    path = output / filename
    text = path.read_text(encoding='utf-8')
    assert text.count(old) == 1, f'Unexpected upstream dependency shape: {filename}'
    path.write_text('// ' + modification_notice + '\n' + text.replace(old, new), encoding='utf-8', newline='\n')

replace_exact('js/threeCompat.js',
              'https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js',
              '../vendor/three-0.170.0/build/three.module.js')
replace_exact('js/stepWorker.js', 'https://cdn.jsdelivr.net/npm/meshstep@0.1.0/+esm',
              '../vendor/meshstep-0.1.0/dist/index.js')
runpy.run_path(str(root / 'build/adapt-host.py'))['adapt_host'](output, notice)
# The OBJ writer is unchanged upstream source; resolve its sole package import
# locally so it also runs in a worker without the page's import map.
replace_exact('vendor/three-0.170.0/examples/jsm/exporters/OBJExporter.js',
              "from 'three'", "from '../../../build/three.module.js'",
              'Modified for Mesh Me360 by Computer Universal Technology Systems, 2026. Original work: Three.js authors. Dependency resolution only; original MIT license retained.')
page = (output / 'index.html').read_text(encoding='utf-8')
page = page.replace('<title>BumpMesh by CNC Kitchen</title>', '<title>Mesh Me360</title>')
page = re.sub(r'^.*<link rel="(?:canonical|preconnect)"[^\n]*\n', '', page, flags=re.M)
page = re.sub(r'^.*<meta (?:property="og:|name="twitter:)[^\n]*\n', '', page, flags=re.M)
theme = re.search(r'<script>(.*?)</script>', page, re.S)
assert theme
(output / 'app/theme-init.js').write_text('// ' + notice + '\n' + theme[1], encoding='utf-8', newline='\n')
page = page.replace(theme[0], '<script src="app/theme-init.js"></script>')
for old, new in [('https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js', './vendor/three-0.170.0/build/three.module.js'),
                 ('https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/', './vendor/three-0.170.0/examples/jsm/'),
                 ('https://cdn.jsdelivr.net/npm/fflate@0.8.2/esm/browser.js', './vendor/fflate-0.8.2/esm/browser.js')]:
    assert old in page
    page = page.replace(old, new)
import_map = re.search(r'<script type="importmap">(.*?)</script>', page, re.S)[1]
digest = base64.b64encode(hashlib.sha256(import_map.encode()).digest()).decode()
csp = (f"default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-{digest}'; "
       "style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; "
       "worker-src 'self' blob:; connect-src 'self' https://bridge.meshme360.local; font-src 'self'; "
       "object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'")
page = page.replace('<meta charset="UTF-8" />', '<meta charset="UTF-8" />\n  <meta http-equiv="Content-Security-Policy" content="' + csp + '" />')
page = page.replace('<script type="module" src="js/main.js"></script>', '<script type="module" src="app/bootstrap.js"></script>')
page = page.replace('</head>', '<link rel="stylesheet" href="app/desktop.css" />\n</head>')
page = page.replace('</head>', '<link rel="stylesheet" href="app/locked-ui.css" />\n</head>')
page = page.replace('<!DOCTYPE html>', '<!DOCTYPE html>\n<!-- ' + notice + ' -->')
(output / 'index.html').write_text(page, encoding='utf-8', newline='\n')
shutil.copyfile(root / 'THIRD_PARTY_NOTICES.md', output / 'THIRD_PARTY_NOTICES.md')
print('PASS: staged offline page, both worker paths, local dependencies and textures')
