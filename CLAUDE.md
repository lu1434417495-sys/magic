# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Running the Game

Open in Godot 4.6 editor and press **F5** (or run via CLI):

```bash
godot --path . scenes/main/login_screen.tscn
```

## Running Tests

Tests are standalone scripts runnable headlessly:

```bash
godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/battle_runtime/run_battle_board_regression.gd
godot --headless --script tests/progression/run_progression_tests.gd
godot --headless --script tests/warehouse/run_party_warehouse_regression.gd
```

There is no lint or build step — Godot parses GDScript at runtime.

## Directory Layout

From `AGENTS.md`:
- `scenes/main/` — entry scenes (login, world map)
- `scenes/ui/` + `scripts/ui/` — reusable UI and battle HUD
- `scenes/common/` — reusable scene templates (e.g. `battle_board_prop.tscn`)
- `scripts/systems/` — cross-cutting game logic (battle, world map, session)
- `scripts/player/` — character progression, warehouse state
- `scripts/utils/` — generation helpers, registries, configs
- `data/configs/` — `.tres` resource files for world presets, items, professions
- `data/saves/` — save files (binary `.dat`)
- `tests/` — regression scripts

## High-Level Architecture

### Two Runtime Modes

The game alternates between **exploration** (`WorldMapSystem`) and **battle** (`BattleRuntimeModule`). Both are orchestrated from `scenes/main/world_map.tscn`, which hosts both the `WorldMapView` and the `BattleMapPanel` (hidden until a battle starts).

### Autoload: GameSession

`scripts/systems/game_session.gd` is the only autoload. It holds:
- The active `PartyState` (member progression, warehouse)
- Content registries (`ProgressionContentRegistry`, `ItemContentRegistry`)
- Save/load orchestration via `ProgressionSerialization`
- World generation config reference

All state persistence flows through `GameSession.persist_game_state()`.

### State Container + Service Pattern

Game logic is split into pure data containers and stateless services:

- **Container**: `BattleState` holds cells, units, timeline — it never runs logic itself
- **Orchestrator**: `BattleRuntimeModule.advance(delta)` drives the battle loop each frame, delegating to services
- **Services**: `BattleGridService` (pathfinding/LOS), `BattleDamageResolver` (hit/damage calc), `BattleAiService` (enemy decisions), `BattleTerrainGenerator` (map generation)

The same pattern applies to progression: `PartyState` / `PartyMemberState` / `UnitProgress` are pure data; `ProgressionService`, `ProfessionRuleService`, `AttributeService` are stateless logic.

### Serialization

Every state object implements `to_dict() -> Dictionary` and `from_dict(d: Dictionary)`. `ProgressionSerialization` composes these into binary saves. Do not store logic in state objects — keep them as plain data carriers.

### Battle Rendering (TileMap Layer Stack)

The battle board (`scenes/ui/battle_board_2d.tscn`) uses a fixed set of `TileMapLayer` nodes managed by `BattleBoardController`:

| Layer group | Count | Purpose |
|---|---|---|
| `InputLayer` | 1 | Click hit detection via `local_to_map()` only — no visible tiles |
| `TopH0..H3` | 4 | Main terrain surface; `position.y = -height_index * 16px` |
| `CliffEastH1..H3` | 3 | East-facing cliff sides; `position.y = -(index+1) * 16px` |
| `CliffSouthH1..H3` | 3 | South-facing cliff sides |
| `OverlayH0..H3` | 4 | Mud, water, scrub, rubble overlays |
| `MarkerLayer` | 1 | Selection and skill-range preview tiles |
| `PropLayer` | 1 | `Node2D` (YSort) — tall independent prop scenes |
| `UnitLayer` | 1 | `Node2D` (YSort) — unit tokens |

**Data flow**: `BattleState.cells` → `BattleBoardController.configure()` → populates all layers. The TileSet is built at runtime from PNG files in `res://assets/main/battle/terrain/canyon/`. Tile variant selection uses a deterministic coord hash so the same seed always renders identically.

**Tile spec**: 64×32 isometric diamond. Top/overlay canvas: 64×64. Cliff canvas: 64×96. Height step: 16px.

**Cliff rendering**: `_draw_cliff_face()` iterates from source height down to neighbor height, placing 1-step cliff tiles per layer. Cliffs are never combined into multi-step tiles.

### Signal Flow

UI → system communication uses signals; system → UI uses direct method calls:

```
WorldMapView.cell_clicked → WorldMapSystem._on_cell_clicked()
BattleMapPanel.battle_cell_clicked → WorldMapSystem._on_battle_cell_clicked()
BattleMapPanel.battle_skill_slot_selected → WorldMapSystem._on_skill_slot_selected()
```

Systems call `BattleMapPanel.refresh(battle_state)` and `WorldMapView.set_runtime_state()` directly.

### Battle Command Lifecycle

1. Player selects skill → UI enters targeting mode → `_selected_battle_skill_id` stored in `WorldMapSystem`
2. Player clicks target → `BattleRuntimeModule.preview_command(cmd)` returns `{allowed, reason, state_delta}` → rendered as preview
3. Player confirms → `BattleRuntimeModule.issue_command(cmd)` mutates `BattleState`
4. `advance(delta)` processes the queued command, updates health/effects/timeline, returns `BattleEventBatch` for HUD

### Post-Battle Reward Pipeline

1. `BattleRuntimeModule` detects victory → creates `PendingCharacterReward` entries
2. Entries queued in `PartyState.pending_character_rewards`
3. `MasteryRewardWindow` displays them; user confirms
4. `CharacterManagementModule.apply_pending_character_reward()` mutates `PartyMemberState`
5. `GameSession.persist_game_state()` writes save

## Current Work Status (as of 2026-04-06)

From `NEXT_ACTION.md` — the TileMap rendering refactor is mostly complete:

- **Done**: `BattleBoard2D` layer skeleton, `BattleBoardController`, cliff/overlay rendering, canyon terrain profile, unit layer with y-sort
- **Partially done**: `PropLayer` — layer exists but tall props (tents, torches, spike barricades, objective markers) are not yet migrated to independent scenes
- **Pending**: Final canyon art TileSet (currently using runtime-generated placeholder textures), old `BattleMapView.gd` cleanup
- The old `BattleMapView` self-draw renderer is still in the repo but is no longer in the active runtime path
