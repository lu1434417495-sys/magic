#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal sealed partial class CombatEffectJsonDto
{
    [ContentJsonSchemaStableStringValues(typeof(CombatTickEffectSchemaValues))]
    [JsonPropertyName("tick_effect_type")] public string TickEffectType { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatEffectLifetimeSchemaValues))]
    [JsonPropertyName("lifetime_policy")] public string LifetimePolicy { get; init; } = "timed";
    [JsonPropertyName("heal_to_hp_percent_floor")] public int HealToHpPercentFloor { get; init; }
    [JsonPropertyName("heal_missing_hp_percent")] public int HealMissingHpPercent { get; init; }
    [JsonPropertyName("move_cost_delta")] public int MoveCostDelta { get; init; }
    [JsonPropertyName("render_overlay_id")] public string RenderOverlayId { get; init; } = "";
    [JsonPropertyName("overlay_priority")] public int OverlayPriority { get; init; }
    [JsonPropertyName("display_name")] public string DisplayName { get; init; } = "";
    [JsonPropertyName("does_not_stack_with_status_id")] public string DoesNotStackWithStatusId { get; init; } = "";
    [JsonPropertyName("does_not_stack_with_status_ids")] public IReadOnlyList<string> DoesNotStackWithStatusIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("damage_ratio_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? DamageRatioPercent { get; init; }
    [JsonPropertyName("pre_resistance_damage_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public double? PreResistanceDamageMultiplier { get; init; }
    [JsonPropertyName("weapon_dice_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? WeaponDiceMultiplier { get; init; }
    [JsonPropertyName("bonus_weapon_dice_multiplier")] public int BonusWeaponDiceMultiplier { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [Description(
        "Closed runtime damage tag. Use freeze for cold damage; cold is not valid. Force damage also requires force_effect in effect_categories."
    )]
    [JsonPropertyName("damage_tag")] public string DamageTag { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [JsonPropertyName("damage_tags")] public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [JsonPropertyName("mitigation_bypass_damage_tags")] public IReadOnlyList<string> MitigationBypassDamageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(DamageMitigationTierSchemaValues))]
    [JsonPropertyName("mitigation_bypass_tiers")] public IReadOnlyList<string> MitigationBypassTiers { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(DamageCategorySchemaValues))]
    [JsonPropertyName("damage_category")] public string DamageCategory { get; init; } = "";
    [JsonPropertyName("dr_bypass_tag")] public string DrBypassTag { get; init; } = "";
    [JsonPropertyName("hp_ratio_threshold_percent")] public int HpRatioThresholdPercent { get; init; }
    [JsonPropertyName("dice_count")] public int DiceCount { get; init; }
    [JsonPropertyName("dice_sides")] public int DiceSides { get; init; }
    [JsonPropertyName("dice_bonus")] public int DiceBonus { get; init; }
    [JsonPropertyName("dice_sides_base")] public int DiceSidesBase { get; init; }
    [JsonPropertyName("dice_sides_per_constitution_mod")] public int DiceSidesPerConstitutionMod { get; init; }
    [JsonPropertyName("dice_sides_per_willpower_mod")] public int DiceSidesPerWillpowerMod { get; init; }
    [JsonPropertyName("shield_family")] public string ShieldFamily { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(ShieldAttributeModifierSchemaValues))]
    [JsonPropertyName("shield_attribute_modifier_id")] public string ShieldAttributeModifierId { get; init; } = "";
    [JsonPropertyName("shield_roll_per_target")] public bool ShieldRollPerTarget { get; init; }
    [JsonPropertyName("bonus_damage_dice_count")] public int BonusDamageDiceCount { get; init; }
    [JsonPropertyName("bonus_damage_dice_sides")] public int BonusDamageDiceSides { get; init; }
    [JsonPropertyName("bonus_damage_dice_bonus")] public int BonusDamageDiceBonus { get; init; }
    [JsonPropertyName("bonus_damage_separate_event")] public bool BonusDamageSeparateEvent { get; init; }
    [JsonPropertyName("source_bound_weapon_bonus_damage_dice_count")] public int SourceBoundWeaponBonusDamageDiceCount { get; init; }
    [JsonPropertyName("source_bound_weapon_bonus_damage_dice_sides")] public int SourceBoundWeaponBonusDamageDiceSides { get; init; }
    [JsonPropertyName("source_bound_weapon_bonus_damage_dice_bonus")] public int SourceBoundWeaponBonusDamageDiceBonus { get; init; }
    [JsonPropertyName("add_weapon_dice")] public bool AddWeaponDice { get; init; }
    [JsonPropertyName("requires_weapon")] public bool RequiresWeapon { get; init; }
    [JsonPropertyName("use_weapon_physical_damage_tag")] public bool UseWeaponPhysicalDamageTag { get; init; }
    [JsonPropertyName("resolve_as_weapon_attack")] public bool ResolveAsWeaponAttack { get; init; }
    [JsonPropertyName("allow_repeat_hits_across_steps")] public bool AllowRepeatHitsAcrossSteps { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatPathStepAreaPatternSchemaValues))]
    [JsonPropertyName("path_step_area_pattern")] public string PathStepAreaPattern { get; init; } = "diamond";
    [JsonPropertyName("path_step_radius")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? PathStepRadius { get; init; }
    [JsonPropertyName("path_step_log_label")] public string PathStepLogLabel { get; init; } = "";
    [JsonPropertyName("repeat_hit_status_id")] public string RepeatHitStatusId { get; init; } = "";
    [JsonPropertyName("repeat_hit_status_threshold")] public int RepeatHitStatusThreshold { get; init; }
    [JsonPropertyName("repeat_hit_status_min_skill_level")] public int RepeatHitStatusMinSkillLevel { get; init; }
    [JsonPropertyName("repeat_hit_status_power")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? RepeatHitStatusPower { get; init; }
    [JsonPropertyName("repeat_hit_status_duration_tu")] public int RepeatHitStatusDurationTu { get; init; }
    [JsonPropertyName("repeat_hit_status_log_template")] public string RepeatHitStatusLogTemplate { get; init; } = "";
    [JsonPropertyName("prevent_repeat_target")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? PreventRepeatTarget { get; init; }
    [JsonPropertyName("chain_base_hop_range")] public int ChainBaseHopRange { get; init; }
    [JsonPropertyName("chain_conductive_hop_range")] public int ChainConductiveHopRange { get; init; }
    [JsonPropertyName("chain_max_total_targets")] public int ChainMaxTotalTargets { get; init; }
    [JsonPropertyName("chain_conductive_status_ids")] public IReadOnlyList<string> ChainConductiveStatusIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("chain_conductive_terrain_effect_ids")] public IReadOnlyList<string> ChainConductiveTerrainEffectIds { get; init; } = Array.Empty<string>();
    [JsonPropertyName("chain_backlash_hop_range_bonus")] public int ChainBacklashHopRangeBonus { get; init; }
    [JsonPropertyName("stop_on_miss")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? StopOnMiss { get; init; }
    [JsonPropertyName("stop_on_target_down")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? StopOnTargetDown { get; init; }
    [JsonPropertyName("fixed_attack_count")] public int FixedAttackCount { get; init; }
    [JsonPropertyName("follow_up_damage_multiplier_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? FollowUpDamageMultiplierPercent { get; init; }
    [JsonPropertyName("follow_up_attack_roll_bonus_curve")] public IReadOnlyList<int> FollowUpAttackRollBonusCurve { get; init; } = Array.Empty<int>();
    [JsonPropertyName("remove_harmful")] public bool RemoveHarmful { get; init; }
    [JsonPropertyName("remove_harmful_from_allies")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? RemoveHarmfulFromAllies { get; init; }
    [JsonPropertyName("remove_beneficial")] public bool RemoveBeneficial { get; init; }
    [JsonPropertyName("remove_beneficial_from_enemies")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? RemoveBeneficialFromEnemies { get; init; }
    [JsonPropertyName("require_damage_applied")] public bool RequireDamageApplied { get; init; }
    [JsonPropertyName("max_status_removed")] public int MaxStatusRemoved { get; init; }
    [JsonPropertyName("min_hp_after_damage")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinHpAfterDamage { get; init; }
    [JsonPropertyName("death_prevention_priority")] public int DeathPreventionPriority { get; init; }
    [JsonPropertyName("threshold_base_value")] public int ThresholdBaseValue { get; init; }
    [JsonPropertyName("threshold_level_anchor")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ThresholdLevelAnchor { get; init; }
    [JsonPropertyName("threshold_level_bonus_per_delta")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ThresholdLevelBonusPerDelta { get; init; }
    [JsonPropertyName("threshold_max_hp_ratio_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ThresholdMaxHpRatioPercent { get; init; }
    [JsonPropertyName("threshold_cap_max_hp_ratio_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ThresholdCapMaxHpRatioPercent { get; init; }
    [JsonPropertyName("soul_fracture_duration_tu")] public int SoulFractureDurationTu { get; init; }
    [JsonPropertyName("heal_multiplier_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? HealMultiplierPercent { get; init; }
    [JsonPropertyName("shield_gain_multiplier_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ShieldGainMultiplierPercent { get; init; }
    [JsonPropertyName("attack_roll_penalty")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? AttackRollPenalty { get; init; }
    [JsonPropertyName("attack_roll_bonus")] public int AttackRollBonus { get; init; }
    [JsonPropertyName("attack_roll_advantage")] public bool AttackRollAdvantage { get; init; }
    [JsonPropertyName("consume_on_next_attack_check")] public bool ConsumeOnNextAttackCheck { get; init; }
    [JsonPropertyName("consume_on_next_save")] public bool ConsumeOnNextSave { get; init; }
    [JsonPropertyName("undispellable")] public bool Undispellable { get; init; }
    [JsonPropertyName("dispellable_magic")] public bool DispellableMagic { get; init; }
    [JsonPropertyName("dispellable_harmful_magic")] public bool DispellableHarmfulMagic { get; init; }
    [JsonPropertyName("dispellable_beneficial_magic")] public bool DispellableBeneficialMagic { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(DamageMitigationTierSchemaValues))]
    [JsonPropertyName("mitigation_tier")] public string MitigationTier { get; init; } = "";
    [JsonPropertyName("secondary_hit_dc_base")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? SecondaryHitDcBase { get; init; }
    [JsonPropertyName("debuff_count_threshold")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? DebuffCountThreshold { get; init; }
    [JsonPropertyName("base_heal")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? BaseHeal { get; init; }
    [JsonPropertyName("heal_per_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? HealPerLevel { get; init; }
    [JsonPropertyName("con_mod_base")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ConModBase { get; init; }
    [JsonPropertyName("con_mod_per_2_levels")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ConModPer2Levels { get; init; }
    [JsonPropertyName("effect_categories")]
    [Description(
        "Typed effect categories used by runtime rules. Force damage must explicitly include force_effect; it is not inferred from damage_tag."
    )]
    public IReadOnlyList<string> EffectCategories { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(CombatEffectTargetTeamFilterSchemaValues))]
    [JsonPropertyName("effect_target_team_filter")] public string EffectTargetTeamFilter { get; init; } = "";
    [JsonPropertyName("max_affected_targets")] public int MaxAffectedTargets { get; init; }
    [JsonPropertyName("exclude_source")] public bool ExcludeSource { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatEffectTargetOrderSchemaValues))]
    [JsonPropertyName("target_order")] public string TargetOrder { get; init; } = "";
    [JsonPropertyName("required_target_creature_type_tag")] public string RequiredTargetCreatureTypeTag { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatCognitionSchemaValues))]
    [JsonPropertyName("required_target_min_cognition")] public string RequiredTargetMinCognition { get; init; } = "";
    [JsonPropertyName("status_id")] public string StatusId { get; init; } = "";
    [JsonPropertyName("applied_status_duration_tu")] public int AppliedStatusDurationTu { get; init; }
    [JsonPropertyName("terrain_effect_id")] public string TerrainEffectId { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatTerrainContactSchemaValues))]
    [JsonPropertyName("terrain_contact_mode")] public string TerrainContactMode { get; init; } = "";
    [JsonPropertyName("terrain_effective_trigger_count")] public int TerrainEffectiveTriggerCount { get; init; }
    [JsonPropertyName("terrain_requires_ground_contact")] public bool TerrainRequiresGroundContact { get; init; }
    [JsonPropertyName("terrain_recheck_from_inside")] public bool TerrainRecheckFromInside { get; init; }
    [JsonPropertyName("terrain_max_active_instances_per_source")] public int TerrainMaxActiveInstancesPerSource { get; init; }
    [JsonPropertyName("terrain_replace_existing_from_source")] public bool TerrainReplaceExistingFromSource { get; init; }
    [JsonPropertyName("terrain_replace_to")] public string TerrainReplaceTo { get; init; } = "";
    [JsonPropertyName("height_delta")] public int HeightDelta { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatBodySizeSchemaValues))]
    [JsonPropertyName("body_size_category")] public string BodySizeCategory { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatForcedMoveSchemaValues))]
    [JsonPropertyName("forced_move_mode")] public string ForcedMoveMode { get; init; } = "";
    [JsonPropertyName("forced_move_distance")] public int ForcedMoveDistance { get; init; }
    [JsonPropertyName("forced_move_max_target_body_size")] public int ForcedMoveMaxTargetBodySize { get; init; }
    [JsonPropertyName("grapple_max_height_gain")] public int GrappleMaxHeightGain { get; init; }
    [JsonPropertyName("source_retreat_distance")] public int SourceRetreatDistance { get; init; }
    [JsonPropertyName("charge_trap_immunity_min_skill_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ChargeTrapImmunityMinSkillLevel { get; init; }
    [JsonPropertyName("jump_base_budget")] public int JumpBaseBudget { get; init; }
    [JsonPropertyName("jump_str_scale")] public double JumpStrScale { get; init; }
    [JsonPropertyName("jump_arc_ratio")] public double JumpArcRatio { get; init; }
    [JsonPropertyName("jump_range_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? JumpRangeMultiplier { get; init; }
    [JsonPropertyName("tick_interval_tu")] public int TickIntervalTu { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatStackBehaviorSchemaValues))]
    [JsonPropertyName("stack_behavior")] public string StackBehavior { get; init; } = "refresh";
    [JsonPropertyName("stack_limit")] public int StackLimit { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatDamageBonusConditionSchemaValues))]
    [JsonPropertyName("bonus_condition")] public string BonusCondition { get; init; } = "";
    [JsonPropertyName("bonus_condition_creature_type_tag")] public string BonusConditionCreatureTypeTag { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatEffectTriggerEventSchemaValues))]
    [JsonPropertyName("trigger_event")] public string TriggerEvent { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatEffectTriggerConditionSchemaValues))]
    [JsonPropertyName("trigger_condition")] public string TriggerCondition { get; init; } = "";
    [JsonPropertyName("trigger_status_id")] public string TriggerStatusId { get; init; } = "";
    [JsonPropertyName("save_dc")] public int SaveDc { get; init; }
    [JsonPropertyName("save_dc_bonus")] public int SaveDcBonus { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveDcModeSchemaValues))]
    [JsonPropertyName("save_dc_mode")] public string SaveDcMode { get; init; } = "static";
    [ContentJsonSchemaStableStringValues(typeof(SkillSaveAbilitySchemaValues))]
    [JsonPropertyName("save_dc_source_ability")] public string SaveDcSourceAbility { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(SkillSaveAbilitySchemaValues))]
    [JsonPropertyName("save_ability")] public string SaveAbility { get; init; } = "";
    [JsonPropertyName("save_failure_status_id")] public string SaveFailureStatusId { get; init; } = "";
    [JsonPropertyName("save_failure_status_outcomes")] public IReadOnlyList<CombatWeightedStatusOutcomeJsonDto> SaveFailureStatusOutcomes { get; init; } = Array.Empty<CombatWeightedStatusOutcomeJsonDto>();
    [JsonPropertyName("save_partial_on_success")] public bool SavePartialOnSuccess { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveTagSchemaValues))]
    [Description(
        "Closed semantic save context used by save bonuses. Choose a registered enum value such as fireball or magic; do not invent a per-skill identifier."
    )]
    [JsonPropertyName("save_tag")] public string SaveTag { get; init; } = "";
    [JsonPropertyName("consumed_status_id")] public string ConsumedStatusId { get; init; } = "";
    [JsonPropertyName("required_target_status_id")] public string RequiredTargetStatusId { get; init; } = "";
    [JsonPropertyName("required_target_status_min_stacks")] public int RequiredTargetStatusMinStacks { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatStatusSourceSelectorSchemaValues))]
    [JsonPropertyName("required_target_status_source_selector")] public string RequiredTargetStatusSourceSelector { get; init; } = "";
    [JsonPropertyName("dice_per_consumed_stack")] public int DicePerConsumedStack { get; init; }
    [JsonPropertyName("dice_sides_per_stack")] public int DiceSidesPerStack { get; init; }
    [JsonPropertyName("ap_gain")] public int ApGain { get; init; }
    [JsonPropertyName("free_move_points_gain")] public int FreeMovePointsGain { get; init; }
    [JsonPropertyName("counts_as_debuff_override")] public bool CountsAsDebuffOverride { get; init; }
    [JsonPropertyName("counts_as_debuff")] public bool CountsAsDebuff { get; init; }
    [JsonPropertyName("lock_counterattack")] public bool LockCounterattack { get; init; }
    [JsonPropertyName("lock_guard")] public bool LockGuard { get; init; }
    [JsonPropertyName("lock_dodge_bonus")] public bool LockDodgeBonus { get; init; }
    [JsonPropertyName("lock_crit")] public bool LockCrit { get; init; }
    [JsonPropertyName("skip_turn")] public bool SkipTurn { get; init; }
    [JsonPropertyName("break_on_positive_damage")] public bool BreakOnPositiveDamage { get; init; }
    [JsonPropertyName("on_removed_status_id")] public string OnRemovedStatusId { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveTagSchemaValues))]
    [JsonPropertyName("on_removed_status_save_immunity_tags")] public IReadOnlyList<string> OnRemovedStatusSaveImmunityTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("on_removed_status_undispellable")] public bool OnRemovedStatusUndispellable { get; init; }
    [JsonPropertyName("on_removed_status_consume_after_normal_turn")] public bool OnRemovedStatusConsumeAfterNormalTurn { get; init; }
    [JsonPropertyName("save_bonus")] public int SaveBonus { get; init; }
    [JsonPropertyName("control_save_bonus")] public int ControlSaveBonus { get; init; }
    [JsonPropertyName("passive_reduction")] public int PassiveReduction { get; init; }
    [JsonPropertyName("content_dr")] public int ContentDr { get; init; }
    [JsonPropertyName("guard_block")] public int GuardBlock { get; init; }
    [JsonPropertyName("range_bonus")] public int RangeBonus { get; init; }
    [JsonPropertyName("main_skill_lock_other_debuff_count")] public int MainSkillLockOtherDebuffCount { get; init; }
    [JsonPropertyName("melee_combo_stack_gain_bonus")] public int MeleeComboStackGainBonus { get; init; }
    [JsonPropertyName("combo_attack_bonus_status_id")] public string ComboAttackBonusStatusId { get; init; } = "";
    [JsonPropertyName("combo_attack_bonus_stack_divisor")] public int ComboAttackBonusStackDivisor { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatResourceSchemaValues))]
    [JsonPropertyName("upkeep_resource")] public string UpkeepResource { get; init; } = "";
    [JsonPropertyName("upkeep_interval_tu")] public int UpkeepIntervalTu { get; init; }
    [JsonPropertyName("upkeep_base_cost")] public int UpkeepBaseCost { get; init; }
    [JsonPropertyName("upkeep_escalation_interval_tu")] public int UpkeepEscalationIntervalTu { get; init; }
    [JsonPropertyName("upkeep_cost_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? UpkeepCostMultiplier { get; init; }
    [JsonPropertyName("break_on_hard_control")] public bool BreakOnHardControl { get; init; }
    [JsonPropertyName("termination_status_id")] public string TerminationStatusId { get; init; } = "";
    [JsonPropertyName("termination_status_duration_tu")] public int TerminationStatusDurationTu { get; init; }
    [JsonPropertyName("termination_attack_roll_penalty")] public int TerminationAttackRollPenalty { get; init; }
    [JsonPropertyName("termination_cooldown_tu")] public int TerminationCooldownTu { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveTagSchemaValues))]
    [JsonPropertyName("save_advantage_tags")] public IReadOnlyList<string> SaveAdvantageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveTagSchemaValues))]
    [JsonPropertyName("save_disadvantage_tags")] public IReadOnlyList<string> SaveDisadvantageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(CombatSaveTagSchemaValues))]
    [JsonPropertyName("save_immunity_tags")] public IReadOnlyList<string> SaveImmunityTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("effect_tags")] public IReadOnlyList<string> EffectTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("equipment_durability_slot_weights")] public IReadOnlyList<CombatEffectSlotWeightJsonDto> EquipmentDurabilitySlotWeights { get; init; } = Array.Empty<CombatEffectSlotWeightJsonDto>();
    [JsonPropertyName("extra_damage_segments")] public IReadOnlyList<CombatDamageSegmentJsonDto> ExtraDamageSegments { get; init; } = Array.Empty<CombatDamageSegmentJsonDto>();
    [JsonPropertyName("target_damage_multiplier_rules")] public IReadOnlyList<CombatTargetDamageMultiplierRuleJsonDto> TargetDamageMultiplierRules { get; init; } = Array.Empty<CombatTargetDamageMultiplierRuleJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatEffectSlotWeightJsonDto
{
    [ContentJsonSchemaStableStringValues(typeof(CombatEquipmentSlotSchemaValues))]
    [JsonPropertyName("slot_id")] public string SlotId { get; init; } = "";
    [JsonPropertyName("weight")] public int Weight { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatDamageSegmentJsonDto
{
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [JsonPropertyName("damage_tag")] public string DamageTag { get; init; } = "";
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [JsonPropertyName("damage_tags")] public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    [JsonPropertyName("mitigation_bypass_damage_tags")] public IReadOnlyList<string> MitigationBypassDamageTags { get; init; } = Array.Empty<string>();
    [ContentJsonSchemaStableStringValues(typeof(DamageMitigationTierSchemaValues))]
    [JsonPropertyName("mitigation_bypass_tiers")] public IReadOnlyList<string> MitigationBypassTiers { get; init; } = Array.Empty<string>();
    [JsonPropertyName("power")] public int Power { get; init; }
    [JsonPropertyName("dice_count")] public int DiceCount { get; init; }
    [JsonPropertyName("dice_sides")] public int DiceSides { get; init; }
    [JsonPropertyName("dice_bonus")] public int DiceBonus { get; init; }
    [JsonPropertyName("double_dice_on_critical")] public bool DoubleDiceOnCritical { get; init; }
    [JsonPropertyName("pre_resistance_damage_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public double? PreResistanceDamageMultiplier { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatTargetDamageMultiplierRuleJsonDto
{
    [JsonPropertyName("any_creature_type_tags")] public IReadOnlyList<string> AnyCreatureTypeTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("all_creature_type_tags")] public IReadOnlyList<string> AllCreatureTypeTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("excluded_creature_type_tags")] public IReadOnlyList<string> ExcludedCreatureTypeTags { get; init; } = Array.Empty<string>();
    [JsonPropertyName("multiplier_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MultiplierPercent { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatWeightedStatusOutcomeJsonDto
{
    [JsonPropertyName("outcome_id")] public string OutcomeId { get; init; } = "";
    [JsonPropertyName("weight")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? Weight { get; init; }
    [JsonPropertyName("status_effect")] [JsonRequired] public CombatEffectJsonDto StatusEffect { get; init; } = null!;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EmptyCombatEffectPayloadJsonDto { }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class StatusEffectPayloadJsonDto
{
    [JsonPropertyName("breaks_barrier_layer")] public string BreaksBarrierLayer { get; init; } = "";
    [JsonPropertyName("source_skill_id")] public string SourceSkillId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HealEffectPayloadJsonDto
{
    [JsonPropertyName("con_mod_heal")] public bool ConModHeal { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentDurabilityDamageEffectPayloadJsonDto
{
    [JsonPropertyName("max_damaged_items")] public int MaxDamagedItems { get; init; } = 1;
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(CombatEquipmentSlotSchemaValues))]
    [JsonPropertyName("target_slots")] public IReadOnlyList<string> TargetSlots { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RepeatAttackUntilFailEffectPayloadJsonDto
{
    [JsonPropertyName("base_attack_bonus")] public int BaseAttackBonus { get; init; }
    [ContentJsonSchemaStableStringValues(typeof(CombatResourceSchemaValues))]
    [JsonPropertyName("cost_resource")] public string CostResource { get; init; } = "aura";
    [JsonPropertyName("follow_up_cost_addition")] public int FollowUpCostAddition { get; init; }
    [JsonPropertyName("follow_up_cost_multiplier")] public double FollowUpCostMultiplier { get; init; } = 1.0;
    [JsonPropertyName("follow_up_attack_penalty")] public int FollowUpAttackPenalty { get; init; }
    [JsonPropertyName("penalty_free_stages_by_level")] public IReadOnlyDictionary<string, int> PenaltyFreeStagesByLevel { get; init; } = new Dictionary<string, int>();
    [JsonPropertyName("same_target_only")] public bool SameTargetOnly { get; init; }
    [JsonPropertyName("follow_up_fixed_cost")] public int FollowUpFixedCost { get; init; }
    [JsonPropertyName("exponential_penalty")] public bool ExponentialPenalty { get; init; }
    [JsonPropertyName("stop_on_insufficient_resource")] public bool StopOnInsufficientResource { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GradedSaveExecuteEffectPayloadJsonDto
{
    [JsonPropertyName("critical_failure_damage_dice_count")] [JsonRequired] public int CriticalFailureDamageDiceCount { get; init; }
    [JsonPropertyName("critical_failure_damage_dice_sides")] [JsonRequired] public int CriticalFailureDamageDiceSides { get; init; }
    [JsonPropertyName("critical_failure_execute_threshold_max_hp_percent")] [JsonRequired] public int CriticalFailureExecuteThresholdMaxHpPercent { get; init; }
    [JsonPropertyName("critical_failure_frightened_duration_tu")] [JsonRequired] public int CriticalFailureFrightenedDurationTu { get; init; }
    [JsonPropertyName("critical_failure_stunned_duration_tu")] [JsonRequired] public int CriticalFailureStunnedDurationTu { get; init; }
    [JsonPropertyName("failure_damage_dice_count")] [JsonRequired] public int FailureDamageDiceCount { get; init; }
    [JsonPropertyName("failure_damage_dice_sides")] [JsonRequired] public int FailureDamageDiceSides { get; init; }
    [JsonPropertyName("failure_execute_threshold_fixed")] [JsonRequired] public int FailureExecuteThresholdFixed { get; init; }
    [JsonPropertyName("failure_execute_threshold_max_hp_percent")] [JsonRequired] public int FailureExecuteThresholdMaxHpPercent { get; init; }
    [JsonPropertyName("failure_frightened_duration_tu")] [JsonRequired] public int FailureFrightenedDurationTu { get; init; }
    [JsonPropertyName("failure_reaction_lock_duration_tu")] [JsonRequired] public int FailureReactionLockDurationTu { get; init; }
    [JsonPropertyName("profile_id")] [JsonRequired] public string ProfileId { get; init; } = null!;
    [JsonPropertyName("success_aftershock_duration_tu")] [JsonRequired] public int SuccessAftershockDurationTu { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DispelMagicEffectPayloadJsonDto
{
    [JsonPropertyName("breaks_barrier_layer")] public string BreaksBarrierLayer { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class OnKillGainResourcesEffectPayloadJsonDto
{
    [ContentJsonSchemaStableStringValues(typeof(CombatOnKillGrantScopeSchemaValues))]
    [JsonPropertyName("grant_scope")] public string GrantScope { get; init; } = "";
    [JsonPropertyName("require_target_defeated_by_same_skill")] public bool RequireTargetDefeatedBySameSkill { get; init; }
    [JsonPropertyName("stack_on_multiple_kills")] public bool StackOnMultipleKills { get; init; }
}
