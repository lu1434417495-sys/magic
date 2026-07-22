using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

internal sealed class SkillDamageEffectValidator
{
    private readonly record struct EquipmentDurabilityDamageValidationParameters(
        int MaxDamagedItems,
        bool RequireDamageApplied,
        bool TargetSlotsMissingOrEmpty
    )
    {
        public static EquipmentDurabilityDamageValidationParameters FromEffect(
            CombatEffectDef effectDef
        )
        {
            Dictionary parameters = effectDef?.@params ?? new Dictionary();
            return new EquipmentDurabilityDamageValidationParameters(
                SkillContentRegistry.DictInt(parameters, "max_damaged_items", 1),
                effectDef?.require_damage_applied ?? false,
                ReadTargetSlotsMissingOrEmpty(parameters)
            );
        }

        private static bool ReadTargetSlotsMissingOrEmpty(Dictionary parameters)
        {
            if (!SkillContentRegistry.TryGetParameter(parameters, "target_slots", out object rawTargetSlots))
                return true;
            return SkillContentRegistry.TryAsArray(rawTargetSlots, out Array targetSlots) && targetSlots.Count == 0;
        }
    }

    internal void AppendDamageEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        var damageTag = effectDef.damage_tag;
        bool usesWeaponDamageTag = effectDef.use_weapon_physical_damage_tag;

