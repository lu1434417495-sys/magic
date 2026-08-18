#nullable enable

using System;
using System.Collections.Generic;

internal static partial class SkillCanonicalJsonSchema
{
    private static IReadOnlyList<int> DefaultWindupSkillLevelTierCaps { get; } =
        Array.AsReadOnly(new[] { 1, 1, 2, 2, 3, 0 });
    private static IReadOnlyList<int> DefaultWindupBaseWeaponDiceMultipliers { get; } =
        Array.AsReadOnly(new[] { 1, 1, 1, 1, 1, 2 });

    private static ContentCanonicalJsonValueSchema<CombatSkillImportModel> Combat =>
        CombatSchemaHolder.Value;

    internal static ContentCanonicalJsonValueSchema<CombatSkillImportModel> CombatValueSchema =>
        Combat;

    private static class CombatSchemaHolder
    {
        internal static ContentCanonicalJsonValueSchema<CombatSkillImportModel> Value { get; } =
            ContentCanonicalJsonValue.Object(BuildCombatSchema());
    }

    private static ContentCanonicalJsonObjectSchema<CombatSkillImportModel> BuildCombatSchema()
    {
        ContentCanonicalJsonValueSchema<CombatWindupImportModel> windup =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatWindupImportModel>(
                    OInt<CombatWindupImportModel>("stamina_cost_per_tier", static x => x.StaminaCostPerTier, 6),
                    OInt<CombatWindupImportModel>("weapon_dice_per_tier", static x => x.WeaponDicePerTier, 1),
                    ContentCanonicalJsonProperty<CombatWindupImportModel>.Optional(
                        "skill_level_tier_caps",
                        static x => x.SkillLevelTierCaps,
                        DefaultWindupSkillLevelTierCaps,
                        ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Int32),
                        ReadOnlyListSequenceComparer<int>.Instance
                    ),
                    ContentCanonicalJsonProperty<CombatWindupImportModel>.Optional(
                        "base_weapon_dice_multipliers",
                        static x => x.BaseWeaponDiceMultipliers,
                        DefaultWindupBaseWeaponDiceMultipliers,
                        ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Int32),
                        ReadOnlyListSequenceComparer<int>.Instance
                    )
                )
            );
        ContentCanonicalJsonValueSchema<CombatDirectionalPiercingImportModel> directional =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatDirectionalPiercingImportModel>(
                    OptionalList<CombatDirectionalPiercingImportModel, int>("base_damage_percent_curve", static x => x.BaseDamagePercentCurve, ContentCanonicalJsonValue.Int32),
                    OInt<CombatDirectionalPiercingImportModel>("successful_hit_decay_percent", static x => x.SuccessfulHitDecayPercent, 20),
                    OInt<CombatDirectionalPiercingImportModel>("minimum_damage_percent", static x => x.MinimumDamagePercent, 40),
                    OInt<CombatDirectionalPiercingImportModel>("stamina_flat_base", static x => x.StaminaFlatBase, 32),
                    OInt<CombatDirectionalPiercingImportModel>("stamina_range_square_coefficient", static x => x.StaminaRangeSquareCoefficient, 1),
                    OInt<CombatDirectionalPiercingImportModel>("stamina_strength_square_scale", static x => x.StaminaStrengthSquareScale, 100),
                    OInt<CombatDirectionalPiercingImportModel>("minimum_stamina_cost", static x => x.MinimumStaminaCost, 1),
                    OInt<CombatDirectionalPiercingImportModel>("maximum_height_delta", static x => x.MaximumHeightDelta, 1)
                )
            );
        ContentCanonicalJsonValueSchema<CombatApproachAttackImportModel> approach =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatApproachAttackImportModel>(
                    OInt<CombatApproachAttackImportModel>("maximum_path_height_delta_from_origin", static x => x.MaximumPathHeightDeltaFromOrigin)
                )
            );
        ContentCanonicalJsonValueSchema<CombatLineThroughAttackImportModel> lineThrough =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatLineThroughAttackImportModel>(
                    OInt<CombatLineThroughAttackImportModel>("maximum_weapon_range", static x => x.MaximumWeaponRange, 2),
                    OInt<CombatLineThroughAttackImportModel>("intermediate_weapon_dice_multiplier", static x => x.IntermediateWeaponDiceMultiplier, 1),
                    OptionalList<CombatLineThroughAttackImportModel, int>("primary_weapon_dice_multiplier_curve", static x => x.PrimaryWeaponDiceMultiplierCurve, ContentCanonicalJsonValue.Int32),
                    OptionalList<CombatLineThroughAttackImportModel, int>("primary_attack_roll_bonus_curve", static x => x.PrimaryAttackRollBonusCurve, ContentCanonicalJsonValue.Int32),
                    OInt<CombatLineThroughAttackImportModel>("successful_intermediate_hit_bonus_weapon_dice", static x => x.SuccessfulIntermediateHitBonusWeaponDice, 1),
                    OInt<CombatLineThroughAttackImportModel>("successful_intermediate_hit_attack_roll_bonus", static x => x.SuccessfulIntermediateHitAttackRollBonus, 1),
                    OptionalList<CombatLineThroughAttackImportModel, int>("successful_intermediate_hit_bonus_cap_curve", static x => x.SuccessfulIntermediateHitBonusCapCurve, ContentCanonicalJsonValue.Int32)
                )
            );
        ContentCanonicalJsonValueSchema<CombatSequentialLineHitImportModel> sequential =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatSequentialLineHitImportModel>(
                    OptionalList<CombatSequentialLineHitImportModel, int>("minimum_primary_distance_curve", static x => x.MinimumPrimaryDistanceCurve, ContentCanonicalJsonValue.Int32),
                    OptionalList<CombatSequentialLineHitImportModel, int>("continuation_range_curve", static x => x.ContinuationRangeCurve, ContentCanonicalJsonValue.Int32),
                    OptionalList<CombatSequentialLineHitImportModel, int>("follow_up_attack_penalty_curve", static x => x.FollowUpAttackPenaltyCurve, ContentCanonicalJsonValue.Int32)
                )
            );
        ContentCanonicalJsonValueSchema<CombatSpellReactionImportModel> spellReaction =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatSpellReactionImportModel>(
                    OName<CombatSpellReactionImportModel>("trigger_delivery_category", static x => x.TriggerDeliveryCategory, "spell"),
                    OName<CombatSpellReactionImportModel>("reaction_skill_id", static x => x.ReactionSkillId, "basic_attack"),
                    OName<CombatSpellReactionImportModel>("readiness_status_id", static x => x.ReadinessStatusId),
                    OName<CombatSpellReactionImportModel>("required_weapon_family", static x => x.RequiredWeaponFamily),
                    OEnum<CombatSpellReactionImportModel, CombatSaveAbilityImportKind>("save_ability", static x => x.SaveAbility, CombatSaveAbilityImportKind.Constitution, SkillRootCombatImportValueRules.GetWireValue),
                    OName<CombatSpellReactionImportModel>("save_tag", static x => x.SaveTag, "spell_maintenance"),
                    OInt<CombatSpellReactionImportModel>("base_save_dc", static x => x.BaseSaveDc, 10),
                    OInt<CombatSpellReactionImportModel>("hp_damage_divisor", static x => x.HpDamageDivisor, 2),
                    OptionalList<CombatSpellReactionImportModel, int>("attack_roll_bonus_by_skill_level", static x => x.AttackRollBonusBySkillLevel, ContentCanonicalJsonValue.Int32),
                    OptionalList<CombatSpellReactionImportModel, int>("save_dc_bonus_by_skill_level", static x => x.SaveDcBonusBySkillLevel, ContentCanonicalJsonValue.Int32),
                    OBool<CombatSpellReactionImportModel>("require_hp_damage", static x => x.RequireHpDamage, true),
                    OBool<CombatSpellReactionImportModel>("consume_on_trigger", static x => x.ConsumeOnTrigger, true),
                    OBool<CombatSpellReactionImportModel>("expire_on_owner_turn_start", static x => x.ExpireOnOwnerTurnStart, true)
                )
            );
        ContentCanonicalJsonValueSchema<CombatRangedWeaponReactionImportModel> rangedReaction =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatRangedWeaponReactionImportModel>(
                    OName<CombatRangedWeaponReactionImportModel>("readiness_status_id", static x => x.ReadinessStatusId),
                    OptionalList<CombatRangedWeaponReactionImportModel, SkillImportStringName>("trigger_weapon_families", static x => x.TriggerWeaponFamilies, StringName),
                    OEnum<CombatRangedWeaponReactionImportModel, DamageTagImportKind>("damage_tag", static x => x.DamageTag, DamageTagImportKind.Force, SkillRootCombatImportValueRules.GetWireValue),
                    OEnum<CombatRangedWeaponReactionImportModel, CombatSkillLevelOverrideAttackDefenseMode>("attack_defense_mode", static x => x.AttackDefenseMode, CombatSkillLevelOverrideAttackDefenseMode.Touch, SkillJsonImportValueRules.GetWireValue),
                    OptionalList<CombatRangedWeaponReactionImportModel, int>("attack_roll_bonus_by_skill_level", static x => x.AttackRollBonusBySkillLevel, ContentCanonicalJsonValue.Int32),
                    OInt<CombatRangedWeaponReactionImportModel>("consume_status_stacks", static x => x.ConsumeStatusStacks, 1),
                    OBool<CombatRangedWeaponReactionImportModel>("trigger_on_hit", static x => x.TriggerOnHit, true),
                    OBool<CombatRangedWeaponReactionImportModel>("trigger_on_miss", static x => x.TriggerOnMiss, true),
                    OBool<CombatRangedWeaponReactionImportModel>("allow_critical", static x => x.AllowCritical)
                )
            );

        ContentCanonicalJsonValueSchema<CombatSkillLevelOverrideImportModel> levelOverride =
            ContentCanonicalJsonValue.Object(BuildLevelOverrideSchema());
        ContentCanonicalJsonValueSchema<CombatCastVariantPayloadImportModel> castPayload =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatCastVariantPayloadImportModel>(
                    ONValue<CombatCastVariantPayloadImportModel, CombatCastSquare2Corner>(
                        "square2_corner", static x => x.Square2Corner,
                        SkillRootCombatImportValueRules.GetWireValue
                    )
                )
            );
        ContentCanonicalJsonValueSchema<CombatCastVariantImportModel> castVariant =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatCastVariantImportModel>(
                    ContentCanonicalJsonProperty<CombatCastVariantImportModel>.Required("variant_id", static x => x.VariantId, Identifier),
                    OText<CombatCastVariantImportModel>("display_name", static x => x.DisplayName),
                    OText<CombatCastVariantImportModel>("description", static x => x.Description),
                    OInt<CombatCastVariantImportModel>("min_skill_level", static x => x.MinSkillLevel),
                    OEnum<CombatCastVariantImportModel, CombatSkillImportTargetMode>("target_mode", static x => x.TargetMode, CombatSkillImportTargetMode.Ground, SkillJsonImportValueRules.GetWireValue),
                    OEnum<CombatCastVariantImportModel, CombatCastFootprintImportKind>("footprint_pattern", static x => x.FootprintPattern, CombatCastFootprintImportKind.Single, SkillRootCombatImportValueRules.GetWireValue),
                    OInt<CombatCastVariantImportModel>("required_coord_count", static x => x.RequiredCoordCount, 1),
                    OptionalList<CombatCastVariantImportModel, BattleTerrainImportKind>("allowed_base_terrains", static x => x.AllowedBaseTerrains, ContentCanonicalJsonValue.StableBusinessString<BattleTerrainImportKind>(SkillRootCombatImportValueRules.GetWireValue)),
                    OEnum<CombatCastVariantImportModel, CombatProjectileImportKind>("projectile_kind_override", static x => x.ProjectileKindOverride, CombatProjectileImportKind.Inherit, SkillRootCombatImportValueRules.GetWireValue),
                    OptionalList<CombatCastVariantImportModel, CombatEffectImportModel>("effect_defs", static x => x.EffectDefs, Effect),
                    ContentCanonicalJsonProperty<CombatCastVariantImportModel>.Optional<CombatCastVariantPayloadImportModel>(
                        "payload",
                        static x => x.Payload.Square2Corner.HasValue ? x.Payload : null!,
                        null!,
                        castPayload
                    )
                )
            );

        return new ContentCanonicalJsonObjectSchema<CombatSkillImportModel>(
            ContentCanonicalJsonProperty<CombatSkillImportModel>.Required("skill_id", static x => x.SkillId, Identifier),
            OEnum<CombatSkillImportModel, CombatSkillImportTargetMode>("target_mode", static x => x.TargetMode, CombatSkillImportTargetMode.Unit, SkillJsonImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatSkillImportTargetTeamFilter>("target_team_filter", static x => x.TargetTeamFilter, CombatSkillImportTargetTeamFilter.Enemy, SkillJsonImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatSkillImportRangePattern>("range_pattern", static x => x.RangePattern, CombatSkillImportRangePattern.Single, SkillJsonImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("range_value", static x => x.RangeValue, 1),
            OEnum<CombatSkillImportModel, CombatSkillImportAreaPattern>("area_pattern", static x => x.AreaPattern, CombatSkillImportAreaPattern.Single, SkillJsonImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("ap_cost", static x => x.ApCost, 1),
            OInt<CombatSkillImportModel>("mp_cost", static x => x.MpCost),
            OInt<CombatSkillImportModel>("cooldown_tu", static x => x.CooldownTu),
            OptionalList<CombatSkillImportModel, CombatEffectImportModel>("effect_defs", static x => x.EffectDefs, Effect),
            OptionalMap<CombatSkillImportModel, IReadOnlyDictionary<int, CombatSkillLevelOverrideImportModel>, int, CombatSkillLevelOverrideImportModel>("level_overrides", static x => x.LevelOverrides, static x => x, ContentCanonicalJsonKey.InvariantInt32, ContentCanonicalJsonKey.InvariantInt32Order, levelOverride),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("excluded_target_creature_type_tags", static x => x.ExcludedTargetCreatureTypeTags, StringName),
            OInt<CombatSkillImportModel>("range_move_point_capacity_multiplier", static x => x.RangeMovePointCapacityMultiplier),
            OEnum<CombatSkillImportModel, CombatWeaponRangePolicyImportKind>("weapon_range_policy", static x => x.WeaponRangePolicy, CombatWeaponRangePolicyImportKind.CurrentWeapon, SkillRootCombatImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("area_value", static x => x.AreaValue),
            OBool<CombatSkillImportModel>("requires_los", static x => x.RequiresLos),
            OBool<CombatSkillImportModel>("ground_effect_require_full_area", static x => x.GroundEffectRequireFullArea),
            OBool<CombatSkillImportModel>("ground_effect_require_empty", static x => x.GroundEffectRequireEmpty),
            OBool<CombatSkillImportModel>("ground_effect_require_traversable", static x => x.GroundEffectRequireTraversable),
            OInt<CombatSkillImportModel>("stamina_cost", static x => x.StaminaCost),
            OInt<CombatSkillImportModel>("mp_cost_per_target_slot", static x => x.MpCostPerTargetSlot),
            OInt<CombatSkillImportModel>("stamina_cost_per_target_slot", static x => x.StaminaCostPerTargetSlot),
            OInt<CombatSkillImportModel>("casting_time_tu", static x => x.CastingTimeTu),
            OInt<CombatSkillImportModel>("casting_maintenance_dc", static x => x.CastingMaintenanceDc),
            OInt<CombatSkillImportModel>("casting_spell_control_dc", static x => x.CastingSpellControlDc),
            OObject<CombatSkillImportModel, CombatWindupImportModel>("windup_profile", static x => x.WindupProfile, windup),
            OObject<CombatSkillImportModel, CombatDirectionalPiercingImportModel>("directional_piercing_profile", static x => x.DirectionalPiercingProfile, directional),
            OObject<CombatSkillImportModel, CombatApproachAttackImportModel>("approach_attack_profile", static x => x.ApproachAttackProfile, approach),
            OObject<CombatSkillImportModel, CombatLineThroughAttackImportModel>("line_through_attack_profile", static x => x.LineThroughAttackProfile, lineThrough),
            OObject<CombatSkillImportModel, CombatSequentialLineHitImportModel>("sequential_line_hit_profile", static x => x.SequentialLineHitProfile, sequential),
            OObject<CombatSkillImportModel, CombatSpellReactionImportModel>("spell_reaction_profile", static x => x.SpellReactionProfile, spellReaction),
            OObject<CombatSkillImportModel, CombatRangedWeaponReactionImportModel>("ranged_weapon_reaction_profile", static x => x.RangedWeaponReactionProfile, rangedReaction),
            OEnum<CombatSkillImportModel, PendingCastBindingModeKind>("pending_cast_binding_mode", static x => x.PendingCastBindingMode, PendingCastBindingModeKind.SoftAnchor, SkillJsonImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("attack_roll_bonus", static x => x.AttackRollBonus),
            OEnum<CombatSkillImportModel, CombatSkillLevelOverrideAttackResolutionMode>("attack_resolution_mode", static x => x.AttackResolutionMode, CombatSkillLevelOverrideAttackResolutionMode.Auto, SkillJsonImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatSkillLevelOverrideAttackDefenseMode>("attack_defense_mode", static x => x.AttackDefenseMode, CombatSkillLevelOverrideAttackDefenseMode.Normal, SkillJsonImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("aura_cost", static x => x.AuraCost),
            OEnum<CombatSkillImportModel, CombatMasteryTriggerImportKind>("mastery_trigger_mode", static x => x.MasteryTriggerMode, CombatMasteryTriggerImportKind.SkillDamageDiceMax, SkillRootCombatImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatMasteryAmountImportKind>("mastery_amount_mode", static x => x.MasteryAmountMode, CombatMasteryAmountImportKind.PerTargetRank, SkillRootCombatImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("mastery_base_amount", static x => x.MasteryBaseAmount, 1),
            OEnum<CombatSkillImportModel, CombatSpellFateImportKind>("spell_fate_mode", static x => x.SpellFateMode, CombatSpellFateImportKind.None, SkillRootCombatImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatSpellCriticalImportKind>("spell_critical_mode", static x => x.SpellCriticalMode, CombatSpellCriticalImportKind.None, SkillRootCombatImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("spell_critical_mp_refund_percent", static x => x.SpellCriticalMpRefundPercent),
            OptionalList<CombatSkillImportModel, int>("fumble_protection_curve", static x => x.FumbleProtectionCurve, ContentCanonicalJsonValue.Int32),
            OInt<CombatSkillImportModel>("fumble_protection_extra_mp_percent", static x => x.FumbleProtectionExtraMpPercent, 100),
            OEnum<CombatSkillImportModel, CombatBacklashImportKind>("backlash_mode", static x => x.BacklashMode, CombatBacklashImportKind.None, SkillRootCombatImportValueRules.GetWireValue),
            ONEnum<CombatSkillImportModel, CombatSkillImportTargetTeamFilter>("backlash_target_filter", static x => x.BacklashTargetFilter, SkillJsonImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("backlash_offset_radius", static x => x.BacklashOffsetRadius),
            OEnum<CombatSkillImportModel, CombatAreaOriginImportKind>("area_origin_mode", static x => x.AreaOriginMode, CombatAreaOriginImportKind.Target, SkillRootCombatImportValueRules.GetWireValue),
            OEnum<CombatSkillImportModel, CombatAreaDirectionImportKind>("area_direction_mode", static x => x.AreaDirectionMode, CombatAreaDirectionImportKind.TargetVector, SkillRootCombatImportValueRules.GetWireValue),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("ai_tags", static x => x.AiTags, StringName),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("delivery_categories", static x => x.DeliveryCategories, StringName),
            OName<CombatSkillImportModel>("attack_roll_bonus_status_id", static x => x.AttackRollBonusStatusId),
            OInt<CombatSkillImportModel>("attack_roll_bonus_status_stack_divisor", static x => x.AttackRollBonusStatusStackDivisor),
            OEnum<CombatSkillImportModel, CombatBaseProjectileImportKind>("projectile_kind", static x => x.ProjectileKind, CombatBaseProjectileImportKind.None, SkillRootCombatImportValueRules.GetWireValue),
            OName<CombatSkillImportModel>("special_resolution_profile_id", static x => x.SpecialResolutionProfileId),
            OEnum<CombatSkillImportModel, CombatTargetSelectionImportKind>("target_selection_mode", static x => x.TargetSelectionMode, CombatTargetSelectionImportKind.SingleUnit, SkillRootCombatImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("min_target_count", static x => x.MinTargetCount, 1),
            OInt<CombatSkillImportModel>("max_target_count", static x => x.MaxTargetCount, 1),
            OBool<CombatSkillImportModel>("allow_repeat_target", static x => x.AllowRepeatTarget),
            OEnum<CombatSkillImportModel, CombatUnitTargetResolutionImportKind>("unit_target_resolution_mode", static x => x.UnitTargetResolutionMode, CombatUnitTargetResolutionImportKind.Aggregate, SkillRootCombatImportValueRules.GetWireValue),
            OInt<CombatSkillImportModel>("max_hits_per_target", static x => x.MaxHitsPerTarget),
            OInt<CombatSkillImportModel>("random_chain_attack_count", static x => x.RandomChainAttackCount),
            OBool<CombatSkillImportModel>("random_chain_continue_on_miss", static x => x.RandomChainContinueOnMiss),
            OEnum<CombatSkillImportModel, CombatSelectionOrderImportKind>("selection_order_mode", static x => x.SelectionOrderMode, CombatSelectionOrderImportKind.Stable, SkillRootCombatImportValueRules.GetWireValue),
            OptionalList<CombatSkillImportModel, CombatEffectImportModel>("passive_effect_defs", static x => x.PassiveEffectDefs, Effect),
            OptionalList<CombatSkillImportModel, CombatCastVariantImportModel>("cast_variants", static x => x.CastVariants, castVariant),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("required_weapon_families", static x => x.RequiredWeaponFamilies, StringName),
            OBool<CombatSkillImportModel>("allows_natural_weapon", static x => x.AllowsNaturalWeapon),
            OBool<CombatSkillImportModel>("requires_heavy_weapon", static x => x.RequiresHeavyWeapon),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("required_weapon_type_ids", static x => x.RequiredWeaponTypeIds, StringName),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("excluded_weapon_families", static x => x.ExcludedWeaponFamilies, StringName),
            OptionalList<CombatSkillImportModel, SkillImportStringName>("excluded_weapon_type_ids", static x => x.ExcludedWeaponTypeIds, StringName),
            OBool<CombatSkillImportModel>("requires_equipped_shield", static x => x.RequiresEquippedShield),
            OInt<CombatSkillImportModel>("mastery_low_hp_bonus_multiplier", static x => x.MasteryLowHpBonusMultiplier, 1),
            OInt<CombatSkillImportModel>("mastery_low_hp_threshold_percent", static x => x.MasteryLowHpThresholdPercent, 50)
        );
    }

    private static ContentCanonicalJsonObjectSchema<CombatSkillLevelOverrideImportModel> BuildLevelOverrideSchema() => new(
        ONInt<CombatSkillLevelOverrideImportModel>("ap_cost", static x => x.ApCost),
        ONInt<CombatSkillLevelOverrideImportModel>("mp_cost", static x => x.MpCost),
        ONInt<CombatSkillLevelOverrideImportModel>("stamina_cost", static x => x.StaminaCost),
        ONInt<CombatSkillLevelOverrideImportModel>("mp_cost_per_target_slot", static x => x.MpCostPerTargetSlot),
        ONInt<CombatSkillLevelOverrideImportModel>("stamina_cost_per_target_slot", static x => x.StaminaCostPerTargetSlot),
        ONInt<CombatSkillLevelOverrideImportModel>("aura_cost", static x => x.AuraCost),
        ONInt<CombatSkillLevelOverrideImportModel>("cooldown_tu", static x => x.CooldownTu),
        ONInt<CombatSkillLevelOverrideImportModel>("casting_time_tu", static x => x.CastingTimeTu),
        ONInt<CombatSkillLevelOverrideImportModel>("casting_maintenance_dc", static x => x.CastingMaintenanceDc),
        ONInt<CombatSkillLevelOverrideImportModel>("casting_spell_control_dc", static x => x.CastingSpellControlDc),
        ONValue<CombatSkillLevelOverrideImportModel, PendingCastBindingModeKind>("pending_cast_binding_mode", static x => x.PendingCastBindingMode, SkillJsonImportValueRules.GetWireValue),
        ONInt<CombatSkillLevelOverrideImportModel>("attack_roll_bonus", static x => x.AttackRollBonus),
        ONValue<CombatSkillLevelOverrideImportModel, CombatSkillLevelOverrideAttackResolutionMode>("attack_resolution_mode", static x => x.AttackResolutionMode, SkillJsonImportValueRules.GetWireValue),
        ONValue<CombatSkillLevelOverrideImportModel, CombatSkillLevelOverrideAttackDefenseMode>("attack_defense_mode", static x => x.AttackDefenseMode, SkillJsonImportValueRules.GetWireValue),
        ONInt<CombatSkillLevelOverrideImportModel>("area_value", static x => x.AreaValue),
        ONInt<CombatSkillLevelOverrideImportModel>("range_value", static x => x.RangeValue),
        ONValue<CombatSkillLevelOverrideImportModel, CombatSkillLevelOverrideAreaPattern>("area_pattern", static x => x.AreaPattern, SkillJsonImportValueRules.GetWireValue),
        ONInt<CombatSkillLevelOverrideImportModel>("max_target_count", static x => x.MaxTargetCount),
        ONInt<CombatSkillLevelOverrideImportModel>("random_chain_attack_count", static x => x.RandomChainAttackCount)
    );

    private static ContentCanonicalJsonProperty<T> OInt<T>(string name, Func<T, int> getter, int defaultValue = 0) =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, defaultValue, ContentCanonicalJsonValue.Int32);
    private static ContentCanonicalJsonProperty<T> ONInt<T>(string name, Func<T, int?> getter) =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, null, ContentCanonicalJsonValue.NullableValue(ContentCanonicalJsonValue.Int32));
    private static ContentCanonicalJsonProperty<T> OBool<T>(string name, Func<T, bool> getter, bool defaultValue = false) =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, defaultValue, ContentCanonicalJsonValue.Boolean);
    private static ContentCanonicalJsonProperty<T> OText<T>(string name, Func<T, string> getter, string defaultValue = "") =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, defaultValue, Text);
    private static ContentCanonicalJsonProperty<T> OName<T>(string name, Func<T, SkillImportStringName> getter, string defaultValue = "") =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, CreateName(defaultValue), StringName);
    private static ContentCanonicalJsonProperty<T> OEnum<T, TEnum>(string name, Func<T, TEnum> getter, TEnum defaultValue, Func<TEnum, string> wire) where TEnum : struct =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, defaultValue, ContentCanonicalJsonValue.StableBusinessString<TEnum>(wire));
    private static ContentCanonicalJsonProperty<T> ONEnum<T, TEnum>(string name, Func<T, TEnum?> getter, Func<TEnum, string> wire) where TEnum : struct =>
        ONValue(name, getter, wire);
    private static ContentCanonicalJsonProperty<T> ONValue<T, TEnum>(string name, Func<T, TEnum?> getter, Func<TEnum, string> wire) where TEnum : struct =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, null, ContentCanonicalJsonValue.NullableValue(ContentCanonicalJsonValue.StableBusinessString<TEnum>(wire)));
    private static ContentCanonicalJsonProperty<T> OObject<T, TValue>(string name, Func<T, TValue?> getter, ContentCanonicalJsonValueSchema<TValue> schema) where TValue : class =>
        ContentCanonicalJsonProperty<T>.Optional<TValue>(name, staticValue => getter(staticValue)!, null!, schema);

    private static SkillImportStringName CreateName(string value)
    {
        if (!SkillImportStringName.TryCreate(value, out SkillImportStringName result))
            throw new InvalidOperationException($"Invalid canonical default StringName '{value}'.");
        return result;
    }
}
