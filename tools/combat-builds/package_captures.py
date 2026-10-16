"""Package unedited Unity renders into preview GIFs and a build comparison."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
out=root/'art/playtests/combat-builds'
captures=out/'720p-release'
evidence=json.loads((captures/'combat-check.json').read_text())
assert not evidence['errors'], evidence['errors']
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',21)
sheet=Image.new('RGB',(1536,570),'#060b09')
draw=ImageDraw.Draw(sheet)
summary=[]
for run in evidence['runs']:
    starter=run['starter']
    frames=[Image.open(captures/f'build-{starter}-{i:02}.png').convert('RGB') for i in range(len(run['samples']))]
    samples=run['samples']
    durations=[max(20,round((samples[i+1]['time']-samples[i]['time'])*1000/10)*10) for i in range(len(samples)-1)]
    durations=durations+[durations[-1]]
    frames[0].save(out/f'build-{starter}.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0)
    sheet.paste(frames[20].crop((370,180,882,692)),(starter*512,50))
    draw.text((starter*512+18,14),f"{run['first']} + {run['second']}",font=font,fill='#9addbd')
    summary.append({'build':f"{run['first']} + {run['second']}",'frames':len(frames),'seconds':round(samples[-1]['time']-samples[0]['time'],3),'boltHits':samples[-1]['boltHits']-samples[0]['boltHits'],'wardHits':samples[-1]['wardHits']-samples[0]['wardHits'],'orbitHits':samples[-1]['orbitHits']-samples[0]['orbitHits']})
sheet.save(out/'builds.png')
(out/'preview-summary.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary))
