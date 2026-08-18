#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Godot;
using GdDictionary = Godot.Collections.Dictionary;

/// <summary>
/// The only Godot Resource boundary for the skill JSON import graph. Resource, Variant,
/// Godot collections, and transient JsonElement carriers are consumed synchronously here;
/// callers only receive the same fully normalized plain CLR model used by JSON authoring.
/// </summary>
internal static partial class SkillTresImportAdapter
{
    private const string InvalidResourceRule = "skill.tres.invalid_resource";

    internal static ContentImportStageResult<SkillImportModel> TryAdapt(
        JsonContentEntryContext context,
        SkillDef skill
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        if (skill == null)
            return Failure(context, "Skill Resource must not be null.", "");

        var diagnostics = new List<ContentJsonDiagnostic>();
        SkillJsonDto dto = BuildSkillDto(context, skill, diagnostics);
        if (diagnostics.Count > 0)
            return ContentImportStageResult<SkillImportModel>.Failure(diagnostics);

        return SkillJsonImportParser.NormalizeResourceSnapshot(context, dto);
    }

    private static SkillJsonDto BuildSkillDto(
        JsonContentEntryContext context,
        SkillDef skill,
        List<ContentJsonDiagnostic> diagnostics
    ) => new()
    {
        SkillId = Text(skill.skill_id),
        DisplayName = skill.display_name ?? "",
        IconId = Text(skill.icon_id),
        Description = skill.description ?? "",
        SkillType = Text(skill.skill_type),
        MaxLevel = skill.max_level,
        NonCoreMaxLevel = skill.non_core_max_level,
        DynamicMaxLevelStatId = Text(skill.dynamic_max_level_stat_id),
        DynamicMaxLevelBase = skill.dynamic_max_level_base,
        DynamicMaxLevelPerStat = skill.dynamic_max_level_per_stat,
        MasteryCurve = Copy(skill.mastery_curve),
        Tags = Texts(skill.tags),
        LearnSource = Text(skill.learn_source),
        LearnRequirements = Texts(skill.learn_requirements),
        UnlockMode = Text(skill.unlock_mode),
        KnowledgeRequirements = Texts(skill.knowledge_requirements),
        SkillLevelRequirements = IntMap(context, skill.skill_level_requirements, "/skill_level_requirements", diagnostics),
        AttributeRequirements = IntMap(context, skill.attribute_requirements, "/attribute_requirements", diagnostics),
        AchievementRequirements = Texts(skill.achievement_requirements),
        UpgradeSourceSkillIds = Texts(skill.upgrade_source_skill_ids),
        RetainSourceSkillsOnUnlock = skill.retain_source_skills_on_unlock,
        CoreSkillTransitionMode = Text(skill.core_skill_transition_mode),
        MasterySources = Texts(skill.mastery_sources),
        GrowthTier = Text(skill.growth_tier),
        AttributeGrowthProgress = StrictStringIntMap(
            context,
            skill.attribute_growth_progress,
            "/attribute_growth_progress",
            diagnostics
        ),
        PracticeTier = Text(skill.practice_tier),
        AttributeModifiers = AttributeModifiers(context, skill.attribute_modifiers, diagnostics),
        LevelDescriptionTemplate = skill.level_description_template ?? "",
        LevelDescriptionConfigs = LevelDescriptionConfigs(context, skill.level_description_configs, diagnostics),
        CombatProfile = skill.combat_profile == null
            ? null
            : BuildCombatDto(context, skill.combat_profile, diagnostics),
        ContingencyAutomationProfile = skill.contingency_automation_profile == null
            ? null
            : BuildContingencyDto(context, skill.contingency_automation_profile, diagnostics),
    };

