"""Render an original ASCII character from the project's existing glyph definitions.

The image-generated attempt is retained as source.png, NOT used as the final art.
No external fonts are embedded in the sprite. Preview labels use a local system font.
"""
from pathlib import Path
import hashlib, json, re
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
GLYPHS = REPO / 'AsciiArena/Assets/Prototype/Rendering/GlyphAtlas.cs'
OLD = REPO / 'art/sprites/roster-01-07-v1/runtime/runescribe/frames/idle_south_00.png'
patterns = {}
for key, pattern in re.findall(r"\['(.+?)'\]=\"([01/]+)\"", GLYPHS.read_text(encoding='utf-8')):
    if key == '\\\\': key = '\\'
    patterns[key] = pattern.split('/')
rows = (ROOT / 'hero.txt').read_text(encoding='utf-8').splitlines()
assert len(rows) == 9 and all(len(row.rstrip()) <= 12 for row in rows)
palette = {'cyan': '#42e1df', 'teal': '#299fa7', 'ivory': '#f5e6b5'}
sprite = Image.new('RGBA', (128, 128))
cells = []
for y, row in enumerate(rows):
    for x, ch in enumerate(row):
        if ch == ' ': continue
        assert 32 <= ord(ch) <= 126 and ch in patterns, ch
        name = 'cyan'
        if ch in '.()' or x == 9 and ch == '|' or y == 8: name = 'ivory'
        elif ch in ':=': name = 'teal'
        color = tuple(bytes.fromhex(palette[name][1:])) + (255,)
        cells.append({'column': x, 'row': y, 'glyph': ch, 'color': palette[name]})
        for py, bits in enumerate(patterns[ch]):
            for px, bit in enumerate(bits):
                # Bold terminal weight; keep counters in asterisk, colon and equals open.
                # This preserves thin strokes when the camera reduces 72px to 57px.
                if bit == '1':
                    for dx,dy in ([(0,0),(1,0)] if ch in '*:=' else [(0,0),(1,0),(0,1),(1,1)]):
                        sprite.putpixel((34 + x*6 + px + dx, 36 + y*8 + py + dy), color)
sprite.save(ROOT / 'runescribe-ascii.png')
old = Image.open(OLD).convert('RGBA')
old.save(ROOT / 'runescribe-current.png')
font_path = 'C:/Windows/Fonts/consola.ttf'
font = ImageFont.truetype(font_path, 14)
small = ImageFont.truetype(font_path, 12)
title = ImageFont.truetype(font_path, 21)
bg = '#101720'
board = Image.new('RGBA', (880, 650), bg)
draw = ImageDraw.Draw(board)
draw.text((24, 18), 'RUNESCRIBE / one static front pose', font=title, fill='#e2e9eb')
draw.text((24, 50), 'SAME CAMERA + 64 PPU / Point filtering / no glow', font=small, fill='#9aaab8')
draw.text((70, 88), 'CURRENT / pixel art', font=font, fill='#cbd5e0')
draw.text((490, 88), 'NEW / literal ASCII glyphs', font=font, fill='#42e1df')

def preview_pair(resolution, baseline, label):
    scale = resolution / (2 * 10.6 * 64)
    canvas = round(128 * scale)
    root = (round(64 * canvas/128), round(108 * canvas/128))
    draw.text((24, baseline - 93), label, font=small, fill='#9aaab8')
    for im, center in [(old, 225), (sprite, 655)]:
        display = im.resize((canvas, canvas), Image.Resampling.NEAREST)
        board.alpha_composite(display, (center-root[0], baseline-root[1]))
        draw.line((center-60, baseline+4, center+60, baseline+4), fill='#26313e')

preview_pair(1080, 208, '1080p / 1 screen pixel = 1 preview pixel')
preview_pair(720, 328, '720p / same world size and camera')
draw.text((24, 365), 'DETAIL / 3x native texture pixels (not game scale)', font=small, fill='#9aaab8')
for im, center in [(old, 225), (sprite, 655)]:
    enlarged = im.resize((384,384), Image.Resampling.NEAREST)
    board.alpha_composite(enlarged, (center-192, 637-324))
