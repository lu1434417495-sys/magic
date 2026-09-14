using System.Linq;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

internal sealed class SkillDefinitionDamageEffectValidator
{
    private readonly record struct EquipmentDurabilityDamageValidationParameters(
        int MaxDamagedItems,
        bool RequireDamageApplied,
        bool TargetSlotsMissingOrEmpty
    )
    {
        public static EquipmentDurabilityDamageValidationParameters FromEffect(
            CombatEffectDefinition effectDef
        )
        {
            EquipmentDurabilityDamageEffectPayloadDefinition payload =
                effectDef?.Payload as EquipmentDurabilityDamageEffectPayloadDefinition;
            return new EquipmentDurabilityDamageValidationParameters(
                payload?.MaxDamagedItems ?? 0,
                effectDef?.RequireDamageApplied ?? false,
                payload == null || payload.TargetSlots.Count == 0
            );
        }
    }

    internal void AppendDamageEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        var damageTag = effectDef.DamageTag;
        bool usesWeaponDamageTag = effectDef.UseWeaponPhysicalDamageTag;
        if (usesWeaponDamageTag)
        {
            if (damageTag != "")
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} cannot combine damage_tag with use_weapon_physical_damage_tag."
                );
        }
        else
        {
            if (damageTag == "")
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} must declare damage_tag or set use_weapon_physical_damage_tag = true."
                );
            else if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} uses unsupported damage_tag {damageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
        }

        AppendDamageEffectMitigationBypassValidationErrors(
            errors,
            skillId,
            effectDef,
            contextLabel
        );
        AppendExtraDamageSegmentValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendTargetDamageMultiplierRuleValidationErrors(
            errors,
            skillId,
            effectDef,
            contextLabel
        );

        if (effectDef.HpRatioThresholdPercent < 0 || effectDef.HpRatioThresholdPercent > 100)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} hp_ratio_threshold_percent must be 0 or from 1 to 100."
            );

        BattleDamageBonusConditionKind bonusConditionKind = effectDef.BonusConditionKind;
        if (bonusConditionKind == BattleDamageBonusConditionKind.Unknown)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} uses unsupported bonus_condition {effectDef.BonusCondition}."
            );
        }
        if (
            bonusConditionKind == BattleDamageBonusConditionKind.TargetCreatureType
            && effectDef.BonusConditionCreatureTypeTag == ""
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_condition target_creature_type requires bonus_condition_creature_type_tag."
            );
        }
        if (
            effectDef.BonusConditionCreatureTypeTag != ""
            && bonusConditionKind != BattleDamageBonusConditionKind.TargetCreatureType
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_condition_creature_type_tag requires bonus_condition target_creature_type."
            );
        }
        if (effectDef.WeaponDiceMultiplier < 1)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} weapon_dice_multiplier must be >= 1."
            );
        }
        if (effectDef.BonusWeaponDiceMultiplier < 0)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_weapon_dice_multiplier must be >= 0."
            );
        }
        if (
            (effectDef.WeaponDiceMultiplier > 1 || effectDef.BonusWeaponDiceMultiplier > 0)
            && !effectDef.AddWeaponDice
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} weapon dice multipliers require add_weapon_dice."
            );
        }
        if (
            effectDef.BonusWeaponDiceMultiplier > 0
            && effectDef.BonusCondition == ""
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_weapon_dice_multiplier requires bonus_condition."
            );
        }

        bool hasBonusDamageDice =
            effectDef.BonusDamageDiceCount > 0
            || effectDef.BonusDamageDiceSides > 0
            || effectDef.BonusDamageDiceBonus != 0;
        if (effectDef.BonusDamageSeparateEvent && !hasBonusDamageDice)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_separate_event requires bonus damage dice."
            );
        }
        if (!hasBonusDamageDice)
            return;
        if (effectDef.BonusCondition == "")
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice requires bonus_condition."
            );
        if (effectDef.BonusDamageDiceCount < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_count must be positive."
            );
        if (effectDef.BonusDamageDiceSides < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_sides must be positive."
            );
    }

    private static void AppendExtraDamageSegmentValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef?.ExtraDamageSegments == null)
            return;
        for (int index = 0; index < effectDef.ExtraDamageSegments.Count; index++)
        {
            CombatDamageSegmentDefinition segment = effectDef.ExtraDamageSegments[index];
            if (segment == null)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must be set."
                );
                continue;
            }
            if (segment.DamageTag == "")
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must declare damage_tag."
                );
            }
            else if (DamageTagContentRules.ToDamageTagKind(segment.DamageTag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] uses unsupported damage_tag {segment.DamageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
            bool hasDamageBudget =
                segment.Power > 0
                || segment.DiceCount > 0
                || segment.DiceSides > 0
                || segment.DiceBonus != 0;
            if (!hasDamageBudget)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must set power or dice_count/dice_sides."
                );
            }
            if (
                (segment.DiceCount > 0 || segment.DiceSides > 0 || segment.DiceBonus != 0)
                && (segment.DiceCount < 1 || segment.DiceSides < 1)
            )
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must set dice_count and dice_sides >= 1 together."
                );
            }
            if (
                segment.DoubleDiceOnCritical
                && (segment.DiceCount < 1 || segment.DiceSides < 1)
            )
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] double_dice_on_critical requires dice_count and dice_sides >= 1."
                );
            }
            for (int damageTagIndex = 0; damageTagIndex < segment.DamageTags.Count; damageTagIndex++)
            {
                StringName damageTag = ProgressionDataUtils.to_string_name(
                    segment.DamageTags[damageTagIndex]
                );
                if (
                    damageTag == ""
                    || DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown
                )
                {
                    errors.Add(
                        $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}].damage_tags[{damageTagIndex}] must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
                    );
                }
            }
            AppendDamageSegmentMitigationBypassValidationErrors(
                errors,
                skillId,
                contextLabel,
                segment,
                index
            );
        }
    }

    private static void AppendDamageSegmentMitigationBypassValidationErrors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        CombatDamageSegmentDefinition segment,
        int segmentIndex
    )
    {
        int damageTagCount = segment.MitigationBypassDamageTags?.Count ?? 0;
        int tierCount = segment.MitigationBypassTiers?.Count ?? 0;
        if (damageTagCount == 0 && tierCount == 0)
            return;
        if (damageTagCount == 0 || tierCount == 0)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{segmentIndex}] mitigation bypass requires both mitigation_bypass_damage_tags and mitigation_bypass_tiers."
            );
        }
        for (int index = 0; index < damageTagCount; index++)
        {
            StringName bypassDamageTag = ProgressionDataUtils.to_string_name(
                segment.MitigationBypassDamageTags[index]
            );
            if (DamageTagContentRules.ToDamageTagKind(bypassDamageTag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{segmentIndex}].mitigation_bypass_damage_tags[{index}] uses unsupported damage tag {bypassDamageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
        }
        for (int index = 0; index < tierCount; index++)
        {
            StringName tier = ProgressionDataUtils.to_string_name(
                segment.MitigationBypassTiers[index]
            );
            if (
                DamageTagContentRules.ToMitigationTierKind(tier)
                == DamageMitigationTierKind.Unknown
            )
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{segmentIndex}].mitigation_bypass_tiers[{index}] uses unsupported mitigation tier {tier}; expected one of {DamageTagContentRules.ValidMitigationTierLabel()}."
                );
            }
        }
    }

    private static void AppendTargetDamageMultiplierRuleValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef?.TargetDamageMultiplierRules == null)
            return;
        for (int index = 0; index < effectDef.TargetDamageMultiplierRules.Count; index++)
        {
            CombatTargetDamageMultiplierRuleDefinition rule =
                effectDef.TargetDamageMultiplierRules[index];
            if (rule == null)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} target_damage_multiplier_rules[{index}] must be set."
                );
                continue;
            }
            if (rule.MultiplierPercent < 0)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} target_damage_multiplier_rules[{index}] multiplier_percent must be >= 0."
                );
            }
            bool hasTargetCondition =
                (rule.AnyCreatureTypeTags?.Count ?? 0) > 0
                || (rule.AllCreatureTypeTags?.Count ?? 0) > 0;
            if (!hasTargetCondition)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} target_damage_multiplier_rules[{index}] must declare any_creature_type_tags or all_creature_type_tags."
                );
            }
            AppendStringNameListValidationErrors(
                errors,
                skillId,
                contextLabel,
                $"target_damage_multiplier_rules[{index}].any_creature_type_tags",
                rule.AnyCreatureTypeTags
            );
            AppendStringNameListValidationErrors(
                errors,
                skillId,
                contextLabel,
                $"target_damage_multiplier_rules[{index}].all_creature_type_tags",
                rule.AllCreatureTypeTags
            );
            AppendStringNameListValidationErrors(
                errors,
                skillId,
                contextLabel,
                $"target_damage_multiplier_rules[{index}].excluded_creature_type_tags",
                rule.ExcludedCreatureTypeTags
            );
        }
    }

    private static void AppendStringNameListValidationErrors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        string fieldLabel,
        IReadOnlyList<StringName> values
    )
    {
        if (values == null)
            return;
        var seen = new HashSet<StringName>();
        for (int index = 0; index < values.Count; index++)
        {
            StringName value = ProgressionDataUtils.to_string_name(values[index]);
            if (value == "")
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} {fieldLabel}[{index}] must be non-empty."
                );
                continue;
            }
            if (!seen.Add(value))
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} {fieldLabel} repeats {value}."
                );
            }
        }
    }

    private static void AppendDamageEffectMitigationBypassValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        int damageTagCount = effectDef.MitigationBypassDamageTags?.Count ?? 0;
        int tierCount = effectDef.MitigationBypassTiers?.Count ?? 0;
        if (damageTagCount == 0 && tierCount == 0)
            return;
        if (damageTagCount == 0 || tierCount == 0)
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} mitigation bypass requires both mitigation_bypass_damage_tags and mitigation_bypass_tiers."
            );
        }
        for (int index = 0; index < damageTagCount; index++)
        {
            StringName bypassDamageTag = ProgressionDataUtils.to_string_name(
                effectDef.MitigationBypassDamageTags[index]
            );
            if (DamageTagContentRules.ToDamageTagKind(bypassDamageTag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} mitigation_bypass_damage_tags[{index}] uses unsupported damage tag {bypassDamageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
        }
        for (int index = 0; index < tierCount; index++)
        {
            StringName tier = ProgressionDataUtils.to_string_name(
                effectDef.MitigationBypassTiers[index]
            );
            if (
                DamageTagContentRules.ToMitigationTierKind(tier)
                == DamageMitigationTierKind.Unknown
            )
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} mitigation_bypass_tiers[{index}] uses unsupported mitigation tier {tier}; expected one of {DamageTagContentRules.ValidMitigationTierLabel()}."
                );
            }
        }
    }

    internal void AppendStatusDamageFilterValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        if (
            effectDef.DamageTag != ""
            && DamageTagContentRules.ToDamageTagKind(effectDef.DamageTag)
                == DamageTagKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        for (int index = 0; index < effectDef.DamageTags.Count; index++)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(effectDef.DamageTags[index]);
            if (
                damageTag == ""
                || DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} damage_tags[{index}] must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
        }
        if (
            effectDef.DamageCategory != ""
            && DamageTagContentRules.ToDamageCategoryKind(effectDef.DamageCategory)
                == DamageCategoryKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_category must be one of {DamageTagContentRules.ValidDamageCategoryLabel()}."
            );
        }
        if (
            effectDef.MitigationTier != ""
            && DamageTagContentRules.ToMitigationTierKind(effectDef.MitigationTier)
                == DamageMitigationTierKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} mitigation_tier must be one of {DamageTagContentRules.ValidMitigationTierLabel()}."
            );
        }
    }

    internal void AppendEquipmentDurabilityDamageValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        if (effectDef.Power <= 0)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must have power >= 1."
            );
        bool hasDynamicSave = effectDef.SaveDcModeKind == BattleSaveDcMode.CasterSpell;
        if (effectDef.SaveDc <= 0 && !hasDynamicSave)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must configure a save DC."
            );
        var validationParameters = EquipmentDurabilityDamageValidationParameters.FromEffect(
            effectDef
        );
        if (validationParameters.MaxDamagedItems != 1)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} currently supports max_damaged_items = 1 only."
            );
        if (!validationParameters.RequireDamageApplied)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must set require_damage_applied = true."
            );

        if (validationParameters.TargetSlotsMissingOrEmpty)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.target_slots must include at least one slot."
            );
        _append_equipment_slot_array_validation_errors(
            errors,
            skillId,
            contextLabel,
            (effectDef.Payload as EquipmentDurabilityDamageEffectPayloadDefinition)
                ?.TargetSlots
        );
        _append_equipment_slot_weight_validation_errors(
            errors,
            skillId,
            contextLabel,
            effectDef.EquipmentDurabilitySlotWeights
        );
    }

    private void _append_equipment_slot_array_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        IReadOnlyList<StringName> slotValues
    )
    {
        if (slotValues == null)
            return;
        var seenSlots = new HashSet<StringName>();
        foreach (StringName rawSlotId in slotValues)
        {
            var slotId = ProgressionDataUtils.to_string_name(rawSlotId);
            if (!EquipmentRules.IsValidSlot(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} payload.target_slots uses unsupported slot {slotId}."
                );
                continue;
            }
            if (!seenSlots.Add(slotId))
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} payload.target_slots repeats slot {slotId}."
                );
        }
    }

    private void _append_equipment_slot_weight_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        IReadOnlyList<EquipmentSlotWeightDefinition> slotWeights
    )
    {
        if (slotWeights == null || slotWeights.Count == 0)
            return;
        var seenSlots = new HashSet<StringName>();
        for (int index = 0; index < slotWeights.Count; index++)
        {
            EquipmentSlotWeightDefinition slotWeight = slotWeights[index];
            if (slotWeight == null)
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights[{index}] must be set."
                );
                continue;
            }
            var slotId = ProgressionDataUtils.to_string_name(slotWeight.SlotId);
            if (!EquipmentRules.IsValidSlot(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights uses unsupported slot {slotId}."
                );
            }
            else if (!seenSlots.Add(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights repeats slot {slotId}."
                );
            }
            if (slotWeight.Weight <= 0)
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights[{slotId}] must be a positive int."
                );
            }
        }
    }

    internal void AppendPathStepAoeValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;

        if (
            effectDef.PathStepAreaPatternKind
            is not (
                BattleAreaPattern.Single
                or BattleAreaPattern.Self
                or BattleAreaPattern.Diamond
                or BattleAreaPattern.Square
                or BattleAreaPattern.Radius
                or BattleAreaPattern.Cross
            )
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} uses unsupported path_step_area_pattern {effectDef.PathStepAreaPattern}."
            );
        if (effectDef.PathStepRadius < 0)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} path_step_radius must be >= 0."
            );

        if (!HasRepeatHitStatusConfig(effectDef))
            return;

        if (effectDef.RepeatHitStatusId == "")
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat-hit status config requires repeat_hit_status_id."
            );
        if (effectDef.RepeatHitStatusThreshold < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat_hit_status_threshold must be >= 1."
            );
        if (effectDef.RepeatHitStatusMinSkillLevel < 0)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat_hit_status_min_skill_level must be >= 0."
            );
        if (effectDef.RepeatHitStatusPower < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat_hit_status_power must be >= 1."
            );
        if (
            effectDef.RepeatHitStatusDurationTu <= 0
            || !SkillDefinitionValidationRules.IsValidTuValue(effectDef.RepeatHitStatusDurationTu)
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat_hit_status_duration_tu must be a positive multiple of {SkillDefinitionValidationRules.TuGranularity}."
            );
    }

    private static bool HasRepeatHitStatusConfig(CombatEffectDefinition effectDef)
    {
        return effectDef != null
            && (
                effectDef.RepeatHitStatusId != ""
                || effectDef.RepeatHitStatusThreshold != 0
                || effectDef.RepeatHitStatusMinSkillLevel != 0
                || effectDef.RepeatHitStatusPower != 1
                || effectDef.RepeatHitStatusDurationTu != 0
                || !string.IsNullOrWhiteSpace(effectDef.RepeatHitStatusLogTemplate)
            );
    }

    internal void AppendJumpEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDefinition effectDef,
        string contextLabel
    )
    {
        if (effectDef.ForcedMoveDistance < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have forced_move_distance >= 0 (0 = no max_range cap)."
            );
        if (effectDef.JumpArcRatio < CombatEffectContentRules.MinJumpArcRatio)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio >= {CombatEffectContentRules.MinJumpArcRatio:0.00}; jump must lift the unit."
            );
        if (effectDef.JumpArcRatio > 1.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio <= 1.0."
            );
        if (effectDef.JumpBaseBudget < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_base_budget >= 0."
            );
        if (effectDef.JumpStrScale < 0.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_str_scale >= 0."
            );
        if (effectDef.JumpRangeMultiplier < 1)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_range_multiplier >= 1."
            );
    }
}
