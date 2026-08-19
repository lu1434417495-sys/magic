#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityContentPackJsonDto
{
    [JsonPropertyName("pack_id")]
    [JsonRequired]
    public string PackId { get; init; } = "";

    [JsonPropertyName("schema_version")]
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("load_order")]
    [JsonRequired]
    public int LoadOrder { get; init; }

    [JsonPropertyName("dependencies")]
    [JsonRequired]
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    [JsonPropertyName("bindings")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityBindingJsonDto> Bindings { get; init; } = Array.Empty<EquipmentAbilityBindingJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityBindingJsonDto
{
    [JsonPropertyName("binding_id")]
    [JsonRequired]
    public string BindingId { get; init; } = "";

    [JsonPropertyName("trait_id")]
    [JsonRequired]
    public string TraitId { get; init; } = "";

    [JsonPropertyName("override_mode")]
    [JsonRequired]
    public string OverrideMode { get; init; } = "";

    [JsonPropertyName("replaces_binding_id")]
    [JsonRequired]
    public string ReplacesBindingId { get; init; } = "";

    [JsonPropertyName("allowed_source_kinds")]
    [JsonRequired]
    public IReadOnlyList<string> AllowedSourceKinds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_trait_categories")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredTraitCategories { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_effective_trait_ids")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredEffectiveTraitIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_item_tags")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredItemTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("supported_equipment_type_ids")]
    [JsonRequired]
    public IReadOnlyList<string> SupportedEquipmentTypeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("activation_status_id")]
    [JsonRequired]
    public string ActivationStatusId { get; init; } = "";

    [JsonPropertyName("state_schemas")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityStateSchemaJsonDto> StateSchemas { get; init; } = Array.Empty<EquipmentAbilityStateSchemaJsonDto>();

    [JsonPropertyName("reactions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityReactionJsonDto> Reactions { get; init; } = Array.Empty<EquipmentAbilityReactionJsonDto>();

    [JsonPropertyName("fatal_intercepts")]
    [JsonRequired]
    public IReadOnlyList<EquipmentFatalInterceptJsonDto> FatalIntercepts { get; init; } = Array.Empty<EquipmentFatalInterceptJsonDto>();

    [JsonPropertyName("mitigation_auras")]
    [JsonRequired]
    public IReadOnlyList<EquipmentMitigationAuraJsonDto> MitigationAuras { get; init; } = Array.Empty<EquipmentMitigationAuraJsonDto>();

    [JsonPropertyName("movement_trails")]
    [JsonRequired]
    public IReadOnlyList<EquipmentMovementTrailJsonDto> MovementTrails { get; init; } = Array.Empty<EquipmentMovementTrailJsonDto>();

    [JsonPropertyName("granted_actions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentGrantedActionJsonDto> GrantedActions { get; init; } = Array.Empty<EquipmentGrantedActionJsonDto>();

    [JsonPropertyName("temporal_progress_modifiers")]
    [JsonRequired]
    public IReadOnlyList<EquipmentTemporalProgressModifierJsonDto> TemporalProgressModifiers { get; init; } = Array.Empty<EquipmentTemporalProgressModifierJsonDto>();

    [JsonPropertyName("cognition_ceiling_modifiers")]
    [JsonRequired]
    public IReadOnlyList<EquipmentCognitionCeilingModifierJsonDto> CognitionCeilingModifiers { get; init; } = Array.Empty<EquipmentCognitionCeilingModifierJsonDto>();

    [JsonPropertyName("weapon_profile_overlays")]
    [JsonRequired]
    public IReadOnlyList<EquipmentWeaponProfileOverlayJsonDto> WeaponProfileOverlays { get; init; } = Array.Empty<EquipmentWeaponProfileOverlayJsonDto>();

    [JsonPropertyName("world_effects")]
    [JsonRequired]
    public IReadOnlyList<EquipmentWorldEffectJsonDto> WorldEffects { get; init; } = Array.Empty<EquipmentWorldEffectJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityReactionJsonDto
{
    [JsonPropertyName("reaction_id")]
    [JsonRequired]
    public string ReactionId { get; init; } = "";

    [JsonPropertyName("trigger")]
    [JsonRequired]
    public string Trigger { get; init; } = "";

    [JsonPropertyName("timing")]
    [JsonRequired]
    public string Timing { get; init; } = "";

    [JsonPropertyName("priority")]
    [JsonRequired]
    public int Priority { get; init; }

    [JsonPropertyName("once_scope")]
    [JsonRequired]
    public string OnceScope { get; init; } = "";

    [JsonPropertyName("requires_player_confirmation")]
    [JsonRequired]
    public bool RequiresPlayerConfirmation { get; init; }

    [JsonPropertyName("condition_group")]
    [JsonRequired]
    public EquipmentAbilityConditionGroupJsonDto ConditionGroup { get; init; } = null!;

    [JsonPropertyName("roll_gate")]
    [JsonRequired]
    public EquipmentRollGateJsonDto RollGate { get; init; } = null!;

    [JsonPropertyName("outcome_table")]
    [JsonRequired]
    public EquipmentOutcomeTableJsonDto OutcomeTable { get; init; } = null!;

    [JsonPropertyName("projected_effect_categories")]
    [JsonRequired]
    public IReadOnlyList<string> ProjectedEffectCategories { get; init; } = Array.Empty<string>();

    [JsonPropertyName("actions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityActionJsonDto> Actions { get; init; } = Array.Empty<EquipmentAbilityActionJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityConditionGroupJsonDto
{
    [JsonPropertyName("mode")]
    [JsonRequired]
    public string Mode { get; init; } = "";

    [JsonPropertyName("negate")]
    [JsonRequired]
    public bool Negate { get; init; }

    [JsonPropertyName("conditions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityConditionJsonDto> Conditions { get; init; } = Array.Empty<EquipmentAbilityConditionJsonDto>();

    [JsonPropertyName("groups")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityConditionGroupJsonDto> Groups { get; init; } = Array.Empty<EquipmentAbilityConditionGroupJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(EquipmentAbilityConditionClosedKindSchemaSpec))]
internal sealed class EquipmentAbilityConditionJsonDto
{
    [JsonPropertyName("condition_id")]
    [JsonRequired]
    public string ConditionId { get; init; } = "";

    [JsonPropertyName("kind")]
    [JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload")]
    [JsonRequired]
    public object Payload { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HasStatusConditionPayloadJsonDto
{
    [JsonPropertyName("subject")]
    [JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CompareFactConditionPayloadJsonDto
{
    [JsonPropertyName("left")]
    [JsonRequired]
    public EquipmentAbilityFactQueryJsonDto Left { get; init; } = null!;

    [JsonPropertyName("compare")]
    [JsonRequired]
    public string Compare { get; init; } = "";

    [JsonPropertyName("right")]
    [JsonRequired]
    public EquipmentAbilityFactQueryJsonDto Right { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HasEquipmentTagConditionPayloadJsonDto
{
    [JsonPropertyName("subject")]
    [JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("equipment_selector")]
    [JsonRequired]
    public string EquipmentSelector { get; init; } = "";

    [JsonPropertyName("all_tags")]
    [JsonRequired]
    public IReadOnlyList<string> AllTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("any_tags")]
    [JsonRequired]
    public IReadOnlyList<string> AnyTags { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityFactQueryJsonDto
{
    [JsonPropertyName("query_kind")]
    [JsonRequired]
    public string QueryKind { get; init; } = "";

    [JsonPropertyName("fact_id")]
    [JsonRequired]
    public string FactId { get; init; } = "";

    [JsonPropertyName("subject")]
    [JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("binding_id")]
    [JsonRequired]
    public string BindingId { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("require_source_unit_match")]
    [JsonRequired]
    public bool RequireSourceUnitMatch { get; init; }

    [JsonPropertyName("attribute_id")]
    [JsonRequired]
    public string AttributeId { get; init; } = "";

    [JsonPropertyName("aggregation")]
    [JsonRequired]
    public string Aggregation { get; init; } = "";

    [JsonPropertyName("value_kind")]
    [JsonRequired]
    public string ValueKind { get; init; } = "";

    [JsonPropertyName("bool_literal")]
    [JsonRequired]
    public bool BoolLiteral { get; init; }

    [JsonPropertyName("int_literal")]
    [JsonRequired]
    public int IntLiteral { get; init; }

    [JsonPropertyName("float_literal")]
    [JsonRequired]
    public float FloatLiteral { get; init; }

    [JsonPropertyName("string_name_literal")]
    [JsonRequired]
    public string StringNameLiteral { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DiceExpressionJsonDto
{
    [JsonPropertyName("terms")]
    [JsonRequired]
    public IReadOnlyList<DiceExpressionTermJsonDto> Terms { get; init; } = Array.Empty<DiceExpressionTermJsonDto>();

    [JsonPropertyName("flat_bonus")]
    [JsonRequired]
    public int FlatBonus { get; init; }

    [JsonPropertyName("preview_policy")]
    [JsonRequired]
    public string PreviewPolicy { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DiceExpressionTermJsonDto
{
    [JsonPropertyName("dice_count")]
    [JsonRequired]
    public int DiceCount { get; init; }

    [JsonPropertyName("dice_sides")]
    [JsonRequired]
    public int DiceSides { get; init; }

    [JsonPropertyName("count_bonus_fact")]
    [JsonRequired]
    public EquipmentAbilityFactQueryJsonDto CountBonusFact { get; init; } = null!;

    [JsonPropertyName("count_bonus_multiplier")]
    [JsonRequired]
    public float CountBonusMultiplier { get; init; }

    [JsonPropertyName("max_dice_count")]
    [JsonRequired]
    public int MaxDiceCount { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(EquipmentAbilityActionClosedKindSchemaSpec))]
internal sealed class EquipmentAbilityActionJsonDto
{
    [JsonPropertyName("action_id")]
    [JsonRequired]
    public string ActionId { get; init; } = "";

    [JsonPropertyName("kind")]
    [JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload")]
    [JsonRequired]
    public object Payload { get; init; } = null!;

    [JsonPropertyName("condition_group")]
    [JsonRequired]
    public EquipmentAbilityConditionGroupJsonDto ConditionGroup { get; init; } = null!;

    [JsonPropertyName("roll_gate")]
    [JsonRequired]
    public EquipmentRollGateJsonDto RollGate { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AddDamageDiceActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("dice")]
    [JsonRequired]
    public DiceExpressionJsonDto Dice { get; init; } = null!;

    [JsonPropertyName("damage_type")]
    [JsonRequired]
    public string DamageType { get; init; } = "";

    [JsonPropertyName("require_weapon_damage")]
    [JsonRequired]
    public bool RequireWeaponDamage { get; init; }

    [JsonPropertyName("subtract")]
    [JsonRequired]
    public bool Subtract { get; init; }

    [JsonPropertyName("replacement_group_id")]
    [JsonRequired]
    public string ReplacementGroupId { get; init; } = "";

    [JsonPropertyName("replacement_priority")]
    [JsonRequired]
    public int ReplacementPriority { get; init; }

    [JsonPropertyName("damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mitigation_bypass_damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> MitigationBypassDamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mitigation_bypass_tiers")]
    [JsonRequired]
    public IReadOnlyList<string> MitigationBypassTiers { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ImmediateWeaponAttackActionPayloadJsonDto
{
    [JsonPropertyName("anchor_selector")]
    [JsonRequired]
    public string AnchorSelector { get; init; } = "";

    [JsonPropertyName("target_team_filter")]
    [JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("radius")]
    [JsonRequired]
    public int Radius { get; init; }

    [JsonPropertyName("max_attacks")]
    [JsonRequired]
    public int MaxAttacks { get; init; }

    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = "";

    [JsonPropertyName("require_weapon_range")]
    [JsonRequired]
    public bool RequireWeaponRange { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DealDamageActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("dice")]
    [JsonRequired]
    public DiceExpressionJsonDto Dice { get; init; } = null!;

    [JsonPropertyName("damage_type")]
    [JsonRequired]
    public string DamageType { get; init; } = "";

    [JsonPropertyName("damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mitigation_bypass_damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> MitigationBypassDamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mitigation_bypass_tiers")]
    [JsonRequired]
    public IReadOnlyList<string> MitigationBypassTiers { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HealActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("dice")]
    [JsonRequired]
    public DiceExpressionJsonDto Dice { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class HealFromFactActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("amount_fact")]
    [JsonRequired]
    public EquipmentAbilityFactQueryJsonDto AmountFact { get; init; } = null!;

    [JsonPropertyName("multiplier_percent")]
    [JsonRequired]
    public int MultiplierPercent { get; init; }

    [JsonPropertyName("max_amount")]
    [JsonRequired]
    public int MaxAmount { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AttackRollBonusActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("bonus")]
    [JsonRequired]
    public int Bonus { get; init; }

    [JsonPropertyName("attribute_modifier_id")]
    [JsonRequired]
    public string AttributeModifierId { get; init; } = "";

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    public string StackMode { get; init; } = "";

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

    [JsonPropertyName("require_weapon_damage")]
    [JsonRequired]
    public bool RequireWeaponDamage { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class AttackRollAdvantageActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("mode")]
    [JsonRequired]
    public string Mode { get; init; } = "";

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    public string StackMode { get; init; } = "";

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CriticalHitOverrideActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("require_weapon_damage")]
    [JsonRequired]
    public bool RequireWeaponDamage { get; init; }

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DamageRollModeOverrideActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("roll_mode")]
    [JsonRequired]
    public string RollMode { get; init; } = "";

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    public string StackMode { get; init; } = "";

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class DamageReductionActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("amount")]
    [JsonRequired]
    public int Amount { get; init; }

    [JsonPropertyName("damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class LootQuantityMultiplierActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("multiplier_percent")]
    [JsonRequired]
    public int MultiplierPercent { get; init; }

    [JsonPropertyName("affected_drop_kinds")]
    [JsonRequired]
    public IReadOnlyList<string> AffectedDropKinds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("any_item_tags")]
    [JsonRequired]
    public IReadOnlyList<string> AnyItemTags { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ApplyStatusActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("duration_turns")]
    [JsonRequired]
    public int DurationTurns { get; init; }

    [JsonPropertyName("duration_tu")]
    [JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("stack_delta")]
    [JsonRequired]
    public int StackDelta { get; init; }

    [JsonPropertyName("stack_behavior")]
    [JsonRequired]
    public string StackBehavior { get; init; } = "";

    [JsonPropertyName("stack_limit")]
    [JsonRequired]
    public int StackLimit { get; init; }

    [JsonPropertyName("display_label")]
    [JsonRequired]
    public string DisplayLabel { get; init; } = "";

    [JsonPropertyName("attack_roll_penalty")]
    [JsonRequired]
    public int AttackRollPenalty { get; init; }

    [JsonPropertyName("armor_class_bonus_per_stack")]
    [JsonRequired]
    public int ArmorClassBonusPerStack { get; init; }

    [JsonPropertyName("source_bound_attack_roll_penalty")]
    [JsonRequired]
    public int SourceBoundAttackRollPenalty { get; init; }

    [JsonPropertyName("source_bound_attack_roll_penalty_min_stacks")]
    [JsonRequired]
    public int SourceBoundAttackRollPenaltyMinStacks { get; init; }

    [JsonPropertyName("source_bound_incoming_attack_roll_bonus_per_stack")]
    [JsonRequired]
    public int SourceBoundIncomingAttackRollBonusPerStack { get; init; }

    [JsonPropertyName("source_bound_incoming_attack_roll_bonus_min_stacks")]
    [JsonRequired]
    public int SourceBoundIncomingAttackRollBonusMinStacks { get; init; }

    [JsonPropertyName("override_heal_multiplier_percent")]
    [JsonRequired]
    public bool OverrideHealMultiplierPercent { get; init; }

    [JsonPropertyName("heal_multiplier_percent")]
    [JsonRequired]
    public int HealMultiplierPercent { get; init; }

    [JsonPropertyName("move_point_capacity_delta")]
    [JsonRequired]
    public int MovePointCapacityDelta { get; init; }

    [JsonPropertyName("forced_move_immune")]
    [JsonRequired]
    public bool ForcedMoveImmune { get; init; }

    [JsonPropertyName("damage_tag")]
    [JsonRequired]
    public string DamageTag { get; init; } = "";

    [JsonPropertyName("damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("mitigation_tier")]
    [JsonRequired]
    public string MitigationTier { get; init; } = "";

    [JsonPropertyName("counts_as_debuff_override")]
    [JsonRequired]
    public bool CountsAsDebuffOverride { get; init; }

    [JsonPropertyName("counts_as_debuff")]
    [JsonRequired]
    public bool CountsAsDebuff { get; init; }

    [JsonPropertyName("undispellable")]
    [JsonRequired]
    public bool Undispellable { get; init; }

    [JsonPropertyName("dispellable_magic")]
    [JsonRequired]
    public bool DispellableMagic { get; init; }

    [JsonPropertyName("dispellable_harmful_magic")]
    [JsonRequired]
    public bool DispellableHarmfulMagic { get; init; }

    [JsonPropertyName("dispellable_beneficial_magic")]
    [JsonRequired]
    public bool DispellableBeneficialMagic { get; init; }

    [JsonPropertyName("lock_counterattack")]
    [JsonRequired]
    public bool LockCounterattack { get; init; }

    [JsonPropertyName("lock_guard")]
    [JsonRequired]
    public bool LockGuard { get; init; }

    [JsonPropertyName("lock_dodge_bonus")]
    [JsonRequired]
    public bool LockDodgeBonus { get; init; }

    [JsonPropertyName("tick_interval_tu")]
    [JsonRequired]
    public int TickIntervalTu { get; init; }

    [JsonPropertyName("timeline_damage_dice_count")]
    [JsonRequired]
    public int TimelineDamageDiceCount { get; init; }

    [JsonPropertyName("timeline_damage_dice_sides")]
    [JsonRequired]
    public int TimelineDamageDiceSides { get; init; }

    [JsonPropertyName("timeline_damage_flat_bonus")]
    [JsonRequired]
    public int TimelineDamageFlatBonus { get; init; }

    [JsonPropertyName("save_dc")]
    [JsonRequired]
    public int SaveDc { get; init; }

    [JsonPropertyName("save_ability")]
    [JsonRequired]
    public string SaveAbility { get; init; } = "";

    [JsonPropertyName("save_tag")]
    [JsonRequired]
    public string SaveTag { get; init; } = "";

    [JsonPropertyName("apply_on_save_failure")]
    [JsonRequired]
    public bool ApplyOnSaveFailure { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ModifyActionPointsActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("mode")]
    [JsonRequired]
    public string Mode { get; init; } = "";

    [JsonPropertyName("amount")]
    [JsonRequired]
    public int Amount { get; init; }

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("display_label")]
    [JsonRequired]
    public string DisplayLabel { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ModifyAbilityStateActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("binding_id")]
    [JsonRequired]
    public string BindingId { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("operation")]
    [JsonRequired]
    public string Operation { get; init; } = "";

    [JsonPropertyName("int_delta")]
    [JsonRequired]
    public int IntDelta { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MarkTargetActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("stack_delta")]
    [JsonRequired]
    public int StackDelta { get; init; }

    [JsonPropertyName("remove_on_source_missing")]
    [JsonRequired]
    public bool RemoveOnSourceMissing { get; init; }

    [JsonPropertyName("remove_on_target_defeated")]
    [JsonRequired]
    public bool RemoveOnTargetDefeated { get; init; }

    [JsonPropertyName("unique_per_source")]
    [JsonRequired]
    public bool UniquePerSource { get; init; }

    [JsonPropertyName("mirror_status_id")]
    [JsonRequired]
    public string MirrorStatusId { get; init; } = "";

    [JsonPropertyName("mirror_status_duration_tu")]
    [JsonRequired]
    public int MirrorStatusDurationTu { get; init; }

    [JsonPropertyName("mirror_status_stack_behavior")]
    [JsonRequired]
    public string MirrorStatusStackBehavior { get; init; } = "";

    [JsonPropertyName("mirror_status_stack_limit")]
    [JsonRequired]
    public int MirrorStatusStackLimit { get; init; }

    [JsonPropertyName("mirror_status_display_label")]
    [JsonRequired]
    public string MirrorStatusDisplayLabel { get; init; } = "";

    [JsonPropertyName("clear_status_ids_on_replace")]
    [JsonRequired]
    public IReadOnlyList<string> ClearStatusIdsOnReplace { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ClearStatusActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("mark_binding_id")]
    [JsonRequired]
    public string MarkBindingId { get; init; } = "";

    [JsonPropertyName("mark_state_key")]
    [JsonRequired]
    public string MarkStateKey { get; init; } = "";

    [JsonPropertyName("require_source_unit_match")]
    [JsonRequired]
    public bool RequireSourceUnitMatch { get; init; }

    [JsonPropertyName("clear_target_mark")]
    [JsonRequired]
    public bool ClearTargetMark { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class TriggerSkillActionPayloadJsonDto
{
    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = "";

    [JsonPropertyName("skill_level")]
    [JsonRequired]
    public int SkillLevel { get; init; }

    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("merge_into_parent_result")]
    [JsonRequired]
    public bool MergeIntoParentResult { get; init; }

    [JsonPropertyName("handle_target_defeat")]
    [JsonRequired]
    public bool HandleTargetDefeat { get; init; }

    [JsonPropertyName("activation_log")]
    [JsonRequired]
    public string ActivationLog { get; init; } = "";

    [JsonPropertyName("save_log_label")]
    [JsonRequired]
    public string SaveLogLabel { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SummonUnitsActionPayloadJsonDto
{
    [JsonPropertyName("anchor_selector")]
    [JsonRequired]
    public string AnchorSelector { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("count_dice")]
    [JsonRequired]
    public DiceExpressionJsonDto CountDice { get; init; } = null!;

    [JsonPropertyName("max_living_units")]
    [JsonRequired]
    public int MaxLivingUnits { get; init; }

    [JsonPropertyName("duration_tu")]
    [JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("spawn_radius")]
    [JsonRequired]
    public int SpawnRadius { get; init; }

    [JsonPropertyName("unit_id_prefix")]
    [JsonRequired]
    public string UnitIdPrefix { get; init; } = "";

    [JsonPropertyName("unit_display_name")]
    [JsonRequired]
    public string UnitDisplayName { get; init; } = "";

    [JsonPropertyName("body_size_category")]
    [JsonRequired]
    public string BodySizeCategory { get; init; } = "";

    [JsonPropertyName("control_mode")]
    [JsonRequired]
    public string ControlMode { get; init; } = "";

    [JsonPropertyName("ai_brain_id")]
    [JsonRequired]
    public string AiBrainId { get; init; } = "";

    [JsonPropertyName("ai_state_id")]
    [JsonRequired]
    public string AiStateId { get; init; } = "";

    [JsonPropertyName("cognition_kind")]
    [JsonRequired]
    public string CognitionKind { get; init; } = "";

    [JsonPropertyName("hp_max")]
    [JsonRequired]
    public int HpMax { get; init; }

    [JsonPropertyName("armor_class")]
    [JsonRequired]
    public int ArmorClass { get; init; }

    [JsonPropertyName("attack_bonus")]
    [JsonRequired]
    public int AttackBonus { get; init; }

    [JsonPropertyName("base_attack_bonus")]
    [JsonRequired]
    public int BaseAttackBonus { get; init; }

    [JsonPropertyName("action_points")]
    [JsonRequired]
    public int ActionPoints { get; init; }

    [JsonPropertyName("move_points")]
    [JsonRequired]
    public int MovePoints { get; init; }

    [JsonPropertyName("known_active_skill_ids")]
    [JsonRequired]
    public IReadOnlyList<string> KnownActiveSkillIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("natural_weapon_profile_type_id")]
    [JsonRequired]
    public string NaturalWeaponProfileTypeId { get; init; } = "";

    [JsonPropertyName("natural_weapon_damage_tag")]
    [JsonRequired]
    public string NaturalWeaponDamageTag { get; init; } = "";

    [JsonPropertyName("natural_weapon_attack_range")]
    [JsonRequired]
    public int NaturalWeaponAttackRange { get; init; }

    [JsonPropertyName("natural_weapon_damage_dice")]
    [JsonRequired]
    public DiceExpressionJsonDto NaturalWeaponDamageDice { get; init; } = null!;

    [JsonPropertyName("natural_weapon_family")]
    [JsonRequired]
    public string NaturalWeaponFamily { get; init; } = "";

    [JsonPropertyName("creature_type_tags")]
    [JsonRequired]
    public IReadOnlyList<string> CreatureTypeTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("movement_tags")]
    [JsonRequired]
    public IReadOnlyList<string> MovementTags { get; init; } = Array.Empty<string>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ConsumeSummonedUnitsActionPayloadJsonDto
{
    [JsonPropertyName("source_binding_id")]
    [JsonRequired]
    public string SourceBindingId { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("count")]
    [JsonRequired]
    public int Count { get; init; }

    [JsonPropertyName("selection_mode")]
    [JsonRequired]
    public string SelectionMode { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ConsumeStatusStacksActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("status_id")]
    [JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("count")]
    [JsonRequired]
    public int Count { get; init; }

    [JsonPropertyName("require_source_unit_match")]
    [JsonRequired]
    public bool RequireSourceUnitMatch { get; init; }

    [JsonPropertyName("selection_mode")]
    [JsonRequired]
    public string SelectionMode { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SummonedUnitAttackRollModifierActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("source_binding_id")]
    [JsonRequired]
    public string SourceBindingId { get; init; } = "";

    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("radius")]
    [JsonRequired]
    public int Radius { get; init; }

    [JsonPropertyName("bonus_per_unit")]
    [JsonRequired]
    public int BonusPerUnit { get; init; }

    [JsonPropertyName("max_absolute_bonus")]
    [JsonRequired]
    public int MaxAbsoluteBonus { get; init; }

    [JsonPropertyName("min_units")]
    [JsonRequired]
    public int MinUnits { get; init; }

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    public string StackMode { get; init; } = "";

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentTemporalProgressModifierJsonDto
{
    [JsonPropertyName("modifier_id")]
    [JsonRequired]
    public string ModifierId { get; init; } = "";

    [JsonPropertyName("applies_to_action_progress")]
    [JsonRequired]
    public bool AppliesToActionProgress { get; init; }

    [JsonPropertyName("applies_to_cast_progress")]
    [JsonRequired]
    public bool AppliesToCastProgress { get; init; }

    [JsonPropertyName("save_dc")]
    [JsonRequired]
    public int SaveDc { get; init; }

    [JsonPropertyName("attribute_modifier_id")]
    [JsonRequired]
    public string AttributeModifierId { get; init; } = "";

    [JsonPropertyName("success_rate_percent")]
    [JsonRequired]
    public int SuccessRatePercent { get; init; }

    [JsonPropertyName("failure_rate_percent")]
    [JsonRequired]
    public int FailureRatePercent { get; init; }

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentCognitionCeilingModifierJsonDto
{
    [JsonPropertyName("modifier_id")]
    [JsonRequired]
    public string ModifierId { get; init; } = "";

    [JsonPropertyName("cognition_ceiling")]
    [JsonRequired]
    public string CognitionCeiling { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentSlotWeightJsonDto
{
    [JsonPropertyName("slot_id")]
    [JsonRequired]
    public string SlotId { get; init; } = "";

    [JsonPropertyName("weight")]
    [JsonRequired]
    public int Weight { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentDurabilityDamageActionPayloadJsonDto
{
    [JsonPropertyName("target_selector")]
    [JsonRequired]
    public string TargetSelector { get; init; } = "";

    [JsonPropertyName("target_slots")]
    [JsonRequired]
    public IReadOnlyList<string> TargetSlots { get; init; } = Array.Empty<string>();

    [JsonPropertyName("slot_weights")]
    [JsonRequired]
    public IReadOnlyList<EquipmentSlotWeightJsonDto> SlotWeights { get; init; } = Array.Empty<EquipmentSlotWeightJsonDto>();

    [JsonPropertyName("required_item_tags")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredItemTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_equipment_type_ids")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredEquipmentTypeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("durability_loss")]
    [JsonRequired]
    public int DurabilityLoss { get; init; }

    [JsonPropertyName("save_tag")]
    [JsonRequired]
    public string SaveTag { get; init; } = "";

    [JsonPropertyName("save_dc")]
    [JsonRequired]
    public int SaveDc { get; init; }

    [JsonPropertyName("require_attack_success")]
    [JsonRequired]
    public bool RequireAttackSuccess { get; init; }

    [JsonPropertyName("max_damaged_items")]
    [JsonRequired]
    public int MaxDamagedItems { get; init; }

    [JsonPropertyName("max_target_rarity")]
    [JsonRequired]
    public int MaxTargetRarity { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAttackDefenseModifierJsonDto
{
    [JsonPropertyName("modifier_id")]
    [JsonRequired]
    public string ModifierId { get; init; } = "";

    [JsonPropertyName("ignored_ac_components")]
    [JsonRequired]
    public IReadOnlyList<string> IgnoredAcComponents { get; init; } = Array.Empty<string>();

    [JsonPropertyName("ac_component_multipliers")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAcComponentMultiplierJsonDto> AcComponentMultipliers { get; init; } = Array.Empty<EquipmentAcComponentMultiplierJsonDto>();

    [JsonPropertyName("lock_dodge_bonus")]
    [JsonRequired]
    public bool LockDodgeBonus { get; init; }

    [JsonPropertyName("required_target_equipment_selector")]
    [JsonRequired]
    public string RequiredTargetEquipmentSelector { get; init; } = "";

    [JsonPropertyName("required_target_item_tags")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredTargetItemTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_target_equipment_type_ids")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredTargetEquipmentTypeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("cover_policy")]
    [JsonRequired]
    public string CoverPolicy { get; init; } = "";

    [JsonPropertyName("projectile_obstacle_policy")]
    [JsonRequired]
    public string ProjectileObstaclePolicy { get; init; } = "";

    [JsonPropertyName("trace_label")]
    [JsonRequired]
    public string TraceLabel { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAcComponentMultiplierJsonDto
{
    [JsonPropertyName("ac_component_id")]
    [JsonRequired]
    public string AcComponentId { get; init; } = "";

    [JsonPropertyName("multiplier_percent")]
    [JsonRequired]
    public int MultiplierPercent { get; init; }

    [JsonPropertyName("stack_mode")]
    [JsonRequired]
    public string StackMode { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentWeaponProfileOverlayJsonDto
{
    [JsonPropertyName("overlay_id")]
    [JsonRequired]
    public string OverlayId { get; init; } = "";

    [JsonPropertyName("priority")]
    [JsonRequired]
    public int Priority { get; init; }

    [JsonPropertyName("condition_group")]
    [JsonRequired]
    public EquipmentAbilityConditionGroupJsonDto ConditionGroup { get; init; } = null!;

    [JsonPropertyName("require_equipped_weapon")]
    [JsonRequired]
    public bool RequireEquippedWeapon { get; init; }

    [JsonPropertyName("required_weapon_families")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredWeaponFamilies { get; init; } = Array.Empty<string>();

    [JsonPropertyName("required_weapon_type_ids")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredWeaponTypeIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("attack_range_delta")]
    [JsonRequired]
    public int AttackRangeDelta { get; init; }

    [JsonPropertyName("min_attack_range")]
    [JsonRequired]
    public int MinAttackRange { get; init; }

    [JsonPropertyName("max_attack_range")]
    [JsonRequired]
    public int MaxAttackRange { get; init; }

    [JsonPropertyName("one_handed_dice_overlay")]
    [JsonRequired]
    public EquipmentWeaponDiceOverlayJsonDto OneHandedDiceOverlay { get; init; } = null!;

    [JsonPropertyName("two_handed_dice_overlay")]
    [JsonRequired]
    public EquipmentWeaponDiceOverlayJsonDto TwoHandedDiceOverlay { get; init; } = null!;

    [JsonPropertyName("physical_damage_tag_override")]
    [JsonRequired]
    public string PhysicalDamageTagOverride { get; init; } = "";

    [JsonPropertyName("grip_override")]
    [JsonRequired]
    public string GripOverride { get; init; } = "";

    [JsonPropertyName("uses_two_hands_override")]
    [JsonRequired]
    public bool UsesTwoHandsOverride { get; init; }

    [JsonPropertyName("is_versatile_override")]
    [JsonRequired]
    public bool IsVersatileOverride { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentWeaponDiceOverlayJsonDto
{
    [JsonPropertyName("mode")]
    [JsonRequired]
    public string Mode { get; init; } = "";

    [JsonPropertyName("dice_count_delta")]
    [JsonRequired]
    public int DiceCountDelta { get; init; }

    [JsonPropertyName("dice_sides_override")]
    [JsonRequired]
    public int DiceSidesOverride { get; init; }

    [JsonPropertyName("flat_bonus_delta")]
    [JsonRequired]
    public int FlatBonusDelta { get; init; }

    [JsonPropertyName("dice_override")]
    [JsonRequired]
    public DiceExpressionJsonDto DiceOverride { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentRollGateJsonDto
{
    [JsonPropertyName("rng_stream")]
    [JsonRequired]
    public string RngStream { get; init; } = "";

    [JsonPropertyName("roll")]
    [JsonRequired]
    public DiceExpressionJsonDto Roll { get; init; } = null!;

    [JsonPropertyName("compare")]
    [JsonRequired]
    public string Compare { get; init; } = "";

    [JsonPropertyName("threshold")]
    [JsonRequired]
    public int Threshold { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentFatalInterceptJsonDto
{
    [JsonPropertyName("intercept_id")]
    [JsonRequired]
    public string InterceptId { get; init; } = "";

    [JsonPropertyName("resolution_order")]
    [JsonRequired]
    public int ResolutionOrder { get; init; }

    [JsonPropertyName("protection_priority")]
    [JsonRequired]
    public int ProtectionPriority { get; init; }

    [JsonPropertyName("usage_period_kind")]
    [JsonRequired]
    public string UsagePeriodKind { get; init; } = "";

    [JsonPropertyName("max_attempts_per_period")]
    [JsonRequired]
    public int MaxAttemptsPerPeriod { get; init; }

    [JsonPropertyName("consume_on_attempt")]
    [JsonRequired]
    public bool ConsumeOnAttempt { get; init; }

    [JsonPropertyName("roll_gate")]
    [JsonRequired]
    public EquipmentRollGateJsonDto RollGate { get; init; } = null!;

    [JsonPropertyName("recovery_kind")]
    [JsonRequired]
    public string RecoveryKind { get; init; } = "";

    [JsonPropertyName("recovery_dice")]
    [JsonRequired]
    public DiceExpressionJsonDto RecoveryDice { get; init; } = null!;

    [JsonPropertyName("recovery_percent_basis_points")]
    [JsonRequired]
    public int RecoveryPercentBasisPoints { get; init; }

    [JsonPropertyName("success_actions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityActionJsonDto> SuccessActions { get; init; } = Array.Empty<EquipmentAbilityActionJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentMitigationAuraJsonDto
{
    [JsonPropertyName("aura_id")]
    [JsonRequired]
    public string AuraId { get; init; } = "";

    [JsonPropertyName("radius")]
    [JsonRequired]
    public int Radius { get; init; }

    [JsonPropertyName("target_team_filter")]
    [JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("damage_tag")]
    [JsonRequired]
    public string DamageTag { get; init; } = "";

    [JsonPropertyName("mitigation_tier")]
    [JsonRequired]
    public string MitigationTier { get; init; } = "";

    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentMovementTrailJsonDto
{
    [JsonPropertyName("trail_id")]
    [JsonRequired]
    public string TrailId { get; init; } = "";

    [JsonPropertyName("replacement_group_id")]
    [JsonRequired]
    public string ReplacementGroupId { get; init; } = "";

    [JsonPropertyName("priority")]
    [JsonRequired]
    public int Priority { get; init; }

    [JsonPropertyName("required_skill_id")]
    [JsonRequired]
    public string RequiredSkillId { get; init; } = "";

    [JsonPropertyName("duration_tu")]
    [JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("target_team_filter")]
    [JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("damage_dice")]
    [JsonRequired]
    public DiceExpressionJsonDto DamageDice { get; init; } = null!;

    [JsonPropertyName("damage_tag")]
    [JsonRequired]
    public string DamageTag { get; init; } = "";

    [JsonPropertyName("damage_tags")]
    [JsonRequired]
    public IReadOnlyList<string> DamageTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentOutcomeTableJsonDto
{
    [JsonPropertyName("table_id")]
    [JsonRequired]
    public string TableId { get; init; } = "";

    [JsonPropertyName("roll")]
    [JsonRequired]
    public DiceExpressionJsonDto Roll { get; init; } = null!;

    [JsonPropertyName("entries")]
    [JsonRequired]
    public IReadOnlyList<EquipmentOutcomeEntryJsonDto> Entries { get; init; } = Array.Empty<EquipmentOutcomeEntryJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentOutcomeEntryJsonDto
{
    [JsonPropertyName("min_roll")]
    [JsonRequired]
    public int MinRoll { get; init; }

    [JsonPropertyName("max_roll")]
    [JsonRequired]
    public int MaxRoll { get; init; }

    [JsonPropertyName("actions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityActionJsonDto> Actions { get; init; } = Array.Empty<EquipmentAbilityActionJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityStateSchemaJsonDto
{
    [JsonPropertyName("state_key")]
    [JsonRequired]
    public string StateKey { get; init; } = "";

    [JsonPropertyName("owner_scope")]
    [JsonRequired]
    public string OwnerScope { get; init; } = "";

    [JsonPropertyName("value_kind")]
    [JsonRequired]
    public string ValueKind { get; init; } = "";

    [JsonPropertyName("initial_int_value")]
    [JsonRequired]
    public int InitialIntValue { get; init; }

    [JsonPropertyName("max_int_value")]
    [JsonRequired]
    public int MaxIntValue { get; init; }

    [JsonPropertyName("reset_timing")]
    [JsonRequired]
    public string ResetTiming { get; init; } = "";

    [JsonPropertyName("persist_outside_battle")]
    [JsonRequired]
    public bool PersistOutsideBattle { get; init; }

    [JsonPropertyName("visible_to_ui")]
    [JsonRequired]
    public bool VisibleToUi { get; init; }

    [JsonPropertyName("sync_source_state_key")]
    [JsonRequired]
    public string SyncSourceStateKey { get; init; } = "";

    [JsonPropertyName("sync_aggregation")]
    [JsonRequired]
    public string SyncAggregation { get; init; } = "";

    [JsonPropertyName("sync_int_literal")]
    [JsonRequired]
    public int SyncIntLiteral { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentGrantedActionJsonDto
{
    [JsonPropertyName("granted_action_id")]
    [JsonRequired]
    public string GrantedActionId { get; init; } = "";

    [JsonPropertyName("granted_kind")]
    [JsonRequired]
    public string GrantedKind { get; init; } = "";

    [JsonPropertyName("skill_id")]
    [JsonRequired]
    public string SkillId { get; init; } = "";

    [JsonPropertyName("skill_level")]
    [JsonRequired]
    public int SkillLevel { get; init; }

    [JsonPropertyName("usage_period_kind")]
    [JsonRequired]
    public string UsagePeriodKind { get; init; } = "";

    [JsonPropertyName("max_uses_per_period")]
    [JsonRequired]
    public int MaxUsesPerPeriod { get; init; }

    [JsonPropertyName("display_category")]
    [JsonRequired]
    public string DisplayCategory { get; init; } = "";

    [JsonPropertyName("display_priority")]
    [JsonRequired]
    public int DisplayPriority { get; init; }

    [JsonPropertyName("availability_conditions")]
    [JsonRequired]
    public EquipmentAbilityConditionGroupJsonDto AvailabilityConditions { get; init; } = null!;

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentWorldEffectJsonDto
{
    [JsonPropertyName("world_effect_id")]
    [JsonRequired]
    public string WorldEffectId { get; init; } = "";

    [JsonPropertyName("trigger")]
    [JsonRequired]
    public string Trigger { get; init; } = "";

    [JsonPropertyName("timing")]
    [JsonRequired]
    public string Timing { get; init; } = "";

    [JsonPropertyName("condition_group")]
    [JsonRequired]
    public EquipmentAbilityConditionGroupJsonDto ConditionGroup { get; init; } = null!;

    [JsonPropertyName("actions")]
    [JsonRequired]
    public IReadOnlyList<EquipmentAbilityActionJsonDto> Actions { get; init; } = Array.Empty<EquipmentAbilityActionJsonDto>();

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ScheduleAreaEffectActionPayloadJsonDto
{
    [JsonPropertyName("anchor_selector")]
    [JsonRequired]
    public string AnchorSelector { get; init; } = "";

    [JsonPropertyName("delay_tu")]
    [JsonRequired]
    public int DelayTu { get; init; }

    [JsonPropertyName("terrain_effect_id")]
    [JsonRequired]
    public string TerrainEffectId { get; init; } = "";

    [JsonPropertyName("area_pattern")]
    [JsonRequired]
    public string AreaPattern { get; init; } = "";

    [JsonPropertyName("area_value")]
    [JsonRequired]
    public int AreaValue { get; init; }

    [JsonPropertyName("lifetime_policy")]
    [JsonRequired]
    public string LifetimePolicy { get; init; } = "";

    [JsonPropertyName("effect_type")]
    [JsonRequired]
    public string EffectType { get; init; } = "";

    [JsonPropertyName("target_team_filter")]
    [JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("stack_behavior")]
    [JsonRequired]
    public string StackBehavior { get; init; } = "";

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("render_overlay_id")]
    [JsonRequired]
    public string RenderOverlayId { get; init; } = "";

    [JsonPropertyName("overlay_priority")]
    [JsonRequired]
    public int OverlayPriority { get; init; }

    [JsonPropertyName("contact_status_id")]
    [JsonRequired]
    public string ContactStatusId { get; init; } = "";

    [JsonPropertyName("contact_status_duration_tu")]
    [JsonRequired]
    public int ContactStatusDurationTu { get; init; }

    [JsonPropertyName("contact_stack_behavior")]
    [JsonRequired]
    public string ContactStackBehavior { get; init; } = "";

    [JsonPropertyName("contact_stack_limit")]
    [JsonRequired]
    public int ContactStackLimit { get; init; }

    [JsonPropertyName("contact_status_display_label")]
    [JsonRequired]
    public string ContactStatusDisplayLabel { get; init; } = "";

    [JsonPropertyName("contact_counts_as_debuff_override")]
    [JsonRequired]
    public bool ContactCountsAsDebuffOverride { get; init; }

    [JsonPropertyName("contact_counts_as_debuff")]
    [JsonRequired]
    public bool ContactCountsAsDebuff { get; init; }

    [JsonPropertyName("contact_undispellable")]
    [JsonRequired]
    public bool ContactUndispellable { get; init; }

    [JsonPropertyName("contact_dispellable_magic")]
    [JsonRequired]
    public bool ContactDispellableMagic { get; init; }

    [JsonPropertyName("contact_dispellable_harmful_magic")]
    [JsonRequired]
    public bool ContactDispellableHarmfulMagic { get; init; }

    [JsonPropertyName("contact_dispellable_beneficial_magic")]
    [JsonRequired]
    public bool ContactDispellableBeneficialMagic { get; init; }

    [JsonPropertyName("contact_save_dc")]
    [JsonRequired]
    public int ContactSaveDc { get; init; }

    [JsonPropertyName("contact_save_ability")]
    [JsonRequired]
    public string ContactSaveAbility { get; init; } = "";

    [JsonPropertyName("contact_save_tag")]
    [JsonRequired]
    public string ContactSaveTag { get; init; } = "";

    [JsonPropertyName("contact_apply_on_save_failure")]
    [JsonRequired]
    public bool ContactApplyOnSaveFailure { get; init; }

    [JsonPropertyName("contact_tick_interval_tu")]
    [JsonRequired]
    public int ContactTickIntervalTu { get; init; }

    [JsonPropertyName("contact_timeline_damage_dice_count")]
    [JsonRequired]
    public int ContactTimelineDamageDiceCount { get; init; }

    [JsonPropertyName("contact_timeline_damage_dice_sides")]
    [JsonRequired]
    public int ContactTimelineDamageDiceSides { get; init; }

    [JsonPropertyName("contact_timeline_damage_flat_bonus")]
    [JsonRequired]
    public int ContactTimelineDamageFlatBonus { get; init; }

    [JsonPropertyName("contact_blocked_by_trait_id")]
    [JsonRequired]
    public string ContactBlockedByTraitId { get; init; } = "";

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto
{
    [JsonPropertyName("anchor_selector")]
    [JsonRequired]
    public string AnchorSelector { get; init; } = "";

    [JsonPropertyName("terrain_effect_id")]
    [JsonRequired]
    public string TerrainEffectId { get; init; } = "";

    [JsonPropertyName("move_cost_delta")]
    [JsonRequired]
    public int MoveCostDelta { get; init; }

    [JsonPropertyName("target_team_filter")]
    [JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("stack_behavior")]
    [JsonRequired]
    public string StackBehavior { get; init; } = "";

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("render_overlay_id")]
    [JsonRequired]
    public string RenderOverlayId { get; init; } = "";

    [JsonPropertyName("overlay_priority")]
    [JsonRequired]
    public int OverlayPriority { get; init; }

    [JsonPropertyName("check_attribute_modifier_id")]
    [JsonRequired]
    public string CheckAttributeModifierId { get; init; } = "";

    [JsonPropertyName("check_compare")]
    [JsonRequired]
    public string CheckCompare { get; init; } = "";

    [JsonPropertyName("check_threshold")]
    [JsonRequired]
    public int CheckThreshold { get; init; }

    [JsonPropertyName("natural_twenty_auto_success")]
    [JsonRequired]
    public bool NaturalTwentyAutoSuccess { get; init; }

    [JsonPropertyName("natural_one_auto_failure")]
    [JsonRequired]
    public bool NaturalOneAutoFailure { get; init; }

}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ApplyEdgeFeatureActionPayloadJsonDto
{
    [JsonPropertyName("from_selector")]
    [JsonRequired]
    public string FromSelector { get; init; } = "";

    [JsonPropertyName("to_selector")]
    [JsonRequired]
    public string ToSelector { get; init; } = "";

    [JsonPropertyName("duration_tu")]
    [JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("max_active_edges")]
    [JsonRequired]
    public int MaxActiveEdges { get; init; }

    [JsonPropertyName("refresh_existing")]
    [JsonRequired]
    public bool RefreshExisting { get; init; }

    [JsonPropertyName("require_adjacent")]
    [JsonRequired]
    public bool RequireAdjacent { get; init; }

    [JsonPropertyName("feature_kind")]
    [JsonRequired]
    public string FeatureKind { get; init; } = "";

    [JsonPropertyName("render_kind")]
    [JsonRequired]
    public string RenderKind { get; init; } = "";

    [JsonPropertyName("render_layers")]
    [JsonRequired]
    public int RenderLayers { get; init; }

    [JsonPropertyName("blocks_move")]
    [JsonRequired]
    public bool BlocksMove { get; init; }

    [JsonPropertyName("blocks_occupancy")]
    [JsonRequired]
    public bool BlocksOccupancy { get; init; }

    [JsonPropertyName("blocks_los")]
    [JsonRequired]
    public bool BlocksLos { get; init; }

    [JsonPropertyName("interaction_kind")]
    [JsonRequired]
    public string InteractionKind { get; init; } = "";

    [JsonPropertyName("state_tag")]
    [JsonRequired]
    public string StateTag { get; init; } = "";

}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    WriteIndented = true
)]
[JsonSerializable(typeof(EquipmentAbilityContentPackJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityBindingJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityReactionJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityConditionGroupJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityConditionJsonDto))]
[JsonSerializable(typeof(HasStatusConditionPayloadJsonDto))]
[JsonSerializable(typeof(CompareFactConditionPayloadJsonDto))]
[JsonSerializable(typeof(HasEquipmentTagConditionPayloadJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityFactQueryJsonDto))]
[JsonSerializable(typeof(DiceExpressionJsonDto))]
[JsonSerializable(typeof(DiceExpressionTermJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityActionJsonDto))]
[JsonSerializable(typeof(AddDamageDiceActionPayloadJsonDto))]
[JsonSerializable(typeof(ImmediateWeaponAttackActionPayloadJsonDto))]
[JsonSerializable(typeof(DealDamageActionPayloadJsonDto))]
[JsonSerializable(typeof(HealActionPayloadJsonDto))]
[JsonSerializable(typeof(HealFromFactActionPayloadJsonDto))]
[JsonSerializable(typeof(AttackRollBonusActionPayloadJsonDto))]
[JsonSerializable(typeof(AttackRollAdvantageActionPayloadJsonDto))]
[JsonSerializable(typeof(CriticalHitOverrideActionPayloadJsonDto))]
[JsonSerializable(typeof(DamageRollModeOverrideActionPayloadJsonDto))]
[JsonSerializable(typeof(DamageReductionActionPayloadJsonDto))]
[JsonSerializable(typeof(LootQuantityMultiplierActionPayloadJsonDto))]
[JsonSerializable(typeof(ApplyStatusActionPayloadJsonDto))]
[JsonSerializable(typeof(ModifyActionPointsActionPayloadJsonDto))]
[JsonSerializable(typeof(ModifyAbilityStateActionPayloadJsonDto))]
[JsonSerializable(typeof(MarkTargetActionPayloadJsonDto))]
[JsonSerializable(typeof(ClearStatusActionPayloadJsonDto))]
[JsonSerializable(typeof(TriggerSkillActionPayloadJsonDto))]
[JsonSerializable(typeof(SummonUnitsActionPayloadJsonDto))]
[JsonSerializable(typeof(ConsumeSummonedUnitsActionPayloadJsonDto))]
[JsonSerializable(typeof(ConsumeStatusStacksActionPayloadJsonDto))]
[JsonSerializable(typeof(SummonedUnitAttackRollModifierActionPayloadJsonDto))]
[JsonSerializable(typeof(EquipmentTemporalProgressModifierJsonDto))]
[JsonSerializable(typeof(EquipmentCognitionCeilingModifierJsonDto))]
[JsonSerializable(typeof(EquipmentSlotWeightJsonDto))]
[JsonSerializable(typeof(EquipmentDurabilityDamageActionPayloadJsonDto))]
[JsonSerializable(typeof(EquipmentAttackDefenseModifierJsonDto))]
[JsonSerializable(typeof(EquipmentAcComponentMultiplierJsonDto))]
[JsonSerializable(typeof(EquipmentWeaponProfileOverlayJsonDto))]
[JsonSerializable(typeof(EquipmentWeaponDiceOverlayJsonDto))]
[JsonSerializable(typeof(EquipmentRollGateJsonDto))]
[JsonSerializable(typeof(EquipmentFatalInterceptJsonDto))]
[JsonSerializable(typeof(EquipmentMitigationAuraJsonDto))]
[JsonSerializable(typeof(EquipmentMovementTrailJsonDto))]
[JsonSerializable(typeof(EquipmentOutcomeTableJsonDto))]
[JsonSerializable(typeof(EquipmentOutcomeEntryJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityStateSchemaJsonDto))]
[JsonSerializable(typeof(EquipmentGrantedActionJsonDto))]
[JsonSerializable(typeof(EquipmentWorldEffectJsonDto))]
[JsonSerializable(typeof(ScheduleAreaEffectActionPayloadJsonDto))]
[JsonSerializable(typeof(ApplyBattleTerrainEffectAfterCheckActionPayloadJsonDto))]
[JsonSerializable(typeof(ApplyEdgeFeatureActionPayloadJsonDto))]
[JsonSerializable(typeof(EquipmentAbilityJsonDocumentDto))]
[JsonSerializable(typeof(System.Text.Json.JsonElement))]
internal partial class EquipmentAbilityJsonSerializerContext : JsonSerializerContext { }
