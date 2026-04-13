# Repo Architecture

## Domain Ownership

- World runtime: `scripts/systems/world_map_system.gd` coordinates scene-level flow; prefer putting reusable rules into `world_map_*` services, `world_time_system.gd`, `world_map_spawn_system.gd`, `encounter_anchor_data.gd`, and `encounter_roster_builder.gd`.
- Battle runtime: `battle_runtime_module.gd` executes commands and batches; `battle_state.gd` and `battle_timeline_state.gd` own state; `battle_grid_service.gd` owns traversal and occupancy; `battle_ai_service.gd` chooses commands; `battle_preview.gd` validates intent before commit.
- UI: `scripts/ui/*.gd` and `scenes/ui/*.tscn` are mostly view or modal layers. Keep drawing and input there; keep reusable gameplay rules in services or state objects.
- Progression, equipment, and warehouse: `scripts/player/` contains player-facing state and definitions; cross-cutting mutations usually belong in `scripts/systems/`.
- Runtime/session entry points: `project.godot` autoloads `GameSession`; avoid spreading session ownership into unrelated nodes.

## Design Heuristics

- Start from the smallest owner that can hold the new rule.
- Treat `WorldMapSystem` as a coordinator, not the default home for every world rule.
- Treat `WorldMapView` as rendering and click translation, not gameplay state ownership.
- Treat `BattleRuntimeModule` as execution flow, not the place for every content-specific branch.
- Pair `.gd` changes with the owning `.tscn` or test harness whenever node paths, signals, or exported fields are involved.
- Keep headless snapshot and text-command paths stable when touching world or modal flows.

## Test Map

- Battle runtime and AI: `tests/battle_runtime/`
- Headless text runtime and startup flows: `tests/text_runtime/`
- Progression: `tests/progression/run_progression_tests.gd`
- Equipment: `tests/equipment/run_party_equipment_regression.gd`
- Warehouse: `tests/warehouse/run_party_warehouse_regression.gd`

Run only the relevant subset unless the change crosses domains.
