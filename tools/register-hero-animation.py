"""Technical registration/QA of generated frames; does not paint or alter source images."""
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'art/sprites/hero-animations'
RES = ROOT / 'AsciiArena/Assets/Resources/HeroAnimations'


def inspect(path):
    image = Image.open(path).convert('RGBA')
    pixels = np.asarray(image)
    ys, xs = np.where(pixels[:, :, 3] > 8)
    if not len(xs):
        raise ValueError(f'Empty frame: {path}')
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
    return image, pixels, bbox


frames, qc = [], []
for hero in ['doomguy', 'spark', 'pacman']:
    reference = RES / hero / ('closed.png' if hero == 'pacman' else 'south-pass.png')
    master, _, box = inspect(reference)
    # Common source scale per hero, independent of each pose's alpha bounds.
    height = 1.0 if hero == 'pacman' else 1.39
    ppu = (box[3] - box[1]) / height
    baseline = box[3]
    for path in sorted((RES / hero).glob('*.png')):
        image, pixels, bbox = inspect(path)
        ratio = image.height / master.height
        if hero == 'pacman':
            root_x = (box[0] + box[2]) * .5 * ratio
            root_y = (box[1] + box[3]) * .5 * ratio
        else:
            # Register to head/body axis, not weapon or trailing scarf bounds.
            y0 = int(bbox[1] + (bbox[3]-bbox[1]) * (.08 if hero == 'doomguy' else .24))
            y1 = int(bbox[1] + (bbox[3]-bbox[1]) * (.20 if hero == 'doomguy' else .38))
            centers = []
            for row in pixels[y0:y1, :, 3]:
                x = np.where(row > 128)[0]
                if len(x):
                    centers.append((x.min()+x.max())/2)
            root_x = float(np.median(centers))
            root_y = baseline * ratio
        frame = dict(path=path.relative_to(ROOT/'AsciiArena').as_posix(), rootX=root_x, rootY=root_y,
                     pixelsPerUnit=ppu*ratio)
        frames.append(frame)
        qc.append(dict(path=frame['path'], width=image.width, height=image.height, bbox=bbox,
                       alpha_min=int(pixels[:,:,3].min()), alpha_max=int(pixels[:,:,3].max()),
                       transparent_fraction=float((pixels[:,:,3]==0).mean()),
                       sha256=hashlib.sha256(path.read_bytes()).hexdigest()))

ART.mkdir(parents=True, exist_ok=True)
(ART/'registration.json').write_text(json.dumps(dict(frames=frames), indent=2), encoding='utf-8')
(ART/'source-qc.json').write_text(json.dumps(qc, indent=2), encoding='utf-8')

# Static technical contact sheet at 80 screen pixels per world unit (1440p camera 9).
# Uniform import scale and root reproduce Unity registration, no per-frame auto-fit.
for hero in ['doomguy','spark','pacman']:
    chosen = [f for f in frames if '/'+hero+'/' in f['path']]
    cols = 5 if hero != 'pacman' else 4
    rows = (len(chosen)+cols-1)//cols
    sheet = Image.new('RGB',(cols*220,rows*200),(12,17,16))
    draw = ImageDraw.Draw(sheet)
    for i,f in enumerate(chosen):
        image = Image.open(ROOT/'AsciiArena'/f['path']).convert('RGBA')
        scale = 80/f['pixelsPerUnit']
        image = image.resize((round(image.width*scale),round(image.height*scale)),Image.Resampling.LANCZOS)
        cx,cy=(i%cols)*220+110,(i//cols)*200+165
        if hero=='pacman': cy-=40
        sheet.paste(image,(round(cx-f['rootX']*scale),round(cy-f['rootY']*scale)),image)
        draw.line((cx-12,cy,cx+12,cy),fill=(50,100,80))
        draw.text(((i%cols)*220+6,(i//cols)*200+180),Path(f['path']).stem,fill=(220,230,220))
    sheet.save(ART/(hero+'-registered.png'))
print(json.dumps(dict(frames=len(frames), alpha_valid=all(q['alpha_min']==0 and q['alpha_max']==255 for q in qc))))
