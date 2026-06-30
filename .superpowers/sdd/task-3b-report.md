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
