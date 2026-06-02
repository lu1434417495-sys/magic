using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;

// 翻译自 battle_hit_resolver.gd（2026-05-26，命中检定 C# 迁移）。
// 无 runtime 的规则解析器：BAB/降序AC/d20 命中与真随机掷骰口径统一收敛在此。
[GlobalClass]
public partial class BattleHitResolver : RefCounted
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

    public AttackRollResult resolve_repeat_attack_stage_hit(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        AttackCheckInput attackCheck = build_repeat_attack_stage_hit_check(
            active_unit,
            target_unit,
            skill_def,
            repeat_attack_effect,
            stage_index
        );
        return roll_attack_check(battle_state, attackCheck);
    }

    public AttackCheckInput build_repeat_attack_stage_hit_check(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        int skillLevel =
            active_unit != null && skill_def != null
                ? GetInt(active_unit.known_skill_level_map, skill_def.skill_id, 0)
                : 0;
        BattleRepeatAttackStageSpec stageSpec =
            BattleRepeatAttackStageSpec.from_repeat_attack_effect(
                repeat_attack_effect,
                stage_index,
                0,
                skillLevel
            );
        return build_skill_attack_check(
            active_unit,
            target_unit,
            skill_def,
            stageSpec.stage_base_attack_bonus,
            stageSpec.resolve_stage_attack_penalty()
        );
    }

    public AttackCheckInput build_fate_aware_repeat_attack_stage_hit_check(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        AttackCheckInput baseAttackCheck = build_repeat_attack_stage_hit_check(
            active_unit,
            target_unit,
            skill_def,
            repeat_attack_effect,
            stage_index
        );
        return _build_fate_aware_attack_check_preview(
            battle_state,
            active_unit,
            target_unit,
            baseAttackCheck
        );
    }

    public AttackPreviewData build_repeat_attack_preview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int preview_stage_count = -1
    )
    {
        if (
            active_unit == null
            || target_unit == null
            || skill_def == null
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
                skill_def,
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
            AttackCheckInput attackCheck = build_fate_aware_repeat_attack_stage_hit_check(
                battle_state,
                active_unit,
                target_unit,
                skill_def,
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
        GDictionary effectParams = repeat_attack_effect?.@params ?? new GDictionary();
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
            SummaryText = _format_repeat_attack_preview_summary(stageChecks),
            Stages = stages,
            HitRatePercent = avgSuccessRate,
            SuccessRatePercent = avgSuccessRate,
            BaseHitRatePercent = avgBaseHitRate,
            BaseAttackBonus = effectParams != null ? GetInt(effectParams, "base_attack_bonus", 0) : 0,
            FollowUpAttackPenalty = effectParams != null
                ? GetInt(effectParams, "follow_up_attack_penalty", 0)
                : 0,
        };
    }

    public AttackPreviewData build_skill_attack_preview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        bool force_hit_no_crit = false
    )
    {
        if (active_unit == null || target_unit == null || skill_def == null)
        {
            return new AttackPreviewData();
        }
        if (force_hit_no_crit)
        {
            return build_force_hit_no_crit_attack_preview();
        }
        AttackCheckInput attackCheck = _build_fate_aware_attack_check_preview(
            battle_state,
            active_unit,
            target_unit,
            build_skill_attack_check(active_unit, target_unit, skill_def)
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
        };
    }

    public AttackPreviewData build_force_hit_no_crit_attack_preview()
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
        };
    }

    public AttackCheckInput build_skill_attack_check(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def
    )
    {
        return build_skill_attack_check(active_unit, target_unit, skill_def, 0, 0);
    }

    public AttackCheckInput build_skill_attack_check(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        int flat_bonus = 0,
        int flat_penalty = 0
    )
    {
        int attackerBaseAttackBonus = _get_unit_attribute_value(
            active_unit,
            AttributeService.BASE_ATTACK_BONUS_ID(),
            0
        );
        int attackerAttackBonus = _get_unit_attribute_value(
            active_unit,
            AttributeService.ATTACK_BONUS_ID(),
            0
        );
        if (!_unit_has_attribute_value(target_unit, AttributeService.ARMOR_CLASS_ID()))
        {
            string errorMessage =
                "BattleHitResolver cannot build attack check: target unit is missing armor_class.";
            GameLog.Error(errorMessage, "battle.hit.resolve_failed", "battle");
            return _build_invalid_attack_check(
                ATTACK_CHECK_ERROR_MISSING_TARGET_ARMOR_CLASS,
                errorMessage
            );
        }
        int targetArmorClass = _get_target_armor_class(target_unit);
        int skillLevel = 0;
        if (active_unit != null && skill_def != null)
        {
            GDictionary knownSkillLevelMap = active_unit.known_skill_level_map;
            StringName skillId = skill_def.skill_id;
            if (TryGetValue(knownSkillLevelMap, skillId, out dynamic skillLevelValue))
            {
                skillLevel = ToInt(skillLevelValue, 0);
            }
            else if (active_unit.known_active_skill_ids.Contains(skillId))
            {
                skillLevel = 1;
            }
        }
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        int skillAttackBonus =
            (combatProfile as CombatSkillDef)?.get_effective_attack_roll_bonus(skillLevel) ?? 0;
        int lockedSkillHitBonus = _get_skill_lock_hit_bonus(
            active_unit,
            skill_def?.skill_id ?? new StringName("")
        );
        int statusAttackBonusDelta = _get_attacker_status_attack_bonus_delta(active_unit);
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

    public int _get_unit_attribute_value(
        BattleUnitState unit_state,
        StringName attribute_id,
        int fallback = 0
    )
    {
        AttributeSnapshot snapshot = unit_state?.attribute_snapshot;
        if (snapshot == null)
        {
            return fallback;
        }
        if (!snapshot.has_value(attribute_id))
        {
            return fallback;
        }
        return snapshot.get_value(attribute_id);
    }

    public bool _unit_has_attribute_value(BattleUnitState unit_state, StringName attribute_id)
    {
        AttributeSnapshot snapshot = unit_state?.attribute_snapshot;
        return snapshot != null && snapshot.has_value(attribute_id);
    }

    public int _get_target_armor_class(BattleUnitState target_unit)
    {
        int targetArmorClass = _get_unit_attribute_value(
            target_unit,
            AttributeService.ARMOR_CLASS_ID(),
            0
        );
        targetArmorClass -= _get_target_armor_break_penalty(target_unit);
        if (_is_target_dodge_bonus_locked(target_unit))
        {
            targetArmorClass -= Math.Max(
                _get_unit_attribute_value(target_unit, AttributeService.DODGE_BONUS_ID(), 0),
                0
            );
        }
        else
        {
            targetArmorClass += _get_target_status_dodge_bonus(target_unit);
        }
        return Math.Max(targetArmorClass, 1);
    }

    public AttackCheckInput _build_invalid_attack_check(StringName error_id, string error_message)
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

    public int _get_target_armor_break_penalty(BattleUnitState target_unit)
    {
        if (target_unit == null)
        {
            return 0;
        }
        var statusEntry = target_unit.get_status_effect(STATUS_ARMOR_BREAK);
        if (statusEntry == null)
        {
            return 0;
        }
        return Math.Max(Math.Max(statusEntry.power, statusEntry.stacks), 1) * 2;
    }

    public int _get_target_status_dodge_bonus(BattleUnitState target_unit)
    {
        if (target_unit == null)
        {
            return 0;
        }
        var statusEntry = target_unit.get_status_effect(STATUS_DODGE_BONUS_UP);
        if (statusEntry == null)
        {
            return 0;
        }
        return Math.Max(Math.Max(statusEntry.power, statusEntry.stacks), 1) * 2;
    }

    public int _get_attacker_status_attack_bonus_delta(BattleUnitState active_unit)
    {
        if (active_unit == null)
        {
            return 0;
        }
        int attackDelta = 0;
        if (active_unit.has_status_effect(STATUS_BLACK_STAR_BRAND_ELITE))
        {
            attackDelta = BLACK_STAR_BRAND_ATTACK_BONUS_DELTA;
        }
        else
        {
            var statusEntry = active_unit.get_status_effect(STATUS_ATTACK_ROLL_BONUS_UP);
            if (statusEntry != null)
            {
                attackDelta = Math.Max(statusEntry.power, statusEntry.stacks);
            }
        }
        return attackDelta - _get_attacker_status_attack_penalty(active_unit);
    }

    public int _get_attacker_status_attack_penalty(BattleUnitState active_unit)
    {
        if (active_unit == null)
        {
            return 0;
        }
        int penalty = 0;
        foreach (var statusIdValue in active_unit.status_effects.Keys)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            var statusEntry = active_unit.get_status_effect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            penalty = Math.Max(
                penalty,
                BattleStatusSemanticTable.GetAttackRollPenalty(statusEntry)
            );
        }
        return penalty;
    }

    public bool _is_target_dodge_bonus_locked(BattleUnitState target_unit)
    {
        return target_unit != null
            && (
                target_unit.has_status_effect(STATUS_CROWN_BREAK_BLINDED_EYE)
                || _unit_has_status_bool_param(target_unit, "lock_dodge_bonus")
            );
    }

    public bool _unit_has_status_bool_param(BattleUnitState unit_state, StringName param_key)
    {
        if (unit_state == null || IsEmpty(param_key))
        {
            return false;
        }
        foreach (var statusIdValue in unit_state.status_effects.Keys)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            var statusEntry = unit_state.get_status_effect(statusId);
            if (statusEntry == null || statusEntry.@params == null)
            {
                continue;
            }
            if (_get_status_param_bool(statusEntry.@params, param_key, false))
            {
                return true;
            }
        }
        return false;
    }

    public bool _get_status_param_bool(
        GDictionary @params,
        StringName param_key,
        bool fallback = false
    )
    {
        if (@params == null || IsEmpty(param_key))
        {
            return fallback;
        }
        if (!TryGetValue(@params, param_key, out dynamic value))
            return fallback;
        return value.AsBool();
    }

    public virtual AttackRollResult roll_attack_check(BattleState battle_state, AttackCheckInput attack_check)
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
        int roll = _roll_battle_d20(battle_state);
        StringName rollDisposition = _resolve_attack_roll_disposition_for_check(roll, attack_check);
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
            resolutionText: format_attack_check_resolution(attack_check, result),
            attackRollNonce: result.AttackRollNonce
        );
    }

    public virtual AttackRollResult roll_hit_rate(BattleState battle_state, int hit_rate_percent)
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
        return roll_attack_check(battle_state, syntheticAttackCheck);
    }

    public virtual AttackResolutionMetadata resolve_attack_metadata(
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
        if (attack_check.Invalid)
        {
            return new AttackResolutionMetadata
            {
                AttackResolution = ATTACK_RESOLUTION_MISS,
                AttackSuccess = false,
                OrdinaryMiss = true,
                IsDisadvantage = isDisadvantage,
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
        bool critLocked = BattleFateAttackRules.IsAttackCritLocked(source_unit) || forceHitNoCrit;
        int requiredRoll = attack_check.RequiredRoll;
        var metadata = new AttackResolutionMetadata
        {
            AttackResolution = ATTACK_RESOLUTION_MISS,
            IsDisadvantage = isDisadvantage,
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
            int critGateRoll = _roll_attack_die(critGateDie, isDisadvantage, attack_context);
            metadata.CritGateRoll = critGateRoll;
            if (BattleFateAttackRules.DoesGateDieCrit(critGateRoll, critGateDie, critLocked))
            {
                metadata.AttackResolution = ATTACK_RESOLUTION_CRITICAL_HIT;
                metadata.AttackSuccess = true;
                metadata.CriticalHit = true;
                return metadata;
            }
        }

        int hitRoll = _roll_attack_die(NATURAL_HIT_ROLL, isDisadvantage, attack_context);
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

        if (BattleFateAttackRules.DoesAttackRollHit(hitRoll, attack_check))
        {
            metadata.AttackResolution = ATTACK_RESOLUTION_HIT;
            metadata.AttackSuccess = true;
            return metadata;
        }

        metadata.OrdinaryMiss = true;
        return metadata;
    }

    public virtual GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        AttackContext attack_context
    ) => resolve_spell_control_metadata_typed(source_unit, attack_context).ToDictionary();

    public virtual BattleSpellControlMetadata resolve_spell_control_metadata_typed(
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
            int critGateRoll = _roll_attack_die(critGateDie, isDisadvantage, attack_context);
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

        int hitRoll = _roll_attack_die(NATURAL_HIT_ROLL, isDisadvantage, attack_context);
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

    public int _get_skill_lock_hit_bonus(BattleUnitState unit_state, StringName skill_id)
    {
        if (unit_state == null || IsEmpty(skill_id))
        {
            return 0;
        }
        return Math.Max(
            GetInt(
                unit_state.known_skill_lock_hit_bonus_map,
                skill_id,
                0
            ),
            0
        );
    }

    public int _get_skill_lock_hit_bonus_from_context(
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

    public virtual int roll_attack_die(
        int die_size,
        bool is_disadvantage,
        AttackContext attack_context
    )
    {
        return _roll_attack_die(die_size, is_disadvantage, attack_context);
    }

    public string format_attack_check_preview(AttackCheckInput attack_check)
    {
        int hitRatePercent = attack_check.SuccessRatePercent;
        int requiredRoll = attack_check.RequiredRoll;
        return $"{hitRatePercent}%（{_format_required_roll_text(requiredRoll)}）";
    }

    public string format_attack_check_resolution(
        AttackCheckInput attack_check,
        AttackRollResult attack_result
    )
    {
        string previewText = string.IsNullOrEmpty(attack_check.PreviewText)
            ? format_attack_check_preview(attack_check)
            : attack_check.PreviewText;
        int roll = attack_result.Roll;
        StringName rollDisposition = IsEmpty(attack_result.RollDisposition)
            ? _resolve_attack_roll_disposition_for_check(roll, attack_check)
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

    public int _roll_battle_d20(BattleState battle_state)
    {
        if (battle_state == null)
        {
            return NATURAL_MISS_ROLL;
        }
        battle_state.next_attack_roll_nonce();
        return TrueRandomSeedService.randi_range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL);
    }

    public AttackTraitTriggerResult _resolve_natural_one_trait_reroll(
        BattleUnitState source_unit,
        int hit_roll,
        AttackContext attack_context
    )
    {
        if (_trait_trigger_hooks == null)
        {
            return new AttackTraitTriggerResult();
        }
        AttackTraitTriggerResult hookResult = _trait_trigger_hooks.on_natural_one_typed(
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
        int rerolledRoll = _roll_attack_die(NATURAL_HIT_ROLL, false, attack_context);
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

    public bool _resolve_attack_disadvantage(
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
        return battleState.is_attack_disadvantage(source_unit, target_unit);
    }

    public int _roll_attack_die(int die_size, bool is_disadvantage, AttackContext attack_context)
    {
        int normalizedDieSize = Math.Max(die_size, 1);
        BattleState battleState = attack_context?.BattleState;
        int firstRoll = _roll_attack_die_once(normalizedDieSize, attack_context, battleState);
        if (!is_disadvantage)
        {
            return firstRoll;
        }
        int secondRoll = _roll_attack_die_once(normalizedDieSize, attack_context, battleState);
        return Math.Min(firstRoll, secondRoll);
    }

    public int _roll_attack_die_once(
        int die_size,
        AttackContext attack_context,
        BattleState battle_state
    )
    {
        if (attack_context != null && attack_context.TryConsumeAttackRollOverride(die_size, out int overrideRoll))
        {
            if (battle_state != null)
            {
                battle_state.next_attack_roll_nonce();
            }
            return overrideRoll;
        }
        return _roll_true_random_attack_range(1, die_size, battle_state);
    }

    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.next_attack_roll_nonce();
        }
        return TrueRandomSeedService.randi_range(min_value, max_value);
    }

    public bool _try_apply_reverse_fate_amulet(BattleUnitState source_unit)
    {
        if (source_unit == null)
        {
            return false;
        }
        if (
            !LowLuckRelicRules.UnitHasFlag(
                source_unit,
                LowLuckRelicRules.ATTR_REVERSE_FATE_AMULET
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
        _apply_runtime_status(
            source_unit,
            LowLuckRelicRules.STATUS_REVERSE_FATE_WEAKENED,
            LowLuckRelicRules.REVERSE_FATE_DURATION_TU,
            new GDictionary
            {
                ["outgoing_damage_multiplier"] = LowLuckRelicRules.REVERSE_FATE_DAMAGE_MULTIPLIER,
                ["counts_as_debuff"] = true,
            }
        );
        return true;
    }

    public void _apply_runtime_status(
        BattleUnitState unit_state,
        StringName status_id,
        int duration_tu,
        GDictionary status_params = null,
        StringName source_unit_id = null
    )
    {
        status_params ??= new GDictionary();
        source_unit_id ??= new StringName("");
        if (unit_state == null || IsEmpty(status_id))
        {
            return;
        }
        var statusEntry = new BattleStatusEffectState
        {
            status_id = status_id,
            source_unit_id = source_unit_id,
            power = 1,
            stacks = 1,
            duration = Math.Max(duration_tu, -1),
            @params = (GDictionary)status_params.Duplicate(true),
        };
        unit_state.set_status_effect(statusEntry);
    }

    public void _append_trait_trigger_result(GDictionary target, AttackTraitTriggerResult trigger_result)
    {
        if (target == null || !trigger_result.Triggered)
        {
            return;
        }
        GArray results = (GArray)GetArray(target, "trait_trigger_results").Duplicate(true);
        results.Add(new GDictionary
        {
            ["triggered"] = trigger_result.Triggered,
            ["event"] = trigger_result.Event,
            ["trait_id"] = trigger_result.TraitId,
            ["effect_type"] = trigger_result.EffectType,
            ["original_roll"] = trigger_result.OriginalRoll,
            ["reroll_die"] = trigger_result.RerollDie,
            ["rerolled_roll"] = trigger_result.RerolledRoll,
            ["die_size"] = trigger_result.DieSize,
            ["charge_key"] = trigger_result.ChargeKey,
            ["charges_remaining"] = trigger_result.ChargesRemaining,
        });
        target["trait_trigger_results"] = results;
    }

    public int _resolve_repeat_attack_preview_stage_count(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (
            active_unit == null
            || skill_def == null
            || combatProfile == null
            || repeat_attack_effect == null
        )
        {
            return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT;
        }
        if (active_unit.has_status_effect(STATUS_CROWN_BREAK_BROKEN_HAND))
        {
            return 1;
        }

        int skillLevel = 0;
        if (
            active_unit.known_skill_level_map != null
            && skill_def != null
            && TryGetValue(active_unit.known_skill_level_map, skill_def.skill_id, out dynamic skillLevelValue)
        )
        {
            skillLevel = ToInt(skillLevelValue, 0);
        }
        BattleRepeatAttackStageSpec firstStageSpec =
            BattleRepeatAttackStageSpec.from_repeat_attack_effect(
                repeat_attack_effect,
                0,
                0,
                skillLevel,
                true
            );
        firstStageSpec = firstStageSpec.with_base_resource_cost(
            _get_repeat_attack_preview_base_cost(skill_def, firstStageSpec.cost_resource_kind)
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

    public int _get_repeat_attack_preview_base_cost(
        SkillDef skill_def,
        CombatResourceKind cost_resource_kind
    )
    {
        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (combatProfile == null)
            return 0;
        return cost_resource_kind switch
        {
            CombatResourceKind.Ap => combatProfile.ap_cost,
            CombatResourceKind.Aura => combatProfile.aura_cost,
            CombatResourceKind.Mp => combatProfile.mp_cost,
            CombatResourceKind.Stamina => combatProfile.stamina_cost,
            _ => 0,
        };
    }

    public int _get_unit_resource_value(
        BattleUnitState active_unit,
        CombatResourceKind cost_resource_kind
    )
    {
        return cost_resource_kind switch
        {
            CombatResourceKind.Ap => active_unit?.current_ap ?? 0,
            CombatResourceKind.Aura => active_unit?.current_aura ?? 0,
            CombatResourceKind.Mp => active_unit?.current_mp ?? 0,
            CombatResourceKind.Stamina => active_unit?.current_stamina ?? 0,
            _ => 0,
        };
    }

    public string _format_repeat_attack_preview_summary(List<AttackCheckInput> stage_checks)
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
                    ? format_attack_check_preview(stageCheck)
                    : stageCheck.PreviewText
            );
        }
        return $"预计命中率 {string.Join(" -> ", parts)}";
    }

    public AttackCheckInput _build_fate_aware_attack_check_preview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
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
        if (battle_state == null || active_unit == null || target_unit == null)
        {
            return CopyAttackCheck(
                attack_check,
                baseHitRatePercent: baseHitRatePercent,
                previewText: format_attack_check_preview(attack_check)
            );
        }

        bool isDisadvantage = battle_state.is_attack_disadvantage(active_unit, target_unit);
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(active_unit);
        int faithLuckBonus = _get_faith_luck_bonus(active_unit);
        int effectiveLuck = Math.Clamp(
            hiddenLuckAtBirth + faithLuckBonus,
            UnitBaseAttributes.EFFECTIVE_LUCK_MIN(),
            UnitBaseAttributes.EFFECTIVE_LUCK_MAX()
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
            isDisadvantage
        );
        AttackCheckInput resolvedCheck = CopyAttackCheck(
            attack_check,
            hitRatePercent: successRatePercent,
            successRatePercent: successRatePercent,
            baseHitRatePercent: baseHitRatePercent,
            isDisadvantage: isDisadvantage,
            critGateDie: critGateDie,
            critThreshold: critThreshold,
            fumbleLowEnd: fumbleLowEnd,
            critLocked: critLocked
        );
        return CopyAttackCheck(
            resolvedCheck,
            previewText: _format_fate_aware_attack_check_preview(resolvedCheck)
        );
    }

    public string _format_fate_aware_attack_check_preview(AttackCheckInput attack_check)
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

    public int _compute_fate_attack_success_rate_percent(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage
    )
    {
        double basisPoints = _compute_fate_attack_success_rate_basis_points(
            attack_check,
            crit_locked,
            crit_gate_die,
            crit_threshold,
            fumble_low_end,
            is_disadvantage
        );
        return Math.Clamp((int)Math.Round(basisPoints / 100.0), 0, 100);
    }

    public double _compute_fate_attack_success_rate_basis_points(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage
    )
    {
        double d20SuccessBasisPoints = _compute_d20_attack_success_rate_basis_points(
            attack_check,
            crit_locked,
            crit_gate_die,
            crit_threshold,
            fumble_low_end,
            is_disadvantage
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
        return gateCritBasisPoints
            + (10000.0 - gateCritBasisPoints) * d20SuccessBasisPoints / 10000.0;
    }

    public double _compute_d20_attack_success_rate_basis_points(
        AttackCheckInput attack_check,
        bool crit_locked,
        int crit_gate_die,
        int crit_threshold,
        int fumble_low_end,
        bool is_disadvantage
    )
    {
        int successOutcomes = 0;
        int totalOutcomes = NATURAL_HIT_ROLL;
        if (!is_disadvantage)
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
                int roll = Math.Min(firstRoll, secondRoll);
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

    public bool _is_d20_attack_success_roll(
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

    public int _get_hidden_luck_at_birth(BattleUnitState unit_state)
    {
        AttributeSnapshot snapshot = unit_state?.attribute_snapshot;
        if (snapshot == null)
        {
            return 0;
        }
        return snapshot.get_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH());
    }

    public int _get_faith_luck_bonus(BattleUnitState unit_state)
    {
        AttributeSnapshot snapshot = unit_state?.attribute_snapshot;
        if (snapshot == null)
        {
            return 0;
        }
        return snapshot.get_value(UnitBaseAttributes.FAITH_LUCK_BONUS());
    }

    public int _get_effective_luck(BattleUnitState unit_state)
    {
        return Math.Clamp(
            _get_hidden_luck_at_birth(unit_state) + _get_faith_luck_bonus(unit_state),
            UnitBaseAttributes.EFFECTIVE_LUCK_MIN(),
            UnitBaseAttributes.EFFECTIVE_LUCK_MAX()
        );
    }

    public double _average_ints(GIntArray values)
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

    public int _compute_hit_rate_percent(int required_roll)
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

    public bool _is_attack_roll_success(int roll, int required_roll)
    {
        return _is_attack_roll_disposition_success(
            _resolve_attack_roll_disposition(roll, required_roll)
        );
    }

    public int _compute_attack_check_success_rate_percent(AttackCheckInput attack_check)
    {
        int successCount = 0;
        for (int roll = NATURAL_MISS_ROLL; roll <= NATURAL_HIT_ROLL; roll++)
        {
            if (
                _is_attack_roll_disposition_success(
                    _resolve_attack_roll_disposition_for_check(roll, attack_check)
                )
            )
            {
                successCount += 1;
            }
        }
        return successCount * 5;
    }

    public StringName _resolve_attack_roll_disposition_for_check(
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

    public StringName _resolve_attack_roll_disposition(int roll, int required_roll)
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

    public bool _is_attack_roll_disposition_success(StringName roll_disposition)
    {
        return roll_disposition == ROLL_DISPOSITION_THRESHOLD_HIT
            || roll_disposition == ROLL_DISPOSITION_NATURAL_AUTO_HIT;
    }

    public int _get_display_required_roll(int required_roll)
    {
        return Math.Clamp(required_roll, NATURAL_MISS_ROLL + 1, NATURAL_HIT_ROLL);
    }

    public string _format_required_roll_text(int required_roll)
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

    public int _get_required_roll_for_hit_rate(int hit_rate_percent)
    {
        int clampedHitRate = Math.Clamp(hit_rate_percent, 0, 100);
        int successfulRolls = (int)Math.Ceiling(clampedHitRate / 5.0);
        return ATTACK_CHECK_TARGET - successfulRolls;
    }

    private static GDictionary GetDict(GDictionary source, object key)
    {
        return TryGetValue(source, key, out dynamic value)
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        return TryGetValue(source, key, out dynamic value) ? value.AsGodotArray() : new GArray();
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out dynamic value))
        {
            return fallback;
        }
        return ToInt(value, fallback);
    }

    private static int ToInt(object rawValue, int fallback = 0)
    {
        try
        {
            dynamic value = rawValue;
            return value.AsInt32();
        }
        catch
        {
            return rawValue switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                float floatValue => (int)floatValue,
                double doubleValue => (int)doubleValue,
                bool boolValue => boolValue ? 1 : 0,
                StringName stringNameValue
                    => int.TryParse(stringNameValue.ToString(), out int parsed)
                        ? parsed
                        : fallback,
                string stringValue => int.TryParse(stringValue, out int parsed) ? parsed : fallback,
                _ => fallback,
            };
        }
    }

    private static bool TryGetValue(GDictionary source, object key, out dynamic value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        try
        {
            dynamic dynamicKey = key;
            if (source.ContainsKey(dynamicKey))
            {
                value = source[dynamicKey];
                return true;
            }
        }
        catch
        {
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
        int? critGateDie = null,
        int? critThreshold = null,
        int? fumbleLowEnd = null,
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
            forceHitNoCrit: source.ForceHitNoCrit,
            skillId: source.SkillId,
            followUpAttackPenalty: source.FollowUpAttackPenalty,
            exponentialPenalty: source.ExponentialPenalty,
            isDisadvantage: isDisadvantage ?? source.IsDisadvantage,
            invalid: source.Invalid,
            errorId: source.ErrorId,
            errorMessage: source.ErrorMessage,
            previewText: previewText ?? source.PreviewText
        );
    }

    private static string _get_attack_roll_nonce_text(BattleState battle_state) =>
        battle_state != null ? battle_state.attack_roll_nonce.ToString() : "";
}
