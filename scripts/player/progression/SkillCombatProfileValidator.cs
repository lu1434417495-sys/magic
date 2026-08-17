using System;
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
            { "shield_family", "shield_family" },
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
            { "fixed_attack_count", "fixed_attack_count" },
            { "follow_up_damage_multiplier_percent", "follow_up_damage_multiplier_percent" },
            { "follow_up_attack_roll_bonus_curve", "follow_up_attack_roll_bonus_curve" },
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
            { "heal_to_hp_percent_floor", "heal_to_hp_percent_floor" },
            { "heal_missing_hp_percent", "heal_missing_hp_percent" },
            { "max_affected_targets", "max_affected_targets" },
            { "exclude_source", "exclude_source" },
            { "target_order", "target_order" },
            {
                "charge_trap_immunity_min_skill_level",
                "charge_trap_immunity_min_skill_level"
            },
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
        if (combatProfile.mastery_base_amount < 1)
            errors.Add(
                $"Skill {skillId} combat_profile mastery_base_amount must be >= 1."
            );
        if (combatProfile.range_value < 0)
            errors.Add($"Skill {skillId} combat_profile range_value must be >= 0.");
        if (combatProfile.range_move_point_capacity_multiplier < 0)
            errors.Add(
                $"Skill {skillId} combat_profile range_move_point_capacity_multiplier must be >= 0."
            );
        if (combatProfile.WeaponRangePolicyKind == CombatWeaponRangePolicy.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported weapon_range_policy {combatProfile.weapon_range_policy}; expected empty, current_weapon, configured, or current_weapon_plus_configured."
            );
        if (combatProfile.area_value < 0)
            errors.Add($"Skill {skillId} combat_profile area_value must be >= 0.");
        if (
            (combatProfile.ground_effect_require_full_area
                || combatProfile.ground_effect_require_empty
                || combatProfile.ground_effect_require_traversable)
            && combatProfile.TargetModeKind != BattleTargetMode.Ground
        )
            errors.Add(
                $"Skill {skillId} combat_profile ground effect placement requirements require target_mode=ground."
            );
        if (combatProfile.random_chain_attack_count < 0)
            errors.Add(
                $"Skill {skillId} combat_profile random_chain_attack_count must be >= 0."
            );
        if (
            combatProfile.random_chain_attack_count > 0
            && combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.RandomChain
        )
            errors.Add(
                $"Skill {skillId} combat_profile random_chain_attack_count requires target_selection_mode random_chain."
            );
        if (
            combatProfile.random_chain_continue_on_miss
            && combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.RandomChain
        )
            errors.Add(
                $"Skill {skillId} combat_profile random_chain_continue_on_miss requires target_selection_mode random_chain."
            );
        if (
            combatProfile.ap_cost < 0
            || combatProfile.mp_cost < 0
            || combatProfile.stamina_cost < 0
            || combatProfile.mp_cost_per_target_slot < 0
            || combatProfile.stamina_cost_per_target_slot < 0
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
        if (combatProfile.AttackDefenseModeKind == CombatSkillAttackDefenseMode.Unknown)
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported attack_defense_mode {combatProfile.attack_defense_mode}."
            );
        if (
            !CombatUnitTargetResolutionContentRules.IsValid(
                combatProfile.unit_target_resolution_mode
            )
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported unit_target_resolution_mode {combatProfile.unit_target_resolution_mode}; expected one of {CombatUnitTargetResolutionContentRules.ValidValueLabel()}."
            );
        }
        if (
            combatProfile.attack_roll_bonus_status_id == ""
            && combatProfile.attack_roll_bonus_status_stack_divisor != 0
        )
            errors.Add(
                $"Skill {skillId} combat_profile attack_roll_bonus_status_stack_divisor requires attack_roll_bonus_status_id."
            );
        if (
            combatProfile.attack_roll_bonus_status_id != ""
            && combatProfile.attack_roll_bonus_status_stack_divisor <= 0
        )
            errors.Add(
                $"Skill {skillId} combat_profile attack_roll_bonus_status_stack_divisor must be > 0 when attack_roll_bonus_status_id is set."
            );
        if (
            combatProfile.ProjectileKindTyped == CombatProjectileKind.Unknown
            || combatProfile.ProjectileKindTyped == CombatProjectileKind.Inherit
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile uses unsupported projectile_kind {combatProfile.projectile_kind}; expected {CombatProjectileContentRules.ValidBaseKindLabel()}."
            );
        }
        bool hasCastingTime = combatProfile.casting_time_tu > 0;
        if (hasCastingTime)
        {
            AppendCastingTimeCompatibilityErrors(errors, skillId, combatProfile, skillDef);
        }
        if (combatProfile.windup_profile != null)
        {
            CombatWindupDef windup = combatProfile.windup_profile;
            if (combatProfile.casting_time_tu != 0)
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile cannot be combined with casting_time_tu."
                );
            if (
                combatProfile.casting_maintenance_dc != 0
                || combatProfile.casting_spell_control_dc != 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile cannot use spell casting maintenance/control DCs."
                );
            if (combatProfile.cast_variants.Count > 0)
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile cannot be combined with cast_variants."
                );
            if (
                combatProfile.special_resolution_profile_id != ""
                || combatProfile.random_chain_attack_count > 0
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile cannot use special or random-chain resolution."
                );
            if (
                combatProfile.TargetModeKind != BattleTargetMode.Unit
                || combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.SingleUnit
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile requires one unit target."
                );
            if (
                skillDef?.contingency_automation_profile?.can_be_stored_in_contingency
                == true
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile cannot be stored in contingency."
                );
            if (windup.stamina_cost_per_tier < 0 || windup.weapon_dice_per_tier <= 0)
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile costs must be >= 0 and weapon_dice_per_tier must be > 0."
                );
            if (
                windup.skill_level_tier_caps == null
                || windup.skill_level_tier_caps.Length <= Math.Max(skillDef?.max_level ?? 0, 0)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile skill_level_tier_caps must cover levels 0 through max_level."
                );
            if (
                windup.base_weapon_dice_multipliers == null
                || windup.base_weapon_dice_multipliers.Length <= Math.Max(skillDef?.max_level ?? 0, 0)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile base_weapon_dice_multipliers must cover levels 0 through max_level."
                );
            if (
                windup.skill_level_tier_caps != null
                && System.Array.Exists(windup.skill_level_tier_caps, value => value < 0)
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile tier caps must be >= 0."
                );
            if (
                windup.base_weapon_dice_multipliers != null
                && System.Array.Exists(
                    windup.base_weapon_dice_multipliers,
                    value => value <= 0
                )
            )
                errors.Add(
                    $"Skill {skillId} combat_profile.windup_profile base weapon dice multipliers must be > 0."
                );
        }
        AppendDirectionalPiercingValidationErrors(errors, skillId, combatProfile, skillDef);
        AppendApproachAttackValidationErrors(errors, skillId, combatProfile, skillDef);
        CombatLineThroughAttackContentRules.AppendValidationErrors(
            errors,
            skillId,
            combatProfile,
            skillDef
        );
        CombatSequentialLineHitContentRules.AppendValidationErrors(
            errors,
            skillId,
            combatProfile,
            skillDef
        );
        AppendSpellReactionValidationErrors(errors, skillId, combatProfile, skillDef);
        AppendRangedWeaponReactionValidationErrors(
            errors,
            skillId,
            combatProfile,
            skillDef
        );
        _executeEffectValidator.AppendTemporalReleaseSkillValidationErrors(errors, skillId, combatProfile);

        AppendSpellFateValidationErrors(errors, skillId, combatProfile);
        AppendStringNameArrayValidationErrors(
            errors,
            skillId,
            "combat_profile.delivery_categories",
            combatProfile.delivery_categories
        );
        AppendProjectileCategoryOwnershipErrors(
            errors,
            skillId,
            "combat_profile.delivery_categories",
            combatProfile.delivery_categories
        );
        foreach (
            StringName requiredCategory in CombatEffectCategoryContentRules.RequiredDeliveryCategories(
                skillDef != null && skillDef.HasTag("mage") && skillDef.HasTag("magic"),
                skillDef != null && skillDef.HasTag("dragon_breath")
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
            "combat_profile.required_weapon_type_ids",
            combatProfile.required_weapon_type_ids
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
            foreach (
                string costKey in new[]
                {
                    "ap_cost",
                    "mp_cost",
                    "stamina_cost",
                    "mp_cost_per_target_slot",
                    "stamina_cost_per_target_slot",
                    "aura_cost",
                }
            )
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
                combatProfile.UnitTargetResolutionModeKind
                    != CombatUnitTargetResolutionMode.OrderedSlots
                && (
                    overrideDict.ContainsKey("mp_cost_per_target_slot")
                    || overrideDict.ContainsKey("stamina_cost_per_target_slot")
                )
            )
            {
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey} per-target-slot costs require unit_target_resolution_mode=ordered_slots."
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
            if (overrideDict.ContainsKey("attack_resolution_mode"))
            {
                var overrideAttackResolutionMode = ProgressionDataUtils.to_string_name(
                    overrideDict["attack_resolution_mode"]
                );
                if (
                    CombatSkillContentRules.ToAttackResolutionMode(
                        overrideAttackResolutionMode
                    ) == CombatSkillAttackResolutionMode.Unknown
                )
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.attack_resolution_mode uses unsupported value {overrideAttackResolutionMode}."
                    );
            }
            if (overrideDict.ContainsKey("attack_defense_mode"))
            {
                var overrideAttackDefenseMode = ProgressionDataUtils.to_string_name(
                    overrideDict["attack_defense_mode"]
                );
                if (
                    CombatSkillContentRules.ToAttackDefenseMode(
                        overrideAttackDefenseMode
                    ) == CombatSkillAttackDefenseMode.Unknown
                )
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.attack_defense_mode uses unsupported value {overrideAttackDefenseMode}."
                    );
            }
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
            if (
                SkillContentRegistry.TryReadLevelOverrideInt(
                    errors,
                    skillId,
                    overrideLevelKey,
                    overrideDict,
                    "random_chain_attack_count",
                    out int randomChainAttackCount
                )
                && randomChainAttackCount < 1
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.random_chain_attack_count must be >= 1."
                );
            if (
                overrideDict.ContainsKey("random_chain_attack_count")
                && combatProfile.TargetSelectionModeKind
                    != BattleTargetSelectionMode.RandomChain
            )
                errors.Add(
                    $"Skill {skillId} combat_profile level override {overrideLevelKey}.random_chain_attack_count requires target_selection_mode random_chain."
                );
            if (castingTimeTu > 0)
            {
                if (
                    combatProfile.UnitTargetResolutionModeKind
                    == CombatUnitTargetResolutionMode.OrderedSlots
                )
                {
                    errors.Add(
                        $"Skill {skillId} combat_profile level override {overrideLevelKey}.casting_time_tu is incompatible with ordered_slots."
                    );
                }
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
        if (
            combatProfile.UnitTargetResolutionModeKind
            == CombatUnitTargetResolutionMode.OrderedSlots
        )
        {
            if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
                errors.Add(
                    $"Skill {skillId} combat_profile ordered_slots requires target_mode=unit."
                );
            if (
                combatProfile.TargetSelectionModeKind
                != BattleTargetSelectionMode.MultiUnit
            )
            {
                errors.Add(
                    $"Skill {skillId} combat_profile ordered_slots requires target_selection_mode=multi_unit."
                );
            }
            if (!combatProfile.allow_repeat_target)
                errors.Add(
                    $"Skill {skillId} combat_profile ordered_slots requires allow_repeat_target=true."
                );
            if (
                combatProfile.SelectionOrderModeKind
                != BattleTargetSelectionOrderMode.Manual
            )
            {
                errors.Add(
                    $"Skill {skillId} combat_profile ordered_slots requires selection_order_mode=manual."
                );
            }
            if (combatProfile.casting_time_tu > 0)
                errors.Add(
                    $"Skill {skillId} combat_profile ordered_slots does not support casting_time_tu; ordered target slots must resolve in the issuing command."
                );
        }
        else if (
            combatProfile.mp_cost_per_target_slot > 0
            || combatProfile.stamina_cost_per_target_slot > 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile per-target-slot costs require unit_target_resolution_mode=ordered_slots."
            );
        }
        _executeEffectValidator.AppendExecuteCombatProfileValidationErrors(errors, skillId, skillDef, combatProfile);

        for (int effectIndex = 0; effectIndex < combatProfile.effect_defs.Count; effectIndex++)
            AppendEffectValidationErrors(
                errors,
                skillId,
                combatProfile.effect_defs[effectIndex],
                $"combat_profile.effect_defs[{effectIndex}]",
                skillDef
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
                    $"combat_profile.passive_effect_defs[{passiveIndex}]",
                    skillDef
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
            if (castVariant.ProjectileKindOverrideTyped == CombatProjectileKind.Unknown)
            {
                errors.Add(
                    $"Skill {skillId} cast option {castVariant.variant_id} uses unsupported projectile_kind_override {castVariant.projectile_kind_override}; expected {CombatProjectileContentRules.ValidOverrideKindLabel()}."
                );
            }

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
                    $"combat_profile.cast_variants[{optionIndex}].effect_defs[{effectIndex}]",
                    skillDef
                );
        }

        AppendSourceRetreatProfileValidationErrors(
            errors,
            skillId,
            skillDef,
            combatProfile
        );
        AppendAirbornePullProfileValidationErrors(
            errors,
            skillId,
            skillDef,
            combatProfile
        );
    }

    private static void AppendDirectionalPiercingValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatDirectionalPiercingDef profile = combatProfile.directional_piercing_profile;
        if (profile == null)
            return;

        if (
            combatProfile.TargetModeKind != BattleTargetMode.Ground
            || combatProfile.target_selection_mode != new StringName("single_unit")
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile requires ground targeting with one direction coord."
            );
        }
        if (combatProfile.target_team_filter != new StringName("any"))
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile requires target_team_filter any for friendly fire."
            );
        if (
            combatProfile.cast_variants.Count > 0
            || combatProfile.casting_time_tu != 0
            || combatProfile.windup_profile != null
            || combatProfile.random_chain_attack_count > 0
            || combatProfile.special_resolution_profile_id != ""
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile cannot combine with cast variants, delayed casting, windup, random chain, or special profiles."
            );
        }
        if (
            skillDef?.contingency_automation_profile?.can_be_stored_in_contingency
            == true
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile cannot be stored in contingency."
            );
        }
        if (
            combatProfile.required_weapon_families.Count != 1
            || combatProfile.required_weapon_families[0] != new StringName("bow")
            || combatProfile.allows_natural_weapon
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile requires exactly the bow weapon family and cannot allow natural weapons."
            );
        }
        if (
            combatProfile.ProjectileKindTyped != CombatProjectileKind.CurrentWeapon
            || !combatProfile.requires_los
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile requires current_weapon projectile delivery and LOS."
            );
        }
        int requiredCurveLength = Math.Max(skillDef?.max_level ?? 0, 0) + 1;
        if (
            profile.base_damage_percent_curve == null
            || profile.base_damage_percent_curve.Length < requiredCurveLength
            || System.Array.Exists(profile.base_damage_percent_curve, value => value <= 0)
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile base_damage_percent_curve must cover levels 0 through max_level with positive values."
            );
        }
        if (profile.successful_hit_decay_percent <= 0 || profile.successful_hit_decay_percent > 100)
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile successful_hit_decay_percent must be within 1..100."
            );
        if (profile.minimum_damage_percent <= 0 || profile.minimum_damage_percent > 100)
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile minimum_damage_percent must be within 1..100."
            );
        if (
            profile.stamina_flat_base < 0
            || profile.stamina_range_square_coefficient < 0
            || profile.stamina_strength_square_scale <= 0
            || profile.minimum_stamina_cost <= 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile stamina parameters must be non-negative with positive scale and minimum cost."
            );
        }
        if (profile.maximum_height_delta < 0)
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile maximum_height_delta must be >= 0."
            );
        if (combatProfile.effect_defs.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile requires exactly one standard weapon damage effect."
            );
            return;
        }
        CombatEffectDef damage = combatProfile.effect_defs[0];
        if (
            damage == null
            || damage.EffectKind != BattleEffectKind.Damage
            || !damage.add_weapon_dice
            || !damage.requires_weapon
            || !damage.use_weapon_physical_damage_tag
            || !damage.resolve_as_weapon_attack
            || damage.power != 0
            || damage.dice_count != 0
            || damage.dice_sides != 0
            || damage.dice_bonus != 0
            || damage.bonus_damage_dice_count != 0
            || damage.bonus_damage_dice_sides != 0
            || damage.bonus_damage_dice_bonus != 0
            || damage.dr_bypass_tag != ""
            || damage.mitigation_bypass_damage_tags.Count != 0
            || damage.mitigation_bypass_tiers.Count != 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.directional_piercing_profile damage must be one ordinary current-weapon attack without fixed damage or mitigation bypass."
            );
        }
    }

    private static void AppendApproachAttackValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatApproachAttackDef profile = combatProfile.approach_attack_profile;
        if (profile == null)
            return;

        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.target_selection_mode != new StringName("single_unit")
            || combatProfile.min_target_count != 1
            || combatProfile.max_target_count != 1
            || combatProfile.allow_repeat_target
            || combatProfile.max_hits_per_target != 1
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile requires exactly one enemy unit target."
            );
        }
        if (
            combatProfile.WeaponRangePolicyKind
                != CombatWeaponRangePolicy.CurrentWeaponPlusConfigured
            || !combatProfile.requires_los
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile requires current_weapon_plus_configured range and LOS."
            );
        }
        int maxLevel = Math.Max(skillDef?.max_level ?? 0, 0);
        for (int level = 0; level <= maxLevel; level++)
        {
            if (combatProfile.GetEffectiveRangeValue(level) > 0)
                continue;
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile requires a positive configured approach distance at every level."
            );
            break;
        }
        if (
            combatProfile.required_weapon_families.Count != 0
            || combatProfile.required_weapon_type_ids.Count == 0
            || combatProfile.allows_natural_weapon
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile requires explicit equipped weapon type ids and cannot allow natural weapons."
            );
        }
        if (
            combatProfile.cast_variants.Count > 0
            || combatProfile.passive_effect_defs.Count > 0
            || combatProfile.casting_time_tu != 0
            || combatProfile.windup_profile != null
            || combatProfile.directional_piercing_profile != null
            || combatProfile.spell_reaction_profile != null
            || combatProfile.random_chain_attack_count > 0
            || combatProfile.special_resolution_profile_id != ""
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile cannot combine with passive effects, cast variants, delayed casting, windup, directional piercing, spell reactions, random chain, or special profiles."
            );
        }
        if (
            skillDef?.contingency_automation_profile?.can_be_stored_in_contingency
            == true
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile cannot be stored in contingency."
            );
        }
        if (profile.maximum_path_height_delta_from_origin < 0)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile maximum_path_height_delta_from_origin must be >= 0."
            );
        }
        if (combatProfile.effect_defs.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile requires exactly one standard weapon damage effect."
            );
            return;
        }

        CombatEffectDef damage = combatProfile.effect_defs[0];
        if (
            damage == null
            || damage.EffectKind != BattleEffectKind.Damage
            || !damage.add_weapon_dice
            || !damage.requires_weapon
            || !damage.use_weapon_physical_damage_tag
            || !damage.resolve_as_weapon_attack
            || damage.power != 0
            || damage.dice_count != 0
            || damage.dice_sides != 0
            || damage.dice_bonus != 0
            || damage.bonus_damage_dice_count != 0
            || damage.bonus_damage_dice_sides != 0
            || damage.bonus_damage_dice_bonus != 0
            || damage.dr_bypass_tag != ""
            || damage.mitigation_bypass_damage_tags.Count != 0
            || damage.mitigation_bypass_tiers.Count != 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.approach_attack_profile damage must be one ordinary current-weapon attack without fixed damage or mitigation bypass."
            );
        }
    }

    private static void AppendSpellReactionValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatSpellReactionDef reaction = combatProfile?.spell_reaction_profile;
        if (reaction == null)
            return;
        if (reaction.trigger_delivery_category == "")
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile requires trigger_delivery_category."
            );
        if (reaction.reaction_skill_id == "")
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile requires reaction_skill_id."
            );
        if (reaction.readiness_status_id == "")
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile requires readiness_status_id."
            );
        else if (!HasStatusEffect(combatProfile.effect_defs, reaction.readiness_status_id))
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile readiness_status_id must match a status effect in combat_profile.effect_defs."
            );
        if (reaction.required_weapon_family == "")
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile requires required_weapon_family."
            );
        else if (!combatProfile.required_weapon_families.Contains(reaction.required_weapon_family))
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile required_weapon_family must also be present in combat_profile.required_weapon_families."
            );
        if (!BattleSaveContentRules.IsValidSaveAbility(reaction.save_ability))
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile uses unsupported save_ability {reaction.save_ability}."
            );
        if (reaction.save_tag == "")
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile requires save_tag."
            );
        if (reaction.base_save_dc <= 0 || reaction.hp_damage_divisor <= 0)
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile base_save_dc and hp_damage_divisor must be > 0."
            );
        int requiredCurveLength = Math.Max(skillDef?.max_level ?? 0, 0) + 1;
        if (
            reaction.attack_roll_bonus_by_skill_level == null
            || reaction.attack_roll_bonus_by_skill_level.Length < requiredCurveLength
        )
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile attack roll curve must cover levels 0 through max_level."
            );
        if (
            reaction.save_dc_bonus_by_skill_level == null
            || reaction.save_dc_bonus_by_skill_level.Length < requiredCurveLength
        )
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile save DC curve must cover levels 0 through max_level."
            );
        if (
            reaction.attack_roll_bonus_by_skill_level != null
            && System.Array.Exists(reaction.attack_roll_bonus_by_skill_level, value => value < 0)
        )
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile attack roll bonuses must be >= 0."
            );
        if (
            reaction.save_dc_bonus_by_skill_level != null
            && System.Array.Exists(reaction.save_dc_bonus_by_skill_level, value => value < 0)
        )
            errors.Add(
                $"Skill {skillId} combat_profile.spell_reaction_profile save DC bonuses must be >= 0."
            );
    }

    private static void AppendRangedWeaponReactionValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatRangedWeaponReactionDef reaction =
            combatProfile?.ranged_weapon_reaction_profile;
        if (reaction == null)
            return;

        const string profilePath = "combat_profile.ranged_weapon_reaction_profile";
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Self
            || combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.Self
            || combatProfile.range_value != 0
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires a range-0 self unit target."
            );
        }
        if (
            combatProfile.AttackResolutionModeKind
            != CombatSkillAttackResolutionMode.DirectEffect
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires attack_resolution_mode=direct_effect on the arming cast."
            );
        }
        if (
            combatProfile.spell_reaction_profile != null
            || combatProfile.windup_profile != null
            || combatProfile.directional_piercing_profile != null
            || combatProfile.approach_attack_profile != null
            || combatProfile.line_through_attack_profile != null
            || combatProfile.sequential_line_hit_profile != null
            || combatProfile.special_resolution_profile_id != ""
            || combatProfile.random_chain_attack_count > 0
            || combatProfile.cast_variants.Count > 0
            || combatProfile.passive_effect_defs.Count > 0
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot combine with another special resolution profile, cast variants, or passive effects."
            );
        }
        if (reaction.readiness_status_id == "")
            errors.Add($"Skill {skillId} {profilePath} requires readiness_status_id.");
        if (
            reaction.trigger_weapon_families == null
            || reaction.trigger_weapon_families.Count == 0
        )
        {
            errors.Add($"Skill {skillId} {profilePath} requires trigger_weapon_families.");
        }
        else
        {
            var seenFamilies = new HashSet<StringName>();
            foreach (StringName family in reaction.trigger_weapon_families)
            {
                if (family == "" || !seenFamilies.Add(family))
                {
                    errors.Add(
                        $"Skill {skillId} {profilePath} trigger_weapon_families must be non-empty and unique."
                    );
                    break;
                }
            }
        }
        if (DamageTagContentRules.ToDamageTagKind(reaction.damage_tag) == DamageTagKind.Unknown)
        {
            errors.Add(
                $"Skill {skillId} {profilePath} uses unsupported damage_tag {reaction.damage_tag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
            );
        }
        if (
            CombatSkillContentRules.ToAttackDefenseMode(reaction.attack_defense_mode)
            == CombatSkillAttackDefenseMode.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} uses unsupported attack_defense_mode {reaction.attack_defense_mode}."
            );
        }
        else if (
            CombatSkillContentRules.ToAttackDefenseMode(reaction.attack_defense_mode)
            != combatProfile.AttackDefenseModeKind
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} attack_defense_mode must match combat_profile.attack_defense_mode."
            );
        }
        if (!reaction.trigger_on_hit && !reaction.trigger_on_miss)
            errors.Add($"Skill {skillId} {profilePath} must trigger on hit, miss, or both.");
        if (reaction.consume_status_stacks <= 0)
            errors.Add($"Skill {skillId} {profilePath} consume_status_stacks must be > 0.");

        int maxLevel = Math.Max(skillDef?.max_level ?? 0, 0);
        int requiredCurveLength = maxLevel + 1;
        if (
            reaction.attack_roll_bonus_by_skill_level == null
            || reaction.attack_roll_bonus_by_skill_level.Length < requiredCurveLength
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} attack roll curve must cover levels 0 through max_level."
            );
        }
        else if (
            System.Array.Exists(
                reaction.attack_roll_bonus_by_skill_level,
                value => value < 0
            )
        )
        {
            errors.Add($"Skill {skillId} {profilePath} attack roll bonuses must be >= 0.");
        }

        for (int level = 0; level <= maxLevel; level++)
        {
            int activeReadinessCount = 0;
            foreach (CombatEffectDef effect in combatProfile.effect_defs)
            {
                bool active = effect != null
                    && level >= Math.Max(effect.min_skill_level, 0)
                    && (effect.max_skill_level < 0 || level <= effect.max_skill_level);
                if (!active)
                    continue;
                if (
                    effect.EffectKind != BattleEffectKind.Status
                    || effect.status_id != reaction.readiness_status_id
                )
                {
                    errors.Add(
                        $"Skill {skillId} {profilePath} may only arm its readiness status at level {level}."
                    );
                    continue;
                }
                activeReadinessCount++;
                if (
                    effect.stack_behavior != new StringName("add")
                    || effect.power < reaction.consume_status_stacks
                    || effect.stack_limit < effect.power
                    || effect.duration_tu <= 0
                )
                {
                    errors.Add(
                        $"Skill {skillId} {profilePath} readiness status at level {level} requires add stacking, positive consumable power, stack_limit >= power, and duration_tu > 0."
                    );
                }
            }
            if (activeReadinessCount != 1)
            {
                errors.Add(
                    $"Skill {skillId} {profilePath} must resolve to exactly one readiness status at level {level}; found {activeReadinessCount}."
                );
            }
        }
    }

    private static void AppendSourceRetreatProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;

        int baseEffectCount = CountEffectsOfKind(
            combatProfile.effect_defs,
            BattleEffectKind.SourceRetreat
        );
        int passiveEffectCount = CountEffectsOfKind(
            combatProfile.passive_effect_defs,
            BattleEffectKind.SourceRetreat
        );
        int variantEffectCount = 0;
        foreach (
            CombatCastVariantDef castVariant
            in combatProfile.cast_variants ?? new Godot.Collections.Array<CombatCastVariantDef>()
        )
        {
            variantEffectCount += CountEffectsOfKind(
                castVariant?.effect_defs,
                BattleEffectKind.SourceRetreat
            );
        }
        int totalEffectCount = baseEffectCount + passiveEffectCount + variantEffectCount;
        if (totalEffectCount == 0)
            return;

        if (baseEffectCount != 1 || totalEffectCount != 1)
            errors.Add(
                $"Skill {skillId} source_retreat must appear exactly once in combat_profile.effect_defs."
            );
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetSelectionModeKind
                != BattleTargetSelectionMode.SingleUnit
            || combatProfile.min_target_count != 1
            || combatProfile.max_target_count != 1
        )
            errors.Add(
                $"Skill {skillId} source_retreat requires one single_unit unit target."
            );
        if (combatProfile.casting_time_tu != 0 || combatProfile.windup_profile != null)
            errors.Add(
                $"Skill {skillId} source_retreat cannot be combined with casting_time_tu or windup_profile because the chosen direction must execute immediately."
            );
        if (combatProfile.cast_variants.Count > 0)
            errors.Add(
                $"Skill {skillId} source_retreat cannot be placed behind cast_variants."
            );
        if (
            combatProfile.special_resolution_profile_id != ""
            || combatProfile.random_chain_attack_count > 0
        )
            errors.Add(
                $"Skill {skillId} source_retreat cannot use special or random-chain resolution."
            );
        if (
            skillDef?.contingency_automation_profile?.can_be_stored_in_contingency
            == true
        )
            errors.Add(
                $"Skill {skillId} source_retreat cannot be stored in contingency because automatic execution has no selected direction."
            );
    }

    private static int CountEffectsOfKind(
        Godot.Collections.Array<CombatEffectDef> effects,
        BattleEffectKind expectedKind
    )
    {
        int count = 0;
        foreach (
            CombatEffectDef effect
            in effects ?? new Godot.Collections.Array<CombatEffectDef>()
        )
        {
            if (effect?.EffectKind == expectedKind)
                count++;
        }
        return count;
    }

    private static void AppendAirbornePullProfileValidationErrors(
        Array<string> errors,
        StringName skillId,
        SkillDef skillDef,
        CombatSkillDef combatProfile
    )
    {
        if (combatProfile == null)
            return;
        var airborneEffects = new List<CombatEffectDef>();
        foreach (CombatEffectDef effect in combatProfile.effect_defs)
        {
            if (IsAirbornePullEffect(effect))
                airborneEffects.Add(effect);
        }
        bool misplaced = false;
        foreach (CombatEffectDef effect in combatProfile.passive_effect_defs)
            misplaced |= IsAirbornePullEffect(effect);
        foreach (CombatCastVariantDef variant in combatProfile.cast_variants)
        {
            foreach (CombatEffectDef effect in variant?.effect_defs ?? new())
                misplaced |= IsAirbornePullEffect(effect);
        }
        if (airborneEffects.Count == 0 && !misplaced)
            return;
        if (airborneEffects.Count == 0 || misplaced)
        {
            errors.Add(
                $"Skill {skillId} airborne_pull effects must be authored only in combat_profile.effect_defs."
            );
        }
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.TargetSelectionModeKind
                != BattleTargetSelectionMode.SingleUnit
            || combatProfile.min_target_count != 1
            || combatProfile.max_target_count != 1
            || combatProfile.allow_repeat_target
        )
        {
            errors.Add(
                $"Skill {skillId} airborne_pull requires exactly one enemy unit target."
            );
        }
        if (
            combatProfile.AttackResolutionModeKind
            != CombatSkillAttackResolutionMode.DirectEffect
        )
        {
            errors.Add(
                $"Skill {skillId} airborne_pull must use attack_resolution_mode = direct_effect because it does not roll against AC."
            );
        }
        if (
            combatProfile.casting_time_tu != 0
            || combatProfile.windup_profile != null
            || combatProfile.cast_variants.Count > 0
            || combatProfile.special_resolution_profile_id != ""
            || combatProfile.random_chain_attack_count > 0
        )
        {
            errors.Add(
                $"Skill {skillId} airborne_pull cannot use delayed casting, windup, cast variants, special resolution, or random-chain resolution because it requires an explicit destination."
            );
        }
        if (
            skillDef?.contingency_automation_profile?.can_be_stored_in_contingency
            == true
        )
        {
            errors.Add(
                $"Skill {skillId} airborne_pull cannot be stored in contingency because automatic execution has no selected destination."
            );
        }
        int maxLevel = Math.Max(skillDef?.max_level ?? 0, 0);
        for (int level = 0; level <= maxLevel; level++)
        {
            int activeCount = 0;
            foreach (CombatEffectDef effect in airborneEffects)
            {
                if (
                    level >= Math.Max(effect.min_skill_level, 0)
                    && (effect.max_skill_level < 0 || level <= effect.max_skill_level)
                )
                {
                    activeCount++;
                }
            }
            if (activeCount == 1)
                continue;
            errors.Add(
                $"Skill {skillId} airborne_pull must resolve to exactly one active forced_move effect at level {level}; found {activeCount}."
            );
        }
        foreach (CombatEffectDef effect in combatProfile.effect_defs)
        {
            if (
                !IsAirbornePullEffect(effect)
                && effect?.TriggerEventKind
                    != CombatEffectTriggerEvent.ForcedMoveApplied
            )
            {
                errors.Add(
                    $"Skill {skillId} airborne_pull is pure control and cannot include independently resolved {effect?.effect_type} effects."
                );
            }
            if (
                effect?.TriggerEventKind == CombatEffectTriggerEvent.ForcedMoveApplied
                && effect.EffectKind
                    is not BattleEffectKind.Status
                    and not BattleEffectKind.ApplyStatus
                    and not BattleEffectKind.EraseStatus
            )
            {
                errors.Add(
                    $"Skill {skillId} forced_move_applied currently supports only status, apply_status, or erase_status follow-up effects."
                );
            }
        }
    }

    private static bool IsAirbornePullEffect(CombatEffectDef effect) =>
        effect?.EffectKind == BattleEffectKind.ForcedMove
        && effect.ForcedMoveModeKind == BattleForcedMoveMode.AirbornePull;

    private static bool HasStatusEffect(
        Godot.Collections.Array<CombatEffectDef> effects,
        StringName statusId
    )
    {
        foreach (
            CombatEffectDef effect
            in effects ?? new Godot.Collections.Array<CombatEffectDef>()
        )
        {
            if (
                effect != null
                && (
                    effect.EffectKind == BattleEffectKind.Status
                    || effect.EffectKind == BattleEffectKind.ApplyStatus
                )
                && effect.status_id == statusId
            )
            {
                return true;
            }
        }
        return false;
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
        string contextLabel,
        SkillDef skillDef = null
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
        if (
            effectDef.TriggerEventKind == CombatEffectTriggerEvent.ForcedMoveApplied
            && effectKind
                is not BattleEffectKind.Status
                and not BattleEffectKind.ApplyStatus
                and not BattleEffectKind.EraseStatus
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} uses forced_move_applied on an unsupported effect type."
            );
        }
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
        if (effectDef.max_affected_targets < 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} max_affected_targets must be >= 0."
            );
        if (effectDef.TargetOrderKind == CombatEffectTargetOrder.Unknown)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} target_order must be empty or lowest_hp_percent_then_unit_id."
            );
        if (
            effectDef.max_affected_targets > 0
            && effectDef.TargetOrderKind
                != CombatEffectTargetOrder.LowestHpPercentThenUnitId
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} max_affected_targets requires target_order lowest_hp_percent_then_unit_id."
            );
        if (effectDef.target_order != "" && effectDef.max_affected_targets <= 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} target_order requires max_affected_targets >= 1."
            );
        bool hasTargetLimiter =
            effectDef.max_affected_targets != 0
            || effectDef.exclude_source
            || effectDef.target_order != "";
        if (hasTargetLimiter && !BattleTypedNames.IsUnitPayloadEffect(effectKind))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} target limiting is only supported for unit payload effects."
            );
        if (
            hasTargetLimiter
            && (
                effectKind == BattleEffectKind.ChainDamage
                || effectKind == BattleEffectKind.SourceRetreat
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} target limiting is not supported by specialized chain_damage or source_retreat resolution."
            );
        if (
            hasTargetLimiter
            && skillDef?.combat_profile?.TargetSelectionModeKind
                == BattleTargetSelectionMode.RandomChain
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} target limiting is not supported by random_chain target selection."
            );
        if (effectDef.heal_to_hp_percent_floor < 0 || effectDef.heal_to_hp_percent_floor > 100)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} heal_to_hp_percent_floor must be between 0 and 100."
            );
        if (
            effectDef.heal_to_hp_percent_floor != 0
            && effectKind != BattleEffectKind.Heal
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} heal_to_hp_percent_floor is only supported on heal effects."
            );
        if (effectDef.heal_missing_hp_percent < 0 || effectDef.heal_missing_hp_percent > 100)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} heal_missing_hp_percent must be between 0 and 100."
            );
        if (
            effectDef.heal_missing_hp_percent != 0
            && effectKind != BattleEffectKind.Heal
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} heal_missing_hp_percent is only supported on heal effects."
            );
        if (
            effectDef.required_target_creature_type_tag != ""
            && string.IsNullOrWhiteSpace(effectDef.required_target_creature_type_tag.ToString())
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} required_target_creature_type_tag must not be whitespace."
            );
        if (
            effectDef.required_target_min_cognition != ""
            && !BattleCognitionContentRules.IsKnown(
                BattleCognitionContentRules.ToKind(
                    effectDef.required_target_min_cognition
                )
            )
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} required_target_min_cognition "
                + $"must be one of {BattleCognitionContentRules.ValidValueLabel()}; "
                + $"got {effectDef.required_target_min_cognition}."
            );
        }
        if (!SkillContentRegistry.IsValidTuValue(effectDef.duration_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} duration_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );
        if (!SkillContentRegistry.IsValidTuValue(effectDef.tick_interval_tu))
            errors.Add(
                $"Skill {skillId} effect {contextLabel} tick_interval_tu must be 0 or a multiple of {SkillContentRegistry.TuGranularity}."
            );

        if (
            effectDef.EffectKind != BattleEffectKind.Shield
            && (
                effectDef.shield_family != ""
                || effectDef.shield_attribute_modifier_id != ""
                || effectDef.shield_roll_per_target
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} shield fields are only supported on shield effects."
            );
        AppendSaveValidationErrors(errors, skillId, effectDef, contextLabel);
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Skill {skillId} effect {contextLabel}.save_advantage_tags",
            effectDef.save_advantage_tags
        );
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Skill {skillId} effect {contextLabel}.save_disadvantage_tags",
            effectDef.save_disadvantage_tags
        );
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Skill {skillId} effect {contextLabel}.save_immunity_tags",
            effectDef.save_immunity_tags
        );

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
        AppendProjectileCategoryOwnershipErrors(
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
        if (
            effectKind == BattleEffectKind.FixedRepeatAttack
            && effectDef.fixed_attack_count < 2
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} fixed_repeat_attack must set fixed_attack_count >= 2."
            );
        if (
            effectKind != BattleEffectKind.FixedRepeatAttack
            && effectDef.fixed_attack_count != 0
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} fixed_attack_count is only supported on fixed_repeat_attack."
            );
        bool isRepeatAttack =
            effectKind == BattleEffectKind.FixedRepeatAttack
            || effectKind == BattleEffectKind.RepeatAttackUntilFail;
        if (!isRepeatAttack && effectDef.follow_up_damage_multiplier_percent != 100)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} follow_up_damage_multiplier_percent is only supported on repeat attacks."
            );
        if (isRepeatAttack && effectDef.follow_up_damage_multiplier_percent <= 0)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} follow_up_damage_multiplier_percent must be > 0."
            );
        if (
            !isRepeatAttack
            && effectDef.follow_up_attack_roll_bonus_curve != null
            && effectDef.follow_up_attack_roll_bonus_curve.Length > 0
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} follow_up_attack_roll_bonus_curve is only supported on repeat attacks."
            );
        if (
            effectDef.follow_up_attack_roll_bonus_curve != null
            && System.Array.Exists(
                effectDef.follow_up_attack_roll_bonus_curve,
                value => value < 0
            )
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} follow_up_attack_roll_bonus_curve values must be >= 0."
            );
        if (
            effectKind != BattleEffectKind.SourceRetreat
            && effectDef.source_retreat_distance != 0
        )
            errors.Add(
                $"Skill {skillId} effect {contextLabel} source_retreat_distance is only supported on source_retreat."
            );
        if (
            effectKind != BattleEffectKind.ForcedMove
            && effectDef.forced_move_max_target_body_size != 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} forced_move_max_target_body_size is only supported on forced_move."
            );
        }

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
            StringName comboAttackStatusId = ProgressionDataUtils.to_string_name(
                effectDef.combo_attack_bonus_status_id
            );
            if (
                (comboAttackStatusId == "")
                != (effectDef.combo_attack_bonus_stack_divisor <= 0)
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} combo_attack_bonus_status_id and positive combo_attack_bonus_stack_divisor must be configured together."
                );
            }
            StringName upkeepResource = ProgressionDataUtils.to_string_name(
                effectDef.upkeep_resource
            );
            bool hasUpkeep = upkeepResource != "";
            if (
                hasUpkeep
                && CombatResourceKindUtils.FromStringName(upkeepResource)
                    == CombatResourceKind.None
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_resource is unsupported."
                );
            }
            if (hasUpkeep && effectDef.upkeep_interval_tu <= 0)
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_interval_tu must be positive."
                );
            }
            if (
                effectDef.upkeep_interval_tu > 0
                && effectDef.upkeep_interval_tu % SkillContentRegistry.TuGranularity != 0
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_interval_tu must be a multiple of {SkillContentRegistry.TuGranularity}."
                );
            }
            if (hasUpkeep && effectDef.upkeep_base_cost <= 0)
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_base_cost must be positive."
                );
            }
            if (
                hasUpkeep
                && (
                    effectDef.upkeep_escalation_interval_tu <= 0
                    || effectDef.upkeep_escalation_interval_tu
                        % System.Math.Max(effectDef.upkeep_interval_tu, 1) != 0
                )
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_escalation_interval_tu must be a positive multiple of upkeep_interval_tu."
                );
            }
            if (hasUpkeep && effectDef.upkeep_cost_multiplier < 2)
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} upkeep_cost_multiplier must be >= 2."
                );
            }
            if (
                (hasUpkeep || effectDef.break_on_hard_control)
                && (
                    ProgressionDataUtils.to_string_name(effectDef.termination_status_id) == ""
                    || effectDef.termination_status_duration_tu <= 0
                    || effectDef.termination_attack_roll_penalty <= 0
                    || effectDef.termination_cooldown_tu <= 0
                )
            )
            {
                errors.Add(
                    $"Skill {skillId} status effect in {contextLabel} maintained status termination must configure status id, duration, attack penalty, and cooldown."
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
            AttributeSnapshotIdKind shieldModifierKind =
                effectDef.ShieldAttributeModifierKind;
            if (
                effectDef.shield_family != ""
                && string.IsNullOrWhiteSpace(effectDef.shield_family.ToString())
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} shield_family must not be whitespace."
                );
            if (
                effectDef.shield_attribute_modifier_id != ""
                && !AttributeSnapshot.IsAbilityModifierKind(shieldModifierKind)
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} shield_attribute_modifier_id must name a base ability modifier."
                );
            if (
                effectDef.shield_roll_per_target
                && !hasValidFixedDiceConfig
                && !hasValidDynamicDiceConfig
            )
                errors.Add(
                    $"Skill {skillId} shield effect in {contextLabel} shield_roll_per_target requires a valid dice config."
                );
        }
        else if (
            effectKind == BattleEffectKind.Heal
            || effectKind == BattleEffectKind.StaminaRestore
        )
        {
            bool hasFixedDiceKeys = _has_fixed_dice_fields(effectDef);
            bool hasHpPercentFloor =
                effectKind == BattleEffectKind.Heal
                && effectDef.heal_to_hp_percent_floor > 0;
            bool hasMissingHpPercent =
                effectKind == BattleEffectKind.Heal
                && effectDef.heal_missing_hp_percent > 0;
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
            if (
                (hasHpPercentFloor || hasMissingHpPercent)
                && (
                    effectDef.power != 0
                    || hasFixedDiceKeys
                    || _has_attribute_scaled_dice_fields(effectDef)
                )
            )
                errors.Add(
                    $"Skill {skillId} heal effect in {contextLabel} percentage-based healing cannot be combined with power or dice healing."
                );
            if (hasHpPercentFloor && hasMissingHpPercent)
                errors.Add(
                    $"Skill {skillId} heal effect in {contextLabel} heal_to_hp_percent_floor and heal_missing_hp_percent are mutually exclusive."
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
            if (effectDef.TerrainContactModeKind == CombatTerrainContactMode.Unknown)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} uses unsupported terrain_contact_mode {effectDef.terrain_contact_mode}."
                );
            if (
                effectDef.TerrainContactModeKind
                == CombatTerrainContactMode.InterruptMovementOnFailedSave
            )
            {
                if (effectDef.save_dc <= 0 || effectDef.save_ability == "")
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} movement interruption requires positive save_dc and save_ability."
                    );
                if (effectDef.terrain_effective_trigger_count <= 0)
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} movement interruption requires terrain_effective_trigger_count >= 1."
                    );
                if (!effectDef.terrain_recheck_from_inside)
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} movement interruption must enable terrain_recheck_from_inside."
                    );
                if (
                    effectDef.status_id != ""
                    || effectDef.save_failure_status_id != ""
                    || effectDef.save_failure_status_outcomes.Count > 0
                    || effectDef.power != 0
                    || effectDef.dice_count != 0
                    || effectDef.dice_sides != 0
                    || parameters.ContainsKey("contact_status_id")
                    || parameters.ContainsKey("contact_damage_dice_count")
                    || parameters.ContainsKey("contact_damage_dice_sides")
                    || parameters.ContainsKey("contact_damage_flat_bonus")
                    || parameters.ContainsKey("contact_damage_tag")
                )
                    errors.Add(
                        $"Skill {skillId} terrain_effect in {contextLabel} movement interruption cannot also author damage or status payloads."
                    );
            }
            if (effectDef.terrain_max_active_instances_per_source < 0)
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} terrain_max_active_instances_per_source must be >= 0."
                );
            if (
                effectDef.terrain_replace_existing_from_source
                && effectDef.terrain_max_active_instances_per_source != 1
            )
                errors.Add(
                    $"Skill {skillId} terrain_effect in {contextLabel} source replacement currently requires terrain_max_active_instances_per_source=1."
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
        else if (effectKind == BattleEffectKind.PositionSwap)
        {
            CombatSkillDef profile = skillDef?.combat_profile;
            if (contextLabel.Contains("passive_effect_defs", StringComparison.Ordinal))
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} cannot be passive."
                );
            if (profile?.TargetModeKind != BattleTargetMode.Unit)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires target_mode unit."
                );
            if (profile?.TargetSelectionModeKind != BattleTargetSelectionMode.SingleUnit)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires target_selection_mode single_unit."
                );
            if (profile?.target_team_filter != "any")
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires target_team_filter any."
                );
            if (profile?.requires_los != true)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires requires_los=true."
                );
            if (effectDef.effect_target_team_filter != "any")
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires effect_target_team_filter any."
                );
            if (!effectDef.exclude_source)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires exclude_source=true."
                );
            if (effectDef.SaveDcModeKind != BattleSaveDcMode.CasterSpell)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} requires save_dc_mode caster_spell for hostile targets."
                );
            if (effectDef.TriggerEventKind != CombatEffectTriggerEvent.None)
                errors.Add(
                    $"Skill {skillId} position_swap effect in {contextLabel} must resolve directly and cannot have trigger_event."
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
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.GrappleAscent)
            {
                if (effectDef.forced_move_distance != 1)
                    errors.Add(
                        $"Skill {skillId} grapple_ascent effect in {contextLabel} must have forced_move_distance = 1."
                    );
                if (effectDef.grapple_max_height_gain < 2)
                    errors.Add(
                        $"Skill {skillId} grapple_ascent effect in {contextLabel} must have grapple_max_height_gain >= 2."
                    );
            }
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.AirbornePull)
            {
                if (effectDef.forced_move_distance <= 0)
                    errors.Add(
                        $"Skill {skillId} airborne_pull effect in {contextLabel} must have forced_move_distance >= 1."
                    );
                if (
                    effectDef.forced_move_max_target_body_size < 1
                    || effectDef.forced_move_max_target_body_size > 4
                )
                {
                    errors.Add(
                        $"Skill {skillId} airborne_pull effect in {contextLabel} forced_move_max_target_body_size must be between 1 and 4."
                    );
                }
                if (effectDef.required_target_status_id == "")
                    errors.Add(
                        $"Skill {skillId} airborne_pull effect in {contextLabel} requires required_target_status_id."
                    );
                if (
                    effectDef.save_dc != 0
                    || effectDef.save_ability != ""
                    || effectDef.save_tag != ""
                )
                {
                    errors.Add(
                        $"Skill {skillId} airborne_pull effect in {contextLabel} cannot declare a saving throw."
                    );
                }
                if (effectDef.TriggerEventKind != CombatEffectTriggerEvent.None)
                    errors.Add(
                        $"Skill {skillId} airborne_pull effect in {contextLabel} must resolve directly and cannot have trigger_event."
                    );
            }
            else if (effectDef.ForcedMoveModeKind == BattleForcedMoveMode.WindPush)
            {
                if (effectDef.forced_move_distance <= 0)
                    errors.Add(
                        $"Skill {skillId} wind_push effect in {contextLabel} must have forced_move_distance >= 1."
                    );
                if (
                    effectDef.forced_move_max_target_body_size < 1
                    || effectDef.forced_move_max_target_body_size > 4
                )
                {
                    errors.Add(
                        $"Skill {skillId} wind_push effect in {contextLabel} forced_move_max_target_body_size must be between 1 and 4."
                    );
                }
                if (
                    effectDef.save_dc <= 0
                    && effectDef.SaveDcModeKind != BattleSaveDcMode.CasterSpell
                )
                {
                    errors.Add(
                        $"Skill {skillId} wind_push effect in {contextLabel} requires a saving throw."
                    );
                }
            }
            else if (effectDef.forced_move_distance <= 0)
                errors.Add(
                    $"Skill {skillId} forced_move effect in {contextLabel} must have forced_move_distance >= 1."
                );
        }
        else if (effectKind == BattleEffectKind.SourceRetreat)
        {
            if (parameters.ContainsKey("distance"))
                errors.Add(
                    $"Skill {skillId} source_retreat effect in {contextLabel} params.distance is unsupported; use source_retreat_distance."
                );
            if (effectDef.source_retreat_distance <= 0)
                errors.Add(
                    $"Skill {skillId} source_retreat effect in {contextLabel} must have source_retreat_distance >= 1."
                );
        }
        else if (effectKind == BattleEffectKind.Charge)
        {
            foreach (
                string legacyParam in new[]
                {
                    "skill_id",
                    "base_distance",
                    "distance_by_level",
                    "trap_immunity_level",
                    "collision_base_damage",
                    "collision_size_gap_damage",
                }
            )
            {
                if (parameters.ContainsKey(legacyParam))
                {
                    errors.Add(
                        $"Skill {skillId} charge effect in {contextLabel} params.{legacyParam} is unsupported; charge distance comes from combat_profile range_value/level_overrides and collision damage comes from terrain interaction."
                    );
                }
            }
            if (effectDef.charge_trap_immunity_min_skill_level < -1)
            {
                errors.Add(
                    $"Skill {skillId} charge effect in {contextLabel} charge_trap_immunity_min_skill_level must be -1 or a non-negative skill level."
                );
            }
            else if (
                skillDef != null
                && effectDef.charge_trap_immunity_min_skill_level > skillDef.max_level
            )
            {
                errors.Add(
                    $"Skill {skillId} charge effect in {contextLabel} charge_trap_immunity_min_skill_level must not exceed max_level {skillDef.max_level}."
                );
            }
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
        bool hasWeightedFailureOutcomes =
            effectDef.save_failure_status_outcomes != null
            && effectDef.save_failure_status_outcomes.Count > 0;
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
            if (hasWeightedFailureOutcomes)
                errors.Add(
                    $"Skill {skillId} effect {contextLabel} save_failure_status_outcomes requires save_dc >= 1 or caster_spell save_dc_mode."
                );
            AppendWeightedSaveFailureOutcomeValidationErrors(
                errors,
                skillId,
                effectDef,
                contextLabel
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
        AppendWeightedSaveFailureOutcomeValidationErrors(
            errors,
            skillId,
            effectDef,
            contextLabel
        );
    }

    private void AppendWeightedSaveFailureOutcomeValidationErrors(
        Array<string> errors,
        StringName skillId,
        CombatEffectDef effectDef,
        string contextLabel
    )
    {
        if (
            effectDef?.save_failure_status_outcomes == null
            || effectDef.save_failure_status_outcomes.Count == 0
        )
        {
            return;
        }
        if (effectDef.EffectKind != BattleEffectKind.Damage)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_failure_status_outcomes is only supported on damage effects."
            );
        if (effectDef.save_failure_status_id != "")
            errors.Add(
                $"Skill {skillId} effect {contextLabel} cannot combine save_failure_status_id with save_failure_status_outcomes."
            );

        var outcomeIds = new System.Collections.Generic.HashSet<StringName>();
        long totalWeight = 0;
        for (int index = 0; index < effectDef.save_failure_status_outcomes.Count; index++)
        {
            CombatWeightedStatusOutcomeDef outcome =
                effectDef.save_failure_status_outcomes[index];
            string outcomeLabel =
                $"{contextLabel}.save_failure_status_outcomes[{index}]";
            if (outcome == null)
            {
                errors.Add($"Skill {skillId} effect {outcomeLabel} is null.");
                continue;
            }
            StringName outcomeId = ProgressionDataUtils.to_string_name(outcome.outcome_id);
            if (outcomeId == "")
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.outcome_id must be non-empty."
                );
            else if (!outcomeIds.Add(outcomeId))
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.outcome_id {outcomeId} is duplicated."
                );
            if (outcome.weight <= 0)
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.weight must be > 0."
                );
            else
                totalWeight += outcome.weight;

            CombatEffectDef statusEffect = outcome.status_effect;
            if (statusEffect == null)
            {
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect must be non-null."
                );
                continue;
            }
            if (
                statusEffect.EffectKind != BattleEffectKind.Status
                && statusEffect.EffectKind != BattleEffectKind.ApplyStatus
            )
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect must be status or apply_status."
                );
            if (statusEffect.effect_target_team_filter != "")
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect must inherit the already-resolved target and leave effect_target_team_filter empty."
                );
            if (statusEffect.min_skill_level != 0 || statusEffect.max_skill_level != -1)
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect cannot define an independent level window."
                );
            if (
                (statusEffect.trigger_event != "" && statusEffect.trigger_event != "none")
                || (statusEffect.trigger_condition != ""
                    && statusEffect.trigger_condition != "none")
            )
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect cannot define an independent trigger."
                );
            if (
                statusEffect.save_dc > 0
                || statusEffect.SaveDcModeKind == BattleSaveDcMode.CasterSpell
                || statusEffect.save_ability != ""
                || statusEffect.save_tag != ""
                || statusEffect.save_failure_status_id != ""
                || statusEffect.save_failure_status_outcomes.Count > 0
                || statusEffect.save_partial_on_success
            )
                errors.Add(
                    $"Skill {skillId} effect {outcomeLabel}.status_effect cannot define a nested save or failure outcome."
                );

            AppendEffectValidationErrors(
                errors,
                skillId,
                statusEffect,
                $"{outcomeLabel}.status_effect"
            );
        }
        if (totalWeight > int.MaxValue)
            errors.Add(
                $"Skill {skillId} effect {contextLabel} save_failure_status_outcomes total weight exceeds {int.MaxValue}."
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

    private static void AppendProjectileCategoryOwnershipErrors(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        Array<StringName> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            StringName value = values[index];
            if (CombatEffectCategoryContentRules.IsDerivedProjectileCategory(value))
            {
                errors.Add(
                    $"Skill {skillId} {fieldLabel}[{index}] cannot author derived projectile category {value}; use combat_profile.projectile_kind or cast_variants[].projectile_kind_override."
                );
            }
            else if (CombatEffectCategoryContentRules.IsRemovedProjectileCategory(value))
            {
                errors.Add(
                    $"Skill {skillId} {fieldLabel}[{index}] uses removed projectile category {value}; use combat_profile.projectile_kind or cast_variants[].projectile_kind_override."
                );
            }
        }
    }

    private bool IsValidPendingCastBindingMode(StringName value)
    {
        return value == BattleTypedNames.PendingCastBindingSoftAnchor
            || value == BattleTypedNames.PendingCastBindingHardAnchor
            || value == BattleTypedNames.PendingCastBindingGroundBind;
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
            if (MisfortuneContentRules.IsGatedSkill(skillDef.skill_id))
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
                    || effectDef.ForcedMoveModeKind == BattleForcedMoveMode.GrappleAscent
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
