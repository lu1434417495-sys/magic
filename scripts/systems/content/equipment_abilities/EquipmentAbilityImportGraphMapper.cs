#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

internal static class EquipmentAbilityImportGraphMapper
{

    internal static EquipmentAbilityContentPackImportModel FromDto(
        EquipmentAbilityContentPackJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityContentPackImportModel
        {
            pack_id = value.PackId ?? "",
            schema_version = value.SchemaVersion,
            load_order = value.LoadOrder,
            dependencies = MapStringNames(value.Dependencies),
            bindings = Map(value.Bindings, (item, index) => FromDto(item, context, $"{pointer}/bindings/{index}", diagnostics)),
        };
    }

    internal static EquipmentAbilityContentPackJsonDto ToDto(EquipmentAbilityContentPackImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityContentPackJsonDto
        {
            PackId = value.pack_id,
            SchemaVersion = value.schema_version,
            LoadOrder = value.load_order,
            Dependencies = MapStringNamesToText(value.dependencies),
            Bindings = Map(value.bindings, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentAbilityBindingImportModel FromDto(
        EquipmentAbilityBindingJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityBindingImportModel
        {
            binding_id = value.BindingId ?? "",
            trait_id = value.TraitId ?? "",
            override_mode = value.OverrideMode ?? "",
            replaces_binding_id = value.ReplacesBindingId ?? "",
            allowed_source_kinds = MapStringNames(value.AllowedSourceKinds),
            required_trait_categories = MapStringNames(value.RequiredTraitCategories),
            required_effective_trait_ids = MapStringNames(value.RequiredEffectiveTraitIds),
            required_item_tags = MapStringNames(value.RequiredItemTags),
            supported_equipment_type_ids = MapStringNames(value.SupportedEquipmentTypeIds),
            activation_status_id = value.ActivationStatusId ?? "",
            state_schemas = Map(value.StateSchemas, (item, index) => FromDto(item, context, $"{pointer}/state_schemas/{index}", diagnostics)),
            reactions = Map(value.Reactions, (item, index) => FromDto(item, context, $"{pointer}/reactions/{index}", diagnostics)),
            fatal_intercepts = Map(value.FatalIntercepts, (item, index) => FromDto(item, context, $"{pointer}/fatal_intercepts/{index}", diagnostics)),
            mitigation_auras = Map(value.MitigationAuras, (item, index) => FromDto(item, context, $"{pointer}/mitigation_auras/{index}", diagnostics)),
            movement_trails = Map(value.MovementTrails, (item, index) => FromDto(item, context, $"{pointer}/movement_trails/{index}", diagnostics)),
            granted_actions = Map(value.GrantedActions, (item, index) => FromDto(item, context, $"{pointer}/granted_actions/{index}", diagnostics)),
            temporal_progress_modifiers = Map(value.TemporalProgressModifiers, (item, index) => FromDto(item, context, $"{pointer}/temporal_progress_modifiers/{index}", diagnostics)),
            cognition_ceiling_modifiers = Map(value.CognitionCeilingModifiers, (item, index) => FromDto(item, context, $"{pointer}/cognition_ceiling_modifiers/{index}", diagnostics)),
            weapon_profile_overlays = Map(value.WeaponProfileOverlays, (item, index) => FromDto(item, context, $"{pointer}/weapon_profile_overlays/{index}", diagnostics)),
            world_effects = Map(value.WorldEffects, (item, index) => FromDto(item, context, $"{pointer}/world_effects/{index}", diagnostics)),
        };
    }

    internal static EquipmentAbilityBindingJsonDto ToDto(EquipmentAbilityBindingImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityBindingJsonDto
        {
            BindingId = value.binding_id,
            TraitId = value.trait_id,
            OverrideMode = value.override_mode,
            ReplacesBindingId = value.replaces_binding_id,
            AllowedSourceKinds = MapStringNamesToText(value.allowed_source_kinds),
            RequiredTraitCategories = MapStringNamesToText(value.required_trait_categories),
            RequiredEffectiveTraitIds = MapStringNamesToText(value.required_effective_trait_ids),
            RequiredItemTags = MapStringNamesToText(value.required_item_tags),
            SupportedEquipmentTypeIds = MapStringNamesToText(value.supported_equipment_type_ids),
            ActivationStatusId = value.activation_status_id,
            StateSchemas = Map(value.state_schemas, static (item, _) => ToDto(item)),
            Reactions = Map(value.reactions, static (item, _) => ToDto(item)),
            FatalIntercepts = Map(value.fatal_intercepts, static (item, _) => ToDto(item)),
            MitigationAuras = Map(value.mitigation_auras, static (item, _) => ToDto(item)),
            MovementTrails = Map(value.movement_trails, static (item, _) => ToDto(item)),
            GrantedActions = Map(value.granted_actions, static (item, _) => ToDto(item)),
            TemporalProgressModifiers = Map(value.temporal_progress_modifiers, static (item, _) => ToDto(item)),
            CognitionCeilingModifiers = Map(value.cognition_ceiling_modifiers, static (item, _) => ToDto(item)),
            WeaponProfileOverlays = Map(value.weapon_profile_overlays, static (item, _) => ToDto(item)),
            WorldEffects = Map(value.world_effects, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentAbilityReactionImportModel FromDto(
        EquipmentAbilityReactionJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityReactionImportModel
        {
            reaction_id = value.ReactionId ?? "",
            trigger = value.Trigger ?? "",
            timing = value.Timing ?? "",
            priority = value.Priority,
            once_scope = value.OnceScope ?? "",
            requires_player_confirmation = value.RequiresPlayerConfirmation,
            condition_group = value.ConditionGroup == null ? null! : FromDto(value.ConditionGroup, context, $"{pointer}/condition_group", diagnostics),
            roll_gate = value.RollGate == null ? null! : FromDto(value.RollGate, context, $"{pointer}/roll_gate", diagnostics),
            outcome_table = value.OutcomeTable == null ? null! : FromDto(value.OutcomeTable, context, $"{pointer}/outcome_table", diagnostics),
            projected_effect_categories = MapStringNames(value.ProjectedEffectCategories),
            actions = Map(value.Actions, (item, index) => FromDto(item, context, $"{pointer}/actions/{index}", diagnostics)),
        };
    }

    internal static EquipmentAbilityReactionJsonDto ToDto(EquipmentAbilityReactionImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityReactionJsonDto
        {
            ReactionId = value.reaction_id,
            Trigger = value.trigger,
            Timing = value.timing,
            Priority = value.priority,
            OnceScope = value.once_scope,
            RequiresPlayerConfirmation = value.requires_player_confirmation,
            ConditionGroup = value.condition_group == null ? null! : ToDto(value.condition_group),
            RollGate = value.roll_gate == null ? null! : ToDto(value.roll_gate),
            OutcomeTable = value.outcome_table == null ? null! : ToDto(value.outcome_table),
            ProjectedEffectCategories = MapStringNamesToText(value.projected_effect_categories),
            Actions = Map(value.actions, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentAbilityConditionGroupImportModel FromDto(
        EquipmentAbilityConditionGroupJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityConditionGroupImportModel
        {
            mode = value.Mode ?? "",
            negate = value.Negate,
            conditions = Map(value.Conditions, (item, index) => FromDto(item, context, $"{pointer}/conditions/{index}", diagnostics)),
            groups = Map(value.Groups, (item, index) => FromDto(item, context, $"{pointer}/groups/{index}", diagnostics)),
        };
    }

    internal static EquipmentAbilityConditionGroupJsonDto ToDto(EquipmentAbilityConditionGroupImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityConditionGroupJsonDto
        {
            Mode = value.mode,
            Negate = value.negate,
            Conditions = Map(value.conditions, static (item, _) => ToDto(item)),
            Groups = Map(value.groups, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentAbilityConditionImportModel FromDto(
        EquipmentAbilityConditionJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityConditionImportModel
        {
            condition_id = value.ConditionId ?? "",
            kind = value.Kind ?? "",
            payload = FromDtoConditionPayload(value.Kind ?? "", value.Payload, context, $"{pointer}/payload", diagnostics),
        };
    }

    internal static EquipmentAbilityConditionJsonDto ToDto(EquipmentAbilityConditionImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityConditionJsonDto
        {
            ConditionId = value.condition_id,
            Kind = value.kind,
            Payload = ToDtoConditionPayload(value.payload),
        };
    }


    internal static HasStatusConditionPayloadImportModel FromDto(
        HasStatusConditionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HasStatusConditionPayloadImportModel
        {
            subject = value.Subject ?? "",
            status_id = value.StatusId ?? "",
        };
    }

    internal static HasStatusConditionPayloadJsonDto ToDto(HasStatusConditionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HasStatusConditionPayloadJsonDto
        {
            Subject = value.subject,
            StatusId = value.status_id,
        };
    }


    internal static CompareFactConditionPayloadImportModel FromDto(
        CompareFactConditionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CompareFactConditionPayloadImportModel
        {
            left = value.Left == null ? null! : FromDto(value.Left, context, $"{pointer}/left", diagnostics),
            compare = value.Compare ?? "",
            right = value.Right == null ? null! : FromDto(value.Right, context, $"{pointer}/right", diagnostics),
        };
    }

    internal static CompareFactConditionPayloadJsonDto ToDto(CompareFactConditionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CompareFactConditionPayloadJsonDto
        {
            Left = value.left == null ? null! : ToDto(value.left),
            Compare = value.compare,
            Right = value.right == null ? null! : ToDto(value.right),
        };
    }


    internal static HasEquipmentTagConditionPayloadImportModel FromDto(
        HasEquipmentTagConditionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HasEquipmentTagConditionPayloadImportModel
        {
            subject = value.Subject ?? "",
            equipment_selector = value.EquipmentSelector ?? "",
            all_tags = MapStringNames(value.AllTags),
            any_tags = MapStringNames(value.AnyTags),
        };
    }

    internal static HasEquipmentTagConditionPayloadJsonDto ToDto(HasEquipmentTagConditionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HasEquipmentTagConditionPayloadJsonDto
        {
            Subject = value.subject,
            EquipmentSelector = value.equipment_selector,
            AllTags = MapStringNamesToText(value.all_tags),
            AnyTags = MapStringNamesToText(value.any_tags),
        };
    }


    internal static EquipmentAbilityFactQueryImportModel FromDto(
        EquipmentAbilityFactQueryJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityFactQueryImportModel
        {
            query_kind = value.QueryKind ?? "",
            fact_id = value.FactId ?? "",
            subject = value.Subject ?? "",
            binding_id = value.BindingId ?? "",
            state_key = value.StateKey ?? "",
            status_id = value.StatusId ?? "",
            require_source_unit_match = value.RequireSourceUnitMatch,
            attribute_id = value.AttributeId ?? "",
            aggregation = value.Aggregation ?? "",
            value_kind = value.ValueKind ?? "",
            bool_literal = value.BoolLiteral,
            int_literal = value.IntLiteral,
            float_literal = value.FloatLiteral,
            string_name_literal = value.StringNameLiteral ?? "",
        };
    }

    internal static EquipmentAbilityFactQueryJsonDto ToDto(EquipmentAbilityFactQueryImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityFactQueryJsonDto
        {
            QueryKind = value.query_kind,
            FactId = value.fact_id,
            Subject = value.subject,
            BindingId = value.binding_id,
            StateKey = value.state_key,
            StatusId = value.status_id,
            RequireSourceUnitMatch = value.require_source_unit_match,
            AttributeId = value.attribute_id,
            Aggregation = value.aggregation,
            ValueKind = value.value_kind,
            BoolLiteral = value.bool_literal,
            IntLiteral = value.int_literal,
            FloatLiteral = value.float_literal,
            StringNameLiteral = value.string_name_literal,
        };
    }


    internal static DiceExpressionImportModel FromDto(
        DiceExpressionJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DiceExpressionImportModel
        {
            terms = Map(value.Terms, (item, index) => FromDto(item, context, $"{pointer}/terms/{index}", diagnostics)),
            flat_bonus = value.FlatBonus,
            preview_policy = value.PreviewPolicy ?? "",
        };
    }

    internal static DiceExpressionJsonDto ToDto(DiceExpressionImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DiceExpressionJsonDto
        {
            Terms = Map(value.terms, static (item, _) => ToDto(item)),
            FlatBonus = value.flat_bonus,
            PreviewPolicy = value.preview_policy,
        };
    }


    internal static DiceExpressionTermImportModel FromDto(
        DiceExpressionTermJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DiceExpressionTermImportModel
        {
            dice_count = value.DiceCount,
            dice_sides = value.DiceSides,
            count_bonus_fact = value.CountBonusFact == null ? null! : FromDto(value.CountBonusFact, context, $"{pointer}/count_bonus_fact", diagnostics),
            count_bonus_multiplier = value.CountBonusMultiplier,
            max_dice_count = value.MaxDiceCount,
        };
    }

    internal static DiceExpressionTermJsonDto ToDto(DiceExpressionTermImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DiceExpressionTermJsonDto
        {
            DiceCount = value.dice_count,
            DiceSides = value.dice_sides,
            CountBonusFact = value.count_bonus_fact == null ? null! : ToDto(value.count_bonus_fact),
            CountBonusMultiplier = value.count_bonus_multiplier,
            MaxDiceCount = value.max_dice_count,
        };
    }


    internal static EquipmentAbilityActionImportModel FromDto(
        EquipmentAbilityActionJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityActionImportModel
        {
            action_id = value.ActionId ?? "",
            kind = value.Kind ?? "",
            payload = FromDtoActionPayload(value.Kind ?? "", value.Payload, context, $"{pointer}/payload", diagnostics),
            condition_group = value.ConditionGroup == null ? null! : FromDto(value.ConditionGroup, context, $"{pointer}/condition_group", diagnostics),
            roll_gate = value.RollGate == null ? null! : FromDto(value.RollGate, context, $"{pointer}/roll_gate", diagnostics),
        };
    }

    internal static EquipmentAbilityActionJsonDto ToDto(EquipmentAbilityActionImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityActionJsonDto
        {
            ActionId = value.action_id,
            Kind = value.kind,
            Payload = ToDtoActionPayload(value.payload),
            ConditionGroup = value.condition_group == null ? null! : ToDto(value.condition_group),
            RollGate = value.roll_gate == null ? null! : ToDto(value.roll_gate),
        };
    }


    internal static AddDamageDiceActionPayloadImportModel FromDto(
        AddDamageDiceActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AddDamageDiceActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            dice = value.Dice == null ? null! : FromDto(value.Dice, context, $"{pointer}/dice", diagnostics),
            damage_type = value.DamageType ?? "",
            damage_type_mode = value.DamageTypeMode ?? "",
            require_weapon_damage = value.RequireWeaponDamage,
            subtract = value.Subtract,
            replacement_group_id = value.ReplacementGroupId ?? "",
            replacement_priority = value.ReplacementPriority,
            damage_tags = MapStringNames(value.DamageTags),
            mitigation_bypass_damage_tags = MapStringNames(value.MitigationBypassDamageTags),
            mitigation_bypass_tiers = MapStringNames(value.MitigationBypassTiers),
        };
    }

    internal static AddDamageDiceActionPayloadJsonDto ToDto(AddDamageDiceActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AddDamageDiceActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Dice = value.dice == null ? null! : ToDto(value.dice),
            DamageType = value.damage_type,
            DamageTypeMode = value.damage_type_mode,
            RequireWeaponDamage = value.require_weapon_damage,
            Subtract = value.subtract,
            ReplacementGroupId = value.replacement_group_id,
            ReplacementPriority = value.replacement_priority,
            DamageTags = MapStringNamesToText(value.damage_tags),
            MitigationBypassDamageTags = MapStringNamesToText(value.mitigation_bypass_damage_tags),
            MitigationBypassTiers = MapStringNamesToText(value.mitigation_bypass_tiers),
        };
    }


    internal static ImmediateWeaponAttackActionPayloadImportModel FromDto(
        ImmediateWeaponAttackActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ImmediateWeaponAttackActionPayloadImportModel
        {
            anchor_selector = value.AnchorSelector ?? "",
            target_team_filter = value.TargetTeamFilter ?? "",
            radius = value.Radius,
            max_attacks = value.MaxAttacks,
            skill_id = value.SkillId ?? "",
            require_weapon_range = value.RequireWeaponRange,
        };
    }

    internal static ImmediateWeaponAttackActionPayloadJsonDto ToDto(ImmediateWeaponAttackActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ImmediateWeaponAttackActionPayloadJsonDto
        {
            AnchorSelector = value.anchor_selector,
            TargetTeamFilter = value.target_team_filter,
            Radius = value.radius,
            MaxAttacks = value.max_attacks,
            SkillId = value.skill_id,
            RequireWeaponRange = value.require_weapon_range,
        };
    }


    internal static DealDamageActionPayloadImportModel FromDto(
        DealDamageActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DealDamageActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            dice = value.Dice == null ? null! : FromDto(value.Dice, context, $"{pointer}/dice", diagnostics),
            damage_type = value.DamageType ?? "",
            damage_tags = MapStringNames(value.DamageTags),
            mitigation_bypass_damage_tags = MapStringNames(value.MitigationBypassDamageTags),
            mitigation_bypass_tiers = MapStringNames(value.MitigationBypassTiers),
        };
    }

    internal static DealDamageActionPayloadJsonDto ToDto(DealDamageActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DealDamageActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Dice = value.dice == null ? null! : ToDto(value.dice),
            DamageType = value.damage_type,
            DamageTags = MapStringNamesToText(value.damage_tags),
            MitigationBypassDamageTags = MapStringNamesToText(value.mitigation_bypass_damage_tags),
            MitigationBypassTiers = MapStringNamesToText(value.mitigation_bypass_tiers),
        };
    }


    internal static HealActionPayloadImportModel FromDto(
        HealActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HealActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            dice = value.Dice == null ? null! : FromDto(value.Dice, context, $"{pointer}/dice", diagnostics),
        };
    }

    internal static HealActionPayloadJsonDto ToDto(HealActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HealActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Dice = value.dice == null ? null! : ToDto(value.dice),
        };
    }


    internal static HealFromFactActionPayloadImportModel FromDto(
        HealFromFactActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HealFromFactActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            amount_fact = value.AmountFact == null ? null! : FromDto(value.AmountFact, context, $"{pointer}/amount_fact", diagnostics),
            multiplier_percent = value.MultiplierPercent,
            max_amount = value.MaxAmount,
        };
    }

    internal static HealFromFactActionPayloadJsonDto ToDto(HealFromFactActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new HealFromFactActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            AmountFact = value.amount_fact == null ? null! : ToDto(value.amount_fact),
            MultiplierPercent = value.multiplier_percent,
            MaxAmount = value.max_amount,
        };
    }


    internal static AttackRollBonusActionPayloadImportModel FromDto(
        AttackRollBonusActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttackRollBonusActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            bonus = value.Bonus,
            attribute_modifier_id = value.AttributeModifierId ?? "",
            stack_mode = value.StackMode ?? "",
            label = value.Label,
            require_weapon_damage = value.RequireWeaponDamage,
        };
    }

    internal static AttackRollBonusActionPayloadJsonDto ToDto(AttackRollBonusActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttackRollBonusActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Bonus = value.bonus,
            AttributeModifierId = value.attribute_modifier_id,
            StackMode = value.stack_mode,
            Label = value.label,
            RequireWeaponDamage = value.require_weapon_damage,
        };
    }


    internal static AttackRollAdvantageActionPayloadImportModel FromDto(
        AttackRollAdvantageActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttackRollAdvantageActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            mode = value.Mode ?? "",
            stack_mode = value.StackMode ?? "",
            label = value.Label,
        };
    }

    internal static AttackRollAdvantageActionPayloadJsonDto ToDto(AttackRollAdvantageActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new AttackRollAdvantageActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Mode = value.mode,
            StackMode = value.stack_mode,
            Label = value.label,
        };
    }


