# 3D Model Source Notes

## Original 3D source

- **Asset:** LowPoly Animated Knight
- **Author:** Quaternius
- **Source:** https://opengameart.org/content/lowpoly-animated-knight
- **Direct download:** https://opengameart.org/sites/default/files/Knight%20Character%20by%20%40Quaternius.zip
- **License:** CC0 (Public Domain)
- **Downloaded:** 2026-06-28
- **Formats in archive:** .blend, .fbx, .obj

## Files used for rendering

- `knight_character.zip` — original archive
- `knight_character/Knight Character by @Quaternius/OBJ/KnightCharacter.obj`
- `knight_character/Knight Character by @Quaternius/OBJ/KnightCharacter_bright.obj` (edited MTL for brighter armor)
- `knight_character/Knight Character by @Quaternius/OBJ/Sword.obj`
- `knight_character.glb` — converted from the brightened OBJ
- `sword.glb` — converted from Sword.obj

## Generated outputs

- `warrior_knight_render.png` — raw 96x128 render from the Godot offline renderer
- `warrior_001_board.png` — final 96x128 battle-board sprite with post-process sharpening

Both are RGBA with transparent background.

## Why the armor is brighter

The original OBJ materials were very dark (`Armor Kd 0.12`, `Boots Kd 0.02`), which made the
knight look like a black silhouette on the brown battlefield. A copy of the OBJ was created
with brightened diffuse colors and converted to GLB for rendering:

- `Armor` brightened to `Kd 0.45 0.45 0.48`, `Ks 0.7`
- `Boots` brightened to `Kd 0.25 0.15 0.10`
- `Skin` brightened to `Kd 0.70 0.55 0.38`

The source `.zip` and original `.obj`/`.mtl` remain untouched.

## Render settings

Rendered with `tools/render_unit_sprite.gd` using Godot 4.6.2 (Vulkan Forward+,
NVIDIA RTX 5090 D v2):

- Viewport: 768x1024 (8x), scaled down to 96x128 with `INTERPOLATE_NEAREST`
- Camera: Orthographic, size 6.2
- Camera position: `(3.5, 6.0, 4.0)`
- Look-at target: `(0, 2.8, 0)`
- Lights: key directional + front fill + rim light + strong ambient
- Post-process: PIL UnsharpMask + contrast/brightness boost

## Usage

The `.glb`/`.obj`/`.fbx` files are **not used directly in the game runtime**. They are only
sources for offline 3D-to-2D baking. The final PNG should match the project's battle unit
sprite specs:

- 1x1 units: 96x128 px canvas, anchor (48, 112)
- 2x2 units: 192x160 px canvas, anchor (96, 136)

See `docs/design/battle_unit_sprite_requirements.md` for full requirements.
