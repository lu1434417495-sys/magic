using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleRepeatAttackResolver : RefCounted
{
    public static readonly StringName REPEAT_ATTACK_EFFECT_TYPE = "repeat_attack_until_fail";
    public const int REPEAT_ATTACK_STAGE_GUARD = 32;
    public const int DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT = 3;
    public static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";

    private static readonly StringName DamageEffect = "damage";
    private static readonly string PreResistanceStage = "pre_resistance";

    private readonly record struct RepeatAttackRuntimeParameters(
        bool StopOnMiss,
        bool StopOnTargetDown,
        double FollowUpDamageMultiplier
    )
    {
        public static RepeatAttackRuntimeParameters FromEffect(CombatEffectDef effectDef)
        {
            GDictionary parameters = effectDef?.@params ?? new GDictionary();
            return new RepeatAttackRuntimeParameters(
                effectDef?.stop_on_miss ?? true,
                effectDef?.stop_on_target_down ?? true,
                Math.Max(GetFloat(parameters, "follow_up_damage_multiplier", 1.0), 1.0)
            );
        }

        public double GetStageDamageMultiplier(int stageIndex)
        {
            return stageIndex <= 0 ? 1.0 : Math.Pow(FollowUpDamageMultiplier, stageIndex);
        }

    }

    private WeakReference<BattleRuntimeModule> _runtimeRef;
    private WeakReference<BattleSkillMasteryService> _masteryRecorderRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    private BattleSkillMasteryService _masteryRecorder
    {
        get => ResolveWeakRef(_masteryRecorderRef);
        set =>
            _masteryRecorderRef =
                value != null ? new WeakReference<BattleSkillMasteryService>(value) : null;
    }

    public void setup(BattleRuntimeModule runtime, BattleSkillMasteryService mastery_recorder = null)
    {
        _runtime = runtime;
        _masteryRecorder = mastery_recorder;
    }

    public void dispose()
    {
        _runtime = null;
        _masteryRecorder = null;
    }

    public bool apply_repeat_attack_skill_result(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        GCombatEffectArray effect_defs,
        CombatEffectDef repeat_attack_effect,
        BattleEventBatch batch
    )
    {
        GCombatEffectArray stagedEffects = collect_repeat_attack_base_effects(effect_defs);
        if (
            active_unit == null
            || target_unit == null
            || skill_def == null
            || repeat_attack_effect == null
            || stagedEffects.Count == 0
        )
        {
            return false;
        }

        int totalDamage = 0;
        int totalHealing = 0;
        int totalKillCount = 0;
        int stageIndex = 0;
        bool anyAttackSucceeded = false;
        RepeatAttackRuntimeParameters repeatParameters =
            RepeatAttackRuntimeParameters.FromEffect(repeat_attack_effect);

        while (stageIndex < REPEAT_ATTACK_STAGE_GUARD && target_unit.is_alive)
        {
            if (
                stageIndex > 0
                && _runtime != null
                && _runtime.is_unit_follow_up_locked(active_unit)
            )
            {
                AppendLog(batch, $"{DisplayName(active_unit)} 受折手封印影响，无法继续追击。");
                break;
            }

            BattleRepeatAttackStageSpec stageSpec = BuildRuntimeStageSpec(
                active_unit,
                skill_def,
                repeat_attack_effect,
                stageIndex,
                0,
                true
            );
            int stageResourceCost = _get_repeat_attack_stage_cost(stageSpec);
            string costResourceLabel = _resolve_repeat_attack_resource_label(stageSpec);
            string costResourceAbbr = _resolve_repeat_attack_resource_abbr(stageSpec);
            if (stageIndex > 0)
            {
                if (!_can_pay_repeat_attack_stage_cost(active_unit, stageSpec))
                {
                    AppendLog(
                        batch,
                        $"{DisplayName(active_unit)} 的 {DisplayName(skill_def)} 在第 {stageIndex + 1} 段前{costResourceLabel}不足，连斩中止。"
                    );
                    break;
                }
                _consume_repeat_attack_stage_cost(active_unit, stageSpec);
                (_runtime as BattleRuntimeModule)?.append_changed_unit_id(batch, active_unit.unit_id);
            }

            double stageDamageMultiplier = _get_repeat_attack_stage_damage_multiplier(
                repeat_attack_effect,
                stageIndex
            );
            GCombatEffectArray stageEffects = _build_repeat_attack_stage_effects(
                stagedEffects,
                repeat_attack_effect,
                stageDamageMultiplier
            );
            AttackEffectResolutionResult stageResult = ResolveRepeatAttackStageResult(
                active_unit,
                target_unit,
                skill_def,
                repeat_attack_effect,
                stageSpec,
                stageIndex,
                stageEffects
            );

            int stageSuccessRate = stageResult.SuccessRatePercent;
            string stageResolutionText = string.IsNullOrEmpty(stageResult.ResolutionText)
                ? $"{stageSuccessRate}%"
                : stageResult.ResolutionText;
            if (!stageResult.AttackSuccess)
            {
                AppendLog(
                    batch,
                    $"{DisplayName(active_unit)} 的 {DisplayName(skill_def)} 第 {stageIndex + 1} 段未命中 {DisplayName(target_unit)}，{stageResolutionText}，{costResourceAbbr} 消耗 {stageResourceCost}。"
                );
                _runtime?.append_result_report_entry(batch, stageResult);
                if (repeatParameters.StopOnMiss)
                {
                    break;
                }
                stageIndex += 1;
                continue;
            }

            anyAttackSucceeded = true;
            if (_masteryRecorder != null)
            {
                _masteryRecorder.RecordTargetResult(
                    active_unit,
                    target_unit,
                    skill_def,
                    stageResult,
                    stageEffects
                );
                if (stageIndex >= 4)
                {
                    int bonusBase = (int)Mathf.Pow(2, stageIndex - 4);
                    _masteryRecorder.RecordBonus(active_unit, target_unit, skill_def, bonusBase);
                }
            }

            (_runtime as BattleRuntimeModule)?.mark_applied_statuses_for_turn_timing(
                target_unit,
                stageResult.StatusEffectIds
            );
            _runtime?.append_result_source_status_effects(
                batch,
                active_unit,
                stageResult
            );
            (_runtime as BattleRuntimeModule)?.append_changed_unit_id(batch, target_unit.unit_id);
            (_runtime as BattleRuntimeModule)?._append_changed_unit_coords(batch, target_unit);

            int damage = stageResult.Damage;
            int healing = stageResult.Healing;
            totalDamage += damage;
            totalHealing += healing;
            _runtime?.append_damage_result_log_lines(
                batch,
                $"{DisplayName(active_unit)} 的 {DisplayName(skill_def)} 第 {stageIndex + 1} 段，倍率 x{_format_runtime_multiplier(stageDamageMultiplier)}，{costResourceAbbr} 消耗 {stageResourceCost}，{stageResolutionText}",
                DisplayName(target_unit),
                stageResult
            );
            _runtime?.append_result_report_entry(batch, stageResult);

            if (healing > 0)
            {
                AppendLog(
                    batch,
                    $"{DisplayName(active_unit)} 的 {DisplayName(skill_def)} 第 {stageIndex + 1} 段为 {DisplayName(target_unit)} 恢复 {healing} 点生命。"
                );
            }

            foreach (var statusId in stageResult.StatusEffectIds ?? new Godot.Collections.Array<StringName>())
            {
                AppendLog(batch, $"{DisplayName(target_unit)} 获得状态 {statusId}。");
            }

            if (!target_unit.is_alive)
            {
                totalKillCount += 1;
                _runtime?.handle_unit_defeated_by_runtime_effect(
                    target_unit,
                    active_unit,
                    batch,
                    $"{DisplayName(target_unit)} 被击倒。",
                    new BattleDefeatHandlingOptions(recordEnemyDefeatedAchievement: true)
                );
                if (repeatParameters.StopOnTargetDown)
                {
                    break;
                }
            }

            if (damage > 0 || healing > 0 || !target_unit.is_alive)
            {
                _runtime?.record_battle_contribution_result(
                    active_unit,
                    target_unit,
                    damage,
                    healing,
                    !target_unit.is_alive,
                    new StringName("repeat"),
                    skill_def.skill_id
                );
            }

            stageIndex += 1;
        }

        if (stageIndex >= REPEAT_ATTACK_STAGE_GUARD && target_unit.is_alive)
        {
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 的 {DisplayName(skill_def)} 达到内部连斩保护上限后被强制中止。"
            );
        }

        return anyAttackSucceeded;
    }

    public CombatEffectDef get_repeat_attack_effect_def(GCombatEffectArray effect_defs)
    {
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (effectDef != null && effectDef.effect_type == REPEAT_ATTACK_EFFECT_TYPE)
            {
                return effectDef;
            }
        }
        return null;
    }

    public GCombatEffectArray collect_repeat_attack_base_effects(GCombatEffectArray effect_defs)
    {
        var stagedEffects = new GCombatEffectArray();
        BattleSkillResolutionRules resolutionRules =
            (_runtime as BattleRuntimeModule)?._skill_resolution_rules;
        foreach (CombatEffectDef effectDef in effect_defs ?? new GCombatEffectArray())
        {
            if (
                effectDef != null
                && resolutionRules != null
                && resolutionRules.is_unit_effect(effectDef)
            )
            {
                stagedEffects.Add(effectDef);
            }
        }
        return stagedEffects;
    }

    private BattleRepeatAttackStageSpec BuildRuntimeStageSpec(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index,
        int stage_count,
        bool fate_aware
    )
    {
        int skillLevel =
            _runtime != null
                ? _runtime._get_unit_skill_level(
                    active_unit,
                    skill_def?.skill_id ?? new StringName("")
                )
                : _resolve_static_skill_level(active_unit, skill_def);
        BattleRepeatAttackStageSpec spec =
            BattleRepeatAttackStageSpec.from_repeat_attack_effect(
                repeat_attack_effect,
                stage_index,
                stage_count,
                skillLevel,
                fate_aware
            );
        spec = spec.with_base_resource_cost(
            _get_repeat_attack_base_resource_cost(active_unit, skill_def, spec.cost_resource_kind)
        );
        return spec;
    }

    public static BattleRepeatAttackStageSpec build_stage_spec_from_repeat_attack_effect(
        GodotObject active_unit,
        GodotObject skill_def,
        GodotObject repeat_attack_effect,
        int stage_index,
        int stage_count,
        bool fate_aware
    )
    {
        int skillLevel = _resolve_static_skill_level(active_unit, skill_def);
        BattleRepeatAttackStageSpec spec = BattleRepeatAttackStageSpec.from_repeat_attack_effect(
            repeat_attack_effect as CombatEffectDef,
            stage_index,
            stage_count,
            skillLevel,
            fate_aware
        );
        SkillDef skillDef = skill_def as SkillDef;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        GDictionary effectiveCosts = GetEffectiveResourceCosts(combatProfile, skillLevel);
        spec = spec.with_base_resource_cost(
            _get_repeat_attack_preview_base_cost(skill_def, spec.cost_resource_kind, effectiveCosts)
        );
        return spec;
    }

    public static List<BattleRepeatAttackStageSpec> build_stage_specs_from_repeat_attack_effect(
        GodotObject active_unit,
        GodotObject skill_def,
        GodotObject repeat_attack_effect,
        int preview_stage_count,
        bool fate_aware
    )
    {
        var specs = new List<BattleRepeatAttackStageSpec>();
        if (active_unit == null || skill_def == null || repeat_attack_effect == null)
        {
            return specs;
        }
        int resolvedStageCount = preview_stage_count;
        if (resolvedStageCount <= 0)
        {
            resolvedStageCount = resolve_repeat_attack_preview_stage_count(
                active_unit,
                skill_def,
                repeat_attack_effect
            );
        }
        int normalizedStageCount = Math.Min(
            Math.Max(resolvedStageCount, 1),
            REPEAT_ATTACK_STAGE_GUARD
        );
        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            specs.Add(
                build_stage_spec_from_repeat_attack_effect(
                    active_unit,
                    skill_def,
                    repeat_attack_effect,
                    stageIndex,
                    normalizedStageCount,
                    fate_aware
                )
            );
        }
        return specs;
    }

    public static int resolve_repeat_attack_preview_stage_count(
        GodotObject active_unit,
        GodotObject skill_def,
        GodotObject repeat_attack_effect
    )
    {
        SkillDef skillDef = skill_def as SkillDef;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (
            active_unit == null
            || skill_def == null
            || combatProfile == null
            || repeat_attack_effect == null
        )
        {
            return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT;
        }
        if ((active_unit as BattleUnitState)?.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND) == true)
        {
            return 1;
        }

        BattleRepeatAttackStageSpec firstStageSpec = build_stage_spec_from_repeat_attack_effect(
            active_unit,
            skill_def,
            repeat_attack_effect,
            0,
            0,
            false
        );
        int baseCost = firstStageSpec.base_resource_cost;
        if (baseCost <= 0)
        {
            return REPEAT_ATTACK_STAGE_GUARD;
        }

        int remainingResource = _get_unit_resource_value(
            active_unit,
            firstStageSpec.cost_resource_kind
        );
        if (remainingResource < baseCost)
        {
            return 1;
        }
        remainingResource -= baseCost;
        int stages = 1;
        while (stages < REPEAT_ATTACK_STAGE_GUARD)
        {
            int nextStageCost = firstStageSpec.resolve_resource_cost_for_stage(stages);
            if (nextStageCost > 0 && remainingResource < nextStageCost)
            {
                break;
            }
            remainingResource -= nextStageCost;
            stages += 1;
        }
        return stages;
    }

    public static int _resolve_static_skill_level(GodotObject active_unit, GodotObject skill_def)
    {
        if (active_unit == null || skill_def == null)
        {
            return 0;
        }
        BattleUnitState activeUnit = active_unit as BattleUnitState;
        SkillDef skillDef = skill_def as SkillDef;
        return activeUnit != null && skillDef != null
            ? GetInt(activeUnit.known_skill_level_map, skillDef.skill_id, 0)
            : 0;
    }

    public static int _get_repeat_attack_preview_base_cost(
        GodotObject skill_def,
        CombatResourceKind cost_resource_kind,
        GDictionary effective_costs
    )
    {
        SkillDef skillDef = skill_def as SkillDef;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        string costField = CombatResourceKindUtils.ToEffectiveCostField(cost_resource_kind);
        if (string.IsNullOrEmpty(costField))
        {
            return 0;
        }
        return GetInt(effective_costs, costField, GetCombatProfileCost(combatProfile, cost_resource_kind));
    }

    public static int _get_unit_resource_value(
        GodotObject active_unit,
        CombatResourceKind cost_resource_kind
    )
    {
        if (active_unit == null)
        {
            return 0;
        }
        string currentField = CombatResourceKindUtils.ToCurrentUnitField(cost_resource_kind);
        if (string.IsNullOrEmpty(currentField))
        {
            return 0;
        }
        return GetUnitResourceValue(active_unit as BattleUnitState, cost_resource_kind);
    }

    private AttackEffectResolutionResult ResolveRepeatAttackStageResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        BattleRepeatAttackStageSpec stage_spec,
        int stage_index,
        GCombatEffectArray stage_effects
    )
    {
        BattleRuntimeModule runtime = _runtime as BattleRuntimeModule;
        BattleState battleState = runtime?.get_state();
        BattleAttackCheckPolicyService attackPolicy = runtime?.get_attack_check_policy_service();
        if (attackPolicy == null)
        {
            return new AttackEffectResolutionResult
            {
                Applied = false,
                BlockedReason = "attack_policy_unavailable",
                StatusEffectIds = new Godot.Collections.Array<StringName>(),
                RemovedStatusEffectIds = new Godot.Collections.Array<StringName>(),
                SourceStatusEffectIds = new Godot.Collections.Array<StringName>(),
                TerrainEffectIds = new Godot.Collections.Array<StringName>(),
            };
        }

        BattleRepeatAttackStageSpec stageSpec = stage_spec;
        BattleAttackCheckPolicyContext attackContext = attackPolicy.build_repeat_attack_stage_context(
            battleState,
            active_unit,
            target_unit,
            skill_def,
            stageSpec,
            new StringName("repeat_attack_stage_check"),
            new StringName("execute")
        );
        AttackCheckInput attackCheck =
            attackPolicy.build_fate_aware_repeat_attack_stage_hit_check(attackContext);
        BattleDamageResolver damageResolver = runtime?.get_damage_resolver();
        int attackSuccessRatePercent = attackCheck.SuccessRatePercent;
        GDictionary legacyResult;
        if (damageResolver != null)
        {
            var attackResolutionContext = new AttackContext
            {
                BattleState = battleState,
                SkillId = skill_def != null ? skill_def.skill_id : new StringName(""),
            };
            legacyResult = damageResolver.resolve_attack_effects(
                active_unit,
                target_unit,
                ToUntypedArray(stage_effects),
                attackCheck,
                attackResolutionContext
            );
        }
        else
        {
            legacyResult = new GDictionary
            {
                ["attack_success"] = false,
                ["hit_rate_percent"] = attackSuccessRatePercent,
                ["success_rate_percent"] = attackSuccessRatePercent,
            };
        }
        legacyResult["hit_rate_percent"] = attackSuccessRatePercent;
        legacyResult["success_rate_percent"] = attackSuccessRatePercent;
        AttackEffectResolutionResult result =
            AttackEffectResolutionResultReader.ReadLegacyResolverResult(legacyResult, attackCheck);
        result.HitRatePercent = attackSuccessRatePercent;
        result.SuccessRatePercent = attackSuccessRatePercent;
        result.ResolutionText = FormatRepeatAttackStageResolutionText(
            attackCheck,
            result
        );
        return result;
    }

    internal string FormatRepeatAttackStageResolutionText(
        AttackCheckInput attack_check,
        AttackEffectResolutionResult attack_result
    )
    {
        int successRate = attack_check.SuccessRatePercent;
        string previewText = string.IsNullOrEmpty(attack_check.PreviewText)
            ? $"{successRate}%"
            : attack_check.PreviewText;
        AttackResolutionKind attackResolution = attack_result.AttackResolution;
        int hitRoll = attack_result.HitRoll;
        if (hitRoll > 0)
        {
            if (attackResolution == AttackResolutionKind.CriticalHit)
            {
                return $"{previewText}，d20={hitRoll}（大成功）";
            }
            if (attackResolution == AttackResolutionKind.CriticalFail)
            {
                return $"{previewText}，d20={hitRoll}（大失败）";
            }
            if (attackResolution == AttackResolutionKind.Miss)
            {
                return $"{previewText}，d20={hitRoll}（未命中）";
            }
            return $"{previewText}，d20={hitRoll}";
        }

        int critGateDie = attack_result.CritGateDie;
        int critGateRoll = attack_result.CritGateRoll;
        if (critGateDie > 0 && critGateRoll > 0)
        {
            return $"{previewText}，门骰 d{critGateDie}={critGateRoll}";
        }
        return previewText;
    }

    public int _get_repeat_attack_stage_cost(BattleRepeatAttackStageSpec stage_spec)
    {
        return Math.Max(stage_spec.stage_resource_cost, 0);
    }

    public GDictionary _resolve_effective_skill_costs(
        GodotObject active_unit,
        GodotObject skill_def
    )
    {
        BattleUnitState activeUnit = active_unit as BattleUnitState;
        SkillDef skillDef = skill_def as SkillDef;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (activeUnit == null || skillDef == null || combatProfile == null)
        {
            return new GDictionary();
        }
        int skillLevel;
        if (_runtime != null)
        {
            skillLevel = _runtime._get_unit_skill_level(
                activeUnit,
                skillDef.skill_id
            );
        }
        else
        {
            skillLevel = _resolve_static_skill_level(active_unit, skill_def);
        }
        return GetEffectiveResourceCosts(combatProfile, skillLevel);
    }

    public string _resolve_repeat_attack_resource_label(BattleRepeatAttackStageSpec stage_spec)
    {
        return CombatResourceKindUtils.ToLabel(stage_spec.cost_resource_kind);
    }

    public string _resolve_repeat_attack_resource_abbr(BattleRepeatAttackStageSpec stage_spec)
    {
        return CombatResourceKindUtils.ToAbbr(stage_spec.cost_resource_kind);
    }

    public int _get_repeat_attack_base_resource_cost(
        GodotObject active_unit,
        GodotObject skill_def,
        CombatResourceKind cost_resource_kind
    )
    {
        SkillDef skillDef = skill_def as SkillDef;
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (skillDef == null || combatProfile == null)
        {
            return 0;
        }
        GDictionary effectiveCosts = _resolve_effective_skill_costs(active_unit, skill_def);
        string costField = CombatResourceKindUtils.ToEffectiveCostField(cost_resource_kind);
        if (string.IsNullOrEmpty(costField))
        {
            return 0;
        }
        return GetInt(effectiveCosts, costField, GetCombatProfileCost(combatProfile, cost_resource_kind));
    }

    public bool _can_pay_repeat_attack_stage_cost(
        GodotObject active_unit,
        BattleRepeatAttackStageSpec stage_spec
    )
    {
        if (active_unit == null)
        {
            return false;
        }
        int stageCost = _get_repeat_attack_stage_cost(stage_spec);
        string currentField = CombatResourceKindUtils.ToCurrentUnitField(stage_spec.cost_resource_kind);
        if (string.IsNullOrEmpty(currentField))
        {
            return stageCost <= 0;
        }
        if (stageCost <= 0)
        {
            return true;
        }
        return GetUnitResourceValue(active_unit as BattleUnitState, stage_spec.cost_resource_kind)
            >= stageCost;
    }

    public void _consume_repeat_attack_stage_cost(
        GodotObject active_unit,
        BattleRepeatAttackStageSpec stage_spec
    )
    {
        int stageCost = _get_repeat_attack_stage_cost(stage_spec);
        if (active_unit == null || stageCost <= 0)
        {
            return;
        }
        string currentField = CombatResourceKindUtils.ToCurrentUnitField(stage_spec.cost_resource_kind);
        if (string.IsNullOrEmpty(currentField))
        {
            return;
        }
        BattleUnitState activeUnit = active_unit as BattleUnitState;
        SetUnitResourceValue(
            activeUnit,
            stage_spec.cost_resource_kind,
            Math.Max(GetUnitResourceValue(activeUnit, stage_spec.cost_resource_kind) - stageCost, 0)
        );
    }

    public bool _should_stop_repeat_attack_on_miss(GodotObject repeat_attack_effect)
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect as CombatEffectDef)
            .StopOnMiss;
    }

    public bool _should_stop_repeat_attack_on_target_down(GodotObject repeat_attack_effect)
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect as CombatEffectDef)
            .StopOnTargetDown;
    }

    public double _get_repeat_attack_stage_damage_multiplier(
        GodotObject repeat_attack_effect,
        int stage_index
    )
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect as CombatEffectDef)
            .GetStageDamageMultiplier(stage_index);
    }

    public GCombatEffectArray _build_repeat_attack_stage_effects(
        GCombatEffectArray base_effects,
        CombatEffectDef repeat_attack_effect,
        double damage_multiplier
    )
    {
        var stagedEffects = new GCombatEffectArray();
        string damageMultiplierStage = GetString(
            repeat_attack_effect?.@params,
            "damage_multiplier_stage",
            PreResistanceStage
        );
        foreach (CombatEffectDef effectDef in base_effects ?? new GCombatEffectArray())
        {
            if (effectDef == null)
            {
                continue;
            }
            CombatEffectDef stageEffect = effectDef.duplicate_for_runtime();
            if (stageEffect == null)
            {
                continue;
            }
            if (
                stageEffect.effect_type == DamageEffect
                && damageMultiplierStage == PreResistanceStage
                && damage_multiplier > 1.0
            )
            {
                GDictionary stageParams = stageEffect.@params ?? new GDictionary();
                stageParams["runtime_pre_resistance_damage_multiplier"] = damage_multiplier;
                stageEffect.@params = stageParams;
            }
            stagedEffects.Add(stageEffect);
        }
        return stagedEffects;
    }

    public string _format_runtime_multiplier(double multiplier)
    {
        double rounded = Math.Round(multiplier);
        if (Mathf.IsEqualApprox(multiplier, rounded))
        {
            return ((int)rounded).ToString(CultureInfo.GetCultureInfo(""));
        }
        double snapped = Math.Round(multiplier / 0.01) * 0.01;
        return snapped.ToString("0.##", CultureInfo.GetCultureInfo(""));
    }

    public bool _has_runtime()
    {
        return _runtime != null;
    }

    private static GDictionary GetEffectiveResourceCosts(CombatSkillDef combatProfile, int skillLevel)
    {
        return combatProfile?.get_effective_resource_costs(skillLevel) ?? new GDictionary();
    }

    private static GArray ToUntypedArray(GCombatEffectArray values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (CombatEffectDef value in values)
        {
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static int GetCombatProfileCost(
        CombatSkillDef combatProfile,
        CombatResourceKind costResourceKind
    )
    {
        if (combatProfile == null)
        {
            return 0;
        }
        return costResourceKind switch
        {
            CombatResourceKind.Ap => combatProfile.ap_cost,
            CombatResourceKind.Aura => combatProfile.aura_cost,
            CombatResourceKind.Mp => combatProfile.mp_cost,
            CombatResourceKind.Stamina => combatProfile.stamina_cost,
            _ => 0,
        };
    }

    private static int GetUnitResourceValue(
        BattleUnitState activeUnit,
        CombatResourceKind costResourceKind
    )
    {
        if (activeUnit == null)
        {
            return 0;
        }
        return costResourceKind switch
        {
            CombatResourceKind.Ap => activeUnit.current_ap,
            CombatResourceKind.Aura => activeUnit.current_aura,
            CombatResourceKind.Mp => activeUnit.current_mp,
            CombatResourceKind.Stamina => activeUnit.current_stamina,
            _ => 0,
        };
    }

    private static void SetUnitResourceValue(
        BattleUnitState activeUnit,
        CombatResourceKind costResourceKind,
        int value
    )
    {
        if (activeUnit == null)
        {
            return;
        }
        int normalizedValue = Math.Max(value, 0);
        switch (costResourceKind)
        {
            case CombatResourceKind.Ap:
                activeUnit.current_ap = normalizedValue;
                break;
            case CombatResourceKind.Aura:
                activeUnit.current_aura = normalizedValue;
                break;
            case CombatResourceKind.Mp:
                activeUnit.current_mp = normalizedValue;
                break;
            case CombatResourceKind.Stamina:
                activeUnit.current_stamina = normalizedValue;
                break;
        }
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.Bool => value.AsBool() ? 1 : 0,
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName
                => int.TryParse(value.AsStringName().ToString(), out int parsed)
                    ? parsed
                    : fallback,
            _ => fallback,
        };
    }

    private static double GetFloat(GDictionary source, object key, double fallback = 0.0)
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.Bool => value.AsBool() ? 1.0 : 0.0,
            Variant.Type.String => double.TryParse(value.AsString(), out double parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName
                => double.TryParse(value.AsStringName().ToString(), out double parsed)
                    ? parsed
                    : fallback,
            _ => fallback,
        };
    }

    private static string GetString(GDictionary source, object key, string fallback = "")
    {
        if (!TryGetValue(source, key, out Variant value))
        {
            return fallback;
        }
        if (value.VariantType == Variant.Type.Nil)
        {
            return fallback;
        }
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => value.ToString(),
        };
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        batch?.log_lines.Add(line);
    }

    private static string DisplayName(object value)
    {
        return value switch
        {
            BattleUnitState unitState => unitState.display_name,
            SkillDef skillDef => skillDef.display_name,
            _ => "",
        };
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : GodotObject
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out T target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
