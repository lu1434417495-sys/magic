# World Map Battle Encounter

## Goal

Implement a battle encounter flow on the world map. When the player enters a
cell occupied by a wild monster, the world map stops rendering inside the main
game window and switches to a randomly generated battle map.

## Requirements

- Entering a wild monster cell on the world map triggers battle mode.
- While battle mode is active, the world map view is hidden and no longer
  renders in the main window.
- The battle map is generated randomly for each encounter.
- The battle map contains elevation differences and terrain types.
- Base terrain types include land, forest, and water.
- Water tiles must be enclosed by land/forest neighbors or by the map boundary
  on open sides.
- Land and forest use elevation values. Adjacent elevation difference `1` is
  passable, and elevation difference greater than `1` is allowed to generate.
- Forest tiles are passable.
- Additional terrain types include mud and spikes.
- Mud, spikes, and forest are passable.
- Mud and spikes consume double movement points.
- If the unit does not have enough remaining movement points to pay the full
  terrain cost, movement is blocked even if some movement points remain.

## Files To Touch

- `scenes/main/world_map.tscn`
- `scenes/ui/battle_map_panel.tscn`
- `scripts/systems/world_map_system.gd`
- `scripts/systems/battle_map_generation_system.gd`
- `scripts/ui/battle_map_panel.gd`
- `scripts/ui/battle_map_view.gd`
- `prompts/world_map_battle_encounter.md`

## Acceptance Checks

- Moving onto a wild monster cell enters battle mode from the world map.
- The world map view is hidden while battle mode is active.
- The battle map renders with random terrain, elevation, and water areas.
- Water tiles only border passable land/forest terrain or the map boundary.
- Forest is passable.
- Mud and spikes cost double movement points.
- A move into mud or spikes is rejected when remaining movement points are
  insufficient to pay the doubled cost.
- Height difference greater than `1` blocks movement, while difference `1`
  remains passable.
