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

    private readonly BattleFateAttackRules _fate_attack_rules = new();
    private readonly TraitTriggerHooks _trait_trigger_hooks = new();

    public GDictionary resolve_repeat_attack_stage_hit(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        GDictionary attackCheck = build_repeat_attack_stage_hit_check(
            active_unit,
            target_unit,
            skill_def,
            repeat_attack_effect,
            stage_index
        );
        return roll_attack_check(battle_state, attackCheck);
    }

    public GDictionary build_repeat_attack_stage_hit_check(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        int stagePenalty = 0;
        int baseAttackBonus = 0;
        GDictionary effectParams =
            repeat_attack_effect != null
                ? GdInterop.GetDictionary(repeat_attack_effect, "params")
                : null;
        if (repeat_attack_effect != null && effectParams != null)
        {
            baseAttackBonus = GdInterop.GetInt(effectParams, "base_attack_bonus", 0);
            int penaltyValue = GdInterop.GetInt(effectParams, "follow_up_attack_penalty", 0);

            int penaltyFreeStages = 0;
            GDictionary levelStagesMap = GdInterop.GetDictionary(
                effectParams,
                "penalty_free_stages_by_level"
            );
            if (levelStagesMap.Count != 0 && active_unit != null && skill_def != null)
            {
                int skillLevel = GdInterop.GetInt(
                    GdInterop.GetDictionary(active_unit, "known_skill_level_map"),
                    GdInterop.GetStringName(skill_def, "skill_id"),
                    0
                );
                int bestLevel = -1;
                foreach (var levelKey in levelStagesMap.Keys)
                {
                    int levelVal = (int)levelKey.AsDouble();
                    if (levelVal <= skillLevel && levelVal > bestLevel)
                    {
                        bestLevel = levelVal;
                        penaltyFreeStages = GdInterop.GetInt(levelStagesMap, levelKey, 0);
                    }
                }
            }

            if (stage_index < penaltyFreeStages)
            {
                stagePenalty = 0;
            }
            else if (GdInterop.GetBool(effectParams, "exponential_penalty", false))
            {
                stagePenalty = (int)Math.Pow(2, stage_index) * penaltyValue;
            }
            else
            {
                stagePenalty = Math.Max(stage_index, 0) * penaltyValue;
            }
        }
        return build_skill_attack_check(
            active_unit,
            target_unit,
            skill_def,
            baseAttackBonus,
            stagePenalty
        );
    }

    public GDictionary build_fate_aware_repeat_attack_stage_hit_check(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect,
        int stage_index
    )
    {
        GDictionary baseAttackCheck = build_repeat_attack_stage_hit_check(
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

    public GDictionary build_repeat_attack_preview(
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
            return new GDictionary();
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
        var stageChecks = new GDictArray();
        var stageHitRates = new GIntArray();
        var stageSuccessRates = new GIntArray();
        var stageBaseHitRates = new GIntArray();
        var stageRequiredRolls = new GIntArray();
        var stagePreviewTexts = new Godot.Collections.Array<string>();
        for (int stageIndex = 0; stageIndex < normalizedStageCount; stageIndex++)
        {
            GDictionary attackCheck = build_fate_aware_repeat_attack_stage_hit_check(
                battle_state,
                active_unit,
                target_unit,
                skill_def,
                repeat_attack_effect,
                stageIndex
            );
            int stageSuccessRate = GdInterop.GetInt(attackCheck, "success_rate_percent", 0);
            stageChecks.Add((GDictionary)attackCheck.Duplicate(true));
            stageHitRates.Add(stageSuccessRate);
            stageSuccessRates.Add(stageSuccessRate);
            stageBaseHitRates.Add(GdInterop.GetInt(attackCheck, "base_hit_rate_percent", 0));
            stageRequiredRolls.Add(GdInterop.GetInt(attackCheck, "display_required_roll", 20));
            stagePreviewTexts.Add(GdInterop.GetString(attackCheck, "preview_text", ""));
        }
        GDictionary effectParams = GdInterop.GetDictionary(repeat_attack_effect, "params");
        return new GDictionary
        {
            ["summary_text"] = _format_repeat_attack_preview_summary(stageChecks),
            ["stage_checks"] = stageChecks,
            ["stage_hit_rates"] = stageHitRates,
            ["stage_success_rates"] = stageSuccessRates,
            ["stage_base_hit_rates"] = stageBaseHitRates,
            ["stage_required_rolls"] = stageRequiredRolls,
            ["stage_preview_texts"] = stagePreviewTexts,
            ["hit_rate_percent"] = Mathf.RoundToInt((float)_average_ints(stageSuccessRates)),
            ["success_rate_percent"] = Mathf.RoundToInt((float)_average_ints(stageSuccessRates)),
            ["base_hit_rate_percent"] = Mathf.RoundToInt((float)_average_ints(stageBaseHitRates)),
            ["base_attack_bonus"] =
                effectParams != null ? GdInterop.GetInt(effectParams, "base_attack_bonus", 0) : 0,
            ["follow_up_attack_penalty"] =
                effectParams != null
                    ? GdInterop.GetInt(effectParams, "follow_up_attack_penalty", 0)
                    : 0,
        };
    }

    public GDictionary build_skill_attack_preview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def,
        bool force_hit_no_crit = false
    )
    {
        if (active_unit == null || target_unit == null || skill_def == null)
        {
            return new GDictionary();
        }
        if (force_hit_no_crit)
        {
            return build_force_hit_no_crit_attack_preview();
        }
        GDictionary attackCheck = _build_fate_aware_attack_check_preview(
            battle_state,
            active_unit,
            target_unit,
            build_skill_attack_check(active_unit, target_unit, skill_def)
        );
        int successRate = GdInterop.GetInt(attackCheck, "success_rate_percent", 0);
        int baseHitRate = GdInterop.GetInt(attackCheck, "base_hit_rate_percent", successRate);
        string previewText = GdInterop.GetString(attackCheck, "preview_text", "");
        return new GDictionary
        {
            ["summary_text"] = $"预计命中率 {previewText}",
            ["stage_checks"] = new GDictArray { (GDictionary)attackCheck.Duplicate(true) },
            ["stage_hit_rates"] = new GIntArray { successRate },
            ["stage_success_rates"] = new GIntArray { successRate },
            ["stage_base_hit_rates"] = new GIntArray { baseHitRate },
            ["stage_required_rolls"] = new GIntArray
            {
                GdInterop.GetInt(attackCheck, "display_required_roll", NATURAL_HIT_ROLL),
            },
            ["stage_preview_texts"] = new Godot.Collections.Array<string> { previewText },
            ["hit_rate_percent"] = successRate,
            ["success_rate_percent"] = successRate,
            ["base_hit_rate_percent"] = baseHitRate,
        };
    }

    public GDictionary build_force_hit_no_crit_attack_preview()
    {
        string previewText = "100%（必定命中；禁暴击）";
        var attackCheck = new GDictionary
        {
            ["required_roll"] = NATURAL_MISS_ROLL,
            ["display_required_roll"] = NATURAL_MISS_ROLL,
            ["hit_rate_percent"] = 100,
            ["success_rate_percent"] = 100,
            ["base_hit_rate_percent"] = 100,
            ["force_hit_no_crit"] = true,
            ["crit_locked"] = true,
            ["natural_one_auto_miss"] = false,
            ["natural_twenty_auto_hit"] = false,
            ["preview_text"] = previewText,
        };
        return new GDictionary
        {
            ["summary_text"] = $"预计命中率 {previewText}",
            ["stage_checks"] = new GDictArray { (GDictionary)attackCheck.Duplicate(true) },
            ["stage_hit_rates"] = new GIntArray { 100 },
            ["stage_success_rates"] = new GIntArray { 100 },
            ["stage_base_hit_rates"] = new GIntArray { 100 },
            ["stage_required_rolls"] = new GIntArray { NATURAL_MISS_ROLL },
            ["stage_preview_texts"] = new Godot.Collections.Array<string> { previewText },
            ["hit_rate_percent"] = 100,
            ["success_rate_percent"] = 100,
            ["base_hit_rate_percent"] = 100,
            ["force_hit_no_crit"] = true,
            ["crit_locked"] = true,
        };
    }

    public GDictionary build_skill_attack_check(
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        SkillDef skill_def
    )
    {
        return build_skill_attack_check(active_unit, target_unit, skill_def, 0, 0);
    }

    public GDictionary build_skill_attack_check(
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
            GDictionary knownSkillLevelMap = GdInterop.GetDictionary(
                active_unit,
                "known_skill_level_map"
            );
            StringName skillId = GdInterop.GetStringName(skill_def, "skill_id");
            if (knownSkillLevelMap.ContainsKey(skillId))
            {
                skillLevel = GdInterop.GetInt(knownSkillLevelMap, skillId, 0);
            }
            else if (GdInterop.GetArray(active_unit, "known_active_skill_ids").Contains(skillId))
            {
                skillLevel = 1;
            }
        }
        GodotObject combatProfile =
            skill_def != null ? GdInterop.GetObject(skill_def, "combat_profile") : null;
        int skillAttackBonus =
            combatProfile != null
                ? combatProfile.Call("get_effective_attack_roll_bonus", skillLevel).AsInt32()
                : 0;
        int lockedSkillHitBonus = _get_skill_lock_hit_bonus(
            active_unit,
            skill_def != null ? GdInterop.GetStringName(skill_def, "skill_id") : new StringName("")
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
        var attackCheck = new GDictionary
        {
            ["attacker_base_attack_bonus"] = attackerBaseAttackBonus,
            ["attacker_attack_bonus"] = attackerAttackBonus,
            ["attacker_bab"] = attackerBaseAttackBonus,
            ["target_armor_class"] = targetArmorClass,
            ["skill_attack_bonus"] = skillAttackBonus,
            ["locked_skill_hit_bonus"] = lockedSkillHitBonus,
            ["situational_attack_bonus"] = situationalAttackBonus,
            ["situational_attack_penalty"] = situationalAttackPenalty,
            ["required_roll"] = requiredRoll,
            ["display_required_roll"] = _get_display_required_roll(requiredRoll),
            ["hit_rate_percent"] = hitRatePercent,
            ["success_rate_percent"] = hitRatePercent,
            ["natural_one_auto_miss"] = true,
            ["natural_twenty_auto_hit"] = true,
        };
        attackCheck["preview_text"] = format_attack_check_preview(attackCheck);
        return attackCheck;
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

    public GDictionary _build_invalid_attack_check(StringName error_id, string error_message)
    {
        return new GDictionary
        {
            ["invalid"] = true,
            ["error_id"] = error_id.ToString(),
            ["error_message"] = error_message,
            ["attacker_base_attack_bonus"] = 0,
            ["attacker_attack_bonus"] = 0,
            ["attacker_bab"] = 0,
            ["target_armor_class"] = 0,
            ["skill_attack_bonus"] = 0,
            ["locked_skill_hit_bonus"] = 0,
            ["situational_attack_bonus"] = 0,
            ["situational_attack_penalty"] = 0,
            ["required_roll"] = ATTACK_CHECK_TARGET,
            ["display_required_roll"] = _get_display_required_roll(ATTACK_CHECK_TARGET),
            ["hit_rate_percent"] = 0,
            ["success_rate_percent"] = 0,
            ["base_hit_rate_percent"] = 0,
            ["natural_one_auto_miss"] = true,
            ["natural_twenty_auto_hit"] = false,
            ["preview_text"] = $"无效命中检定：{error_message}",
        };
    }

    public int _get_target_armor_break_penalty(BattleUnitState target_unit)
    {
        if (target_unit == null)
        {
            return 0;
        }
        var statusEntry =
            target_unit.Call("get_status_effect", STATUS_ARMOR_BREAK).AsGodotObject()
            as BattleStatusEffectState;
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
        var statusEntry =
            target_unit.Call("get_status_effect", STATUS_DODGE_BONUS_UP).AsGodotObject()
            as BattleStatusEffectState;
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
        if (active_unit.Call("has_status_effect", STATUS_BLACK_STAR_BRAND_ELITE).AsBool())
        {
            attackDelta = BLACK_STAR_BRAND_ATTACK_BONUS_DELTA;
        }
        else
        {
            var statusEntry =
                active_unit.Call("get_status_effect", STATUS_ATTACK_ROLL_BONUS_UP).AsGodotObject()
                as BattleStatusEffectState;
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
        foreach (
            Variant statusIdValue in GdInterop.GetDictionary(active_unit, "status_effects").Keys
        )
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            var statusEntry =
                active_unit.Call("get_status_effect", statusId).AsGodotObject()
                as BattleStatusEffectState;
            if (statusEntry == null)
            {
                continue;
            }
            penalty = Math.Max(
                penalty,
                BattleStatusSemanticTable.get_attack_roll_penalty(statusEntry)
            );
        }
        return penalty;
    }

    public bool _is_target_dodge_bonus_locked(BattleUnitState target_unit)
    {
        return target_unit != null
            && (
                target_unit.Call("has_status_effect", STATUS_CROWN_BREAK_BLINDED_EYE).AsBool()
                || _unit_has_status_bool_param(target_unit, "lock_dodge_bonus")
            );
    }

    public bool _unit_has_status_bool_param(BattleUnitState unit_state, StringName param_key)
    {
        if (unit_state == null || GdInterop.IsEmpty(param_key))
        {
            return false;
        }
        foreach (
            Variant statusIdValue in GdInterop.GetDictionary(unit_state, "status_effects").Keys
        )
        {
            StringName statusId = ProgressionDataUtils.to_string_name(statusIdValue);
            var statusEntry =
                unit_state.Call("get_status_effect", statusId).AsGodotObject()
                as BattleStatusEffectState;
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
        if (@params == null || GdInterop.IsEmpty(param_key))
        {
            return fallback;
        }
        return GdInterop.TryGet(@params, param_key, out var value) ? value.AsBool() : fallback;
    }

    public GDictionary roll_attack_check(BattleState battle_state, GDictionary attack_check)
    {
        if (attack_check.Count == 0)
        {
            StringName emptyDisposition = _resolve_attack_roll_disposition(
                NATURAL_MISS_ROLL,
                ATTACK_CHECK_TARGET
            );
            return new GDictionary
            {
                ["success"] = false,
                ["roll"] = NATURAL_MISS_ROLL,
                ["required_roll"] = ATTACK_CHECK_TARGET,
                ["display_required_roll"] = _get_display_required_roll(ATTACK_CHECK_TARGET),
                ["hit_rate_percent"] = 0,
                ["success_rate_percent"] = 0,
                ["natural_one_auto_miss"] = true,
                ["natural_twenty_auto_hit"] = true,
                ["roll_disposition"] = emptyDisposition,
                ["preview_text"] = format_attack_check_preview(new GDictionary()),
                ["resolution_text"] = format_attack_check_resolution(
                    new GDictionary
                    {
                        ["roll"] = NATURAL_MISS_ROLL,
                        ["required_roll"] = ATTACK_CHECK_TARGET,
                        ["roll_disposition"] = emptyDisposition,
                    }
                ),
            };
        }
        if (GdInterop.GetBool(attack_check, "invalid", false))
        {
            GDictionary invalidResult = (GDictionary)attack_check.Duplicate(true);
            invalidResult["success"] = false;
            invalidResult["roll"] = 0;
            invalidResult["roll_disposition"] = ROLL_DISPOSITION_THRESHOLD_MISS;
            invalidResult["resolution_text"] = GdInterop.GetString(
                attack_check,
                "preview_text",
                "无效命中检定"
            );
            return invalidResult;
        }
        int roll = _roll_battle_d20(battle_state);
        StringName rollDisposition = _resolve_attack_roll_disposition_for_check(roll, attack_check);
        GDictionary result = (GDictionary)attack_check.Duplicate(true);
        result["roll"] = roll;
        result["roll_disposition"] = rollDisposition;
        result["success"] = _is_attack_roll_disposition_success(rollDisposition);
        result["resolution_text"] = format_attack_check_resolution(result);
        return result;
    }

    public GDictionary roll_hit_rate(BattleState battle_state, int hit_rate_percent)
    {
        int clampedHitRate = Math.Clamp(hit_rate_percent, 0, 100);
        int syntheticRequiredRoll = _get_required_roll_for_hit_rate(clampedHitRate);
        var syntheticAttackCheck = new GDictionary
        {
            ["required_roll"] = syntheticRequiredRoll,
            ["display_required_roll"] = _get_display_required_roll(syntheticRequiredRoll),
            ["natural_one_auto_miss"] = clampedHitRate < 100,
            ["natural_twenty_auto_hit"] = clampedHitRate > 0,
        };
        int resolvedHitRate = _compute_attack_check_success_rate_percent(syntheticAttackCheck);
        syntheticAttackCheck["hit_rate_percent"] = resolvedHitRate;
        syntheticAttackCheck["success_rate_percent"] = resolvedHitRate;
        syntheticAttackCheck["preview_text"] = format_attack_check_preview(syntheticAttackCheck);
        return roll_attack_check(battle_state, syntheticAttackCheck);
    }

    public GDictionary resolve_attack_metadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary attack_check,
        GDictionary attack_context
    )
    {
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(source_unit);
        int faithLuckBonus = _get_faith_luck_bonus(source_unit);
        int effectiveLuck = _get_effective_luck(source_unit);
        bool isDisadvantage = _resolve_attack_disadvantage(
            source_unit,
            target_unit,
            attack_context
        );
        int critGateDie = FateAttackFormula.calc_crit_gate_die_size(effectiveLuck, isDisadvantage);
        bool forceHitNoCrit = GdInterop.GetBool(attack_context, "force_hit_no_crit", false);
        bool critLocked = _fate_attack_rules.is_attack_crit_locked(source_unit) || forceHitNoCrit;
        int requiredRoll = GdInterop.GetInt(attack_check, "required_roll", ATTACK_CHECK_TARGET);
        var metadata = new GDictionary
        {
            ["attack_resolution"] = ATTACK_RESOLUTION_MISS,
            ["attack_success"] = false,
            ["critical_hit"] = false,
            ["critical_fail"] = false,
            ["ordinary_miss"] = false,
            ["is_disadvantage"] = isDisadvantage,
            ["hidden_luck_at_birth"] = hiddenLuckAtBirth,
            ["faith_luck_bonus"] = faithLuckBonus,
            ["effective_luck"] = effectiveLuck,
            ["crit_locked"] = critLocked,
            ["crit_gate_die"] = critGateDie,
            ["crit_gate_roll"] = 0,
            ["hit_roll"] = 0,
            ["fumble_low_end"] = FateAttackFormula.calc_fumble_low_end(effectiveLuck),
            ["crit_threshold"] = FateAttackFormula.calc_crit_threshold(
                hiddenLuckAtBirth,
                faithLuckBonus
            ),
            ["required_roll"] = requiredRoll,
            ["display_required_roll"] = GdInterop.GetInt(
                attack_check,
                "display_required_roll",
                Math.Clamp(requiredRoll, 2, NATURAL_HIT_ROLL)
            ),
            ["hit_rate_percent"] = GdInterop.GetInt(attack_check, "hit_rate_percent", 0),
            ["trait_trigger_results"] = new GArray(),
        };
        if (forceHitNoCrit)
        {
            metadata["attack_resolution"] = ATTACK_RESOLUTION_HIT;
            metadata["attack_success"] = true;
            return metadata;
        }

        if (critGateDie > NATURAL_HIT_ROLL)
        {
            int critGateRoll = _roll_attack_die(critGateDie, isDisadvantage, attack_context);
            metadata["crit_gate_roll"] = critGateRoll;
            if (_fate_attack_rules.does_gate_die_crit(critGateRoll, critGateDie, critLocked))
            {
                metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT;
                metadata["attack_success"] = true;
                metadata["critical_hit"] = true;
                return metadata;
            }
        }

        int hitRoll = _roll_attack_die(NATURAL_HIT_ROLL, isDisadvantage, attack_context);
        metadata["hit_roll"] = hitRoll;
        GDictionary naturalOneTraitResult = _resolve_natural_one_trait_reroll(
            source_unit,
            hitRoll,
            attack_context
        );
        if (GdInterop.GetBool(naturalOneTraitResult, "triggered", false))
        {
            hitRoll = GdInterop.GetInt(naturalOneTraitResult, "rerolled_roll", hitRoll);
            metadata["hit_roll"] = hitRoll;
            _append_trait_trigger_result(metadata, naturalOneTraitResult);
        }

        if (hitRoll <= GdInterop.GetInt(metadata, "fumble_low_end", 1))
        {
            if (_try_apply_reverse_fate_amulet(source_unit))
            {
                metadata["attack_resolution"] = ATTACK_RESOLUTION_MISS;
                metadata["ordinary_miss"] = true;
                metadata["reverse_fate_downgraded"] = true;
                return metadata;
            }
            metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_FAIL;
            metadata["critical_fail"] = true;
            return metadata;
        }

        if (
            _fate_attack_rules.is_high_threat_crit_roll(
                hitRoll,
                critLocked,
                critGateDie,
                GdInterop.GetInt(metadata, "crit_threshold", NATURAL_HIT_ROLL)
            )
        )
        {
            metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT;
            metadata["attack_success"] = true;
            metadata["critical_hit"] = true;
            return metadata;
        }

        if (_fate_attack_rules.does_attack_roll_hit(hitRoll, attack_check))
        {
            metadata["attack_resolution"] = ATTACK_RESOLUTION_HIT;
            metadata["attack_success"] = true;
            return metadata;
        }

        metadata["ordinary_miss"] = true;
        return metadata;
    }

    public GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        GDictionary attack_context
    )
    {
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(source_unit);
        int faithLuckBonus = _get_faith_luck_bonus(source_unit);
        int effectiveLuck = _get_effective_luck(source_unit);
        bool isDisadvantage = GdInterop.GetBool(attack_context, "is_disadvantage", false);
        int lockedSkillHitBonus = _get_skill_lock_hit_bonus_from_context(
            source_unit,
            attack_context
        );
        int critGateDie = FateAttackFormula.calc_crit_gate_die_size(effectiveLuck, isDisadvantage);
        bool critLocked = _fate_attack_rules.is_attack_crit_locked(source_unit);
        var metadata = new GDictionary
        {
            ["attack_resolution"] = ATTACK_RESOLUTION_HIT,
            ["spell_control_resolution"] = new StringName("normal"),
            ["attack_success"] = true,
            ["critical_hit"] = false,
            ["critical_fail"] = false,
            ["ordinary_miss"] = false,
            ["is_disadvantage"] = isDisadvantage,
            ["hidden_luck_at_birth"] = hiddenLuckAtBirth,
            ["faith_luck_bonus"] = faithLuckBonus,
            ["effective_luck"] = effectiveLuck,
            ["crit_locked"] = critLocked,
            ["crit_gate_die"] = critGateDie,
            ["crit_gate_roll"] = 0,
            ["hit_roll"] = 0,
            ["fumble_low_end"] = FateAttackFormula.calc_fumble_low_end(effectiveLuck),
            ["crit_threshold"] = FateAttackFormula.calc_crit_threshold(
                hiddenLuckAtBirth,
                faithLuckBonus
            ),
            ["locked_skill_hit_bonus"] = lockedSkillHitBonus,
            ["trait_trigger_results"] = new GArray(),
        };

        if (critGateDie > NATURAL_HIT_ROLL)
        {
            int critGateRoll = _roll_attack_die(critGateDie, isDisadvantage, attack_context);
            metadata["crit_gate_roll"] = critGateRoll;
            if (_fate_attack_rules.does_gate_die_crit(critGateRoll, critGateDie, critLocked))
            {
                metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT;
                metadata["spell_control_resolution"] = new StringName("critical_success");
                metadata["critical_hit"] = true;
                return metadata;
            }
        }

        int hitRoll = _roll_attack_die(NATURAL_HIT_ROLL, isDisadvantage, attack_context);
        metadata["hit_roll"] = hitRoll;
        GDictionary naturalOneTraitResult = _resolve_natural_one_trait_reroll(
            source_unit,
            hitRoll,
            attack_context
        );
        if (GdInterop.GetBool(naturalOneTraitResult, "triggered", false))
        {
            hitRoll = GdInterop.GetInt(naturalOneTraitResult, "rerolled_roll", hitRoll);
            metadata["hit_roll"] = hitRoll;
            _append_trait_trigger_result(metadata, naturalOneTraitResult);
        }

        int effectiveHitRoll = hitRoll + lockedSkillHitBonus;
        metadata["effective_hit_roll"] = effectiveHitRoll;
        if (effectiveHitRoll <= GdInterop.GetInt(metadata, "fumble_low_end", 1))
        {
            if (_try_apply_reverse_fate_amulet(source_unit))
            {
                metadata["spell_control_resolution"] = new StringName("reverse_fate_downgraded");
                metadata["reverse_fate_downgraded"] = true;
                return metadata;
            }
            metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_FAIL;
            metadata["spell_control_resolution"] = new StringName("critical_fail");
            metadata["attack_success"] = false;
            metadata["critical_fail"] = true;
            return metadata;
        }

        if (
            _fate_attack_rules.is_high_threat_crit_roll(
                effectiveHitRoll,
                critLocked,
                critGateDie,
                GdInterop.GetInt(metadata, "crit_threshold", NATURAL_HIT_ROLL)
            )
        )
        {
            metadata["attack_resolution"] = ATTACK_RESOLUTION_CRITICAL_HIT;
            metadata["spell_control_resolution"] = new StringName("critical_success");
            metadata["critical_hit"] = true;
            return metadata;
        }

        return metadata;
    }

    public int _get_skill_lock_hit_bonus(BattleUnitState unit_state, StringName skill_id)
    {
        if (unit_state == null || GdInterop.IsEmpty(skill_id))
        {
            return 0;
        }
        return Math.Max(
            GdInterop.GetInt(
                GdInterop.GetDictionary(unit_state, "known_skill_lock_hit_bonus_map"),
                skill_id,
                0
            ),
            0
        );
    }

    public int _get_skill_lock_hit_bonus_from_context(
        BattleUnitState unit_state,
        GDictionary context
    )
    {
        if (context == null)
        {
            return 0;
        }
        return _get_skill_lock_hit_bonus(
            unit_state,
            ProgressionDataUtils.to_string_name(context.GetValueOrDefault("skill_id", ""))
        );
    }

    public int roll_attack_die(int die_size, bool is_disadvantage, GDictionary attack_context)
    {
        return _roll_attack_die(die_size, is_disadvantage, attack_context);
    }

    public string format_attack_check_preview(GDictionary attack_check)
    {
        int hitRatePercent = GdInterop.GetInt(attack_check, "success_rate_percent", 0);
        int requiredRoll = GdInterop.GetInt(attack_check, "required_roll", ATTACK_CHECK_TARGET);
        return $"{hitRatePercent}%（{_format_required_roll_text(requiredRoll)}）";
    }

    public string format_attack_check_resolution(GDictionary attack_result)
    {
        string previewText = GdInterop.GetString(
            attack_result,
            "preview_text",
            format_attack_check_preview(attack_result)
        );
        int roll = GdInterop.GetInt(attack_result, "roll", NATURAL_MISS_ROLL);
        StringName rollDisposition = GdInterop.GetStringName(
            attack_result,
            "roll_disposition",
            _resolve_attack_roll_disposition(
                roll,
                GdInterop.GetInt(attack_result, "required_roll", ATTACK_CHECK_TARGET)
            )
        );
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
        int nonce = Math.Max(GdInterop.GetInt(battle_state, "attack_roll_nonce", 0), 0);
        battle_state.Set("attack_roll_nonce", nonce + 1);
        return TrueRandomSeedService.randi_range(NATURAL_MISS_ROLL, NATURAL_HIT_ROLL);
    }

    public GDictionary _resolve_natural_one_trait_reroll(
        BattleUnitState source_unit,
        int hit_roll,
        GDictionary attack_context
    )
    {
        if (_trait_trigger_hooks == null)
        {
            return new GDictionary();
        }
        GDictionary hookResult = _trait_trigger_hooks.on_natural_one(
            source_unit,
            new GDictionary
            {
                ["roll"] = hit_roll,
                ["die_size"] = NATURAL_HIT_ROLL,
                ["battle_state"] = attack_context.GetValueOrDefault("battle_state", default),
            }
        );
        if (!GdInterop.GetBool(hookResult, "triggered", false))
        {
            return hookResult;
        }
        if (GdInterop.GetBool(hookResult, "reroll_die", false))
        {
            hookResult["rerolled_roll"] = _roll_attack_die(NATURAL_HIT_ROLL, false, attack_context);
        }
        return hookResult;
    }

    public bool _resolve_attack_disadvantage(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary attack_context
    )
    {
        if (attack_context.ContainsKey("is_disadvantage"))
        {
            return GdInterop.GetBool(attack_context, "is_disadvantage", false);
        }
        var battleState = GdInterop.GetObject(attack_context, "battle_state") as BattleState;
        if (battleState == null)
        {
            return false;
        }
        return battleState.Call("is_attack_disadvantage", source_unit, target_unit).AsBool();
    }

    public int _roll_attack_die(int die_size, bool is_disadvantage, GDictionary attack_context)
    {
        var battleState = GdInterop.GetObject(attack_context, "battle_state") as BattleState;
        int normalizedDieSize = Math.Max(die_size, 1);
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
        GDictionary attack_context,
        BattleState battle_state
    )
    {
        int overrideRoll = _consume_attack_roll_override(attack_context, die_size);
        if (overrideRoll > 0)
        {
            if (battle_state != null)
            {
                battle_state.Set(
                    "attack_roll_nonce",
                    Math.Max(GdInterop.GetInt(battle_state, "attack_roll_nonce", 0), 0) + 1
                );
            }
            return overrideRoll;
        }
        return _roll_true_random_attack_range(1, die_size, battle_state);
    }

    public int _consume_attack_roll_override(GDictionary attack_context, int die_size)
    {
        if (attack_context == null)
        {
            return 0;
        }
        int normalizedDieSize = Math.Max(die_size, 1);
        if (attack_context.ContainsKey("attack_roll_overrides"))
        {
            GArray overrideValues = GdInterop.GetArray(attack_context, "attack_roll_overrides");
            if (overrideValues.Count != 0)
            {
                var rawValue = overrideValues[0];
                overrideValues.RemoveAt(0);
                attack_context["attack_roll_overrides"] = overrideValues;
                return Math.Clamp(rawValue.AsInt32(), 1, normalizedDieSize);
            }
        }
        if (attack_context.ContainsKey("attack_roll_override"))
        {
            var rawSingle = attack_context["attack_roll_override"];
            attack_context.Remove("attack_roll_override");
            return Math.Clamp(rawSingle.AsInt32(), 1, normalizedDieSize);
        }
        return 0;
    }

    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.Set(
                "attack_roll_nonce",
                Math.Max(GdInterop.GetInt(battle_state, "attack_roll_nonce", 0), 0) + 1
            );
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
            !LowLuckRelicRules.unit_has_flag(
                source_unit,
                LowLuckRelicRules.ATTR_REVERSE_FATE_AMULET
            )
        )
        {
            return false;
        }
        GDictionary aiBlackboard = GdInterop.GetDictionary(source_unit, "ai_blackboard");
        if (GdInterop.GetBool(aiBlackboard, LowLuckRelicRules.BATTLE_FLAG_REVERSE_FATE_USED, false))
        {
            return false;
        }
        aiBlackboard[LowLuckRelicRules.BATTLE_FLAG_REVERSE_FATE_USED] = true;
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
        if (unit_state == null || GdInterop.IsEmpty(status_id))
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
        unit_state.Call("set_status_effect", statusEntry);
    }

    public void _append_trait_trigger_result(GDictionary target, GDictionary trigger_result)
    {
        if (
            target == null
            || trigger_result == null
            || !GdInterop.GetBool(trigger_result, "triggered", false)
        )
        {
            return;
        }
        GArray results = (GArray)GdInterop
            .GetArray(target, "trait_trigger_results")
            .Duplicate(true);
        results.Add(trigger_result.Duplicate(true));
        target["trait_trigger_results"] = results;
    }

    public int _resolve_repeat_attack_preview_stage_count(
        BattleUnitState active_unit,
        SkillDef skill_def,
        CombatEffectDef repeat_attack_effect
    )
    {
        GodotObject combatProfile =
            skill_def != null ? GdInterop.GetObject(skill_def, "combat_profile") : null;
        if (
            active_unit == null
            || skill_def == null
            || combatProfile == null
            || repeat_attack_effect == null
        )
        {
            return DEFAULT_REPEAT_ATTACK_PREVIEW_STAGE_COUNT;
        }
        if (active_unit.Call("has_status_effect", STATUS_CROWN_BREAK_BROKEN_HAND).AsBool())
        {
            return 1;
        }

        GDictionary parameters = GdInterop.GetDictionary(repeat_attack_effect, "params");
        StringName costResource = GdInterop.GetStringName(parameters, "cost_resource", "aura");
        int baseCost = _get_repeat_attack_preview_base_cost(skill_def, costResource);
        if (baseCost <= 0)
        {
            return REPEAT_ATTACK_PREVIEW_STAGE_GUARD;
        }

        double followUpCostMultiplier = Math.Max(
            GdInterop.GetFloat(parameters, "follow_up_cost_multiplier", 1.0),
            1.0
        );
        int remainingResource = _get_unit_resource_value(active_unit, costResource);
        if (remainingResource < baseCost)
        {
            return 1;
        }

        remainingResource -= baseCost;
        int stages = 1;
        while (stages < REPEAT_ATTACK_PREVIEW_STAGE_GUARD)
        {
            int nextStageCost = Math.Max(
                (int)Math.Round(baseCost * Math.Pow(followUpCostMultiplier, stages)),
                0
            );
            if (nextStageCost > 0 && remainingResource < nextStageCost)
            {
                break;
            }
            remainingResource -= nextStageCost;
            stages += 1;
        }
        return stages;
    }

    public int _get_repeat_attack_preview_base_cost(SkillDef skill_def, StringName cost_resource)
    {
        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (cost_resource == "mp")
        {
            return GdInterop.GetInt(combatProfile, "mp_cost");
        }
        if (cost_resource == "stamina")
        {
            return GdInterop.GetInt(combatProfile, "stamina_cost");
        }
        if (cost_resource == "ap")
        {
            return GdInterop.GetInt(combatProfile, "ap_cost");
        }
        return GdInterop.GetInt(combatProfile, "aura_cost");
    }

    public int _get_unit_resource_value(BattleUnitState active_unit, StringName cost_resource)
    {
        if (cost_resource == "mp")
        {
            return GdInterop.GetInt(active_unit, "current_mp");
        }
        if (cost_resource == "stamina")
        {
            return GdInterop.GetInt(active_unit, "current_stamina");
        }
        if (cost_resource == "ap")
        {
            return GdInterop.GetInt(active_unit, "current_ap");
        }
        return GdInterop.GetInt(active_unit, "current_aura");
    }

    public string _format_repeat_attack_preview_summary(GDictArray stage_checks)
    {
        if (stage_checks.Count == 0)
        {
            return "";
        }
        var parts = new List<string>();
        foreach (GDictionary stageCheck in stage_checks)
        {
            parts.Add(
                GdInterop.GetString(
                    stageCheck,
                    "preview_text",
                    format_attack_check_preview(stageCheck)
                )
            );
        }
        return $"预计命中率 {string.Join(" -> ", parts)}";
    }

    public GDictionary _build_fate_aware_attack_check_preview(
        BattleState battle_state,
        BattleUnitState active_unit,
        BattleUnitState target_unit,
        GDictionary attack_check
    )
    {
        GDictionary resolvedCheck = (GDictionary)attack_check.Duplicate(true);
        if (GdInterop.GetBool(resolvedCheck, "invalid", false))
        {
            resolvedCheck["hit_rate_percent"] = 0;
            resolvedCheck["success_rate_percent"] = 0;
            resolvedCheck["base_hit_rate_percent"] = 0;
            resolvedCheck["preview_text"] = GdInterop.GetString(
                resolvedCheck,
                "preview_text",
                "无效命中检定"
            );
            return resolvedCheck;
        }
        int baseHitRatePercent = GdInterop.GetInt(attack_check, "hit_rate_percent", 0);
        resolvedCheck["base_hit_rate_percent"] = baseHitRatePercent;
        if (!resolvedCheck.ContainsKey("success_rate_percent"))
        {
            resolvedCheck["success_rate_percent"] = 0;
        }
        if (battle_state == null || active_unit == null || target_unit == null)
        {
            resolvedCheck["preview_text"] = format_attack_check_preview(resolvedCheck);
            return resolvedCheck;
        }

        bool isDisadvantage = battle_state
            .Call("is_attack_disadvantage", active_unit, target_unit)
            .AsBool();
        int hiddenLuckAtBirth = _get_hidden_luck_at_birth(active_unit);
        int faithLuckBonus = _get_faith_luck_bonus(active_unit);
        int effectiveLuck = Math.Clamp(
            hiddenLuckAtBirth + faithLuckBonus,
            UnitBaseAttributes.EFFECTIVE_LUCK_MIN(),
            UnitBaseAttributes.EFFECTIVE_LUCK_MAX()
        );
        int critGateDie = FateAttackFormula.calc_crit_gate_die_size(effectiveLuck, isDisadvantage);
        int critThreshold = FateAttackFormula.calc_crit_threshold(
            hiddenLuckAtBirth,
            faithLuckBonus
        );
        int fumbleLowEnd = FateAttackFormula.calc_fumble_low_end(effectiveLuck);
        bool critLocked = _fate_attack_rules.is_attack_crit_locked(active_unit);
        int successRatePercent = _compute_fate_attack_success_rate_percent(
            attack_check,
            critLocked,
            critGateDie,
            critThreshold,
            fumbleLowEnd,
            isDisadvantage
        );
        resolvedCheck["hit_rate_percent"] = successRatePercent;
        resolvedCheck["success_rate_percent"] = successRatePercent;
        resolvedCheck["is_disadvantage"] = isDisadvantage;
        resolvedCheck["effective_luck"] = effectiveLuck;
        resolvedCheck["crit_gate_die"] = critGateDie;
        resolvedCheck["crit_threshold"] = critThreshold;
        resolvedCheck["fumble_low_end"] = fumbleLowEnd;
        resolvedCheck["crit_locked"] = critLocked;
        resolvedCheck["preview_text"] = _format_fate_aware_attack_check_preview(resolvedCheck);
        return resolvedCheck;
    }

    public string _format_fate_aware_attack_check_preview(GDictionary attack_check)
    {
        int successRatePercent = GdInterop.GetInt(attack_check, "success_rate_percent", 0);
        string requiredRollText = _format_required_roll_text(
            GdInterop.GetInt(attack_check, "required_roll", ATTACK_CHECK_TARGET)
        );
        int baseHitRatePercent = GdInterop.GetInt(
            attack_check,
            "base_hit_rate_percent",
            successRatePercent
        );
        if (successRatePercent <= baseHitRatePercent)
        {
            return $"{successRatePercent}%（{requiredRollText}）";
        }
        bool critLocked = GdInterop.GetBool(attack_check, "crit_locked", false);
        int critGateDie = GdInterop.GetInt(attack_check, "crit_gate_die", NATURAL_HIT_ROLL);
        if (!critLocked && critGateDie == NATURAL_HIT_ROLL)
        {
            return $"{successRatePercent}%（{requiredRollText}；高位大成功 {GdInterop.GetInt(attack_check, "crit_threshold", NATURAL_HIT_ROLL)}-20 直达）";
        }
        if (!critLocked && critGateDie > NATURAL_HIT_ROLL)
        {
            return $"{successRatePercent}%（{requiredRollText}；含门骰 d{critGateDie}）";
        }
        return $"{successRatePercent}%（{requiredRollText}）";
    }

    public int _compute_fate_attack_success_rate_percent(
        GDictionary attack_check,
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
        GDictionary attack_check,
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
        GDictionary attack_check,
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
        GDictionary attack_check,
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
            _fate_attack_rules.is_high_threat_crit_roll(
                roll,
                crit_locked,
                crit_gate_die,
                crit_threshold
            )
        )
        {
            return true;
        }
        return _fate_attack_rules.does_attack_roll_hit(roll, attack_check);
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

    public int _compute_attack_check_success_rate_percent(GDictionary attack_check)
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

    public StringName _resolve_attack_roll_disposition_for_check(int roll, GDictionary attack_check)
    {
        int requiredRoll = GdInterop.GetInt(attack_check, "required_roll", ATTACK_CHECK_TARGET);
        if (
            GdInterop.GetBool(attack_check, "natural_one_auto_miss", true)
            && roll <= NATURAL_MISS_ROLL
        )
        {
            return ROLL_DISPOSITION_NATURAL_AUTO_MISS;
        }
        if (
            GdInterop.GetBool(attack_check, "natural_twenty_auto_hit", true)
            && roll >= NATURAL_HIT_ROLL
        )
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
}
