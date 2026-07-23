using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

internal static class EquipmentAbilityPayloadValidators
{
    private const int TuGranularity = 5;
    private static readonly StringName StatusStackRefresh = "refresh";
    private static readonly StringName StatusStackAdd = "add";

    internal static StringName ReadStringNamePayloadMember(Resource payload, string memberName)
    {
        if (payload == null || string.IsNullOrWhiteSpace(memberName))
            return "";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        Type type = payload.GetType();
        PropertyInfo property = type.GetProperty(memberName, flags);
        object raw = property != null ? property.GetValue(payload) : null;
        if (raw == null)
        {
            FieldInfo field = type.GetField(memberName, flags);
            raw = field != null ? field.GetValue(payload) : null;
        }
        if (raw is StringName name)
            return name;
        if (raw is string text)
            return new StringName(text);
        return "";
    }

    internal static void ValidateAddDamageDicePayload(
        AddDamageDiceActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (
            payload.target_selector == ""
            || payload.damage_type == ""
            || payload.dice == null
            || (!hasDiceTerm && !hasFlatBonus)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "add_damage_dice requires target_selector, damage_type, and dice terms or positive flat_bonus"
            );
        }
        if (EquipmentAbilityContentRegistry.HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(payload.damage_type))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                $"{path}.payload.damage_type",
                $"damage_type {payload.damage_type} is not known"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        ValidateMitigationBypassArrays(
            payload.mitigation_bypass_damage_tags,
            payload.mitigation_bypass_tiers,
            context,
            path,
            errors
        );
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    internal static void ValidateImmediateWeaponAttackPayload(
        ImmediateWeaponAttackActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.target_team_filter == ""
            || payload.radius < 0
            || payload.max_attacks <= 0
            || payload.skill_id == ""
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "immediate_weapon_attack requires anchor_selector, target_team_filter, non-negative radius, positive max_attacks, and skill_id"
            );
        }
        EquipmentAbilityBindingValidator.ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
        StringName filter = ProgressionDataUtils.to_string_name(payload.target_team_filter);
        if (filter != "enemy" && filter != "ally" && filter != "any")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.target_team_filter",
                "immediate_weapon_attack target_team_filter must be enemy, ally, or any"
            );
        }
    }

    internal static void ValidateDealDamagePayload(
        DealDamageActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (
            payload.target_selector == ""
            || payload.damage_type == ""
            || payload.dice == null
            || (!hasDiceTerm && !hasFlatBonus)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "deal_damage requires target_selector, damage_type, and dice terms or positive flat_bonus"
            );
        }
        if (EquipmentAbilityContentRegistry.HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(payload.damage_type))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                $"{path}.payload.damage_type",
                $"damage_type {payload.damage_type} is not known"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        ValidateMitigationBypassArrays(
            payload.mitigation_bypass_damage_tags,
            payload.mitigation_bypass_tiers,
            context,
            path,
            errors
        );
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    internal static void ValidateHealPayload(
        HealActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (payload.target_selector == "" || payload.dice == null || (!hasDiceTerm && !hasFlatBonus))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "heal requires target_selector and dice terms or positive flat_bonus"
            );
        }
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    internal static void ValidateHealFromFactPayload(
        HealFromFactActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.amount_fact == null || payload.multiplier_percent <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "heal_from_fact requires target_selector, amount_fact and positive multiplier_percent"
            );
        }
        EquipmentAbilityBindingValidator.ValidateFactQuery(payload.amount_fact, context, $"{path}.payload.amount_fact", errors);
        if (payload.max_amount < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.max_amount",
                "heal_from_fact max_amount must be zero or positive"
            );
        }
    }

    private static void ValidateDamageTagArray(
        Godot.Collections.Array<StringName> damageTags,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (damageTags == null || damageTags.Count == 0)
            return;
        for (int index = 0; index < damageTags.Count; index++)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(damageTags[index]);
            if (damageTag == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}[{index}]",
                    "damage tag must be non-empty"
                );
                continue;
            }
            if (EquipmentAbilityContentRegistry.HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(damageTag))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}[{index}]",
                    $"damage tag {damageTag} is not known"
                );
            }
        }
    }

    private static void ValidateMitigationBypassArrays(
        Godot.Collections.Array<StringName> damageTags,
        Godot.Collections.Array<StringName> tiers,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        int tagCount = damageTags?.Count ?? 0;
        int tierCount = tiers?.Count ?? 0;
        if (tagCount == 0 && tierCount == 0)
            return;
        if (tagCount == 0 || tierCount == 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_MITIGATION_BYPASS_INCOMPLETE",
                $"{path}.payload",
                "mitigation bypass requires both mitigation_bypass_damage_tags and mitigation_bypass_tiers"
            );
        }
        ValidateDamageTagArray(
            damageTags,
            context,
            $"{path}.payload.mitigation_bypass_damage_tags",
            errors
        );
        for (int index = 0; index < tierCount; index++)
        {
            StringName tier = ProgressionDataUtils.to_string_name(tiers[index]);
            if (
                DamageTagContentRules.ToMitigationTierKind(tier)
                == DamageMitigationTierKind.Unknown
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_BYPASS_TIER_INVALID",
                    $"{path}.payload.mitigation_bypass_tiers[{index}]",
                    $"mitigation bypass tier {tier} is not supported"
                );
            }
        }
    }

    internal static void ValidateAttackRollBonusPayload(
        AttackRollBonusActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        bool hasDynamicBonus =
            ProgressionDataUtils.to_string_name(payload.attribute_modifier_id) != "";
        if (payload.target_selector == "" || (payload.bonus == 0 && !hasDynamicBonus))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_roll_bonus requires target_selector and non-zero bonus or attribute_modifier_id"
            );
        }
    }

    internal static void ValidateAttackRollAdvantagePayload(
        AttackRollAdvantageActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.mode != "advantage")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_roll_advantage requires target_selector and mode=advantage"
            );
        }
    }

    internal static void ValidateAttackDefenseModifierPayload(
        EquipmentAttackDefenseModifierDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.modifier_id == ""
            || (
                (payload.ignored_ac_components?.Count ?? 0) == 0
                && (payload.ac_component_multipliers?.Count ?? 0) == 0
                && !payload.lock_dodge_bonus
            )
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_defense_modifier requires modifier_id and at least one AC adjustment"
            );
        }

        var ignored = new HashSet<StringName>();
        foreach (StringName componentId in payload.ignored_ac_components ?? new Godot.Collections.Array<StringName>())
        {
            if (!AttributeContentRules.IsArmorClassComponentAttributeId(componentId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_UNKNOWN",
                    $"{path}.payload.ignored_ac_components[{componentId}]",
                    $"AC component {componentId} is not registered"
                );
                continue;
            }
            ignored.Add(componentId);
        }

        foreach (EquipmentAcComponentMultiplierDef multiplier in payload.ac_component_multipliers ?? new Godot.Collections.Array<EquipmentAcComponentMultiplierDef>())
        {
            if (multiplier == null)
                continue;
            if (
                !AttributeContentRules.IsArmorClassComponentAttributeId(
                    multiplier.ac_component_id
                )
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_UNKNOWN",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}]",
                    $"AC component {multiplier.ac_component_id} is not registered"
                );
            }
            if (ignored.Contains(multiplier.ac_component_id))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_CONFLICT",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}]",
                    $"AC component {multiplier.ac_component_id} cannot be both ignored and multiplied"
                );
            }
            if (multiplier.multiplier_percent < 0 || multiplier.multiplier_percent > 100)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_MULTIPLIER_INVALID",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}].multiplier_percent",
                    "AC component multiplier percent must be between 0 and 100"
                );
            }
            if (multiplier.stack_mode != "" && multiplier.stack_mode != "min")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_STACK_MODE_UNSUPPORTED",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}].stack_mode",
                    "AC component multiplier stack_mode must be empty or min"
                );
            }
        }

        bool hasEquipmentFilter =
            (payload.required_target_item_tags?.Count ?? 0) > 0
            || (payload.required_target_equipment_type_ids?.Count ?? 0) > 0;
        if (hasEquipmentFilter && payload.required_target_equipment_selector == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.required_target_equipment_selector",
                "attack_defense_modifier target equipment filters require required_target_equipment_selector"
            );
        }
        if (
            payload.required_target_equipment_selector != ""
            && payload.required_target_equipment_selector != "target_armor"
            && payload.required_target_equipment_selector != "target_shield"
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ATTACK_DEFENSE_TARGET_EQUIPMENT_SELECTOR_UNSUPPORTED",
                $"{path}.payload.required_target_equipment_selector",
                $"target equipment selector {payload.required_target_equipment_selector} is not supported"
            );
        }
        if (payload.cover_policy != "" && payload.cover_policy != "normal")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ATTACK_DEFENSE_COVER_POLICY_UNSUPPORTED",
                $"{path}.payload.cover_policy",
                $"cover policy {payload.cover_policy} is not supported"
            );
        }
        if (
            payload.projectile_obstacle_policy != ""
            && payload.projectile_obstacle_policy != "normal"
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ATTACK_DEFENSE_PROJECTILE_POLICY_UNSUPPORTED",
                $"{path}.payload.projectile_obstacle_policy",
                $"projectile obstacle policy {payload.projectile_obstacle_policy} is not supported"
            );
        }
    }

    internal static void ValidateDamageRollModeOverridePayload(
        DamageRollModeOverrideActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.target_selector == ""
            || (
                payload.roll_mode != "random"
                && payload.roll_mode != "average"
                && payload.roll_mode != "maximum"
            )
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "damage_roll_mode_override requires target_selector and roll_mode random/average/maximum"
            );
        }
    }

    internal static void ValidateDamageReductionPayload(
        DamageReductionActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.target_selector == ""
            || payload.amount <= 0
            || (payload.damage_tags?.Count ?? 0) == 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "damage_reduction requires target_selector, positive amount, and at least one damage tag"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        if (
            payload.target_selector != ""
            && payload.target_selector != "self"
            && payload.target_selector != "holder"
            && payload.target_selector != "defender"
            && payload.target_selector != "damage_target"
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_DAMAGE_REDUCTION_TARGET_SELECTOR_UNSUPPORTED",
                $"{path}.payload.target_selector",
                $"damage_reduction target_selector {payload.target_selector} is not supported"
            );
        }
    }

    internal static void ValidateLootQuantityMultiplierPayload(
        LootQuantityMultiplierActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.multiplier_percent <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "loot_quantity_multiplier requires target_selector and positive multiplier_percent"
            );
        }
        foreach (StringName dropKind in payload.affected_drop_kinds)
        {
            if (dropKind == "" || BattleLootIds.ToDropKind(dropKind) == BattleLootDropKind.Unknown)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_LOOT_DROP_KIND_INVALID",
                    $"{path}.payload.affected_drop_kinds",
                    $"loot_quantity_multiplier drop kind {dropKind} is not supported"
                );
            }
        }
    }

    internal static void ValidateApplyStatusPayload(
        ApplyStatusActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.status_id == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_status requires target_selector and status_id"
            );
        }
        EquipmentAbilityBindingValidator.ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        ValidateStatusSemanticPayload(
            payload.stack_behavior,
            payload.stack_limit,
            payload.counts_as_debuff_override,
            payload.counts_as_debuff,
            payload.undispellable,
            payload.dispellable_magic,
            payload.dispellable_harmful_magic,
            payload.dispellable_beneficial_magic,
            path,
            "apply_status",
            "",
            errors
        );
        if (payload.source_bound_attack_roll_penalty_min_stacks <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_SOURCE_BOUND_MIN_STACKS_INVALID",
                $"{path}.payload.source_bound_attack_roll_penalty_min_stacks",
                "apply_status source_bound_attack_roll_penalty_min_stacks must be positive"
            );
        }
        if (payload.source_bound_incoming_attack_roll_bonus_min_stacks <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_SOURCE_BOUND_MIN_STACKS_INVALID",
                $"{path}.payload.source_bound_incoming_attack_roll_bonus_min_stacks",
                "apply_status source_bound_incoming_attack_roll_bonus_min_stacks must be positive"
            );
        }
        if (payload.heal_multiplier_percent < 0 || payload.heal_multiplier_percent > 100)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_HEAL_MULTIPLIER_INVALID",
                $"{path}.payload.heal_multiplier_percent",
                "apply_status heal_multiplier_percent must be between 0 and 100"
            );
        }
        if (payload.save_dc > 0 && (payload.save_ability == "" || payload.save_tag == ""))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_status save gate requires save_ability and save_tag when save_dc is positive"
            );
        }
        bool hasTimelineDamageDice =
            payload.timeline_damage_dice_count > 0
            || payload.timeline_damage_dice_sides > 0
            || payload.timeline_damage_flat_bonus > 0;
        if (payload.tick_interval_tu < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_TICK_INVALID",
                $"{path}.payload.tick_interval_tu",
                "apply_status tick_interval_tu must be >= 0"
            );
        }
        if (payload.timeline_damage_dice_count < 0 || payload.timeline_damage_dice_sides < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                path,
                "apply_status timeline damage dice count/sides must be >= 0"
            );
        }
        if (payload.timeline_damage_flat_bonus < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                $"{path}.payload.timeline_damage_flat_bonus",
                "apply_status timeline_damage_flat_bonus must be >= 0"
            );
        }
        if (hasTimelineDamageDice)
        {
            if (payload.tick_interval_tu <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATUS_TICK_INVALID",
                    $"{path}.payload.tick_interval_tu",
                    "apply_status timeline damage dice require positive tick_interval_tu"
                );
            }
            if (payload.timeline_damage_dice_count <= 0 || payload.timeline_damage_dice_sides <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                    path,
                    "apply_status timeline damage dice require positive dice count and sides"
                );
            }
        }
    }

    internal static void ValidateModifyActionPointsPayload(
        ModifyActionPointsActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        StringName mode = ProgressionDataUtils.to_string_name(payload.mode);
        if (payload.target_selector == "" || mode == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "modify_action_points requires target_selector and mode"
            );
        }
        if (
            mode != ""
            && mode != "add_base_action_points"
            && mode != "subtract_current_action_points"
            && mode != "restore_current_action_points_capped"
            && mode != "set_next_turn_ap_to_zero"
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_MODE_INVALID",
                $"{path}.payload.mode",
                $"modify_action_points mode {mode} is not supported"
            );
        }
        if (payload.amount < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points amount must be >= 0"
            );
        }
        if (mode == "subtract_current_action_points" && payload.amount <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points subtract_current_action_points requires positive amount"
            );
        }
        if (mode == "restore_current_action_points_capped" && payload.amount <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points restore_current_action_points_capped requires positive amount"
            );
        }
        if (mode == "set_next_turn_ap_to_zero")
        {
            if (payload.status_id == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ACTION_REQUIRED_FIELD_MISSING",
                    path,
                    "modify_action_points set_next_turn_ap_to_zero requires status_id"
                );
            }
            EquipmentAbilityBindingValidator.ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        }
    }

    internal static void ValidateMarkTargetPayload(
        MarkTargetActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.state_key == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "mark_target requires target_selector and state_key"
            );
        }
        if (payload.mirror_status_id != "")
        {
            EquipmentAbilityBindingValidator.ValidateStatusReference(
                payload.mirror_status_id,
                context,
                $"{path}.payload.mirror_status_id",
                errors
            );
            ValidateStatusSemanticPayload(
                payload.mirror_status_stack_behavior,
                payload.mirror_status_stack_limit,
                countsAsDebuffOverride: false,
                countsAsDebuff: false,
                undispellable: false,
                dispellableMagic: false,
                dispellableHarmfulMagic: false,
                dispellableBeneficialMagic: false,
                path,
                "mark_target mirror",
                "mirror_status_",
                errors
            );
        }
        foreach (StringName statusId in payload.clear_status_ids_on_replace ?? new Godot.Collections.Array<StringName>())
        {
            EquipmentAbilityBindingValidator.ValidateStatusReference(
                statusId,
                context,
                $"{path}.payload.clear_status_ids_on_replace[{statusId}]",
                errors
            );
        }
    }

    internal static void ValidateSummonUnitsPayload(
        SummonUnitsActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload == null
            || payload.state_key == ""
            || payload.count_dice == null
            || payload.count_dice.terms.Count == 0
            || payload.max_living_units <= 0
            || string.IsNullOrWhiteSpace(payload.unit_display_name)
            || payload.hp_max <= 0
            || payload.armor_class <= 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "summon_units requires state_key, count_dice terms, max_living_units, unit_display_name, positive hp_max, and positive armor_class"
            );
        }
        if (payload != null && payload.spawn_radius < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_SUMMON_SPAWN_RADIUS_INVALID",
                $"{path}.payload.spawn_radius",
                "summon_units spawn_radius must be >= 0"
            );
        }
        foreach (StringName skillId in payload?.known_active_skill_ids ?? new Godot.Collections.Array<StringName>())
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_SUMMON_KNOWN_SKILL_ID_EMPTY",
                    $"{path}.payload.known_active_skill_ids",
                    "summon_units known_active_skill_ids cannot contain empty ids"
                );
                continue;
            }
            if (EquipmentAbilityContentRegistry.HasKnownValues(context.KnownSkillIds) && !context.KnownSkillIds.Contains(normalizedSkillId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_SKILL",
                    $"{path}.payload.known_active_skill_ids",
                    $"skill_id {normalizedSkillId} is not known"
                );
            }
        }
        bool hasNaturalWeapon =
            payload != null
            && (
                payload.natural_weapon_profile_type_id != ""
                || payload.natural_weapon_damage_tag != ""
                || payload.natural_weapon_attack_range > 0
                || payload.natural_weapon_damage_dice != null
                || payload.natural_weapon_family != ""
            );
        if (hasNaturalWeapon)
        {
            bool hasOneDiceTerm = payload.natural_weapon_damage_dice?.terms?.Count == 1;
            DiceExpressionTermDef term = hasOneDiceTerm
                ? payload.natural_weapon_damage_dice.terms[0]
                : null;
            if (
                payload.natural_weapon_profile_type_id == ""
                || payload.natural_weapon_damage_tag == ""
                || payload.natural_weapon_attack_range <= 0
                || term == null
                || term.dice_count <= 0
                || term.dice_sides <= 0
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_SUMMON_NATURAL_WEAPON_INVALID",
                    $"{path}.payload.natural_weapon_damage_dice",
                    "summon_units natural weapon requires profile type, damage tag, positive attack range, and exactly one positive dice term"
                );
            }
            else if (
                DamageTagContentRules.ToDamageTagKind(payload.natural_weapon_damage_tag)
                == DamageTagKind.Unknown
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}.payload.natural_weapon_damage_tag",
                    $"damage tag {payload.natural_weapon_damage_tag} is not known"
                );
            }
        }
        foreach (DiceExpressionTermDef term in payload?.count_dice?.terms ?? new Godot.Collections.Array<DiceExpressionTermDef>())
        {
            if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_DICE_INVALID",
                    $"{path}.payload.count_dice",
                    "summon_units dice terms must have positive dice_count and dice_sides"
                );
            }
        }
    }

    internal static void ValidateConsumeSummonedUnitsPayload(
        ConsumeSummonedUnitsActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload == null || payload.state_key == "" || payload.count <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "consume_summoned_units requires state_key and positive count"
            );
        }
    }

    internal static void ValidateConsumeStatusStacksPayload(
        ConsumeStatusStacksActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload == null || payload.target_selector == "" || payload.status_id == "" || payload.count <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "consume_status_stacks requires target_selector, status_id and positive count"
            );
            return;
        }
        EquipmentAbilityBindingValidator.ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        if (payload.selection_mode != "" && payload.selection_mode != "highest_stacks")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.selection_mode",
                $"consume_status_stacks selection_mode {payload.selection_mode} is not supported"
            );
        }
    }

    internal static void ValidateSummonedUnitAttackRollModifierPayload(
        SummonedUnitAttackRollModifierActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload == null
            || payload.target_selector == ""
            || payload.source_binding_id == ""
            || payload.state_key == ""
            || payload.bonus_per_unit == 0
            || payload.max_absolute_bonus <= 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "summoned_unit_attack_roll_modifier requires target_selector, source_binding_id, state_key, non-zero bonus_per_unit, and positive max_absolute_bonus"
            );
        }
        if (payload != null && payload.radius < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_SUMMON_RADIUS_INVALID",
                $"{path}.payload.radius",
                "summoned_unit_attack_roll_modifier radius must be >= 0"
            );
        }
    }

    internal static void ValidateClearStatusPayload(
        ClearStatusActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.status_id == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "clear_status requires target_selector and status_id"
            );
        }
        if (payload.target_selector == "marked_target" || payload.target_selector == "equipment_target_mark")
        {
            if (payload.mark_binding_id == "" || payload.mark_state_key == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_ACTION_REQUIRED_FIELD_MISSING",
                    path,
                    "clear_status marked_target selector requires mark_binding_id and mark_state_key"
                );
            }
        }
        EquipmentAbilityBindingValidator.ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
    }

    internal static void ValidateCriticalHitOverridePayload(
        CriticalHitOverrideActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "critical_hit_override requires target_selector"
            );
        }
    }

    internal static void ValidateTriggerSkillPayload(
        TriggerSkillActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.skill_id == "" || payload.skill_level <= 0 || payload.target_selector == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "trigger_skill requires skill_id, positive skill_level, and target_selector"
            );
        }
        EquipmentAbilityBindingValidator.ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
    }

    private static void ValidateStatusSemanticPayload(
        StringName stackBehavior,
        int stackLimit,
        bool countsAsDebuffOverride,
        bool countsAsDebuff,
        bool undispellable,
        bool dispellableMagic,
        bool dispellableHarmfulMagic,
        bool dispellableBeneficialMagic,
        string path,
        string ownerLabel,
        string fieldPrefix,
        List<string> errors
    )
    {
        StringName normalizedStackBehavior = ProgressionDataUtils.to_string_name(stackBehavior);
        if (
            normalizedStackBehavior != ""
            && normalizedStackBehavior != StatusStackRefresh
            && normalizedStackBehavior != StatusStackAdd
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_STACK_BEHAVIOR_INVALID",
                $"{path}.payload.{fieldPrefix}stack_behavior",
                $"{ownerLabel} status stack_behavior must be refresh or add"
            );
        }
        if (stackLimit < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_STACK_LIMIT_INVALID",
                $"{path}.payload.{fieldPrefix}stack_limit",
                $"{ownerLabel} status stack_limit must be >= 0"
            );
        }
        if (countsAsDebuff && !countsAsDebuffOverride)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_DEBUFF_FLAG_INVALID",
                $"{path}.payload.{fieldPrefix}counts_as_debuff",
                $"{ownerLabel} status counts_as_debuff requires counts_as_debuff_override"
            );
        }
        if (
            undispellable
            && (dispellableMagic || dispellableHarmfulMagic || dispellableBeneficialMagic)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_DISPEL_FLAG_INVALID",
                $"{path}.payload.{fieldPrefix}undispellable",
                $"{ownerLabel} status cannot be both undispellable and dispellable"
            );
        }
    }

    internal static void ValidateScheduleAreaEffectPayload(
        ScheduleAreaEffectActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.delay_tu <= 0
            || payload.terrain_effect_id == ""
            || payload.area_pattern == ""
            || payload.lifetime_policy == ""
            || payload.effect_type == ""
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "schedule_area_effect requires anchor_selector, positive delay_tu, terrain_effect_id, area_pattern, lifetime_policy, and effect_type"
            );
        }
        if (payload.delay_tu > 0 && payload.delay_tu % TuGranularity != 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.delay_tu",
                $"schedule_area_effect delay_tu must be a multiple of {TuGranularity}"
            );
        }
        if (payload.area_value < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_AREA_VALUE_INVALID",
                $"{path}.payload.area_value",
                "schedule_area_effect area_value must be >= 0"
            );
        }
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(payload.target_team_filter))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_TARGET_TEAM_FILTER_INVALID",
                $"{path}.payload.target_team_filter",
                $"schedule_area_effect target_team_filter {payload.target_team_filter} is not supported"
            );
        }

        bool hasContactStatus = payload.contact_status_id != "";
        if (!hasContactStatus)
        {
            return;
        }
        EquipmentAbilityBindingValidator.ValidateStatusReference(
            payload.contact_status_id,
            context,
            $"{path}.payload.contact_status_id",
            errors
        );
        ValidateStatusSemanticPayload(
            payload.contact_stack_behavior,
            payload.contact_stack_limit,
            payload.contact_counts_as_debuff_override,
            payload.contact_counts_as_debuff,
            payload.contact_undispellable,
            payload.contact_dispellable_magic,
            payload.contact_dispellable_harmful_magic,
            payload.contact_dispellable_beneficial_magic,
            path,
            "schedule_area_effect contact",
            "contact_",
            errors
        );
        if (payload.contact_status_duration_tu <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.contact_status_duration_tu",
                "schedule_area_effect contact status requires positive contact_status_duration_tu"
            );
        }
        if (payload.contact_save_dc > 0 && (payload.contact_save_ability == "" || payload.contact_save_tag == ""))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "schedule_area_effect contact save gate requires contact_save_ability and contact_save_tag when contact_save_dc is positive"
            );
        }
        if (payload.contact_tick_interval_tu < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_TICK_INVALID",
                $"{path}.payload.contact_tick_interval_tu",
                "schedule_area_effect contact_tick_interval_tu must be >= 0"
            );
        }
        if (
            payload.contact_tick_interval_tu > 0
            && payload.contact_tick_interval_tu % TuGranularity != 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.contact_tick_interval_tu",
                $"schedule_area_effect contact_tick_interval_tu must be a multiple of {TuGranularity}"
            );
        }
        bool hasContactTimelineDamage =
            payload.contact_timeline_damage_dice_count > 0
            || payload.contact_timeline_damage_dice_sides > 0
            || payload.contact_timeline_damage_flat_bonus > 0;
        if (
            payload.contact_timeline_damage_dice_count < 0
            || payload.contact_timeline_damage_dice_sides < 0
            || payload.contact_timeline_damage_flat_bonus < 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                path,
                "schedule_area_effect contact timeline damage dice fields must be >= 0"
            );
        }
        if (hasContactTimelineDamage)
        {
            if (payload.contact_tick_interval_tu <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATUS_TICK_INVALID",
                    $"{path}.payload.contact_tick_interval_tu",
                    "schedule_area_effect contact timeline damage requires positive contact_tick_interval_tu"
                );
            }
            if (
                payload.contact_timeline_damage_dice_count <= 0
                || payload.contact_timeline_damage_dice_sides <= 0
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                    path,
                    "schedule_area_effect contact timeline damage requires positive dice count and sides"
                );
            }
        }
    }

    internal static void ValidateApplyBattleTerrainEffectAfterCheckPayload(
        ApplyBattleTerrainEffectAfterCheckActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.terrain_effect_id == ""
            || payload.check_attribute_modifier_id == ""
            || payload.check_compare == ""
            || payload.check_threshold <= 0
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_battle_terrain_effect_after_check requires anchor_selector, terrain_effect_id, check_attribute_modifier_id, check_compare, and positive check_threshold"
            );
        }
        if (payload.move_cost_delta <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_MOVE_COST_DELTA_INVALID",
                $"{path}.payload.move_cost_delta",
                "apply_battle_terrain_effect_after_check move_cost_delta must be positive"
            );
        }
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(payload.target_team_filter))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_TARGET_TEAM_FILTER_INVALID",
                $"{path}.payload.target_team_filter",
                $"apply_battle_terrain_effect_after_check target_team_filter {payload.target_team_filter} is not supported"
            );
        }
        if (!IsValidIntCompareOperator(payload.check_compare))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_COMPARE_OPERATOR_INVALID",
                $"{path}.payload.check_compare",
                $"apply_battle_terrain_effect_after_check check_compare {payload.check_compare} is not supported"
            );
        }
    }

    internal static void ValidateApplyEdgeFeaturePayload(
        ApplyEdgeFeatureActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.from_selector == ""
            || payload.to_selector == ""
            || payload.duration_tu <= 0
            || payload.feature_kind == ""
            || payload.render_kind == ""
            || payload.interaction_kind == ""
            || payload.state_tag == ""
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_edge_feature requires from_selector, to_selector, positive duration_tu, feature_kind, render_kind, interaction_kind, and state_tag"
            );
        }
        if (payload.duration_tu > 0 && payload.duration_tu % TuGranularity != 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.duration_tu",
                $"apply_edge_feature duration_tu must be a multiple of {TuGranularity}"
            );
        }
        if (payload.max_active_edges < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.max_active_edges",
                "apply_edge_feature max_active_edges must be >= 0"
            );
        }
        if (payload.render_layers < 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.render_layers",
                "apply_edge_feature render_layers must be >= 0"
            );
        }
        if (!IsValidEdgeEndpointSelector(payload.from_selector, sourceOnly: true))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.from_selector",
                "apply_edge_feature from_selector must be source, owner, attacker, or source_attacker"
            );
        }
        if (!IsValidEdgeEndpointSelector(payload.to_selector, sourceOnly: false))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.to_selector",
                "apply_edge_feature to_selector must be target or attack_target"
            );
        }
        if (BattleEdgeFeatureState.ToFeatureKind(payload.feature_kind) == BattleEdgeFeatureKind.Unknown)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.feature_kind",
                $"apply_edge_feature feature_kind {payload.feature_kind} is not supported"
            );
        }
        if (BattleEdgeFeatureState.ToRenderKind(payload.render_kind) == BattleEdgeRenderKind.Unknown)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.render_kind",
                $"apply_edge_feature render_kind {payload.render_kind} is not supported"
            );
        }
        if (
            BattleEdgeFeatureState.ToInteractionKind(payload.interaction_kind)
            == BattleEdgeInteractionKind.Unknown
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.interaction_kind",
                $"apply_edge_feature interaction_kind {payload.interaction_kind} is not supported"
            );
        }
    }

    internal static void ValidateModifyAbilityStatePayload(
        ModifyAbilityStateActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.state_key == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "modify_ability_state requires target_selector and state_key"
            );
        }
    }

    private static bool IsValidIntCompareOperator(StringName compare)
    {
        return ProgressionDataUtils.to_string_name(compare).ToString() switch
        {
            "lte" or "lt" or "gte" or "gt" or "eq" => true,
            _ => false,
        };
    }

    private static bool IsValidEdgeEndpointSelector(StringName selector, bool sourceOnly)
    {
        string normalized = ProgressionDataUtils.to_string_name(selector).ToString();
        if (normalized == "source" || normalized == "owner" || normalized == "attacker" || normalized == "source_attacker")
            return true;
        return !sourceOnly && (normalized == "target" || normalized == "attack_target");
    }

    internal static void ValidateDurabilityPayload(
        EquipmentDurabilityDamageActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.durability_loss <= 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "equipment_durability_damage requires target_selector and positive durability_loss"
            );
        }
        if (payload.max_damaged_items != 1)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_DURABILITY_MAX_DAMAGED_ITEMS_UNSUPPORTED",
                $"{path}.payload.max_damaged_items",
                "V1 requires max_damaged_items = 1"
            );
        }
        if (payload.max_target_rarity < -1 || !IsKnownEquipmentRarity(payload.max_target_rarity))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_DURABILITY_TARGET_RARITY_INVALID",
                $"{path}.payload.max_target_rarity",
                "max_target_rarity must be -1 or a valid EquipmentInstanceState.RarityTier value"
            );
        }
        if (EquipmentAbilityContentRegistry.HasKnownValues(context.KnownEquipmentSlotIds))
        {
            foreach (StringName slot in payload.target_slots)
            {
                if (!context.KnownEquipmentSlotIds.Contains(slot))
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_REFERENCE_UNKNOWN_SLOT",
                        $"{path}.payload.target_slots[{slot}]",
                        $"equipment slot {slot} is not known"
                    );
                }
            }
        }
        HashSet<StringName> weightedSlots = new();
        if (payload.slot_weights != null)
        {
            for (int index = 0; index < payload.slot_weights.Count; index++)
            {
                EquipmentSlotWeightDef weight = payload.slot_weights[index];
                if (weight == null || weight.slot_id == "" || weight.weight <= 0)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_SLOT_WEIGHT_INVALID",
                        $"{path}.payload.slot_weights[{index}]",
                        "slot_weights entries require slot_id and positive weight"
                    );
                    continue;
                }
                if (!weightedSlots.Add(weight.slot_id))
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_SLOT_WEIGHT_DUPLICATE",
                        $"{path}.payload.slot_weights[{weight.slot_id}]",
                        $"slot_weight for {weight.slot_id} is duplicated"
                    );
                }
                if (
                    EquipmentAbilityContentRegistry.HasKnownValues(context.KnownEquipmentSlotIds)
                    && !context.KnownEquipmentSlotIds.Contains(weight.slot_id)
                )
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_REFERENCE_UNKNOWN_SLOT",
                        $"{path}.payload.slot_weights[{weight.slot_id}]",
                        $"equipment slot {weight.slot_id} is not known"
                    );
                }
            }
        }
    }

    private static bool IsKnownEquipmentRarity(int rarity)
    {
        return rarity == -1 || EquipmentInstanceState.IsValidRarity(rarity);
    }

    internal static void ValidateDeclaredStateKey(
        StringName stateKey,
        HashSet<StringName> declaredStateKeys,
        string path,
        List<string> errors
    )
    {
        if (stateKey == "" || declaredStateKeys.Contains(stateKey))
            return;
        EquipmentAbilityContentRegistry.AddError(
            errors,
            "EQA_STATE_KEY_UNDECLARED",
            $"{path}.payload.state_key",
            $"state_key {stateKey} is not declared by binding state_schemas"
        );
    }
}
