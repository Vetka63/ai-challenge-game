"""Assemble actual GPU captures into a preview; preserve the recorded simulation timing."""
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1] / "art/playtests/endless-arena"
source = root / "720p-final"
evidence = json.loads((source / "endless-check.json").read_text(encoding="utf-8"))
assert not evidence["errors"], evidence["errors"]
frames = [Image.open(path).convert("RGB").quantize(colors=256, dither=Image.Dither.NONE)
          for path in sorted(source.glob("walk-*.png"))]
samples = evidence["walk"]
durations = [max(20, round((samples[min(i+1,len(samples)-1)]["time"] - sample["time"]) * 100) * 10)
             for i, sample in enumerate(samples)]
durations[-1] = durations[-2]
assert len(frames) == len(samples) == 60
frames[0].save(root / "traversal.gif", save_all=True, append_images=frames[1:],
               duration=durations, loop=0, optimize=False, disposal=2)
print(json.dumps({"frames": len(frames), "seconds": sum(durations)/1000,
                  "size": frames[0].size, "path": str(root / "traversal.gif")}))
