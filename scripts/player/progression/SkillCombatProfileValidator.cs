using System.Collections.Generic;
using Godot;
using Godot.Collections;
using VT = Godot.Variant.Type;

internal sealed class SkillCombatProfileValidator
{
    private readonly SkillDamageEffectValidator _damageEffectValidator;
    private readonly SkillExecuteEffectValidator _executeEffectValidator;

    internal SkillCombatProfileValidator(
        SkillDamageEffectValidator damageEffectValidator,
        SkillExecuteEffectValidator executeEffectValidator
    )
    {
        _damageEffectValidator = damageEffectValidator;
        _executeEffectValidator = executeEffectValidator;
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> TypedEffectParamTargets =
        new()
        {
            { "dice_count", "dice_count" },
            { "dice_sides", "dice_sides" },
            { "dice_bonus", "dice_bonus" },
            { "base_sides", "dice_sides_base" },
            { "con_mod_sides", "dice_sides_per_constitution_mod" },
            { "will_mod_sides", "dice_sides_per_willpower_mod" },
            { "dice_sides_base", "dice_sides_base" },
            { "dice_sides_per_constitution_mod", "dice_sides_per_constitution_mod" },
            { "dice_sides_per_willpower_mod", "dice_sides_per_willpower_mod" },
            { "runtime_pre_resistance_damage_multiplier", "pre_resistance_damage_multiplier" },
            { "dr_bypass_tag", "dr_bypass_tag" },
            { "hp_ratio_threshold_percent", "hp_ratio_threshold_percent" },
            { "bonus_damage_dice_count", "bonus_damage_dice_count" },
            { "bonus_damage_dice_sides", "bonus_damage_dice_sides" },
            { "bonus_damage_dice_bonus", "bonus_damage_dice_bonus" },
            { "add_weapon_dice", "add_weapon_dice" },
            { "requires_weapon", "requires_weapon" },
            { "use_weapon_physical_damage_tag", "use_weapon_physical_damage_tag" },
            { "resolve_as_weapon_attack", "resolve_as_weapon_attack" },
            { "allow_repeat_hits_across_steps", "allow_repeat_hits_across_steps" },
            { "prevent_repeat_target", "prevent_repeat_target" },
            { "stop_on_miss", "stop_on_miss" },
            { "stop_on_target_down", "stop_on_target_down" },
            { "remove_harmful", "remove_harmful" },
            { "remove_harmful_from_allies", "remove_harmful_from_allies" },
            { "remove_beneficial", "remove_beneficial" },
            { "remove_beneficial_from_enemies", "remove_beneficial_from_enemies" },
            { "require_damage_applied", "require_damage_applied" },
            { "lifetime_policy", "lifetime_policy" },
            { "move_cost_delta", "move_cost_delta" },
            { "min_hp_after_damage", "min_hp_after_damage" },
            { "threshold_base_value", "threshold_base_value" },
            { "threshold_level_anchor", "threshold_level_anchor" },
            { "threshold_level_bonus_per_delta", "threshold_level_bonus_per_delta" },
            { "threshold_max_hp_ratio_percent", "threshold_max_hp_ratio_percent" },
            { "threshold_cap_max_hp_ratio_percent", "threshold_cap_max_hp_ratio_percent" },
            { "soul_fracture_duration_tu", "soul_fracture_duration_tu" },
            { "heal_multiplier_percent", "heal_multiplier_percent" },
            { "shield_gain_multiplier_percent", "shield_gain_multiplier_percent" },
            { "attack_roll_penalty", "attack_roll_penalty" },
            { "attack_roll_bonus", "attack_roll_bonus" },
            { "attack_roll_advantage", "attack_roll_advantage" },
            { "consume_on_next_attack_check", "consume_on_next_attack_check" },
            { "consume_on_next_save", "consume_on_next_save" },
            { "undispellable", "undispellable" },
            { "dispellable_magic", "dispellable_magic" },
            { "dispellable_harmful_magic", "dispellable_harmful_magic" },
            { "dispellable_beneficial_magic", "dispellable_beneficial_magic" },
            { "mitigation_tier", "mitigation_tier" },
            { "counts_as_debuff_override", "counts_as_debuff_override" },
            { "counts_as_debuff", "counts_as_debuff" },
            { "lock_counterattack", "lock_counterattack" },
            { "lock_guard", "lock_guard" },
            { "lock_dodge_bonus", "lock_dodge_bonus" },
            { "lock_crit", "lock_crit" },
            { "save_bonus", "save_bonus" },
            { "control_save_bonus", "control_save_bonus" },
            { "save_advantage_tags", "save_advantage_tags" },
            { "save_disadvantage_tags", "save_disadvantage_tags" },
            { "save_immunity_tags", "save_immunity_tags" },
            { "save_tags", "save_advantage_tags/save_disadvantage_tags/save_immunity_tags" },
            { "secondary_hit_save_bonus", "control_save_bonus" },
            { "passive_reduction", "passive_reduction" },
            { "content_dr", "content_dr" },
            { "guard_block", "guard_block" },
            { "main_skill_lock_other_debuff_count", "main_skill_lock_other_debuff_count" },
            { "ap_gain", "ap_gain" },
            { "free_move_points_gain", "free_move_points_gain" },
        };

    internal void AppendCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        AppendCombatProfileValidationErrors(errors, skillId, combatProfile, null);
    }

