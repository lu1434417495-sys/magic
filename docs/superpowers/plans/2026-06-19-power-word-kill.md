# Power Word Kill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the partial execute implementation with the documented `execute v1 / Power Word Kill` protocol and add the formal `mage_power_word_kill` skill resource.

**Architecture:** PWK is a constrained single-target unit effect, not a general instant-death framework. Static content/schema owns legal effect shape, `BattleExecutionRules` owns pure HP-threshold planning, `BattleDamageResolver` owns mutation, runtime validation owns pre-cost target gates, HUD consumes structured preview data, and AI consumes structured execute metrics rather than presentation text.

**Tech Stack:** Godot 4.6, C# resources and runtime, `.tres` content resources, standalone Godot headless C# regression runners.

## Execution Status

- 2026-06-19: Tasks 1-10 have been implemented and verified in the working tree.
- Commit steps are intentionally deferred; no commit was created because this session did not receive an explicit commit request.
- The planned `run_battle_execute_save_branch_regression.cs`, old skill-protocol `.gd`, and old runtime-AI `.gd` runners do not exist in the current tree. Save-branch behavior is covered by `tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs`; action assembler coverage uses `tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.cs`; runtime selection ownership is covered by `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`.

## Global Constraints

- No compatibility handling, legacy aliases, fallback migrations, or old payload/schema support.
- Do not keep staged execute, `burst_damage`, `finisher_damage`, `shield_absorption_percent`, Boss non-lethal execute, or high-HP non-lethal execute.
- PWK threshold reads skill level and target max HP only; source attributes affect save DC through `save_dc_mode = "caster_spell"` only.
- PWK ignores `target_rank`, `boss_target`, and `fortune_mark_target`.
- PWK high-HP targets are rejected before AP / MP / cooldown cost consumption.
- PWK fatal execute bypasses shields without changing shield fields.
- PWK fatal execute uses tiered death-protection authority, not blanket `BypassDeathPrevention=true`.
- Low-HP execute survivors receive weak `soul_fracture`; high-HP invalid targets and dead targets do not.
- Player preview/HUD shows structured branch text and player-facing hit chance; AI uses `kill_probability_basis_points`.
- Do not add `skill_id == "mage_power_word_kill"` runtime branches; behavior is keyed by `effect_type = "execute"`.
- Do not run battle simulation or balance runners as routine validation.

---

## File Map

