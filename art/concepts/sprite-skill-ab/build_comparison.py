"""Embed unmodified source PNGs in an offline comparison. No raster editing."""
from pathlib import Path
import base64
import json

root = Path(__file__).resolve().parent
experiment = json.loads((root / 'experiment.json').read_text(encoding='utf-8'))
metrics = json.loads((root / 'image-inspection.json').read_text(encoding='utf-8'))
data = {'actors': experiment['characters'], 'assets': {}}
for item in metrics:
    source = root / item['path']
    item['data'] = 'data:image/png;base64,' + base64.b64encode(source.read_bytes()).decode('ascii')
    data['assets'][item['id']] = item
template = (root / 'comparison.template.html').read_text(encoding='utf-8')
html = template.replace('__COMPARISON_DATA__', json.dumps(data, ensure_ascii=False))
output = root / 'comparison.html'
output.write_text(html, encoding='utf-8')
print(output, output.stat().st_size)
