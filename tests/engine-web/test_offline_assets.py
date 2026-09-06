"""M1 static gates; these do not substitute for WebView2/offline Windows acceptance."""
from pathlib import Path
import base64
import hashlib
import json
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / 'artifacts/web'
UPSTREAM = ROOT / 'src/MeshMe360.EngineWeb/upstream'

class OfflineAssets(unittest.TestCase):
    def test_only_declared_upstream_changes(self):
        changed = {'index.html', 'js/threeCompat.js', 'js/stepWorker.js', 'js/main.js', 'js/exporter.js', 'js/viewer.js'}
        for path in WEB.rglob('*'):
            relative = path.relative_to(WEB)
            original = UPSTREAM / relative
            if path.is_file() and original.is_file() and relative.as_posix() not in changed:
                self.assertEqual(path.read_bytes(), original.read_bytes(), relative)
        for name in changed:
            self.assertIn('Modified for Mesh Me360', (WEB / name).read_text(encoding='utf-8'))

    def test_import_map_and_script_csp(self):
        page = (WEB / 'index.html').read_text(encoding='utf-8')
        import_map = re.search(r'<script type="importmap">(.*?)</script>', page, re.S)[1]
        imports = json.loads(import_map)['imports']
        for path in imports.values():
            self.assertTrue(path.startswith('./vendor/'))
            self.assertTrue((WEB / path).exists(), path)
        digest = base64.b64encode(hashlib.sha256(import_map.encode()).digest()).decode()
        csp = re.search(r'http-equiv="Content-Security-Policy" content="([^"]+)"', page)[1]
        self.assertIn("'sha256-" + digest + "'", csp)
        scripts = csp.split('script-src ')[1].split(';')[0]
        self.assertNotIn('unsafe-inline', scripts)
        self.assertNotIn('unsafe-eval', scripts)
        self.assertNotIn('*', csp)
        self.assertNotIn('cdn.jsdelivr.net', page)
        for attrs, body in re.findall(r'<script([^>]*)>(.*?)</script>', page, re.S):
            if body.strip():
                self.assertIn('type="importmap"', attrs)

    def test_module_dependency_closure(self):
        mapping = {'three': 'vendor/three-0.170.0/build/three.module.js',
                   'fflate': 'vendor/fflate-0.8.2/esm/browser.js'}
        for path in WEB.rglob('*.js'):
            text = path.read_text(encoding='utf-8')
            specs = re.findall(r'(?:from\s*|import\s*\()\s*[\'\"]([^\'\"]+)', text)
            for spec in specs:
                self.assertFalse(spec.startswith(('http:', 'https:', '//')), (path, spec))
                if spec in mapping:
                    resolved = WEB / mapping[spec]
                elif spec.startswith('three/addons/'):
                    resolved = WEB / 'vendor/three-0.170.0/examples/jsm' / spec.removeprefix('three/addons/')
                else:
                    self.assertTrue(spec.startswith('.'), (path, spec))
                    resolved = (path.parent / spec).resolve()
                self.assertTrue(resolved.is_relative_to(WEB.resolve()), resolved)
                self.assertTrue(resolved.is_file(), (path, spec))
        # Dynamic worker fallback and language imports need explicit checks too.
        compat = (WEB / 'js/threeCompat.js').read_text(encoding='utf-8')
        fallback = re.search(r"const CDN_THREE = '([^']+)'", compat)[1]
        self.assertTrue((WEB / 'js' / fallback).is_file())
        for name in ['exportWorker.js', 'stepWorker.js']:
            self.assertTrue((WEB / 'js' / name).is_file())
        self.assertEqual(len(list((WEB / 'js/i18n').glob('*.js'))), 14)

    def test_all_textures_and_thumbnails(self):
        source = (WEB / 'js/presetTextures.js').read_text(encoding='utf-8')
        paths = re.findall(r"(?:url|thumb):\s*'(textures/[^']+)'", source)
        self.assertEqual(len(paths), 48)
        for path in paths:
            self.assertEqual((WEB / path).read_bytes(), (UPSTREAM / path).read_bytes())

    def test_no_development_servers_or_binary_payloads(self):
        for name in ['Dockerfile', 'compose.yaml', '.env.example', 'bench-pipeline.mjs']:
            self.assertFalse((WEB / name).exists())
        self.assertFalse(list(WEB.rglob('*.map')))
        self.assertFalse(list(WEB.rglob('*.ts')))

if __name__ == '__main__':
    unittest.main(verbosity=2)