        if (parameters.ContainsKey("damage_tag"))
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} params.damage_tag is unsupported on damage effects; use damage_tag or use_weapon_physical_damage_tag."
            );
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

        if (effectDef.hp_ratio_threshold_percent < 0 || effectDef.hp_ratio_threshold_percent > 100)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} hp_ratio_threshold_percent must be 0 or from 1 to 100."
            );

        if (
            effectDef.bonus_condition == "target_creature_type"
            && effectDef.bonus_condition_creature_type_tag == ""
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_condition target_creature_type requires bonus_condition_creature_type_tag."
            );
        }
        if (
            effectDef.bonus_condition_creature_type_tag != ""
            && effectDef.bonus_condition != "target_creature_type"
        )
        {
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_condition_creature_type_tag requires bonus_condition target_creature_type."
            );
        }

        bool hasBonusDamageDice =
            effectDef.bonus_damage_dice_count > 0
            || effectDef.bonus_damage_dice_sides > 0
            || effectDef.bonus_damage_dice_bonus != 0;
        if (!hasBonusDamageDice)
            return;
        if (effectDef.bonus_condition == "")
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice requires bonus_condition."
            );
        if (effectDef.bonus_damage_dice_count < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_count must be positive."
            );
        if (effectDef.bonus_damage_dice_sides < 1)
            errors.Add(
                $"Skill {skillId} damage effect in {contextLabel} bonus_damage_dice_sides must be positive."
            );
    }

    private static void AppendExtraDamageSegmentValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef?.extra_damage_segments == null)
            return;
        for (int index = 0; index < effectDef.extra_damage_segments.Count; index++)
        {
            CombatDamageSegmentDef segment = effectDef.extra_damage_segments[index];
            if (segment == null)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must be set."
                );
                continue;
            }
            if (segment.damage_tag == "")
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must declare damage_tag."
                );
            }
            else if (DamageTagContentRules.ToDamageTagKind(segment.damage_tag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] uses unsupported damage_tag {segment.damage_tag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
            bool hasDamageBudget =
                segment.power > 0
                || segment.dice_count > 0
                || segment.dice_sides > 0
                || segment.dice_bonus != 0;
            if (!hasDamageBudget)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must set power or dice_count/dice_sides."
                );
            }
            if (
                (segment.dice_count > 0 || segment.dice_sides > 0 || segment.dice_bonus != 0)
                && (segment.dice_count < 1 || segment.dice_sides < 1)
            )
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} extra_damage_segments[{index}] must set dice_count and dice_sides >= 1 together."
                );
            }
            for (int damageTagIndex = 0; damageTagIndex < segment.damage_tags.Count; damageTagIndex++)
            {
                StringName damageTag = ProgressionDataUtils.to_string_name(
                    segment.damage_tags[damageTagIndex]
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
        CombatDamageSegmentDef segment,
        int segmentIndex
    )
    {
        int damageTagCount = segment.mitigation_bypass_damage_tags?.Count ?? 0;
        int tierCount = segment.mitigation_bypass_tiers?.Count ?? 0;
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
                segment.mitigation_bypass_damage_tags[index]
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
                segment.mitigation_bypass_tiers[index]
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
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef?.target_damage_multiplier_rules == null)
            return;
        for (int index = 0; index < effectDef.target_damage_multiplier_rules.Count; index++)
        {
            CombatTargetDamageMultiplierRuleDef rule =
                effectDef.target_damage_multiplier_rules[index];
            if (rule == null)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} target_damage_multiplier_rules[{index}] must be set."
                );
                continue;
            }
            if (rule.multiplier_percent < 0)
            {
                errors.Add(
                    $"Skill {skillId} damage effect in {contextLabel} target_damage_multiplier_rules[{index}] multiplier_percent must be >= 0."
                );
            }
            bool hasTargetCondition =
                (rule.any_creature_type_tags?.Count ?? 0) > 0
                || (rule.all_creature_type_tags?.Count ?? 0) > 0;
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
                rule.any_creature_type_tags
            );
            AppendStringNameListValidationErrors(
                errors,
                skillId,
                contextLabel,
                $"target_damage_multiplier_rules[{index}].all_creature_type_tags",
                rule.all_creature_type_tags
            );
            AppendStringNameListValidationErrors(
                errors,
                skillId,
                contextLabel,
                $"target_damage_multiplier_rules[{index}].excluded_creature_type_tags",
                rule.excluded_creature_type_tags
            );
        }
    }

    private static void AppendStringNameListValidationErrors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        string fieldLabel,
        Godot.Collections.Array<StringName> values
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
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        int damageTagCount = effectDef.mitigation_bypass_damage_tags?.Count ?? 0;
        int tierCount = effectDef.mitigation_bypass_tiers?.Count ?? 0;
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
                effectDef.mitigation_bypass_damage_tags[index]
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
                effectDef.mitigation_bypass_tiers[index]
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
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        if (parameters.ContainsKey("damage_tag"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_tag is unsupported; use CombatEffectDef.damage_tag."
            );
        }
        if (parameters.ContainsKey("damage_tags"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_tags is unsupported; use CombatEffectDef.damage_tags."
            );
        }
        if (parameters.ContainsKey("damage_category"))
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} params.damage_category is unsupported; use CombatEffectDef.damage_category."
            );
        }
        if (
            effectDef.damage_tag != ""
            && DamageTagContentRules.ToDamageTagKind(effectDef.damage_tag)
                == DamageTagKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        for (int index = 0; index < effectDef.damage_tags.Count; index++)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(effectDef.damage_tags[index]);
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
            effectDef.damage_category != ""
            && DamageTagContentRules.ToDamageCategoryKind(effectDef.damage_category)
                == DamageCategoryKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} status effect in {contextLabel} damage_category must be one of {DamageTagContentRules.ValidDamageCategoryLabel()}."
            );
        }
        if (
            effectDef.mitigation_tier != ""
            && DamageTagContentRules.ToMitigationTierKind(effectDef.mitigation_tier)
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
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        if (effectDef.power <= 0)
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} must have power >= 1."
            );
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        bool hasDynamicSave = effectDef.SaveDcModeKind == BattleSaveDcMode.CasterSpell;
        if (effectDef.save_dc <= 0 && !hasDynamicSave)
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
            parameters,
            "target_slots"
        );
        if (parameters.ContainsKey("slot_weight_map"))
        {
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.slot_weight_map is unsupported; use equipment_durability_slot_weights."
            );
        }
        _append_equipment_slot_weight_validation_errors(
            errors,
            skillId,
            contextLabel,
            effectDef.equipment_durability_slot_weights
        );
    }

    private void _append_equipment_slot_array_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        Dictionary parameters,
        string paramName
    )
    {
        if (parameters == null || !parameters.ContainsKey(paramName))
            return;
        object value = parameters[paramName];
        if (!SkillContentRegistry.TryAsArray(value, out Array slotValues))
        {
            errors.Add(
                $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} must be an Array."
            );
            return;
        }
        var seenSlots = new HashSet<StringName>();
        foreach (object rawSlotId in slotValues)
        {
            var slotId = ProgressionDataUtils.to_string_name(rawSlotId);
            if (!EquipmentRules.IsValidSlot(slotId))
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} uses unsupported slot {slotId}."
                );
                continue;
            }
            if (!seenSlots.Add(slotId))
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} params.{paramName} repeats slot {slotId}."
                );
        }
    }

    private void _append_equipment_slot_weight_validation_errors(
        Array<string> errors,
        StringName skillId,
        string contextLabel,
        Godot.Collections.Array<CombatEffectSlotWeightDef> slotWeights
    )
    {
        if (slotWeights == null || slotWeights.Count == 0)
            return;
        var seenSlots = new HashSet<StringName>();
        for (int index = 0; index < slotWeights.Count; index++)
        {
            CombatEffectSlotWeightDef slotWeight = slotWeights[index];
            if (slotWeight == null)
            {
                errors.Add(
                    $"Skill {skillId} equipment_durability_damage effect in {contextLabel} equipment_durability_slot_weights[{index}] must be set."
                );
                continue;
            }
            var slotId = ProgressionDataUtils.to_string_name(slotWeight.slot_id);
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
            if (slotWeight.weight <= 0)
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
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null || effectDef.@params == null)
            return;
        Dictionary parameters = effectDef.@params;
        if (
            parameters.ContainsKey("path_step_log_label")
            && SkillContentRegistry.DictString(parameters, "path_step_log_label").StripEdges().Length == 0
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.path_step_log_label must be non-empty when set."
            );
        if (!HasRepeatHitStatusConfig(parameters))
            return;

        var statusId = SkillContentRegistry.DictStringName(parameters, "repeat_hit_status_id");
        if (statusId == "")
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat-hit status config requires params.repeat_hit_status_id."
            );
        if (SkillContentRegistry.DictInt(parameters, "repeat_hit_status_threshold") < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_threshold must be >= 1."
            );
        if (SkillContentRegistry.DictInt(parameters, "repeat_hit_status_min_skill_level") < 0)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_min_skill_level must be >= 0."
            );
        if (SkillContentRegistry.DictInt(parameters, "repeat_hit_status_power", 1) < 1)
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_power must be >= 1."
            );
        if (!parameters.ContainsKey("repeat_hit_status_duration_tu"))
        {
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} repeat-hit status config requires params.repeat_hit_status_duration_tu."
            );
        }
        else
        {
            int durationTu = SkillContentRegistry.DictInt(parameters, "repeat_hit_status_duration_tu");
            if (durationTu <= 0 || !SkillContentRegistry.IsValidTuValue(durationTu))
                errors.Add(
                    $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_duration_tu must be a positive multiple of {SkillContentRegistry.TuGranularity}."
                );
        }
        if (
            parameters.ContainsKey("repeat_hit_status_params")
            && !SkillContentRegistry.TryAsDictionary(parameters["repeat_hit_status_params"], out _)
        )
            errors.Add(
                $"Skill {skillId} path_step_aoe effect in {contextLabel} params.repeat_hit_status_params must be a Dictionary."
            );
    }

    private bool HasRepeatHitStatusConfig(Dictionary parameters)
    {
        foreach (
            string key in new[]
            {
                "repeat_hit_status_id",
                "repeat_hit_status_threshold",
                "repeat_hit_status_min_skill_level",
                "repeat_hit_status_power",
                "repeat_hit_status_duration_tu",
                "repeat_hit_status_params",
                "repeat_hit_status_log_template",
            }
        )
        {
            if (parameters.ContainsKey(key))
                return true;
        }
        return false;
    }

    internal void AppendJumpEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef.forced_move_distance < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have forced_move_distance >= 0 (0 = no max_range cap)."
            );
        if (effectDef.jump_arc_ratio < CombatEffectContentRules.MinJumpArcRatio)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio >= {CombatEffectContentRules.MinJumpArcRatio:0.00}; jump must lift the unit."
            );
        if (effectDef.jump_arc_ratio > 1.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} requires jump_arc_ratio <= 1.0."
            );
        if (effectDef.jump_base_budget < 0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_base_budget >= 0."
            );
        if (effectDef.jump_str_scale < 0.0)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_str_scale >= 0."
            );
        if (effectDef.jump_range_multiplier < 1)
            errors.Add(
                $"Skill {skillId} jump effect in {contextLabel} must have jump_range_multiplier >= 1."
            );
    }
}
