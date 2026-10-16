"""Create a preview from real GPU frames, retaining their recorded simulation timing."""
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1] / "art/playtests/tactical-crowd"
source = root / "final-720p"
report = json.loads((source / "tactics-check.json").read_text(encoding="utf-8"))
assert not report["errors"], report["errors"]
frames = [Image.open(path).convert("RGB").quantize(colors=256, dither=Image.Dither.NONE)
          for path in sorted(source.glob("crowd-*.png"))]
times = report["times"]
assert len(frames) == len(times) == 32
durations = [max(20, round((times[min(i+1, len(times)-1)] - t) * 100) * 10)
             for i, t in enumerate(times)]
durations[-1] = durations[-2]
frames[0].save(root / "tactical-crowd.gif", save_all=True, append_images=frames[1:],
               duration=durations, loop=0, optimize=False, disposal=2)
print(json.dumps({"frames": len(frames), "seconds": sum(durations)/1000, "path": str(root / "tactical-crowd.gif")}))
