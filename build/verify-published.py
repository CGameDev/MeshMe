"""Check the actual M1 handoff payload, not just the source web directory."""
from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[1]
published = root / (sys.argv[1] if len(sys.argv) > 1 else 'artifacts/m1-windows')
staged = root / 'artifacts/web'
expected = {p.relative_to(staged).as_posix(): p for p in staged.rglob('*') if p.is_file()}
actual = {p.relative_to(published / 'EngineWeb').as_posix(): p
          for p in (published / 'EngineWeb').rglob('*') if p.is_file()}
assert actual.keys() == expected.keys(), 'Published engine inventory differs from staged web root'
for name, path in expected.items():
    assert path.read_bytes() == actual[name].read_bytes(), name
for name in ['MeshMe360.exe', 'MeshMe360.dll', 'Microsoft.Web.WebView2.Core.dll',
             'Microsoft.Web.WebView2.Wpf.dll', 'coreclr.dll', 'PresentationFramework.dll',
             'LICENSE', 'THIRD_PARTY_NOTICES.md', 'Licenses/WebView2-SDK-LICENSE.txt',
             'Licenses/WebView2-SDK-NOTICE.txt', 'Licenses/DotNet-LICENSE.txt',
             'Licenses/DotNet-THIRD-PARTY-NOTICES.txt']:
    assert (published / name).is_file(), f'Missing handoff asset: {name}'
assert list(published.rglob('WebView2Loader.dll')), 'Missing native WebView2 loader'
runtime = json.loads((published / 'MeshMe360.runtimeconfig.json').read_text())['runtimeOptions']
assert runtime['tfm'] == 'net8.0'
assert any(f['name'] == 'Microsoft.WindowsDesktop.App' and f['version'].startswith('8.0.')
           for f in runtime['includedFrameworks'])
print(f'PASS: {len(expected)} published web files match staging; .NET 8/WPF/SDK/notices present')
print('MANUAL GATE REMAINS: actual WebView2, GPU, native dialogs and disabled-NIC acceptance')
