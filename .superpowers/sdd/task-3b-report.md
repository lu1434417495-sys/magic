# Task 3B Report

## Status

DONE_WITH_CONCERNS

## Summary of changes

- Added an AI-facing `ResolveAvailableSkillEntries(...)` helper backed by `BattleSkillAvailabilityService` for known-skill entries with `AiPlanning` consumer.
- Routed acting-unit AI candidate/evaluator paths through `BattleAvailableSkillEntry` so generated skill commands carry the selected `SkillEntryId`.
- Updated AI generated-plan classification/signature and acting-unit affordance/cache/scoring paths to use availability entries and entry skill levels.
- Kept threat/opponent known-skill scans out of scope.
- Extended the focused unit-skill AI regression with command-builder entry-id assertions, availability filtering, and candidate command entry-id coverage.

## Files changed

- `scripts/enemies/EnemyAiActionHelper.cs`
- `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- `scripts/systems/battle/ai/BattleAiChargeActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiChargePathAoeActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiContext.cs`
- `scripts/systems/battle/ai/BattleAiGroundSkillActionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiMoveToMultiUnitSkillPositionEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiMultiUnitSkillEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiQueryService.cs`
- `scripts/systems/battle/ai/BattleAiRandomChainSkillEvaluator.cs`
- `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs`
- `scripts/systems/battle/ai/BattleAiScoreService.Projection.cs`
- `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- `scripts/systems/battle/ai/BattleAiUnitSkillCandidateEvaluator.cs`
- `tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs`
- `.superpowers/sdd/task-3b-report.md`

## Failing test command and observed failure before implementation

Command:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs
```

Observed failure:

- `unit command should carry the known-skill entry id. | actual= expected=known_skill:ai_helper_entry_probe`
- `ground command should carry the known-skill entry id. | actual= expected=known_skill:ai_helper_entry_probe`
- `BattleAiTypedActionHelper should expose ResolveAvailableSkillEntries for AI planning.`

## Passing verification commands after implementation

```bash
dotnet build magic.csproj
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs
godot --headless -s res://tests/static_analysis/run_contingency_autocast_no_known_spoof_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_runtime_action_plan_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_random_chain_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_charge_path_aoe_behavior_regression.cs
```

## Commit hash

See final response. The exact commit hash cannot be embedded in the committed report without changing the commit hash.

## Concerns

- Several Godot headless runners printed exit-time leaked-resource warnings while still exiting 0 and printing PASS. The static analysis runner and build were clean.
- `git diff --check` passed, but Git warned that `scripts/systems/battle/ai/BattleAiGroundSkillActionEvaluator.cs` will normalize CRLF to LF when touched.

## Review Fix: Authored Enemy Action Availability Routing

Commit: `b747c1b7 fix: route authored ai actions through availability`

The first task review rejected the implementation because authored enemy actions still bypassed `BattleSkillAvailabilityService`:

- `UseGroundRepositionSkillAction` iterated raw known skill ids and built ground commands from `skill_id`.
- `WaitAction` scanned `unit_state.known_active_skill_ids` and built preview commands from `skillDefinition.SkillId`.
- `EnemyAiAction` raw skill-id command helpers directly stamped `BattleSkillEntryIds.KnownSkill(skillId)`.

Fix applied:

- Added `EnemyAiAction._resolve_available_skill_entries(...)` backed by `BattleSkillAvailabilityService` with `Consumer = AiPlanning`.
- Routed `UseGroundRepositionSkillAction` through `BattleAvailableSkillEntry`, using `entry.SkillLevel` for ground option unlocks and `entry.EntryRef` for command construction.
- Routed `WaitAction` active-rest hostile-skill checks and preview commands through `BattleAvailableSkillEntry`.
- Changed raw `EnemyAiAction` skill-id command helpers to validate through availability before building commands, returning null if the skill id is stale/unavailable.
- Strengthened `run_battle_ai_unit_skill_candidate_evaluator_regression.cs` with entry-preservation assertions and source guards for the authored-action old paths.

Additional verification run after the fix:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_unit_skill_candidate_evaluator_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_ground_reposition_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_wait_behavior_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs
godot --headless -s res://tests/static_analysis/run_contingency_autocast_no_known_spoof_regression.cs
```

All commands above exited 0. The Godot runners for ground reposition, wait behavior, and contingency origin still print existing Resource ownership/leak diagnostics at process exit while reporting PASS.
