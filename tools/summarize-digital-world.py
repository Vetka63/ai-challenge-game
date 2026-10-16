"""Summarize measured combat and build QA previews from captured game frames."""
from pathlib import Path
from collections import defaultdict
import json, statistics, hashlib
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[1]
out=root/'art/playtests/digital-world'
data=json.loads((out/'core-balance.json').read_text(encoding='utf-8'))['cases']
groups=defaultdict(list)
for case in data:groups[(case['rank'],case['policy'])].append(case)
lines=['# Kernel balance evidence','','144 actual ECS fights, 100 HP, base movement, no level-up healing.','Controllers read announced hazards; these are not human win rates.','','| Rank | Policy | Wins | Median victory seconds | Mean damage |','| --- | --- | --- | --- | --- |']
for (rank,policy),cases in sorted(groups.items()):
    wins=[c for c in cases if c['victory']]
    median=f"{statistics.median(c['seconds'] for c in wins):.1f}" if wins else '—'
    lines.append(f"| {rank} | {['stationary','circle','reactive + dash','reactive no dash'][policy]} | {len(wins)}/{len(cases)} | {median} | {statistics.mean(c['damage'] for c in cases):.1f} |")
(out/'core-results.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
source=out/('player-final' if (out/'player-final/report.json').exists() else 'player-v2')
sheet=Image.new('RGB',(1280,800),'#080d0b');draw=ImageDraw.Draw(sheet)
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',20)
for i,name in enumerate(['Spark','Pacman','Doomguy','Ascii']):
    p=source/(name+'-pattern-0.png')
    frame=Image.open(p).convert('RGB').resize((640,360),Image.Resampling.LANCZOS)
    x=(i%2)*640;y=(i//2)*400
    draw.text((x+15,y+10),name+' / actual gameplay',font=font,fill='#cfdbc8');sheet.paste(frame,(x,y+40))
sheet.save(out/'heroes-in-combat.png')
assets=root/'AsciiArena/Assets/Resources/DigitalHeroes'
qa=[]
for name in ['spark','pacman','doomguy']:
    p=assets/(name+'.png');im=Image.open(p)
    qa.append(dict(name=name,size=im.size,mode=im.mode,alpha_extrema=im.getchannel('A').getextrema(),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(root/'art/sprites/digital-outsiders/source-qc.json').write_text(json.dumps(qa,indent=2),encoding='utf-8')
print('Saved measured results, source alpha/hashes and real combat comparison.')
