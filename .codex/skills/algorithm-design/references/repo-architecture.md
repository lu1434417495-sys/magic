# Repo Architecture

## Domain Ownership

- World runtime: `scripts/systems/game_runtime/WorldMapSystem.cs` coordinates scene-level flow; prefer putting reusable rules into `scripts/systems/world/*` services such as `WorldTimeSystem.cs`, `WorldMapSpawnSystem.cs`, `EncounterAnchorData.cs`, and `EncounterRosterBuilder.cs`.
- Battle runtime: `scripts/systems/battle/runtime/BattleRuntimeModule.cs` executes commands and batches; `scripts/systems/battle/core/BattleState.cs` and `BattleTimelineState.cs` own state; `scripts/systems/battle/terrain/BattleGridService.cs` owns traversal and occupancy; `scripts/systems/battle/ai/BattleAiService.cs` chooses commands; `scripts/systems/battle/core/BattlePreview.cs` validates intent before commit.
- UI: `scripts/ui/*.cs` and `scenes/ui/*.tscn` are mostly view or modal layers. Keep drawing and input there; keep reusable gameplay rules in services or state objects.
- Progression, equipment, and warehouse: `scripts/player/` contains player-facing typed state and definitions; cross-cutting mutations usually belong in `scripts/systems/`.
- Runtime/session entry points: `project.godot` autoloads `GameSession`; avoid spreading session ownership into unrelated nodes.

## Design Heuristics

- Start from the smallest owner that can hold the new rule.
- Treat `WorldMapSystem` as a coordinator, not the default home for every world rule.
- Treat `WorldMapView` as rendering and click translation, not gameplay state ownership.
- Treat `BattleRuntimeModule` as execution flow, not the place for every content-specific branch.
- Pair `.cs` UI changes with the owning `.tscn` or test harness whenever node paths, signals, callable names, or exported members are involved. Apply the same rule to legacy `.gd` files that still exist.
- Keep formal runtime state in typed C# owners and services. Use `Godot.Collections.Dictionary` / `Array` primarily for Godot resource, scene, save, or trace projection boundaries.
- Model closed value domains as C# enums or typed rule utilities first. Resource-facing `StringName` fields should decode through one typed owner, not through repeated string checks or duplicated whitelist sets.
- Model multi-field runtime constraints as small typed DTOs/value objects. Do not use `GDictionary` as the formal request, result, or validation shape when a typed class can express the contract.
- Keep headless snapshot and text-command paths stable when touching world or modal flows.

## Test Map

- Battle runtime and AI: `tests/battle_runtime/`
- Headless text runtime and startup flows: `tests/text_runtime/`
- Progression: `tests/progression/core/` and `tests/progression/schema/`
- Equipment: `tests/equipment/run_party_equipment_regression.cs`
- Warehouse: `tests/warehouse/`
- Compile baseline: `dotnet build magic.csproj`

Run only the relevant subset unless the change crosses domains.