    private static CombatSkillJsonDto BuildCombatDto(
        JsonContentEntryContext context,
        CombatSkillDef combat,
        List<ContentJsonDiagnostic> diagnostics
    ) => new()
    {
        SkillId = Text(combat.skill_id),
        TargetMode = Text(combat.target_mode),
        TargetTeamFilter = Text(combat.target_team_filter),
        ExcludedTargetCreatureTypeTags = Texts(combat.excluded_target_creature_type_tags),
        RangePattern = Text(combat.range_pattern),
        RangeValue = combat.range_value,
        RangeMovePointCapacityMultiplier = combat.range_move_point_capacity_multiplier,
        WeaponRangePolicy = Text(combat.weapon_range_policy),
        AreaPattern = Text(combat.area_pattern),
        AreaValue = combat.area_value,
        RequiresLos = combat.requires_los,
        GroundEffectRequireFullArea = combat.ground_effect_require_full_area,
        GroundEffectRequireEmpty = combat.ground_effect_require_empty,
        GroundEffectRequireTraversable = combat.ground_effect_require_traversable,
        ApCost = combat.ap_cost,
        MpCost = combat.mp_cost,
        StaminaCost = combat.stamina_cost,
        MpCostPerTargetSlot = combat.mp_cost_per_target_slot,
        StaminaCostPerTargetSlot = combat.stamina_cost_per_target_slot,
        CooldownTu = combat.cooldown_tu,
        CastingTimeTu = combat.casting_time_tu,
        CastingMaintenanceDc = combat.casting_maintenance_dc,
        CastingSpellControlDc = combat.casting_spell_control_dc,
        WindupProfile = combat.windup_profile == null ? null : Windup(combat.windup_profile),
        DirectionalPiercingProfile = combat.directional_piercing_profile == null ? null : Directional(combat.directional_piercing_profile),
        ApproachAttackProfile = combat.approach_attack_profile == null ? null : new CombatApproachAttackJsonDto { MaximumPathHeightDeltaFromOrigin = combat.approach_attack_profile.maximum_path_height_delta_from_origin },
        LineThroughAttackProfile = combat.line_through_attack_profile == null ? null : LineThrough(combat.line_through_attack_profile),
        SequentialLineHitProfile = combat.sequential_line_hit_profile == null ? null : Sequential(combat.sequential_line_hit_profile),
        SpellReactionProfile = combat.spell_reaction_profile == null ? null : SpellReaction(combat.spell_reaction_profile),
        RangedWeaponReactionProfile = combat.ranged_weapon_reaction_profile == null ? null : RangedReaction(combat.ranged_weapon_reaction_profile),
        PendingCastBindingMode = Text(combat.pending_cast_binding_mode),
        AttackRollBonus = combat.attack_roll_bonus,
        AttackResolutionMode = Text(combat.attack_resolution_mode),
        AttackDefenseMode = Text(combat.attack_defense_mode),
        AuraCost = combat.aura_cost,
        LevelOverrides = LevelOverrides(context, combat.level_overrides, diagnostics),
        MasteryTriggerMode = Text(combat.mastery_trigger_mode),
        MasteryAmountMode = Text(combat.mastery_amount_mode),
        MasteryBaseAmount = combat.mastery_base_amount,
        SpellFateMode = Text(combat.spell_fate_mode),
        SpellCriticalMode = Text(combat.spell_critical_mode),
        SpellCriticalMpRefundPercent = combat.spell_critical_mp_refund_percent,
        FumbleProtectionCurve = Copy(combat.fumble_protection_curve),
        FumbleProtectionExtraMpPercent = combat.fumble_protection_extra_mp_percent,
        BacklashMode = Text(combat.backlash_mode),
        BacklashTargetFilter = Text(combat.backlash_target_filter),
        BacklashOffsetRadius = combat.backlash_offset_radius,
        AreaOriginMode = Text(combat.area_origin_mode),
        AreaDirectionMode = Text(combat.area_direction_mode),
        AiTags = Texts(combat.ai_tags),
        DeliveryCategories = Texts(combat.delivery_categories),
        AttackRollBonusStatusId = Text(combat.attack_roll_bonus_status_id),
        AttackRollBonusStatusStackDivisor = combat.attack_roll_bonus_status_stack_divisor,
        ProjectileKind = Text(combat.projectile_kind),
        SpecialResolutionProfileId = Text(combat.special_resolution_profile_id),
        TargetSelectionMode = Text(combat.target_selection_mode),
        MinTargetCount = combat.min_target_count,
        MaxTargetCount = combat.max_target_count,
        AllowRepeatTarget = combat.allow_repeat_target,
        UnitTargetResolutionMode = Text(combat.unit_target_resolution_mode),
        MaxHitsPerTarget = combat.max_hits_per_target,
        RandomChainAttackCount = combat.random_chain_attack_count,
        RandomChainContinueOnMiss = combat.random_chain_continue_on_miss,
        SelectionOrderMode = Text(combat.selection_order_mode),
        EffectDefs = Effects(context, combat.effect_defs, "/combat_profile/effect_defs", diagnostics),
        PassiveEffectDefs = Effects(context, combat.passive_effect_defs, "/combat_profile/passive_effect_defs", diagnostics),
        CastVariants = CastVariants(context, combat.cast_variants, diagnostics),
        RequiredWeaponFamilies = Texts(combat.required_weapon_families),
        AllowsNaturalWeapon = combat.allows_natural_weapon,
        RequiresHeavyWeapon = combat.requires_heavy_weapon,
        RequiredWeaponTypeIds = Texts(combat.required_weapon_type_ids),
        ExcludedWeaponFamilies = Texts(combat.excluded_weapon_families),
        ExcludedWeaponTypeIds = Texts(combat.excluded_weapon_type_ids),
        RequiresEquippedShield = combat.requires_equipped_shield,
        MasteryLowHpBonusMultiplier = combat.mastery_low_hp_bonus_multiplier,
        MasteryLowHpThresholdPercent = combat.mastery_low_hp_threshold_percent,
    };

