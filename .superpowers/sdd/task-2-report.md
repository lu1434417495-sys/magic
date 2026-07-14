# Task 2 Report: Known-Skill Availability Parity

## Status

DONE_WITH_CONCERNS

## Summary of Changes

- Added `BattleSkillAvailabilityService` and DTO/view/access-result types for known-skill availability entries.
- Routed manual battle skill slot selection, selected-skill sync, selected preview/command construction, HUD skill slots, snapshot selection highlighting, and runtime preview access through exact `skill_entry_id` resolution.
- Kept known-only helpers reading `known_active_skill_ids` where the semantic remains long-term learned skills.
- Stamped existing known-skill AI/runtime command builders with `known_skill:{skill_id}` so current known-skill commands remain valid under the new runtime access gate without implementing equipment-granted AI availability.
- Updated the focused headless regression to prove known-skill order, entry ids, selection state, command identity, and stale-entry fail-closed behavior.
- Updated the project context map for the new availability owner/read boundary.

## Files Changed

- `.superpowers/sdd/task-2-report.md`
- `docs/design/project_context_units.md`
- `scripts/systems/battle/ai/BattleAiChargeActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiChargePathAoeActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiGroundSkillActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiMultiUnitSkillEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiRandomChainSkillEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- `scripts/systems/battle/runtime/BattleSkillAvailabilityService.cs`
- `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs`

## Failing Test Before Implementation

- Command: `dotnet build magic.csproj`
- Observed failure after adding the focused regression:
  - `tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs(...)`: `CS0246` for missing `BattleAvailableSkillEntry`.
- Note: the first `godot --headless -s res://tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs` attempt returned PASS from a stale C# assembly; `dotnet build` was the reliable red compile check.

## Passing Verification After Implementation

- `dotnet build magic.csproj`
  - Exit code 0, 0 warnings, 0 errors.
- `godot --headless -s res://tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs`
  - Exit code 0, `Battle skill entry identity regression: PASS`.
- `godot --headless -s res://tests/text_runtime/headless/run_text_command_party_battle_surface_regression.cs`
  - Exit code 0, `Text command party/battle surface regression: PASS`.

## Commit Hash

Recorded in final response after commit. A committed report cannot contain its own final Git object hash without changing that hash.

## Concerns

- Both final Godot headless runs exited 0 and printed PASS, but still emitted Godot shutdown leak messages (`Leaked unsafe reference`, `ObjectDB instances leaked`, `1 resources still in use`). This appears consistent with the runner noise observed during the red/green loop and was not introduced as a failing exit code by this task.

## Follow-up Fix: Authored Ground Reposition Known-Skill Entry

### Status

DONE_WITH_CONCERNS

### Summary of Changes

- Tightened the authored ground-reposition AI regression to require the selected known-skill command to carry `known_skill:{skill_id}`.
- Stamped the legacy authored ground-skill command builder in `EnemyAiAction` with `BattleSkillEntryIds.KnownSkill(skillId)`.
- Did not implement equipment-granted skills or full AI availability migration.

### Files Changed

- `.superpowers/sdd/task-2-report.md`
- `scripts/enemies/EnemyAiAction.cs`
- `tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs`

### Failing Test Before Fix

- `godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs`
  - Exit code 1.
  - Observed failures after rebuilding with the production stamp removed:
    - `blink reposition should stamp the known skill entry id for runtime access. | actual= expected=known_skill:mage_blink`
    - `blink reposition command should pass formal preview. logs=技能入口无效。`

### Passing Verification After Fix

- `dotnet build magic.csproj`
  - Exit code 0, 0 warnings, 0 errors.
- `godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs`
  - Exit code 0, `Battle AI ground reposition behavior regression: PASS`.
- `godot --headless -s res://tests/text_runtime/headless/run_battle_skill_entry_identity_regression.cs`
  - Exit code 0, `Battle skill entry identity regression: PASS`.

### New Commit Hash

Recorded in final response after commit. A committed report cannot contain its own final Git object hash without changing that hash.

### Concerns

- Godot headless runners can reuse stale C# assemblies if launched before `dotnet build magic.csproj` completes; the final focused AI regression was rerun sequentially after the successful build and passed.
- Godot headless verification still emits shutdown leak/resource warnings despite PASS and exit code 0, consistent with the Task 2 baseline runner noise.
