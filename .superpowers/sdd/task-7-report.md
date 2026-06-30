# Task 7 Report: Project Equipment Ability Sources To Battle Units

## Status

Complete on branch `codex/phantasmal-kill-tdd`.

## Scope Completed

- Added battle-unit equipment ability source state with strict payload schema, clone support, and source-kind validation.
- Added `BattleUnitState.equipment_ability_sources` and `BattleUnitState.creature_type_tags` as typed battle facts owned by the unit state.
- Added battle projection service for:
  - player persistent equipment sources from battle-local `EquipmentState` plus `effective_trait_instances`
  - enemy battle-only equipment sources from `EncounterRosterBuilder` template entry projection
  - creature type tags copied onto `BattleUnitState`
- Shared the equipment ability binding matcher between the static content registry and battle projection.
- Threaded trait defs and equipment ability bindings through `GameContentCatalog`, `GameRuntimeFacade`, `BattleRuntimeModule`, `BattleUnitFactory`, and `EncounterRosterBuilder`.
- Updated `docs/design/project_context_units.md` with the new runtime ownership and read-boundary constraints.

## Files Changed

- `scripts/systems/battle/core/BattleEquipmentAbilitySourceState.cs`
- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- `scripts/systems/world/EncounterRosterBuilder.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`
- `tests/battle_runtime/runtime/run_battle_unit_factory_weapon_projection_regression.cs`
- `tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs`
- `docs/design/project_context_units.md`
- `.superpowers/sdd/task-7-report.md`

Ignored unrelated pre-existing `.ralph/*` deletions.

## TDD RED Summary

Initial compile after adding tests:

- Command: `dotnet build magic.csproj`
- Result: exited `1`.
- Expected failures: missing `BattleEquipmentAbilitySourceState`, `EquipmentAbilitySourceKind`, `BattleUnitState.equipment_ability_sources`, `BattleUnitState.creature_type_tags`, projection service, and runtime/builder catalog parameters.

Additional expected compile failure during test shaping:

- Missing test alias for `Godot.Collections.Array<Godot.StringName>` in the encounter roster runner.

## GREEN Verification

- Command: `dotnet build magic.csproj`
- Result: exited `0`; `0` warnings, `0` errors.

- Command: `godot --headless -s res://tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`
- Result: exited `0`; `Battle unit state schema regression: PASS`.

- Command: `godot --headless -s res://tests/battle_runtime/runtime/run_battle_unit_factory_weapon_projection_regression.cs`
- Result: exited `0`; `Battle unit factory weapon projection regression: PASS`.
- Note: Godot emitted exit-time unsafe reference/resource leak warnings after PASS.

- Command: `godot --headless -s res://tests/battle_runtime/runtime/run_encounter_roster_builder_typed_boundary_regression.cs`
- Result: exited `0`; `Encounter roster builder typed boundary regression: PASS`.
- Note: Godot emitted exit-time unsafe reference/resource leak warnings after PASS.

- Command: `godot --headless -s res://tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- Result: exited `0`; `Equipment ability content registry regression: PASS`.
- Note: Godot emitted an exit-time ObjectDB leak warning after PASS.

- Command: `godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs`
- Result: exited `0`; `Game root content catalog regression: PASS`.
- Note: Godot emitted exit-time unsafe reference/resource leak warnings after PASS.

## Constraint Check

- Player projection reads battle-local equipment and battle-unit effective trait state, not persistent character equipment directly.
- Enemy projection copies attack-equipment ability sources and creature tags onto `BattleUnitState`; later rules must read the unit state, not the enemy template.
- `BattleRuntimeModule.setup(...)` new optional parameters are appended at the end to avoid breaking existing positional callers.
- No compatibility migrations, fallback migrations, legacy aliases, or old payload/schema support were added.
- No action dispatch, durability commit pipeline, granted skill availability runtime, or full equipment ability executor was implemented in this task.

## Concerns

- `BattleUnitState` save/schema payload now requires `equipment_ability_sources` and `creature_type_tags`. This intentionally keeps the strict schema policy, but it is a save-format surface and should be called out in PR notes.
- Focused Godot runners pass with exit code `0`, but several emit existing-style shutdown leak warnings. I did not expand this task into fixture ownership cleanup.
