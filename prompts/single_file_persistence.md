# Single File Persistence

## Goal

Unify all runtime state that needs to survive a restart into a single save
file managed by the game session, while keeping fog of war as a purely
real-time computed system.

## Requirements

- All persistent runtime state must be serialized into one save file only.
- World state and player state must share the same root save payload.
- Player progression must be stored in the same save file as the world map
  state.
- Fog of war must be recalculated during runtime from current world state and
  player position, and must not be saved.
- UI and gameplay systems must not write separate persistence files directly.
- The current implementation does not need backward compatibility with older
  save formats.

## Files To Touch

- `scripts/systems/game_session.gd`
- `scripts/systems/progression_serialization.gd`
- `scripts/player/progression/*.gd`
- `scripts/systems/world_map_*.gd`
- `scripts/ui/login_screen.gd`
- `prompts/single_file_persistence.md`

## Acceptance Checks

- Starting a new game creates exactly one save file for runtime persistence.
- Reloading the game restores world state and player progression from that file.
- Moving on the world map still saves correctly through the unified session
  save.
- Fog of war is rebuilt at runtime and does not depend on persisted fog data.
- No additional world-state or player-state save files are created.
