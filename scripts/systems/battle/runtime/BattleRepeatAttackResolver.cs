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

    private WeakReference<GodotObject> _runtimeRef;
    private WeakReference<GodotObject> _masteryRecorderRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef) as BattleRuntimeModule;
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    private BattleSkillMasteryService _masteryRecorder
    {
        get => ResolveWeakRef(_masteryRecorderRef) as BattleSkillMasteryService;
        set => _masteryRecorderRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void setup(GodotObject runtime, GodotObject mastery_recorder = null)
    {
        _runtime = runtime as BattleRuntimeModule;
        _masteryRecorder = mastery_recorder as BattleSkillMasteryService;
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
                if (_should_stop_repeat_attack_on_miss(repeat_attack_effect))
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
                    new GDictionary { ["record_enemy_defeated_achievement"] = true }
                );
                if (_should_stop_repeat_attack_on_target_down(repeat_attack_effect))
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
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
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
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
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
        GDictionary levelMap = GdInterop.GetDictionary(active_unit, "known_skill_level_map");
        return GdInterop.GetInt(levelMap, GdInterop.GetStringName(skill_def, "skill_id"), 0);
    }

    public static int _get_repeat_attack_preview_base_cost(
        GodotObject skill_def,
        CombatResourceKind cost_resource_kind,
        GDictionary effective_costs
    )
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        string costField = CombatResourceKindUtils.ToEffectiveCostField(cost_resource_kind);
        if (string.IsNullOrEmpty(costField))
        {
            return 0;
        }
        return GdInterop.GetInt(
            effective_costs,
            costField,
            GdInterop.GetInt(combatProfile, costField)
        );
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
        return GdInterop.GetInt(active_unit, currentField);
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
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (active_unit == null || skill_def == null || combatProfile == null)
        {
            return new GDictionary();
        }
        int skillLevel;
        if (_runtime != null)
        {
            skillLevel = _runtime._get_unit_skill_level(
                active_unit as BattleUnitState,
                GdInterop.GetStringName(skill_def, "skill_id")
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
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (skill_def == null || combatProfile == null)
        {
            return 0;
        }
        GDictionary effectiveCosts = _resolve_effective_skill_costs(active_unit, skill_def);
        string costField = CombatResourceKindUtils.ToEffectiveCostField(cost_resource_kind);
        if (string.IsNullOrEmpty(costField))
        {
            return 0;
        }
        return GdInterop.GetInt(
            effectiveCosts,
            costField,
            GdInterop.GetInt(combatProfile, costField)
        );
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
        return GdInterop.GetInt(active_unit, currentField) >= stageCost;
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
        active_unit.Set(
            currentField,
            Math.Max(GdInterop.GetInt(active_unit, currentField) - stageCost, 0)
        );
    }

    public bool _should_stop_repeat_attack_on_miss(GodotObject repeat_attack_effect)
    {
        return GdInterop.GetBool(
            GdInterop.GetDictionary(repeat_attack_effect, "params"),
            "stop_on_miss",
            true
        );
    }

    public bool _should_stop_repeat_attack_on_target_down(GodotObject repeat_attack_effect)
    {
        return GdInterop.GetBool(
            GdInterop.GetDictionary(repeat_attack_effect, "params"),
            "stop_on_target_down",
            true
        );
    }

    public double _get_repeat_attack_stage_damage_multiplier(
        GodotObject repeat_attack_effect,
        int stage_index
    )
    {
        if (repeat_attack_effect == null || stage_index <= 0)
        {
            return 1.0;
        }
        double followUpDamageMultiplier = Math.Max(
            GdInterop.GetFloat(
                GdInterop.GetDictionary(repeat_attack_effect, "params"),
                "follow_up_damage_multiplier",
                1.0
            ),
            1.0
        );
        return Math.Pow(followUpDamageMultiplier, stage_index);
    }

    public GCombatEffectArray _build_repeat_attack_stage_effects(
        GCombatEffectArray base_effects,
        CombatEffectDef repeat_attack_effect,
        double damage_multiplier
    )
    {
        var stagedEffects = new GCombatEffectArray();
        string damageMultiplierStage = GdInterop.GetString(
            GdInterop.GetDictionary(repeat_attack_effect, "params"),
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

    private static GDictionary GetEffectiveResourceCosts(GodotObject combatProfile, int skillLevel)
    {
        return (combatProfile as CombatSkillDef)?.get_effective_resource_costs(skillLevel)
            ?? new GDictionary();
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

    private static void AppendLog(GodotObject batch, string line)
    {
        GdInterop.GetArray(batch, "log_lines").Add(line);
    }

    private static string DisplayName(GodotObject value)
    {
        return GdInterop.GetString(value, "display_name");
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
        {
            return null;
        }
        return target;
    }
}
