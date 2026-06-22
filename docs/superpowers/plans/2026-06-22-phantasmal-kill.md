# Phantasmal Kill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the formal `mage_phantasmal_kill` 9-level ultimate illusion spell as a standard ground-targeted skill backed by a new typed per-target `graded_save_execute` effect.

**Architecture:** Content owns the skill shape and profile parameters; `BattleGradedSaveExecutionRules` owns pure grade/threshold/probability math; `BattleDamageResolver` owns per-target mutation through existing damage/status paths; `BattleGroundEffectService` continues to own ground-area iteration and kill submission; AI and HUD consume typed preview/score facts rather than parsing log text.

**Tech Stack:** Godot 4.6, C# runtime helpers and resources, `.tres` skill/enemy resources, standalone Godot headless C# regression runners.

## Global Constraints

- No compatibility aliases, fallback migrations, legacy payload support, or old skill IDs without explicit user approval.
- Current skill system supports `max_level = 9`; this skill uses `max_level = 9`, not `spell_rank`.
- `graded_save_execute` must be a normal effect type in `BattleEffectKind` / `BattleTypedNames`, not a hard-coded `skill_id == "mage_phantasmal_kill"` branch.
- `mage_phantasmal_kill` stays a ground skill with `target_team_filter = any`, `area_pattern = square`, `area_value = 3`, and no `special_resolution_profile_id`.
- Immunity is expressed through existing save-tag semantics such as `illusion_immunity`; immune save results must be handled before natural-roll or grade logic.
- Death and non-execute damage must go through existing typed damage application paths. Do not directly assign `target.current_hp = 0`.
- `lock_guard` is a new typed field on `CombatEffectDef` and `BattleStatusEffectState`; do not overload `guard_block`.
- Residual `params.lock_guard` must not drive runtime behavior.
- Avoid restoring Godot payload wrappers inside plain C# helpers. Internal chains stay typed; projection happens at existing boundary surfaces.
- Do not include battle simulation or balance runners in routine validation unless explicitly requested.

---

## File Map

**Effect schema and resources**
- Modify: `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/player/progression/SkillContentRegistry.cs`
- Create: `data/configs/skills/mage_phantasmal_kill.tres`

**Rules and runtime**
- Create: `scripts/systems/battle/rules/BattleGradedSaveExecutionRules.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Modify if helper visibility requires it: `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs`
- Modify if helper visibility requires it: `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectState.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectParams.cs` only to preserve existing guard-block behavior; do not parse `lock_guard` from residual params
- Modify: `scripts/systems/battle/core/BattleState.cs`
- Modify: `scripts/systems/battle/core/BattleSkillCastBlockReasonKind.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`

**Preview and AI**
- Modify: `scripts/systems/battle/core/BattlePreview.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify: `scripts/systems/battle/ai/BattleAiActionIntent.cs`
- Modify: `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- Modify: `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
- Modify: `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreInput.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProjection.cs`
- Modify: `scripts/enemies/EnemyAiAction.cs`
- Modify: `scripts/enemies/actions/UseGroundSkillAction.cs` only if existing soft friendly-fire limit tracing needs projection; do not add a hard reject for ordinary friendly targets

**Enemy template projection**
- Modify: `scripts/enemies/EnemyTemplateDef.cs`
- Modify: `scripts/systems/world/EncounterRosterBuilder.cs`

**Focused tests**
- Create: `tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`
- Create or extend: `tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs`
- Create: `tests/progression/schema/run_phantasmal_kill_schema_regression.cs`
- Create: `tests/battle_runtime/skills/run_phantasmal_kill_regression.cs`
- Create: `tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs`
- Create: `tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs`
- Create: `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- Create: `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`
- Modify: `tests/runtime/validation/run_resource_validation_regression.cs`

---

### Task 1: Register `graded_save_execute` and Schema Validation

**Files:**
- Modify: `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- Modify: `scripts/player/progression/SkillContentRegistry.cs`
- Create: `tests/progression/schema/run_phantasmal_kill_schema_regression.cs`

**Interfaces:**
- Produces: `BattleEffectKind.GradedSaveExecute`
- Produces: `BattleTypedNames.EffectGradedSaveExecute = "graded_save_execute"`
- Produces: `SkillContentRegistry.AppendGradedSaveExecuteValidationErrors(...)`