    private static IReadOnlyList<AttributeModifierJsonDto> AttributeModifiers(
        JsonContentEntryContext context,
        Godot.Collections.Array<Resource>? values,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<AttributeModifierJsonDto>();
        if (values == null)
            return result;
        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] is not AttributeModifier value)
            {
                AddInvalid(context, $"/attribute_modifiers/{index}", "Attribute modifier must be a non-null AttributeModifier Resource.", diagnostics);
                continue;
            }
            result.Add(new AttributeModifierJsonDto
            {
                AttributeId = Text(value.attribute_id), Mode = Text(value.mode), Value = value.value,
                ValuePerRank = value.value_per_rank, SourceType = Text(value.source_type), SourceId = Text(value.source_id),
            });
        }
        return result;
    }

    private static ContingencyAutomationJsonDto BuildContingencyDto(
        JsonContentEntryContext context,
        ContingencyAutomationDef value,
        List<ContentJsonDiagnostic> diagnostics
    ) => new()
    {
        CanBeStoredInContingency = value.can_be_stored_in_contingency,
        MinContingencySkillLevel = value.min_contingency_skill_level,
        EffectCategory = Text(value.effect_category), Tags = Texts(value.tags),
        ContingencyLoadOverride = value.contingency_load_override,
        AllowedTargetResolvers = Texts(value.allowed_target_resolvers),
        RequiresManualTargeting = value.requires_manual_targeting,
        AllowedParameterBindings = ParameterBindings(context, value.allowed_parameter_bindings, diagnostics),
    };

    private static CombatWindupJsonDto Windup(CombatWindupDef value) => new()
    {
        StaminaCostPerTier = value.stamina_cost_per_tier, WeaponDicePerTier = value.weapon_dice_per_tier,
        SkillLevelTierCaps = Copy(value.skill_level_tier_caps), BaseWeaponDiceMultipliers = Copy(value.base_weapon_dice_multipliers),
    };

    private static CombatDirectionalPiercingJsonDto Directional(CombatDirectionalPiercingDef value) => new()
    {
        BaseDamagePercentCurve = Copy(value.base_damage_percent_curve), SuccessfulHitDecayPercent = value.successful_hit_decay_percent,
        MinimumDamagePercent = value.minimum_damage_percent, StaminaFlatBase = value.stamina_flat_base,
        StaminaRangeSquareCoefficient = value.stamina_range_square_coefficient, StaminaStrengthSquareScale = value.stamina_strength_square_scale,
        MinimumStaminaCost = value.minimum_stamina_cost, MaximumHeightDelta = value.maximum_height_delta,
    };

    private static CombatLineThroughAttackJsonDto LineThrough(CombatLineThroughAttackDef value) => new()
    {
        MaximumWeaponRange = value.maximum_weapon_range, IntermediateWeaponDiceMultiplier = value.intermediate_weapon_dice_multiplier,
        PrimaryWeaponDiceMultiplierCurve = Copy(value.primary_weapon_dice_multiplier_curve), PrimaryAttackRollBonusCurve = Copy(value.primary_attack_roll_bonus_curve),
        SuccessfulIntermediateHitBonusWeaponDice = value.successful_intermediate_hit_bonus_weapon_dice,
        SuccessfulIntermediateHitAttackRollBonus = value.successful_intermediate_hit_attack_roll_bonus,
        SuccessfulIntermediateHitBonusCapCurve = Copy(value.successful_intermediate_hit_bonus_cap_curve),
    };

    private static CombatSequentialLineHitJsonDto Sequential(CombatSequentialLineHitDef value) => new()
    {
        MinimumPrimaryDistanceCurve = Copy(value.minimum_primary_distance_curve),
        ContinuationRangeCurve = Copy(value.continuation_range_curve), FollowUpAttackPenaltyCurve = Copy(value.follow_up_attack_penalty_curve),
    };

    private static CombatSpellReactionJsonDto SpellReaction(CombatSpellReactionDef value) => new()
    {
        TriggerDeliveryCategory = Text(value.trigger_delivery_category), ReactionSkillId = Text(value.reaction_skill_id),
        ReadinessStatusId = Text(value.readiness_status_id), RequiredWeaponFamily = Text(value.required_weapon_family),
        SaveAbility = Text(value.save_ability), SaveTag = Text(value.save_tag), BaseSaveDc = value.base_save_dc,
        HpDamageDivisor = value.hp_damage_divisor, AttackRollBonusBySkillLevel = Copy(value.attack_roll_bonus_by_skill_level),
        SaveDcBonusBySkillLevel = Copy(value.save_dc_bonus_by_skill_level), RequireHpDamage = value.require_hp_damage,
        ConsumeOnTrigger = value.consume_on_trigger, ExpireOnOwnerTurnStart = value.expire_on_owner_turn_start,
    };

    private static CombatRangedWeaponReactionJsonDto RangedReaction(CombatRangedWeaponReactionDef value) => new()
    {
        ReadinessStatusId = Text(value.readiness_status_id), TriggerWeaponFamilies = Texts(value.trigger_weapon_families),
        DamageTag = Text(value.damage_tag), AttackDefenseMode = Text(value.attack_defense_mode),
        AttackRollBonusBySkillLevel = Copy(value.attack_roll_bonus_by_skill_level), ConsumeStatusStacks = value.consume_status_stacks,
        TriggerOnHit = value.trigger_on_hit, TriggerOnMiss = value.trigger_on_miss, AllowCritical = value.allow_critical,
    };

    private static IReadOnlyList<CombatCastVariantJsonDto> CastVariants(
        JsonContentEntryContext context,
        Godot.Collections.Array<CombatCastVariantDef>? values,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<CombatCastVariantJsonDto>();
        if (values == null)
            return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatCastVariantDef? value = values[index];
            string pointer = $"/combat_profile/cast_variants/{index}";
            if (value == null)
            {
                AddInvalid(context, pointer, "Cast variant must not be null.", diagnostics);
                continue;
            }
            string? corner = ReadCastCorner(context, value.@params, pointer + "/payload", diagnostics);
            result.Add(new CombatCastVariantJsonDto
            {
                VariantId = Text(value.variant_id), DisplayName = value.display_name ?? "", Description = value.description ?? "",
                MinSkillLevel = value.min_skill_level, TargetMode = Text(value.target_mode), FootprintPattern = Text(value.footprint_pattern),
                RequiredCoordCount = value.required_coord_count, AllowedBaseTerrains = Texts(value.allowed_base_terrains),
                ProjectileKindOverride = Text(value.projectile_kind_override),
                EffectDefs = Effects(context, value.effect_defs, pointer + "/effect_defs", diagnostics),
                Payload = new CombatCastVariantPayloadJsonDto { Square2Corner = corner },
            });
        }
        return result;
    }

    private static string? ReadCastCorner(JsonContentEntryContext context, GdDictionary? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        if (values == null)
        {
            AddInvalid(context, pointer, "Cast variant params dictionary must not be null.", diagnostics);
            return null;
        }
        if (values.Count == 0)
            return null;
        string? corner = null;
        bool foundCorner = false;
        foreach (Variant key in values.Keys)
        {
            if (!TryStrictText(key, out string name) || name != "square2_corner")
            {
                AddInvalid(context, pointer + "/" + Escape(name), "Unknown cast variant payload member.", diagnostics);
                continue;
            }
            if (foundCorner)
            {
                AddInvalid(context, pointer + "/square2_corner", "Duplicate canonical cast payload member.", diagnostics);
                continue;
            }
            foundCorner = true;
            Variant raw = values[key];
            if (!TryStrictText(raw, out corner))
                AddInvalid(context, pointer + "/square2_corner", "square2_corner must be a string token.", diagnostics);
        }
        return corner;
    }

    private static IReadOnlyDictionary<string, SkillLevelOverrideJsonDto> LevelOverrides(
        JsonContentEntryContext context, GdDictionary? values, List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new SortedDictionary<string, SkillLevelOverrideJsonDto>(StringComparer.Ordinal);
        if (values == null)
            return result;
        foreach (Variant rawKey in values.Keys)
        {
            if (!TryInt32(rawKey, out int level))
            {
                AddInvalid(context, "/combat_profile/level_overrides", "Level override keys must be integers.", diagnostics);
                continue;
            }
            string key = level.ToString(CultureInfo.InvariantCulture);
            Variant rawValue = values[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
            {
                AddInvalid(context, "/combat_profile/level_overrides/" + key, "Level override must be a dictionary.", diagnostics);
                continue;
            }
            GdDictionary overrideValues = rawValue.AsGodotDictionary();
            if (!result.TryAdd(key, LevelOverride(context, overrideValues, "/combat_profile/level_overrides/" + key, diagnostics)))
                AddInvalid(context, "/combat_profile/level_overrides/" + key, "Duplicate canonical level override key.", diagnostics);
        }
        return result;
    }

    private static SkillLevelOverrideJsonDto LevelOverride(JsonContentEntryContext context, GdDictionary values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var ints = new Dictionary<string, int?>(StringComparer.Ordinal);
        var texts = new Dictionary<string, string?>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.String)
            {
                string invalidKey = rawKey.VariantType == Variant.Type.StringName
                    ? rawKey.AsString()
                    : "";
                AddInvalid(
                    context,
                    invalidKey.Length == 0 ? pointer : pointer + "/" + Escape(invalidKey),
                    "Level override member names must use Variant.String.",
                    diagnostics
                );
                continue;
            }
            string key = rawKey.AsString();
            if (!seen.Add(key))
            {
                AddInvalid(context, pointer + "/" + Escape(key), "Duplicate canonical level override member.", diagnostics);
                continue;
            }
            Variant raw = values[rawKey];
            if (key is "pending_cast_binding_mode" or "attack_resolution_mode" or "attack_defense_mode" or "area_pattern")
            {
                if (!TryStrictText(raw, out string textValue)) AddInvalid(context, pointer + "/" + key, "Level override member must be a string token.", diagnostics);
                else
                {
                    if (key == "attack_resolution_mode" && textValue.Length == 0)
                        textValue = "auto";
                    else if (key == "attack_defense_mode" && textValue.Length == 0)
                        textValue = "normal";
                    texts.Add(key, textValue);
                }
            }
            else if (key is "ap_cost" or "mp_cost" or "stamina_cost" or "mp_cost_per_target_slot" or "stamina_cost_per_target_slot" or "aura_cost" or "cooldown_tu" or "casting_time_tu" or "casting_maintenance_dc" or "casting_spell_control_dc" or "attack_roll_bonus" or "area_value" or "range_value" or "max_target_count" or "random_chain_attack_count")
            {
                if (!TryInt32(raw, out int intValue)) AddInvalid(context, pointer + "/" + key, "Level override member must be an Int32 integer.", diagnostics);
                else ints.Add(key, intValue);
            }
            else AddInvalid(context, pointer + "/" + Escape(key), "Unknown level override member.", diagnostics);
        }
        int? I(string key) => ints.TryGetValue(key, out int? value) ? value : null;
        string? S(string key) => texts.TryGetValue(key, out string? value) ? value : null;
        return new SkillLevelOverrideJsonDto
        {
            ApCost = I("ap_cost"), MpCost = I("mp_cost"), StaminaCost = I("stamina_cost"), MpCostPerTargetSlot = I("mp_cost_per_target_slot"),
            StaminaCostPerTargetSlot = I("stamina_cost_per_target_slot"), AuraCost = I("aura_cost"), CooldownTu = I("cooldown_tu"),
            CastingTimeTu = I("casting_time_tu"), CastingMaintenanceDc = I("casting_maintenance_dc"), CastingSpellControlDc = I("casting_spell_control_dc"),
            PendingCastBindingMode = S("pending_cast_binding_mode"), AttackRollBonus = I("attack_roll_bonus"),
            AttackResolutionMode = S("attack_resolution_mode"), AttackDefenseMode = S("attack_defense_mode"), AreaValue = I("area_value"),
            RangeValue = I("range_value"), AreaPattern = S("area_pattern"), MaxTargetCount = I("max_target_count"), RandomChainAttackCount = I("random_chain_attack_count"),
        };
    }

    private static IReadOnlyDictionary<string, int> IntMap(JsonContentEntryContext context, GdDictionary? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (values == null) return result;
        foreach (Variant rawKey in values.Keys)
        {
            if (!TryStrictText(rawKey, out string key) || !TryInt32(values[rawKey], out int intValue))
            {
                AddInvalid(context, pointer, "Map entries must use string keys and Int32 integer values.", diagnostics);
                continue;
            }
            if (!result.TryAdd(key, intValue))
                AddInvalid(context, pointer + "/" + Escape(key), "Duplicate canonical map key.", diagnostics);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, int> StrictStringIntMap(
        JsonContentEntryContext context,
        GdDictionary? values,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (values == null)
            return result;
        foreach (Variant rawKey in values.Keys)
        {
            string key = rawKey.VariantType == Variant.Type.StringName
                ? rawKey.AsString()
                : "";
            if (rawKey.VariantType != Variant.Type.String)
            {
                AddInvalid(
                    context,
                    key.Length == 0 ? pointer : pointer + "/" + Escape(key),
                    "Map entries must use Variant.String keys.",
                    diagnostics
                );
                continue;
            }
            key = rawKey.AsString();
            if (!TryInt32(values[rawKey], out int intValue))
            {
                AddInvalid(
                    context,
                    pointer + "/" + Escape(key),
                    "Map entries must use Int32 integer values.",
                    diagnostics
                );
                continue;
            }
            if (!result.TryAdd(key, intValue))
                AddInvalid(context, pointer + "/" + Escape(key), "Duplicate canonical map key.", diagnostics);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LevelDescriptionConfigs(JsonContentEntryContext context, GdDictionary? values, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new SortedDictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        if (values == null) return result;
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.String)
            {
                AddInvalid(context, "/level_description_configs", "Description config level keys must be canonical integer strings.", diagnostics);
                continue;
            }
            string key = rawKey.AsString();
            Variant rawValue = values[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
            {
                AddInvalid(context, "/level_description_configs/" + Escape(key), "Description config must be a dictionary.", diagnostics);
                continue;
            }
            var variables = new SortedDictionary<string, string>(StringComparer.Ordinal);
            GdDictionary source = rawValue.AsGodotDictionary();
            foreach (Variant rawVariableKey in source.Keys)
            {
                if (rawVariableKey.VariantType != Variant.Type.String)
                {
                    string invalidVariableKey = rawVariableKey.VariantType == Variant.Type.StringName
                        ? rawVariableKey.AsString()
                        : "";
                    AddInvalid(
                        context,
                        "/level_description_configs/" + Escape(key)
                            + (invalidVariableKey.Length == 0 ? "" : "/" + Escape(invalidVariableKey)),
                        "Description variable keys must use Variant.String.",
                        diagnostics
                    );
                    continue;
                }
                string variableKey = rawVariableKey.AsString();
                Variant rawVariableValue = source[rawVariableKey];
                if (rawVariableValue.VariantType != Variant.Type.String)
                {
                    AddInvalid(context, "/level_description_configs/" + Escape(key) + "/" + Escape(variableKey), "Description variable values must be strings.", diagnostics);
                    continue;
                }
                string variableValue = rawVariableValue.AsString();
                if (!variables.TryAdd(variableKey, variableValue))
                    AddInvalid(context, "/level_description_configs/" + Escape(key) + "/" + Escape(variableKey), "Duplicate canonical description variable key.", diagnostics);
            }
            if (!result.TryAdd(key, variables))
                AddInvalid(context, "/level_description_configs/" + Escape(key), "Duplicate canonical description level key.", diagnostics);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, JsonElement> ParameterBindings(JsonContentEntryContext context, GdDictionary? values, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        if (values == null) return result;
        foreach (Variant rawKey in values.Keys)
        {
            if (!TryStrictText(rawKey, out string key))
            {
                AddInvalid(context, "/contingency_automation_profile/allowed_parameter_bindings", "Parameter binding keys must be strings.", diagnostics);
                continue;
            }
            if (!TryVariantJson(values[rawKey], out JsonElement element))
            {
                AddInvalid(context, "/contingency_automation_profile/allowed_parameter_bindings/" + Escape(key), "Parameter binding value must be bool, int, finite number, string, or string array.", diagnostics);
                continue;
            }
            if (!result.TryAdd(key, element))
                AddInvalid(context, "/contingency_automation_profile/allowed_parameter_bindings/" + Escape(key), "Duplicate canonical parameter binding key.", diagnostics);
        }
        return result;
    }

    private static bool TryVariantJson(Variant value, out JsonElement element)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Bool: element = JsonSerializer.SerializeToElement(value.AsBool()); return true;
            case Variant.Type.Int: element = JsonSerializer.SerializeToElement(value.AsInt64()); return true;
            case Variant.Type.Float:
                double number = value.AsDouble();
                if (double.IsFinite(number)) { element = JsonSerializer.SerializeToElement(number); return true; }
                break;
            case Variant.Type.String:
            case Variant.Type.StringName: element = JsonSerializer.SerializeToElement(value.AsString()); return true;
            case Variant.Type.Array:
                Godot.Collections.Array array = value.AsGodotArray();
                var strings = new List<string>(array.Count);
                foreach (Variant item in array)
                {
                    if (!TryStrictText(item, out string text)) { element = default; return false; }
                    strings.Add(text);
                }
                element = JsonSerializer.SerializeToElement(strings); return true;
        }
        element = default;
        return false;
    }

    private static IReadOnlyList<int> Copy(int[]? values) => values == null ? Array.Empty<int>() : Array.AsReadOnly((int[])values.Clone());
    private static IReadOnlyList<string> Texts(Godot.Collections.Array<StringName>? values)
    {
        if (values == null) return Array.Empty<string>();
        var result = new List<string>(values.Count);
        foreach (StringName value in values) result.Add(Text(value));
        return result;
    }
    private static string Text(StringName value) => value.ToString();
    private static bool TryStrictText(Variant value, out string text)
    {
        if (value.VariantType is Variant.Type.String or Variant.Type.StringName) { text = value.AsString(); return true; }
        text = ""; return false;
    }
    private static bool TryInt32(Variant value, out int result)
    {
        if (value.VariantType == Variant.Type.Int)
        {
            long raw = value.AsInt64();
            if (raw is >= int.MinValue and <= int.MaxValue)
            {
                result = (int)raw;
                return true;
            }
        }
        result = 0;
        return false;
    }
    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    private static void AddInvalid(JsonContentEntryContext context, string pointer, string message, List<ContentJsonDiagnostic> diagnostics) =>
        diagnostics.Add(new ContentJsonDiagnostic(InvalidResourceRule, message, context.SourceLabel, context.JsonPointer + pointer));
    private static ContentImportStageResult<SkillImportModel> Failure(JsonContentEntryContext context, string message, string pointer) =>
        ContentImportStageResult<SkillImportModel>.Failure(new ContentJsonDiagnostic(InvalidResourceRule, message, context.SourceLabel, context.JsonPointer + pointer));
}
