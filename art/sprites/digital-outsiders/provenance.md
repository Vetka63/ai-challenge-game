# Digital outsiders — source and integration

Art source: **host_image**, built-in `image_gen`, native transparent background.
No external game sprites were downloaded. Model/version and USD cost were
not reported by the host tool. The character designs were generated separately;
this is not a character animation sheet. Three masters were selected and
reviewed by the agent under the user's request to implement the whole stage.

Source image mapping (originals also kept in the host generated_images folder):

- `doomguy.png`: `exec-9ece5328-8d36-438b-8de8-196ff61bf62f.png`
- `pacman.png`: `exec-199c431c-61a6-408d-a6a9-5abcd11d1305.png`
- `spark.png`: `exec-c3b93349-73fb-41a2-92a3-58d82807a179.png`

Project originals are in `AsciiArena/Assets/Resources/DigitalHeroes/`.
Unity's importer trims transparent padding through sprite rectangles; the
original PNGs remain unchanged. Texture size is limited at import to 512 px;
runtime sprite height is 1.63 world units (humanoids) or 1.18 (Pac-Man).
Facing: front three-quarter right, mirrored left; Pac-Man rotates cardinally.
Motion: runtime transform sway, dash squash/stretch and glyph trails. These
are not video-generated or frame-by-frame animation clips.

Prompts used:

**Doomguy**

Create one production-ready transparent-background 2D game character sprite:
classic Doomguy / Doom marine, full body, front three-quarter view facing
slightly right, chunky olive green helmet with glossy dark cyan visor,
muscular tan bare forearms, olive chest armor, brown belt and boots, holding
a compact dark shotgun close across chest. No pedestal, floor, shadow on
ground, lettering, UI or extra character. Stylized compact arcade sprite for
a top-down survivors game, readable at 56 pixels high, large clear silhouette,
very few broad color clusters, thick dark outline with narrow pale mint edge
highlights, restrained flat colors, no realistic texture, no fine detail, no
glows around silhouette. Consistent orthographic perspective slightly viewed
from above. Body fully visible, neutral combat stance, both feet visible.
Single character fills central 75 percent of image height, transparent padding
all around. This is the actual sprite asset, not a concept illustration or sprite sheet.

**Pac-Man**

One transparent-background production 2D game sprite of classic Pac-Man.
Iconic plain yellow circular orb with a wide triangular open mouth facing
right, a tiny black oval eye above mouth. No arms, legs, gloves, shoes,
pellet, text or other elements. Near-flat arcade illustration with only two
broad yellow tones, crisp dark thin contour, slight pale yellow upper edge.
No 3D rendering, no realistic texture, no lighting halo. Compact extremely
readable silhouette at 48 pixels tall, truly transparent background, generous
padding. One single character, central 75% height. Actual game sprite, no concept scene or sheet.

**Spark**

One production-ready transparent-background 2D sprite for a top-down survivors
game. An original mysterious tiny traveller called Spark, a short floating
cloak figure with a smooth ivory oval porcelain mask and exactly two dark
eye slits, deep midnight teal pointed hood, compact turquoise cape, a short
mustard amber scarf trailing to the left, small dark mitten hands, no visible
legs or feet. Ivory mask is the clear focal point, a tiny amber diamond brooch.
Front three-quarter view facing slightly right, slightly above orthographic
game perspective. Cute but determined, not horror. Simple hand-painted arcade
illustration, broad clean flat color clusters, thick dark outer contour and
pale mint rim on hood and cape, no realistic texture or tiny details. Strong
asymmetric silhouette readable at 56 px high. Single full-body character fills
central 75 percent image height with transparent margin all around. No letters,
weapons, UI, floor, drop shadow, checkerboard, background, decorative glow,
extra character or sprite sheet. Actual game sprite to integrate into a dark green ASCII terminal world.

Capability reports `doctor.json` (initial encoding failure) and
`doctor-utf8.json` (corrected UTF-8 environment) are retained. Optional video
generation and ffmpeg routes are unavailable. No Forge character animation
or frame-QA output is claimed. Actual alpha validation is performed by
DigitalHeroImport; visual quality is checked in the built game.