- [ ] Add `run_phantasmal_kill_schema_regression.cs` with these test methods:
  - `TestFormalPhantasmalKillShapePasses()`
  - `TestGradedSaveExecuteRejectsWrongSaveAndTargeting()`
  - `TestGradedSaveExecuteRejectsUnknownOrMissingParams()`
  - `TestPhantasmalKillRequiresNineLevelDescriptionCoverage()`

- [ ] In the test helper, build a valid skill with:
  - `skill_id = "mage_phantasmal_kill"`
  - `max_level = 9`
  - `non_core_max_level = 7`
  - `mastery_curve` length 9
  - `growth_tier = "ultimate"`
  - attribute growth budget `intelligence = 160`, `willpower = 80`
  - `combat_profile.target_mode = "ground"`
  - `combat_profile.target_team_filter = "any"`
  - `combat_profile.target_selection_mode = "single_coord"`
  - `combat_profile.range_value = 12`
  - `combat_profile.area_pattern = "square"`
  - `combat_profile.area_value = 3`
  - one `CombatEffectDef` with `effect_type = "graded_save_execute"` and `profile_id = "phantasmal_kill"` in params

- [ ] Run the schema runner and verify the expected initial failure:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

Expected initial failure: `graded_save_execute` is unsupported by effect kind/schema validation.

- [ ] In `BattleTypedEnums.cs`, add `GradedSaveExecute` to `BattleEffectKind`.

- [ ] In `BattleTypedNames`, add:
  - string constant `EffectGradedSaveExecute`
  - `ToEffectKind(...)` mapping
  - `ToStringName(BattleEffectKind.GradedSaveExecute)` mapping
  - `IsAiOffensiveEffect(...)`
  - `IsUnitPayloadEffect(...)`

- [ ] Keep `IsGroundPayloadEffect(...)` unchanged. The effect is a unit payload collected by a ground skill, not a ground-cell topology or terrain effect.

- [ ] In `SkillContentRegistry.AppendEffectValidationErrors(...)`, branch to `AppendGradedSaveExecuteValidationErrors(...)` when `effectDef.EffectKind == BattleEffectKind.GradedSaveExecute`.

- [ ] Implement validation requirements:
  - `effect_target_team_filter == "any"`
  - `damage_tag == "psychic"`
  - `save_dc_mode == "caster_spell"`
  - `save_dc == 0`
  - `save_dc_source_ability == "intelligence"`
  - `save_ability == "willpower"`
  - `save_tag == "illusion"`
  - `save_partial_on_success == false`
  - `params.profile_id == "phantasmal_kill"`
  - params key set is exactly the documented white list
  - all dice fields are positive integers
  - percentage fields are `1..100`
  - fixed threshold is `>= 0`
  - all duration fields are positive multiples of `TuGranularity`

- [ ] Add a skill-specific validation branch for `skill_id == "mage_phantasmal_kill"` requiring `level_description_configs` to cover string keys `"0"` through `"9"`. Keep the existing global contiguous-range rule unchanged for other skills.

- [ ] Re-run:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
dotnet build magic.csproj
```

Expected after implementation: schema runner and build pass. The formal resource load test belongs to Task 5 so each task can close on a green TDD cycle.

---

### Task 2: Pure Graded Save Rules

**Files:**
- Create: `scripts/systems/battle/rules/BattleGradedSaveExecutionRules.cs`
- Create: `tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs`

**Interfaces:**
- Produces: `GradedSaveExecutionGrade`
- Produces: `BattleGradedSaveExecutionProfile`
- Produces: `BattleGradedSaveGradeDistribution`
- Produces: `BattleGradedSaveExecutionRules.TryReadPhantasmalKillProfile(...)`
- Produces: `BattleGradedSaveExecutionRules.ResolveGrade(BattleSaveResult saveResult)`
- Produces: `BattleGradedSaveExecutionRules.ResolveFailureExecuteThreshold(...)`
- Produces: `BattleGradedSaveExecutionRules.ResolveCriticalFailureExecuteThreshold(...)`
- Produces: `BattleGradedSaveExecutionRules.EstimateGradeDistribution(...)`

- [ ] Create the regression runner with tests:
  - `TestImmuneSaveBecomesImmuneGrade()`
  - `TestNaturalOneDowngradesFailureToCriticalFailure()`
  - `TestNaturalTwentyUpgradesSuccessToCriticalSuccess()`
  - `TestFailureThresholdUsesMaxOfFixedAndPercent()`
  - `TestCriticalFailureThresholdUsesPercentOnly()`
  - `TestAverageDiceDamageUsesDiceMean()`
  - `TestGradeDistributionNormalAdvantageDisadvantage()`
  - `TestGradeDistributionImmuneIsAllImmune()`
  - `TestRollOverridesProduceDeterministicDistribution()`

- [ ] Run the rules runner and verify it fails because the new rules class does not exist:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs
```

