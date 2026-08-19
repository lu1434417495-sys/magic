#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed partial class SkillJsonDto
{
    private string? _iconId;
    private string? _dynamicMaxLevelStatId;
    private string? _unlockMode;
    private string? _coreSkillTransitionMode;
    private string? _growthTier;
    private string? _practiceTier;
    private IReadOnlyList<int>? _masteryCurve;
    private IReadOnlyList<string>? _learnRequirements;
    private IReadOnlyList<string>? _knowledgeRequirements;
    private IReadOnlyDictionary<string, int>? _skillLevelRequirements;
    private IReadOnlyDictionary<string, int>? _attributeRequirements;
    private IReadOnlyList<string>? _achievementRequirements;
    private IReadOnlyList<string>? _upgradeSourceSkillIds;
    private IReadOnlyList<string>? _masterySources;
    private IReadOnlyDictionary<string, int>? _attributeGrowthProgress;
    private IReadOnlyList<AttributeModifierJsonDto>? _attributeModifiers;

    [JsonPropertyName("icon_id")] public string IconId { get => _iconId ?? ""; init => _iconId = value; }
    [JsonPropertyName("non_core_max_level")] public int NonCoreMaxLevel { get; init; }
    [JsonPropertyName("dynamic_max_level_stat_id")] public string DynamicMaxLevelStatId { get => _dynamicMaxLevelStatId ?? ""; init => _dynamicMaxLevelStatId = value; }
    [JsonPropertyName("dynamic_max_level_base")] public int DynamicMaxLevelBase { get; init; }
    [JsonPropertyName("dynamic_max_level_per_stat")] public int DynamicMaxLevelPerStat { get; init; }
    [JsonPropertyName("mastery_curve")] public IReadOnlyList<int> MasteryCurve { get => _masteryCurve ?? Array.Empty<int>(); init => _masteryCurve = value; }
    [JsonPropertyName("learn_requirements")] public IReadOnlyList<string> LearnRequirements { get => _learnRequirements ?? Array.Empty<string>(); init => _learnRequirements = value; }
    [JsonPropertyName("unlock_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillUnlockModeSchemaValues))]
    public string UnlockMode { get => _unlockMode ?? "standard"; init => _unlockMode = value; }
    [JsonPropertyName("knowledge_requirements")] public IReadOnlyList<string> KnowledgeRequirements { get => _knowledgeRequirements ?? Array.Empty<string>(); init => _knowledgeRequirements = value; }
    [JsonPropertyName("skill_level_requirements")] public IReadOnlyDictionary<string, int> SkillLevelRequirements { get => _skillLevelRequirements ?? EmptyIntMap; init => _skillLevelRequirements = value; }
    [JsonPropertyName("attribute_requirements")] public IReadOnlyDictionary<string, int> AttributeRequirements { get => _attributeRequirements ?? EmptyIntMap; init => _attributeRequirements = value; }
    [JsonPropertyName("achievement_requirements")] public IReadOnlyList<string> AchievementRequirements { get => _achievementRequirements ?? Array.Empty<string>(); init => _achievementRequirements = value; }
    [JsonPropertyName("upgrade_source_skill_ids")] public IReadOnlyList<string> UpgradeSourceSkillIds { get => _upgradeSourceSkillIds ?? Array.Empty<string>(); init => _upgradeSourceSkillIds = value; }
    [JsonPropertyName("retain_source_skills_on_unlock")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? RetainSourceSkillsOnUnlock { get; init; }
    [JsonPropertyName("core_skill_transition_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillCoreTransitionSchemaValues))]
    public string CoreSkillTransitionMode { get => _coreSkillTransitionMode ?? "inherit"; init => _coreSkillTransitionMode = value; }
    [JsonPropertyName("mastery_sources")] public IReadOnlyList<string> MasterySources { get => _masterySources ?? Array.Empty<string>(); init => _masterySources = value; }
    [JsonPropertyName("growth_tier")]
    [ContentJsonSchemaStableStringValues(typeof(SkillProgressionTierSchemaValues))]
    public string GrowthTier { get => _growthTier ?? ""; init => _growthTier = value; }
    [JsonPropertyName("attribute_growth_progress")]
    [Description(
        "Positive base-attribute allocations. When present, values must sum exactly to the selected growth_tier budget: basic=60, intermediate=120, advanced=180, ultimate=240."
    )]
    public IReadOnlyDictionary<string, int> AttributeGrowthProgress { get => _attributeGrowthProgress ?? EmptyIntMap; init => _attributeGrowthProgress = value; }
    [JsonPropertyName("practice_tier")]
    [ContentJsonSchemaStableStringValues(typeof(SkillProgressionTierSchemaValues))]
    public string PracticeTier { get => _practiceTier ?? ""; init => _practiceTier = value; }
    [JsonPropertyName("attribute_modifiers")] public IReadOnlyList<AttributeModifierJsonDto> AttributeModifiers { get => _attributeModifiers ?? Array.Empty<AttributeModifierJsonDto>(); init => _attributeModifiers = value; }
    [JsonPropertyName("contingency_automation_profile")] public ContingencyAutomationJsonDto? ContingencyAutomationProfile { get; init; }

    private static IReadOnlyDictionary<string, int> EmptyIntMap { get; } =
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AttributeModifierJsonDto
{
    private string? _attributeId;
    private string? _mode;
    private string? _sourceType;
    private string? _sourceId;

    [JsonPropertyName("attribute_id")] public string AttributeId { get => _attributeId ?? ""; init => _attributeId = value; }
    [JsonPropertyName("mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttributeModifierModeSchemaValues))]
    public string Mode { get => _mode ?? "flat"; init => _mode = value; }
    [JsonPropertyName("value")] public int Value { get; init; }
    [JsonPropertyName("value_per_rank")] public int ValuePerRank { get; init; }
    [JsonPropertyName("source_type")] public string SourceType { get => _sourceType ?? ""; init => _sourceType = value; }
    [JsonPropertyName("source_id")] public string SourceId { get => _sourceId ?? ""; init => _sourceId = value; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyAutomationJsonDto
{
    private string? _effectCategory;
    private IReadOnlyList<string>? _tags;
    private IReadOnlyList<string>? _allowedTargetResolvers;
    private IReadOnlyDictionary<string, JsonElement>? _allowedParameterBindings;

    [JsonPropertyName("can_be_stored_in_contingency")] public bool CanBeStoredInContingency { get; init; }
    [JsonPropertyName("min_contingency_skill_level")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinContingencySkillLevel { get; init; }
    [JsonPropertyName("effect_category")] public string EffectCategory { get => _effectCategory ?? ""; init => _effectCategory = value; }
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get => _tags ?? Array.Empty<string>(); init => _tags = value; }
    [JsonPropertyName("contingency_load_override")] public int ContingencyLoadOverride { get; init; }
    [JsonPropertyName("allowed_target_resolvers")] public IReadOnlyList<string> AllowedTargetResolvers { get => _allowedTargetResolvers ?? Array.Empty<string>(); init => _allowedTargetResolvers = value; }
    [JsonPropertyName("requires_manual_targeting")] public bool RequiresManualTargeting { get; init; }
    [JsonPropertyName("allowed_parameter_bindings")]
    [ContentJsonSchemaScalarOrStringArrayDictionaryValues]
    public IReadOnlyDictionary<string, JsonElement> AllowedParameterBindings { get => _allowedParameterBindings ?? EmptyBindings; init => _allowedParameterBindings = value; }

    private static IReadOnlyDictionary<string, JsonElement> EmptyBindings { get; } =
        new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>());
}

internal sealed partial class CombatSkillJsonDto
{
    private string? _weaponRangePolicy;
    private string? _pendingCastBindingMode;
    private string? _attackResolutionMode;
    private string? _attackDefenseMode;
    private string? _masteryTriggerMode;
    private string? _masteryAmountMode;
    private string? _spellFateMode;
    private string? _spellCriticalMode;
    private string? _backlashMode;
    private string? _backlashTargetFilter;
    private string? _areaOriginMode;
    private string? _areaDirectionMode;
    private string? _attackRollBonusStatusId;
    private string? _projectileKind;
    private string? _specialResolutionProfileId;
    private string? _targetSelectionMode;
    private string? _unitTargetResolutionMode;
    private string? _selectionOrderMode;
    private IReadOnlyList<string>? _excludedTargetCreatureTypeTags;
    private IReadOnlyList<int>? _fumbleProtectionCurve;
    private IReadOnlyList<string>? _aiTags;
    private IReadOnlyList<string>? _deliveryCategories;
    private IReadOnlyList<CombatEffectJsonDto>? _passiveEffectDefs;
    private IReadOnlyList<CombatCastVariantJsonDto>? _castVariants;
    private IReadOnlyList<string>? _requiredWeaponFamilies;
    private IReadOnlyList<string>? _requiredWeaponTypeIds;
    private IReadOnlyList<string>? _excludedWeaponFamilies;
    private IReadOnlyList<string>? _excludedWeaponTypeIds;

    [JsonPropertyName("excluded_target_creature_type_tags")] public IReadOnlyList<string> ExcludedTargetCreatureTypeTags { get => _excludedTargetCreatureTypeTags ?? Array.Empty<string>(); init => _excludedTargetCreatureTypeTags = value; }
    [JsonPropertyName("range_move_point_capacity_multiplier")] public int RangeMovePointCapacityMultiplier { get; init; }
    [JsonPropertyName("weapon_range_policy")]
    [ContentJsonSchemaStableStringValues(typeof(SkillWeaponRangePolicySchemaValues))]
    public string WeaponRangePolicy { get => _weaponRangePolicy ?? ""; init => _weaponRangePolicy = value; }
    [JsonPropertyName("area_value")] public int AreaValue { get; init; }
    [JsonPropertyName("requires_los")] public bool RequiresLos { get; init; }
    [JsonPropertyName("ground_effect_require_full_area")] public bool GroundEffectRequireFullArea { get; init; }
    [JsonPropertyName("ground_effect_require_empty")] public bool GroundEffectRequireEmpty { get; init; }
    [JsonPropertyName("ground_effect_require_traversable")] public bool GroundEffectRequireTraversable { get; init; }
    [JsonPropertyName("stamina_cost")] public int StaminaCost { get; init; }
    [JsonPropertyName("mp_cost_per_target_slot")] public int MpCostPerTargetSlot { get; init; }
    [JsonPropertyName("stamina_cost_per_target_slot")] public int StaminaCostPerTargetSlot { get; init; }
    [JsonPropertyName("casting_time_tu")] public int CastingTimeTu { get; init; }
    [JsonPropertyName("casting_maintenance_dc")] public int CastingMaintenanceDc { get; init; }
    [JsonPropertyName("casting_spell_control_dc")] public int CastingSpellControlDc { get; init; }
    [JsonPropertyName("windup_profile")] public CombatWindupJsonDto? WindupProfile { get; init; }
    [JsonPropertyName("directional_piercing_profile")] public CombatDirectionalPiercingJsonDto? DirectionalPiercingProfile { get; init; }
    [JsonPropertyName("approach_attack_profile")] public CombatApproachAttackJsonDto? ApproachAttackProfile { get; init; }
    [JsonPropertyName("line_through_attack_profile")] public CombatLineThroughAttackJsonDto? LineThroughAttackProfile { get; init; }
    [JsonPropertyName("sequential_line_hit_profile")] public CombatSequentialLineHitJsonDto? SequentialLineHitProfile { get; init; }
    [JsonPropertyName("spell_reaction_profile")] public CombatSpellReactionJsonDto? SpellReactionProfile { get; init; }
    [JsonPropertyName("ranged_weapon_reaction_profile")] public CombatRangedWeaponReactionJsonDto? RangedWeaponReactionProfile { get; init; }
    [JsonPropertyName("pending_cast_binding_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillPendingCastBindingModeSchemaValues))]
    public string PendingCastBindingMode { get => _pendingCastBindingMode ?? "soft_anchor"; init => _pendingCastBindingMode = value; }
    [JsonPropertyName("attack_roll_bonus")] public int AttackRollBonus { get; init; }
    [JsonPropertyName("attack_resolution_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttackResolutionModeSchemaValues))]
    public string AttackResolutionMode { get => _attackResolutionMode ?? ""; init => _attackResolutionMode = value; }
    [JsonPropertyName("attack_defense_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttackDefenseModeSchemaValues))]
    public string AttackDefenseMode { get => _attackDefenseMode ?? "normal"; init => _attackDefenseMode = value; }
    [JsonPropertyName("aura_cost")] public int AuraCost { get; init; }
    [JsonPropertyName("mastery_trigger_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillMasteryTriggerSchemaValues))]
    public string MasteryTriggerMode { get => _masteryTriggerMode ?? "skill_damage_dice_max"; init => _masteryTriggerMode = value; }
    [JsonPropertyName("mastery_amount_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillMasteryAmountSchemaValues))]
    public string MasteryAmountMode { get => _masteryAmountMode ?? "per_target_rank"; init => _masteryAmountMode = value; }
    [JsonPropertyName("mastery_base_amount")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MasteryBaseAmount { get; init; }
    [JsonPropertyName("spell_fate_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillSpellFateSchemaValues))]
    public string SpellFateMode { get => _spellFateMode ?? ""; init => _spellFateMode = value; }
    [JsonPropertyName("spell_critical_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillSpellCriticalSchemaValues))]
    public string SpellCriticalMode { get => _spellCriticalMode ?? ""; init => _spellCriticalMode = value; }
    [JsonPropertyName("spell_critical_mp_refund_percent")] public int SpellCriticalMpRefundPercent { get; init; }
    [JsonPropertyName("fumble_protection_curve")] public IReadOnlyList<int> FumbleProtectionCurve { get => _fumbleProtectionCurve ?? Array.Empty<int>(); init => _fumbleProtectionCurve = value; }
    [JsonPropertyName("fumble_protection_extra_mp_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? FumbleProtectionExtraMpPercent { get; init; }
    [JsonPropertyName("backlash_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillBacklashSchemaValues))]
    public string BacklashMode { get => _backlashMode ?? ""; init => _backlashMode = value; }
    [JsonPropertyName("backlash_target_filter")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTargetTeamFilterSchemaValues))]
    public string BacklashTargetFilter { get => _backlashTargetFilter ?? ""; init => _backlashTargetFilter = value; }
    [JsonPropertyName("backlash_offset_radius")] public int BacklashOffsetRadius { get; init; }
    [JsonPropertyName("area_origin_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAreaOriginSchemaValues))]
    public string AreaOriginMode { get => _areaOriginMode ?? "target"; init => _areaOriginMode = value; }
    [JsonPropertyName("area_direction_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAreaDirectionSchemaValues))]
    public string AreaDirectionMode { get => _areaDirectionMode ?? "target_vector"; init => _areaDirectionMode = value; }
    [JsonPropertyName("ai_tags")] public IReadOnlyList<string> AiTags { get => _aiTags ?? Array.Empty<string>(); init => _aiTags = value; }
    [JsonPropertyName("delivery_categories")] public IReadOnlyList<string> DeliveryCategories { get => _deliveryCategories ?? Array.Empty<string>(); init => _deliveryCategories = value; }
    [JsonPropertyName("attack_roll_bonus_status_id")] public string AttackRollBonusStatusId { get => _attackRollBonusStatusId ?? ""; init => _attackRollBonusStatusId = value; }
    [JsonPropertyName("attack_roll_bonus_status_stack_divisor")] public int AttackRollBonusStatusStackDivisor { get; init; }
    [JsonPropertyName("projectile_kind")]
    [ContentJsonSchemaStableStringValues(typeof(SkillBaseProjectileSchemaValues))]
    public string ProjectileKind { get => _projectileKind ?? "none"; init => _projectileKind = value; }
    [JsonPropertyName("special_resolution_profile_id")] public string SpecialResolutionProfileId { get => _specialResolutionProfileId ?? ""; init => _specialResolutionProfileId = value; }
    [JsonPropertyName("target_selection_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTargetSelectionSchemaValues))]
    public string TargetSelectionMode { get => _targetSelectionMode ?? "single_unit"; init => _targetSelectionMode = value; }
    [JsonPropertyName("min_target_count")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinTargetCount { get; init; }
    [JsonPropertyName("max_target_count")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaxTargetCount { get; init; }
    [JsonPropertyName("allow_repeat_target")] public bool AllowRepeatTarget { get; init; }
    [JsonPropertyName("unit_target_resolution_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillUnitTargetResolutionSchemaValues))]
    public string UnitTargetResolutionMode { get => _unitTargetResolutionMode ?? "aggregate"; init => _unitTargetResolutionMode = value; }
    [JsonPropertyName("max_hits_per_target")] public int MaxHitsPerTarget { get; init; }
    [JsonPropertyName("random_chain_attack_count")] public int RandomChainAttackCount { get; init; }
    [JsonPropertyName("random_chain_continue_on_miss")] public bool RandomChainContinueOnMiss { get; init; }
    [JsonPropertyName("selection_order_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillSelectionOrderSchemaValues))]
    public string SelectionOrderMode { get => _selectionOrderMode ?? "stable"; init => _selectionOrderMode = value; }
    [JsonPropertyName("passive_effect_defs")] public IReadOnlyList<CombatEffectJsonDto> PassiveEffectDefs { get => _passiveEffectDefs ?? Array.Empty<CombatEffectJsonDto>(); init => _passiveEffectDefs = value; }
    [JsonPropertyName("cast_variants")] public IReadOnlyList<CombatCastVariantJsonDto> CastVariants { get => _castVariants ?? Array.Empty<CombatCastVariantJsonDto>(); init => _castVariants = value; }
    [JsonPropertyName("required_weapon_families")] public IReadOnlyList<string> RequiredWeaponFamilies { get => _requiredWeaponFamilies ?? Array.Empty<string>(); init => _requiredWeaponFamilies = value; }
    [JsonPropertyName("allows_natural_weapon")] public bool AllowsNaturalWeapon { get; init; }
    [JsonPropertyName("requires_heavy_weapon")] public bool RequiresHeavyWeapon { get; init; }
    [JsonPropertyName("required_weapon_type_ids")] public IReadOnlyList<string> RequiredWeaponTypeIds { get => _requiredWeaponTypeIds ?? Array.Empty<string>(); init => _requiredWeaponTypeIds = value; }
    [JsonPropertyName("excluded_weapon_families")] public IReadOnlyList<string> ExcludedWeaponFamilies { get => _excludedWeaponFamilies ?? Array.Empty<string>(); init => _excludedWeaponFamilies = value; }
    [JsonPropertyName("excluded_weapon_type_ids")] public IReadOnlyList<string> ExcludedWeaponTypeIds { get => _excludedWeaponTypeIds ?? Array.Empty<string>(); init => _excludedWeaponTypeIds = value; }
    [JsonPropertyName("requires_equipped_shield")] public bool RequiresEquippedShield { get; init; }
    [JsonPropertyName("mastery_low_hp_bonus_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MasteryLowHpBonusMultiplier { get; init; }
    [JsonPropertyName("mastery_low_hp_threshold_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MasteryLowHpThresholdPercent { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatWindupJsonDto
{
    private IReadOnlyList<int>? _skillLevelTierCaps;
    private IReadOnlyList<int>? _baseWeaponDiceMultipliers;

    [JsonPropertyName("stamina_cost_per_tier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaCostPerTier { get; init; }
    [JsonPropertyName("weapon_dice_per_tier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? WeaponDicePerTier { get; init; }
    [JsonPropertyName("skill_level_tier_caps")] public IReadOnlyList<int> SkillLevelTierCaps { get => _skillLevelTierCaps ?? DefaultSkillLevelTierCaps; init => _skillLevelTierCaps = value; }
    [JsonPropertyName("base_weapon_dice_multipliers")] public IReadOnlyList<int> BaseWeaponDiceMultipliers { get => _baseWeaponDiceMultipliers ?? DefaultBaseWeaponDiceMultipliers; init => _baseWeaponDiceMultipliers = value; }

    private static IReadOnlyList<int> DefaultSkillLevelTierCaps { get; } = Array.AsReadOnly(new[] { 1, 1, 2, 2, 3, 0 });
    private static IReadOnlyList<int> DefaultBaseWeaponDiceMultipliers { get; } = Array.AsReadOnly(new[] { 1, 1, 1, 1, 1, 2 });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatDirectionalPiercingJsonDto
{
    private IReadOnlyList<int>? _baseDamagePercentCurve;

    [JsonPropertyName("base_damage_percent_curve")] public IReadOnlyList<int> BaseDamagePercentCurve { get => _baseDamagePercentCurve ?? Array.Empty<int>(); init => _baseDamagePercentCurve = value; }
    [JsonPropertyName("successful_hit_decay_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? SuccessfulHitDecayPercent { get; init; }
    [JsonPropertyName("minimum_damage_percent")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinimumDamagePercent { get; init; }
    [JsonPropertyName("stamina_flat_base")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaFlatBase { get; init; }
    [JsonPropertyName("stamina_range_square_coefficient")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaRangeSquareCoefficient { get; init; }
    [JsonPropertyName("stamina_strength_square_scale")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? StaminaStrengthSquareScale { get; init; }
    [JsonPropertyName("minimum_stamina_cost")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MinimumStaminaCost { get; init; }
    [JsonPropertyName("maximum_height_delta")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaximumHeightDelta { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatApproachAttackJsonDto
{
    [JsonPropertyName("maximum_path_height_delta_from_origin")] public int MaximumPathHeightDeltaFromOrigin { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatLineThroughAttackJsonDto
{
    private IReadOnlyList<int>? _primaryWeaponDiceMultiplierCurve;
    private IReadOnlyList<int>? _primaryAttackRollBonusCurve;
    private IReadOnlyList<int>? _successfulIntermediateHitBonusCapCurve;

    [JsonPropertyName("maximum_weapon_range")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? MaximumWeaponRange { get; init; }
    [JsonPropertyName("intermediate_weapon_dice_multiplier")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? IntermediateWeaponDiceMultiplier { get; init; }
    [JsonPropertyName("primary_weapon_dice_multiplier_curve")] public IReadOnlyList<int> PrimaryWeaponDiceMultiplierCurve { get => _primaryWeaponDiceMultiplierCurve ?? Array.Empty<int>(); init => _primaryWeaponDiceMultiplierCurve = value; }
    [JsonPropertyName("primary_attack_roll_bonus_curve")] public IReadOnlyList<int> PrimaryAttackRollBonusCurve { get => _primaryAttackRollBonusCurve ?? Array.Empty<int>(); init => _primaryAttackRollBonusCurve = value; }
    [JsonPropertyName("successful_intermediate_hit_bonus_weapon_dice")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? SuccessfulIntermediateHitBonusWeaponDice { get; init; }
    [JsonPropertyName("successful_intermediate_hit_attack_roll_bonus")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? SuccessfulIntermediateHitAttackRollBonus { get; init; }
    [JsonPropertyName("successful_intermediate_hit_bonus_cap_curve")] public IReadOnlyList<int> SuccessfulIntermediateHitBonusCapCurve { get => _successfulIntermediateHitBonusCapCurve ?? Array.Empty<int>(); init => _successfulIntermediateHitBonusCapCurve = value; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatSequentialLineHitJsonDto
{
    private IReadOnlyList<int>? _minimumPrimaryDistanceCurve;
    private IReadOnlyList<int>? _continuationRangeCurve;
    private IReadOnlyList<int>? _followUpAttackPenaltyCurve;

    [JsonPropertyName("minimum_primary_distance_curve")] public IReadOnlyList<int> MinimumPrimaryDistanceCurve { get => _minimumPrimaryDistanceCurve ?? Array.Empty<int>(); init => _minimumPrimaryDistanceCurve = value; }
    [JsonPropertyName("continuation_range_curve")] public IReadOnlyList<int> ContinuationRangeCurve { get => _continuationRangeCurve ?? Array.Empty<int>(); init => _continuationRangeCurve = value; }
    [JsonPropertyName("follow_up_attack_penalty_curve")] public IReadOnlyList<int> FollowUpAttackPenaltyCurve { get => _followUpAttackPenaltyCurve ?? Array.Empty<int>(); init => _followUpAttackPenaltyCurve = value; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatSpellReactionJsonDto
{
    private string? _triggerDeliveryCategory;
    private string? _reactionSkillId;
    private string? _readinessStatusId;
    private string? _requiredWeaponFamily;
    private string? _saveAbility;
    private string? _saveTag;
    private IReadOnlyList<int>? _attackRollBonusBySkillLevel;
    private IReadOnlyList<int>? _saveDcBonusBySkillLevel;

    [JsonPropertyName("trigger_delivery_category")] public string TriggerDeliveryCategory { get => _triggerDeliveryCategory ?? "spell"; init => _triggerDeliveryCategory = value; }
    [JsonPropertyName("reaction_skill_id")] public string ReactionSkillId { get => _reactionSkillId ?? "basic_attack"; init => _reactionSkillId = value; }
    [JsonPropertyName("readiness_status_id")] public string ReadinessStatusId { get => _readinessStatusId ?? ""; init => _readinessStatusId = value; }
    [JsonPropertyName("required_weapon_family")] public string RequiredWeaponFamily { get => _requiredWeaponFamily ?? ""; init => _requiredWeaponFamily = value; }
    [JsonPropertyName("save_ability")]
    [ContentJsonSchemaStableStringValues(typeof(SkillSaveAbilitySchemaValues))]
    public string SaveAbility { get => _saveAbility ?? "constitution"; init => _saveAbility = value; }
    [JsonPropertyName("save_tag")] public string SaveTag { get => _saveTag ?? "spell_maintenance"; init => _saveTag = value; }
    [JsonPropertyName("base_save_dc")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? BaseSaveDc { get; init; }
    [JsonPropertyName("hp_damage_divisor")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? HpDamageDivisor { get; init; }
    [JsonPropertyName("attack_roll_bonus_by_skill_level")] public IReadOnlyList<int> AttackRollBonusBySkillLevel { get => _attackRollBonusBySkillLevel ?? Array.Empty<int>(); init => _attackRollBonusBySkillLevel = value; }
    [JsonPropertyName("save_dc_bonus_by_skill_level")] public IReadOnlyList<int> SaveDcBonusBySkillLevel { get => _saveDcBonusBySkillLevel ?? Array.Empty<int>(); init => _saveDcBonusBySkillLevel = value; }
    [JsonPropertyName("require_hp_damage")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? RequireHpDamage { get; init; }
    [JsonPropertyName("consume_on_trigger")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? ConsumeOnTrigger { get; init; }
    [JsonPropertyName("expire_on_owner_turn_start")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? ExpireOnOwnerTurnStart { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatRangedWeaponReactionJsonDto
{
    private string? _readinessStatusId;
    private string? _damageTag;
    private string? _attackDefenseMode;
    private IReadOnlyList<string>? _triggerWeaponFamilies;
    private IReadOnlyList<int>? _attackRollBonusBySkillLevel;

    [JsonPropertyName("readiness_status_id")] public string ReadinessStatusId { get => _readinessStatusId ?? ""; init => _readinessStatusId = value; }
    [JsonPropertyName("trigger_weapon_families")] public IReadOnlyList<string> TriggerWeaponFamilies { get => _triggerWeaponFamilies ?? Array.Empty<string>(); init => _triggerWeaponFamilies = value; }
    [JsonPropertyName("damage_tag")]
    [ContentJsonSchemaStableStringValues(typeof(SkillDamageTagSchemaValues))]
    public string DamageTag { get => _damageTag ?? "force"; init => _damageTag = value; }
    [JsonPropertyName("attack_defense_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillAttackDefenseModeSchemaValues))]
    public string AttackDefenseMode { get => _attackDefenseMode ?? "touch"; init => _attackDefenseMode = value; }
    [JsonPropertyName("attack_roll_bonus_by_skill_level")] public IReadOnlyList<int> AttackRollBonusBySkillLevel { get => _attackRollBonusBySkillLevel ?? Array.Empty<int>(); init => _attackRollBonusBySkillLevel = value; }
    [JsonPropertyName("consume_status_stacks")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? ConsumeStatusStacks { get; init; }
    [JsonPropertyName("trigger_on_hit")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? TriggerOnHit { get; init; }
    [JsonPropertyName("trigger_on_miss")]
    [ContentJsonSchemaDisallowExplicitNull]
    public bool? TriggerOnMiss { get; init; }
    [JsonPropertyName("allow_critical")] public bool AllowCritical { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatCastVariantJsonDto
{
    private string? _displayName;
    private string? _description;
    private string? _targetMode;
    private string? _footprintPattern;
    private string? _projectileKindOverride;
    private IReadOnlyList<string>? _allowedBaseTerrains;
    private IReadOnlyList<CombatEffectJsonDto>? _effectDefs;

    [JsonPropertyName("variant_id")] [JsonRequired] public string VariantId { get; init; } = null!;
    [JsonPropertyName("display_name")] public string DisplayName { get => _displayName ?? ""; init => _displayName = value; }
    [JsonPropertyName("description")] public string Description { get => _description ?? ""; init => _description = value; }
    [JsonPropertyName("min_skill_level")] public int MinSkillLevel { get; init; }
    [JsonPropertyName("target_mode")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTargetModeSchemaValues))]
    public string TargetMode { get => _targetMode ?? "ground"; init => _targetMode = value; }
    [JsonPropertyName("footprint_pattern")]
    [ContentJsonSchemaStableStringValues(typeof(SkillCastFootprintSchemaValues))]
    public string FootprintPattern { get => _footprintPattern ?? "single"; init => _footprintPattern = value; }
    [JsonPropertyName("required_coord_count")]
    [ContentJsonSchemaDisallowExplicitNull]
    public int? RequiredCoordCount { get; init; }
    [JsonPropertyName("allowed_base_terrains")]
    [ContentJsonSchemaStableStringValues(typeof(SkillTerrainSchemaValues))]
    public IReadOnlyList<string> AllowedBaseTerrains { get => _allowedBaseTerrains ?? Array.Empty<string>(); init => _allowedBaseTerrains = value; }
    [JsonPropertyName("projectile_kind_override")]
    [ContentJsonSchemaStableStringValues(typeof(SkillBaseProjectileSchemaValues))]
    public string ProjectileKindOverride { get => _projectileKindOverride ?? ""; init => _projectileKindOverride = value; }
    [JsonPropertyName("effect_defs")] public IReadOnlyList<CombatEffectJsonDto> EffectDefs { get => _effectDefs ?? Array.Empty<CombatEffectJsonDto>(); init => _effectDefs = value; }
    [JsonPropertyName("payload")]
    [ContentJsonSchemaDisallowExplicitNull]
    public CombatCastVariantPayloadJsonDto? Payload { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CombatCastVariantPayloadJsonDto
{
    [JsonPropertyName("square2_corner")]
    [ContentJsonSchemaStableStringValues(typeof(SkillSquare2CornerSchemaValues))]
    [ContentJsonSchemaDisallowExplicitNull]
    public string? Square2Corner { get; init; }
}
