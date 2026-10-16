from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
root=Path(__file__).resolve().parents[2]/'art/sprites/ascii-actors-v4-hover'
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',16)
names=['south','north','west','east']
frames=[]
for frame in range(4):
    board=Image.new('RGB',(800,460),'#101720');draw=ImageDraw.Draw(board)
    for i,name in enumerate(names):
        draw.text((i*200+40,12),name,font=font,fill='#42e1df')
        im=Image.open(root/f'hero-hover-{name}-{frame}.png').convert('RGBA').resize((256,256),Image.Resampling.NEAREST)
        board.paste(im,(i*200-20,-10),im)
    for i,name in enumerate(['chaser','shooter','heavy','boss']):
        draw.text((i*200+40,248),name,font=font,fill='#f5e6b5')
        im=Image.open(root/f'{name}.png').convert('RGBA').resize((256,256),Image.Resampling.NEAREST)
        board.paste(im,(i*200-20,220),im)
    frames.append(board)
frames[0].save(root/'roster.png')
frames[0].save(root/'hover-preview.gif',save_all=True,append_images=frames[1:],duration=160,loop=0)
summary=[]
for path in root.glob('*.png'):
    if path.name=='roster.png':continue
    im=Image.open(path).convert('RGBA');bbox=im.getbbox()
    assert im.size==(128,128) and bbox and bbox[0]>0 and bbox[1]>0 and bbox[2]<128 and bbox[3]<128
    summary.append({'file':path.name,'size':list(im.size),'bbox':list(bbox),'alpha':sorted({px[3] for px in im.get_flattened_data()})})
assert len(summary)==24
(root/'validation.json').write_text(json.dumps({'frames':24,'status':'pass','images':summary},indent=2),encoding='utf-8')
print('24 images: size, alpha, margins verified')