    internal void AppendCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        if (combatProfile.skill_id != "" && combatProfile.skill_id != skillId)
            errors.Add($"Skill {skillId} combat_profile.skill_id must match skill_id.");
        if (combatProfile.target_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_mode.");
        else if (
            !CombatSkillTargetingContentRules.IsValidCombatTargetMode(combatProfile.target_mode)
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_mode {combatProfile.target_mode}; expected one of {CombatSkillTargetingContentRules.ValidCombatTargetModeLabel()}."
            );

        if (combatProfile.target_team_filter == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_team_filter.");
        else if (
            !CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(
                combatProfile.target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_team_filter {combatProfile.target_team_filter}; expected one of {CombatTargetTeamContentRules.ValidSkillTargetTeamFilterLabel()}."
            );

        if (combatProfile.target_selection_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing target_selection_mode.");
        else if (
            !CombatSkillTargetingContentRules.IsValidTargetSelectionMode(
                combatProfile.target_selection_mode
            )
        )
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported target_selection_mode {combatProfile.target_selection_mode}; expected one of {CombatSkillTargetingContentRules.ValidTargetSelectionModeLabel()}."
            );

        if (combatProfile.selection_order_mode == "")
            errors.Add($"Skill {skillId} combat_profile is missing selection_order_mode.");
        else if (combatProfile.SelectionOrderModeKind == BattleTargetSelectionOrderMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported selection_order_mode {combatProfile.selection_order_mode}; expected one of {CombatSkillTargetingContentRules.ValidSelectionOrderModeLabel()}."
            );

        if (!CombatSkillTargetingContentRules.IsValidAreaPattern(combatProfile.area_pattern))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_pattern {combatProfile.area_pattern}; expected one of {CombatSkillTargetingContentRules.ValidAreaPatternLabel()}."
            );
        if (combatProfile.MasteryTriggerModeKind == CombatSkillMasteryTriggerMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported mastery_trigger_mode {combatProfile.mastery_trigger_mode}."
            );
        if (combatProfile.MasteryAmountModeKind == CombatSkillMasteryAmountMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported mastery_amount_mode {combatProfile.mastery_amount_mode}."
            );
        if (combatProfile.range_value < 0)
            errors.Add($"Skill {skillId} combat_profile range_value must be >= 0.");
        if (!IsValidWeaponRangePolicy(combatProfile.weapon_range_policy))
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported weapon_range_policy {combatProfile.weapon_range_policy}; expected empty, current_weapon, or configured."
            );
        if (combatProfile.area_value < 0)
            errors.Add($"Skill {skillId} combat_profile area_value must be >= 0.");
        if (
            combatProfile.ap_cost < 0
            || combatProfile.mp_cost < 0
            || combatProfile.stamina_cost < 0
            || combatProfile.aura_cost < 0
        )
            errors.Add($"Skill {skillId} combat_profile costs must be >= 0.");
        if (!SkillContentRegistry.IsValidTuValue(combatProfile.cooldown_tu))
            errors.Add(
                $"Skill {skillId} combat_profile cooldown_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );
        if (!SkillContentRegistry.IsValidTuValue(combatProfile.casting_time_tu))
            errors.Add(
                $"Skill {skillId} combat_profile casting_time_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );
        if (combatProfile.casting_maintenance_dc < 0)
            errors.Add($"Skill {skillId} combat_profile casting_maintenance_dc must be >= 0.");
        if (combatProfile.casting_spell_control_dc < 0)
            errors.Add($"Skill {skillId} combat_profile casting_spell_control_dc must be >= 0.");
        if (!IsValidPendingCastBindingMode(combatProfile.pending_cast_binding_mode))
            errors.Add(
                $"Skill {skillId} combat_profile pending_cast_binding_mode uses unsupported value {combatProfile.pending_cast_binding_mode}."
            );
        if (combatProfile.AttackResolutionModeKind == CombatSkillAttackResolutionMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported attack_resolution_mode {combatProfile.attack_resolution_mode}."
            );
        bool hasCastingTime = combatProfile.casting_time_tu > 0;
        if (hasCastingTime)
        {
            AppendCastingTimeCompatibilityErrors(errors, skillId, combatProfile, skillDef);
        }
        _executeEffectValidator.AppendTemporalReleaseSkillValidationErrors(errors, skillId, combatProfile);

        AppendSpellFateValidationErrors(errors, skillId, combatProfile);
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.delivery_categories",
            combatProfile.delivery_categories
        );
        foreach (
            StringName requiredCategory in CombatEffectCategoryContentRules.RequiredDeliveryCategories(
                skillDef != null && skillDef.HasTag("mage") && skillDef.HasTag("magic"),
                skillDef != null && skillDef.HasTag("dragon_breath"),
                skillDef != null && skillDef.HasTag("archer") && skillDef.HasTag("ranged"),
                combatProfile.range_value,
                HasDamageEffect(combatProfile),
                HasAttackDamage(combatProfile)
            )
        )
        {
            if (!combatProfile.delivery_categories.Contains(requiredCategory))
                errors.Add(
                    $"Skill {skillId} combat_profile.delivery_categories must explicitly include {requiredCategory}."
                );
        }
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.required_weapon_families",
            combatProfile.required_weapon_families
        );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.excluded_weapon_families",
            combatProfile.excluded_weapon_families
        );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.excluded_weapon_type_ids",
            combatProfile.excluded_weapon_type_ids
        );

        foreach (object overrideLevelKey in combatProfile.level_overrides.Keys)
        {
            if (!SkillContentRegistry.TryStrictInt(overrideLevelKey, out int overrideLevel))
            {
                errors.Add(
                    $"Skill {skillId} combat_profile level override key {overrideLevelKey} must be an int."
                );
                continue;
            }
            SkillContentRegistry.TryGetDictionaryValue(combatProfile.level_overrides, overrideLevelKey, out object overrideData);
            if (!SkillContentRegistry.TryAsDictionary(overrideData, out Dictionary overrideDict))
            {
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey} must be a Dictionary."
                );
                continue;
            }
            if (overrideLevel < 0)
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey} must use a non-negative level."
                );
            foreach (string costKey in new[] { "ap_cost", "mp_cost", "stamina_cost", "aura_cost" })
            {
                if (
                    SkillContentRegistry.TryReadLevelOverrideInt(
                        errors,
                        skillId,
                        overrideLevelKey,
                        overrideDict,
                        costKey,
                        out int costValue
                    )
                    && costValue < 0
                )
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.{costKey} must be >= 0."
                    );
            }
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "cooldown_tu",
                    out int cooldownTu
                )
                && !SkillContentRegistry.IsValidTuValue(cooldownTu)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.cooldown_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
                );
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_time_tu",
                    out int castingTimeTu
                )
                && !SkillContentRegistry.IsValidTuValue(castingTimeTu)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_time_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
                );
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_maintenance_dc",
                    out int castingMaintenanceDc
                )
                && castingMaintenanceDc < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_maintenance_dc must be >= 0."
                );
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "casting_spell_control_dc",
                    out int castingSpellControlDc
                )
                && castingSpellControlDc < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_spell_control_dc must be >= 0."
                );
            if (overrideDict.ContainsKey("pending_cast_binding_mode"))
            {
                var overrideBindingMode = ProgressionDataUtils.to_string_name(
                    overrideDict["pending_cast_binding_mode"]
                );
                if (!IsValidPendingCastBindingMode(overrideBindingMode))
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.pending_cast_binding_mode uses unsupported value {overrideBindingMode}."
                    );
            }
            SkillContentRegistry.TryReadLevelOverrideInt(
                errors,
                skillId,
                overrideLevelKey,
                overrideDict,
                "attack_roll_bonus",
                out _
            );
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "area_value",
                    out int areaValue
                )
                && areaValue < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_value must be >= 0."
                );
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "range_value",
                    out int rangeValue
                )
                && rangeValue < 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.range_value must be >= 0."
                );
            if (overrideDict.ContainsKey("area_pattern"))
            {
                var overrideAreaPattern = ProgressionDataUtils.to_string_name(
                    overrideDict["area_pattern"]
                );
                if (!CombatSkillTargetingContentRules.IsValidAreaPattern(overrideAreaPattern))
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.area_pattern uses unsupported area_pattern {overrideAreaPattern}; expected one of {CombatSkillTargetingContentRules.ValidAreaPatternLabel()}."
                    );
            }
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "max_target_count",
                    out int maxTargetCount
                )
                && maxTargetCount < 1
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.max_target_count must be >= 1."
                );
            if (castingTimeTu > 0)
            {
                AppendCastingTimeCompatibilityErrors(
                    errors,
                    skillId,
                    combatProfile,
                    skillDef,
                    $"combat_profile level override {overrideLevelKey}"
                );
            }
        }

        if (combatProfile.min_target_count <= 0)
            errors.Add($"Skill {skillId} combat_profile min_target_count must be >= 1.");
        if (combatProfile.max_target_count < combatProfile.min_target_count)
            errors.Add(
                $"Skill {skillId} combat_profile max_target_count must be >= min_target_count."
            );
        _executeEffectValidator.AppendExecuteCombatProfileValidationErrors(errors, skillId, skillDef, combatProfile);

        for (int effectIndex = 0; effectIndex < combatProfile.effect_defs.Count; effectIndex++)
            AppendEffectValidationErrors(
                errors,
                skillId,
                combatProfile.effect_defs[effectIndex],
                $"combat_profile.effect_defs[{effectIndex}]"
            );

        if (
            combatProfile.passive_effect_defs != null
            && combatProfile.passive_effect_defs.Count > 0
        )
        {
            for (
                int passiveIndex = 0;
                passiveIndex < combatProfile.passive_effect_defs.Count;
                passiveIndex++
            )
            {
                CombatEffectDef passiveEffect = combatProfile.passive_effect_defs[passiveIndex];
                if (
                    passiveEffect != null
                    && passiveEffect.EffectKind == BattleEffectKind.Execute
                )
                {
                    errors.Add(
                        $"Skill {skillId} passive_effect_defs[{passiveIndex}] uses effect_type 'execute', which is not allowed in passive effects."
                    );
                    continue;
                }
                AppendEffectValidationErrors(
                    errors,
                    skillId,
                    passiveEffect,
                    $"combat_profile.passive_effect_defs[{passiveIndex}]"
                );
            }
        }

        var seenOptionIds = new HashSet<StringName>();
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            if (castVariant == null)
            {
                errors.Add(
                    $"Skill {skillId} combat_profile.cast_variants[{optionIndex}] failed to cast to CombatCastVariantDef."
                );
                continue;
            }
            if (castVariant.variant_id == "")
                errors.Add($"Skill {skillId} has a cast option without variant_id.");
            else if (!seenOptionIds.Add(castVariant.variant_id))
                errors.Add(
                    $"Skill {skillId} declares duplicate cast option {castVariant.variant_id}."
                );

            if (castVariant.target_mode == "")
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} is missing target_mode."
                );
            else if (
                castVariant.TargetModeKind == BattleTargetMode.Unknown
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported target_mode {castVariant.target_mode}; expected one of {CombatSkillTargetingContentRules.ValidCastVariantTargetModeLabel()}."
                );

            if (
                !CombatSkillTargetingContentRules.IsValidFootprintPattern(
                    castVariant.footprint_pattern
                )
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported footprint_pattern {castVariant.footprint_pattern}; expected one of {CombatSkillTargetingContentRules.ValidFootprintPatternLabel()}."
                );

            if (castVariant.min_skill_level < 0)
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} min_skill_level must be >= 0."
                );
            else if (
                skillDef != null
                && skillDef.dynamic_max_level_stat_id == ""
                && skillDef.max_level >= 0
                && castVariant.min_skill_level > skillDef.max_level
            )
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} min_skill_level must be <= max_level {skillDef.max_level}."
                );

            if (castVariant.required_coord_count <= 0)
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} must have required_coord_count >= 1."
                );

            for (int effectIndex = 0; effectIndex < castVariant.effect_defs.Count; effectIndex++)
                AppendEffectValidationErrors(
                    errors,
                    skillId,
                    castVariant.effect_defs[effectIndex],
                    $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]"
                );
        }
    }

    private void AppendSpellFateValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;
        if (combatProfile.SpellFateModeKind == CombatSpellFateMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_fate_mode {combatProfile.spell_fate_mode}."
            );
        if (combatProfile.SpellCriticalModeKind == CombatSpellCriticalMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported spell_critical_mode {combatProfile.spell_critical_mode}."
            );
        if (combatProfile.BacklashModeKind == CombatSkillBacklashMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported backlash_mode {combatProfile.backlash_mode}."
            );
        if (
            combatProfile.SpellCriticalModeKind != CombatSpellCriticalMode.None
            && combatProfile.SpellFateModeKind == CombatSpellFateMode.None
        )
            errors.Add(
                $"Skill {skillId} combat_profile spell_critical_mode requires spell_fate_mode."
            );
        if (
            combatProfile.BacklashModeKind != CombatSkillBacklashMode.None
            && combatProfile.SpellFateModeKind == CombatSpellFateMode.None
        )
            errors.Add($"Skill {skillId} combat_profile backlash_mode requires spell_fate_mode.");
        if (combatProfile.AreaOriginModeKind == CombatAreaOriginMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_origin_mode {combatProfile.area_origin_mode}."
            );
        if (combatProfile.AreaDirectionModeKind == CombatAreaDirectionMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported area_direction_mode {combatProfile.area_direction_mode}."
            );
        if (
            combatProfile.spell_critical_mp_refund_percent < 0
            || combatProfile.spell_critical_mp_refund_percent > 100
        )
            errors.Add(
                $"Skill {skillId} combat_profile spell_critical_mp_refund_percent must be between 0 and 100."
            );
        if (combatProfile.fumble_protection_extra_mp_percent < 0)
            errors.Add(
                $"Skill {skillId} combat_profile fumble_protection_extra_mp_percent must be >= 0."
            );
        foreach (int protectionValue in combatProfile.fumble_protection_curve)
        {
            if (protectionValue < 0)
            {
                errors.Add(
                    $"Skill {skillId} combat_profile fumble_protection_curve values must be >= 0."
                );
                break;
            }
        }
        if (combatProfile.backlash_offset_radius < 0)
            errors.Add($"Skill {skillId} combat_profile backlash_offset_radius must be >= 0.");
        if (combatProfile.BacklashModeKind == CombatSkillBacklashMode.GroundAnchorDrift)
        {
            if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires target_mode ground."
                );
            if (combatProfile.backlash_offset_radius <= 0)
                errors.Add(
                    $"Skill {skillId} combat_profile ground_anchor_drift requires backlash_offset_radius >= 1."
                );
        }
    }

    internal void AppendEffectValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
        {
            errors.Add($"Skill {skillId} has a null effect in {contextLabel}.");
            return;
        }
        if (effectDef.effect_type == "")
        {
            errors.Add($"Skill {skillId} has an effect without effect_type in {contextLabel}.");
            return;
        }
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (effectKind == BattleEffectKind.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_type {effectDef.effect_type}."
            );
        if (effectDef.min_skill_level < 0)
            errors.Add($"Skill {skillId} effect {contextLabel} min_skill_level must be >= 0.");
        if (effectDef.max_skill_level >= 0 && effectDef.max_skill_level < effectDef.min_skill_level)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} max_skill_level must be >= min_skill_level or -1."
            );
        if (effectDef.TriggerEventKind == CombatEffectTriggerEvent.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_event {effectDef.trigger_event}."
            );
        if (effectDef.TriggerConditionKind == CombatEffectTriggerCondition.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported trigger_condition {effectDef.trigger_condition}."
            );
        if (
            !CombatTargetTeamContentRules.IsValidEffectTargetTeamFilter(
                effectDef.effect_target_team_filter
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported effect_target_team_filter {effectDef.effect_target_team_filter}; expected one of {CombatTargetTeamContentRules.ValidEffectTargetTeamFilterLabel()}."
            );
        if (!SkillContentRegistry.IsValidTuValue(effectDef.duration_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} duration_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );
        if (!SkillContentRegistry.IsValidTuValue(effectDef.tick_interval_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} tick_interval_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );

        AppendSaveValidationErrors(errors, skillId, effectDef, contextLabel);

        Dictionary parameters = effectDef.@params ?? new Dictionary();
        var unsupportedParamAliases = new System.Collections.Generic.Dictionary<string, string>
        {
            { "damage_dice_count", "dice_count" },
            { "damage_dice_sides", "dice_sides" },
            { "damage_dice_bonus", "dice_bonus" },
            { "tag", "damage_tag" },
            { "bypass_tag", "dr_bypass_tag" },
            { "low_hp_ratio", "hp_ratio_threshold_percent" },
        };
        foreach (var alias in unsupportedParamAliases)
        {
            if (parameters.ContainsKey(alias.Key))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{alias.Key} is unsupported; use {alias.Value}."
                );
        }
        if (parameters.ContainsKey("duration"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.duration is unsupported; use CombatEffectDef.duration_tu."
            );
        if (parameters.ContainsKey("effect_tags"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.effect_tags is unsupported; use CombatEffectDef.effect_tags."
            );
        if (parameters.ContainsKey("status_tags"))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.status_tags is unsupported; status tags are projected from CombatEffectDef.effect_tags."
            );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            $"effect {contextLabel} effect_tags",
            effectDef.effect_tags
        );
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            $"effect {contextLabel} effect_categories",
            effectDef.effect_categories
        );
        foreach (
            StringName requiredCategory in CombatEffectCategoryContentRules.RequiredEffectCategories(
                effectDef.damage_tag,
                effectDef.save_tag,
                effectDef.EffectKind
            )
        )
        {
            if (!effectDef.effect_categories.Contains(requiredCategory))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel}.effect_categories must explicitly include {requiredCategory}."
                );
        }
        _executeEffectValidator.AppendSaveBonusByTagValidationErrors(errors, skillId, effectDef, contextLabel);
        _executeEffectValidator.AppendTemporalStatusEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendTypedEffectParamValidationErrors(errors, skillId, effectDef, contextLabel);
        AppendAttributeScaledDiceValidationErrors(errors, skillId, effectDef, contextLabel);

        if (effectKind == BattleEffectKind.Damage)
        {
            _damageEffectValidator.AppendDamageEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (
            effectKind == BattleEffectKind.Status
            || effectKind == BattleEffectKind.ApplyStatus
        )
        {
            if (effectDef.status_id == "")
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} is missing status_id."
                );
            if (effectDef.terrain_effect_id == "" && parameters.ContainsKey("duration_tu"))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.duration_tu is unsupported; use CombatEffectDef.duration_tu."
                );
            if (effectDef.terrain_effect_id == "" && parameters.ContainsKey("tick_interval_tu"))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.tick_interval_tu is unsupported; use CombatEffectDef.tick_interval_tu."
                );
            if (parameters.ContainsKey("range_bonus"))
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} params.range_bonus is unsupported; use CombatEffectDef.range_bonus."
                );
            bool hasSourceBoundWeaponBonusDice =
                effectDef.source_bound_weapon_bonus_damage_dice_count > 0
                || effectDef.source_bound_weapon_bonus_damage_dice_sides > 0
                || effectDef.source_bound_weapon_bonus_damage_dice_bonus != 0;
            if (hasSourceBoundWeaponBonusDice)
            {
                if (effectDef.source_bound_weapon_bonus_damage_dice_count < 1)
                    errors.Add(
                        $"Skill {skillId} status effect in {contextLabel} source_bound_weapon_bonus_damage_dice_count must be positive."
                    );
                if (effectDef.source_bound_weapon_bonus_damage_dice_sides < 1)
                    errors.Add(
                        $"Skill {skillId} status effect in {contextLabel} source_bound_weapon_bonus_damage_dice_sides must be positive."
                    );
            }
            _damageEffectValidator.AppendStatusDamageFilterValidationErrors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
        else if (effectKind == BattleEffectKind.Shield)
        {
            bool hasFixedDiceKeys = _has_fixed_dice_fields(effectDef);
            bool hasValidFixedDiceConfig = _has_valid_fixed_dice_config(effectDef);
            bool hasValidDynamicDiceConfig = _has_valid_attribute_scaled_dice_config(effectDef);
            if (effectDef.power <= 0 && !hasValidFixedDiceConfig && !hasValidDynamicDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have power >= 1, a valid dice_count/dice_sides config, or a valid attribute-scaled dice config."
                );
            if (hasFixedDiceKeys && !hasValidFixedDiceConfig)
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must set dice_count and dice_sides >= 1 together."
                );
            if (
                effectDef.duration_tu <= 0
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} must have positive duration_tu in {SkillContentRegistry.TuGranularity} TU steps."
                );
        }
        else if (
            effectKind == BattleEffectKind.Heal
            || effectKind == BattleEffectKind.StaminaRestore
        )
        {
            bool hasFixedDiceKeys = _has_fixed_dice_fields(effectDef);
            if (hasFixedDiceKeys && !_has_valid_fixed_dice_config(effectDef))
                errors.Add(
                    $"Skill {skillId} {effectDef.effect_type} effect in {contextLabel} must set dice_count and dice_sides >= 1 together."
                );
            if (
                effectKind == BattleEffectKind.StaminaRestore
                && effectDef.power <= 0
                && !_has_valid_fixed_dice_config(effectDef)
                && !_has_valid_attribute_scaled_dice_config(effectDef)
            )
                errors.Add(
                    $"Skill {skillId} stamina_restore effect in {contextLabel} must have power >= 1, a valid dice_count/dice_sides config, or a valid attribute-scaled dice config."
                );
        }
        else if (effectKind == BattleEffectKind.TerrainEffect)
        {
            if (effectDef.terrain_effect_id == "")
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} is missing terrain_effect_id."
                );
            if (effectDef.LifetimePolicyKind == CombatEffectLifetimePolicy.Unknown)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} lifetime_policy must be battle or timed."
                );
            if (effectDef.move_cost_delta < 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} move_cost_delta must be >= 0."
                );
            if (effectDef.overlay_priority < 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} overlay_priority must be >= 0."
                );
            if (parameters.ContainsKey("render_overlay_id"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.render_overlay_id is unsupported; use CombatEffectDef.render_overlay_id."
                );
            if (parameters.ContainsKey("overlay_priority"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.overlay_priority is unsupported; use CombatEffectDef.overlay_priority."
                );
            if (parameters.ContainsKey("display_name"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.display_name is unsupported; use CombatEffectDef.display_name."
                );
            if (parameters.ContainsKey("does_not_stack_with_status_id"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.does_not_stack_with_status_id is unsupported; use CombatEffectDef.does_not_stack_with_status_id."
                );
            if (parameters.ContainsKey("does_not_stack_with_status_ids"))
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} params.does_not_stack_with_status_ids is unsupported; use CombatEffectDef.does_not_stack_with_status_ids."
                );
            AppendStringNameArrayValidationErrors(
                errors,
                skillId,
                $"terrain_effect in {contextLabel} does_not_stack_with_status_ids",
                effectDef.does_not_stack_with_status_ids
            );
            if (effectDef.duration_tu > 0 && effectDef.tick_interval_tu <= 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} must have positive tick_interval_tu in {SkillContentRegistry.TuGranularity} TU steps."
                );
            if (effectDef.TerrainTickEffectKind == BattleTerrainEffectRuntimeKind.Status)
            {
                if (effectDef.status_id == "")
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} with tick_effect_type=status is missing status_id."
                    );
                if (parameters.ContainsKey("status_id"))
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} params.status_id is unsupported; use CombatEffectDef.status_id."
                    );
                if (parameters.ContainsKey("duration_tu"))
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} params.duration_tu is unsupported; use CombatEffectDef.applied_status_duration_tu."
                    );
                if (!SkillContentRegistry.IsValidTuValue(effectDef.applied_status_duration_tu) || effectDef.applied_status_duration_tu <= 0)
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} with tick_effect_type=status must set positive applied_status_duration_tu in {SkillContentRegistry.TuGranularity} TU steps."
                    );
            }
        }
        else if (
            effectKind == BattleEffectKind.Terrain
            || effectKind == BattleEffectKind.TerrainReplace
            || effectKind == BattleEffectKind.TerrainReplaceTo
        )
        {
            if (effectDef.terrain_replace_to == "")
                errors.Add(
                    $"Skill {skillId} terrain_replace effect in {contextLabel} is missing terrain_replace_to."
                );
        }
        else if (
            effectKind == BattleEffectKind.Height
            || effectKind == BattleEffectKind.HeightDelta
        )
        {
            if (effectDef.height_delta == 0)
                errors.Add(
                    $"Skill {skillId} height effect in {contextLabel} must have non-zero height_delta."
                );
        }
        else if (effectKind == BattleEffectKind.BodySizeCategoryOverride)
        {
            if (effectDef.status_id == "")
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} is missing status_id."
                );
            if (effectDef.body_size_category == "")
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} is missing body_size_category."
                );
            else if (
                !BodySizeContentRules.IsValidBodySizeCategory(effectDef.body_size_category)
            )
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} uses unsupported body_size_category {effectDef.body_size_category}."
                );
            if (effectDef.duration_tu <= 0)
                errors.Add(
                    $"Skill {skillId} body_size_category_override effect in {contextLabel} must have positive duration_tu."
                );
        }
        else if (effectKind == BattleEffectKind.ForcedMove)
        {
            if (parameters.ContainsKey("mode"))
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} params.mode is unsupported; use forced_move_mode."
                );
            if (parameters.ContainsKey("distance"))
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} params.distance is unsupported; use forced_move_distance."
                );
            if (effectDef.forced_move_mode == "")
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} is missing forced_move_mode."
                );
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Unknown)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} uses unsupported forced_move_mode {effectDef.forced_move_mode}."
                );
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump)
                _damageEffectValidator.AppendJumpEffectValidationErrors(errors, skillId, effectDef, contextLabel);
            else if (effectDef.forced_move_distance <= 0)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} must have forced_move_distance >= 1."
                );
        }
        else if (effectKind == BattleEffectKind.Charge)
        {
            if (
                SkillContentRegistry.DictStringName(parameters, "skill_id").ToString().Length == 0
            )
                errors.Add(
                    $"Skill {skillId} charge effect in {contextLabel} is missing params.skill_id."
                );
        }
        else if (effectKind == BattleEffectKind.PathStepAoe)
        {
            _damageEffectValidator.AppendPathStepAoeValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectKind == BattleEffectKind.EquipmentDurabilityDamage)
        {
            _damageEffectValidator.AppendEquipmentDurabilityDamageValidationErrors(
                errors,
                skillId,
                effectDef,
                contextLabel
            );
        }
        else if (effectKind == BattleEffectKind.Execute)
        {
            _executeEffectValidator.AppendExecuteEffectValidationErrors(errors, skillId, effectDef, contextLabel);
        }
        else if (effectKind == BattleEffectKind.GradedSaveExecute)
        {
            _executeEffectValidator.AppendGradedSaveExecuteValidationErrors(errors, skillId, effectDef, contextLabel);
        }
    }

    internal void AppendPhantasmalKillLevelDescriptionValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillId != "mage_phantasmal_kill" || skillDef == null)
            return;

        var coveredLevels = new HashSet<int>();
        foreach (
            SkillDef.LevelDescriptionConfigEntryData entry in skillDef.LevelDescriptionConfigEntriesTyped
        )
        {
            if (entry.KeyIsStrictString && entry.HasParsedLevelKey && entry.ValueIsDictionary)
                coveredLevels.Add(entry.Level);
        }

        for (int level = 0; level <= 9; level++)
        {
            if (!coveredLevels.Contains(level))
                errors.Add(
                    $"Skill {skillId} level_description_configs must include level {level}."
                );
        }
    }

    internal void AppendPhantasmalKillCombatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef
    )
    {
        if (skillId != "mage_phantasmal_kill" || skillDef?.combat_profile == null)
            return;

        CombatSkillDef combatProfile = skillDef.combat_profile;
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_mode",
            combatProfile.target_mode,
            "ground"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_team_filter",
            combatProfile.target_team_filter,
            "any"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.target_selection_mode",
            combatProfile.target_selection_mode,
            "single_coord"
        );
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.area_pattern",
            combatProfile.area_pattern,
            "square"
        );
        SkillContentRegistry.RequireInt(errors, skillId, "combat_profile.area_value", combatProfile.area_value, 3);
        SkillContentRegistry.RequireStringName(
            errors,
            skillId,
            "combat_profile.special_resolution_profile_id",
            combatProfile.special_resolution_profile_id,
            ""
        );
    }

    private void AppendSaveValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null)
            return;
        int saveDc = effectDef.save_dc;
        BattleSaveDcMode saveDcMode = effectDef.SaveDcModeKind;
        bool dynamicSaveDc = saveDcMode == BattleSaveDcMode.CasterSpell;
        bool hasSaveDc = saveDc > 0 || dynamicSaveDc;
        var saveAbility = ProgressionDataUtils.to_string_name(effectDef.save_ability);
        var saveDcSourceAbility = ProgressionDataUtils.to_string_name(
            effectDef.save_dc_source_ability
        );
        var saveTag = ProgressionDataUtils.to_string_name(effectDef.save_tag);
        if (saveDcMode == BattleSaveDcMode.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_dc_mode {effectDef.save_dc_mode}."
            );
        if (saveDc < 0)
            errors.Add($"Skill {skillId} effect {contextLabel} save_dc must be >= 0.");
        if (dynamicSaveDc && saveDc > 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} caster_spell save_dc_mode must leave static save_dc at 0."
            );
        if (!dynamicSaveDc && saveDcSourceAbility != "")
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_dc_source_ability requires caster_spell save_dc_mode."
            );
        if (dynamicSaveDc && !BattleSaveContentRules.IsValidSaveAbility(saveDcSourceAbility))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_dc_source_ability {saveDcSourceAbility}."
            );

        if (!hasSaveDc)
        {
            if (saveAbility != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_ability requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (saveTag != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_tag requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (effectDef.save_failure_status_id != "")
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_failure_status_id requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            if (effectDef.save_partial_on_success)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_partial_on_success requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            return;
        }

        if (!BattleSaveContentRules.IsValidSaveAbility(saveAbility))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_ability {saveAbility}."
            );
        if (!BattleSaveContentRules.IsValidSaveTag(saveTag))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses unsupported save_tag {saveTag}."
            );
        BattleEffectKind effectKind = effectDef.EffectKind;
        if (effectDef.save_partial_on_success && effectKind != BattleEffectKind.Damage)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_partial_on_success is only supported on damage effects."
            );
        if (
            effectDef.save_failure_status_id != ""
            && effectKind != BattleEffectKind.Status
            && effectKind != BattleEffectKind.ApplyStatus
            && effectKind != BattleEffectKind.Damage
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_failure_status_id is only supported on status or damage effects."
            );
    }

    private void AppendTypedEffectParamValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        Dictionary parameters = effectDef.@params ?? new Dictionary();
        foreach (var migratedParam in TypedEffectParamTargets)
        {
            if (parameters.ContainsKey(migratedParam.Key))
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} params.{migratedParam.Key} is unsupported; use CombatEffectDef.{migratedParam.Value}."
                );
        }
    }

    private void AppendAttributeScaledDiceValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (effectDef == null || !_has_attribute_scaled_dice_fields(effectDef))
            return;
        if (effectDef.dice_count < 1)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} attribute-scaled dice must set dice_count >= 1."
            );
        if (effectDef.dice_sides_base < 1)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} attribute-scaled dice must set dice_sides_base >= 1."
            );
        if (effectDef.dice_sides_per_constitution_mod < 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} dice_sides_per_constitution_mod must be >= 0."
            );
        if (effectDef.dice_sides_per_willpower_mod < 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} dice_sides_per_willpower_mod must be >= 0."
            );
        if (effectDef.dice_sides > 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} cannot combine dice_sides with attribute-scaled dice_sides_base."
            );
        if (
            effectDef.power > 0
            && (
                effectDef.EffectKind == BattleEffectKind.Heal
                || effectDef.EffectKind == BattleEffectKind.Shield
                || effectDef.EffectKind == BattleEffectKind.StaminaRestore
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses attribute-scaled dice; put the dice count in dice_count, not power."
            );
    }

    private void AppendStringNameArrayValidationErrors(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        Array<StringName> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value == "")
                errors.Add($"Skill {skillId} {fieldLabel}[{index}] must be non-empty.");
        }
    }

    private static bool HasDamageEffect(CombatSkillDef combatProfile)
    {
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (effectDef?.EffectKind == BattleEffectKind.Damage)
                return true;
        }
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant == null)
                continue;
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                if (effectDef?.EffectKind == BattleEffectKind.Damage)
                    return true;
            }
        }
        return false;
    }

    private static bool HasAttackDamage(CombatSkillDef combatProfile)
    {
        foreach (CombatEffectDef effectDef in combatProfile.effect_defs)
        {
            if (IsAttackDamage(effectDef))
                return true;
        }
        foreach (CombatCastVariantDef castVariant in combatProfile.cast_variants)
        {
            if (castVariant == null)
                continue;
            foreach (CombatEffectDef effectDef in castVariant.effect_defs)
            {
                if (IsAttackDamage(effectDef))
                    return true;
            }
        }
        return false;
    }

    private static bool IsAttackDamage(CombatEffectDef effectDef)
    {
        return effectDef?.EffectKind == BattleEffectKind.Damage
            && effectDef.save_dc <= 0
            && effectDef.save_dc_mode == ""
            && effectDef.save_ability == "";
    }

    private bool IsValidPendingCastBindingMode(StringName value)
    {
        return value == BattleTypedNames.PendingCastBindingSoftAnchor
            || value == BattleTypedNames.PendingCastBindingHardAnchor
            || value == BattleTypedNames.PendingCastBindingGroundBind;
    }

    private static bool IsValidWeaponRangePolicy(StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized == "" || normalized == "current_weapon" || normalized == "configured";
    }

    private void AppendCastingTimeCompatibilityErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef,
        string contextLabel = "combat_profile"
    )
    {
        if (combatProfile.special_resolution_profile_id != "")
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with special_resolution_profile_id."
            );
        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.RandomChain)
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with random_chain target_selection_mode."
            );
        if (combatProfile.fumble_protection_curve != null && combatProfile.fumble_protection_curve.Length > 0)
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with fumble_protection_curve."
            );
        if (skillDef != null)
        {
            if (IsIdentityLearnSource(skillDef.learn_source))
                errors.Add(
                    $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with identity-granted learn_source {skillDef.learn_source}."
                );
            if (MisfortuneService.IsMisfortuneGatedSkill(skillDef.skill_id))
                errors.Add(
                    $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with misfortune-gated skills."
                );
        }
        if (skillId == "black_contract_push")
            errors.Add(
                $"Skill {skillId} {contextLabel} cannot combine casting_time_tu with black-contract-push variants."
            );
        AppendCastingTimeEffectCompatibilityErrors(
            errors,
            skillId,
            combatProfile.effect_defs,
            $"{contextLabel}.effect_defs"
        );
        for (int optionIndex = 0; optionIndex < combatProfile.cast_variants.Count; optionIndex++)
        {
            CombatCastVariantDef castVariant = combatProfile.cast_variants[optionIndex];
            AppendCastingTimeEffectCompatibilityErrors(
                errors,
                skillId,
                castVariant?.effect_defs,
                $"{contextLabel}.cast_variants[{optionIndex}].effect_defs"
            );
        }
    }

    private void AppendCastingTimeEffectCompatibilityErrors(
        Array<string> errors,
        StringName skillId,
        Array<CombatEffectDef> effectDefs,
        string contextLabel
    )
    {
        if (effectDefs == null)
        {
            return;
        }
        for (int effectIndex = 0; effectIndex < effectDefs.Count; effectIndex++)
        {
            CombatEffectDef effectDef = effectDefs[effectIndex];
            if (effectDef == null)
            {
                continue;
            }
            if (
                effectDef.EffectKind == BattleEffectKind.Charge
                || effectDef.EffectKind == BattleEffectKind.PathStepAoe
            )
            {
                errors.Add(
                    $"Skill {skillId} {contextLabel}[{effectIndex}] cannot use {effectDef.effect_type} with casting_time_tu."
                );
            }
            if (
                effectDef.EffectKind == BattleEffectKind.ForcedMove
                && (
                    effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Jump
                    || effectDef.ForcedMoveModeKind == BattleForcedMoveMode.Blink
                )
                && effectDef.effect_target_team_filter == "self"
            )
            {
                errors.Add(
                    $"Skill {skillId} {contextLabel}[{effectIndex}] cannot use self relocation with casting_time_tu."
                );
            }
        }
    }

    private static bool IsIdentityLearnSource(StringName learnSource)
    {
        return learnSource == "race"
            || learnSource == "subrace"
            || learnSource == "ascension"
            || learnSource == "bloodline";
    }

    private bool HasValidShieldDiceConfig(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return _has_valid_fixed_dice_config(effectDef)
            || _has_valid_attribute_scaled_dice_config(effectDef);
    }

    private static bool _has_fixed_dice_fields(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        bool hasAttributeScaledDice = _has_attribute_scaled_dice_fields(effectDef);
        return effectDef.dice_sides > 0
            || (effectDef.dice_bonus != 0 && !hasAttributeScaledDice)
            || (effectDef.dice_count > 0 && !hasAttributeScaledDice);
    }

    private static bool _has_valid_fixed_dice_config(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_count > 0 && effectDef.dice_sides > 0;
    }

    private static bool _has_attribute_scaled_dice_fields(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_sides_base > 0
            || effectDef.dice_sides_per_constitution_mod != 0
            || effectDef.dice_sides_per_willpower_mod != 0;
    }

    private static bool _has_valid_attribute_scaled_dice_config(CombatEffectDef effectDef)
    {
        if (effectDef == null)
            return false;
        return effectDef.dice_count > 0 && effectDef.dice_sides_base > 0;
    }
}
