"""Fail closed: structural measurements alone never pass the locked UI milestone."""
import json
import sys
from pathlib import Path

evidence = Path(sys.argv[1])
report = json.loads(evidence.read_text(encoding='utf-8'))
expected = {'project': (7,153,288,630), 'viewport': (302,153,847,630),
            'inspector': (1156,153,373,701), 'shelf': (7,789,1142,195),
            'quick-actions': (1156,858,373,126)}
failures = []
if report['size'] != [1536,1024]: failures.append('Wrong client size')
for name, bounds in expected.items():
    actual = report['regions'].get(name, {})
    for key, target in zip(('x','y','width','height'), bounds):
        if abs(actual.get(key, -10000)-target)>3: failures.append(f'{name}.{key} outside ±3 px')
names = 'Select|Move|Rotate|Scale|Texture|Emboss|Deboss|Pattern|Repair|Hollow|Measure|Boolean|Split|Combine|STL|3MF|OBJ|Import'.split('|')
centers = [52,134,207,279,381,467,545,621,707,784,866,982,1056,1132,1245,1308,1370,1477]
if [b['name'] for b in report['toolbar']] != names: failures.append('Toolbar order differs')
for button, center in zip(report['toolbar'], centers):
    rect = button['rect']
    if abs(rect['x']+rect['width']/2-center)>4 or abs(rect['y']+rect['height']/2-102)>4:
        failures.append(button['name']+' center outside ±4 px')
missing = [c['name'] for c in report['controls'] if not c['implemented']]
detail_path = evidence.with_name('locked-details.json')
if detail_path.exists():
    details = json.loads(detail_path.read_text(encoding='utf-8'))
    for selector, bounds in {'.locked-menu .document-name':(1064,19,328,17),
                            '.locked-menu .export-group':(1402,8,117,39),
                            '.locked-source-grid':(1171,295,343,205),
                            '.locked-apply':(1362,810,152,35)}.items():
        for key, target in zip(('x','y','width','height'), bounds):
            if abs(details.get(selector,{}).get(key,-10000)-target)>3:
                failures.append(selector+'.'+key+' outside ±3 px')
    inspector = details.get('.locked-inspector-body',{})
    if inspector.get('scrollHeight',0)>inspector.get('clientHeight',0):
        failures.append('Reference inspector controls require scrolling')
result = {'structuralGate': 'FAIL' if failures else 'PASS', 'structuralFailures':failures,
          'unimplementedCommands':missing, 'functionalGate':'INCOMPLETE',
          'pixelAppearanceReview':'PENDING', 'humanVisualReview':'PENDING',
          'milestone':'NOT COMPLETE'}
output=evidence.with_name('locked-gate.json');output.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
print('Full control subrequirements, pixel appearance and human review remain required.')
sys.exit(1)
