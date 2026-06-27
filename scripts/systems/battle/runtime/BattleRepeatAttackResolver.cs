using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleRepeatAttackResolver
{
    private static readonly StringName REPEAT_ATTACK_EFFECT_TYPE = "repeat_attack_until_fail";
    private const int REPEAT_ATTACK_STAGE_GUARD = 32;
    private const int DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT = 3;
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";

    private static readonly StringName DamageEffect = "damage";
    private static readonly string PreResistanceStage = "pre_resistance";

    private readonly record struct RepeatAttackRuntimeParameters(
        bool StopOnMiss,
        bool StopOnTargetDown,
        double FollowUpDamageMultiplier
    )
    {
        public static RepeatAttackRuntimeParameters FromEffect(
            CombatEffectDefinition effectDefinition
        )
        {
            return new RepeatAttackRuntimeParameters(
                effectDefinition?.StopOnMiss ?? true,
                effectDefinition?.StopOnTargetDown ?? true,
                Math.Max(
                    GetFloat(effectDefinition?.Parameters, "follow_up_damage_multiplier", 1.0),
                    1.0
                )
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

    internal void Setup(BattleRuntimeModule runtime, BattleSkillMasteryService masteryRecorder = null)
    {
        _runtime = runtime;
        _masteryRecorder = masteryRecorder;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
        _masteryRecorder = null;
    }

    internal bool ApplyRepeatAttackSkillResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        IEnumerable<CombatEffectDefinition> effect_definitions,
        CombatEffectDefinition repeat_attack_effect,
        BattleEventBatch batch
    )
    {
        List<CombatEffectDefinition> stagedEffects = CollectRepeatAttackBaseEffects(effect_definitions);
        if (
            active_unit == null
            || target_unit == null
            || skill_definition == null
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
                && _runtime.IsUnitFollowUpLocked(active_unit)
            )
            {
                AppendLog(batch, $"{DisplayName(active_unit)} 受折手封印影响，无法继续追击。");
                break;
            }

            BattleRepeatAttackStageSpec stageSpec = BuildRuntimeStageSpec(
                active_unit,
                skill_definition,
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
                        $"{DisplayName(active_unit)} 的 {DisplayName(skill_definition)} 在第 {stageIndex + 1} 段前{costResourceLabel}不足，连斩中止。"
                    );
                    break;
                }
                _consume_repeat_attack_stage_cost(active_unit, stageSpec);
                (_runtime as BattleRuntimeModule)?.AppendChangedUnitId(batch, active_unit.unit_id);
            }

            double stageDamageMultiplier = _get_repeat_attack_stage_damage_multiplier(
                repeat_attack_effect,
                stageIndex
            );
            List<CombatEffectDefinition> stageEffects = _build_repeat_attack_stage_effects(
                stagedEffects,
                repeat_attack_effect,
                stageDamageMultiplier
            );
            AttackEffectResolutionResult stageResult = ResolveRepeatAttackStageResult(
                active_unit,
                target_unit,
                skill_definition,
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
                    $"{DisplayName(active_unit)} 的 {DisplayName(skill_definition)} 第 {stageIndex + 1} 段未命中 {DisplayName(target_unit)}，{stageResolutionText}，{costResourceAbbr} 消耗 {stageResourceCost}。"
                );
                _runtime?.AppendResultReportEntry(batch, stageResult);
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
                    skill_definition,
                    stageResult
                );
                if (stageIndex >= 4)
                {
                    int bonusBase = (int)Mathf.Pow(2, stageIndex - 4);
                    _masteryRecorder.RecordBonus(
                        active_unit,
                        target_unit,
                        skill_definition,
                        bonusBase
                    );
                }
            }

            (_runtime as BattleRuntimeModule)?.MarkAppliedStatusesForTurnTiming(
                target_unit,
                stageResult.StatusEffectIds
            );
            _runtime?.AppendResultSourceStatusEffects(
                batch,
                active_unit,
                stageResult
            );
            (_runtime as BattleRuntimeModule)?.AppendChangedUnitId(batch, target_unit.unit_id);
            (_runtime as BattleRuntimeModule)?._append_changed_unit_coords(batch, target_unit);

            int damage = stageResult.Damage;
            int healing = stageResult.Healing;
            totalDamage += damage;
            totalHealing += healing;
            _runtime?.AppendDamageResultLogLines(
                batch,
                $"{DisplayName(active_unit)} 的 {DisplayName(skill_definition)} 第 {stageIndex + 1} 段，倍率 x{_format_runtime_multiplier(stageDamageMultiplier)}，{costResourceAbbr} 消耗 {stageResourceCost}，{stageResolutionText}",
                DisplayName(target_unit),
                stageResult
            );
            _runtime?.AppendResultReportEntry(batch, stageResult);

            if (healing > 0)
            {
                AppendLog(
                    batch,
                    $"{DisplayName(active_unit)} 的 {DisplayName(skill_definition)} 第 {stageIndex + 1} 段为 {DisplayName(target_unit)} 恢复 {healing} 点生命。"
                );
            }

            if (stageResult.StatusEffectIds != null)
            {
                foreach (StringName statusId in stageResult.StatusEffectIds)
                {
                    AppendLog(batch, $"{DisplayName(target_unit)} 获得状态 {statusId}。");
                }
            }

            if (!target_unit.is_alive)
            {
                totalKillCount += 1;
                _runtime?.HandleUnitDefeatedByRuntimeEffect(
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
                _runtime?.RecordBattleContributionResult(
                    active_unit,
                    target_unit,
                    damage,
                    healing,
                    !target_unit.is_alive,
                    new StringName("repeat"),
                    skill_definition.SkillId
                );
            }

            stageIndex += 1;
        }

        if (stageIndex >= REPEAT_ATTACK_STAGE_GUARD && target_unit.is_alive)
        {
            AppendLog(
                batch,
                $"{DisplayName(active_unit)} 的 {DisplayName(skill_definition)} 达到内部连斩保护上限后被强制中止。"
            );
        }

        return anyAttackSucceeded;
    }

    internal CombatEffectDefinition get_repeat_attack_effect_def(
        IEnumerable<CombatEffectDefinition> effect_definitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in
                effect_definitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && effectDefinition.EffectKind == BattleEffectKind.RepeatAttackUntilFail
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private List<CombatEffectDefinition> CollectRepeatAttackBaseEffects(
        IEnumerable<CombatEffectDefinition> effect_definitions
    )
    {
        var stagedEffects = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effectDefinition in
                effect_definitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition != null
                && BattleTypedNames.IsUnitPayloadEffect(effectDefinition.EffectKind)
            )
            {
                stagedEffects.Add(effectDefinition);
            }
        }
        return stagedEffects;
    }

    private BattleRepeatAttackStageSpec BuildRuntimeStageSpec(
        BattleUnitState active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int stage_index,
        int stage_count,
        bool fate_aware
    )
    {
        int skillLevel =
            _runtime != null
                ? _runtime._get_unit_skill_level(
                    active_unit,
                    skill_definition?.SkillId ?? new StringName("")
                )
                : _resolve_static_skill_level(active_unit, skill_definition);
        BattleRepeatAttackStageSpec spec =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeat_attack_effect,
                stage_index,
                stage_count,
                skillLevel,
                fate_aware
            );
        spec = spec.WithBaseResourceCost(
            _get_repeat_attack_base_resource_cost(
                active_unit,
                skill_definition,
                spec.cost_resource_kind
            )
        );
        return spec;
    }

    public static BattleRepeatAttackStageSpec BuildStageSpecFromRepeatAttackEffect(
        BattleUnitState active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int stage_index,
        int stage_count,
        bool fate_aware
    )
    {
        int skillLevel = _resolve_static_skill_level(active_unit, skill_definition);
        BattleRepeatAttackStageSpec spec = BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
            repeat_attack_effect,
            stage_index,
            stage_count,
            skillLevel,
            fate_aware
        );
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        CombatSkillResourceCosts effectiveCosts = GetEffectiveResourceCosts(
            combatProfile,
            skillLevel
        );
        spec = spec.WithBaseResourceCost(
            _get_repeat_attack_preview_base_cost(
                skill_definition,
                spec.cost_resource_kind,
                effectiveCosts
            )
        );
        return spec;
    }

    internal static BattleRepeatAttackStageSpec BuildStageSpecFromRepeatAttackEffect(
        BattleUnitReadView active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int stage_index,
        int stage_count,
        bool fate_aware
    )
    {
        int skillLevel = _resolve_static_skill_level(active_unit, skill_definition);
        BattleRepeatAttackStageSpec spec = BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
            repeat_attack_effect,
            stage_index,
            stage_count,
            skillLevel,
            fate_aware
        );
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        CombatSkillResourceCosts effectiveCosts = GetEffectiveResourceCosts(
            combatProfile,
            skillLevel
        );
        spec = spec.WithBaseResourceCost(
            _get_repeat_attack_preview_base_cost(
                skill_definition,
                spec.cost_resource_kind,
                effectiveCosts
            )
        );
        return spec;
    }

    public static List<BattleRepeatAttackStageSpec> BuildStageSpecsFromRepeatAttackEffect(
        BattleUnitState active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int preview_stage_count,
        bool fate_aware
    )
    {
        var specs = new List<BattleRepeatAttackStageSpec>();
        if (active_unit == null || skill_definition == null || repeat_attack_effect == null)
        {
            return specs;
        }
        int resolvedStageCount = preview_stage_count;
        if (resolvedStageCount <= 0)
        {
            resolvedStageCount = resolve_repeat_attack_preview_stage_count(
                active_unit,
                skill_definition,
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
                BuildStageSpecFromRepeatAttackEffect(
                    active_unit,
                    skill_definition,
                    repeat_attack_effect,
                    stageIndex,
                    normalizedStageCount,
                    fate_aware
                )
            );
        }
        return specs;
    }

    internal static List<BattleRepeatAttackStageSpec> BuildStageSpecsFromRepeatAttackEffect(
        BattleUnitReadView active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int preview_stage_count,
        bool fate_aware
    )
    {
        var specs = new List<BattleRepeatAttackStageSpec>();
        if (!active_unit.IsValid || skill_definition == null || repeat_attack_effect == null)
        {
            return specs;
        }
        int resolvedStageCount = preview_stage_count;
        if (resolvedStageCount <= 0)
        {
            resolvedStageCount = resolve_repeat_attack_preview_stage_count(
                active_unit,
                skill_definition,
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
                BuildStageSpecFromRepeatAttackEffect(
                    active_unit,
                    skill_definition,
                    repeat_attack_effect,
                    stageIndex,
                    normalizedStageCount,
                    fate_aware
                )
            );
        }
        return specs;
    }

    internal static int resolve_repeat_attack_preview_stage_count(
        BattleUnitState active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (
            active_unit == null
            || skill_definition == null
            || combatProfile == null
            || repeat_attack_effect == null
        )
        {
            return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT;
        }
        if (active_unit.HasStatusEffect(STATUS_CROWN_BREAK_BROKEN_HAND))
        {
            return 1;
        }

        BattleRepeatAttackStageSpec firstStageSpec = BuildStageSpecFromRepeatAttackEffect(
            active_unit,
            skill_definition,
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
            int nextStageCost = firstStageSpec.ResolveResourceCostForStage(stages);
            if (nextStageCost > 0 && remainingResource < nextStageCost)
            {
                break;
            }
            remainingResource -= nextStageCost;
            stages += 1;
        }
        return stages;
    }

    internal static int resolve_repeat_attack_preview_stage_count(
        BattleUnitReadView active_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (
            !active_unit.IsValid
            || skill_definition == null
            || combatProfile == null
            || repeat_attack_effect == null
        )
        {
            return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT;
        }
        if (active_unit.HasStatusEffect(STATUS_CROWN_BREAK_BROKEN_HAND))
        {
            return 1;
        }

        BattleRepeatAttackStageSpec firstStageSpec = BuildStageSpecFromRepeatAttackEffect(
            active_unit,
            skill_definition,
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
            int nextStageCost = firstStageSpec.ResolveResourceCostForStage(stages);
            if (nextStageCost > 0 && remainingResource < nextStageCost)
            {
                break;
            }
            remainingResource -= nextStageCost;
            stages += 1;
        }
        return stages;
    }

    internal static int _resolve_static_skill_level(
        BattleUnitState active_unit,
        SkillDefinition skill_definition
    )
    {
        if (active_unit == null || skill_definition == null)
        {
            return 0;
        }
        return active_unit.GetKnownSkillLevelTyped(skill_definition.SkillId);
    }

    internal static int _resolve_static_skill_level(
        BattleUnitReadView active_unit,
        SkillDefinition skill_definition
    )
    {
        if (!active_unit.IsValid || skill_definition == null)
        {
            return 0;
        }
        return active_unit.GetKnownSkillLevel(skill_definition.SkillId);
    }

    internal static int _get_repeat_attack_preview_base_cost(
        SkillDefinition skill_definition,
        CombatResourceKind cost_resource_kind,
        CombatSkillResourceCosts effective_costs
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (skill_definition == null || combatProfile == null)
        {
            return 0;
        }
        return GetEffectiveCostValue(effective_costs, cost_resource_kind);
    }

    internal static int _get_unit_resource_value(
        BattleUnitState active_unit,
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
        return GetUnitResourceValue(active_unit, cost_resource_kind);
    }

    internal static int _get_unit_resource_value(
        BattleUnitReadView active_unit,
        CombatResourceKind cost_resource_kind
    )
    {
        if (!active_unit.IsValid)
        {
            return 0;
        }
        string currentField = CombatResourceKindUtils.ToCurrentUnitField(cost_resource_kind);
        if (string.IsNullOrEmpty(currentField))
        {
            return 0;
        }
        return active_unit.GetResourceValue(cost_resource_kind);
    }

    private AttackEffectResolutionResult ResolveRepeatAttackStageResult(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        BattleRepeatAttackStageSpec stage_spec,
        int stage_index,
        IEnumerable<CombatEffectDefinition> stage_effects
    )
    {
        BattleRuntimeModule runtime = _runtime as BattleRuntimeModule;
        BattleState battleState = runtime?.GetState();
        BattleAttackCheckPolicyService attackPolicy = runtime?.GetAttackCheckPolicyService();
        if (attackPolicy == null)
        {
            return new AttackEffectResolutionResult
            {
                Applied = false,
                BlockedReason = "attack_policy_unavailable",
                StatusEffectIds = new StringNameList(),
                RemovedStatusEffectIds = new StringNameList(),
                SourceStatusEffectIds = new StringNameList(),
                TerrainEffectIds = new StringNameList(),
            };
        }

        BattleRepeatAttackStageSpec stageSpec = stage_spec;
        BattleAttackCheckPolicyContext attackContext = attackPolicy.BuildRepeatAttackStageContext(
            battleState,
            active_unit,
            target_unit,
            skill_definition,
            stageSpec,
            new StringName("repeat_attack_stage_check"),
            new StringName("execute")
        );
        AttackCheckInput attackCheck =
            attackPolicy.BuildFateAwareRepeatAttackStageHitCheck(attackContext);
        BattleDamageResolver damageResolver = runtime?.GetDamageResolver();
        int attackSuccessRatePercent = attackCheck.SuccessRatePercent;
        AttackEffectResolutionResult result;
        if (damageResolver != null)
        {
            var attackResolutionContext = new AttackContext
            {
                BattleState = battleState,
                SkillId = skill_definition != null ? skill_definition.SkillId : new StringName(""),
            };
            result = damageResolver.ResolveAttackEffects(
                active_unit,
                target_unit,
                stage_effects,
                attackCheck,
                attackResolutionContext
            );
        }
        else
        {
            result = new AttackEffectResolutionResult
            {
                Applied = false,
                AttackSuccess = false,
                HitRatePercent = attackSuccessRatePercent,
                SuccessRatePercent = attackSuccessRatePercent,
                AttackCheck = attackCheck,
                StatusEffectIds = new StringNameList(),
                RemovedStatusEffectIds = new StringNameList(),
                SourceStatusEffectIds = new StringNameList(),
                TerrainEffectIds = new StringNameList(),
            };
        }
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

    internal int _get_repeat_attack_stage_cost(BattleRepeatAttackStageSpec stage_spec)
    {
        return Math.Max(stage_spec.stage_resource_cost, 0);
    }

    internal CombatSkillResourceCosts _resolve_effective_skill_costs(
        BattleUnitState active_unit,
        SkillDefinition skill_definition
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (active_unit == null || skill_definition == null || combatProfile == null)
        {
            return CombatSkillResourceCosts.Zero;
        }
        int skillLevel;
        if (_runtime != null)
        {
            skillLevel = _runtime._get_unit_skill_level(
                active_unit,
                skill_definition.SkillId
            );
        }
        else
        {
            skillLevel = _resolve_static_skill_level(active_unit, skill_definition);
        }
        return GetEffectiveResourceCosts(combatProfile, skillLevel);
    }

    internal string _resolve_repeat_attack_resource_label(BattleRepeatAttackStageSpec stage_spec)
    {
        return CombatResourceKindUtils.ToLabel(stage_spec.cost_resource_kind);
    }

    internal string _resolve_repeat_attack_resource_abbr(BattleRepeatAttackStageSpec stage_spec)
    {
        return CombatResourceKindUtils.ToAbbr(stage_spec.cost_resource_kind);
    }

    internal int _get_repeat_attack_base_resource_cost(
        BattleUnitState active_unit,
        SkillDefinition skill_definition,
        CombatResourceKind cost_resource_kind
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (skill_definition == null || combatProfile == null)
        {
            return 0;
        }
        CombatSkillResourceCosts effectiveCosts = _resolve_effective_skill_costs(
            active_unit,
            skill_definition
        );
        return GetEffectiveCostValue(effectiveCosts, cost_resource_kind);
    }

    internal bool _can_pay_repeat_attack_stage_cost(
        BattleUnitState active_unit,
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
        return GetUnitResourceValue(active_unit, stage_spec.cost_resource_kind) >= stageCost;
    }

    internal void _consume_repeat_attack_stage_cost(
        BattleUnitState active_unit,
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
        SetUnitResourceValue(
            active_unit,
            stage_spec.cost_resource_kind,
            Math.Max(GetUnitResourceValue(active_unit, stage_spec.cost_resource_kind) - stageCost, 0)
        );
    }

    internal bool _should_stop_repeat_attack_on_miss(
        CombatEffectDefinition repeat_attack_effect
    )
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect)
            .StopOnMiss;
    }

    internal bool _should_stop_repeat_attack_on_target_down(
        CombatEffectDefinition repeat_attack_effect
    )
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect)
            .StopOnTargetDown;
    }

    internal double _get_repeat_attack_stage_damage_multiplier(
        CombatEffectDefinition repeat_attack_effect,
        int stage_index
    )
    {
        return RepeatAttackRuntimeParameters
            .FromEffect(repeat_attack_effect)
            .GetStageDamageMultiplier(stage_index);
    }

    internal List<CombatEffectDefinition> _build_repeat_attack_stage_effects(
        IEnumerable<CombatEffectDefinition> base_effects,
        CombatEffectDefinition repeat_attack_effect,
        double damage_multiplier
    )
    {
        var stagedEffects = new List<CombatEffectDefinition>();
        string damageMultiplierStage = GetString(
            repeat_attack_effect?.Parameters,
            "damage_multiplier_stage",
            PreResistanceStage
        );
        foreach (
            CombatEffectDefinition effectDefinition in
                base_effects ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition == null)
            {
                continue;
            }
            CombatEffectDefinition stageEffect = effectDefinition;
            if (
                stageEffect.EffectKind == BattleEffectKind.Damage
                && damageMultiplierStage == PreResistanceStage
                && damage_multiplier > 1.0
            )
            {
                stageEffect = stageEffect.WithPreResistanceDamageMultiplier(damage_multiplier);
            }
            stagedEffects.Add(stageEffect);
        }
        return stagedEffects;
    }

    internal string _format_runtime_multiplier(double multiplier)
    {
        double rounded = Math.Round(multiplier);
        if (Mathf.IsEqualApprox(multiplier, rounded))
        {
            return ((int)rounded).ToString(CultureInfo.GetCultureInfo(""));
        }
        double snapped = Math.Round(multiplier / 0.01) * 0.01;
        return snapped.ToString("0.##", CultureInfo.GetCultureInfo(""));
    }

    internal bool _has_runtime()
    {
        return _runtime != null;
    }

    private static CombatSkillResourceCosts GetEffectiveResourceCosts(
        CombatSkillDefinition combatProfile,
        int skillLevel
    )
    {
        return combatProfile?.GetEffectiveResourceCostValues(skillLevel)
            ?? CombatSkillResourceCosts.Zero;
    }

    private static int GetEffectiveCostValue(
        CombatSkillResourceCosts effectiveCosts,
        CombatResourceKind costResourceKind
    )
    {
        return costResourceKind switch
        {
            CombatResourceKind.Ap => effectiveCosts.ApCost,
            CombatResourceKind.Aura => effectiveCosts.AuraCost,
            CombatResourceKind.Mp => effectiveCosts.MpCost,
            CombatResourceKind.Stamina => effectiveCosts.StaminaCost,
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
                activeUnit.SetCurrentAp(normalizedValue);
                break;
            case CombatResourceKind.Aura:
                activeUnit.SetCurrentAura(normalizedValue);
                break;
            case CombatResourceKind.Mp:
                activeUnit.SetCurrentMp(normalizedValue);
                break;
            case CombatResourceKind.Stamina:
                activeUnit.SetCurrentStamina(normalizedValue);
                break;
        }
    }

    private static int GetInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        return value.AsInt32();
    }

    private static double GetFloat(GDictionary source, string key, double fallback = 0.0)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        return value.AsDouble();
    }

    private static double GetFloat(
        IReadOnlyDictionary<string, Variant> source,
        string key,
        double fallback = 0.0
    )
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static string GetString(GDictionary source, string key, string fallback = "")
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        string result = value.ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
    }

    private static string GetString(
        IReadOnlyDictionary<string, Variant> source,
        string key,
        string fallback = ""
    )
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        string result = value.ToString();
        return string.IsNullOrEmpty(result) || result == "<null>" ? fallback : result;
    }

    private static bool TryResolveStringKey(GDictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }

    private static bool TryResolveStringKey(
        IReadOnlyDictionary<string, Variant> source,
        string key,
        out Variant value
    )
    {
        value = default;
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        return source.TryGetValue(key, out value);
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        batch?.AddLogLine(line);
    }

    private static string DisplayName(object value)
    {
        return value switch
        {
            BattleUnitState unitState => unitState.display_name,
            SkillDefinition skillDefinition => skillDefinition.DisplayName,
            _ => "",
        };
    }

    private static T ResolveWeakRef<T>(WeakReference<T> weakRef)
        where T : class
    {
        if (weakRef == null || !weakRef.TryGetTarget(out T target))
        {
            return null;
        }
        return target;
    }
}