- [ ] Create `BattleGradedSaveExecutionRules.cs` with concrete data contracts:

```csharp
internal enum GradedSaveExecutionGrade
{
    Immune,
    CriticalSuccess,
    Success,
    Failure,
    CriticalFailure,
}
```

```csharp
internal readonly record struct BattleGradedSaveExecutionProfile(
    StringName ProfileId,
    int FailureExecuteThresholdFixed,
    int FailureExecuteThresholdMaxHpPercent,
    int FailureDamageDiceCount,
    int FailureDamageDiceSides,
    int FailureFrightenedDurationTu,
    int FailureReactionLockDurationTu,
    int CriticalFailureExecuteThresholdMaxHpPercent,
    int CriticalFailureDamageDiceCount,
    int CriticalFailureDamageDiceSides,
    int CriticalFailureFrightenedDurationTu,
    int CriticalFailureStunnedDurationTu,
    int SuccessAftershockDurationTu
);
```

```csharp
internal readonly record struct BattleGradedSaveGradeDistribution(
    int ImmuneBasisPoints,
    int CriticalSuccessBasisPoints,
    int SuccessBasisPoints,
    int FailureBasisPoints,
    int CriticalFailureBasisPoints
);
```

- [ ] Implement `TryReadPhantasmalKillProfile(CombatEffectDef effectDef, out BattleGradedSaveExecutionProfile profile, out string error)` using typed conversions from `effectDef.@params`.

- [ ] Implement `ResolveGrade(BattleSaveResult saveResult)` with this order:
  - `saveResult.Immune` -> `Immune`
  - `saveResult.Degree == CriticalSuccess` -> `CriticalSuccess`
  - `saveResult.Degree == Success` -> `Success`
  - `saveResult.Degree == Failure` -> `Failure`
  - `saveResult.Degree == CriticalFailure` -> `CriticalFailure`

- [ ] Implement distribution by enumerating natural rolls:
  - normal: each natural roll 1..20 has weight 1
  - advantage: enumerate ordered pairs and select the higher roll
  - disadvantage: enumerate ordered pairs and select the lower roll
  - save roll overrides: use the selected override roll as a deterministic one-roll population

- [ ] Use `BattleSaveResolver.ResolveSaveDegree(...)` for grade math so runtime and AI remain consistent.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs
dotnet build magic.csproj
```

Expected: runner and build pass.

---

### Task 3: `lock_guard` and Phantasmal Status Semantics

**Files:**
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/player/progression/SkillContentRegistry.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectState.cs`
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `scripts/systems/battle/core/BattleState.cs`
- Modify: `scripts/systems/battle/core/BattleSkillCastBlockReasonKind.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`
- Create or extend: `tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs`

**Interfaces:**
- Produces: `[Export] public bool lock_guard { get; set; }` on `CombatEffectDef`
- Produces: `public bool lock_guard { get; set; }` on `BattleStatusEffectState`
- Produces: `BattleRuntimeSkillTurnResolver.HasGuardLockStatus(BattleUnitState unitState)`
- Produces: `BattleSkillCastBlockReasonKind.GuardLockedByStatus`

- [ ] Extend typed-field tests:
  - `params.lock_guard = true` on a residual status does not lock guard
  - `BattleStatusEffectState.lock_guard = true` does lock guard
  - `BattleStatusEffectState.ToDictionary()` / `FromDictionary()` preserves top-level `lock_guard`
  - `BattleRuntimeModule._set_runtime_status_effect(...)` can set `lock_guard`

- [ ] Extend status semantic tests:
  - `aftershock`, `reaction_lock`, `frightened`, `stunned` are defined semantics
  - all four count as harmful
  - all four are dispellable harmful unless `undispellable` is set on a concrete state
  - `aftershock`, `reaction_lock`, `frightened` use refresh semantics with no tick
  - phantasmal kill applies `stunned` immediately to the hit target and clears that target's current AP and move points in the same resolver pass

- [ ] Extend strong-attack disadvantage tests:
  - `frightened` causes strong attack disadvantage
  - existing `fear`, `feared`, and `terrified` remain in the list and are not remapped

