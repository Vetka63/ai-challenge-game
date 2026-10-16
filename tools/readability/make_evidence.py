"""Assemble actual Unity GPU captures; no artwork is generated here."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
out = root / 'art/playtests/readability'
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 22)
small = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 16)

samples = json.loads((out / '720p/battle-check.json').read_text())['samples']
frames = [Image.open(out / f'720p/battle-motion-{i:02}.png').convert('RGB') for i in range(len(samples))]
durations = [max(20, round((samples[i+1]['time']-samples[i]['time'])*1000/10)*10) for i in range(len(samples)-1)]
durations.append(durations[-1])
frames[0].save(out / 'readability-combat.gif', save_all=True, append_images=frames[1:], duration=durations, loop=0)

# Native-resolution crops: the same simulation state with only presentation toggled.
canvas = Image.new('RGB', (1280, 500), '#060b09')
draw = ImageDraw.Draw(canvas)
draw.text((24, 18), 'BEFORE', font=font, fill='#c7d4bd')
draw.text((664, 18), 'AFTER / STYLE A', font=font, fill='#6ee6bf')
for column, suffix in enumerate(('before', 'after')):
    shot = Image.open(out / f'720p/crowd-{suffix}.png')
    canvas.paste(shot.crop((1010, 300, 1250, 550)), (24+column*640, 90))
    canvas.paste(shot.crop((480, 230, 770, 440)), (290+column*640, 90))
    draw.text((24+column*640, 370), 'Enemy overlap', font=small, fill='#c7d4bd')
    draw.text((290+column*640, 370), 'Hero + friendly spell', font=small, fill='#c7d4bd')
draw.text((24, 440), 'Actual 720p GPU crops, 1:1 pixels. Identical combat state.', font=small, fill='#8ca596')
canvas.save(out / 'comparison.png')

stress = Image.new('RGB', (1200, 500), '#060b09')
draw = ImageDraw.Draw(stress)
for column, suffix in enumerate(('before', 'after')):
    shot = Image.open(out / f'1080p/synthetic-overlap-{suffix}.png')
    # 1080p center camera with a little lead from normal camera smoothing.
    crop = shot.crop((690, 300, 1230, 690))
    stress.paste(crop, (30+column*600, 55))
    draw.text((30+column*600, 15), suffix.upper(), font=font, fill='#c7d4bd' if column==0 else '#6ee6bf')
draw.text((30, 465), 'Synthetic overlap test: 9 enemies moved near hero. GPU crops at 1:1.', font=small, fill='#8ca596')
stress.save(out / 'overlap-stress.png')
print(json.dumps({'frames':len(frames), 'seconds':sum(durations)/1000, 'enemyMin':min(s['enemies'] for s in samples), 'enemyMax':max(s['enemies'] for s in samples), 'boltHits':samples[-1]['boltHits']-samples[0]['boltHits'], 'pulseHits':samples[-1]['pulseHits']-samples[0]['pulseHits']}))
