"""Verify the full imported tree against pinned Git blob identities (no network)."""
from pathlib import Path
import hashlib

root = Path(__file__).resolve().parents[1]
upstream = root / 'src/MeshMe360.EngineWeb/upstream'
expected = {}
for line in (root / 'build/upstream-tree.txt').read_text(encoding='utf-8-sig').splitlines():
    metadata, name = line.split('\t', 1)
    mode, kind, digest = metadata.split()
    assert kind == 'blob' and mode in ('100644', '100755'), line
    expected[name] = digest
actual = {p.relative_to(upstream).as_posix(): p for p in upstream.rglob('*') if p.is_file()}
assert actual.keys() == expected.keys(), 'Imported file inventory differs from pinned tree'
for name, digest in expected.items():
    data = actual[name].read_bytes()
    blob = b'blob ' + str(len(data)).encode() + b'\0' + data
    assert hashlib.sha1(blob).hexdigest() == digest, f'Changed upstream file: {name}'
assert (root / 'LICENSE').read_bytes() == (upstream / 'LICENSE').read_bytes()
print(f'PASS: {len(expected)} upstream blobs and complete root AGPL license match pinned baseline')
