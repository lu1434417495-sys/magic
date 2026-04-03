# World Map

## Goal

Create a playable world map prototype with a square-grid strategy layout,
chunk-based structure, fog of war, roaming monsters, world NPCs, and
multi-tile settlements that open interaction windows instead of changing to an
interior map.

## Requirements

- The main game scene opens into a square-grid world map.
- The map has no impassable terrain tiles.
- Fog of war supports visible, explored, and unexplored states based on player
  vision.
- The map contains roaming monsters, world NPCs, and settlements.
- Settlements use tier-based footprints:
  - Village: `1x1`
  - Town: `2x2`
  - City: `2x2`
  - Capital: `3x3`
  - World Stronghold: `4x4`
- Settlement tier unlocks facility eligibility, but does not auto-generate
  facilities.
- Non-combat facility NPCs are bound to facilities and do not use the combat
  progression system.
- Clicking a visible settlement opens a window for facilities, services, and
  NPC interactions.

## Files To Touch

- `scenes/main/world_map.tscn`
- `scenes/ui/settlement_window.tscn`
- `scripts/systems/world_map_*.gd`
- `scripts/ui/world_map_view.gd`
- `scripts/ui/settlement_window.gd`
- `scripts/utils/*.gd`
- `data/configs/world_map/*.tres`
- `scenes/main/login_screen.tscn`

## Acceptance Checks

- The login flow enters the world map scene.
- The world map shows chunk structure, fog of war, and sample entities.
- All base tiles are passable.
- Settlement footprints match their tier sizes.
- Clicking a visible settlement opens a window instead of changing scenes.
- Facilities and service NPCs are displayed through the settlement window.
- Headless Godot validation succeeds with no scene or script errors.
