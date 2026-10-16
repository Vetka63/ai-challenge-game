"""Assemble actual Player captures; does not create or alter game sprites."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1] / 'art/playtests/boss-identity'
source = root / 'final-player'
font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 21)
small = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 15)
names = [('warden-phase1', 'IRON WARDEN', '#f3884b'),
         ('bell-phase1', 'BELLKEEPER', '#f3cc64'),
         ('archive-phase1', 'ARCHIVIST', '#87c2ff')]
sheet = Image.new('RGB', (1390, 338), '#070c09')
d = ImageDraw.Draw(sheet)
d.text((20, 12), 'STYLE A / actual 1280 x 720 Player captures / same scale', font=small, fill='#c3d1bd')
for i, (name, label, colour) in enumerate(names):
    x = 10 + i * 460
    d.text((x+12, 43), label, font=font, fill=colour)
    frame = Image.open(source / (name+'-identity.png')).convert('RGB')
    sheet.paste(frame.crop((590,190,1040,440)), (x,78))
sheet.save(root / 'boss-lineup.png')
for name, _, _ in names + [('bell-phase2','',''),('archive-phase2','','')]:
    frames = [Image.open(p).convert('RGB').resize((960,540),Image.Resampling.LANCZOS)
              for p in sorted(source.glob(name+'-motion-*.png'))]
    frames[0].save(root/(name+'.gif'),save_all=True,append_images=frames[1:],duration=250,loop=0,optimize=False)
print('Created lineup and five motion previews from final-player captures.')
