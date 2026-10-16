"""Package real Unity captures. This does not generate or repaint game art."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import hashlib, json, shutil

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'art/style-study-2026-10-15'
RENDERS = OUT / 'renders'
FONT = ROOT / 'AsciiArena/Assets/StyleStudy/Fonts/JetBrainsMono-Medium.ttf'
shutil.copy2(ROOT / 'art/playtests/crt-hover/1080p-final/crowd.png', OUT / 'current-crt-hover.png')
for key, filename in [('A', 'A-terminal.png'), ('B', 'B-ritual.png')]:
    frames = [Image.open(RENDERS / f'{key}-motion-{i:02}.png').convert('RGB').resize((1280,720),Image.Resampling.LANCZOS) for i in range(24)]
    frames[0].save(OUT/f'{key}-motion.gif',save_all=True,append_images=frames[1:],duration=[80,80,90]*8,loop=0,optimize=False)
    Image.open(RENDERS / filename).resize((1280,720),Image.Resampling.LANCZOS).save(OUT / f'{key}-720.png')

board = Image.new('RGB',(1920,1224),'#0c1114')
draw = ImageDraw.Draw(board)
font = ImageFont.truetype(str(FONT),22)
tiles = [('ASCII Survivors / official Steam frame','references/03-green-field.jpg'),('Current / CrtHover','current-crt-hover.png'),('A / Phosphor terminal','renders/A-terminal.png'),('B / Ritual terminal','renders/B-ritual.png')]
for i,(title,path) in enumerate(tiles):
    x=(i%2)*960;y=(i//2)*612
    draw.text((x+20,y+15),title,font=font,fill='#dfe9e5')
    board.paste(Image.open(OUT/path).convert('RGB').resize((960,540),Image.Resampling.LANCZOS),(x,y+56))
board.save(OUT/'comparison.png')

hashes=['6df6641f4cf0ef70b76388ae563ee30782a36ae9','567895ea735288ec933c1da7ef4edff01683f908','7e9ed1a34fbf54b1382fc2d98c97a827629e1524']
names=['01-field.jpg','02-dense-boss.jpg','03-green-field.jpg']
titles=['Ice Queen / cold palette','Inferno Titan / dense glyph boss','Bone Lord / green environment']
sources={'date':'2026-10-15','status':'Awaiting user choice; visual study only', 'steam':'https://store.steampowered.com/app/4588220/ASCII_Survivors/',
 'references':[{'file':'references/'+name,'title':title,'capture':'Browser screenshot, displayed at 1280x720; not an original texture or a licensed game asset','size':list(Image.open(OUT/'references'/name).size),'url':f'https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/4588220/{h}/ss_{h}.1920x1080.jpg?t=1777655069'} for h,name,title in zip(hashes,names,titles)],
 'font':{'name':'JetBrains Mono Medium','source':'https://github.com/JetBrains/JetBrainsMono','license':'SIL OFL 1.1','license_file':'../../AsciiArena/Assets/StyleStudy/Fonts/OFL.txt','sha256':hashlib.sha256(FONT.read_bytes()).hexdigest()},
 'captures':{'engine':'Unity 6000.3.24f1 / URP / Direct3D11','resolution':[1920,1080],'gif_resolution':[1280,720],'gif_fps':12,'gif_duration_seconds':2,'note':'GPU renders of an isolated presentation scene. GIF timing is a preview, not a gameplay performance measurement. A/B use the same anchors, roles, density and illustrative attack. Current build and reference show different gameplay moments.'}}
(OUT/'sources.json').write_text(json.dumps(sources,ensure_ascii=False,indent=2),encoding='utf-8')
baseline=json.loads((OUT/'evidence/protected-baseline.json').read_text(encoding='utf-8-sig'))
changed=[x['path'] for x in baseline if not Path(x['path']).exists() or hashlib.sha256(Path(x['path']).read_bytes()).hexdigest().upper()!=x['sha256']]
report={'checked_files':len(baseline),'changed_files':changed,'all_unchanged':not changed}
(OUT/'evidence/gameplay-freeze-check.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report))