- [ ] Run focused tests and verify initial failure:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs
```

- [ ] Add `lock_guard` to `CombatEffectDef` near the existing lock fields.

- [ ] Add `lock_guard` to `SkillContentRegistry.TypedEffectParamTargets` so content using `params.lock_guard` receives an error pointing to `CombatEffectDef.lock_guard`.

- [ ] Add `lock_guard` to `BattleStatusEffectState`:
  - `OptionalSchemaFields`
  - public field
  - `DuplicateState()`
  - `ToDictionary()` top-level projection when true
  - `FromDictionary()` top-level parse
  - object initializer return path

- [ ] Do not add `lock_guard` to `BattleStatusEffectState.FormalParamKeys`. Residual status params must stay residual and must not drive runtime semantics.

- [ ] In `BattleStatusSemanticTable`, add constants:
  - `STATUS_AFTERSHOCK = "aftershock"`
  - `STATUS_REACTION_LOCK = "reaction_lock"`
  - `STATUS_FRIGHTENED = "frightened"`
  - `STATUS_STUNNED = "stunned"`

- [ ] Add those status IDs to harmful, cleansable harmful, and dispellable harmful classification.

- [ ] Add `GetSemantic(...)` cases:
  - `aftershock`: refresh, max stack 1, no tick, label `余悸`
  - `reaction_lock`: refresh, max stack 1, no tick, label `反应封锁`
  - `frightened`: refresh, max stack 1, no tick, label `恐惧`
  - `stunned`: refresh, max stack 1, no tick, label `震慑`; this skill's resolver clears current AP and move points when applying the status

- [ ] In `BattleStatusSemanticTable.BuildMergedStatusEffectState(...)`, copy `effectDef.lock_guard` into `statusEntry.lock_guard`.

- [ ] In `BattleRuntimeSkillTurnResolver`, add the new status IDs to `DebuffStatusIds`.

- [ ] Implement `HasGuardLockStatus(BattleUnitState unit_state)` by scanning typed statuses for `statusEntry.lock_guard`.

- [ ] Update both `GetSkillCastBlockReason(...)` overloads:
  - keep the existing black-star guard lock branch and reason
  - add a typed status guard-lock branch when `_runtime._skill_grants_guarding(skill_def)` and `HasGuardLockStatus(...)`
  - return `BattleSkillCastBlockReasonKind.GuardLockedByStatus` for typed non-black-star locks

- [ ] Add `GuardLockedByStatus` to `BattleSkillCastBlockReasonKind`, trace-key projection, and formatted messages.

- [ ] Update `BattleRuntimeModule.IsUnitGuardLocked(...)` to return true when the unit has black-star brand or `_skill_turn_resolver.HasGuardLockStatus(unit_state)`.

- [ ] Add `lock_guard` to `_set_runtime_status_effect(...)` and any typed runtime status wrapper that forwards lock fields.

- [ ] Update `BattleState.StrongAttackDisadvantageStatusIdOrder` to include `frightened`.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs
dotnet build magic.csproj
```

Expected: runners and build pass.

---

### Task 4: Damage Resolver and Ground Effect Execution

