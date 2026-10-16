"""Build contact sheet and motion previews from actual GPU captures."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

root=Path(__file__).resolve().parents[1]/'art/playtests/boss-balance'
source=root/'player'
font=ImageFont.truetype('C:/Windows/Fonts/consola.ttf',20)
names=['warden-crowd60','bell-crowd60','archive-crowd60','bell-basic']
sheet=Image.new('RGB',(1280,800),'#060b08')
draw=ImageDraw.Draw(sheet)
for index,name in enumerate(names):
    files=sorted(source.glob(name+'-pattern*.png'))
    frame=Image.open(files[min(1,len(files)-1)]).convert('RGB').resize((640,360),Image.Resampling.LANCZOS)
    x=(index%2)*640;y=(index//2)*400
    draw.text((x+15,y+9),name+' / actual combat',font=font,fill='#c8d4c1')
    sheet.paste(frame,(x,y+40))
    frames=[Image.open(p).convert('RGB').resize((960,540),Image.Resampling.LANCZOS) for p in sorted(source.glob(name+'-motion-*.png'))]
    frames[0].save(root/(name+'.gif'),save_all=True,append_images=frames[1:],duration=150,loop=0,optimize=False)
sheet.save(root/'crowd-readability.png')
print('Saved real GPU contact sheet and four x4 motion previews.')
