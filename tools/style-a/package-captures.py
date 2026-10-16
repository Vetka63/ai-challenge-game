"""Package actual GPU captures of Arena, without repainting the gameplay frames."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops
import json, hashlib

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/playtests/style-a'
SOURCE=OUT/'720p'
samples=json.loads((SOURCE/'battle-check.json').read_text())['samples']
frames=[Image.open(SOURCE/f'battle-motion-{i:02}.png').convert('RGB') for i in range(len(samples))]
durations=[max(40,round((samples[i+1]['time']-samples[i]['time'])*1000/10)*10) for i in range(len(samples)-1)]
durations.append(durations[-1])
frames[0].save(OUT/'style-a-combat.gif',save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
font=ImageFont.truetype(str(ROOT/'AsciiArena/Assets/StyleStudy/Fonts/JetBrainsMono-Medium.ttf'),20)
board=Image.new('RGB',(1280,840),'#0b100e');draw=ImageDraw.Draw(board)
for i,(title,file) in enumerate([('COMBAT / default rules','crowd.png'),('UPGRADE / real UI','upgrade.png'),('BOSS / controlled smoke setup','boss.png'),('RESULT / controlled smoke setup','result.png')]):
 x=(i%2)*640;y=(i//2)*420;draw.text((x+15,y+12),title,font=font,fill='#c6e5ae')
 board.paste(Image.open(SOURCE/file).resize((640,360),Image.Resampling.LANCZOS),(x,y+48))
board.save(OUT/'verification-contact.png')
baseline=json.loads((OUT/'mechanics-baseline.json').read_text(encoding='utf-8-sig'))
changed=[item['path'] for item in baseline if hashlib.sha256(Path(item['path']).read_bytes()).hexdigest().upper()!=item['sha256']]
report={'mechanics_files_checked':len(baseline),'changed_mechanics_files':changed,'all_mechanics_unchanged':not changed,
 'combat_frames':len(frames),'combat_seconds':samples[-1]['time']-samples[0]['time'],
 'enemies_min':min(s['enemies'] for s in samples),'enemies_max':max(s['enemies'] for s in samples),
 'bolt_hits_delta':samples[-1]['boltHits']-samples[0]['boltHits'],'pulse_hits_delta':samples[-1]['pulseHits']-samples[0]['pulseHits'],
 'kills_delta':samples[-1]['kills']-samples[0]['kills'],
 'motion_confirmed':ImageChops.difference(frames[0],frames[-1]).getbbox() is not None,
 'crt_changes_image':ImageChops.difference(Image.open(SOURCE/'crt-off.png'),Image.open(SOURCE/'crt-on.png')).getbbox() is not None,
 'notes':'Real ECS combat with technical autopilot after accelerated setup at 120s. Automatic upgrade choice only in diagnostic harness. GIF durations follow simulation timestamps; not an FPS benchmark. Boss/result captures use a separate controlled smoke setup.'}
(OUT/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