**Files:**
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Modify if required: `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs`
- Modify if required: `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
- Create: `tests/battle_runtime/skills/run_phantasmal_kill_regression.cs`

**Interfaces:**
- Produces: `BattleDamageResolver.ResolveGradedSaveExecuteEffect(...)`
- Consumes: `BattleGradedSaveExecutionRules`
- Consumes: `BattleSaveResolver.ResolveSaveResult(...)`
- Consumes: existing `ApplyStatusEffect(...)` and typed damage application helpers

- [ ] Add `run_phantasmal_kill_regression.cs` with tests:
  - immune target is no-op and does not treat natural roll 0 as critical failure
  - critical success is no-op
  - success applies only `aftershock`
  - failure below `max(50, 25% max HP)` executes through damage event
  - failure above threshold deals `6d6 psychic`, applies `frightened` and `reaction_lock`
  - critical failure below `35% max HP` executes through damage event
  - critical failure above threshold deals `10d6 psychic`, applies `frightened` and `stunned`
  - death ward or last-stand style death prevention can intercept execute damage
  - psychic resistance or immunity affects non-execute damage but not save grade
  - 7x7 ground skill affects in-range enemies and allies, and ignores out-of-range units
  - repeated statuses refresh through `BattleStatusSemanticTable.MergeStatus(...)`

- [ ] Run the runner and verify initial failure:

```bash
godot --headless -s res://tests/battle_runtime/skills/run_phantasmal_kill_regression.cs
```

- [ ] In `BattleDamageResolver.ResolveEffectsTypedCore(...)`, add a `BattleEffectKind.GradedSaveExecute` branch next to execute/status/damage handling.

- [ ] In `BattleDamageResolver.Effects.cs`, implement the per-target resolver with this flow:
  - parse profile using `BattleGradedSaveExecutionRules.TryReadPhantasmalKillProfile(...)`
  - call `BattleSaveResolver.ResolveSaveResult(sourceUnit, targetUnit, effectDef, resolutionContext.ToBattleSaveContext())`
  - append `SaveResolutionFromBattleSave(saveResult)` to `saveResults`
  - resolve grade with `BattleGradedSaveExecutionRules.ResolveGrade(saveResult)`
  - immune or critical success: return no-op typed result with save result present
  - success: apply `aftershock` using a temporary `CombatEffectDef`
  - failure under threshold: apply fatal psychic damage equal to current HP
  - failure above threshold: roll and apply `6d6 psychic`, then apply `frightened` and `reaction_lock`
  - critical failure under threshold: apply fatal psychic damage equal to current HP
  - critical failure above threshold: roll and apply `10d6 psychic`, then apply `frightened` and `stunned`

- [ ] Build temporary status effects through `CombatEffectDef` and existing `ApplyStatusEffect(...)`:
  - `aftershock`: `status_id = "aftershock"`, `duration_tu = profile.SuccessAftershockDurationTu`, `lock_counterattack = true`, `lock_guard = true`
  - `reaction_lock`: `status_id = "reaction_lock"`, duration from profile, `lock_counterattack = true`, `lock_guard = true`
  - `frightened`: `status_id = "frightened"`, duration from profile, no reaction-lock fields
  - `stunned`: `status_id = "stunned"`, duration from profile, `lock_counterattack = true`, `lock_guard = true`; immediately set target current AP and move points to 0 after the status is applied

- [ ] Build execute damage with:
  - `damage_tag = "psychic"`
  - damage equal to current HP snapshot
  - `MinHpAfterDamage = 0`
  - `BypassShield = true`
  - `BypassDeathPrevention = false`
  - a new death source label for phantasmal kill rather than the Power Word Kill death source

- [ ] If the existing fatal execute helper is too specific to Power Word Kill, extract a generic helper that accepts `DamageTag` and `DeathResolutionContext` instead of reusing the PWK death source.

- [ ] Build non-execute damage through the existing damage outcome and mitigation path. Do not skip shield, mitigation, resistance, trait hooks, or damage events.

- [ ] Return a finalized `AttackEffectResolutionResult` with:
  - `Applied`
  - `Damage`
  - `HpDamage`
  - `ShieldAbsorbed`
  - `ShieldBroken`
  - `StatusEffectIds`
  - `DamageEvents`
  - `SaveResults`
  - `SkillId`

- [ ] Audit `BattleSkillExecutionOrchestrator` for any local `_is_unit_effect()` switch and add `GradedSaveExecute` if still present. Keep `BattleSkillResolutionRules` delegated to `BattleTypedNames.IsUnitPayloadEffect(...)`.

- [ ] Do not change `BattleGroundEffectService.ShouldResolveGroundEffectsAsAttack(...)`; phantasmal kill should resolve through `ResolveEffects(...)`, not attack-roll resolution.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/skills/run_phantasmal_kill_regression.cs
dotnet build magic.csproj
```

Expected: runner and build pass.

---

### Task 5: Formal `mage_phantasmal_kill.tres` Resource

**Files:**
- Create: `data/configs/skills/mage_phantasmal_kill.tres`
- Modify: `tests/progression/schema/run_phantasmal_kill_schema_regression.cs`
- Modify: `tests/runtime/validation/run_resource_validation_regression.cs`

**Interfaces:**
- Produces: formal skill resource loaded by `SkillContentRegistry`

- [ ] Add `TestFormalResourceLoadsAndValidates()` to `tests/progression/schema/run_phantasmal_kill_schema_regression.cs` first.

