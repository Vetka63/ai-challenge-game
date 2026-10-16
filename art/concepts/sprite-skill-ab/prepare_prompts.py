"""Prepare text only. No image generation, image editing, network or credentials."""
from pathlib import Path
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parent
SKILL = Path.home() / '.codex/skills/generate2dsprite'
experiment = json.loads((ROOT / 'experiment.json').read_text(encoding='utf-8'))
for actor in experiment['characters']:
    target = ROOT / 'prompts' / actor['id']
    target.mkdir(parents=True, exist_ok=True)
    common = experiment['common_brief']
    identity = actor['identity']
    size = f"Intended gameplay footprint: about {actor['display_width']} by {actor['display_height']} screen pixels. This is a concept for that scale, not a request for literal output dimensions."
    (target / 'A.txt').write_text(f'{common}\n\nCharacter: {identity}\n\n{size}\n', encoding='utf-8')
    spec = {
        'name': actor['id'], 'subject': actor['subject'], 'identity': identity,
        'class': actor['class'], 'finish': 'pixel', 'facing': 'front',
        'view': 'orthographic slightly elevated front view for a top-down 2D arena game',
        'pose': 'a neutral readable resting pose, full body and all extremities visible',
        'extra': 'Keep body art independent of attack effects: no projectiles, trails, particles or aura. Make the silhouette and identity readable at the intended gameplay size.',
    }
    spec_path = target / 'forge-spec.json'
    spec_path.write_text(json.dumps(spec, indent=2), encoding='utf-8')
    recipe_dir = target / 'forge-recipe'
    if not recipe_dir.exists():
        subprocess.run([sys.executable, '-X', 'utf8', str(SKILL / 'scripts/master_still.py'),
                        'prompt', '--spec', str(spec_path), '--output-dir', str(recipe_dir)], check=True)
    recipe = (recipe_dir / 'prompt.txt').read_text(encoding='utf-8')
    # Retain Forge's identity/facing/framing/opaque-body recipe. The project brief
    # overrides its stock SNES style, text ban and chroma key for this comparison.
    lines = []
    for line in recipe.splitlines():
        if line.startswith('Style:') or line.startswith('Background:'):
            continue
        lines.append(line.replace('magenta margin', 'transparent margin'))
    adapted = '\n'.join(lines).strip()
    final = (f'{common}\n\n{adapted}\n\n{size}\n\n'
             'ASCII art direction takes precedence over stock pixel shading: punctuation-like marks '
             'are the anatomy, not decorative text stamped on solid clothing. Preserve negative-space '
             'openings and exaggerate the few identity cues so they survive a thumbnail. '
             'Use the same thickness of angular strokes throughout. No fine texture, folds or dithering. '
             'Do not fill the open face or the gaps between limbs. Fully opaque coloured strokes on '
             'genuine transparent alpha; do not paint magenta or a checkerboard. No cast shadow.\n')
    (target / 'B.txt').write_text(final, encoding='utf-8')
    print(actor['id'], 'A chars', len((target / 'A.txt').read_text(encoding='utf-8')), 'B chars', len(final))
