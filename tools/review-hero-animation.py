"""Package actual GPU captures into a labeled 20-fps preview and audit state coverage."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/playtests/hero-animations'
CAP=OUT/'player-verified'
report=json.loads((CAP/'report.json').read_text(encoding='utf-8-sig'))
heroes=['Doomguy','Spark','Pacman','Ascii']
directions=['DOWN','UP','LEFT','RIGHT']
coverage=[]
for hero in heroes:
    for direction in range(4):
        rows=[r for r in report['samples'] if r['hero']==hero and r['direction']==direction and r['frame']!=999]
        moving=sorted({r['sprite'] for r in rows if r['frame']<48})
        coverage.append(dict(hero=hero,direction=direction,walk_poses=moving,
                             dash=any(r['dashing'] for r in rows),hurt=any(r['hits']==1 for r in rows)))
        if hero!='Ascii' and len(moving)<3: report['errors'].append(f'Less than three moving poses: {hero}/{direction}')

frames=[]
for frame in range(0,120,3):
    canvas=Image.new('RGB',(800,1000),(8,14,12));draw=ImageDraw.Draw(canvas)
    for row,hero in enumerate(heroes):
        for col,direction in enumerate(directions):
            path=CAP/f'{hero}-{col}-{frame:03}.png'
            canvas.paste(Image.open(path).convert('RGB'),(col*200,row*250+25))
            draw.text((col*200+8,row*250+6),f'{hero} / {direction}',fill=(216,235,221))
    phase='MOVE' if frame<48 else 'DASH / RECOVER' if frame<68 else 'STOP' if frame<80 else 'HURT / RECOVER'
    draw.text((8,986),f'Actual Unity captures / 20 fps / {phase}',fill=(216,235,221))
    frames.append(canvas)
palette=frames[0].quantize(colors=240)
gif=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
gif[0].save(OUT/'heroes-motion.gif',save_all=True,append_images=gif[1:],duration=50,loop=0,optimize=False)
frames[5].save(OUT/'directions-in-game.png')
selection=[2,5,16,17,27,28]
contact=Image.new('RGB',(len(selection)*200,4*245),(8,14,12))
draw=ImageDraw.Draw(contact)
for row,hero in enumerate(heroes):
    for col,index in enumerate(selection):
        contact.paste(Image.open(CAP/f'{hero}-3-{index*3:03}.png'),(col*200,row*245+25))
        draw.text((col*200+6,row*245+6),f'{hero} / f{index*3}',fill=(220,235,225))
contact.save(OUT/'action-contact.png')
(OUT/'coverage.json').write_text(json.dumps(dict(coverage=coverage,errors=report['errors']),indent=2),encoding='utf-8')
print(json.dumps(dict(scenarios=len(coverage),errors=report['errors'],samples=len(report['samples']))))