- [ ] Run the schema runner and verify the expected initial failure:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
```

Expected initial failure: `data/configs/skills/mage_phantasmal_kill.tres` is absent.

- [ ] Create the resource with C# resource scripts for `SkillDef`, `CombatSkillDef`, and `CombatEffectDef`.

- [ ] Set root skill fields:
  - `skill_id = &"mage_phantasmal_kill"`
  - `display_name = "怪影杀戮"`
  - `icon_id = &"mage_phantasmal_kill"`
  - `skill_type = &"active"`
  - `max_level = 9`
  - `non_core_max_level = 7`
  - `mastery_curve = PackedInt32Array(360, 900, 1980, 3600, 5760, 8600, 12000, 16000, 21000)`
  - `tags = Array[StringName]([&"mage", &"magic", &"illusion", &"fear", &"psychic", &"execute", &"output", &"control", &"ultimate"])`
  - `learn_source = &"book"`
  - `growth_tier = &"ultimate"`
  - `attribute_growth_progress = { "intelligence": 160, "willpower": 80 }`

- [ ] Set combat profile fields:
  - `target_mode = &"ground"`
  - `target_team_filter = &"any"`
  - `target_selection_mode = &"single_coord"`
  - `selection_order_mode = &"stable"`
  - `range_value = 12`
  - `area_pattern = &"square"`
  - `area_value = 3`
  - `ap_cost = 3`
  - `mp_cost = 2000`
  - `aura_cost = 2`
  - `cooldown_tu = 600`
  - `ai_tags = Array[StringName]([&"large_aoe", &"ultimate", &"execute", &"friendly_fire_risk"])`
  - `delivery_categories = Array[StringName]([&"spell", &"illusion", &"fear", &"psychic"])`
  - no `special_resolution_profile_id`

- [ ] Set the sole effect:
  - `effect_type = &"graded_save_execute"`
  - `effect_target_team_filter = &"any"`
  - `damage_tag = &"psychic"`
  - `save_dc_mode = &"caster_spell"`
  - `save_dc = 0`
  - `save_dc_source_ability = &"intelligence"`
  - `save_ability = &"willpower"`
  - `save_tag = &"illusion"`
  - `params.profile_id = "phantasmal_kill"`
  - documented threshold, dice, status duration params

- [ ] Write `level_description_template` and `level_description_configs` with keys `"0"` through `"9"`. Each entry must mention range, 7x7 area, willpower illusion save, failure/critical-failure execute thresholds, psychic damage dice, statuses, and friendly-fire risk.

- [ ] Run:

```bash
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
dotnet build magic.csproj
```

Expected: schema runner and resource validation pass.

---

### Task 6: Enemy Template Save-Tag Projection

**Files:**
- Modify: `scripts/enemies/EnemyTemplateDef.cs`
- Modify: `scripts/systems/world/EncounterRosterBuilder.cs`
- Create: `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs`
- Create: `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`

**Interfaces:**
- Produces: `[Export] public Godot.Collections.Array<StringName> save_advantage_tags { get; set; }`
- Produces: template-to-`BattleUnitState.save_advantage_tags` projection

- [ ] Add schema boundary tests:
  - valid `illusion_immunity` passes
  - valid direct `illusion` advantage tag passes
  - valid `illusion_advantage` and `illusion_disadvantage` pass
  - empty tag is rejected
  - unsupported base save tag is rejected
  - `skill_level_map` validation remains unchanged

- [ ] Add runtime start test:
  - build an enemy template with `save_advantage_tags = [&"illusion_immunity"]`
  - build units through `EncounterRosterBuilder.BuildEnemyUnitsTyped(...)`
  - assert the resulting unit contains `illusion_immunity`
  - assert `BattleSaveResolver.ResolveSaveResult(...)` for `save_tag = "illusion"` returns `Immune = true`

- [ ] Run both tests and verify initial failure:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs
```

- [ ] Add `save_advantage_tags` export to `EnemyTemplateDef`.

- [ ] In `EnemyTemplateDef.ValidateSchemaTyped(...)`, validate each tag:
  - tag is non-empty
  - strip optional suffix `_advantage`, `_disadvantage`, or `_immunity`
  - base tag is accepted by `BattleSaveContentRules.IsValidSaveTag(...)`

