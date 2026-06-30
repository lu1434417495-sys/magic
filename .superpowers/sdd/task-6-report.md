# Task 6 Report: Static Equipment Ability Content ABI, Registry, And Validator

## Status

DONE_WITH_CONCERNS

Task 6 was implemented in the current branch/worktree. No separate worktree was created.

## Scope Completed

- Added V1 equipment ability authoring `Resource` ABI classes under `scripts/player/progression/equipment_abilities/`.
- Added plain C# runtime DTOs, registry build result, validation context, and handler spec metadata without retaining live `Resource` or `Godot.Collections.Dictionary` references in DTOs.
- Added built-in V1 condition/action handler specs for:
  - conditions: `has_status`, `compare_fact`, `has_equipment_tag`
  - actions: `add_damage_dice`, `apply_status`, `modify_ability_state`, `mark_target`, `grant_skill`, `equipment_durability_damage`
- Added `EquipmentAbilityContentRegistry` with deterministic pack rebuild, dependency/load-order sorting, revision tracking, read-only snapshots, replace-binding behavior, validation errors, and trait/source/item/equipment-type filtering.
- Integrated the static registry into `ProgressionContentRegistry`, `GameSession`, and `GameContentCatalog`.
- Added focused schema/lifecycle tests under the allowed test folders.
- Left `docs/design/project_context_units.md` unchanged because the implementation matched the documented progression/content ownership boundary and did not change recommended read sets.

## Files Changed

- `scripts/player/progression/equipment_abilities/EquipmentAbilityAuthoringDefs.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityBuiltInHandlerSpecs.cs`
- `scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs`
- `scripts/player/progression/ProgressionContentRegistry.cs`
- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/content/GameContentCatalog.cs`
- `tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- `tests/runtime/validation/run_game_root_content_catalog_regression.cs`
- `.superpowers/sdd/task-6-report.md`

Ignored unrelated pre-existing `.ralph/*` deletions as requested.

## TDD RED Summary

Initial focused runner before production code:

- Command: `godot --headless -s res://tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- Result: failed before the new C# test class could instantiate because the assembly had not yet been rebuilt with the new test class.

Initial compile after adding tests:

- Command: `dotnet build magic.csproj`
- Result: exited `1`.
- Expected failures: missing Task 6 production types including `EquipmentAbilityContentPackDef`, `EquipmentAbilityReactionDef`, `EquipmentAbilityBindingDef`, `EquipmentAbilityActionDef`, and `EquipmentAbilityContentValidationContext`.

Additional RED after adding strict timing coverage:

- Command: `dotnet build magic.csproj; godot --headless -s res://tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- Result: exited `1`.
- Expected failure: invalid `timing = "during_moonrise"` did not emit `EQA_TIMING_UNKNOWN_ID` for `bad.unknown_timing`.

## GREEN Verification

- Command: `dotnet build magic.csproj`
- Result: exited `0`; `0` warnings, `0` errors.

- Command: `godot --headless -s res://tests/progression/schema/run_equipment_ability_content_registry_regression.cs`
- Result: exited `0`; `Equipment ability content registry regression: PASS`.
- Note: Godot emitted `ObjectDB instances leaked at exit` warning after PASS.

- Command: `godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- Result: exited `0`; `Progression content registry typed regression: PASS`.

- Command: `godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs`
- Result: exited `0`; `Game root content catalog regression: PASS`.
- Note: Godot emitted exit-time unsafe reference/resource leak warnings after PASS.

## Constraint Check

- Static content only: no battle projection, runtime dispatcher, granted skill availability runtime, weapon overlay runtime, attack/defense modifier runtime, creature type projection, or battle-end commit pipeline was implemented.
- No compatibility migrations, fallback migrations, legacy aliases, or old payload/schema support were added.
- Empty official `data/configs/equipment_abilities` content succeeds without placeholder gameplay content.
- DTO ABI is guarded by tests to reject `[GlobalClass]`, `Resource` fields/properties, and `Godot.Collections.Dictionary` fields/properties.
- Overlay structures are projected as static DTO content; overlay application/runtime behavior remains out of scope.

## Concerns

- Final Godot headless runs pass with exit code `0`, but two runners emit existing-style Godot shutdown leak warnings. I did not expand this task into fixture ownership cleanup because assertions pass and the warnings are outside the static content ABI/registry scope.