    internal static CriticalHitOverrideActionPayloadImportModel FromDto(
        CriticalHitOverrideActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CriticalHitOverrideActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            require_weapon_damage = value.RequireWeaponDamage,
            label = value.Label,
        };
    }

    internal static CriticalHitOverrideActionPayloadJsonDto ToDto(CriticalHitOverrideActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CriticalHitOverrideActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            RequireWeaponDamage = value.require_weapon_damage,
            Label = value.label,
        };
    }


    internal static DamageRollModeOverrideActionPayloadImportModel FromDto(
        DamageRollModeOverrideActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DamageRollModeOverrideActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            roll_mode = value.RollMode ?? "",
            stack_mode = value.StackMode ?? "",
            label = value.Label,
        };
    }

    internal static DamageRollModeOverrideActionPayloadJsonDto ToDto(DamageRollModeOverrideActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DamageRollModeOverrideActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            RollMode = value.roll_mode,
            StackMode = value.stack_mode,
            Label = value.label,
        };
    }


    internal static DamageReductionActionPayloadImportModel FromDto(
        DamageReductionActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DamageReductionActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            amount = value.Amount,
            damage_tags = MapStringNames(value.DamageTags),
            label = value.Label,
        };
    }

    internal static DamageReductionActionPayloadJsonDto ToDto(DamageReductionActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new DamageReductionActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Amount = value.amount,
            DamageTags = MapStringNamesToText(value.damage_tags),
            Label = value.label,
        };
    }


    internal static GrantMitigationTierActionPayloadImportModel FromDto(
        GrantMitigationTierActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new GrantMitigationTierActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            mitigation_tier = value.MitigationTier ?? "",
            damage_tags = MapStringNames(value.DamageTags),
            label = value.Label ?? "",
        };
    }

    internal static GrantMitigationTierActionPayloadJsonDto ToDto(
        GrantMitigationTierActionPayloadImportModel value
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new GrantMitigationTierActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            MitigationTier = value.mitigation_tier,
            DamageTags = MapStringNamesToText(value.damage_tags),
            Label = value.label,
        };
    }

    internal static LootQuantityMultiplierActionPayloadImportModel FromDto(
        LootQuantityMultiplierActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LootQuantityMultiplierActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            multiplier_percent = value.MultiplierPercent,
            affected_drop_kinds = MapStringNames(value.AffectedDropKinds),
            any_item_tags = MapStringNames(value.AnyItemTags),
        };
    }

    internal static LootQuantityMultiplierActionPayloadJsonDto ToDto(LootQuantityMultiplierActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LootQuantityMultiplierActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            MultiplierPercent = value.multiplier_percent,
            AffectedDropKinds = MapStringNamesToText(value.affected_drop_kinds),
            AnyItemTags = MapStringNamesToText(value.any_item_tags),
        };
    }


    internal static ApplyStatusActionPayloadImportModel FromDto(
        ApplyStatusActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyStatusActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            status_id = value.StatusId ?? "",
            duration_turns = value.DurationTurns,
            duration_tu = value.DurationTu,
            stack_delta = value.StackDelta,
            stack_behavior = value.StackBehavior ?? "",
            stack_limit = value.StackLimit,
            display_label = value.DisplayLabel,
            attack_roll_penalty = value.AttackRollPenalty,
            armor_class_bonus_per_stack = value.ArmorClassBonusPerStack,
            source_bound_attack_roll_penalty = value.SourceBoundAttackRollPenalty,
            source_bound_attack_roll_penalty_min_stacks = value.SourceBoundAttackRollPenaltyMinStacks,
            source_bound_incoming_attack_roll_bonus_per_stack = value.SourceBoundIncomingAttackRollBonusPerStack,
            source_bound_incoming_attack_roll_bonus_min_stacks = value.SourceBoundIncomingAttackRollBonusMinStacks,
            override_heal_multiplier_percent = value.OverrideHealMultiplierPercent,
            heal_multiplier_percent = value.HealMultiplierPercent,
            move_point_capacity_delta = value.MovePointCapacityDelta,
            forced_move_immune = value.ForcedMoveImmune,
            damage_tag = value.DamageTag ?? "",
            damage_tags = MapStringNames(value.DamageTags),
            mitigation_tier = value.MitigationTier ?? "",
            counts_as_debuff_override = value.CountsAsDebuffOverride,
            counts_as_debuff = value.CountsAsDebuff,
            undispellable = value.Undispellable,
            dispellable_magic = value.DispellableMagic,
            dispellable_harmful_magic = value.DispellableHarmfulMagic,
            dispellable_beneficial_magic = value.DispellableBeneficialMagic,
            lock_counterattack = value.LockCounterattack,
            lock_guard = value.LockGuard,
            lock_dodge_bonus = value.LockDodgeBonus,
            tick_interval_tu = value.TickIntervalTu,
            timeline_damage_dice_count = value.TimelineDamageDiceCount,
            timeline_damage_dice_sides = value.TimelineDamageDiceSides,
            timeline_damage_flat_bonus = value.TimelineDamageFlatBonus,
            save_dc = value.SaveDc,
            save_ability = value.SaveAbility ?? "",
            save_tag = value.SaveTag ?? "",
            apply_on_save_failure = value.ApplyOnSaveFailure,
            remove_on_source_deactivated = value.RemoveOnSourceDeactivated,
        };
    }

    internal static ApplyStatusActionPayloadJsonDto ToDto(ApplyStatusActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyStatusActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            StatusId = value.status_id,
            DurationTurns = value.duration_turns,
            DurationTu = value.duration_tu,
            StackDelta = value.stack_delta,
            StackBehavior = value.stack_behavior,
            StackLimit = value.stack_limit,
            DisplayLabel = value.display_label,
            AttackRollPenalty = value.attack_roll_penalty,
            ArmorClassBonusPerStack = value.armor_class_bonus_per_stack,
            SourceBoundAttackRollPenalty = value.source_bound_attack_roll_penalty,
            SourceBoundAttackRollPenaltyMinStacks = value.source_bound_attack_roll_penalty_min_stacks,
            SourceBoundIncomingAttackRollBonusPerStack = value.source_bound_incoming_attack_roll_bonus_per_stack,
            SourceBoundIncomingAttackRollBonusMinStacks = value.source_bound_incoming_attack_roll_bonus_min_stacks,
            OverrideHealMultiplierPercent = value.override_heal_multiplier_percent,
            HealMultiplierPercent = value.heal_multiplier_percent,
            MovePointCapacityDelta = value.move_point_capacity_delta,
            ForcedMoveImmune = value.forced_move_immune,
            DamageTag = value.damage_tag,
            DamageTags = MapStringNamesToText(value.damage_tags),
            MitigationTier = value.mitigation_tier,
            CountsAsDebuffOverride = value.counts_as_debuff_override,
            CountsAsDebuff = value.counts_as_debuff,
            Undispellable = value.undispellable,
            DispellableMagic = value.dispellable_magic,
            DispellableHarmfulMagic = value.dispellable_harmful_magic,
            DispellableBeneficialMagic = value.dispellable_beneficial_magic,
            LockCounterattack = value.lock_counterattack,
            LockGuard = value.lock_guard,
            LockDodgeBonus = value.lock_dodge_bonus,
            TickIntervalTu = value.tick_interval_tu,
            TimelineDamageDiceCount = value.timeline_damage_dice_count,
            TimelineDamageDiceSides = value.timeline_damage_dice_sides,
            TimelineDamageFlatBonus = value.timeline_damage_flat_bonus,
            SaveDc = value.save_dc,
            SaveAbility = value.save_ability,
            SaveTag = value.save_tag,
            ApplyOnSaveFailure = value.apply_on_save_failure,
            RemoveOnSourceDeactivated = value.remove_on_source_deactivated,
        };
    }


    internal static ModifyActionPointsActionPayloadImportModel FromDto(
        ModifyActionPointsActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ModifyActionPointsActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            mode = value.Mode ?? "",
            amount = value.Amount,
            status_id = value.StatusId ?? "",
            display_label = value.DisplayLabel,
        };
    }

    internal static ModifyActionPointsActionPayloadJsonDto ToDto(ModifyActionPointsActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ModifyActionPointsActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            Mode = value.mode,
            Amount = value.amount,
            StatusId = value.status_id,
            DisplayLabel = value.display_label,
        };
    }


    internal static ModifyAbilityStateActionPayloadImportModel FromDto(
        ModifyAbilityStateActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ModifyAbilityStateActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            binding_id = value.BindingId ?? "",
            state_key = value.StateKey ?? "",
            operation = value.Operation ?? "",
            int_delta = value.IntDelta,
        };
    }

    internal static ModifyAbilityStateActionPayloadJsonDto ToDto(ModifyAbilityStateActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ModifyAbilityStateActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            BindingId = value.binding_id,
            StateKey = value.state_key,
            Operation = value.operation,
            IntDelta = value.int_delta,
        };
    }


    internal static MarkTargetActionPayloadImportModel FromDto(
        MarkTargetActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarkTargetActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            state_key = value.StateKey ?? "",
            stack_delta = value.StackDelta,
            remove_on_source_missing = value.RemoveOnSourceMissing,
            remove_on_target_defeated = value.RemoveOnTargetDefeated,
            unique_per_source = value.UniquePerSource,
            mirror_status_id = value.MirrorStatusId ?? "",
            mirror_status_duration_tu = value.MirrorStatusDurationTu,
            mirror_status_stack_behavior = value.MirrorStatusStackBehavior ?? "",
            mirror_status_stack_limit = value.MirrorStatusStackLimit,
            mirror_status_display_label = value.MirrorStatusDisplayLabel,
            clear_status_ids_on_replace = MapStringNames(value.ClearStatusIdsOnReplace),
        };
    }

    internal static MarkTargetActionPayloadJsonDto ToDto(MarkTargetActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new MarkTargetActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            StateKey = value.state_key,
            StackDelta = value.stack_delta,
            RemoveOnSourceMissing = value.remove_on_source_missing,
            RemoveOnTargetDefeated = value.remove_on_target_defeated,
            UniquePerSource = value.unique_per_source,
            MirrorStatusId = value.mirror_status_id,
            MirrorStatusDurationTu = value.mirror_status_duration_tu,
            MirrorStatusStackBehavior = value.mirror_status_stack_behavior,
            MirrorStatusStackLimit = value.mirror_status_stack_limit,
            MirrorStatusDisplayLabel = value.mirror_status_display_label,
            ClearStatusIdsOnReplace = MapStringNamesToText(value.clear_status_ids_on_replace),
        };
    }


    internal static ClearStatusActionPayloadImportModel FromDto(
        ClearStatusActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ClearStatusActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            status_id = value.StatusId ?? "",
            mark_binding_id = value.MarkBindingId ?? "",
            mark_state_key = value.MarkStateKey ?? "",
            require_source_unit_match = value.RequireSourceUnitMatch,
            clear_target_mark = value.ClearTargetMark,
        };
    }

    internal static ClearStatusActionPayloadJsonDto ToDto(ClearStatusActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ClearStatusActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            StatusId = value.status_id,
            MarkBindingId = value.mark_binding_id,
            MarkStateKey = value.mark_state_key,
            RequireSourceUnitMatch = value.require_source_unit_match,
            ClearTargetMark = value.clear_target_mark,
        };
    }


    internal static TriggerSkillActionPayloadImportModel FromDto(
        TriggerSkillActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TriggerSkillActionPayloadImportModel
        {
            skill_id = value.SkillId ?? "",
            skill_level = value.SkillLevel,
            target_selector = value.TargetSelector ?? "",
            merge_into_parent_result = value.MergeIntoParentResult,
            handle_target_defeat = value.HandleTargetDefeat,
            activation_log = value.ActivationLog,
            save_log_label = value.SaveLogLabel,
        };
    }

    internal static TriggerSkillActionPayloadJsonDto ToDto(TriggerSkillActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TriggerSkillActionPayloadJsonDto
        {
            SkillId = value.skill_id,
            SkillLevel = value.skill_level,
            TargetSelector = value.target_selector,
            MergeIntoParentResult = value.merge_into_parent_result,
            HandleTargetDefeat = value.handle_target_defeat,
            ActivationLog = value.activation_log,
            SaveLogLabel = value.save_log_label,
        };
    }


    internal static SummonUnitsActionPayloadImportModel FromDto(
        SummonUnitsActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SummonUnitsActionPayloadImportModel
        {
            anchor_selector = value.AnchorSelector ?? "",
            state_key = value.StateKey ?? "",
            count_dice = value.CountDice == null ? null! : FromDto(value.CountDice, context, $"{pointer}/count_dice", diagnostics),
            max_living_units = value.MaxLivingUnits,
            duration_tu = value.DurationTu,
            spawn_radius = value.SpawnRadius,
            unit_id_prefix = value.UnitIdPrefix ?? "",
            unit_display_name = value.UnitDisplayName,
            body_size_category = value.BodySizeCategory ?? "",
            control_mode = value.ControlMode ?? "",
            ai_brain_id = value.AiBrainId ?? "",
            ai_state_id = value.AiStateId ?? "",
            cognition_kind = value.CognitionKind ?? "",
            hp_max = value.HpMax,
            armor_class = value.ArmorClass,
            attack_bonus = value.AttackBonus,
            base_attack_bonus = value.BaseAttackBonus,
            action_points = value.ActionPoints,
            move_points = value.MovePoints,
            known_active_skill_ids = MapStringNames(value.KnownActiveSkillIds),
            natural_weapon_profile_type_id = value.NaturalWeaponProfileTypeId ?? "",
            natural_weapon_damage_tag = value.NaturalWeaponDamageTag ?? "",
            natural_weapon_attack_range = value.NaturalWeaponAttackRange,
            natural_weapon_damage_dice = value.NaturalWeaponDamageDice == null ? null! : FromDto(value.NaturalWeaponDamageDice, context, $"{pointer}/natural_weapon_damage_dice", diagnostics),
            natural_weapon_family = value.NaturalWeaponFamily ?? "",
            creature_type_tags = MapStringNames(value.CreatureTypeTags),
            movement_tags = MapStringNames(value.MovementTags),
        };
    }

    internal static SummonUnitsActionPayloadJsonDto ToDto(SummonUnitsActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SummonUnitsActionPayloadJsonDto
        {
            AnchorSelector = value.anchor_selector,
            StateKey = value.state_key,
            CountDice = value.count_dice == null ? null! : ToDto(value.count_dice),
            MaxLivingUnits = value.max_living_units,
            DurationTu = value.duration_tu,
            SpawnRadius = value.spawn_radius,
            UnitIdPrefix = value.unit_id_prefix,
            UnitDisplayName = value.unit_display_name,
            BodySizeCategory = value.body_size_category,
            ControlMode = value.control_mode,
            AiBrainId = value.ai_brain_id,
            AiStateId = value.ai_state_id,
            CognitionKind = value.cognition_kind,
            HpMax = value.hp_max,
            ArmorClass = value.armor_class,
            AttackBonus = value.attack_bonus,
            BaseAttackBonus = value.base_attack_bonus,
            ActionPoints = value.action_points,
            MovePoints = value.move_points,
            KnownActiveSkillIds = MapStringNamesToText(value.known_active_skill_ids),
            NaturalWeaponProfileTypeId = value.natural_weapon_profile_type_id,
            NaturalWeaponDamageTag = value.natural_weapon_damage_tag,
            NaturalWeaponAttackRange = value.natural_weapon_attack_range,
            NaturalWeaponDamageDice = value.natural_weapon_damage_dice == null ? null! : ToDto(value.natural_weapon_damage_dice),
            NaturalWeaponFamily = value.natural_weapon_family,
            CreatureTypeTags = MapStringNamesToText(value.creature_type_tags),
            MovementTags = MapStringNamesToText(value.movement_tags),
        };
    }


    internal static ConsumeSummonedUnitsActionPayloadImportModel FromDto(
        ConsumeSummonedUnitsActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ConsumeSummonedUnitsActionPayloadImportModel
        {
            source_binding_id = value.SourceBindingId ?? "",
            state_key = value.StateKey ?? "",
            count = value.Count,
            selection_mode = value.SelectionMode ?? "",
        };
    }

    internal static ConsumeSummonedUnitsActionPayloadJsonDto ToDto(ConsumeSummonedUnitsActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ConsumeSummonedUnitsActionPayloadJsonDto
        {
            SourceBindingId = value.source_binding_id,
            StateKey = value.state_key,
            Count = value.count,
            SelectionMode = value.selection_mode,
        };
    }


    internal static ConsumeStatusStacksActionPayloadImportModel FromDto(
        ConsumeStatusStacksActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ConsumeStatusStacksActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            status_id = value.StatusId ?? "",
            count = value.Count,
            require_source_unit_match = value.RequireSourceUnitMatch,
            selection_mode = value.SelectionMode ?? "",
        };
    }

    internal static ConsumeStatusStacksActionPayloadJsonDto ToDto(ConsumeStatusStacksActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ConsumeStatusStacksActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            StatusId = value.status_id,
            Count = value.count,
            RequireSourceUnitMatch = value.require_source_unit_match,
            SelectionMode = value.selection_mode,
        };
    }


    internal static SummonedUnitAttackRollModifierActionPayloadImportModel FromDto(
        SummonedUnitAttackRollModifierActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SummonedUnitAttackRollModifierActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            source_binding_id = value.SourceBindingId ?? "",
            state_key = value.StateKey ?? "",
            radius = value.Radius,
            bonus_per_unit = value.BonusPerUnit,
            max_absolute_bonus = value.MaxAbsoluteBonus,
            min_units = value.MinUnits,
            stack_mode = value.StackMode ?? "",
            label = value.Label,
        };
    }

    internal static SummonedUnitAttackRollModifierActionPayloadJsonDto ToDto(SummonedUnitAttackRollModifierActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new SummonedUnitAttackRollModifierActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            SourceBindingId = value.source_binding_id,
            StateKey = value.state_key,
            Radius = value.radius,
            BonusPerUnit = value.bonus_per_unit,
            MaxAbsoluteBonus = value.max_absolute_bonus,
            MinUnits = value.min_units,
            StackMode = value.stack_mode,
            Label = value.label,
        };
    }


    internal static EquipmentTemporalProgressModifierImportModel FromDto(
        EquipmentTemporalProgressModifierJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentTemporalProgressModifierImportModel
        {
            modifier_id = value.ModifierId ?? "",
            applies_to_action_progress = value.AppliesToActionProgress,
            applies_to_cast_progress = value.AppliesToCastProgress,
            save_dc = value.SaveDc,
            attribute_modifier_id = value.AttributeModifierId ?? "",
            success_rate_percent = value.SuccessRatePercent,
            failure_rate_percent = value.FailureRatePercent,
            label = value.Label,
        };
    }

    internal static EquipmentTemporalProgressModifierJsonDto ToDto(EquipmentTemporalProgressModifierImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentTemporalProgressModifierJsonDto
        {
            ModifierId = value.modifier_id,
            AppliesToActionProgress = value.applies_to_action_progress,
            AppliesToCastProgress = value.applies_to_cast_progress,
            SaveDc = value.save_dc,
            AttributeModifierId = value.attribute_modifier_id,
            SuccessRatePercent = value.success_rate_percent,
            FailureRatePercent = value.failure_rate_percent,
            Label = value.label,
        };
    }


    internal static EquipmentCognitionCeilingModifierImportModel FromDto(
        EquipmentCognitionCeilingModifierJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentCognitionCeilingModifierImportModel
        {
            modifier_id = value.ModifierId ?? "",
            cognition_ceiling = value.CognitionCeiling ?? "",
        };
    }

    internal static EquipmentCognitionCeilingModifierJsonDto ToDto(EquipmentCognitionCeilingModifierImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentCognitionCeilingModifierJsonDto
        {
            ModifierId = value.modifier_id,
            CognitionCeiling = value.cognition_ceiling,
        };
    }


    internal static EquipmentSlotWeightImportModel FromDto(
        EquipmentSlotWeightJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentSlotWeightImportModel
        {
            slot_id = value.SlotId ?? "",
            weight = value.Weight,
        };
    }

    internal static EquipmentSlotWeightJsonDto ToDto(EquipmentSlotWeightImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentSlotWeightJsonDto
        {
            SlotId = value.slot_id,
            Weight = value.weight,
        };
    }


    internal static EquipmentDurabilityDamageActionPayloadImportModel FromDto(
        EquipmentDurabilityDamageActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentDurabilityDamageActionPayloadImportModel
        {
            target_selector = value.TargetSelector ?? "",
            target_slots = MapStringNames(value.TargetSlots),
            slot_weights = Map(value.SlotWeights, (item, index) => FromDto(item, context, $"{pointer}/slot_weights/{index}", diagnostics)),
            required_item_tags = MapStringNames(value.RequiredItemTags),
            required_equipment_type_ids = MapStringNames(value.RequiredEquipmentTypeIds),
            durability_loss = value.DurabilityLoss,
            save_tag = value.SaveTag ?? "",
            save_dc = value.SaveDc,
            require_attack_success = value.RequireAttackSuccess,
            max_damaged_items = value.MaxDamagedItems,
            max_target_rarity = value.MaxTargetRarity,
        };
    }

    internal static EquipmentDurabilityDamageActionPayloadJsonDto ToDto(EquipmentDurabilityDamageActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentDurabilityDamageActionPayloadJsonDto
        {
            TargetSelector = value.target_selector,
            TargetSlots = MapStringNamesToText(value.target_slots),
            SlotWeights = Map(value.slot_weights, static (item, _) => ToDto(item)),
            RequiredItemTags = MapStringNamesToText(value.required_item_tags),
            RequiredEquipmentTypeIds = MapStringNamesToText(value.required_equipment_type_ids),
            DurabilityLoss = value.durability_loss,
            SaveTag = value.save_tag,
            SaveDc = value.save_dc,
            RequireAttackSuccess = value.require_attack_success,
            MaxDamagedItems = value.max_damaged_items,
            MaxTargetRarity = value.max_target_rarity,
        };
    }


    internal static EquipmentAttackDefenseModifierImportModel FromDto(
        EquipmentAttackDefenseModifierJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAttackDefenseModifierImportModel
        {
            modifier_id = value.ModifierId ?? "",
            ignored_ac_components = MapStringNames(value.IgnoredAcComponents),
            ac_component_multipliers = Map(value.AcComponentMultipliers, (item, index) => FromDto(item, context, $"{pointer}/ac_component_multipliers/{index}", diagnostics)),
            lock_dodge_bonus = value.LockDodgeBonus,
            required_target_equipment_selector = value.RequiredTargetEquipmentSelector ?? "",
            required_target_item_tags = MapStringNames(value.RequiredTargetItemTags),
            required_target_equipment_type_ids = MapStringNames(value.RequiredTargetEquipmentTypeIds),
            cover_policy = value.CoverPolicy ?? "",
            projectile_obstacle_policy = value.ProjectileObstaclePolicy ?? "",
            trace_label = value.TraceLabel ?? "",
        };
    }

    internal static EquipmentAttackDefenseModifierJsonDto ToDto(EquipmentAttackDefenseModifierImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAttackDefenseModifierJsonDto
        {
            ModifierId = value.modifier_id,
            IgnoredAcComponents = MapStringNamesToText(value.ignored_ac_components),
            AcComponentMultipliers = Map(value.ac_component_multipliers, static (item, _) => ToDto(item)),
            LockDodgeBonus = value.lock_dodge_bonus,
            RequiredTargetEquipmentSelector = value.required_target_equipment_selector,
            RequiredTargetItemTags = MapStringNamesToText(value.required_target_item_tags),
            RequiredTargetEquipmentTypeIds = MapStringNamesToText(value.required_target_equipment_type_ids),
            CoverPolicy = value.cover_policy,
            ProjectileObstaclePolicy = value.projectile_obstacle_policy,
            TraceLabel = value.trace_label,
        };
    }


    internal static EquipmentAcComponentMultiplierImportModel FromDto(
        EquipmentAcComponentMultiplierJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAcComponentMultiplierImportModel
        {
            ac_component_id = value.AcComponentId ?? "",
            multiplier_percent = value.MultiplierPercent,
            stack_mode = value.StackMode ?? "",
        };
    }

    internal static EquipmentAcComponentMultiplierJsonDto ToDto(EquipmentAcComponentMultiplierImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAcComponentMultiplierJsonDto
        {
            AcComponentId = value.ac_component_id,
            MultiplierPercent = value.multiplier_percent,
            StackMode = value.stack_mode,
        };
    }


    internal static EquipmentWeaponProfileOverlayImportModel FromDto(
        EquipmentWeaponProfileOverlayJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWeaponProfileOverlayImportModel
        {
            overlay_id = value.OverlayId ?? "",
            priority = value.Priority,
            condition_group = value.ConditionGroup == null ? null! : FromDto(value.ConditionGroup, context, $"{pointer}/condition_group", diagnostics),
            require_equipped_weapon = value.RequireEquippedWeapon,
            required_weapon_families = MapStringNames(value.RequiredWeaponFamilies),
            required_weapon_type_ids = MapStringNames(value.RequiredWeaponTypeIds),
            attack_range_delta = value.AttackRangeDelta,
            min_attack_range = value.MinAttackRange,
            max_attack_range = value.MaxAttackRange,
            one_handed_dice_overlay = value.OneHandedDiceOverlay == null ? null! : FromDto(value.OneHandedDiceOverlay, context, $"{pointer}/one_handed_dice_overlay", diagnostics),
            two_handed_dice_overlay = value.TwoHandedDiceOverlay == null ? null! : FromDto(value.TwoHandedDiceOverlay, context, $"{pointer}/two_handed_dice_overlay", diagnostics),
            physical_damage_tag_override = value.PhysicalDamageTagOverride ?? "",
            grip_override = value.GripOverride ?? "",
            uses_two_hands_override = value.UsesTwoHandsOverride,
            is_versatile_override = value.IsVersatileOverride,
        };
    }

    internal static EquipmentWeaponProfileOverlayJsonDto ToDto(EquipmentWeaponProfileOverlayImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWeaponProfileOverlayJsonDto
        {
            OverlayId = value.overlay_id,
            Priority = value.priority,
            ConditionGroup = value.condition_group == null ? null! : ToDto(value.condition_group),
            RequireEquippedWeapon = value.require_equipped_weapon,
            RequiredWeaponFamilies = MapStringNamesToText(value.required_weapon_families),
            RequiredWeaponTypeIds = MapStringNamesToText(value.required_weapon_type_ids),
            AttackRangeDelta = value.attack_range_delta,
            MinAttackRange = value.min_attack_range,
            MaxAttackRange = value.max_attack_range,
            OneHandedDiceOverlay = value.one_handed_dice_overlay == null ? null! : ToDto(value.one_handed_dice_overlay),
            TwoHandedDiceOverlay = value.two_handed_dice_overlay == null ? null! : ToDto(value.two_handed_dice_overlay),
            PhysicalDamageTagOverride = value.physical_damage_tag_override,
            GripOverride = value.grip_override,
            UsesTwoHandsOverride = value.uses_two_hands_override,
            IsVersatileOverride = value.is_versatile_override,
        };
    }


    internal static EquipmentWeaponDiceOverlayImportModel FromDto(
        EquipmentWeaponDiceOverlayJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWeaponDiceOverlayImportModel
        {
            mode = value.Mode ?? "",
            dice_count_delta = value.DiceCountDelta,
            dice_sides_override = value.DiceSidesOverride,
            flat_bonus_delta = value.FlatBonusDelta,
            dice_override = value.DiceOverride == null ? null! : FromDto(value.DiceOverride, context, $"{pointer}/dice_override", diagnostics),
        };
    }

    internal static EquipmentWeaponDiceOverlayJsonDto ToDto(EquipmentWeaponDiceOverlayImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWeaponDiceOverlayJsonDto
        {
            Mode = value.mode,
            DiceCountDelta = value.dice_count_delta,
            DiceSidesOverride = value.dice_sides_override,
            FlatBonusDelta = value.flat_bonus_delta,
            DiceOverride = value.dice_override == null ? null! : ToDto(value.dice_override),
        };
    }


    internal static EquipmentRollGateImportModel FromDto(
        EquipmentRollGateJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentRollGateImportModel
        {
            rng_stream = value.RngStream ?? "",
            roll = value.Roll == null ? null! : FromDto(value.Roll, context, $"{pointer}/roll", diagnostics),
            compare = value.Compare ?? "",
            threshold = value.Threshold,
        };
    }

    internal static EquipmentRollGateJsonDto ToDto(EquipmentRollGateImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentRollGateJsonDto
        {
            RngStream = value.rng_stream,
            Roll = value.roll == null ? null! : ToDto(value.roll),
            Compare = value.compare,
            Threshold = value.threshold,
        };
    }


    internal static EquipmentFatalInterceptImportModel FromDto(
        EquipmentFatalInterceptJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentFatalInterceptImportModel
        {
            intercept_id = value.InterceptId ?? "",
            resolution_order = value.ResolutionOrder,
            protection_priority = value.ProtectionPriority,
            usage_period_kind = value.UsagePeriodKind ?? "",
            max_attempts_per_period = value.MaxAttemptsPerPeriod,
            consume_on_attempt = value.ConsumeOnAttempt,
            roll_gate = value.RollGate == null ? null! : FromDto(value.RollGate, context, $"{pointer}/roll_gate", diagnostics),
            recovery_kind = value.RecoveryKind ?? "",
            recovery_dice = value.RecoveryDice == null ? null! : FromDto(value.RecoveryDice, context, $"{pointer}/recovery_dice", diagnostics),
            recovery_percent_basis_points = value.RecoveryPercentBasisPoints,
            success_actions = Map(value.SuccessActions, (item, index) => FromDto(item, context, $"{pointer}/success_actions/{index}", diagnostics)),
        };
    }

    internal static EquipmentFatalInterceptJsonDto ToDto(EquipmentFatalInterceptImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentFatalInterceptJsonDto
        {
            InterceptId = value.intercept_id,
            ResolutionOrder = value.resolution_order,
            ProtectionPriority = value.protection_priority,
            UsagePeriodKind = value.usage_period_kind,
            MaxAttemptsPerPeriod = value.max_attempts_per_period,
            ConsumeOnAttempt = value.consume_on_attempt,
            RollGate = value.roll_gate == null ? null! : ToDto(value.roll_gate),
            RecoveryKind = value.recovery_kind,
            RecoveryDice = value.recovery_dice == null ? null! : ToDto(value.recovery_dice),
            RecoveryPercentBasisPoints = value.recovery_percent_basis_points,
            SuccessActions = Map(value.success_actions, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentMitigationAuraImportModel FromDto(
        EquipmentMitigationAuraJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentMitigationAuraImportModel
        {
            aura_id = value.AuraId ?? "",
            radius = value.Radius,
            target_team_filter = value.TargetTeamFilter ?? "",
            damage_tag = value.DamageTag ?? "",
            mitigation_tier = value.MitigationTier ?? "",
            label = value.Label,
        };
    }

    internal static EquipmentMitigationAuraJsonDto ToDto(EquipmentMitigationAuraImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentMitigationAuraJsonDto
        {
            AuraId = value.aura_id,
            Radius = value.radius,
            TargetTeamFilter = value.target_team_filter,
            DamageTag = value.damage_tag,
            MitigationTier = value.mitigation_tier,
            Label = value.label,
        };
    }


    internal static EquipmentMovementTrailImportModel FromDto(
        EquipmentMovementTrailJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentMovementTrailImportModel
        {
            trail_id = value.TrailId ?? "",
            replacement_group_id = value.ReplacementGroupId ?? "",
            priority = value.Priority,
            required_skill_id = value.RequiredSkillId ?? "",
            duration_tu = value.DurationTu,
            target_team_filter = value.TargetTeamFilter ?? "",
            damage_dice = value.DamageDice == null ? null! : FromDto(value.DamageDice, context, $"{pointer}/damage_dice", diagnostics),
            damage_tag = value.DamageTag ?? "",
            damage_tags = MapStringNames(value.DamageTags),
            display_name = value.DisplayName,
        };
    }

    internal static EquipmentMovementTrailJsonDto ToDto(EquipmentMovementTrailImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentMovementTrailJsonDto
        {
            TrailId = value.trail_id,
            ReplacementGroupId = value.replacement_group_id,
            Priority = value.priority,
            RequiredSkillId = value.required_skill_id,
            DurationTu = value.duration_tu,
            TargetTeamFilter = value.target_team_filter,
            DamageDice = value.damage_dice == null ? null! : ToDto(value.damage_dice),
            DamageTag = value.damage_tag,
            DamageTags = MapStringNamesToText(value.damage_tags),
            DisplayName = value.display_name,
        };
    }


    internal static EquipmentOutcomeTableImportModel FromDto(
        EquipmentOutcomeTableJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentOutcomeTableImportModel
        {
            table_id = value.TableId ?? "",
            roll = value.Roll == null ? null! : FromDto(value.Roll, context, $"{pointer}/roll", diagnostics),
            entries = Map(value.Entries, (item, index) => FromDto(item, context, $"{pointer}/entries/{index}", diagnostics)),
        };
    }

    internal static EquipmentOutcomeTableJsonDto ToDto(EquipmentOutcomeTableImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentOutcomeTableJsonDto
        {
            TableId = value.table_id,
            Roll = value.roll == null ? null! : ToDto(value.roll),
            Entries = Map(value.entries, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentOutcomeEntryImportModel FromDto(
        EquipmentOutcomeEntryJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentOutcomeEntryImportModel
        {
            min_roll = value.MinRoll,
            max_roll = value.MaxRoll,
            actions = Map(value.Actions, (item, index) => FromDto(item, context, $"{pointer}/actions/{index}", diagnostics)),
        };
    }

    internal static EquipmentOutcomeEntryJsonDto ToDto(EquipmentOutcomeEntryImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentOutcomeEntryJsonDto
        {
            MinRoll = value.min_roll,
            MaxRoll = value.max_roll,
            Actions = Map(value.actions, static (item, _) => ToDto(item)),
        };
    }


    internal static EquipmentAbilityStateSchemaImportModel FromDto(
        EquipmentAbilityStateSchemaJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityStateSchemaImportModel
        {
            state_key = value.StateKey ?? "",
            owner_scope = value.OwnerScope ?? "",
            value_kind = value.ValueKind ?? "",
            initial_int_value = value.InitialIntValue,
            max_int_value = value.MaxIntValue,
            reset_timing = value.ResetTiming ?? "",
            persist_outside_battle = value.PersistOutsideBattle,
            visible_to_ui = value.VisibleToUi,
            sync_source_state_key = value.SyncSourceStateKey ?? "",
            sync_aggregation = value.SyncAggregation ?? "",
            sync_int_literal = value.SyncIntLiteral,
        };
    }

    internal static EquipmentAbilityStateSchemaJsonDto ToDto(EquipmentAbilityStateSchemaImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentAbilityStateSchemaJsonDto
        {
            StateKey = value.state_key,
            OwnerScope = value.owner_scope,
            ValueKind = value.value_kind,
            InitialIntValue = value.initial_int_value,
            MaxIntValue = value.max_int_value,
            ResetTiming = value.reset_timing,
            PersistOutsideBattle = value.persist_outside_battle,
            VisibleToUi = value.visible_to_ui,
            SyncSourceStateKey = value.sync_source_state_key,
            SyncAggregation = value.sync_aggregation,
            SyncIntLiteral = value.sync_int_literal,
        };
    }


    internal static EquipmentGrantedActionImportModel FromDto(
        EquipmentGrantedActionJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentGrantedActionImportModel
        {
            granted_action_id = value.GrantedActionId ?? "",
            granted_kind = value.GrantedKind ?? "",
            skill_id = value.SkillId ?? "",
            skill_level = value.SkillLevel,
            usage_period_kind = value.UsagePeriodKind ?? "",
            max_uses_per_period = value.MaxUsesPerPeriod,
            display_category = value.DisplayCategory ?? "",
            display_priority = value.DisplayPriority,
            availability_conditions = value.AvailabilityConditions == null ? null! : FromDto(value.AvailabilityConditions, context, $"{pointer}/availability_conditions", diagnostics),
        };
    }

    internal static EquipmentGrantedActionJsonDto ToDto(EquipmentGrantedActionImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentGrantedActionJsonDto
        {
            GrantedActionId = value.granted_action_id,
            GrantedKind = value.granted_kind,
            SkillId = value.skill_id,
            SkillLevel = value.skill_level,
            UsagePeriodKind = value.usage_period_kind,
            MaxUsesPerPeriod = value.max_uses_per_period,
            DisplayCategory = value.display_category,
            DisplayPriority = value.display_priority,
            AvailabilityConditions = value.availability_conditions == null ? null! : ToDto(value.availability_conditions),
        };
    }


    internal static EquipmentWorldEffectImportModel FromDto(
        EquipmentWorldEffectJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWorldEffectImportModel
        {
            world_effect_id = value.WorldEffectId ?? "",
            trigger = value.Trigger ?? "",
            timing = value.Timing ?? "",
            condition_group = value.ConditionGroup == null ? null! : FromDto(value.ConditionGroup, context, $"{pointer}/condition_group", diagnostics),
            actions = Map(value.Actions, (item, index) => FromDto(item, context, $"{pointer}/actions/{index}", diagnostics)),
        };
    }

    internal static EquipmentWorldEffectJsonDto ToDto(EquipmentWorldEffectImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new EquipmentWorldEffectJsonDto
        {
            WorldEffectId = value.world_effect_id,
            Trigger = value.trigger,
            Timing = value.timing,
            ConditionGroup = value.condition_group == null ? null! : ToDto(value.condition_group),
            Actions = Map(value.actions, static (item, _) => ToDto(item)),
        };
    }


    internal static ScheduleAreaEffectActionPayloadImportModel FromDto(
        ScheduleAreaEffectActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ScheduleAreaEffectActionPayloadImportModel
        {
            anchor_selector = value.AnchorSelector ?? "",
            delay_tu = value.DelayTu,
            terrain_effect_id = value.TerrainEffectId ?? "",
            area_pattern = value.AreaPattern ?? "",
            area_value = value.AreaValue,
            lifetime_policy = value.LifetimePolicy ?? "",
            effect_type = value.EffectType ?? "",
            target_team_filter = value.TargetTeamFilter ?? "",
            stack_behavior = value.StackBehavior ?? "",
            display_name = value.DisplayName,
            render_overlay_id = value.RenderOverlayId ?? "",
            overlay_priority = value.OverlayPriority,
            contact_status_id = value.ContactStatusId ?? "",
            contact_status_duration_tu = value.ContactStatusDurationTu,
            contact_stack_behavior = value.ContactStackBehavior ?? "",
            contact_stack_limit = value.ContactStackLimit,
            contact_status_display_label = value.ContactStatusDisplayLabel,
            contact_counts_as_debuff_override = value.ContactCountsAsDebuffOverride,
            contact_counts_as_debuff = value.ContactCountsAsDebuff,
            contact_undispellable = value.ContactUndispellable,
            contact_dispellable_magic = value.ContactDispellableMagic,
            contact_dispellable_harmful_magic = value.ContactDispellableHarmfulMagic,
            contact_dispellable_beneficial_magic = value.ContactDispellableBeneficialMagic,
            contact_save_dc = value.ContactSaveDc,
            contact_save_ability = value.ContactSaveAbility ?? "",
            contact_save_tag = value.ContactSaveTag ?? "",
            contact_apply_on_save_failure = value.ContactApplyOnSaveFailure,
            contact_tick_interval_tu = value.ContactTickIntervalTu,
            contact_timeline_damage_dice_count = value.ContactTimelineDamageDiceCount,
            contact_timeline_damage_dice_sides = value.ContactTimelineDamageDiceSides,
            contact_timeline_damage_flat_bonus = value.ContactTimelineDamageFlatBonus,
            contact_blocked_by_trait_id = value.ContactBlockedByTraitId ?? "",
        };
    }

    internal static ScheduleAreaEffectActionPayloadJsonDto ToDto(ScheduleAreaEffectActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ScheduleAreaEffectActionPayloadJsonDto
        {
            AnchorSelector = value.anchor_selector,
            DelayTu = value.delay_tu,
            TerrainEffectId = value.terrain_effect_id,
            AreaPattern = value.area_pattern,
            AreaValue = value.area_value,
            LifetimePolicy = value.lifetime_policy,
            EffectType = value.effect_type,
            TargetTeamFilter = value.target_team_filter,
            StackBehavior = value.stack_behavior,
            DisplayName = value.display_name,
            RenderOverlayId = value.render_overlay_id,
            OverlayPriority = value.overlay_priority,
            ContactStatusId = value.contact_status_id,
            ContactStatusDurationTu = value.contact_status_duration_tu,
            ContactStackBehavior = value.contact_stack_behavior,
            ContactStackLimit = value.contact_stack_limit,
            ContactStatusDisplayLabel = value.contact_status_display_label,
            ContactCountsAsDebuffOverride = value.contact_counts_as_debuff_override,
            ContactCountsAsDebuff = value.contact_counts_as_debuff,
            ContactUndispellable = value.contact_undispellable,
            ContactDispellableMagic = value.contact_dispellable_magic,
            ContactDispellableHarmfulMagic = value.contact_dispellable_harmful_magic,
            ContactDispellableBeneficialMagic = value.contact_dispellable_beneficial_magic,
            ContactSaveDc = value.contact_save_dc,
            ContactSaveAbility = value.contact_save_ability,
            ContactSaveTag = value.contact_save_tag,
            ContactApplyOnSaveFailure = value.contact_apply_on_save_failure,
            ContactTickIntervalTu = value.contact_tick_interval_tu,
            ContactTimelineDamageDiceCount = value.contact_timeline_damage_dice_count,
            ContactTimelineDamageDiceSides = value.contact_timeline_damage_dice_sides,
            ContactTimelineDamageFlatBonus = value.contact_timeline_damage_flat_bonus,
            ContactBlockedByTraitId = value.contact_blocked_by_trait_id,
        };
    }


    internal static ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel FromDto(
        ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel
        {
            anchor_selector = value.AnchorSelector ?? "",
            terrain_effect_id = value.TerrainEffectId ?? "",
            move_cost_delta = value.MoveCostDelta,
            target_team_filter = value.TargetTeamFilter ?? "",
            stack_behavior = value.StackBehavior ?? "",
            display_name = value.DisplayName,
            render_overlay_id = value.RenderOverlayId ?? "",
            overlay_priority = value.OverlayPriority,
            check_attribute_modifier_id = value.CheckAttributeModifierId ?? "",
            check_compare = value.CheckCompare ?? "",
            check_threshold = value.CheckThreshold,
            natural_twenty_auto_success = value.NaturalTwentyAutoSuccess,
            natural_one_auto_failure = value.NaturalOneAutoFailure,
        };
    }

    internal static ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto ToDto(ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto
        {
            AnchorSelector = value.anchor_selector,
            TerrainEffectId = value.terrain_effect_id,
            MoveCostDelta = value.move_cost_delta,
            TargetTeamFilter = value.target_team_filter,
            StackBehavior = value.stack_behavior,
            DisplayName = value.display_name,
            RenderOverlayId = value.render_overlay_id,
            OverlayPriority = value.overlay_priority,
            CheckAttributeModifierId = value.check_attribute_modifier_id,
            CheckCompare = value.check_compare,
            CheckThreshold = value.check_threshold,
            NaturalTwentyAutoSuccess = value.natural_twenty_auto_success,
            NaturalOneAutoFailure = value.natural_one_auto_failure,
        };
    }


    internal static ApplyEdgeFeatureActionPayloadImportModel FromDto(
        ApplyEdgeFeatureActionPayloadJsonDto value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyEdgeFeatureActionPayloadImportModel
        {
            from_selector = value.FromSelector ?? "",
            to_selector = value.ToSelector ?? "",
            duration_tu = value.DurationTu,
            max_active_edges = value.MaxActiveEdges,
            refresh_existing = value.RefreshExisting,
            require_adjacent = value.RequireAdjacent,
            feature_kind = value.FeatureKind ?? "",
            render_kind = value.RenderKind ?? "",
            render_layers = value.RenderLayers,
            blocks_move = value.BlocksMove,
            blocks_occupancy = value.BlocksOccupancy,
            blocks_los = value.BlocksLos,
            interaction_kind = value.InteractionKind ?? "",
            state_tag = value.StateTag ?? "",
        };
    }

    internal static ApplyEdgeFeatureActionPayloadJsonDto ToDto(ApplyEdgeFeatureActionPayloadImportModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplyEdgeFeatureActionPayloadJsonDto
        {
            FromSelector = value.from_selector,
            ToSelector = value.to_selector,
            DurationTu = value.duration_tu,
            MaxActiveEdges = value.max_active_edges,
            RefreshExisting = value.refresh_existing,
            RequireAdjacent = value.require_adjacent,
            FeatureKind = value.feature_kind,
            RenderKind = value.render_kind,
            RenderLayers = value.render_layers,
            BlocksMove = value.blocks_move,
            BlocksOccupancy = value.blocks_occupancy,
            BlocksLos = value.blocks_los,
            InteractionKind = value.interaction_kind,
            StateTag = value.state_tag,
        };
    }

    private static IReadOnlyList<TOut> Map<TIn, TOut>(IEnumerable<TIn>? values, Func<TIn, int, TOut> map)
    {
        if (values == null)
            return Array.Empty<TOut>();
        var result = new List<TOut>();
        int index = 0;
        foreach (TIn value in values)
        {
            if (value != null)
                result.Add(map(value, index));
            index += 1;
        }
        return result.AsReadOnly();
    }


    private static IReadOnlyList<string> MapStringNames(IEnumerable<string>? values)
    {
        if (values == null)
            return Array.Empty<string>();
        var result = new List<string>();
        foreach (string value in values)
            result.Add(value ?? "");
        return result.AsReadOnly();
    }

    private static IReadOnlyList<string> MapStringNamesToText(IEnumerable<string>? values)
    {
        if (values == null)
            return Array.Empty<string>();
        return new List<string>(values).AsReadOnly();
    }



    private static IEquipmentAbilityConditionPayloadImportModel FromDtoConditionPayload(
        string kind,
        object payload,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.InvalidPayload, "Payload must be an object.", context.SourceLabel, pointer));
            return new HasStatusConditionPayloadImportModel();
        }
        string payloadFailureDetail = "";
        try
        {
            switch (kind ?? "")
            {
                case "has_status":
                {
                    HasStatusConditionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.HasStatusConditionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "compare_fact":
                {
                    CompareFactConditionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.CompareFactConditionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "has_equipment_tag":
                {
                    HasEquipmentTagConditionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.HasEquipmentTagConditionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                default:
                    diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.UnknownKind, "Handler kind is not registered.", context.SourceLabel, pointer[..Math.Max(pointer.Length - 8, 0)] + "/kind", Actual: kind ?? ""));
                    break;
            }
        }
        catch (JsonException exception)
        {
            // exception.Path/Message 是唯一能指出具体是哪个属性不匹配的信息，丢掉它诊断就
            // 只剩一句"payload 不匹配"，作者无从下手。
            payloadFailureDetail = $" {exception.Path}: {exception.Message}";
        }
        diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.InvalidPayload, "Payload does not match the strict DTO registered for its kind." + payloadFailureDetail, context.SourceLabel, pointer));
        return new HasStatusConditionPayloadImportModel();
    }

    private static JsonElement ToDtoConditionPayload(IEquipmentAbilityConditionPayloadImportModel value)
    {
        return value switch
        {
            HasStatusConditionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.HasStatusConditionPayloadJsonDto),
            CompareFactConditionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.CompareFactConditionPayloadJsonDto),
            HasEquipmentTagConditionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.HasEquipmentTagConditionPayloadJsonDto),
            _ => throw new InvalidOperationException("Unknown equipment ability condition payload import type."),
        };
    }


    private static IEquipmentAbilityActionPayloadImportModel FromDtoActionPayload(
        string kind,
        object payload,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.InvalidPayload, "Payload must be an object.", context.SourceLabel, pointer));
            return new AddDamageDiceActionPayloadImportModel();
        }
        string payloadFailureDetail = "";
        try
        {
            switch (kind ?? "")
            {
                case "add_damage_dice":
                {
                    AddDamageDiceActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.AddDamageDiceActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "immediate_weapon_attack":
                {
                    ImmediateWeaponAttackActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ImmediateWeaponAttackActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "deal_damage":
                {
                    DealDamageActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.DealDamageActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "heal":
                {
                    HealActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.HealActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "heal_from_fact":
                {
                    HealFromFactActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.HealFromFactActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "attack_roll_bonus":
                {
                    AttackRollBonusActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.AttackRollBonusActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "attack_roll_advantage":
                {
                    AttackRollAdvantageActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.AttackRollAdvantageActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "critical_hit_override":
                {
                    CriticalHitOverrideActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.CriticalHitOverrideActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "attack_defense_modifier":
                {
                    EquipmentAttackDefenseModifierJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.EquipmentAttackDefenseModifierJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "damage_roll_mode_override":
                {
                    DamageRollModeOverrideActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.DamageRollModeOverrideActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "damage_reduction":
                {
                    DamageReductionActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.DamageReductionActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "grant_mitigation_tier":
                {
                    GrantMitigationTierActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.GrantMitigationTierActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "loot_quantity_multiplier":
                {
                    LootQuantityMultiplierActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.LootQuantityMultiplierActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "apply_status":
                {
                    ApplyStatusActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ApplyStatusActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "modify_action_points":
                {
                    ModifyActionPointsActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ModifyActionPointsActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "schedule_area_effect":
                {
                    ScheduleAreaEffectActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ScheduleAreaEffectActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "apply_battle_terrain_effect_after_check":
                {
                    ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "apply_edge_feature":
                {
                    ApplyEdgeFeatureActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ApplyEdgeFeatureActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "modify_ability_state":
                {
                    ModifyAbilityStateActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ModifyAbilityStateActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "mark_target":
                {
                    MarkTargetActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.MarkTargetActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "clear_status":
                {
                    ClearStatusActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ClearStatusActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "trigger_skill":
                {
                    TriggerSkillActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.TriggerSkillActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "summon_units":
                {
                    SummonUnitsActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.SummonUnitsActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "consume_summoned_units":
                {
                    ConsumeSummonedUnitsActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ConsumeSummonedUnitsActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "consume_status_stacks":
                {
                    ConsumeStatusStacksActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.ConsumeStatusStacksActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "summoned_unit_attack_roll_modifier":
                {
                    SummonedUnitAttackRollModifierActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.SummonedUnitAttackRollModifierActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                case "equipment_durability_damage":
                {
                    EquipmentDurabilityDamageActionPayloadJsonDto? typed = element.Deserialize(EquipmentAbilityJsonSerializerContext.Default.EquipmentDurabilityDamageActionPayloadJsonDto);
                    if (typed != null)
                        return FromDto(typed, context, pointer, diagnostics);
                    break;
                }
                default:
                    diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.UnknownKind, "Handler kind is not registered.", context.SourceLabel, pointer[..Math.Max(pointer.Length - 8, 0)] + "/kind", Actual: kind ?? ""));
                    break;
            }
        }
        catch (JsonException exception)
        {
            payloadFailureDetail = $" {exception.Path}: {exception.Message}";
        }
        diagnostics.Add(new ContentJsonDiagnostic(EquipmentAbilityJsonImportRules.InvalidPayload, "Payload does not match the strict DTO registered for its kind." + payloadFailureDetail, context.SourceLabel, pointer));
        return new AddDamageDiceActionPayloadImportModel();
    }

    private static JsonElement ToDtoActionPayload(IEquipmentAbilityActionPayloadImportModel value)
    {
        return value switch
        {
            AddDamageDiceActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.AddDamageDiceActionPayloadJsonDto),
            ImmediateWeaponAttackActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ImmediateWeaponAttackActionPayloadJsonDto),
            DealDamageActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.DealDamageActionPayloadJsonDto),
            HealActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.HealActionPayloadJsonDto),
            HealFromFactActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.HealFromFactActionPayloadJsonDto),
            AttackRollBonusActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.AttackRollBonusActionPayloadJsonDto),
            AttackRollAdvantageActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.AttackRollAdvantageActionPayloadJsonDto),
            CriticalHitOverrideActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.CriticalHitOverrideActionPayloadJsonDto),
            EquipmentAttackDefenseModifierImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.EquipmentAttackDefenseModifierJsonDto),
            DamageRollModeOverrideActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.DamageRollModeOverrideActionPayloadJsonDto),
            DamageReductionActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.DamageReductionActionPayloadJsonDto),
            GrantMitigationTierActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.GrantMitigationTierActionPayloadJsonDto),
            LootQuantityMultiplierActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.LootQuantityMultiplierActionPayloadJsonDto),
            ApplyStatusActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ApplyStatusActionPayloadJsonDto),
            ModifyActionPointsActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ModifyActionPointsActionPayloadJsonDto),
            ScheduleAreaEffectActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ScheduleAreaEffectActionPayloadJsonDto),
            ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto),
            ApplyEdgeFeatureActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ApplyEdgeFeatureActionPayloadJsonDto),
            ModifyAbilityStateActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ModifyAbilityStateActionPayloadJsonDto),
            MarkTargetActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.MarkTargetActionPayloadJsonDto),
            ClearStatusActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ClearStatusActionPayloadJsonDto),
            TriggerSkillActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.TriggerSkillActionPayloadJsonDto),
            SummonUnitsActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.SummonUnitsActionPayloadJsonDto),
            ConsumeSummonedUnitsActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ConsumeSummonedUnitsActionPayloadJsonDto),
            ConsumeStatusStacksActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.ConsumeStatusStacksActionPayloadJsonDto),
            SummonedUnitAttackRollModifierActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.SummonedUnitAttackRollModifierActionPayloadJsonDto),
            EquipmentDurabilityDamageActionPayloadImportModel typed => JsonSerializer.SerializeToElement(ToDto(typed), EquipmentAbilityJsonSerializerContext.Default.EquipmentDurabilityDamageActionPayloadJsonDto),
            _ => throw new InvalidOperationException("Unknown equipment ability action payload import type."),
        };
    }

}