- [ ] In `EncounterRosterBuilder.BuildUnitsFromTemplate(...)`, after combat resource setup and before AI setup is complete, copy template tags into `unitState.save_advantage_tags`.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs
dotnet build magic.csproj
```

Expected: runners and build pass.

---

### Task 7: Player Preview and HUD Warning

**Files:**
- Modify: `scripts/systems/battle/core/BattlePreview.cs`
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Create: `tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs`

**Interfaces:**
- Produces: `BattlePreview.save_branch_preview` entries with `kind = "graded_save_execute"`
- Produces: HUD text for affected ally count, ally execute-risk count, immune count, and grade-risk summary

- [ ] Add preview regression with:
  - ground hover over a 7x7 area with enemies, allies, and an illusion-immune unit
  - preview payload includes target count by team
  - preview payload includes `friendly_affected_count`
  - preview payload includes `friendly_execute_risk_count`
  - preview payload includes `immune_count`
  - preview text mentions friendly-fire risk and immune/no-op summary

- [ ] Run and verify initial failure:

```bash
godot --headless -s res://tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs
```

- [ ] Add a ground preview builder in `BattleSkillExecutionOrchestrator` for effect sets containing `GradedSaveExecute`.

- [ ] For each preview target unit, use `BattleGradedSaveExecutionRules.EstimateGradeDistribution(...)` and current HP thresholds to compute:
  - `immune_count`
  - `critical_success_expected_count`
  - `success_aftershock_expected_basis_points`
  - `failure_execute_risk_count`
  - `critical_failure_execute_risk_count`
  - `friendly_affected_count`
  - `friendly_execute_risk_count`

- [ ] Store the aggregate in `preview.SetSaveBranchPreview(...)` using a `GDictionary` that includes:
  - `kind = "graded_save_execute"`
  - `profile_id = "phantasmal_kill"`
  - `save_tag = "illusion"`
  - `save_ability = "willpower"`
  - `friendly_affected_count`
  - `friendly_execute_risk_count`
  - `enemy_execute_risk_count`
  - `immune_count`
  - `summary_text`

- [ ] Keep `BattlePreview` as the owner of the copied dictionary. Do not expose a live mutable dictionary.

- [ ] In `BattleHudAdapter`, prefer the existing `save_branch_preview.summary_text` for selected-skill and hover text. Do not parse `log_lines`.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs
dotnet build magic.csproj
```

Expected: runner and build pass.

---

### Task 8: AI Scoring, Affordance, and Friendly Fire

**Files:**
- Modify: `scripts/systems/battle/ai/BattleAiActionIntent.cs`
- Modify: `scripts/systems/battle/ai/BattleAiActionAssembler.cs`
- Modify: `scripts/systems/battle/ai/BattleAiSkillAffordanceClassifier.cs`
- Modify: `scripts/systems/battle/ai/BattleAiTypedActionHelper.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreInput.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProjection.cs`
- Modify: `scripts/enemies/EnemyAiAction.cs`
- Modify if necessary: `scripts/enemies/actions/UseGroundSkillAction.cs`
- Create: `tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs`

**Interfaces:**
- Produces: graded-save execute target metrics in `BattleAiScoreInput`
- Consumes: existing `estimated_friendly_fire_target_count`, `estimated_friendly_lethal_target_count`, `maximum_friendly_fire_target_count`, and `allow_friendly_lethal`; `friendly_fire_reject_reason` remains reserved for non-configurable hard blocks

- [ ] Add AI regression with:
  - low-HP enemy in execute threshold receives higher score than high-HP enemy
  - illusion-immune target contributes no damage, no execute value, and no control value
  - save advantage lowers expected value; save disadvantage raises expected value
  - ally in affected area increments `estimated_friendly_fire_target_count`
  - ally in execute threshold increments `estimated_friendly_lethal_target_count`
  - default `UseGroundSkillAction` rejects a location with any affected ally
  - default `UseGroundSkillAction` rejects a location with friendly lethal risk
  - configured `maximum_friendly_fire_target_count >= affected ally count` allows non-lethal ally exposure
  - configured `allow_friendly_lethal = true` allows friendly lethal risk when other friendly-fire limits pass
  - non-immune enemy-only location remains selectable

