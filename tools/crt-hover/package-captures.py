"""Package unmodified GPU frames into a GIF; assess CRT A/B and nonempty output."""
from pathlib import Path
from PIL import Image, ImageChops, ImageStat, ImageDraw
import json

root = Path(__file__).resolve().parents[2] / 'art/playtests/crt-hover/720p-final'
off = Image.open(root/'crt-off.png').convert('RGB')
on = Image.open(root/'crt-on.png').convert('RGB')
assert on.size == off.size == (1280, 720)
assert max(ImageStat.Stat(on).stddev) > 10, 'Capture is blank'
delta = ImageChops.difference(off, on)
assert delta.getbbox(), 'CRT toggle did not change the rendered frame'
frames = [Image.open(p).convert('RGB') for p in sorted(root.glob('motion-[0-9][0-9].png'))]
assert len(frames) == 24
assert all(ImageChops.difference(frames[i], frames[i+1]).getbbox() for i in range(23))
# Sampling includes GPU readback time; this is a visual preview, not a frame-rate measurement.
frames[0].save(root/'hover-in-game.gif', save_all=True, append_images=frames[1:], duration=110, loop=0, optimize=False)
board = Image.new('RGB',(1280,768),'#070c10')
draw = ImageDraw.Draw(board)
for i in range(12):
    frame = frames[i*2]
    crop = frame.crop((560,270,720,430))
    # Twelve native-size hero crops, arranged to inspect shape stability over time.
    x=(i%6)*212; y=(i//6)*216+28
    board.paste(crop,(x,y));draw.text((x,y-18),f'frame {i*2:02}',fill='white')
board.crop((0,0,1280,460)).save(root/'motion-contact.png')
(root/'visual-check.json').write_text(json.dumps({
    'capture':'URP GPU RenderRequest', 'size':on.size, 'motion_frames':len(frames),
    'crt_mean_absolute_difference':ImageStat.Stat(delta).mean,
    'crt_changes_image':True,'frames_advance':True,
    'gif_note':'110 ms presentation per sampled frame; not real-time FPS evidence'
},indent=2),encoding='utf-8')
print('Nonblank GPU renders; CRT A/B differs; 24 changing motion frames packaged.')