**Rules and data contracts**
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/player/progression/SkillContentRegistry.cs`
- Modify: `scripts/systems/battle/_interop/BattleTypedEnums.cs`
- Modify: `scripts/systems/battle/rules/BattleExecutionRules.cs`
- Modify: `scripts/systems/battle/rules/BattleDeathResolutionRules.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectState.cs`
- Modify: `scripts/systems/battle/core/AttackEffectResolutionResult.cs`
- Modify: `scripts/systems/battle/core/BattlePreview.cs`

**Runtime, UI, AI**
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`
- Modify: `scripts/systems/game_runtime/BattleSessionFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify: `scripts/ui/BattleHoverPreviewOverlay.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreInput.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProjection.cs`

**Content**
- Create: `data/configs/skills/mage_power_word_kill.tres`
- Modify if validation requires explicit ranking: `data/configs/enemies/*.tres`

**Focused tests**
- Modify: `tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs`
- Modify: `tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`
- Modify: `tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs`
- Modify: `tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs`
- Modify: `tests/battle_runtime/runtime/run_battle_validation_result_projection_regression.cs`
- Create: `tests/progression/schema/run_power_word_kill_execute_schema_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs`
- Create: `tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs`

---

### Task 1: Formal Execute Plan Contract

**Files:**
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/systems/battle/rules/BattleExecutionRules.cs`
- Modify: `tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs`

**Interfaces:**
- Consumes: `BattleUnitState.GetKnownSkillLevelTyped(StringName skillId)`, `AttributeService.ToStringName(AttributeIdKind.HpMax)`
- Produces: `BattleExecutionRuleParams.FromEffect(CombatEffectDef effectDef, StringName skillId)`, `BattleExecutionRules.ResolveThreshold(BattleUnitState sourceUnit, BattleUnitState targetUnit, BattleExecutionRuleParams parameters)`, `BattleExecutionRules.BuildExecutePlan(BattleUnitState sourceUnit, BattleUnitState targetUnit, BattleExecutionRuleParams parameters)`

- [ ] **Step 1: Rewrite execution-rules tests for the formal threshold contract**

Replace the old ability-mod and non-lethal assertions in `tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs` with these tests:

```csharp
private void TestThresholdUsesSkillLevelButNotAbility()
{
    BattleUnitState source = MakeUnit("execute_source", 100, 100);
    source.known_skill_level_map["mage_power_word_kill"] = 20;
    source.attribute_snapshot.SetValue("intelligence_modifier", 99);
    BattleUnitState target = MakeUnit("execute_target", 100, 36);
    BattleExecutionRuleParams parameters =
        BattleExecutionRuleParams.FromEffect(
            new CombatEffectDef
            {
                threshold_max_hp_ratio_percent = 20,
                threshold_level_anchor = 17,
                threshold_level_bonus_per_delta = 5,
                threshold_cap_max_hp_ratio_percent = 50,
                soul_fracture_duration_tu = 60,
                heal_multiplier_percent = 50,
                shield_gain_multiplier_percent = 50,
            },
            "mage_power_word_kill"
        );

    int threshold = BattleExecutionRules.ResolveThreshold(source, target, parameters);

    _test.Eq(threshold, 35, "PWK threshold = 20% max HP + skill-level bonus, ignoring ability mod.");
    target.current_hp = 35;
    BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(source, target, parameters);
    _test.True(plan.CanExecute, "current_hp <= formal threshold should execute.");
    _test.Eq(plan.FatalDamage, 35, "fatal damage should be current HP snapshot.");
}

private void TestZeroOrDeadTargetCannotExecute()
{
    BattleUnitState source = MakeUnit("execute_source", 100, 100);
    source.known_skill_level_map["mage_power_word_kill"] = 20;
    BattleUnitState target = MakeUnit("execute_target", 100, 0);
    BattleExecutionRuleParams parameters = BattleExecutionRuleParams.Defaults("mage_power_word_kill");

    BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(source, target, parameters);

    _test.False(plan.CanExecute, "dead/zero-HP targets should not enter low_hp_execute.");
    _test.Eq(plan.Branch, BattleExecutionRules.BranchInvalidTarget, "dead/zero-HP target is invalid.");
    _test.Eq(plan.FatalDamage, 0, "invalid target should not carry fatal damage.");
}
```

- [ ] **Step 2: Run the execution-rules regression and verify it fails**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs
```

Expected: FAIL, because current `ResolveThreshold()` still reads `threshold_ability_mod` and current `BuildExecutePlan()` allows `current_hp == 0`.

- [ ] **Step 3: Remove old execute-only fields from `CombatEffectDef`**

In `scripts/player/progression/CombatEffectDef.cs`, remove these exported fields:

```csharp
public bool staged_execution { get; set; }
public int burst_damage { get; set; } = 9999;
public int finisher_damage { get; set; } = 1;
public double shield_absorption_percent { get; set; } = 50.0;
public StringName threshold_ability_mod { get; set; } = "intelligence_modifier";
public int threshold_ability_mod_multiplier { get; set; } = 5;
public StringName soul_fracture_status_id { get; set; } = "soul_fracture";
public int boss_non_lethal_damage_max_hp_ratio_percent { get; set; } = 12;
public int boss_non_lethal_damage_floor { get; set; } = 25;
public int non_lethal_damage_ratio_percent { get; set; } = 30;
```

Keep these formal PWK fields:

```csharp
[Export] public int threshold_base_value { get; set; }
[Export] public int threshold_level_anchor { get; set; } = 17;
[Export] public int threshold_level_bonus_per_delta { get; set; } = 5;
[Export] public int threshold_max_hp_ratio_percent { get; set; } = 20;
[Export] public int threshold_cap_max_hp_ratio_percent { get; set; } = 50;
[Export] public int soul_fracture_duration_tu { get; set; }
[Export] public int heal_multiplier_percent { get; set; } = 100;
[Export] public int shield_gain_multiplier_percent { get; set; } = 100;
```

- [ ] **Step 4: Simplify `BattleExecutionRuleParams`**

In `scripts/systems/battle/rules/BattleExecutionRules.cs`, replace the record with:

```csharp
public readonly record struct BattleExecutionRuleParams(
    StringName SkillId,
    int ThresholdBaseValue,
    int ThresholdLevelAnchor,
    int ThresholdLevelBonusPerDelta,
    int ThresholdMaxHpRatioPercent,
    int ThresholdCapMaxHpRatioPercent,
    int SoulFractureDurationTu,
    int HealMultiplierPercent,
    int ShieldGainMultiplierPercent
)
{
    public static BattleExecutionRuleParams Defaults(StringName skillId = default) =>
        new(Normalize(skillId), 0, 17, 5, 20, 50, 0, 100, 100);

    public static BattleExecutionRuleParams FromEffect(CombatEffectDef effectDef, StringName skillId = default) =>
        new(
            Normalize(skillId),
            Math.Max(effectDef?.threshold_base_value ?? 0, 0),
            Math.Max(effectDef?.threshold_level_anchor ?? 17, 0),
            Math.Max(effectDef?.threshold_level_bonus_per_delta ?? 5, 0),
            Math.Max(effectDef?.threshold_max_hp_ratio_percent ?? 20, 0),
            Math.Max(effectDef?.threshold_cap_max_hp_ratio_percent ?? 50, 0),
            Math.Max(effectDef?.soul_fracture_duration_tu ?? 0, 0),
            Math.Clamp(effectDef?.heal_multiplier_percent ?? 100, 0, 100),
            Math.Clamp(effectDef?.shield_gain_multiplier_percent ?? 100, 0, 100)
        );

    private static StringName Normalize(StringName value) => value ?? new StringName("");
}
```

- [ ] **Step 5: Update threshold and plan logic**

In `BattleExecutionRules.ResolveThreshold()`, use:

```csharp
int skillLevel = 0;
if (!IsEmpty(parameters.SkillId) && sourceUnit != null)
    skillLevel = sourceUnit.GetKnownSkillLevelTyped(parameters.SkillId);

int levelBonus =
    Math.Max(skillLevel - parameters.ThresholdLevelAnchor, 0)
    * parameters.ThresholdLevelBonusPerDelta;
int targetMaxHp = Math.Max(GetAttributeValue(targetUnit, HpMax), 0);
int hpFloor = Math.Max(targetMaxHp * parameters.ThresholdMaxHpRatioPercent / 100, 0);
int rawThreshold = Math.Max(parameters.ThresholdBaseValue, hpFloor) + levelBonus;
if (targetMaxHp > 0)
    rawThreshold = Math.Max(rawThreshold, 1);
int cap = Math.Max(targetMaxHp * parameters.ThresholdCapMaxHpRatioPercent / 100, 0);
return cap > 0 ? Math.Min(rawThreshold, cap) : rawThreshold;
```

In `BuildExecutePlan()`, require a live target:

```csharp
if (targetUnit != null && targetUnit.is_alive && currentHp > 0 && currentHp <= threshold)
{
    return new BattleExecutePlan(
        BranchLowHpExecute,
        currentHp,
        maxHp,
        threshold,
        currentHp,
        true,
        BuildSoulFractureParams(parameters)
    );
}
```

Delete `ResolveNonLethalDamage()`.

- [ ] **Step 6: Run the execution-rules regression and build**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs
dotnet build magic.csproj
```

Expected: regression PASS, build PASS. Build failures should be only references to removed old execute fields; fix those references in the next tasks, not by restoring the fields.

- [ ] **Step 7: Commit**

```bash
git add scripts/player/progression/CombatEffectDef.cs scripts/systems/battle/rules/BattleExecutionRules.cs tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs
git commit -m "refactor: formalize power word kill execute plan"
```

---

### Task 2: Execute Schema Validation

**Files:**
- Modify: `scripts/player/progression/SkillContentRegistry.cs`
- Create: `tests/progression/schema/run_power_word_kill_execute_schema_regression.cs`

**Interfaces:**
- Consumes: `BattleEffectKind.Execute`, `BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute)`, `UnitBaseAttributes.INTELLIGENCE`, `UnitBaseAttributes.WILLPOWER`
- Produces: `SkillContentRegistry.ValidateTyped()` errors for invalid execute resources before runtime

- [ ] **Step 1: Add focused schema tests**

Create `tests/progression/schema/run_power_word_kill_execute_schema_regression.cs` with test methods:

```csharp
public override void _Initialize()
{
    TestFormalPwkShapePasses();
    TestExecuteRejectsWrongSaveAndTargeting();
    TestExecuteRejectsSiblingPassiveSpecialAndGroundShapes();
    TestExecuteRejectsOldFieldsAndHiddenSiblings();
    Quit(_test.Finish("Power Word Kill execute schema regression"));
}
```

The formal effect helper should return:

```csharp
private static CombatEffectDef FormalExecuteEffect() => new()
{
    effect_type = "execute",
    effect_target_team_filter = "enemy",
    damage_tag = "negative_energy",
    save_dc_mode = "caster_spell",
    save_dc = 0,
    save_dc_source_ability = "intelligence",
    save_ability = "willpower",
    save_tag = "execute",
    threshold_max_hp_ratio_percent = 20,
    threshold_level_anchor = 17,
    threshold_level_bonus_per_delta = 5,
    threshold_cap_max_hp_ratio_percent = 50,
    soul_fracture_duration_tu = 60,
    heal_multiplier_percent = 50,
    shield_gain_multiplier_percent = 50,
};
```

The formal skill helper should set:

```csharp
combat.target_mode = "unit";
combat.target_team_filter = "enemy";
combat.target_selection_mode = "single_unit";
combat.min_target_count = 1;
combat.max_target_count = 1;
combat.allow_repeat_target = false;
combat.area_pattern = "single";
combat.area_value = 0;
combat.effect_defs = new Godot.Collections.Array<CombatEffectDef> { FormalExecuteEffect() };
skill.skill_id = "mage_power_word_kill";
skill.combat_profile = combat;
```

- [ ] **Step 2: Run the schema test and verify it fails**

Run:

```bash
godot --headless --script tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
```

Expected: FAIL, because current schema only rejects passive execute and still allows invalid execute shapes.

- [ ] **Step 3: Add execute profile validation**

In `SkillContentRegistry.cs`, add a call near combat profile validation:

```csharp
AppendExecuteCombatProfileValidationErrors(errors, skillDef, combatProfile);
```

Implement local merge-shape validation:

```csharp
private void AppendExecuteCombatProfileValidationErrors(
    Array<string> errors,
    SkillDef skillDef,
    CombatSkillDef combatProfile
)
{
    if (skillDef == null || combatProfile == null)
        return;
    ValidateExecuteEffectSet(errors, skillDef.skill_id, combatProfile, null, "combat_profile.effect_defs");
    for (int i = 0; i < combatProfile.cast_variants.Count; i++)
        ValidateExecuteEffectSet(errors, skillDef.skill_id, combatProfile, combatProfile.cast_variants[i], $"combat_profile.cast_variants[{i}] merged effect_defs");
}
```

The helper must inspect `combat_profile.effect_defs` plus `cast_variant.effect_defs` in the same merge order runtime uses.

- [ ] **Step 4: Enforce single-target execute protocol**

In `ValidateExecuteEffectSet()`, if the merged set contains execute, assert:

```csharp
if (mergedEffects.Count != 1 || mergedEffects[0]?.EffectKind != BattleEffectKind.Execute)
    errors.Add($"Skill {skillId} {contextLabel} containing execute must contain exactly one execute effect and no sibling effects.");
```

Then assert owner profile fields:

```csharp
RequireStringName(errors, skillId, "combat_profile.target_mode", combatProfile.target_mode, "unit");
RequireStringName(errors, skillId, "combat_profile.target_team_filter", combatProfile.target_team_filter, "enemy");
RequireStringName(errors, skillId, "combat_profile.target_selection_mode", combatProfile.target_selection_mode, "single_unit");
RequireInt(errors, skillId, "combat_profile.min_target_count", combatProfile.min_target_count, 1);
RequireInt(errors, skillId, "combat_profile.max_target_count", combatProfile.max_target_count, 1);
RequireBool(errors, skillId, "combat_profile.allow_repeat_target", combatProfile.allow_repeat_target, false);
RequireStringName(errors, skillId, "combat_profile.area_pattern", combatProfile.area_pattern, "single");
RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.area_value, 0);
```

Reject `skillDef.special_resolution_profile_id != ""` when any execute exists.

- [ ] **Step 5: Enforce execute effect fields**

Add `AppendExecuteEffectValidationErrors()` called from `AppendEffectValidationErrors()` when `effectDef.EffectKind == BattleEffectKind.Execute`.

Required checks:

```csharp
RequireStringName(errors, skillId, $"{contextLabel}.effect_target_team_filter", effectDef.effect_target_team_filter, "enemy");
RequireStringName(errors, skillId, $"{contextLabel}.save_dc_mode", effectDef.save_dc_mode, BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell));
RequireInt(errors, skillId, $"{contextLabel}.save_dc", effectDef.save_dc, 0);
RequireStringName(errors, skillId, $"{contextLabel}.save_dc_source_ability", effectDef.save_dc_source_ability, UnitBaseAttributes.INTELLIGENCE);
RequireStringName(errors, skillId, $"{contextLabel}.save_ability", effectDef.save_ability, UnitBaseAttributes.WILLPOWER);
RequireStringName(errors, skillId, $"{contextLabel}.save_tag", effectDef.save_tag, BattleSaveContentRules.ToStringName(BattleSaveTagKind.Execute));
RequireStringName(errors, skillId, $"{contextLabel}.damage_tag", effectDef.damage_tag, "negative_energy");
RequireBool(errors, skillId, $"{contextLabel}.save_partial_on_success", effectDef.save_partial_on_success, false);
RequireStringName(errors, skillId, $"{contextLabel}.trigger_event", effectDef.trigger_event, "");
RequireStringName(errors, skillId, $"{contextLabel}.trigger_condition", effectDef.trigger_condition, "");
```

Validate ranges:

```csharp
RequireRange(errors, skillId, $"{contextLabel}.threshold_max_hp_ratio_percent", effectDef.threshold_max_hp_ratio_percent, 0, 100);
RequireRange(errors, skillId, $"{contextLabel}.threshold_cap_max_hp_ratio_percent", effectDef.threshold_cap_max_hp_ratio_percent, 0, 100);
if (effectDef.threshold_cap_max_hp_ratio_percent < effectDef.threshold_max_hp_ratio_percent)
    errors.Add($"Skill {skillId} effect {contextLabel} threshold_cap_max_hp_ratio_percent must be >= threshold_max_hp_ratio_percent.");
RequireRange(errors, skillId, $"{contextLabel}.heal_multiplier_percent", effectDef.heal_multiplier_percent, 0, 100);
RequireRange(errors, skillId, $"{contextLabel}.shield_gain_multiplier_percent", effectDef.shield_gain_multiplier_percent, 0, 100);
if (effectDef.soul_fracture_duration_tu <= 0 || effectDef.soul_fracture_duration_tu % 5 != 0)
    errors.Add($"Skill {skillId} effect {contextLabel} soul_fracture_duration_tu must be > 0 and divisible by 5.");
```

Reject non-empty `effectDef.@params` for execute:

```csharp
if (effectDef.@params != null && effectDef.@params.Count > 0)
    errors.Add($"Skill {skillId} effect {contextLabel} execute must not use params payload.");
```

- [ ] **Step 6: Remove old execute param mappings**

In `SkillContentRegistry.cs`, remove execute-only old field names from any promoted param map:

```csharp
"staged_execution"
"burst_damage"
"finisher_damage"
"shield_absorption_percent"
"threshold_ability_mod"
"threshold_ability_mod_multiplier"
"soul_fracture_status"
"soul_fracture_status_id"
"boss_non_lethal_damage_max_hp_ratio_percent"
"boss_non_lethal_damage_floor"
"non_lethal_damage_ratio_percent"
```

Do not add aliases for them.

- [ ] **Step 7: Run schema tests and build**

Run:

```bash
godot --headless --script tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
godot --headless --script tests/progression/schema/run_battle_save_skill_schema_regression.cs
dotnet build magic.csproj
```

Expected: both schema runners PASS, build PASS.

- [ ] **Step 8: Commit**

```bash
git add scripts/player/progression/SkillContentRegistry.cs tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
git commit -m "feat: validate power word kill execute schema"
```

---

### Task 3: Tiered Death-Protection Authority

**Files:**
- Modify: `scripts/player/progression/CombatEffectDef.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectState.cs`
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `scripts/systems/battle/rules/BattleDeathResolutionRules.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Modify: `tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs`

**Interfaces:**
- Produces: `CombatEffectDef.death_prevention_priority`, `BattleStatusEffectState.death_prevention_priority`
- Produces: `BattleDeathResolutionRules.CanDeathPreventionBlock(DeathResolutionContext deathContext, int protectionPriority)`

- [ ] **Step 1: Add death-priority tests**

In `run_battle_death_resolution_rules_regression.cs`, add:

```csharp
private void TestDeathProtectionPriorityComparison()
{
    DeathResolutionContext normal = BattleDeathResolutionRules.NormalFatalContext();
    DeathResolutionContext pwk = BattleDeathResolutionRules.PowerWordKillExecuteContext();

    _test.True(BattleDeathResolutionRules.CanDeathPreventionBlock(normal, 100), "priority 100 protection blocks normal fatal damage.");
    _test.False(BattleDeathResolutionRules.CanDeathPreventionBlock(pwk, 100), "priority 100 protection does not block PWK priority 900.");
    _test.True(BattleDeathResolutionRules.CanDeathPreventionBlock(pwk, 900), "priority 900 protection blocks PWK.");
}
```

- [ ] **Step 2: Add lethal runtime tests**

Create `tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs` with three cases:

```csharp
TestPwkSkipsLowPriorityDeathWard();
TestPwkAllowsHighPriorityDeathWard();
TestPwkBypassesShieldWithoutMutatingShieldFields();
```

Use a formal execute effect helper:

```csharp
private static CombatEffectDef MakeFormalExecuteEffect() => new()
{
    effect_type = "execute",
    effect_target_team_filter = "enemy",
    damage_tag = "negative_energy",
    save_dc_mode = "caster_spell",
    save_dc = 0,
    save_dc_source_ability = "intelligence",
    save_ability = "willpower",
    save_tag = "execute",
    threshold_max_hp_ratio_percent = 20,
    threshold_level_anchor = 17,
    threshold_level_bonus_per_delta = 5,
    threshold_cap_max_hp_ratio_percent = 50,
    soul_fracture_duration_tu = 60,
    heal_multiplier_percent = 50,
    shield_gain_multiplier_percent = 50,
};
```

For the high-priority protection case, set:

```csharp
target.SetStatusEffect(new BattleStatusEffectState
{
    status_id = "death_ward",
    source_skill_id = "test_high_death_ward",
    source_skill_level = 1,
    death_prevention_priority = 900,
});
```

- [ ] **Step 3: Run death tests and verify failures**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs
```

Expected: FAIL, because priority comparison and status priority are not implemented, and PWK currently uses `BypassDeathPrevention=true`.

- [ ] **Step 4: Add typed priority fields**

In `CombatEffectDef.cs`, add:

```csharp
[Export] public int death_prevention_priority { get; set; } = 100;
```

In `BattleStatusEffectState.cs`, add to `FormalParamKeys`:

```csharp
"death_prevention_priority",
```

Add property:

```csharp
public int? death_prevention_priority { get; set; }
```

Project and restore it wherever `heal_multiplier_percent` is projected/restored.

- [ ] **Step 5: Copy priority into merged status entries**

In `BattleStatusSemanticTable.BuildMergedStatusEffectState()`, add:

```csharp
statusEntry.death_prevention_priority = effectDef.death_prevention_priority;
```

- [ ] **Step 6: Add death-priority comparison**

In `BattleDeathResolutionRules.cs`, add:

```csharp
public static bool CanDeathPreventionBlock(
    DeathResolutionContext deathContext,
    int protectionPriority
)
{
    int normalizedProtection = Mathf.Max(protectionPriority, 0);
    int requiredPriority = deathContext.HasDeathSource
        ? Mathf.Max(deathContext.DeathSourcePriority, 0)
        : DeathPriorityNormalFatal;
    return normalizedProtection >= requiredPriority;
}
```

- [ ] **Step 7: Use death context instead of blanket bypass**

In `BuildFatalExecuteDamageInput()`, set:

```csharp
BypassDeathPrevention = false,
```

and create input with:

```csharp
bypassDeathPrevention: false,
```

In `ApplyDamageToTargetResult()`, build the death context from event fields:

```csharp
DeathResolutionContext deathContext = new(
    applicationEvent.DeathSource == "" ? BattleDeathResolutionRules.DamageDeathSource : applicationEvent.DeathSource,
    applicationEvent.DeathSourcePriority > 0 ? applicationEvent.DeathSourcePriority : 100
);
```

When checking death ward, replace unconditional `TriggerLastStand()` with:

```csharp
BattleStatusEffectState deathWard = targetUnit.GetStatusEffect("death_ward");
if (deathWard != null)
{
    int protectionPriority = deathWard.death_prevention_priority ?? 100;
    if (BattleDeathResolutionRules.CanDeathPreventionBlock(deathContext, protectionPriority))
    {
        targetUnit.MarkDead();
        if (!TriggerLastStand(targetUnit, sourceUnit))
            targetUnit.MarkDead();
    }
    else
    {
        targetUnit.EraseStatusEffect("death_ward");
        targetUnit.MarkDead();
    }
}
```

Do not reintroduce `BypassDeathPrevention=true` for PWK.

- [ ] **Step 8: Run death tests**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs
dotnet build magic.csproj
```

Expected: both runners PASS, build PASS.

- [ ] **Step 9: Commit**

```bash
git add scripts/player/progression/CombatEffectDef.cs scripts/systems/battle/core/BattleStatusEffectState.cs scripts/systems/battle/rules/BattleStatusSemanticTable.cs scripts/systems/battle/rules/BattleDeathResolutionRules.cs scripts/systems/battle/rules/BattleDamageResolver.cs scripts/systems/battle/rules/BattleDamageResolver.Effects.cs tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs
git commit -m "feat: add tiered death protection for execute"
```

---

### Task 4: Execute Mutation and Damage Application

**Files:**
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Modify: `scripts/systems/battle/core/AttackEffectResolutionResult.cs`
- Modify: `tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs`
- Create: `tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.cs`

**Interfaces:**
- Produces: execute result with `execute_stage`, `execute_outcome`, `save_results`, actual `status_effect_ids`, and damage events whose `Damage/HpDamage` are actual HP loss

- [ ] **Step 1: Rewrite old execute effect regression**

In `run_battle_execute_effect_regression.cs`, delete tests for:

```text
TestExecuteNonLethalOnHighHpTarget
TestExecuteNonLethalOnBossTarget
TestExecuteShieldEfficiency
TestExecuteMinHpNeverHeals
```

Replace them with:

```csharp
TestLowHpSaveFailureKillsWithCurrentHpDamage();
TestInvalidHighHpReturnsUnappliedWithoutSave();
TestSaveSuccessAppliesSoulFractureOnly();
```

- [ ] **Step 2: Add save-branch regression**

Create `run_battle_execute_save_branch_regression.cs` with:

```csharp
TestSaveSuccessNoDamageAndSoulFracture();
TestExecuteImmunityNoDamageAndSoulFracture();
TestDeadTargetDoesNotReceiveSoulFracture();
```

Use context dictionary overrides already supported by resolver:

```csharp
new GDictionary { ["save_roll_override"] = 20 }
new GDictionary { ["save_roll_override"] = 1 }
```

- [ ] **Step 3: Add damage mutation regression**

Create `run_battle_damage_resolver_mutation_regression.cs` with:

```csharp
TestBypassShieldDoesNotNormalizeOrClearExpiredShieldFields();
TestReportedHpDamageIsActualHpLossAfterClamp();
TestOrdinaryShieldAbsorptionStillDrainsShield();
```

For the bypass test, set:

```csharp
target.current_hp = 10;
target.current_shield_hp = 20;
target.shield_max_hp = 20;
target.shield_duration = -1;
```

After PWK fatal, assert shield fields are unchanged.

- [ ] **Step 4: Run mutation tests and verify failures**

Run:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.cs
```

Expected: FAIL because staged behavior, shield normalize, and old soul-fracture ordering remain.

- [ ] **Step 5: Delete staged execute branch**

In `BattleDamageResolver.Effects.cs`, delete:

```csharp
ResolveStagedExecuteEffect(...)
BuildStagedExecuteSoulFractureStatusEffect(...)
BuildStagedExecuteDamageInput(...)
```

In `ResolveExecuteEffect()`, remove:

```csharp
DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(effectDef);
if (parameters.StagedExecution) { ... }
```

The method should always use formal `BattleExecutionRules.BuildExecutePlan()`.

- [ ] **Step 6: Apply soul fracture after all low-HP branch outcomes**

In `ResolveExecuteEffect()`, after save success or fatal damage:

```csharp
if (targetUnit != null && targetUnit.is_alive && targetUnit.current_hp > 0)
{
    CombatEffectDef tempEffectDef = BuildSoulFractureStatusEffect(executePlan.SoulFractureParams);
    if (ApplyStatusEffect(targetUnit, sourceUnit, tempEffectDef, tempEffectDef.status_id))
        AddUnique(statusEffectIds, tempEffectDef.status_id);
}
```

Do not apply `soul_fracture` when `!executePlan.CanExecute`.

- [ ] **Step 7: Preserve shield fields for bypass damage**

In `ApplyDamageToTargetResult()`, move `targetUnit.NormalizeShieldState()` inside the `!bypassShield` branch:

```csharp
if (!bypassShield)
    targetUnit.NormalizeShieldState();
```

Do not call `NormalizeShieldState()` anywhere inside bypass-shield paths.

- [ ] **Step 8: Return actual HP loss**

At the start of positive damage application, capture:

```csharp
int hpBefore = Math.Max(targetUnit.current_hp, 0);
```

After HP/death resolution, calculate:

```csharp
int hpAfter = Math.Max(targetUnit.current_hp, 0);
int actualHpLost = Math.Max(hpBefore - hpAfter, 0);
```

Return:

```csharp
return BuildAppliedDamageResult(
    damageInput with { Event = applicationEvent },
    actualHpLost,
    shieldAbsorbed,
    shieldBroken
);
```

- [ ] **Step 9: Run mutation tests and build**

Run:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.cs
dotnet build magic.csproj
```

Expected: all PASS, build PASS.

- [ ] **Step 10: Commit**

```bash
git add scripts/systems/battle/rules/BattleDamageResolver.cs scripts/systems/battle/rules/BattleDamageResolver.Effects.cs scripts/systems/battle/core/AttackEffectResolutionResult.cs tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.cs
git commit -m "fix: resolve formal execute mutation"
```

---

### Task 5: Target Gate and Ground Protocol

**Files:**
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeBattleSelection.cs`
- Modify: `scripts/systems/game_runtime/BattleSessionFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs`

**Interfaces:**
- Produces: `GetUnitSkillTargetAffordance(...)` pass-through API
- Produces: pre-cost execute target validation for unit skills and ground skills

- [ ] **Step 1: Add target gate tests**

Create `run_battle_execute_target_gate_regression.cs` with:

```csharp
TestHighHpTargetPreviewDeniedWithoutSaveDamageOrStatus();
TestHighHpCommandDoesNotConsumeApMpCooldown();
TestLowHpTargetPreviewAllowed();
TestBossAndNormalUseSameThresholdGate();
```

The high-HP fixture should set target `current_hp = threshold + 1`, source AP/MP/cooldown known values, issue the command, and assert those values unchanged.

- [ ] **Step 2: Add ground protocol tests**

Create `run_battle_execute_ground_protocol_regression.cs` with:

```csharp
TestGroundExecutePreviewDenied();
TestGroundExecuteIssueDeniedBeforeCost();
TestGroundExecuteDoesNotMutateDamageOrStatus();
```

Use a ground `CombatSkillDef` with `target_mode = "ground"` and an execute effect in `effect_defs`.

- [ ] **Step 3: Run target/ground tests and verify failures**

Run:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs
```

Expected: FAIL because current high-HP gate is runtime-only and ground execute is not rejected consistently.

- [ ] **Step 4: Add single execute lookup helper**

In `BattleSkillExecutionOrchestrator.cs`, add:

```csharp
private (CombatEffectDef Effect, string ErrorMessage) FindSingleExecuteEffect(
    IReadOnlyList<CombatEffectDef> effectDefs
)
{
    CombatEffectDef found = null;
    foreach (CombatEffectDef effectDef in effectDefs ?? System.Array.Empty<CombatEffectDef>())
    {
        if (effectDef?.EffectKind != BattleEffectKind.Execute)
            continue;
        if (found != null)
            return (null, "律令死亡效果配置无效。");
        found = effectDef;
    }
    return (found, "");
}
```

- [ ] **Step 5: Add unit target validation**

In `_get_unit_skill_target_validation_message()` or the equivalent target-message helper, call:

```csharp
string executeMessage = GetExecuteTargetValidationMessage(activeUnit, targetUnit, skillDef, castVariant);
if (!string.IsNullOrEmpty(executeMessage))
    return executeMessage;
```

Implement:

```csharp
private string GetExecuteTargetValidationMessage(
    BattleUnitState activeUnit,
    BattleUnitState targetUnit,
    SkillDef skillDef,
    CombatCastVariantDef castVariant
)
{
    var lookup = FindSingleExecuteEffect(CollectUnitSkillEffectDefs(skillDef, castVariant, activeUnit));
    if (!string.IsNullOrEmpty(lookup.ErrorMessage))
        return lookup.ErrorMessage;
    if (lookup.Effect == null)
        return "";
    if (targetUnit == null)
        return "律令死亡目标无效。";
    if (!targetUnit.is_alive)
        return "";
    if (!IsUnitValidForEffect(activeUnit, targetUnit, skillDef.combat_profile.target_team_filter))
        return "";
    BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(
        lookup.Effect,
        skillDef.skill_id
    );
    BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(activeUnit, targetUnit, parameters);
    return plan.CanExecute ? "" : $"{targetUnit.display_name} 当前生命高于律令死亡阈值。";
}
```

Use the actual local helper names in `BattleSkillExecutionOrchestrator.cs`; do not duplicate range/AP/MP logic.

- [ ] **Step 6: Add target affordance pass-through**

Add an internal result shape in runtime or reuse a `GDictionary`:

```csharp
internal GDictionary GetUnitSkillTargetAffordance(
    BattleUnitState activeUnit,
    BattleUnitState targetUnit,
    SkillDef skillDef,
    CombatCastVariantDef castVariant,
    bool requireAp = true
)
{
    bool allowed = _can_skill_target_unit(activeUnit, targetUnit, skillDef, requireAp, castVariant);
    string reason = allowed ? "" : _get_unit_skill_target_validation_message(activeUnit, targetUnit, skillDef, castVariant);
    return new GDictionary { ["allowed"] = allowed, ["reason"] = reason };
}
```

Expose pure pass-through methods on `BattleRuntimeModule`, `BattleSessionFacade`, and `GameRuntimeFacade`.

- [ ] **Step 7: Make battle selection use affordance**

In `GameRuntimeBattleSelection._collect_valid_unit_skill_target_coords()` and preview command building, replace direct local eligibility decisions with the pass-through affordance. High-HP PWK targets must not enter selected valid target coords.

- [ ] **Step 8: Reject ground execute before cost**

In ground preview/validation and `_handle_ground_skill_command()`, inspect:

```csharp
foreach (CombatEffectDef effectDef in CollectGroundUnitEffectDefs(skillDef, castVariant, activeUnit))
{
    if (effectDef?.EffectKind == BattleEffectKind.Execute)
        return BattleGroundSkillValidationResult.Denied("地面技能不能携带律令死亡。");
}
```

Place this before cost consumption and before `preview.allowed = true`.

- [ ] **Step 9: Run target/ground tests and build**

Run:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs
dotnet build magic.csproj
```

Expected: both runners PASS, build PASS.

- [ ] **Step 10: Commit**

```bash
git add scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs scripts/systems/battle/runtime/BattleRuntimeModule.cs scripts/systems/battle/runtime/BattleGroundEffectService.cs scripts/systems/game_runtime/GameRuntimeBattleSelection.cs scripts/systems/game_runtime/BattleSessionFacade.cs scripts/systems/game_runtime/GameRuntimeFacade.cs tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs
git commit -m "feat: gate execute targets before cost"
```

---

### Task 6: Preview and HUD Hit Chance

**Files:**
- Modify: `scripts/systems/battle/core/BattlePreview.cs`
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify: `scripts/ui/BattleHoverPreviewOverlay.cs`
- Create: `tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs`

**Interfaces:**
- Produces: `BattlePreview.save_branch_preview` / `SaveBranchPreviewTyped`
- Produces HUD keys: `selected_skill_save_branch_preview_payload`, `selected_skill_save_branch_preview_text`, hover `save_branch_preview`, hover `save_branch_preview_text`

- [ ] **Step 1: Add HUD regression**

Create `run_battle_pwk_hover_preview_regression.cs` with:

```csharp
TestHighHpHoverHasNoBranchHitChanceOrDamageText();
TestLowHpHoverShowsBranchTextAndHitChance();
TestHudDoesNotParseLogLinesOrDamagePreviewText();
```

Poison:

```csharp
preview.AddLogLine("POISON_LOG");
preview.SetDamagePreview(new BattleDamagePreviewRangeService.SkillDamagePreview(true, 999, 999, new()));
```

Assert HUD consumes `save_branch_preview` instead.

- [ ] **Step 2: Run HUD regression and verify failure**

Run:

```bash
godot --headless --script tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs
```

Expected: FAIL because `BattlePreview` has no `save_branch_preview`.

- [ ] **Step 3: Add structured preview field**

In `BattlePreview.cs`, add:

```csharp
private GDictionary _saveBranchPreview = new();

public GDictionary save_branch_preview
{
    get => _saveBranchPreview.Duplicate(true);
    set => _saveBranchPreview = value != null ? value.Duplicate(true) : new GDictionary();
}

internal GDictionary SaveBranchPreviewTyped => _saveBranchPreview;

internal void ClearSaveBranchPreview()
{
    _saveBranchPreview.Clear();
}
```

Update `BattleAiPayloadGuard.ValidateNoForbiddenObject(BattlePreview value, ...)` to validate `preview.SaveBranchPreviewTyped`.

- [ ] **Step 4: Fill preview after low-HP validation**

In `_preview_unit_skill_command_impl()`, after validation succeeds and exactly one execute effect is found, build:

```csharp
int saveSuccessBps = BattleSaveResolver.EstimateSaveSuccessProbabilityBasisPoints(
    activeUnit,
    targetUnit,
    executeEffect,
    BuildSaveContextForPreview(skillDef, castVariant)
);
int hitBps = Mathf.Clamp(10000 - saveSuccessBps, 0, 10000);
preview.save_branch_preview = new GDictionary
{
    ["kind"] = "execute",
    ["plan_branch"] = BattleExecutionRules.BranchLowHpExecute,
    ["target_unit_id"] = targetUnit.unit_id,
    ["current_hp"] = plan.CurrentHp,
    ["max_hp"] = plan.MaxHp,
    ["threshold"] = plan.Threshold,
    ["save_tag"] = "execute",
    ["save_ability"] = executeEffect.save_ability,
    ["save_success_chance_basis_points"] = saveSuccessBps,
    ["hit_chance_basis_points"] = hitBps,
    ["failure_outcome_id"] = "fatal_execute",
    ["failure_text"] = "豁免失败：死亡律令",
    ["success_outcome_id"] = BattleStatusSemanticTable.STATUS_SOUL_FRACTURE,
    ["success_text"] = "豁免成功：灵魂裂解",
    ["summary_text"] = $"命中率 {hitBps / 100}%：豁免失败：死亡律令；豁免成功：灵魂裂解。",
};
```

Use the existing save probability owner; if the exact method name differs, add a public/internal method on `BattleSaveResolver` with this signature.

- [ ] **Step 5: Project preview into HUD snapshot**

In `BattleHudAdapter.BuildSnapshot()`, add:

```csharp
GDictionary saveBranchPreview = BuildSelectedSkillSaveBranchPreview(runtimePreview);
["selected_skill_save_branch_preview_payload"] = saveBranchPreview.Duplicate(true),
["selected_skill_save_branch_preview_text"] = DictString(saveBranchPreview, "summary_text"),
```

In `BuildHoverPreview()`, add:

```csharp
result["save_branch_preview"] = saveBranchPreview.Duplicate(true);
result["save_branch_preview_text"] = DictString(saveBranchPreview, "summary_text");
```

Only emit these for valid low-HP hover previews.

- [ ] **Step 6: Render branch text in hover overlay**

In `BattleHoverPreviewOverlay.ApplyPreview()`, prefer:

```csharp
string saveBranchText = DictString(preview, "save_branch_preview_text", "");
if (!string.IsNullOrEmpty(saveBranchText))
    detailLines.Add(saveBranchText);
```

Do not parse `log_lines` or `damage_text`.

- [ ] **Step 7: Run HUD regression and build**

Run:

```bash
godot --headless --script tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs
dotnet build magic.csproj
```

Expected: runner PASS, build PASS.

- [ ] **Step 8: Commit**

```bash
git add scripts/systems/battle/core/BattlePreview.cs scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs scripts/systems/battle/presentation/BattleHudAdapter.cs scripts/ui/BattleHoverPreviewOverlay.cs tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs
git commit -m "feat: show power word kill hit chance preview"
```

---

### Task 7: AI Execute Scoring

**Files:**
- Modify: `scripts/systems/battle/ai/BattleAiScoreInput.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Effects.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs`
- Modify: `scripts/systems/battle/ai/BattleAiScoreProjection.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs`

**Interfaces:**
- Produces: execute metrics with `kill_probability_basis_points` and `soul_fracture_applied`

- [ ] **Step 1: Add AI regression**

Create `run_battle_ai_score_execute_regression.cs` with:

```csharp
TestInvalidHighHpExecuteProducesNoSaveEstimateOrValue();
TestKillProbabilityUsesSaveFailureProbability();
TestExecuteImmunityZeroesKillProbabilityButKeepsSoulFractureValue();
TestDeathProtectionReducesKillProbability();
TestAiIgnoresPreviewPresentationText();
```

- [ ] **Step 2: Run AI regression and verify failure**

Run:

```bash
godot --headless --script tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs
```

Expected: FAIL because current execute metrics do not expose kill bps or soul fracture payoff.

- [ ] **Step 3: Extend target metrics**

In `BattleAiScoreService.Effects.cs`, add to `TargetEffectMetrics`:

```csharp
public bool IsExecute;
public int KillProbabilityBasisPoints;
public bool SoulFractureApplied;
```

Clone these fields in `Clone()`.

- [ ] **Step 4: Estimate execute through shared plan**

Add:

```csharp
private TargetEffectMetrics EstimateExecuteForTargetResult(
    BattleAiScoreInput scoreInput,
    IBattleAiScoreContext context,
    BattleUnitState sourceUnit,
    BattleUnitState targetUnit,
    CombatEffectDef effectDef
)
{
    BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(effectDef, scoreInput.skill_def.skill_id);
    BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(sourceUnit, targetUnit, parameters);
    if (!plan.CanExecute)
        return new TargetEffectMetrics { IsEmpty = true, IsExecute = true };

    DamageSaveEstimate saveEstimate = BuildDamageSaveEstimate(sourceUnit, targetUnit, effectDef, 0, scoreInput.skill_def.skill_id);
    int saveFailureBps = Mathf.Clamp(10000 - saveEstimate.SuccessProbabilityBasisPoints, 0, 10000);
    int protectionPenaltyBps = EstimateDeathProtectionPenaltyBasisPoints(targetUnit, plan);
    int killBps = Mathf.Clamp(saveFailureBps - protectionPenaltyBps, 0, 10000);

    return new TargetEffectMetrics
    {
        IsEmpty = false,
        IsExecute = true,
        Damage = plan.FatalDamage * saveFailureBps / 10000,
        PostSaveDamage = plan.FatalDamage * saveFailureBps / 10000,
        StableLethal = killBps >= 10000,
        KillProbabilityBasisPoints = killBps,
        SoulFractureApplied = true,
        HarmfulControlCount = 1,
        SaveEstimates = new List<DamageSaveEstimate> { saveEstimate },
    };
}
```

Use existing save estimate type/property names exactly as defined in `BattleAiScoreService`.

- [ ] **Step 5: Consume execute lethal bonus from kill bps**

In scoring, replace lethal check for execute effects:

```csharp
if (metrics.IsExecute)
{
    lethalBonus += ResolveExecuteLethalBonusFromBasisPoints(
        metrics.KillProbabilityBasisPoints,
        targetUnit
    );
}
else
{
    lethalBonus += ResolveLethalTargetBonus(metrics.Damage, targetUnit);
}
```

Add capped soul fracture value:

```csharp
if (metrics.SoulFractureApplied)
    controlScore += Math.Min(_scoreProfile.control_effect_weight, _scoreProfile.lethal_target_weight / 4);
```

- [ ] **Step 6: Project AI facts**

In `BattleAiScoreInput.cs`, add:

```csharp
public int execute_kill_probability_basis_points { get; set; }
public bool execute_soul_fracture_applied { get; set; }
```

In `BattleAiScoreProjection.cs`, project these fields for debugging.

- [ ] **Step 7: Run AI regression and build**

Run:

```bash
godot --headless --script tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs
dotnet build magic.csproj
```

Expected: runner PASS, build PASS.

- [ ] **Step 8: Commit**

```bash
git add scripts/systems/battle/ai/BattleAiScoreInput.cs scripts/systems/battle/ai/BattleAiScoreService.Effects.cs scripts/systems/battle/ai/BattleAiScoreService.Scoring.cs scripts/systems/battle/ai/BattleAiScoreProjection.cs tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs
git commit -m "feat: score execute by kill probability"
```

---

### Task 8: Soul Fracture Semantics

**Files:**
- Modify: `scripts/systems/battle/rules/BattleStatusSemanticTable.cs`
- Modify: `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs`
- Modify: `tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs`

**Interfaces:**
- Produces: formal `soul_fracture` semantic: harmful, cleansable, dispellable harmful, refresh, max stack 1, no tick, display label `灵魂裂解`

- [ ] **Step 1: Add semantic assertions**

In `run_status_effect_semantics_regression.cs`, add:

```csharp
private void TestSoulFractureSemantic()
{
    BattleStatusSemantic semantic = BattleStatusSemanticTable.GetSemantic(
        BattleStatusSemanticTable.STATUS_SOUL_FRACTURE
    );

    _test.True(semantic.Defined, "soul_fracture should have a formal semantic.");
    _test.Eq(semantic.StackBehavior, BattleStatusSemanticTable.STACK_REFRESH, "soul_fracture should refresh.");
    _test.Eq(semantic.StackLimit, 1, "soul_fracture max stack should be 1.");
    _test.Eq(semantic.TickMode, BattleStatusSemanticTable.TICK_NONE, "soul_fracture should not tick.");
    _test.Eq(semantic.DisplayLabel, "灵魂裂解", "soul_fracture should have player-facing label.");
    _test.True(BattleStatusSemanticTable.IsHarmfulStatus(BattleStatusSemanticTable.STATUS_SOUL_FRACTURE), "soul_fracture is harmful.");
    _test.True(BattleStatusSemanticTable.IsCleansableHarmfulStatus(BattleStatusSemanticTable.STATUS_SOUL_FRACTURE), "soul_fracture is cleansable harmful.");
    _test.True(BattleStatusSemanticTable.IsDispellableHarmfulStatus(BattleStatusSemanticTable.STATUS_SOUL_FRACTURE), "soul_fracture is dispellable harmful.");
}
```

- [ ] **Step 2: Run semantic tests and verify failure**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
```

Expected: FAIL because `GetSemantic("soul_fracture")` is currently default/undefined.

- [ ] **Step 3: Register semantic**

In `BattleStatusSemanticTable.GetSemantic()`, add before `return default;`:

```csharp
if (normalizedStatusId == STATUS_SOUL_FRACTURE)
{
    return RefreshSemantic(displayLabel: "灵魂裂解");
}
```

Do not add tick behavior or stacking beyond refresh / one stack.

- [ ] **Step 4: Run status tests**

Run:

```bash
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs
```

Expected: both runners PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/battle/rules/BattleStatusSemanticTable.cs tests/battle_runtime/rules/run_status_effect_semantics_regression.cs tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs
git commit -m "feat: register soul fracture status semantic"
```

---

### Task 9: Formal `mage_power_word_kill` Resource

**Files:**
- Create: `data/configs/skills/mage_power_word_kill.tres`
- Modify: `tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs`
- Modify: `tests/progression/schema/run_power_word_kill_execute_schema_regression.cs`

**Interfaces:**
- Produces: formal skill resource consumable through normal skill catalog loading

- [ ] **Step 1: Add formal resource test**

In `run_power_word_kill_execute_schema_regression.cs`, add:

```csharp
private void TestFormalResourceLoadsAndValidates()
{
    SkillDef skill = ResourceLoader.Load<SkillDef>("res://data/configs/skills/mage_power_word_kill.tres");
    _test.True(skill != null, "formal mage_power_word_kill resource should load.");
    using SkillContentRegistry registry = new();
    registry.RegisterSkillDefForTest(skill);
    IReadOnlyList<string> errors = registry.ValidateTyped();
    _test.Eq(errors.Count, 0, $"formal mage_power_word_kill should validate. errors={string.Join("; ", errors)}");
}
```

If `RegisterSkillDefForTest` does not exist, add an internal test helper to `SkillContentRegistry` that inserts a single `SkillDef` into the typed skill map and runs existing validation.

- [ ] **Step 2: Run resource test and verify failure**

Run:

```bash
godot --headless --script tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
```

Expected: FAIL because the resource does not exist.

- [ ] **Step 3: Create `mage_power_word_kill.tres`**

Create `data/configs/skills/mage_power_word_kill.tres` with C# script resources:

```ini
[gd_resource type="Resource" script_class="SkillDef" load_steps=4 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/CombatEffectDef.cs" id="1_effect"]
[ext_resource type="Script" path="res://scripts/player/progression/CombatSkillDef.cs" id="2_combat"]
[ext_resource type="Script" path="res://scripts/player/progression/SkillDef.cs" id="3_skill"]

[sub_resource type="Resource" id="execute"]
script = ExtResource("1_effect")
effect_type = &"execute"
effect_target_team_filter = &"enemy"
damage_tag = &"negative_energy"
save_dc_mode = &"caster_spell"
save_dc = 0
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"execute"
threshold_max_hp_ratio_percent = 20
threshold_level_anchor = 17
threshold_level_bonus_per_delta = 5
threshold_cap_max_hp_ratio_percent = 50
soul_fracture_duration_tu = 60
heal_multiplier_percent = 50
shield_gain_multiplier_percent = 50

[sub_resource type="Resource" id="combat"]
script = ExtResource("2_combat")
skill_id = &"mage_power_word_kill"
target_mode = &"unit"
target_team_filter = &"enemy"
target_selection_mode = &"single_unit"
range_pattern = &"single"
range_value = 12
area_pattern = &"single"
area_value = 0
requires_los = true
ap_cost = 1
mp_cost = 2000
cooldown_tu = 600
min_target_count = 1
max_target_count = 1
allow_repeat_target = false
selection_order_mode = &"stable"
ai_tags = Array[StringName]([&"execute", &"single_target", &"finisher"])
delivery_categories = Array[StringName]([&"spell", &"necromancy"])
effect_defs = Array[ExtResource("1_effect")]([SubResource("execute")])

[resource]
script = ExtResource("3_skill")
skill_id = &"mage_power_word_kill"
display_name = "律令死亡"
icon_id = &"mage_power_word_kill"
description = "以死亡律令裁决濒死敌人。仅可对生命不高于阈值的目标施放；目标进行意志豁免，失败则触发高权威死亡判定，低级免死难以拦截，成功或免疫时不受伤害但仍会短暂受到灵魂裂解。"
skill_type = &"active"
max_level = 5
non_core_max_level = 3
mastery_curve = PackedInt32Array(2400, 5200, 9600, 15000, 22000)
tags = Array[StringName]([&"mage", &"magic", &"necromancy", &"execute", &"single_target", &"output"])
learn_source = &"book"
growth_tier = &"ultimate"
attribute_growth_progress = {
"intelligence": 160,
"willpower": 80
}
combat_profile = SubResource("combat")
```

Use the existing cap ladder `non_core_max_level = 3` -> `max_level = 5`.

- [ ] **Step 4: Run resource/schema tests**

Run:

```bash
godot --headless --script tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
godot --headless --script tests/progression/schema/run_skill_attribute_growth_typed_regression.cs
dotnet build magic.csproj
```

Expected: both runners PASS, build PASS.

- [ ] **Step 5: Commit**

```bash
git add data/configs/skills/mage_power_word_kill.tres tests/progression/schema/run_power_word_kill_execute_schema_regression.cs tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
git commit -m "feat: add mage power word kill skill"
```

---

### Task 10: Final Validation and Documentation Check

**Files:**
- Modify if relationships changed: `docs/design/project_context_units.md`
- Read-only check: `docs/discussions/power_word_kill_design.md`

**Interfaces:**
- Produces: verified implementation branch matching the design document

- [ ] **Step 1: Run quick focused command set**

Run:

```bash
godot --headless --script tests/progression/schema/run_power_word_kill_execute_schema_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_execution_rules_contract_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_death_resolution_rules_regression.cs
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_lethal_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.cs
godot --headless --script tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.cs
godot --headless --script tests/battle_runtime/ai/run_battle_ai_score_execute_regression.cs
dotnet build magic.csproj
```

Expected: all commands exit 0.

- [ ] **Step 2: Run broad affected regressions**

Run:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_game_runtime_battle_selection_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs
godot --headless --script tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd
```

Expected: all commands exit 0. Do not run battle simulation or balance runners.

- [ ] **Step 3: Search for removed old execute paths**

Run:

```bash
rg -n "staged_execution|burst_damage|finisher_damage|shield_absorption_percent|threshold_ability_mod|threshold_ability_mod_multiplier|boss_non_lethal|non_lethal_damage_ratio|BypassDeathPrevention = true|bypassDeathPrevention: true" scripts tests data/configs/skills
```

Expected: no matches except documentation or explicitly unrelated migration notes. If matches remain in active code or tests, remove them in the owning task before proceeding.

- [ ] **Step 4: Check docs context map**

Open `docs/design/project_context_units.md`. If new runtime relationships or recommended read sets changed, update the relevant CU entries. Do not add parameter-by-parameter PWK notes there.

- [ ] **Step 5: Final diff review**

Run:

```bash
git diff --stat
git diff --check
```

Expected: `git diff --check` exits 0.

- [ ] **Step 6: Commit final validation/doc cleanup**

```bash
git add docs/design/project_context_units.md docs/discussions/power_word_kill_design.md
git commit -m "docs: align power word kill implementation context"
```

Skip this commit if neither document changed in this task.

---

## Self-Review

**Spec coverage:** The plan covers formal execute fields, schema gates, no compatibility, HP threshold with skill level, no attribute threshold, caster spell save DC, pre-cost high-HP rejection, ground rejection, shield bypass without shield mutation, actual HP loss, tiered death protection, `soul_fracture` semantics, player hit chance, AI kill probability, formal resource, and focused regressions.

**Placeholder scan:** The plan names concrete files, commands, checks, and expected outcomes for each task instead of leaving unfinished filler notes.

**Type consistency:** Produced interfaces use existing C# owners: `CombatEffectDef`, `BattleStatusEffectState`, `BattleExecutionRules`, `BattlePreview`, `BattleSkillExecutionOrchestrator`, `BattleHudAdapter`, and `BattleAiScoreService`.
