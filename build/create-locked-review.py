"""Produce an unmodified, registered PNG comparison; never imply acceptance."""
import base64
import sys
from pathlib import Path

repo = Path(__file__).resolve().parents[1]
capture = Path(sys.argv[1]).resolve()
reference = repo / 'docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png'
def url(path):
    return 'data:image/png;base64,' + base64.b64encode(path.read_bytes()).decode('ascii')
page = '''<!doctype html><html lang="en"><meta charset="utf-8">
<title>Mesh Me360 — locked UI comparison</title>
<style>body{margin:0;background:#151c21;color:#e6edf3;font:15px Segoe UI,sans-serif}header{padding:16px;position:sticky;top:0;background:#151c21;z-index:2}h1{font-size:20px;margin:0 0 8px}p{margin:8px 0;color:#b6c5d1}button,input{font:inherit}button{padding:6px 12px;margin-right:8px;background:#245b88;color:white;border:1px solid #618bad;border-radius:4px}button:focus-visible,input:focus-visible{outline:3px solid #90d1ff}.frame{position:relative;width:1536px;height:1024px;margin:12px auto}.frame img{position:absolute;inset:0;width:1536px;height:1024px}.scroller{overflow:auto}input{width:280px;vertical-align:middle}</style>
<header><h1>Mesh Me360 — production UI comparison</h1>
<p>Milestone 3 remains open. Compare the locked target with the actual Windows app capture at 1536 × 1024.</p>
<button onclick="setOpacity(0)">Locked target</button><button onclick="setOpacity(1)">Current app</button>
<label>Overlay <input id="mix" aria-label="Current app opacity" type="range" min="0" max="1" step="0.01" value="1" oninput="setOpacity(this.value)"></label>
<p>Images are unmodified and unmasked. Model content and document values differ. Inspect panel boundaries, text, controls, accents, viewport chrome and texture previews; this page does not grant visual acceptance.</p></header>
<div class="scroller"><div class="frame"><img alt="Authoritative locked UI target" src="REFERENCE"><img id="actual" alt="Actual Mesh Me360 Windows app" src="ACTUAL"></div></div>
<script>function setOpacity(value){document.getElementById('actual').style.opacity=value;document.getElementById('mix').value=value}</script></html>'''
output = capture.with_name('locked-visual-review.html')
output.write_text(page.replace('REFERENCE', url(reference)).replace('ACTUAL', url(capture)), encoding='utf-8')
print(output)
