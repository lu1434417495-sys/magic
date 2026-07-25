using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;

// 翻译自 battle_hit_resolver.gd（2026-05-26，命中检定 C# 迁移）。
// 无 runtime 的规则解析器：BAB/降序AC/d20 命中与真随机掷骰口径统一收敛在此。
public class BattleHitResolver : IDisposable
{
    private const int DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT = 3;
    private const int REPEAT_ATTACK_PREVIEW_STAGE_GUARD = 32;
    private const int ATTACK_CHECK_TARGET = 21;
    private const int NATURAL_MISS_ROLL = 1;
    private const int NATURAL_HIT_ROLL = 20;
    private static readonly StringName ATTACK_RESOLUTION_HIT = "hit";
    private static readonly StringName ATTACK_RESOLUTION_MISS = "miss";
    private static readonly StringName ATTACK_RESOLUTION_CRITICAL_HIT = "critical_hit";
    private static readonly StringName ATTACK_RESOLUTION_CRITICAL_FAIL = "critical_fail";
    private static readonly StringName ROLL_DISPOSITION_THRESHOLD_HIT = "threshold_hit";
    private static readonly StringName ROLL_DISPOSITION_THRESHOLD_MISS = "threshold_miss";
    private static readonly StringName ROLL_DISPOSITION_NATURAL_AUTO_MISS = "natural_1_auto_miss";
    private static readonly StringName ROLL_DISPOSITION_NATURAL_AUTO_HIT = "natural_20_auto_hit";
    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";
    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_HAND = "crown_break_broken_hand";
    private static readonly StringName STATUS_CROWN_BREAK_BLINDED_EYE = "crown_break_blinded_eye";
    private static readonly StringName STATUS_ARMOR_BREAK = "armor_break";
    private static readonly StringName STATUS_DODGE_BONUS_UP = "dodge_bonus_up";
    private static readonly StringName STATUS_ATTACK_ROLL_BONUS_UP = "attack_roll_bonus_up";
    private const int BLACK_STAR_BRAND_ATTACK_BONUS_DELTA = -3;
    private static readonly StringName ATTACK_CHECK_ERROR_MISSING_TARGET_ARMOR_CLASS =
        "missing_target_armor_class";

    private readonly TraitTriggerHooks _trait_trigger_hooks = new();

    public void Dispose()
    {
    }

