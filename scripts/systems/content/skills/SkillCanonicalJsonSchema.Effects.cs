#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

internal static partial class SkillCanonicalJsonSchema
{
    private abstract record EffectPayloadProjection(CombatEffectPayloadShape Shape);
    private sealed record EmptyPayloadProjection(EmptyCombatEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.Empty);
    private sealed record StatusPayloadProjection(StatusEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.Status);
    private sealed record HealPayloadProjection(HealEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.Heal);
    private sealed record EquipmentPayloadProjection(EquipmentDurabilityDamageEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.EquipmentDurabilityDamage);
    private sealed record RepeatPayloadProjection(RepeatAttackUntilFailEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.RepeatAttackUntilFail);
    private sealed record BarrierPayloadProjection(LayeredBarrierEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.LayeredBarrier);
    private sealed record GradedPayloadProjection(GradedSaveExecuteEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.GradedSaveExecute);
    private sealed record DispelPayloadProjection(DispelMagicEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.DispelMagic);
    private sealed record OnKillPayloadProjection(OnKillGainResourcesEffectPayloadImportModel Value)
        : EffectPayloadProjection(CombatEffectPayloadShape.OnKillGainResources);

    private static ContentCanonicalJsonValueSchema<CombatEffectImportModel> Effect =>
        EffectSchemaHolder.Value;

    internal static ContentCanonicalJsonValueSchema<CombatEffectImportModel> EffectValueSchema =>
        Effect;

    private static class EffectSchemaHolder
    {
        internal static ContentCanonicalJsonValueSchema<CombatEffectImportModel> Value { get; } =
            BuildEffectSchema();
    }