- [ ] Run and verify initial failure:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs
```

- [ ] In `BattleAiSkillAffordanceClassifier`, treat `GradedSaveExecute` as:
  - damage
  - control
  - execute
  - hostile ground AOE when used by a ground skill

- [ ] In `BattleAiActionIntent.InferForSkill(...)`, ensure the effect contributes offensive intent even when the skill target filter is `any`.

- [ ] In `BattleAiScoreService.Effects.cs`, add target-effect metrics for `GradedSaveExecute`:
  - estimated failure damage using `6d6` average and grade distribution
  - estimated critical-failure damage using `10d6` average and grade distribution
  - execute kill probability for failure and critical-failure thresholds
  - control count for aftershock, frightened, reaction lock, and stunned weighted by grade distribution
  - no-op for immune targets

- [ ] Reuse `BattleGradedSaveExecutionRules.EstimateGradeDistribution(...)` and profile parsing. Do not duplicate save-grade math in AI.

- [ ] For allies:
  - increment `estimated_friendly_fire_target_count` when any non-immune ally is in target set
  - increment `estimated_friendly_lethal_target_count` when ally current HP is inside a failure or critical-failure execute threshold and the corresponding grade probability is non-zero
  - do not set `friendly_fire_reject_reason` for ordinary ally exposure or friendly lethal risk; those are soft-configurable through the existing action limits
  - set `friendly_fire_reject_reason` only for non-configurable invalid or unsafe cases outside the normal Phantasmal Kill friendly-fire risk model

- [ ] Update score projection and trace fingerprint for any new fields added to `BattleAiScoreInput`.

- [ ] Confirm `UseGroundSkillAction.PassesFriendlyFireLimits(...)` consumes existing counts through `maximum_friendly_fire_target_count` and `allow_friendly_lethal`. Only modify it if soft-limit trace projection is missing.

- [ ] Re-run:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs
dotnet build magic.csproj
```

Expected: runners and build pass.

---

### Task 9: Integration Validation and Context Map

**Files:**
- Modify if relationships changed: `docs/design/project_context_units.md`
- Read-only check: `docs/discussions/phantasmal_kill_design.md`

**Interfaces:**
- Produces: verified implementation branch matching the design document and repository context map

- [ ] Run focused phantasmal kill commands:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.cs
godot --headless -s res://tests/progression/schema/run_phantasmal_kill_schema_regression.cs
godot --headless -s res://tests/battle_runtime/skills/run_phantasmal_kill_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_typed_fields_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.cs
godot --headless -s res://tests/battle_runtime/rendering/run_phantasmal_kill_hover_preview_regression.cs
```

- [ ] Run broad affected regressions:

```bash
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs
python tests/run_regression_suite.py
dotnet build magic.csproj
```

- [ ] Search for accidental old/unsupported phantasmal paths:

```bash
rg -n "phantasmal_kill|graded_save_execute|lock_guard|fear|feared|terrified|spell_rank|special_resolution_profile_id|current_hp = 0" scripts data tests docs
```

Expected review result:
  - `spell_rank` has no active-code or new-resource match for this skill
  - `special_resolution_profile_id` is not set on `mage_phantasmal_kill`
  - `fear`, `feared`, and `terrified` are not used as new status IDs for this skill
  - any `current_hp = 0` match is from unrelated existing tests or death plumbing, not the new resolver

- [ ] Run whitespace check:

```bash
git diff --check
```

- [ ] Review `docs/design/project_context_units.md` against actual changes. Update only if the implementation changed owner relationships, runtime chains, or recommended read sets for battle skills, AI, enemy templates, status effects, or resources.

- [ ] Final acceptance criteria:
  - `mage_phantasmal_kill.tres` validates as a 9-level ultimate skill
  - 7x7 ground targeting affects enemies and allies through the same per-target effect
  - immune targets are no-op and never become critical failures from natural roll 0
  - all deaths flow through damage events and existing kill submission
  - `aftershock`, `reaction_lock`, `frightened`, and `stunned` have distinct runtime semantics
  - phantasmal kill's `stunned` status is applied immediately and clears current AP/move points in the same hit resolution
  - typed `lock_guard` blocks guard-granting skills without relying on `guard_block`
  - enemy `save_advantage_tags` projects into battle units
  - AI rejects default friendly-fire and friendly-lethal placements, while respecting explicit soft allowances
  - HUD preview exposes friendly risk, execute risk, and immune/no-op counts
  - focused tests, resource validation, regression suite, and build pass

---

## Self-Review

**Spec coverage:** The plan covers the confirmed decisions: 9-level skill support, formal status differences, new typed `lock_guard`, and reuse of `save_advantage_tags` for illusion immunity.

**Repository fit:** The work stays on current C# owners: `BattleTypedEnums`, `SkillContentRegistry`, `BattleDamageResolver`, `BattleGroundEffectService`, `BattleRuntimeSkillTurnResolver`, AI score helpers, `EnemyTemplateDef`, and `EncounterRosterBuilder`.

**Risk controls:** Tests isolate schema, pure rules, runtime damage/status mutation, enemy projection, AI friendly fire, preview/HUD, and full validation. No routine battle simulation runner is included.