board.convert('RGB').save(ROOT / 'comparison.png')

# Separate compact preview of alpha over two backgrounds.
alpha_board = Image.new('RGBA', (512,256), '#101720')
ad = ImageDraw.Draw(alpha_board)
ad.rectangle((256,0,511,255), fill='#e5e7e9')
for x in [0,256]: alpha_board.alpha_composite(sprite.resize((256,256), Image.Resampling.NEAREST), (x,0))
alpha_board.convert('RGB').save(ROOT / 'alpha-check.png')
colors = sorted(set(sprite.get_flattened_data()))
stats = {'status':'pass', 'canvas':[128,128], 'glyph_count':len(cells),
         'unique_ascii_characters':''.join(sorted({c['glyph'] for c in cells})),
         'grid_columns':12,'grid_rows':9,'cell_px':[6,8],'glyph_px':[5,7], 'font_weight':'2x2 stamp; *:= horizontal 2x1 stamp to preserve counters; extent <=6x8',
         'bbox':list(sprite.getbbox()),'old_bbox':list(old.getbbox()),
         'alpha_values':sorted({c[3] for c in colors}),
         'opaque_colors':len([c for c in colors if c[3]]),
         'root_top_left':[64,108], 'unity_pivot':[0.5,0.15625],'pixels_per_unit':64,
         'orthographic_size':10.6,
         'expected_height_1080p':(sprite.getbbox()[3]-sprite.getbbox()[1])*1080/(21.2*64),
         'expected_height_720p':(sprite.getbbox()[3]-sprite.getbbox()[1])*720/(21.2*64),
         'not_verified':['Unity import of this new sprite','Play Mode','crowd readability in motion'],
         'checks':{'all_glyphs_printable_ascii':True,'no_clipping':all([sprite.getbbox()[0]>0,sprite.getbbox()[1]>0,sprite.getbbox()[2]<128,sprite.getbbox()[3]<128]),'transparent_rgb_zero':all(c[:3]==(0,0,0) for c in colors if c[3]==0)}}
assert stats['checks']['no_clipping'] and stats['checks']['transparent_rgb_zero']
(ROOT/'validation.json').write_text(json.dumps(stats,indent=2),encoding='utf-8')
(ROOT/'glyph-map.json').write_text(json.dumps({'rows':rows,'palette':palette,'origin':[34,36],'font_weight_stamp':[[0,0],[1,0],[0,1],[1,1]],'font_weight_exceptions':{'characters':'*:=','stamp':[[0,0],[1,0]]},'patterns':{ch:patterns[ch] for ch in sorted({c['glyph'] for c in cells})},'cells':cells},indent=2),encoding='utf-8')
(ROOT/'provenance.json').write_text(json.dumps({
    'date':'2026-10-15','final_art_source':'original ASCII text layout rendered with existing project glyphs',
    'final_route':'deterministic glyph composition, not ImageGen output',
    'glyph_source':str(GLYPHS.relative_to(REPO)),
    'glyph_source_sha256':hashlib.sha256(GLYPHS.read_bytes()).hexdigest(),
    'identity_reference':str(OLD.relative_to(REPO)),
    'imagegen_attempt':{'file':'source.png','prompt':'prompt.txt','route':'host_image','model':None,'estimateUsd':None,'selected':False,'reason':'Glyph-like drawing does not ensure exact ASCII glyph shapes and regular cells.'},
    'reference_game':'https://store.steampowered.com/app/4588220/ASCII_Survivors/',
    'third_party_game_art_copied':False,
    'sprite_sha256':hashlib.sha256((ROOT/'runescribe-ascii.png').read_bytes()).hexdigest()
},indent=2),encoding='utf-8')
print(json.dumps(stats,indent=2))