    private static ContentCanonicalJsonValueSchema<CombatEffectImportModel> BuildEffectSchema()
    {
        ContentCanonicalJsonDeferredValueSchema<CombatEffectImportModel> recursiveEffect =
            ContentCanonicalJsonValue.Deferred<CombatEffectImportModel>();

        ContentCanonicalJsonValueSchema<CombatEffectSlotWeightImportModel> slotWeight =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatEffectSlotWeightImportModel>(
                    ContentCanonicalJsonProperty<CombatEffectSlotWeightImportModel>.Required(
                        "slot_id", static x => x.SlotId,
                        ContentCanonicalJsonValue.StableBusinessString<CombatEquipmentSlotImportKind>(
                            SkillCombatEffectValueRules.GetWireValue
                        )
                    ),
                    OInt<CombatEffectSlotWeightImportModel>("weight", static x => x.Weight)
                )
            );
        ContentCanonicalJsonValueSchema<CombatDamageSegmentImportModel> damageSegment =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatDamageSegmentImportModel>(
                    ONEnum<CombatDamageSegmentImportModel, DamageTagImportKind>("damage_tag", static x => x.DamageTag, SkillRootCombatImportValueRules.GetWireValue),
                    OptionalList<CombatDamageSegmentImportModel, DamageTagImportKind>("damage_tags", static x => x.DamageTags, ContentCanonicalJsonValue.StableBusinessString<DamageTagImportKind>(SkillRootCombatImportValueRules.GetWireValue)),
                    OptionalList<CombatDamageSegmentImportModel, DamageTagImportKind>("mitigation_bypass_damage_tags", static x => x.MitigationBypassDamageTags, ContentCanonicalJsonValue.StableBusinessString<DamageTagImportKind>(SkillRootCombatImportValueRules.GetWireValue)),
                    OptionalList<CombatDamageSegmentImportModel, DamageMitigationTierImportKind>("mitigation_bypass_tiers", static x => x.MitigationBypassTiers, ContentCanonicalJsonValue.StableBusinessString<DamageMitigationTierImportKind>(SkillCombatEffectValueRules.GetWireValue)),
                    OInt<CombatDamageSegmentImportModel>("power", static x => x.Power),
                    OInt<CombatDamageSegmentImportModel>("dice_count", static x => x.DiceCount),
                    OInt<CombatDamageSegmentImportModel>("dice_sides", static x => x.DiceSides),
                    OInt<CombatDamageSegmentImportModel>("dice_bonus", static x => x.DiceBonus),
                    OBool<CombatDamageSegmentImportModel>("double_dice_on_critical", static x => x.DoubleDiceOnCritical),
                    ODouble<CombatDamageSegmentImportModel>("pre_resistance_damage_multiplier", static x => x.PreResistanceDamageMultiplier, 1.0)
                )
            );
        ContentCanonicalJsonValueSchema<CombatTargetDamageMultiplierRuleImportModel> targetMultiplier =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatTargetDamageMultiplierRuleImportModel>(
                    OptionalList<CombatTargetDamageMultiplierRuleImportModel, SkillImportStringName>("any_creature_type_tags", static x => x.AnyCreatureTypeTags, StringName),
                    OptionalList<CombatTargetDamageMultiplierRuleImportModel, SkillImportStringName>("all_creature_type_tags", static x => x.AllCreatureTypeTags, StringName),
                    OptionalList<CombatTargetDamageMultiplierRuleImportModel, SkillImportStringName>("excluded_creature_type_tags", static x => x.ExcludedCreatureTypeTags, StringName),
                    OInt<CombatTargetDamageMultiplierRuleImportModel>("multiplier_percent", static x => x.MultiplierPercent, 100)
                )
            );
        ContentCanonicalJsonValueSchema<CombatWeightedStatusOutcomeImportModel> weightedOutcome =
            ContentCanonicalJsonValue.Object(
                new ContentCanonicalJsonObjectSchema<CombatWeightedStatusOutcomeImportModel>(
                    OName<CombatWeightedStatusOutcomeImportModel>("outcome_id", static x => x.OutcomeId),
                    OInt<CombatWeightedStatusOutcomeImportModel>("weight", static x => x.Weight, 1),
                    ContentCanonicalJsonProperty<CombatWeightedStatusOutcomeImportModel>.Required(
                        "status_effect", static x => x.StatusEffect, recursiveEffect
                    )
                )
            );

        ContentCanonicalJsonValueSchema<EffectPayloadProjection> payload =
            BuildPayloadSchema();
        var schema = new ContentCanonicalJsonObjectSchema<CombatEffectImportModel>(
            ContentCanonicalJsonProperty<CombatEffectImportModel>.Required(
                "effect_type", static x => x.Kind,
                ContentCanonicalJsonValue.StableBusinessString<CombatEffectImportKind>(
                    SkillFullCombatEffectClosedSpec.GetWireValue
                )
            ),
            OInt<CombatEffectImportModel>("min_skill_level", static x => x.MinSkillLevel),
            OInt<CombatEffectImportModel>("max_skill_level", static x => x.MaxSkillLevel, -1),
            OInt<CombatEffectImportModel>("power", static x => x.Power),
            OInt<CombatEffectImportModel>("duration_tu", static x => x.DurationTu),
            ContentCanonicalJsonProperty<CombatEffectImportModel>.Required(
                "payload", ProjectEffectPayload, payload
            ),
            OEnum<CombatEffectImportModel, CombatTickEffectImportKind>("tick_effect_type", static x => x.TickEffectType, CombatTickEffectImportKind.None, SkillCombatEffectValueRules.GetWireValue),
            OEnum<CombatEffectImportModel, CombatEffectLifetimeImportKind>("lifetime_policy", static x => x.LifetimePolicy, CombatEffectLifetimeImportKind.Timed, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("heal_to_hp_percent_floor", static x => x.HealToHpPercentFloor),
            OInt<CombatEffectImportModel>("heal_missing_hp_percent", static x => x.HealMissingHpPercent),
            OInt<CombatEffectImportModel>("move_cost_delta", static x => x.MoveCostDelta),
            OName<CombatEffectImportModel>("render_overlay_id", static x => x.RenderOverlayId),
            OInt<CombatEffectImportModel>("overlay_priority", static x => x.OverlayPriority),
            OText<CombatEffectImportModel>("display_name", static x => x.DisplayName),
            OName<CombatEffectImportModel>("does_not_stack_with_status_id", static x => x.DoesNotStackWithStatusId),
            OptionalList<CombatEffectImportModel, SkillImportStringName>("does_not_stack_with_status_ids", static x => x.DoesNotStackWithStatusIds, StringName),
            OInt<CombatEffectImportModel>("damage_ratio_percent", static x => x.DamageRatioPercent, 100),
            ODouble<CombatEffectImportModel>("pre_resistance_damage_multiplier", static x => x.PreResistanceDamageMultiplier, 1.0),
            OInt<CombatEffectImportModel>("weapon_dice_multiplier", static x => x.WeaponDiceMultiplier, 1),
            OInt<CombatEffectImportModel>("bonus_weapon_dice_multiplier", static x => x.BonusWeaponDiceMultiplier),
            ONEnum<CombatEffectImportModel, DamageTagImportKind>("damage_tag", static x => x.DamageTag, SkillRootCombatImportValueRules.GetWireValue),
            OptionalList<CombatEffectImportModel, DamageTagImportKind>("damage_tags", static x => x.DamageTags, ContentCanonicalJsonValue.StableBusinessString<DamageTagImportKind>(SkillRootCombatImportValueRules.GetWireValue)),
            OptionalList<CombatEffectImportModel, DamageTagImportKind>("mitigation_bypass_damage_tags", static x => x.MitigationBypassDamageTags, ContentCanonicalJsonValue.StableBusinessString<DamageTagImportKind>(SkillRootCombatImportValueRules.GetWireValue)),
            OptionalList<CombatEffectImportModel, DamageMitigationTierImportKind>("mitigation_bypass_tiers", static x => x.MitigationBypassTiers, ContentCanonicalJsonValue.StableBusinessString<DamageMitigationTierImportKind>(SkillCombatEffectValueRules.GetWireValue)),
            ONEnum<CombatEffectImportModel, DamageCategoryImportKind>("damage_category", static x => x.DamageCategory, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("dr_bypass_tag", static x => x.DrBypassTag),
            OInt<CombatEffectImportModel>("hp_ratio_threshold_percent", static x => x.HpRatioThresholdPercent),
            OInt<CombatEffectImportModel>("dice_count", static x => x.DiceCount),
            OInt<CombatEffectImportModel>("dice_sides", static x => x.DiceSides),
            OInt<CombatEffectImportModel>("dice_bonus", static x => x.DiceBonus),
            OInt<CombatEffectImportModel>("dice_sides_base", static x => x.DiceSidesBase),
            OInt<CombatEffectImportModel>("dice_sides_per_constitution_mod", static x => x.DiceSidesPerConstitutionMod),
            OInt<CombatEffectImportModel>("dice_sides_per_willpower_mod", static x => x.DiceSidesPerWillpowerMod),
            OName<CombatEffectImportModel>("shield_family", static x => x.ShieldFamily),
            ONEnum<CombatEffectImportModel, ShieldAttributeModifierImportKind>("shield_attribute_modifier_id", static x => x.ShieldAttributeModifierId, SkillCombatEffectValueRules.GetWireValue),
            OBool<CombatEffectImportModel>("shield_roll_per_target", static x => x.ShieldRollPerTarget),
            OInt<CombatEffectImportModel>("bonus_damage_dice_count", static x => x.BonusDamageDiceCount),
            OInt<CombatEffectImportModel>("bonus_damage_dice_sides", static x => x.BonusDamageDiceSides),
            OInt<CombatEffectImportModel>("bonus_damage_dice_bonus", static x => x.BonusDamageDiceBonus),
            OBool<CombatEffectImportModel>("bonus_damage_separate_event", static x => x.BonusDamageSeparateEvent),
            OInt<CombatEffectImportModel>("source_bound_weapon_bonus_damage_dice_count", static x => x.SourceBoundWeaponBonusDamageDiceCount),
            OInt<CombatEffectImportModel>("source_bound_weapon_bonus_damage_dice_sides", static x => x.SourceBoundWeaponBonusDamageDiceSides),
            OInt<CombatEffectImportModel>("source_bound_weapon_bonus_damage_dice_bonus", static x => x.SourceBoundWeaponBonusDamageDiceBonus),
            OBool<CombatEffectImportModel>("add_weapon_dice", static x => x.AddWeaponDice),
            OBool<CombatEffectImportModel>("requires_weapon", static x => x.RequiresWeapon),
            OBool<CombatEffectImportModel>("use_weapon_physical_damage_tag", static x => x.UseWeaponPhysicalDamageTag),
            OBool<CombatEffectImportModel>("resolve_as_weapon_attack", static x => x.ResolveAsWeaponAttack),
            OBool<CombatEffectImportModel>("allow_repeat_hits_across_steps", static x => x.AllowRepeatHitsAcrossSteps),
            OEnum<CombatEffectImportModel, CombatPathStepAreaPatternImportKind>("path_step_area_pattern", static x => x.PathStepAreaPattern, CombatPathStepAreaPatternImportKind.Diamond, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("path_step_radius", static x => x.PathStepRadius, 1),
            OText<CombatEffectImportModel>("path_step_log_label", static x => x.PathStepLogLabel),
            OName<CombatEffectImportModel>("repeat_hit_status_id", static x => x.RepeatHitStatusId),
            OInt<CombatEffectImportModel>("repeat_hit_status_threshold", static x => x.RepeatHitStatusThreshold),
            OInt<CombatEffectImportModel>("repeat_hit_status_min_skill_level", static x => x.RepeatHitStatusMinSkillLevel),
            OInt<CombatEffectImportModel>("repeat_hit_status_power", static x => x.RepeatHitStatusPower, 1),
            OInt<CombatEffectImportModel>("repeat_hit_status_duration_tu", static x => x.RepeatHitStatusDurationTu),
            OText<CombatEffectImportModel>("repeat_hit_status_log_template", static x => x.RepeatHitStatusLogTemplate),
            OBool<CombatEffectImportModel>("prevent_repeat_target", static x => x.PreventRepeatTarget, true),
            OInt<CombatEffectImportModel>("chain_base_hop_range", static x => x.ChainBaseHopRange),
            OInt<CombatEffectImportModel>("chain_conductive_hop_range", static x => x.ChainConductiveHopRange),
            OInt<CombatEffectImportModel>("chain_max_total_targets", static x => x.ChainMaxTotalTargets),
            OptionalList<CombatEffectImportModel, SkillImportStringName>("chain_conductive_status_ids", static x => x.ChainConductiveStatusIds, StringName),
            OptionalList<CombatEffectImportModel, SkillImportStringName>("chain_conductive_terrain_effect_ids", static x => x.ChainConductiveTerrainEffectIds, StringName),
            OInt<CombatEffectImportModel>("chain_backlash_hop_range_bonus", static x => x.ChainBacklashHopRangeBonus),
            OBool<CombatEffectImportModel>("stop_on_miss", static x => x.StopOnMiss, true),
            OBool<CombatEffectImportModel>("stop_on_target_down", static x => x.StopOnTargetDown, true),
            OInt<CombatEffectImportModel>("fixed_attack_count", static x => x.FixedAttackCount),
            OInt<CombatEffectImportModel>("follow_up_damage_multiplier_percent", static x => x.FollowUpDamageMultiplierPercent, 100),
            OptionalList<CombatEffectImportModel, int>("follow_up_attack_roll_bonus_curve", static x => x.FollowUpAttackRollBonusCurve, ContentCanonicalJsonValue.Int32),
            OBool<CombatEffectImportModel>("remove_harmful", static x => x.RemoveHarmful),
            OBool<CombatEffectImportModel>("remove_harmful_from_allies", static x => x.RemoveHarmfulFromAllies, true),
            OBool<CombatEffectImportModel>("remove_beneficial", static x => x.RemoveBeneficial),
            OBool<CombatEffectImportModel>("remove_beneficial_from_enemies", static x => x.RemoveBeneficialFromEnemies, true),
            OBool<CombatEffectImportModel>("require_damage_applied", static x => x.RequireDamageApplied),
            OInt<CombatEffectImportModel>("max_status_removed", static x => x.MaxStatusRemoved),
            OInt<CombatEffectImportModel>("min_hp_after_damage", static x => x.MinHpAfterDamage, 1),
            OInt<CombatEffectImportModel>("death_prevention_priority", static x => x.DeathPreventionPriority),
            OInt<CombatEffectImportModel>("threshold_base_value", static x => x.ThresholdBaseValue),
            OInt<CombatEffectImportModel>("threshold_level_anchor", static x => x.ThresholdLevelAnchor, 17),
            OInt<CombatEffectImportModel>("threshold_level_bonus_per_delta", static x => x.ThresholdLevelBonusPerDelta, 5),
            OInt<CombatEffectImportModel>("threshold_max_hp_ratio_percent", static x => x.ThresholdMaxHpRatioPercent, 20),
            OInt<CombatEffectImportModel>("threshold_cap_max_hp_ratio_percent", static x => x.ThresholdCapMaxHpRatioPercent, 50),
            OInt<CombatEffectImportModel>("soul_fracture_duration_tu", static x => x.SoulFractureDurationTu),
            OInt<CombatEffectImportModel>("heal_multiplier_percent", static x => x.HealMultiplierPercent, 100),
            OInt<CombatEffectImportModel>("shield_gain_multiplier_percent", static x => x.ShieldGainMultiplierPercent, 100),
            OInt<CombatEffectImportModel>("attack_roll_penalty", static x => x.AttackRollPenalty, -1),
            OInt<CombatEffectImportModel>("attack_roll_bonus", static x => x.AttackRollBonus),
            OBool<CombatEffectImportModel>("attack_roll_advantage", static x => x.AttackRollAdvantage),
            OBool<CombatEffectImportModel>("consume_on_next_attack_check", static x => x.ConsumeOnNextAttackCheck),
            OBool<CombatEffectImportModel>("consume_on_next_save", static x => x.ConsumeOnNextSave),
            OBool<CombatEffectImportModel>("undispellable", static x => x.Undispellable),
            OBool<CombatEffectImportModel>("dispellable_magic", static x => x.DispellableMagic),
            OBool<CombatEffectImportModel>("dispellable_harmful_magic", static x => x.DispellableHarmfulMagic),
            OBool<CombatEffectImportModel>("dispellable_beneficial_magic", static x => x.DispellableBeneficialMagic),
            ONEnum<CombatEffectImportModel, DamageMitigationTierImportKind>("mitigation_tier", static x => x.MitigationTier, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("secondary_hit_dc_base", static x => x.SecondaryHitDcBase, 10),
            OInt<CombatEffectImportModel>("debuff_count_threshold", static x => x.DebuffCountThreshold, 3),
            OInt<CombatEffectImportModel>("base_heal", static x => x.BaseHeal, 8),
            OInt<CombatEffectImportModel>("heal_per_level", static x => x.HealPerLevel, 4),
            OInt<CombatEffectImportModel>("con_mod_base", static x => x.ConModBase, 2),
            OInt<CombatEffectImportModel>("con_mod_per_2_levels", static x => x.ConModPer2Levels, 1),
            OptionalList<CombatEffectImportModel, SkillImportStringName>("effect_categories", static x => x.EffectCategories, StringName),
            ONEnum<CombatEffectImportModel, CombatEffectTargetTeamFilterImportKind>("effect_target_team_filter", static x => x.EffectTargetTeamFilter, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("max_affected_targets", static x => x.MaxAffectedTargets),
            OBool<CombatEffectImportModel>("exclude_source", static x => x.ExcludeSource),
            ONEnum<CombatEffectImportModel, CombatEffectTargetOrderImportKind>("target_order", static x => x.TargetOrder, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("required_target_creature_type_tag", static x => x.RequiredTargetCreatureTypeTag),
            ONEnum<CombatEffectImportModel, CombatCognitionImportKind>("required_target_min_cognition", static x => x.RequiredTargetMinCognition, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("status_id", static x => x.StatusId),
            OInt<CombatEffectImportModel>("applied_status_duration_tu", static x => x.AppliedStatusDurationTu),
            OName<CombatEffectImportModel>("terrain_effect_id", static x => x.TerrainEffectId),
            ONEnum<CombatEffectImportModel, CombatTerrainContactImportKind>("terrain_contact_mode", static x => x.TerrainContactMode, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("terrain_effective_trigger_count", static x => x.TerrainEffectiveTriggerCount),
            OBool<CombatEffectImportModel>("terrain_requires_ground_contact", static x => x.TerrainRequiresGroundContact),
            OBool<CombatEffectImportModel>("terrain_recheck_from_inside", static x => x.TerrainRecheckFromInside),
            OInt<CombatEffectImportModel>("terrain_max_active_instances_per_source", static x => x.TerrainMaxActiveInstancesPerSource),
            OBool<CombatEffectImportModel>("terrain_replace_existing_from_source", static x => x.TerrainReplaceExistingFromSource),
            OName<CombatEffectImportModel>("terrain_replace_to", static x => x.TerrainReplaceTo),
            OInt<CombatEffectImportModel>("height_delta", static x => x.HeightDelta),
            ONEnum<CombatEffectImportModel, CombatBodySizeImportKind>("body_size_category", static x => x.BodySizeCategory, SkillCombatEffectValueRules.GetWireValue),
            ONEnum<CombatEffectImportModel, CombatForcedMoveImportKind>("forced_move_mode", static x => x.ForcedMoveMode, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("forced_move_distance", static x => x.ForcedMoveDistance),
            OInt<CombatEffectImportModel>("forced_move_max_target_body_size", static x => x.ForcedMoveMaxTargetBodySize),
            OInt<CombatEffectImportModel>("grapple_max_height_gain", static x => x.GrappleMaxHeightGain),
            OInt<CombatEffectImportModel>("source_retreat_distance", static x => x.SourceRetreatDistance),
            OInt<CombatEffectImportModel>("charge_trap_immunity_min_skill_level", static x => x.ChargeTrapImmunityMinSkillLevel, -1),
            OInt<CombatEffectImportModel>("jump_base_budget", static x => x.JumpBaseBudget),
            ODouble<CombatEffectImportModel>("jump_str_scale", static x => x.JumpStrScale),
            ODouble<CombatEffectImportModel>("jump_arc_ratio", static x => x.JumpArcRatio),
            OInt<CombatEffectImportModel>("jump_range_multiplier", static x => x.JumpRangeMultiplier, 1),
            OInt<CombatEffectImportModel>("tick_interval_tu", static x => x.TickIntervalTu),
            OEnum<CombatEffectImportModel, CombatStackBehaviorImportKind>("stack_behavior", static x => x.StackBehavior, CombatStackBehaviorImportKind.Refresh, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("stack_limit", static x => x.StackLimit),
            ONEnum<CombatEffectImportModel, CombatDamageBonusConditionImportKind>("bonus_condition", static x => x.BonusCondition, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("bonus_condition_creature_type_tag", static x => x.BonusConditionCreatureTypeTag),
            ONEnum<CombatEffectImportModel, CombatEffectTriggerEventImportKind>("trigger_event", static x => x.TriggerEvent, SkillCombatEffectValueRules.GetWireValue),
            ONEnum<CombatEffectImportModel, CombatEffectTriggerConditionImportKind>("trigger_condition", static x => x.TriggerCondition, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("trigger_status_id", static x => x.TriggerStatusId),
            OInt<CombatEffectImportModel>("save_dc", static x => x.SaveDc),
            OInt<CombatEffectImportModel>("save_dc_bonus", static x => x.SaveDcBonus),
            OEnum<CombatEffectImportModel, CombatSaveDcModeImportKind>("save_dc_mode", static x => x.SaveDcMode, CombatSaveDcModeImportKind.Static, SkillCombatEffectValueRules.GetWireValue),
            ONEnum<CombatEffectImportModel, CombatSaveAbilityImportKind>("save_dc_source_ability", static x => x.SaveDcSourceAbility, SkillRootCombatImportValueRules.GetWireValue),
            ONEnum<CombatEffectImportModel, CombatSaveAbilityImportKind>("save_ability", static x => x.SaveAbility, SkillRootCombatImportValueRules.GetWireValue),
            OName<CombatEffectImportModel>("save_failure_status_id", static x => x.SaveFailureStatusId),
            OptionalList<CombatEffectImportModel, CombatWeightedStatusOutcomeImportModel>("save_failure_status_outcomes", static x => x.SaveFailureStatusOutcomes, weightedOutcome),
            OBool<CombatEffectImportModel>("save_partial_on_success", static x => x.SavePartialOnSuccess),
            ONEnum<CombatEffectImportModel, CombatSaveTagImportKind>("save_tag", static x => x.SaveTag, SkillCombatEffectValueRules.GetWireValue),
            OName<CombatEffectImportModel>("consumed_status_id", static x => x.ConsumedStatusId),
            OName<CombatEffectImportModel>("required_target_status_id", static x => x.RequiredTargetStatusId),
            OInt<CombatEffectImportModel>("required_target_status_min_stacks", static x => x.RequiredTargetStatusMinStacks),
            ONEnum<CombatEffectImportModel, CombatStatusSourceSelectorImportKind>("required_target_status_source_selector", static x => x.RequiredTargetStatusSourceSelector, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("dice_per_consumed_stack", static x => x.DicePerConsumedStack),
            OInt<CombatEffectImportModel>("dice_sides_per_stack", static x => x.DiceSidesPerStack),
            OInt<CombatEffectImportModel>("ap_gain", static x => x.ApGain),
            OInt<CombatEffectImportModel>("free_move_points_gain", static x => x.FreeMovePointsGain),
            OBool<CombatEffectImportModel>("counts_as_debuff_override", static x => x.CountsAsDebuffOverride),
            OBool<CombatEffectImportModel>("counts_as_debuff", static x => x.CountsAsDebuff),
            OBool<CombatEffectImportModel>("lock_counterattack", static x => x.LockCounterattack),
            OBool<CombatEffectImportModel>("lock_guard", static x => x.LockGuard),
            OBool<CombatEffectImportModel>("lock_dodge_bonus", static x => x.LockDodgeBonus),
            OBool<CombatEffectImportModel>("lock_crit", static x => x.LockCrit),
            OBool<CombatEffectImportModel>("skip_turn", static x => x.SkipTurn),
            OBool<CombatEffectImportModel>("break_on_positive_damage", static x => x.BreakOnPositiveDamage),
            OName<CombatEffectImportModel>("on_removed_status_id", static x => x.OnRemovedStatusId),
            OptionalList<CombatEffectImportModel, CombatSaveTagImportKind>("on_removed_status_save_immunity_tags", static x => x.OnRemovedStatusSaveImmunityTags, ContentCanonicalJsonValue.StableBusinessString<CombatSaveTagImportKind>(SkillCombatEffectValueRules.GetWireValue)),
            OBool<CombatEffectImportModel>("on_removed_status_undispellable", static x => x.OnRemovedStatusUndispellable),
            OBool<CombatEffectImportModel>("on_removed_status_consume_after_normal_turn", static x => x.OnRemovedStatusConsumeAfterNormalTurn),
            OInt<CombatEffectImportModel>("save_bonus", static x => x.SaveBonus),
            OInt<CombatEffectImportModel>("control_save_bonus", static x => x.ControlSaveBonus),
            OInt<CombatEffectImportModel>("passive_reduction", static x => x.PassiveReduction),
            OInt<CombatEffectImportModel>("content_dr", static x => x.ContentDr),
            OInt<CombatEffectImportModel>("guard_block", static x => x.GuardBlock),
            OInt<CombatEffectImportModel>("range_bonus", static x => x.RangeBonus),
            OInt<CombatEffectImportModel>("main_skill_lock_other_debuff_count", static x => x.MainSkillLockOtherDebuffCount),
            OInt<CombatEffectImportModel>("melee_combo_stack_gain_bonus", static x => x.MeleeComboStackGainBonus),
            OName<CombatEffectImportModel>("combo_attack_bonus_status_id", static x => x.ComboAttackBonusStatusId),
            OInt<CombatEffectImportModel>("combo_attack_bonus_stack_divisor", static x => x.ComboAttackBonusStackDivisor),
            ONEnum<CombatEffectImportModel, CombatResourceImportKind>("upkeep_resource", static x => x.UpkeepResource, SkillCombatEffectValueRules.GetWireValue),
            OInt<CombatEffectImportModel>("upkeep_interval_tu", static x => x.UpkeepIntervalTu),
            OInt<CombatEffectImportModel>("upkeep_base_cost", static x => x.UpkeepBaseCost),
            OInt<CombatEffectImportModel>("upkeep_escalation_interval_tu", static x => x.UpkeepEscalationIntervalTu),
            OInt<CombatEffectImportModel>("upkeep_cost_multiplier", static x => x.UpkeepCostMultiplier, 1),
            OBool<CombatEffectImportModel>("break_on_hard_control", static x => x.BreakOnHardControl),
            OName<CombatEffectImportModel>("termination_status_id", static x => x.TerminationStatusId),
            OInt<CombatEffectImportModel>("termination_status_duration_tu", static x => x.TerminationStatusDurationTu),
            OInt<CombatEffectImportModel>("termination_attack_roll_penalty", static x => x.TerminationAttackRollPenalty),
            OInt<CombatEffectImportModel>("termination_cooldown_tu", static x => x.TerminationCooldownTu),
            OptionalList<CombatEffectImportModel, CombatSaveTagImportKind>("save_advantage_tags", static x => x.SaveAdvantageTags, ContentCanonicalJsonValue.StableBusinessString<CombatSaveTagImportKind>(SkillCombatEffectValueRules.GetWireValue)),
            OptionalList<CombatEffectImportModel, CombatSaveTagImportKind>("save_disadvantage_tags", static x => x.SaveDisadvantageTags, ContentCanonicalJsonValue.StableBusinessString<CombatSaveTagImportKind>(SkillCombatEffectValueRules.GetWireValue)),
            OptionalList<CombatEffectImportModel, CombatSaveTagImportKind>("save_immunity_tags", static x => x.SaveImmunityTags, ContentCanonicalJsonValue.StableBusinessString<CombatSaveTagImportKind>(SkillCombatEffectValueRules.GetWireValue)),
            OptionalList<CombatEffectImportModel, SkillImportStringName>("effect_tags", static x => x.EffectTags, StringName),
            OptionalList<CombatEffectImportModel, CombatEffectSlotWeightImportModel>("equipment_durability_slot_weights", static x => x.EquipmentDurabilitySlotWeights, slotWeight),
            OptionalList<CombatEffectImportModel, CombatDamageSegmentImportModel>("extra_damage_segments", static x => x.ExtraDamageSegments, damageSegment),
            OptionalList<CombatEffectImportModel, CombatTargetDamageMultiplierRuleImportModel>("target_damage_multiplier_rules", static x => x.TargetDamageMultiplierRules, targetMultiplier)
        );

        recursiveEffect.Bind(ContentCanonicalJsonValue.Object(schema));
        return recursiveEffect;
    }

    private static ContentCanonicalJsonValueSchema<EffectPayloadProjection> BuildPayloadSchema()
    {
        ContentCanonicalJsonValueSchema<EmptyCombatEffectPayloadImportModel> empty =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EmptyCombatEffectPayloadImportModel>());
        ContentCanonicalJsonValueSchema<StatusEffectPayloadImportModel> status =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<StatusEffectPayloadImportModel>(
                OName<StatusEffectPayloadImportModel>("breaks_barrier_layer", static x => x.BreaksBarrierLayer),
                OName<StatusEffectPayloadImportModel>("source_skill_id", static x => x.SourceSkillId)
            ));
        ContentCanonicalJsonValueSchema<HealEffectPayloadImportModel> heal =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<HealEffectPayloadImportModel>(
                OBool<HealEffectPayloadImportModel>("con_mod_heal", static x => x.ConModHeal)
            ));
        ContentCanonicalJsonValueSchema<EquipmentDurabilityDamageEffectPayloadImportModel> equipment =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EquipmentDurabilityDamageEffectPayloadImportModel>(
                OInt<EquipmentDurabilityDamageEffectPayloadImportModel>("max_damaged_items", static x => x.MaxDamagedItems, 1),
                ContentCanonicalJsonProperty<EquipmentDurabilityDamageEffectPayloadImportModel>.Required(
                    "target_slots", static x => x.TargetSlots,
                    ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.StableBusinessString<CombatEquipmentSlotImportKind>(SkillCombatEffectValueRules.GetWireValue))
                )
            ));
        ContentCanonicalJsonValueSchema<RepeatAttackUntilFailEffectPayloadImportModel> repeat =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<RepeatAttackUntilFailEffectPayloadImportModel>(
                OInt<RepeatAttackUntilFailEffectPayloadImportModel>("base_attack_bonus", static x => x.BaseAttackBonus),
                OEnum<RepeatAttackUntilFailEffectPayloadImportModel, CombatResourceImportKind>("cost_resource", static x => x.CostResource, CombatResourceImportKind.Aura, SkillCombatEffectValueRules.GetWireValue),
                OInt<RepeatAttackUntilFailEffectPayloadImportModel>("follow_up_cost_addition", static x => x.FollowUpCostAddition),
                ODouble<RepeatAttackUntilFailEffectPayloadImportModel>("follow_up_cost_multiplier", static x => x.FollowUpCostMultiplier, 1.0),
                OInt<RepeatAttackUntilFailEffectPayloadImportModel>("follow_up_attack_penalty", static x => x.FollowUpAttackPenalty),
                OptionalMap<RepeatAttackUntilFailEffectPayloadImportModel, IReadOnlyDictionary<int, int>, int, int>("penalty_free_stages_by_level", static x => x.PenaltyFreeStagesByLevel, static x => x, ContentCanonicalJsonKey.InvariantInt32, ContentCanonicalJsonKey.InvariantInt32Order, ContentCanonicalJsonValue.Int32),
                OBool<RepeatAttackUntilFailEffectPayloadImportModel>("same_target_only", static x => x.SameTargetOnly),
                OInt<RepeatAttackUntilFailEffectPayloadImportModel>("follow_up_fixed_cost", static x => x.FollowUpFixedCost),
                OBool<RepeatAttackUntilFailEffectPayloadImportModel>("exponential_penalty", static x => x.ExponentialPenalty),
                OBool<RepeatAttackUntilFailEffectPayloadImportModel>("stop_on_insufficient_resource", static x => x.StopOnInsufficientResource)
            ));
        ContentCanonicalJsonValueSchema<LayeredBarrierEffectPayloadImportModel> barrier =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<LayeredBarrierEffectPayloadImportModel>(
                ContentCanonicalJsonProperty<LayeredBarrierEffectPayloadImportModel>.Required("area_pattern", static x => x.AreaPattern, ContentCanonicalJsonValue.StableBusinessString<CombatSkillImportAreaPattern>(SkillJsonImportValueRules.GetWireValue)),
                ContentCanonicalJsonProperty<LayeredBarrierEffectPayloadImportModel>.Required("profile_id", static x => x.ProfileId, Identifier),
                ContentCanonicalJsonProperty<LayeredBarrierEffectPayloadImportModel>.Required("radius_cells", static x => x.RadiusCells, ContentCanonicalJsonValue.Int32),
                ContentCanonicalJsonProperty<LayeredBarrierEffectPayloadImportModel>.Required("save_dc", static x => x.SaveDc, ContentCanonicalJsonValue.Int32)
            ));
        ContentCanonicalJsonValueSchema<GradedSaveExecuteEffectPayloadImportModel> graded =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<GradedSaveExecuteEffectPayloadImportModel>(
                RInt<GradedSaveExecuteEffectPayloadImportModel>("critical_failure_damage_dice_count", static x => x.CriticalFailureDamageDiceCount),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("critical_failure_damage_dice_sides", static x => x.CriticalFailureDamageDiceSides),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("critical_failure_execute_threshold_max_hp_percent", static x => x.CriticalFailureExecuteThresholdMaxHpPercent),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("critical_failure_frightened_duration_tu", static x => x.CriticalFailureFrightenedDurationTu),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("critical_failure_stunned_duration_tu", static x => x.CriticalFailureStunnedDurationTu),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_damage_dice_count", static x => x.FailureDamageDiceCount),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_damage_dice_sides", static x => x.FailureDamageDiceSides),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_execute_threshold_fixed", static x => x.FailureExecuteThresholdFixed),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_execute_threshold_max_hp_percent", static x => x.FailureExecuteThresholdMaxHpPercent),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_frightened_duration_tu", static x => x.FailureFrightenedDurationTu),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("failure_reaction_lock_duration_tu", static x => x.FailureReactionLockDurationTu),
                ContentCanonicalJsonProperty<GradedSaveExecuteEffectPayloadImportModel>.Required("profile_id", static x => x.ProfileId, Identifier),
                RInt<GradedSaveExecuteEffectPayloadImportModel>("success_aftershock_duration_tu", static x => x.SuccessAftershockDurationTu)
            ));
        ContentCanonicalJsonValueSchema<DispelMagicEffectPayloadImportModel> dispel =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<DispelMagicEffectPayloadImportModel>(
                OName<DispelMagicEffectPayloadImportModel>("breaks_barrier_layer", static x => x.BreaksBarrierLayer)
            ));
        ContentCanonicalJsonValueSchema<OnKillGainResourcesEffectPayloadImportModel> onKill =
            ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<OnKillGainResourcesEffectPayloadImportModel>(
                OEnum<OnKillGainResourcesEffectPayloadImportModel, CombatOnKillGrantScopeImportKind>("grant_scope", static x => x.GrantScope, CombatOnKillGrantScopeImportKind.None, SkillCombatEffectValueRules.GetWireValue),
                OBool<OnKillGainResourcesEffectPayloadImportModel>("require_target_defeated_by_same_skill", static x => x.RequireTargetDefeatedBySameSkill),
                OBool<OnKillGainResourcesEffectPayloadImportModel>("stack_on_multiple_kills", static x => x.StackOnMultipleKills)
            ));

        return ContentCanonicalJsonValue.ClosedUnion<EffectPayloadProjection, CombatEffectPayloadShape>(
            static x => x.Shape,
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<EmptyPayloadProjection>(CombatEffectPayloadShape.Empty, ContentCanonicalJsonValue.Project<EmptyPayloadProjection, EmptyCombatEffectPayloadImportModel>(static x => x.Value, empty)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<StatusPayloadProjection>(CombatEffectPayloadShape.Status, ContentCanonicalJsonValue.Project<StatusPayloadProjection, StatusEffectPayloadImportModel>(static x => x.Value, status)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<HealPayloadProjection>(CombatEffectPayloadShape.Heal, ContentCanonicalJsonValue.Project<HealPayloadProjection, HealEffectPayloadImportModel>(static x => x.Value, heal)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<EquipmentPayloadProjection>(CombatEffectPayloadShape.EquipmentDurabilityDamage, ContentCanonicalJsonValue.Project<EquipmentPayloadProjection, EquipmentDurabilityDamageEffectPayloadImportModel>(static x => x.Value, equipment)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<RepeatPayloadProjection>(CombatEffectPayloadShape.RepeatAttackUntilFail, ContentCanonicalJsonValue.Project<RepeatPayloadProjection, RepeatAttackUntilFailEffectPayloadImportModel>(static x => x.Value, repeat)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<BarrierPayloadProjection>(CombatEffectPayloadShape.LayeredBarrier, ContentCanonicalJsonValue.Project<BarrierPayloadProjection, LayeredBarrierEffectPayloadImportModel>(static x => x.Value, barrier)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<GradedPayloadProjection>(CombatEffectPayloadShape.GradedSaveExecute, ContentCanonicalJsonValue.Project<GradedPayloadProjection, GradedSaveExecuteEffectPayloadImportModel>(static x => x.Value, graded)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<DispelPayloadProjection>(CombatEffectPayloadShape.DispelMagic, ContentCanonicalJsonValue.Project<DispelPayloadProjection, DispelMagicEffectPayloadImportModel>(static x => x.Value, dispel)),
            ContentCanonicalJsonUnionCase<EffectPayloadProjection, CombatEffectPayloadShape>.Create<OnKillPayloadProjection>(CombatEffectPayloadShape.OnKillGainResources, ContentCanonicalJsonValue.Project<OnKillPayloadProjection, OnKillGainResourcesEffectPayloadImportModel>(static x => x.Value, onKill))
        );
    }

    private static EffectPayloadProjection ProjectEffectPayload(CombatEffectImportModel effect)
    {
        if (!SkillFullCombatEffectClosedSpec.IsPayloadCompatible(effect.Kind, effect.Payload))
            throw new JsonException("Combat effect kind and payload are incompatible.");
        return effect.Payload switch
        {
            EmptyCombatEffectPayloadImportModel value => new EmptyPayloadProjection(value),
            StatusEffectPayloadImportModel value => new StatusPayloadProjection(value),
            HealEffectPayloadImportModel value => new HealPayloadProjection(value),
            EquipmentDurabilityDamageEffectPayloadImportModel value => new EquipmentPayloadProjection(value),
            RepeatAttackUntilFailEffectPayloadImportModel value => new RepeatPayloadProjection(value),
            LayeredBarrierEffectPayloadImportModel value => new BarrierPayloadProjection(value),
            GradedSaveExecuteEffectPayloadImportModel value => new GradedPayloadProjection(value),
            DispelMagicEffectPayloadImportModel value => new DispelPayloadProjection(value),
            OnKillGainResourcesEffectPayloadImportModel value => new OnKillPayloadProjection(value),
            _ => throw new JsonException("Combat effect payload shape is not registered."),
        };
    }

    private static ContentCanonicalJsonProperty<T> ODouble<T>(string name, Func<T, double> getter, double defaultValue = 0.0) =>
        ContentCanonicalJsonProperty<T>.Optional(name, getter, defaultValue, ContentCanonicalJsonValue.FloatingPointDouble);
    private static ContentCanonicalJsonProperty<T> RInt<T>(string name, Func<T, int> getter) =>
        ContentCanonicalJsonProperty<T>.Required(name, getter, ContentCanonicalJsonValue.Int32);
}
