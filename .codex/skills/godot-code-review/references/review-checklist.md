# Godot Review Checklist

## Pairing Rules

- If a `.tscn` file changes, inspect the paired `.cs` script, or legacy `.gd` script if still attached, and verify `%`/`GetNode` paths, exported members, callable method names, and signal names.
- If `project.godot` changes, inspect autoloads, main scene, display settings, C# assembly assumptions, and any startup assumptions.
- If `scripts/ui/*.cs` changes, inspect the paired scene under `scenes/ui/` or `scenes/main/`.
- If world runtime changes, inspect nearby owners such as `scripts/systems/game_runtime/WorldMapSystem.cs`, `scripts/systems/world/WorldMapSpawnSystem.cs`, `scripts/systems/world/WorldTimeSystem.cs`, `scripts/systems/world/EncounterAnchorData.cs`, and `scripts/systems/world/EncounterRosterBuilder.cs`.
- If battle runtime, rules, or AI changes, inspect `scripts/systems/battle/runtime/BattleRuntimeModule.cs`, `scripts/systems/battle/core/BattleState.cs`, `scripts/systems/battle/core/BattleTimelineState.cs`, `scripts/systems/battle/terrain/BattleGridService.cs`, `scripts/systems/battle/ai/BattleAiService.cs`, `scripts/systems/battle/core/BattlePreview.cs`, and `scripts/systems/battle/core/BattleCommand.cs` as needed.

## Failure Patterns To Prioritize

- Scene-script contract drift: renamed nodes, stale resource paths, missing signal handlers, wrong C# node/resource type assumptions.
- Runtime state bugs: world or battle phase mismatches, modal visibility drift, stale typed caches, broken `StringName` or `Vector2I` keys.
- Combat regressions: occupancy not updated, AP or TU cost drift, cooldown drift, preview accepting commands that execution rejects.
- World regressions: encounter anchor lifecycle drift, fog or selection drift, world time growth running in the wrong owner.
- Performance hazards: repeated full-dictionary scans or object creation inside `_process`, `_draw`, or hot loops.
- Persistence hazards: changed save payloads, missing defaults, schema/version drift, or compatibility assumptions that need explicit user confirmation.
- GodotSharp hazards: public Godot projection dictionaries used as formal business state, `GodotObject.Call(...)` where a typed entry exists, stale `[GlobalClass]`/resource registrations, or disposing objects still owned by an active scene/runtime.
- Constraint hazards: new fixed modes, schema keys, tags, resource kinds, or option sets implemented as raw strings or duplicated whitelist sets instead of enum-backed typed conversion and validation.
- Contract hazards: multi-field requests/results passed through `GDictionary` or loose primitive tuples instead of a typed options/result/value object.

## Test Mapping

- C# compile surface: `dotnet build magic.csproj`
- Routine focused suite: `python tests/run_regression_suite.py`
- Battle runtime or rules: focused `.cs` runners under `tests/battle_runtime/runtime/`, `tests/battle_runtime/rules/`, or `tests/battle_runtime/skills/`
- Battle AI behavior: focused `.cs` runners under `tests/battle_runtime/ai/`
- Board rendering or battle panel regressions: `godot --headless --script tests/battle_runtime/rendering/run_battle_board_regression.cs`
- Progression and promotions: focused `.cs` runners under `tests/progression/core/` and `tests/progression/schema/`
- Equipment: `godot --headless --script tests/equipment/run_party_equipment_regression.cs`
- Warehouse: focused `.cs` runners under `tests/warehouse/`
- Headless world/text flows: focused `.cs` runners under `tests/text_runtime/headless/` and `tests/text_runtime/commands/`
- New enum/typed constraint: add or run the closest schema/contract regression that covers invalid values and boundary projection.

Run the smallest relevant set, but call out missing coverage whenever a changed behavior has no matching regression test.