    public AttackCheckInput BuildRepeatAttackStageHitCheck(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int stage_index
    )
    {
        int skillLevel =
            active_unit != null && skill_definition != null
                ? active_unit.GetKnownSkillLevelTyped(skill_definition.SkillId)
                : 0;
        BattleRepeatAttackStageSpec stageSpec =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeat_attack_effect,
                stage_index,
                0,
                skillLevel
            );
        return BuildSkillDefinitionAttackCheck(
            active_unit,
            target_unit,
            skill_definition,
            stageSpec.stage_base_attack_bonus,
            stageSpec.ResolveStageAttackPenalty()
        );
    }

    public AttackCheckInput BuildFateAwareRepeatAttackStageHitCheck(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int stage_index
    )
    {
        AttackCheckInput baseAttackCheck = BuildRepeatAttackStageHitCheck(
            active_unit,
            target_unit,
            skill_definition,
            repeat_attack_effect,
            stage_index
        );
        return BuildFateAwareAttackCheckPreview(
            battle_state,
            active_unit,
            target_unit,
            baseAttackCheck
        );
    }

    public AttackPreviewData BuildRepeatAttackPreview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDefinition skill_definition,
        CombatEffectDefinition repeat_attack_effect,
        int preview_stage_count = -1
    )
    {
        if (
            active_unit == null
            || target_unit == null
            || skill_definition == null
            || repeat_attack_effect == null
        )
        {
            return new AttackPreviewData();
        }

        int resolvedStageCount = preview_stage_count;
        if (resolvedStageCount <= 0)
        {
            resolvedStageCount = _resolve_repeat_attack_preview_stage_count(
                active_unit,
                skill_definition,
                repeat_attack_effect
            );
        }
        int normalizedStageCount = Math.Min(
            Math.Max(resolvedStageCount, 1),
            REPEAT_ATTACK_PREVIEW_STAGE_GUARD
        );
        var stageChecks = new List<AttackCheckInput>();
        var stages = new List<AttackPreviewStage>();
        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            AttackCheckInput attackCheck = BuildFateAwareRepeatAttackStageHitCheck(
                battle_state,
                active_unit,
                target_unit,
                skill_definition,
                repeat_attack_effect,
                stageIndex
            );
            int stageSuccessRate = attackCheck.SuccessRatePercent;
            stageChecks.Add(attackCheck);
            stages.Add(
                new AttackPreviewStage(
                    hitRatePercent: stageSuccessRate,
                    successRatePercent: stageSuccessRate,
                    baseHitRatePercent: attackCheck.BaseHitRatePercent,
                    requiredRoll: attackCheck.RequiredRoll,
                    displayRequiredRoll: attackCheck.DisplayRequiredRoll,
                    previewText: attackCheck.PreviewText
                )
            );
        }
        int avgSuccessRate = 0;
        int avgBaseHitRate = 0;
        if (stageChecks.Count > 0)
        {
            var successRates = new GIntArray();
            var baseHitRates = new GIntArray();
            foreach (var check in stageChecks)
            {
                successRates.Add(check.SuccessRatePercent);
                baseHitRates.Add(check.BaseHitRatePercent);
            }
            avgSuccessRate = Mathf.RoundToInt((float)_average_ints(successRates));
            avgBaseHitRate = Mathf.RoundToInt((float)_average_ints(baseHitRates));
        }
        return new AttackPreviewData
        {
            SummaryText = FormatRepeatAttackPreviewSummary(stageChecks),
            Stages = stages,
            HitRatePercent = avgSuccessRate,
            SuccessRatePercent = avgSuccessRate,
            BaseHitRatePercent = avgBaseHitRate,
            BaseAttackBonus = repeat_attack_effect?.GetIntParamTyped("base_attack_bonus", 0) ?? 0,
            FollowUpAttackPenalty =
                repeat_attack_effect?.GetIntParamTyped("follow_up_attack_penalty", 0) ?? 0,
            FatePreview = stageChecks.Count > 0
                ? BattleFatePreviewData.FromAttackCheck(stageChecks[0])
                : null,
        };
    }

    internal AttackPreviewData BuildSkillAttackPreview(
        BattleState battle_state,
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        bool force_hit_no_crit = false
    )
    {
        return BuildSkillDefinitionAttackPreview(
            battle_state,
            active_unit,
            target_unit,
            skill_definition,
            force_hit_no_crit
        );
    }

    internal AttackPreviewData BuildSkillDefinitionAttackPreview(
        BattleState battle_state,
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        bool force_hit_no_crit
    )
    {
        if (!active_unit.IsValid || !target_unit.IsValid || skill_definition == null)
        {
            return new AttackPreviewData();
        }
        if (force_hit_no_crit)
        {
            return BuildForceHitNoCritAttackPreview();
        }
        AttackCheckInput attackCheck = BuildFateAwareAttackCheckPreview(
            battle_state,
            active_unit,
            target_unit,
            BuildSkillDefinitionAttackCheck(active_unit, target_unit, skill_definition, 0, 0)
        );
        int successRate = attackCheck.SuccessRatePercent;
        int baseHitRate = attackCheck.BaseHitRatePercent;
        string previewText = attackCheck.PreviewText;
        return new AttackPreviewData
        {
            SummaryText = $"预计命中率 {previewText}",
            Stages = new List<AttackPreviewStage>
            {
                new AttackPreviewStage(
                    hitRatePercent: successRate,
                    successRatePercent: successRate,
                    baseHitRatePercent: baseHitRate,
                    requiredRoll: attackCheck.RequiredRoll,
                    displayRequiredRoll: attackCheck.DisplayRequiredRoll,
                    previewText: previewText
                ),
            },
            HitRatePercent = successRate,
            SuccessRatePercent = successRate,
            BaseHitRatePercent = baseHitRate,
            FatePreview = BattleFatePreviewData.FromAttackCheck(attackCheck),
        };
    }

    public AttackPreviewData BuildForceHitNoCritAttackPreview()
    {
        string previewText = "100%（必定命中；禁暴击）";
        return new AttackPreviewData
        {
            SummaryText = $"预计命中率 {previewText}",
            Stages = new List<AttackPreviewStage>
            {
                new AttackPreviewStage(
                    hitRatePercent: 100,
                    successRatePercent: 100,
                    baseHitRatePercent: 100,
                    requiredRoll: NATURAL_MISS_ROLL,
                    displayRequiredRoll: NATURAL_MISS_ROLL,
                    previewText: previewText
                ),
            },
            HitRatePercent = 100,
            SuccessRatePercent = 100,
            BaseHitRatePercent = 100,
            ForceHitNoCrit = true,
            CritLocked = true,
            FatePreview = BattleFatePreviewData.ForceHitNoCritPreview(),
        };
    }

    internal AttackCheckInput BuildSkillAttackCheck(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition
    )
    {
        return BuildSkillDefinitionAttackCheck(active_unit, target_unit, skill_definition, 0, 0);
    }

    internal AttackCheckInput BuildSkillAttackCheck(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        int flat_bonus = 0,
        int flat_penalty = 0
    )
    {
        return BuildSkillDefinitionAttackCheck(
            active_unit,
            target_unit,
            skill_definition,
            flat_bonus,
            flat_penalty
        );
    }

    internal AttackCheckInput BuildSkillDefinitionAttackCheck(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        int flat_bonus,
        int flat_penalty
    )
    {
        return BuildSkillDefinitionAttackCheck(
            active_unit,
            target_unit,
            skill_definition,
            flat_bonus,
            flat_penalty,
            null
        );
    }

    internal AttackCheckInput BuildSkillDefinitionAttackCheck(
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        SkillDefinition skill_definition,
        int flat_bonus,
        int flat_penalty,
        EquipmentAttackDefenseAdjustment defense_adjustment
    )
    {
        int attackerBaseAttackBonus = _get_unit_attribute_value(
            active_unit,
            AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus),
            0
        );
        int attackerAttackBonus = _get_unit_attribute_value(
            active_unit,
            AttributeService.ToStringName(AttributeIdKind.AttackBonus),
            0
        );
        if (!_unit_has_attribute_value(target_unit, AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            string errorMessage =
                "BattleHitResolver cannot build attack check: target unit is missing armor_class.";
            GameLog.Error(errorMessage, "battle.hit.resolve_failed", "battle");
            return _build_invalid_attack_check(
                ATTACK_CHECK_ERROR_MISSING_TARGET_ARMOR_CLASS,
                errorMessage
            );
        }
        int targetArmorClass = _get_target_armor_class(target_unit, defense_adjustment);
        int skillLevel = 0;
        StringName skillId = skill_definition?.SkillId ?? new StringName("");
        if (active_unit.IsValid && !IsEmpty(skillId))
        {
            if (active_unit.HasKnownSkillLevel(skillId))
            {
                skillLevel = active_unit.GetKnownSkillLevel(skillId);
            }
            else if (active_unit.KnowsActiveSkill(skillId))
            {
                skillLevel = 1;
            }
        }
        int skillAttackBonus =
            SkillEffectiveCombatDefinition.BuildUncached(skill_definition, skillLevel).AttackRollBonus;
        int lockedSkillHitBonus = _get_skill_lock_hit_bonus(active_unit, skillId);
        int statusAttackBonusDelta = _get_attacker_status_attack_bonus_delta(active_unit);
        if (
            BattleRangeService.UnitHasMeleeWeapon(active_unit)
            && SkillIncludesWeaponDamage(skill_definition, skillLevel)
        )
        {
            statusAttackBonusDelta += GetComboAttackRollBonus(active_unit);
        }
        int situationalAttackBonus = flat_bonus + Math.Max(statusAttackBonusDelta, 0);
        int situationalAttackPenalty = flat_penalty + Math.Max(-statusAttackBonusDelta, 0);
        int requiredRoll =
            targetArmorClass
            - attackerBaseAttackBonus
            - attackerAttackBonus
            - skillAttackBonus
            - lockedSkillHitBonus
            - situationalAttackBonus
            + situationalAttackPenalty;
        int hitRatePercent = _compute_hit_rate_percent(requiredRoll);
        int displayRequiredRoll = _get_display_required_roll(requiredRoll);
        return new AttackCheckInput(
            attackerBaseAttackBonus: attackerBaseAttackBonus,
            attackerAttackBonus: attackerAttackBonus,
            attackerBab: attackerBaseAttackBonus,
            targetArmorClass: targetArmorClass,
            skillAttackBonus: skillAttackBonus,
            lockedSkillHitBonus: lockedSkillHitBonus,
            situationalAttackBonus: situationalAttackBonus,
            situationalAttackPenalty: situationalAttackPenalty,
            requiredRoll: requiredRoll,
            displayRequiredRoll: displayRequiredRoll,
            hitRatePercent: hitRatePercent,
            successRatePercent: hitRatePercent,
            naturalOneAutoMiss: true,
            naturalTwentyAutoHit: true,
            previewText: $"{hitRatePercent}%（{_format_required_roll_text(requiredRoll)}）"
        );
    }

    private int _get_unit_attribute_value(
        BattleUnitReadView unit_state,
        StringName attribute_id,
        int fallback = 0
    )
    {
        return unit_state.GetAttributeValue(attribute_id, fallback);
    }

    private bool _unit_has_attribute_value(BattleUnitReadView unit_state, StringName attribute_id)
    {
        return unit_state.HasAttributeValue(attribute_id);
    }

    private int _get_target_armor_class(
        BattleUnitReadView target_unit,
        EquipmentAttackDefenseAdjustment defense_adjustment = null
    )
    {
        int targetArmorClass = _get_unit_attribute_value(
            target_unit,
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            0
        );
        targetArmorClass -= _get_target_armor_break_penalty(target_unit);
        targetArmorClass = _apply_attack_defense_adjustment(
            targetArmorClass,
            target_unit,
            defense_adjustment
        );
        if (
            _is_target_dodge_bonus_locked(target_unit)
            || defense_adjustment?.LockDodgeBonus == true
        )
        {
            targetArmorClass -= _get_remaining_ac_component_value(
                target_unit,
                AttributeService.ToStringName(AttributeIdKind.DodgeBonus),
                defense_adjustment
            );
        }
        else
        {
            targetArmorClass += _get_target_status_dodge_bonus(target_unit);
        }
        return Math.Max(targetArmorClass, 1);
    }

    private int _apply_attack_defense_adjustment(
        int targetArmorClass,
        BattleUnitReadView target_unit,
        EquipmentAttackDefenseAdjustment defense_adjustment
    )
    {
        if (defense_adjustment == null || defense_adjustment.IsEmpty)
            return targetArmorClass;
        foreach (
            StringName componentId in AttributeContentRules.ArmorClassComponentAttributeIds
        )
        {
            int componentValue = Math.Max(_get_unit_attribute_value(target_unit, componentId, 0), 0);
            targetArmorClass -= componentValue - _get_remaining_ac_component_value(
                componentValue,
                componentId,
                defense_adjustment
            );
        }
        return targetArmorClass;
    }

    private int _get_remaining_ac_component_value(
        BattleUnitReadView target_unit,
        StringName componentId,
        EquipmentAttackDefenseAdjustment defense_adjustment
    )
    {
        int componentValue = Math.Max(_get_unit_attribute_value(target_unit, componentId, 0), 0);
        return _get_remaining_ac_component_value(componentValue, componentId, defense_adjustment);
    }

    private int _get_remaining_ac_component_value(
        int componentValue,
        StringName componentId,
        EquipmentAttackDefenseAdjustment defense_adjustment
    )
    {
        if (componentValue <= 0)
            return 0;
        if (defense_adjustment == null)
            return componentValue;
        if (defense_adjustment.ShouldIgnoreAcComponent(componentId))
            return 0;
        int multiplierPercent = defense_adjustment.ResolveComponentMultiplierPercent(componentId);
        if (multiplierPercent >= 100)
            return componentValue;
        return Mathf.FloorToInt(componentValue * multiplierPercent / 100.0f);
    }

    private AttackCheckInput _build_invalid_attack_check(StringName error_id, string error_message)
    {
        return new AttackCheckInput(
            requiredRoll: ATTACK_CHECK_TARGET,
            displayRequiredRoll: _get_display_required_roll(ATTACK_CHECK_TARGET),
            naturalOneAutoMiss: true,
            naturalTwentyAutoHit: false,
            invalid: true,
            errorId: error_id,
            errorMessage: error_message,
            previewText: $"无效命中检定：{error_message}"
        );
    }

    private int _get_target_armor_break_penalty(BattleUnitReadView target_unit)
    {
        if (!target_unit.IsValid || !target_unit.HasStatusEffect(STATUS_ARMOR_BREAK))
        {
            return 0;
        }
        return Math.Max(
            Math.Max(
                target_unit.GetStatusPower(STATUS_ARMOR_BREAK),
                target_unit.GetStatusStacks(STATUS_ARMOR_BREAK)
            ),
            1
        ) * 2;
    }

    private int _get_target_status_dodge_bonus(BattleUnitReadView target_unit)
    {
        if (!target_unit.IsValid || !target_unit.HasStatusEffect(STATUS_DODGE_BONUS_UP))
        {
            return 0;
        }
        return Math.Max(
            Math.Max(
                target_unit.GetStatusPower(STATUS_DODGE_BONUS_UP),
                target_unit.GetStatusStacks(STATUS_DODGE_BONUS_UP)
            ),
            1
        ) * 2;
    }

    private int _get_attacker_status_attack_bonus_delta(BattleUnitReadView active_unit)
    {
        if (!active_unit.IsValid)
        {
            return 0;
        }
        // 攻击命中修正设计为跨状态累加(正负都算),不做取大——取大会让
        // 黑星烙印 -3 被任意正面 buff 吞掉,也让多个 buff 互吞。
        int attackDelta = 0;
        if (active_unit.HasStatusEffect(STATUS_BLACK_STAR_BRAND_ELITE))
        {
            attackDelta += BLACK_STAR_BRAND_ATTACK_BONUS_DELTA;
        }
        // attack_roll_bonus_up 的数值载体在 power/stacks,状态本体的
        // attack_roll_bonus 字段为 0,不会在下面的循环中重复计数。
        if (active_unit.HasStatusEffect(STATUS_ATTACK_ROLL_BONUS_UP))
        {
            attackDelta += Math.Max(
                active_unit.GetStatusPower(STATUS_ATTACK_ROLL_BONUS_UP),
                active_unit.GetStatusStacks(STATUS_ATTACK_ROLL_BONUS_UP)
            );
        }
        foreach (BattleStatusReadView status in active_unit.StatusEffects())
        {
            attackDelta += status.AttackRollBonus;
        }
        return attackDelta - _get_attacker_status_attack_penalty(active_unit);
    }

    private static bool SkillIncludesWeaponDamage(
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        foreach (
            CombatEffectDefinition effect in
                skillDefinition?.CombatProfile?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect != null
                && skillLevel >= effect.MinSkillLevel
                && (effect.MaxSkillLevel < 0 || skillLevel <= effect.MaxSkillLevel)
                && (effect.AddWeaponDice || effect.RequiresWeapon)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static int GetComboAttackRollBonus(BattleUnitReadView activeUnit)
    {
        int bonus = 0;
        foreach (BattleStatusReadView status in activeUnit.StatusEffects())
        {
            int divisor = status.ComboAttackBonusStackDivisor;
            StringName stackStatusId = status.ComboAttackBonusStatusId;
            if (divisor <= 0 || IsEmpty(stackStatusId))
                continue;
            bonus += Math.Max(activeUnit.GetStatusStacks(stackStatusId), 0) / divisor;
        }
        return bonus;
    }

    private int _get_attacker_status_attack_penalty(BattleUnitReadView active_unit)
    {
        return active_unit.GetAttackRollPenalty();
    }

    private bool _is_target_dodge_bonus_locked(BattleUnitReadView target_unit)
    {
        return target_unit.IsValid
            && (
                target_unit.HasStatusEffect(STATUS_CROWN_BREAK_BLINDED_EYE)
                || target_unit.HasLockDodgeBonusStatus()
            );
    }

    public virtual AttackRollResult RollAttackCheck(BattleState battle_state, AttackCheckInput attack_check)
    {
        if (attack_check.Invalid)
        {
            return new AttackRollResult(
                roll: 0,
                rollDisposition: ROLL_DISPOSITION_THRESHOLD_MISS,
                success: false,
                resolutionText: string.IsNullOrEmpty(attack_check.PreviewText)
                    ? "无效命中检定"
                    : attack_check.PreviewText,
                attackRollNonce: _get_attack_roll_nonce_text(battle_state)
            );
        }
        int roll = RollBattleD20(battle_state);
        StringName rollDisposition = ResolveAttackRollDispositionForCheck(roll, attack_check);
        bool success = _is_attack_roll_disposition_success(rollDisposition);
        var result = new AttackRollResult(
            roll: roll,
            rollDisposition: rollDisposition,
            success: success,
            attackRollNonce: _get_attack_roll_nonce_text(battle_state)
        );
        return new AttackRollResult(
            roll: roll,
            rollDisposition: rollDisposition,
            success: success,
            resolutionText: FormatAttackCheckResolution(attack_check, result),
            attackRollNonce: result.AttackRollNonce
        );
    }

    public virtual AttackRollResult RollHitRate(BattleState battle_state, int hit_rate_percent)
    {
        int clampedHitRate = Math.Clamp(hit_rate_percent, 0, 100);
        int syntheticRequiredRoll = _get_required_roll_for_hit_rate(clampedHitRate);
        int displayRequiredRoll = _get_display_required_roll(syntheticRequiredRoll);
        var syntheticAttackCheck = new AttackCheckInput(
            requiredRoll: syntheticRequiredRoll,
            displayRequiredRoll: displayRequiredRoll,
            naturalOneAutoMiss: clampedHitRate < 100,
            naturalTwentyAutoHit: clampedHitRate > 0
        );
        int resolvedHitRate = _compute_attack_check_success_rate_percent(syntheticAttackCheck);
        syntheticAttackCheck = new AttackCheckInput(
            requiredRoll: syntheticRequiredRoll,
            displayRequiredRoll: displayRequiredRoll,
            hitRatePercent: resolvedHitRate,
            successRatePercent: resolvedHitRate,
            naturalOneAutoMiss: clampedHitRate < 100,
            naturalTwentyAutoHit: clampedHitRate > 0,
            previewText: $"{resolvedHitRate}%（{_format_required_roll_text(syntheticRequiredRoll)}）"
        );
        return RollAttackCheck(battle_state, syntheticAttackCheck);
    }

    public virtual AttackResolutionMetadata ResolveAttackMetadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackCheckInput attack_check,
        AttackContext attack_context
    )
    {
        attack_context ??= new AttackContext();
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(source_unit);
        int faithLuckBonus = _get_faith_luck_bonus(source_unit);
        int effectiveLuck = _get_effective_luck(source_unit);
        bool isDisadvantage = _resolve_attack_disadvantage(
            source_unit,
            target_unit,
            attack_context
        );
        bool isAdvantage = _resolve_attack_advantage(attack_check, attack_context);
        NormalizeAdvantageState(ref isDisadvantage, ref isAdvantage);
        if (attack_check.Invalid)
        {
            return new AttackResolutionMetadata
            {
                AttackResolution = ATTACK_RESOLUTION_MISS,
                AttackSuccess = false,
                OrdinaryMiss = true,
                IsDisadvantage = isDisadvantage,
                IsAdvantage = isAdvantage,
                HiddenLuckAtBirth = hiddenLuckAtBirth,
                FaithLuckBonus = faithLuckBonus,
                EffectiveLuck = effectiveLuck,
                RequiredRoll = attack_check.RequiredRoll,
                DisplayRequiredRoll = attack_check.DisplayRequiredRoll,
                HitRatePercent = attack_check.HitRatePercent,
                SuccessRatePercent = attack_check.SuccessRatePercent,
                SkillId = attack_context.SkillId,
            };
        }
        int critGateDie = FateAttackFormula.CalcCritGateDieSize(effectiveLuck, isDisadvantage);
        bool forceHitNoCrit = attack_check.ForceHitNoCrit || attack_context.ForceHitNoCrit;
        bool forceHitAllowCrit = attack_context.ForceHitAllowCrit;
        bool critLocked = BattleFateAttackRules.IsAttackCritLocked(source_unit) || forceHitNoCrit;
        int requiredRoll = attack_check.RequiredRoll;
        var metadata = new AttackResolutionMetadata
        {
            AttackResolution = ATTACK_RESOLUTION_MISS,
            IsDisadvantage = isDisadvantage,
            IsAdvantage = isAdvantage,
            HiddenLuckAtBirth = hiddenLuckAtBirth,
            FaithLuckBonus = faithLuckBonus,
            EffectiveLuck = effectiveLuck,
            CritLocked = critLocked,
            CritGateDie = critGateDie,
            FumbleLowEnd = FateAttackFormula.CalcFumbleLowEnd(effectiveLuck),
            CritThreshold = FateAttackFormula.CalcCritThreshold(
                hiddenLuckAtBirth,
                faithLuckBonus
            ),
            RequiredRoll = requiredRoll,
            DisplayRequiredRoll =
                attack_check.DisplayRequiredRoll != 0
                    ? attack_check.DisplayRequiredRoll
                    : Math.Clamp(requiredRoll, 2, NATURAL_HIT_ROLL),
            HitRatePercent = attack_check.HitRatePercent,
            SuccessRatePercent = attack_check.SuccessRatePercent,
            SkillId = attack_context.SkillId,
        };
        if (forceHitNoCrit)
        {
            metadata.AttackResolution = ATTACK_RESOLUTION_HIT;
            metadata.AttackSuccess = true;
            return metadata;
        }

        if (critGateDie > NATURAL_HIT_ROLL)
        {
            int critGateRoll = _roll_attack_die(
                critGateDie,
                isDisadvantage,
                isAdvantage,
                attack_context
            );
            metadata.CritGateRoll = critGateRoll;
            if (BattleFateAttackRules.DoesGateDieCrit(critGateRoll, critGateDie, critLocked))
            {
                metadata.AttackResolution = ATTACK_RESOLUTION_CRITICAL_HIT;
                metadata.AttackSuccess = true;
                metadata.CriticalHit = true;
                return metadata;
            }
        }

        int hitRoll = _roll_attack_die(
            NATURAL_HIT_ROLL,
            isDisadvantage,
            isAdvantage,
            attack_context
        );
        metadata.HitRoll = hitRoll;
        AttackTraitTriggerResult naturalOneTraitResult = _resolve_natural_one_trait_reroll(
            source_unit,
            hitRoll,
            attack_context
        );
        if (naturalOneTraitResult.Triggered)
        {
            hitRoll = naturalOneTraitResult.RerolledRoll;
            metadata.HitRoll = hitRoll;
            metadata.TraitTriggerResults.Add(naturalOneTraitResult);
        }

        if (hitRoll <= metadata.FumbleLowEnd)
        {
            if (forceHitAllowCrit)
            {
                metadata.AttackResolution = ATTACK_RESOLUTION_HIT;
                metadata.AttackSuccess = true;
                return metadata;
            }
            if (_try_apply_reverse_fate_amulet(source_unit))
            {
                metadata.AttackResolution = ATTACK_RESOLUTION_MISS;
                metadata.OrdinaryMiss = true;
                metadata.ReverseFateDowngraded = true;
                return metadata;
            }
            metadata.AttackResolution = ATTACK_RESOLUTION_CRITICAL_FAIL;
            metadata.CriticalFail = true;
            return metadata;
        }

        if (
            BattleFateAttackRules.IsHighThreatCritRoll(
                hitRoll,
                critLocked,
                critGateDie,
                metadata.CritThreshold
            )
        )
        {
            metadata.AttackResolution = ATTACK_RESOLUTION_CRITICAL_HIT;
            metadata.AttackSuccess = true;
            metadata.CriticalHit = true;
            return metadata;
        }

        if (forceHitAllowCrit)
        {
            metadata.AttackResolution = ATTACK_RESOLUTION_HIT;
            metadata.AttackSuccess = true;
            return metadata;
        }

        if (BattleFateAttackRules.DoesAttackRollHit(hitRoll, attack_check))
        {
            bool forcedCritical = attack_check.ForceCriticalOnHit && !critLocked;
            metadata.AttackResolution = forcedCritical
                ? ATTACK_RESOLUTION_CRITICAL_HIT
                : ATTACK_RESOLUTION_HIT;
            metadata.AttackSuccess = true;
            metadata.CriticalHit = forcedCritical;
            return metadata;
        }

        metadata.OrdinaryMiss = true;
        return metadata;
    }

    public virtual BattleSpellControlMetadata ResolveSpellControlMetadataTyped(
        BattleUnitState source_unit,
        AttackContext attack_context
    )
    {
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(source_unit);
        int faithLuckBonus = _get_faith_luck_bonus(source_unit);
        int effectiveLuck = _get_effective_luck(source_unit);
        bool isDisadvantage = attack_context?.IsDisadvantage ?? false;
        int lockedSkillHitBonus = _get_skill_lock_hit_bonus_from_context(
            source_unit,
            attack_context
        );
        int critGateDie = FateAttackFormula.CalcCritGateDieSize(effectiveLuck, isDisadvantage);
        bool critLocked = BattleFateAttackRules.IsAttackCritLocked(source_unit);
        var metadata = new BattleSpellControlMetadata
        {
            AttackResolution = ATTACK_RESOLUTION_HIT,
            SpellControlResolution = "normal",
            AttackSuccess = true,
            CriticalHit = false,
            CriticalFail = false,
            OrdinaryMiss = false,
            IsDisadvantage = isDisadvantage,
            HiddenLuckAtBirth = hiddenLuckAtBirth,
            FaithLuckBonus = faithLuckBonus,
            EffectiveLuck = effectiveLuck,
            CritLocked = critLocked,
            CritGateDie = critGateDie,
            CritGateRoll = 0,
            HitRoll = 0,
            FumbleLowEnd = FateAttackFormula.CalcFumbleLowEnd(effectiveLuck),
            CritThreshold = FateAttackFormula.CalcCritThreshold(
                hiddenLuckAtBirth,
                faithLuckBonus
            ),
            LockedSkillHitBonus = lockedSkillHitBonus,
        };

        if (critGateDie > NATURAL_HIT_ROLL)
        {
            int critGateRoll = _roll_attack_die(critGateDie, isDisadvantage, false, attack_context);
            if (BattleFateAttackRules.DoesGateDieCrit(critGateRoll, critGateDie, critLocked))
            {
                return metadata with
                {
                    AttackResolution = ATTACK_RESOLUTION_CRITICAL_HIT,
                    SpellControlResolution = "critical_success",
                    CriticalHit = true,
                    CritGateRoll = critGateRoll,
                };
            }
            metadata = metadata with { CritGateRoll = critGateRoll };
        }

        int hitRoll = _roll_attack_die(NATURAL_HIT_ROLL, isDisadvantage, false, attack_context);
        metadata = metadata with { HitRoll = hitRoll };
        AttackTraitTriggerResult naturalOneTraitResult = _resolve_natural_one_trait_reroll(
            source_unit,
            hitRoll,
            attack_context
        );
        if (naturalOneTraitResult.Triggered)
        {
            hitRoll = naturalOneTraitResult.RerolledRoll;
            metadata = metadata with { HitRoll = hitRoll };
        }

        int effectiveHitRoll = hitRoll + lockedSkillHitBonus;
        metadata = metadata with { EffectiveHitRoll = effectiveHitRoll };
        if (effectiveHitRoll <= metadata.FumbleLowEnd)
        {
            if (_try_apply_reverse_fate_amulet(source_unit))
            {
                return metadata with
                {
                    SpellControlResolution = "reverse_fate_downgraded",
                    ReverseFateDowngraded = true,
                };
            }
            return metadata with
            {
                AttackResolution = ATTACK_RESOLUTION_CRITICAL_FAIL,
                SpellControlResolution = "critical_fail",
                AttackSuccess = false,
                CriticalFail = true,
            };
        }

        if (
            BattleFateAttackRules.IsHighThreatCritRoll(
                effectiveHitRoll,
                critLocked,
                critGateDie,
                metadata.CritThreshold
            )
        )
        {
            return metadata with
            {
                AttackResolution = ATTACK_RESOLUTION_CRITICAL_HIT,
                SpellControlResolution = "critical_success",
                CriticalHit = true,
            };
        }

        return metadata;
    }

    private int _get_skill_lock_hit_bonus(BattleUnitReadView unit_state, StringName skill_id)
    {
        if (!unit_state.IsValid || IsEmpty(skill_id))
        {
            return 0;
        }
        return Math.Max(unit_state.GetKnownSkillLockHitBonus(skill_id), 0);
    }

    private int _get_skill_lock_hit_bonus_from_context(
        BattleUnitState unit_state,
        AttackContext context
    )
    {
        if (context == null)
        {
            return 0;
        }
        return _get_skill_lock_hit_bonus(unit_state, context.SkillId);
    }

    public virtual int RollAttackDie(
        int die_size,
        bool is_disadvantage,
        AttackContext attack_context
    )
    {
        bool isAdvantage = attack_context?.HasIsAdvantage == true && attack_context.IsAdvantage;
        NormalizeAdvantageState(ref is_disadvantage, ref isAdvantage);
        return _roll_attack_die(die_size, is_disadvantage, isAdvantage, attack_context);
    }

    public string FormatAttackCheckPreview(AttackCheckInput attack_check)
    {
        int hitRatePercent = attack_check.SuccessRatePercent;
        int requiredRoll = attack_check.RequiredRoll;
        return $"{hitRatePercent}%（{_format_required_roll_text(requiredRoll)}）";
    }

    public string FormatAttackCheckResolution(
        AttackCheckInput attack_check,
        AttackRollResult attack_result
    )
    {
        string previewText = string.IsNullOrEmpty(attack_check.PreviewText)
            ? FormatAttackCheckPreview(attack_check)
            : attack_check.PreviewText;
        int roll = attack_result.Roll;
        StringName rollDisposition = IsEmpty(attack_result.RollDisposition)
            ? ResolveAttackRollDispositionForCheck(roll, attack_check)
            : attack_result.RollDisposition;
        if (rollDisposition == ROLL_DISPOSITION_NATURAL_AUTO_MISS)
        {
            return $"{previewText}，d20={roll}（天然 1 失手）";
        }
        if (rollDisposition == ROLL_DISPOSITION_NATURAL_AUTO_HIT)
        {
            return $"{previewText}，d20={roll}（天然 20 命中）";
        }
        return $"{previewText}，d20={roll}";
    }

    protected virtual int RollBattleD20(BattleState battle_state)
    {
        if (battle_state == null)
        {
            return NATURAL_MISS_ROLL;
        }
        battle_state.NextAttackRollNonce();
        return TrueRandomSeedService.RandiRange(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL);
    }

    private AttackTraitTriggerResult _resolve_natural_one_trait_reroll(
        BattleUnitState source_unit,
        int hit_roll,
        AttackContext attack_context
    )
    {
        if (_trait_trigger_hooks == null)
        {
            return new AttackTraitTriggerResult();
        }
        AttackTraitTriggerResult hookResult = _trait_trigger_hooks.OnNaturalOne(
            source_unit,
            hit_roll,
            NATURAL_HIT_ROLL
        );
        if (!hookResult.Triggered)
        {
            return hookResult;
        }
        if (!hookResult.RerollDie)
        {
            return hookResult;
        }
        int rerolledRoll = _roll_attack_die(NATURAL_HIT_ROLL, false, false, attack_context);
        return new AttackTraitTriggerResult(
            triggered: hookResult.Triggered,
            @event: hookResult.Event,
            traitId: hookResult.TraitId,
            effectType: hookResult.EffectType,
            originalRoll: hookResult.OriginalRoll,
            rerollDie: hookResult.RerollDie,
            rerolledRoll: rerolledRoll,
            dieSize: hookResult.DieSize,
            chargeKey: hookResult.ChargeKey,
            chargesRemaining: hookResult.ChargesRemaining
        );
    }

    private bool _resolve_attack_disadvantage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        AttackContext attack_context
    )
    {
        if (attack_context == null)
        {
            return false;
        }
        if (attack_context.HasIsDisadvantage)
        {
            return attack_context.IsDisadvantage;
        }
        BattleState battleState = attack_context.BattleState;
        if (battleState == null)
        {
            return false;
        }
        return battleState.IsAttackDisadvantage(source_unit, target_unit);
    }

    private static bool _resolve_attack_advantage(
        AttackCheckInput attack_check,
        AttackContext attack_context
    )
    {
        if (attack_context?.HasIsAdvantage == true)
        {
            return attack_context.IsAdvantage;
        }
        return attack_check.IsAdvantage;
    }

    private static void NormalizeAdvantageState(ref bool isDisadvantage, ref bool isAdvantage)
    {
        if (!isDisadvantage || !isAdvantage)
            return;
        isDisadvantage = false;
        isAdvantage = false;
    }

    private int _roll_attack_die(
        int die_size,
        bool is_disadvantage,
        bool is_advantage,
        AttackContext attack_context
    )
    {
        int normalizedDieSize = Math.Max(die_size, 1);
        BattleState battleState = attack_context?.BattleState;
        int firstRoll = _roll_attack_die_once(normalizedDieSize, attack_context, battleState);
        if (!is_disadvantage && !is_advantage)
        {
            return firstRoll;
        }
        int secondRoll = _roll_attack_die_once(normalizedDieSize, attack_context, battleState);
        return is_advantage ? Math.Max(firstRoll, secondRoll) : Math.Min(firstRoll, secondRoll);
    }

    private int _roll_attack_die_once(
        int die_size,
        AttackContext attack_context,
        BattleState battle_state
    )
    {
        if (attack_context != null && attack_context.TryConsumeAttackRollOverride(die_size, out int overrideRoll))
        {
            if (battle_state != null)
            {
                battle_state.NextAttackRollNonce();
            }
            return overrideRoll;
        }
        return RollTrueRandomAttackRange(1, die_size, battle_state);
    }

    protected virtual int RollTrueRandomAttackRange(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.NextAttackRollNonce();
        }
        return TrueRandomSeedService.RandiRange(min_value, max_value);
    }

    private bool _try_apply_reverse_fate_amulet(BattleUnitState source_unit)
    {
        if (source_unit == null)
        {
            return false;
        }
        if (
            !LowLuckRelicRules.UnitHasFlag(
                source_unit,
                LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.ReverseFateAmulet)
            )
        )
        {
            return false;
        }
        BattleAiBlackboard aiBlackboard = source_unit.ai_blackboard;
        if (aiBlackboard == null || aiBlackboard.low_luck_reverse_fate_used)
        {
            return false;
        }
        aiBlackboard.low_luck_reverse_fate_used = true;
        _apply_runtime_debuff_status(
            source_unit,
            LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.ReverseFateWeakened),
            LowLuckRelicRules.ReverseFateDurationTu,
            outgoing_damage_multiplier: LowLuckRelicRules.ReverseFateDamageMultiplier
        );
        return true;
    }

    private void _apply_runtime_debuff_status(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        GDictionary status_params = null,
        StringName source_unit_id = null,
        double? incoming_damage_multiplier = null,
        double? outgoing_damage_multiplier = null
    )
    {
        status_params ??= new GDictionary();
        source_unit_id ??= new StringName("");
        if (unit_state == null || IsEmpty(status_id))
        {
            return;
        }
        BattleStatusEffectState statusEntry = BuildRuntimeDebuffStatusEffect(
            status_id,
            duration_tu,
            source_unit_id,
            status_params,
            incoming_damage_multiplier,
            outgoing_damage_multiplier
        );
        unit_state.SetStatusEffect(statusEntry);
    }

    private static BattleStatusEffectState BuildRuntimeDebuffStatusEffect(
        StringName statusId,
        int durationTu,
        StringName sourceUnitId = default,
        GDictionary statusParams = null,
        double? incomingDamageMultiplier = null,
        double? outgoingDamageMultiplier = null
    )
    {
        var state = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = sourceUnitId,
            power = 1,
            stacks = 1,
            duration = Math.Max(durationTu, -1),
            counts_as_debuff_override = true,
            counts_as_debuff = true,
            incoming_damage_multiplier = incomingDamageMultiplier,
            outgoing_damage_multiplier = outgoingDamageMultiplier,
        };
        state.SetParamsTyped(BattleStatusEffectState.CopyResidualParamsPlain(statusParams));
        return state;
    }

    private int _resolve_repeat_attack_preview_stage_count(
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
        if (repeat_attack_effect.EffectKind == BattleEffectKind.FixedRepeatAttack)
        {
            return Math.Clamp(
                repeat_attack_effect.FixedAttackCount,
                1,
                REPEAT_ATTACK_PREVIEW_STAGE_GUARD
            );
        }
        if (active_unit.HasStatusEffect(STATUS_CROWN_BREAK_BROKEN_HAND))
        {
            return 1;
        }

        int skillLevel = 0;
        if (active_unit != null && skill_definition != null)
        {
            skillLevel = active_unit.GetKnownSkillLevelTyped(skill_definition.SkillId);
        }
        BattleRepeatAttackStageSpec firstStageSpec =
            BattleRepeatAttackStageSpec.FromRepeatAttackEffect(
                repeat_attack_effect,
                0,
                0,
                skillLevel,
                true
            );
        firstStageSpec = firstStageSpec.WithBaseResourceCost(
            _get_repeat_attack_preview_base_cost(
                skill_definition,
                firstStageSpec.cost_resource_kind
            )
        );
        int baseCost = firstStageSpec.base_resource_cost;
        if (baseCost <= 0)
        {
            return REPEAT_ATTACK_PREVIEW_STAGE_GUARD;
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
        while (stages < REPEAT_ATTACK_PREVIEW_STAGE_GUARD)
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

    private int _get_repeat_attack_preview_base_cost(
        SkillDefinition skill_definition,
        CombatResourceKind cost_resource_kind
    )
    {
        CombatSkillDefinition combatProfile = skill_definition?.CombatProfile;
        if (combatProfile == null)
            return 0;
        return cost_resource_kind switch
        {
            CombatResourceKind.Ap => combatProfile.ApCost,
            CombatResourceKind.Aura => combatProfile.AuraCost,
            CombatResourceKind.Mp => combatProfile.MpCost,
            CombatResourceKind.Stamina => combatProfile.StaminaCost,
            _ => 0,
        };
    }

    private int _get_unit_resource_value(
        BattleUnitState active_unit,
        CombatResourceKind cost_resource_kind
    )
    {
        return cost_resource_kind switch
        {
            CombatResourceKind.Ap => active_unit?.GetCurrentAp() ?? 0,
            CombatResourceKind.Aura => active_unit?.GetCurrentAura() ?? 0,
            CombatResourceKind.Mp => active_unit?.GetCurrentMp() ?? 0,
            CombatResourceKind.Stamina => active_unit?.GetCurrentStamina() ?? 0,
            _ => 0,
        };
    }

    internal string FormatRepeatAttackPreviewSummary(List<AttackCheckInput> stage_checks)
    {
        if (stage_checks.Count == 0)
        {
            return "";
        }
        var parts = new List<string>();
        foreach (AttackCheckInput stageCheck in stage_checks)
        {
            parts.Add(
                string.IsNullOrEmpty(stageCheck.PreviewText)
                    ? FormatAttackCheckPreview(stageCheck)
                    : stageCheck.PreviewText
            );
        }
        return $"预计命中率 {string.Join(" -> ", parts)}";
    }

    internal AttackCheckInput BuildFateAwareAttackCheckPreview(
        BattleState battle_state,
        BattleUnitReadView active_unit,
        BattleUnitReadView target_unit,
        AttackCheckInput attack_check
    )
    {
        if (attack_check.Invalid)
        {
            return new AttackCheckInput(
                requiredRoll: attack_check.RequiredRoll,
                displayRequiredRoll: attack_check.DisplayRequiredRoll,
                naturalOneAutoMiss: attack_check.NaturalOneAutoMiss,
                naturalTwentyAutoHit: attack_check.NaturalTwentyAutoHit,
                invalid: true,
                errorId: attack_check.ErrorId,
                errorMessage: attack_check.ErrorMessage,
                previewText: string.IsNullOrEmpty(attack_check.PreviewText)
                    ? "无效命中检定"
                    : attack_check.PreviewText
            );
        }
        int baseHitRatePercent = attack_check.HitRatePercent;
        if (battle_state == null || !active_unit.IsValid || !target_unit.IsValid)
        {
            return CopyAttackCheck(
                attack_check,
                baseHitRatePercent: baseHitRatePercent,
                previewText: FormatAttackCheckPreview(attack_check)
            );
        }

        bool isDisadvantage = battle_state.IsAttackDisadvantage(active_unit, target_unit);
        bool isAdvantage = attack_check.IsAdvantage;
        NormalizeAdvantageState(ref isDisadvantage, ref isAdvantage);
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(active_unit);
        int faithLuckBonus = _get_faith_luck_bonus(active_unit);
        int effectiveLuck = Math.Clamp(
            hiddenLuckAtBirth + faithLuckBonus,
            UnitBaseAttributes.EffectiveLuckMin,
            UnitBaseAttributes.EffectiveLuckMax
        );
        int critGateDie = FateAttackFormula.CalcCritGateDieSize(effectiveLuck, isDisadvantage);
        int critThreshold = FateAttackFormula.CalcCritThreshold(
            hiddenLuckAtBirth,
            faithLuckBonus
        );
        int fumbleLowEnd = FateAttackFormula.CalcFumbleLowEnd(effectiveLuck);
        bool critLocked = BattleFateAttackRules.IsAttackCritLocked(active_unit);
        int successRatePercent = _compute_fate_attack_success_rate_percent(
            attack_check,
            critLocked,
            critGateDie,
            critThreshold,
            fumbleLowEnd,
            isDisadvantage,
            isAdvantage
        );
        AttackCheckInput resolvedCheck = CopyAttackCheck(
            attack_check,
            hitRatePercent: successRatePercent,
            successRatePercent: successRatePercent,
            baseHitRatePercent: baseHitRatePercent,
            isDisadvantage: isDisadvantage,
            isAdvantage: isAdvantage,
            critGateDie: critGateDie,
            critThreshold: critThreshold,
            fumbleLowEnd: fumbleLowEnd,
            effectiveLuck: effectiveLuck,
            critLocked: critLocked
        );
        return CopyAttackCheck(
            resolvedCheck,
            previewText: FormatFateAwareAttackCheckPreview(resolvedCheck)
        );
    }

    internal string FormatFateAwareAttackCheckPreview(AttackCheckInput attack_check)
    {
        int successRatePercent = attack_check.SuccessRatePercent;
        string requiredRollText = _format_required_roll_text(attack_check.RequiredRoll);
        int baseHitRatePercent =
            attack_check.BaseHitRatePercent > 0
                ? attack_check.BaseHitRatePercent
                : successRatePercent;
        if (successRatePercent <= baseHitRatePercent)
        {
            return $"{successRatePercent}%（{requiredRollText}）";
        }
        bool critLocked = attack_check.CritLocked;
        int critGateDie = attack_check.CritGateDie;
        if (!critLocked && critGateDie == NATURAL_HIT_ROLL)
        {
            return $"{successRatePercent}%（{requiredRollText}；高位大成功 {attack_check.CritThreshold}-20 直达）";
        }
        if (!critLocked && critGateDie > NATURAL_HIT_ROLL)
        {
            return $"{successRatePercent}%（{requiredRollText}；含门骰 d{critGateDie}）";
        }
        return $"{successRatePercent}%（{requiredRollText}）";
    }

    private int _compute_fate_attack_success_rate_percent(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage,
        bool is_advantage
    )
    {
        double basisPoints = _compute_fate_attack_success_rate_basis_points(
            attack_check,
            crit_locked,
            crit_gate_die,
            crit_threshold,
            fumble_low_end,
            is_disadvantage,
            is_advantage
        );
        return Math.Clamp((int)Math.Round(basisPoints / 100.0), 0, 100);
    }

    private double _compute_fate_attack_success_rate_basis_points(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage,
        bool is_advantage
    )
    {
        double d20SuccessBasisPoints = _compute_d20_attack_success_rate_basis_points(
            attack_check,
            crit_locked,
            crit_gate_die,
            crit_threshold,
            fumble_low_end,
            is_disadvantage,
            is_advantage
        );
        if (crit_locked || crit_gate_die <= NATURAL_HIT_ROLL)
        {
            return d20SuccessBasisPoints;
        }
        double gateCritBasisPoints = 10000.0 / crit_gate_die;
        if (is_disadvantage)
        {
            gateCritBasisPoints /= crit_gate_die;
        }
        else if (is_advantage)
        {
            double missGateChance = (crit_gate_die - 1.0) / crit_gate_die;
            gateCritBasisPoints = 10000.0 * (1.0 - missGateChance * missGateChance);
        }
        return gateCritBasisPoints
            + (10000.0 - gateCritBasisPoints) * d20SuccessBasisPoints / 10000.0;
    }

    private double _compute_d20_attack_success_rate_basis_points(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage,
        bool is_advantage
    )
    {
        int successOutcomes = 0;
        int totalOutcomes = NATURAL_HIT_ROLL;
        if (!is_disadvantage && !is_advantage)
        {
            for (int roll = NATURAL_MISS_ROLL; roll <= NATURAL_HIT_ROLL; roll++)
            {
                if (
                    _is_d20_attack_success_roll(
                        roll,
                        attack_check,
                        crit_locked,
                        crit_gate_die,
                        crit_threshold,
                        fumble_low_end
                    )
                )
                {
                    successOutcomes += 1;
                }
            }
            return successOutcomes * 10000.0 / totalOutcomes;
        }
        totalOutcomes *= NATURAL_HIT_ROLL;
        for (int firstRoll = NATURAL_MISS_ROLL; firstRoll <= NATURAL_HIT_ROLL; firstRoll++)
        {
            for (int secondRoll = NATURAL_MISS_ROLL; secondRoll <= NATURAL_HIT_ROLL; secondRoll++)
            {
                int roll = is_advantage
                    ? Math.Max(firstRoll, secondRoll)
                    : Math.Min(firstRoll, secondRoll);
                if (
                    _is_d20_attack_success_roll(
                        roll,
                        attack_check,
                        crit_locked,
                        crit_gate_die,
                        crit_threshold,
                        fumble_low_end
                    )
                )
                {
                    successOutcomes += 1;
                }
            }
        }
        return successOutcomes * 10000.0 / totalOutcomes;
    }

    private bool _is_d20_attack_success_roll(
        int roll,
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end
    )
    {
        if (roll <= fumble_low_end)
        {
            return false;
        }
        if (
            BattleFateAttackRules.IsHighThreatCritRoll(
                roll,
                crit_locked,
                crit_gate_die,
                crit_threshold
            )
        )
        {
            return true;
        }
        return BattleFateAttackRules.DoesAttackRollHit(roll, attack_check);
    }

    private int _get_hidden_luck_at_birth(BattleUnitReadView unit_state)
    {
        return unit_state.GetAttributeValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth),
            0
        );
    }

    private int _get_faith_luck_bonus(BattleUnitReadView unit_state)
    {
        return unit_state.GetAttributeValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.FaithLuckBonus),
            0
        );
    }

    private int _get_effective_luck(BattleUnitReadView unit_state)
    {
        return Math.Clamp(
            _get_hidden_luck_at_birth(unit_state) + _get_faith_luck_bonus(unit_state),
            UnitBaseAttributes.EffectiveLuckMin,
            UnitBaseAttributes.EffectiveLuckMax
        );
    }

    private double _average_ints(GIntArray values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }
        int total = 0;
        foreach (int value in values)
        {
            total += value;
        }
        return (double)total / values.Count;
    }

    private int _compute_hit_rate_percent(int required_roll)
    {
        int successCount = 0;
        for (int roll = NATURAL_MISS_ROLL; roll <= NATURAL_HIT_ROLL; roll++)
        {
            if (_is_attack_roll_success(roll, required_roll))
            {
                successCount += 1;
            }
        }
        return successCount * 5;
    }

    private bool _is_attack_roll_success(int roll, int required_roll)
    {
        return _is_attack_roll_disposition_success(
            _resolve_attack_roll_disposition(roll, required_roll)
        );
    }

    private int _compute_attack_check_success_rate_percent(AttackCheckInput attack_check)
    {
        int successCount = 0;
        for (int roll = NATURAL_MISS_ROLL; roll <= NATURAL_HIT_ROLL; roll++)
        {
            if (
                _is_attack_roll_disposition_success(
                    ResolveAttackRollDispositionForCheck(roll, attack_check)
                )
            )
            {
                successCount += 1;
            }
        }
        return successCount * 5;
    }

    internal StringName ResolveAttackRollDispositionForCheck(
        int roll,
        AttackCheckInput attack_check
    )
    {
        int requiredRoll = attack_check.RequiredRoll;
        if (attack_check.NaturalOneAutoMiss && roll <= NATURAL_MISS_ROLL)
        {
            return ROLL_DISPOSITION_NATURAL_AUTO_MISS;
        }
        if (attack_check.NaturalTwentyAutoHit && roll >= NATURAL_HIT_ROLL)
        {
            return ROLL_DISPOSITION_NATURAL_AUTO_HIT;
        }
        if (roll >= requiredRoll)
        {
            return ROLL_DISPOSITION_THRESHOLD_HIT;
        }
        return ROLL_DISPOSITION_THRESHOLD_MISS;
    }

    private StringName _resolve_attack_roll_disposition(int roll, int required_roll)
    {
        if (roll <= NATURAL_MISS_ROLL)
        {
            return ROLL_DISPOSITION_NATURAL_AUTO_MISS;
        }
        if (roll >= NATURAL_HIT_ROLL)
        {
            return ROLL_DISPOSITION_NATURAL_AUTO_HIT;
        }
        if (roll >= required_roll)
        {
            return ROLL_DISPOSITION_THRESHOLD_HIT;
        }
        return ROLL_DISPOSITION_THRESHOLD_MISS;
    }

    private bool _is_attack_roll_disposition_success(StringName roll_disposition)
    {
        return roll_disposition == ROLL_DISPOSITION_THRESHOLD_HIT
            || roll_disposition == ROLL_DISPOSITION_NATURAL_AUTO_HIT;
    }

    private int _get_display_required_roll(int required_roll)
    {
        return Math.Clamp(required_roll, NATURAL_MISS_ROLL + 1, NATURAL_HIT_ROLL);
    }

    private string _format_required_roll_text(int required_roll)
    {
        int displayRequiredRoll = _get_display_required_roll(required_roll);
        if (required_roll <= NATURAL_MISS_ROLL + 1)
        {
            return $"需 {displayRequiredRoll}+（天然 1 仍失手）";
        }
        if (required_roll > NATURAL_HIT_ROLL)
        {
            return $"需 {displayRequiredRoll}+（仅天然 20）";
        }
        return $"需 {displayRequiredRoll}+";
    }

    private int _get_required_roll_for_hit_rate(int hit_rate_percent)
    {
        int clampedHitRate = Math.Clamp(hit_rate_percent, 0, 100);
        int successfulRolls = (int)Math.Ceiling(clampedHitRate / 5.0);
        return ATTACK_CHECK_TARGET - successfulRolls;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static AttackCheckInput CopyAttackCheck(
        AttackCheckInput source,
        int? hitRatePercent = null,
        int? successRatePercent = null,
        int? baseHitRatePercent = null,
        bool? isDisadvantage = null,
        bool? isAdvantage = null,
        int? critGateDie = null,
        int? critThreshold = null,
        int? fumbleLowEnd = null,
        int? effectiveLuck = null,
        bool? critLocked = null,
        string previewText = null
    )
    {
        return new AttackCheckInput(
            attackerBaseAttackBonus: source.AttackerBaseAttackBonus,
            attackerAttackBonus: source.AttackerAttackBonus,
            attackerBab: source.AttackerBab,
            targetArmorClass: source.TargetArmorClass,
            skillAttackBonus: source.SkillAttackBonus,
            lockedSkillHitBonus: source.LockedSkillHitBonus,
            situationalAttackBonus: source.SituationalAttackBonus,
            situationalAttackPenalty: source.SituationalAttackPenalty,
            requiredRoll: source.RequiredRoll,
            displayRequiredRoll: source.DisplayRequiredRoll,
            hitRatePercent: hitRatePercent ?? source.HitRatePercent,
            successRatePercent: successRatePercent ?? source.SuccessRatePercent,
            baseHitRatePercent: baseHitRatePercent ?? source.BaseHitRatePercent,
            naturalOneAutoMiss: source.NaturalOneAutoMiss,
            naturalTwentyAutoHit: source.NaturalTwentyAutoHit,
            critThreshold: critThreshold ?? source.CritThreshold,
            fumbleLowEnd: fumbleLowEnd ?? source.FumbleLowEnd,
            critLocked: critLocked ?? source.CritLocked,
            critGateDie: critGateDie ?? source.CritGateDie,
            effectiveLuck: effectiveLuck ?? source.EffectiveLuck,
            forceHitNoCrit: source.ForceHitNoCrit,
            forceCriticalOnHit: source.ForceCriticalOnHit,
            forcedCriticalSourceEquipmentInstanceId: source.ForcedCriticalSourceEquipmentInstanceId,
            forcedCriticalSourceBindingId: source.ForcedCriticalSourceBindingId,
            forcedCriticalSourceActionId: source.ForcedCriticalSourceActionId,
            skillId: source.SkillId,
            followUpAttackPenalty: source.FollowUpAttackPenalty,
            exponentialPenalty: source.ExponentialPenalty,
            isDisadvantage: isDisadvantage ?? source.IsDisadvantage,
            isAdvantage: isAdvantage ?? source.IsAdvantage,
            invalid: source.Invalid,
            errorId: source.ErrorId,
            errorMessage: source.ErrorMessage,
            previewText: previewText ?? source.PreviewText
        );
    }

    private static string _get_attack_roll_nonce_text(BattleState battle_state) =>
        battle_state != null ? battle_state.attack_roll_nonce.ToString() : "";
}
