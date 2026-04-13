# Godot Review Checklist

## Pairing Rules

- If a `.tscn` file changes, inspect the paired `.gd` file and verify `@onready` node paths, exported properties, and signal names.
- If `project.godot` changes, inspect autoloads, main scene, display settings, and any startup assumptions.
- If `scripts/ui/*` changes, inspect the paired scene under `scenes/ui/` or `scenes/main/`.
- If `scripts/systems/world_*` changes, inspect nearby world-state owners such as `world_map_system.gd`, `world_map_spawn_system.gd`, `world_time_system.gd`, and `encounter_anchor_data.gd`.
- If `battle_*` changes, inspect `battle_runtime_module.gd`, `battle_state.gd`, `battle_grid_service.gd`, `battle_ai_service.gd`, `battle_preview.gd`, and `battle_command.gd` as needed.

## Failure Patterns To Prioritize

- Scene-script contract drift: renamed nodes, stale preloads, missing signal handlers, wrong type assumptions.
- Runtime state bugs: world or battle phase mismatches, modal visibility drift, stale caches, broken `StringName` or `Vector2i` keys.
- Combat regressions: occupancy not updated, AP or TU cost drift, cooldown drift, preview accepting commands that execution rejects.
- World regressions: encounter anchor lifecycle drift, fog or selection drift, world time growth running in the wrong owner.
- Performance hazards: repeated full-dictionary scans or object creation inside `_process`, `_draw`, or hot loops.
- Persistence hazards: changed dictionaries, missing defaults, or save data that older snapshots cannot rehydrate.

## Test Mapping

- Battle runtime or AI: `res://tests/battle_runtime/run_battle_runtime_smoke.gd`
- Battle AI behavior: `res://tests/battle_runtime/run_battle_runtime_ai_regression.gd`
- Board rendering or battle panel regressions: `res://tests/battle_runtime/run_battle_board_regression.gd`
- Progression and promotions: `res://tests/progression/run_progression_tests.gd`
- Equipment: `res://tests/equipment/run_party_equipment_regression.gd`
- Warehouse: `res://tests/warehouse/run_party_warehouse_regression.gd`
- Headless world/text flows: `res://tests/text_runtime/run_text_command_regression.gd`

Run the smallest relevant set, but call out missing coverage whenever a changed behavior has no matching regression test.
