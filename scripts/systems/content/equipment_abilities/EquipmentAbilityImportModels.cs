#nullable enable

using System;
using System.Collections.Generic;

internal interface IEquipmentAbilityPayloadImportModel { }
internal interface IEquipmentAbilityConditionPayloadImportModel : IEquipmentAbilityPayloadImportModel { }
internal interface IEquipmentAbilityActionPayloadImportModel : IEquipmentAbilityPayloadImportModel { }

internal sealed class EquipmentAbilityContentPackImportModel
{
    internal string pack_id { get; init; } = "";
    internal int schema_version { get; init; }
    internal int load_order { get; init; }
    internal IReadOnlyList<string> dependencies { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<EquipmentAbilityBindingImportModel> bindings { get; init; } = Array.Empty<EquipmentAbilityBindingImportModel>();
}

internal sealed class EquipmentAbilityBindingImportModel
{
    internal string binding_id { get; init; } = "";
    internal string trait_id { get; init; } = "";
    internal string override_mode { get; init; } = "";
    internal string replaces_binding_id { get; init; } = "";
    internal IReadOnlyList<string> allowed_source_kinds { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_trait_categories { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_effective_trait_ids { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_item_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> supported_equipment_type_ids { get; init; } = Array.Empty<string>();
    internal string activation_status_id { get; init; } = "";
    internal IReadOnlyList<EquipmentAbilityStateSchemaImportModel> state_schemas { get; init; } = Array.Empty<EquipmentAbilityStateSchemaImportModel>();
    internal IReadOnlyList<EquipmentAbilityReactionImportModel> reactions { get; init; } = Array.Empty<EquipmentAbilityReactionImportModel>();
    internal IReadOnlyList<EquipmentFatalInterceptImportModel> fatal_intercepts { get; init; } = Array.Empty<EquipmentFatalInterceptImportModel>();
    internal IReadOnlyList<EquipmentMitigationAuraImportModel> mitigation_auras { get; init; } = Array.Empty<EquipmentMitigationAuraImportModel>();
    internal IReadOnlyList<EquipmentMovementTrailImportModel> movement_trails { get; init; } = Array.Empty<EquipmentMovementTrailImportModel>();
    internal IReadOnlyList<EquipmentGrantedActionImportModel> granted_actions { get; init; } = Array.Empty<EquipmentGrantedActionImportModel>();
    internal IReadOnlyList<EquipmentTemporalProgressModifierImportModel> temporal_progress_modifiers { get; init; } = Array.Empty<EquipmentTemporalProgressModifierImportModel>();
    internal IReadOnlyList<EquipmentCognitionCeilingModifierImportModel> cognition_ceiling_modifiers { get; init; } = Array.Empty<EquipmentCognitionCeilingModifierImportModel>();
    internal IReadOnlyList<EquipmentWeaponProfileOverlayImportModel> weapon_profile_overlays { get; init; } = Array.Empty<EquipmentWeaponProfileOverlayImportModel>();
    internal IReadOnlyList<EquipmentWorldEffectImportModel> world_effects { get; init; } = Array.Empty<EquipmentWorldEffectImportModel>();
}

internal sealed class EquipmentAbilityReactionImportModel
{
    internal string reaction_id { get; init; } = "";
    internal string trigger { get; init; } = "";
    internal string timing { get; init; } = "";
    internal int priority { get; init; }
    internal string once_scope { get; init; } = "";
    internal bool requires_player_confirmation { get; init; }
    internal EquipmentAbilityConditionGroupImportModel condition_group { get; init; } = null!;
    internal EquipmentRollGateImportModel roll_gate { get; init; } = null!;
    internal EquipmentOutcomeTableImportModel outcome_table { get; init; } = null!;
    internal IReadOnlyList<string> projected_effect_categories { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<EquipmentAbilityActionImportModel> actions { get; init; } = Array.Empty<EquipmentAbilityActionImportModel>();
}

internal sealed class EquipmentAbilityConditionGroupImportModel
{
    internal string mode { get; init; } = "";
    internal bool negate { get; init; }
    internal IReadOnlyList<EquipmentAbilityConditionImportModel> conditions { get; init; } = Array.Empty<EquipmentAbilityConditionImportModel>();
    internal IReadOnlyList<EquipmentAbilityConditionGroupImportModel> groups { get; init; } = Array.Empty<EquipmentAbilityConditionGroupImportModel>();
}

internal sealed class EquipmentAbilityConditionImportModel
{
    internal string condition_id { get; init; } = "";
    internal string kind { get; init; } = "";
    internal IEquipmentAbilityConditionPayloadImportModel payload { get; init; } = null!;
}

internal sealed class HasStatusConditionPayloadImportModel : IEquipmentAbilityConditionPayloadImportModel
{
    internal string subject { get; init; } = "";
    internal string status_id { get; init; } = "";
}

internal sealed class CompareFactConditionPayloadImportModel : IEquipmentAbilityConditionPayloadImportModel
{
    internal EquipmentAbilityFactQueryImportModel left { get; init; } = null!;
    internal string compare { get; init; } = "";
    internal EquipmentAbilityFactQueryImportModel right { get; init; } = null!;
}

internal sealed class HasEquipmentTagConditionPayloadImportModel : IEquipmentAbilityConditionPayloadImportModel
{
    internal string subject { get; init; } = "";
    internal string equipment_selector { get; init; } = "";
    internal IReadOnlyList<string> all_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> any_tags { get; init; } = Array.Empty<string>();
}

internal sealed class EquipmentAbilityFactQueryImportModel
{
    internal string query_kind { get; init; } = "";
    internal string fact_id { get; init; } = "";
    internal string subject { get; init; } = "";
    internal string binding_id { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal string status_id { get; init; } = "";
    internal bool require_source_unit_match { get; init; }
    internal string attribute_id { get; init; } = "";
    internal string aggregation { get; init; } = "";
    internal string value_kind { get; init; } = "";
    internal bool bool_literal { get; init; }
    internal int int_literal { get; init; }
    internal float float_literal { get; init; }
    internal string string_name_literal { get; init; } = "";
}

internal sealed class DiceExpressionImportModel
{
    internal IReadOnlyList<DiceExpressionTermImportModel> terms { get; init; } = Array.Empty<DiceExpressionTermImportModel>();
    internal int flat_bonus { get; init; }
    internal string preview_policy { get; init; } = "";
}

internal sealed class DiceExpressionTermImportModel
{
    internal int dice_count { get; init; }
    internal int dice_sides { get; init; }
    internal EquipmentAbilityFactQueryImportModel count_bonus_fact { get; init; } = null!;
    internal float count_bonus_multiplier { get; init; }
    internal int max_dice_count { get; init; }
}

internal sealed class EquipmentAbilityActionImportModel
{
    internal string action_id { get; init; } = "";
    internal string kind { get; init; } = "";
    internal IEquipmentAbilityActionPayloadImportModel payload { get; init; } = null!;
    internal EquipmentAbilityConditionGroupImportModel condition_group { get; init; } = null!;
    internal EquipmentRollGateImportModel roll_gate { get; init; } = null!;
}

internal sealed class AddDamageDiceActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal DiceExpressionImportModel dice { get; init; } = null!;
    internal string damage_type { get; init; } = "";
    internal string damage_type_mode { get; init; } = "explicit";
    internal bool require_weapon_damage { get; init; }
    internal bool subtract { get; init; }
    internal string replacement_group_id { get; init; } = "";
    internal int replacement_priority { get; init; }
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> mitigation_bypass_damage_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> mitigation_bypass_tiers { get; init; } = Array.Empty<string>();
}

internal sealed class ImmediateWeaponAttackActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string anchor_selector { get; init; } = "";
    internal string target_team_filter { get; init; } = "";
    internal int radius { get; init; }
    internal int max_attacks { get; init; }
    internal string skill_id { get; init; } = "";
    internal bool require_weapon_range { get; init; }
}

internal sealed class DealDamageActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal DiceExpressionImportModel dice { get; init; } = null!;
    internal string damage_type { get; init; } = "";
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> mitigation_bypass_damage_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> mitigation_bypass_tiers { get; init; } = Array.Empty<string>();
}

internal sealed class HealActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal DiceExpressionImportModel dice { get; init; } = null!;
}

internal sealed class HealFromFactActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal EquipmentAbilityFactQueryImportModel amount_fact { get; init; } = null!;
    internal int multiplier_percent { get; init; }
    internal int max_amount { get; init; }
}

internal sealed class AttackRollBonusActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal int bonus { get; init; }
    internal string attribute_modifier_id { get; init; } = "";
    internal string stack_mode { get; init; } = "";
    internal string label { get; init; } = "";
    internal bool require_weapon_damage { get; init; }
}

internal sealed class AttackRollAdvantageActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string mode { get; init; } = "";
    internal string stack_mode { get; init; } = "";
    internal string label { get; init; } = "";
}

internal sealed class CriticalHitOverrideActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal bool require_weapon_damage { get; init; }
    internal string label { get; init; } = "";
}

internal sealed class DamageRollModeOverrideActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string roll_mode { get; init; } = "";
    internal string stack_mode { get; init; } = "";
    internal string label { get; init; } = "";
}

internal sealed class DamageReductionActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal int amount { get; init; }
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal string label { get; init; } = "";
}

internal sealed class GrantMitigationTierActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string mitigation_tier { get; init; } = "";
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal string label { get; init; } = "";
}

internal sealed class LootQuantityMultiplierActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal int multiplier_percent { get; init; }
    internal IReadOnlyList<string> affected_drop_kinds { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> any_item_tags { get; init; } = Array.Empty<string>();
}

internal sealed class ApplyStatusActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string status_id { get; init; } = "";
    internal int duration_turns { get; init; }
    internal int duration_tu { get; init; }
    internal int stack_delta { get; init; }
    internal string stack_behavior { get; init; } = "";
    internal int stack_limit { get; init; }
    internal string display_label { get; init; } = "";
    internal int attack_roll_penalty { get; init; }
    internal int armor_class_bonus_per_stack { get; init; }
    internal int source_bound_attack_roll_penalty { get; init; }
    internal int source_bound_attack_roll_penalty_min_stacks { get; init; } = 1;
    internal int source_bound_incoming_attack_roll_bonus_per_stack { get; init; }
    internal int source_bound_incoming_attack_roll_bonus_min_stacks { get; init; } = 1;
    internal bool override_heal_multiplier_percent { get; init; }
    internal int heal_multiplier_percent { get; init; }
    internal int move_point_capacity_delta { get; init; }
    internal bool forced_move_immune { get; init; }
    internal string damage_tag { get; init; } = "";
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal string mitigation_tier { get; init; } = "";
    internal bool counts_as_debuff_override { get; init; }
    internal bool counts_as_debuff { get; init; }
    internal bool undispellable { get; init; }
    internal bool dispellable_magic { get; init; }
    internal bool dispellable_harmful_magic { get; init; }
    internal bool dispellable_beneficial_magic { get; init; }
    internal bool lock_counterattack { get; init; }
    internal bool lock_guard { get; init; }
    internal bool lock_dodge_bonus { get; init; }
    internal int tick_interval_tu { get; init; }
    internal int timeline_damage_dice_count { get; init; }
    internal int timeline_damage_dice_sides { get; init; }
    internal int timeline_damage_flat_bonus { get; init; }
    internal int save_dc { get; init; }
    internal string save_ability { get; init; } = "";
    internal string save_tag { get; init; } = "";
    internal bool apply_on_save_failure { get; init; }
    internal bool remove_on_source_deactivated { get; init; }
}

internal sealed class ModifyActionPointsActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string mode { get; init; } = "";
    internal int amount { get; init; }
    internal string status_id { get; init; } = "";
    internal string display_label { get; init; } = "";
}

internal sealed class ModifyAbilityStateActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string binding_id { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal string operation { get; init; } = "";
    internal int int_delta { get; init; }
}

internal sealed class MarkTargetActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal int stack_delta { get; init; }
    internal bool remove_on_source_missing { get; init; }
    internal bool remove_on_target_defeated { get; init; }
    internal bool unique_per_source { get; init; }
    internal string mirror_status_id { get; init; } = "";
    internal int mirror_status_duration_tu { get; init; }
    internal string mirror_status_stack_behavior { get; init; } = "";
    internal int mirror_status_stack_limit { get; init; }
    internal string mirror_status_display_label { get; init; } = "";
    internal IReadOnlyList<string> clear_status_ids_on_replace { get; init; } = Array.Empty<string>();
}

internal sealed class ClearStatusActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string status_id { get; init; } = "";
    internal string mark_binding_id { get; init; } = "";
    internal string mark_state_key { get; init; } = "";
    internal bool require_source_unit_match { get; init; }
    internal bool clear_target_mark { get; init; }
}

internal sealed class TriggerSkillActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string skill_id { get; init; } = "";
    internal int skill_level { get; init; }
    internal string target_selector { get; init; } = "";
    internal bool merge_into_parent_result { get; init; }
    internal bool handle_target_defeat { get; init; }
    internal string activation_log { get; init; } = "";
    internal string save_log_label { get; init; } = "";
}

internal sealed class SummonUnitsActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string anchor_selector { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal DiceExpressionImportModel count_dice { get; init; } = null!;
    internal int max_living_units { get; init; }
    internal int duration_tu { get; init; }
    internal int spawn_radius { get; init; }
    internal string unit_id_prefix { get; init; } = "";
    internal string unit_display_name { get; init; } = "";
    internal string body_size_category { get; init; } = "";
    internal string control_mode { get; init; } = "";
    internal string ai_brain_id { get; init; } = "";
    internal string ai_state_id { get; init; } = "";
    internal string cognition_kind { get; init; } = "";
    internal int hp_max { get; init; }
    internal int armor_class { get; init; }
    internal int attack_bonus { get; init; }
    internal int base_attack_bonus { get; init; }
    internal int action_points { get; init; }
    internal int move_points { get; init; }
    internal IReadOnlyList<string> known_active_skill_ids { get; init; } = Array.Empty<string>();
    internal string natural_weapon_profile_type_id { get; init; } = "";
    internal string natural_weapon_damage_tag { get; init; } = "";
    internal int natural_weapon_attack_range { get; init; }
    internal DiceExpressionImportModel natural_weapon_damage_dice { get; init; } = null!;
    internal string natural_weapon_family { get; init; } = "";
    internal IReadOnlyList<string> creature_type_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> movement_tags { get; init; } = Array.Empty<string>();
}

internal sealed class ConsumeSummonedUnitsActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string source_binding_id { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal int count { get; init; }
    internal string selection_mode { get; init; } = "";
}

internal sealed class ConsumeStatusStacksActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string status_id { get; init; } = "";
    internal int count { get; init; }
    internal bool require_source_unit_match { get; init; }
    internal string selection_mode { get; init; } = "";
}

internal sealed class SummonedUnitAttackRollModifierActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal string source_binding_id { get; init; } = "";
    internal string state_key { get; init; } = "";
    internal int radius { get; init; }
    internal int bonus_per_unit { get; init; }
    internal int max_absolute_bonus { get; init; }
    internal int min_units { get; init; }
    internal string stack_mode { get; init; } = "";
    internal string label { get; init; } = "";
}

internal sealed class EquipmentTemporalProgressModifierImportModel
{
    internal string modifier_id { get; init; } = "";
    internal bool applies_to_action_progress { get; init; }
    internal bool applies_to_cast_progress { get; init; }
    internal int save_dc { get; init; }
    internal string attribute_modifier_id { get; init; } = "";
    internal int success_rate_percent { get; init; }
    internal int failure_rate_percent { get; init; }
    internal string label { get; init; } = "";
}

internal sealed class EquipmentCognitionCeilingModifierImportModel
{
    internal string modifier_id { get; init; } = "";
    internal string cognition_ceiling { get; init; } = "";
}

internal sealed class EquipmentSlotWeightImportModel
{
    internal string slot_id { get; init; } = "";
    internal int weight { get; init; }
}

internal sealed class EquipmentDurabilityDamageActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string target_selector { get; init; } = "";
    internal IReadOnlyList<string> target_slots { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<EquipmentSlotWeightImportModel> slot_weights { get; init; } = Array.Empty<EquipmentSlotWeightImportModel>();
    internal IReadOnlyList<string> required_item_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_equipment_type_ids { get; init; } = Array.Empty<string>();
    internal int durability_loss { get; init; }
    internal string save_tag { get; init; } = "";
    internal int save_dc { get; init; }
    internal bool require_attack_success { get; init; }
    internal int max_damaged_items { get; init; }
    internal int max_target_rarity { get; init; }
}

internal sealed class EquipmentAttackDefenseModifierImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string modifier_id { get; init; } = "";
    internal IReadOnlyList<string> ignored_ac_components { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<EquipmentAcComponentMultiplierImportModel> ac_component_multipliers { get; init; } = Array.Empty<EquipmentAcComponentMultiplierImportModel>();
    internal bool lock_dodge_bonus { get; init; }
    internal string required_target_equipment_selector { get; init; } = "";
    internal IReadOnlyList<string> required_target_item_tags { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_target_equipment_type_ids { get; init; } = Array.Empty<string>();
    internal string cover_policy { get; init; } = "";
    internal string projectile_obstacle_policy { get; init; } = "";
    internal string trace_label { get; init; } = "";
}

internal sealed class EquipmentAcComponentMultiplierImportModel
{
    internal string ac_component_id { get; init; } = "";
    internal int multiplier_percent { get; init; }
    internal string stack_mode { get; init; } = "";
}

internal sealed class EquipmentWeaponProfileOverlayImportModel
{
    internal string overlay_id { get; init; } = "";
    internal int priority { get; init; }
    internal EquipmentAbilityConditionGroupImportModel condition_group { get; init; } = null!;
    internal bool require_equipped_weapon { get; init; }
    internal IReadOnlyList<string> required_weapon_families { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> required_weapon_type_ids { get; init; } = Array.Empty<string>();
    internal int attack_range_delta { get; init; }
    internal int min_attack_range { get; init; }
    internal int max_attack_range { get; init; }
    internal EquipmentWeaponDiceOverlayImportModel one_handed_dice_overlay { get; init; } = null!;
    internal EquipmentWeaponDiceOverlayImportModel two_handed_dice_overlay { get; init; } = null!;
    internal string physical_damage_tag_override { get; init; } = "";
    internal string grip_override { get; init; } = "";
    internal bool uses_two_hands_override { get; init; }
    internal bool is_versatile_override { get; init; }
}

internal sealed class EquipmentWeaponDiceOverlayImportModel
{
    internal string mode { get; init; } = "";
    internal int dice_count_delta { get; init; }
    internal int dice_sides_override { get; init; }
    internal int flat_bonus_delta { get; init; }
    internal DiceExpressionImportModel dice_override { get; init; } = null!;
}

internal sealed class EquipmentRollGateImportModel
{
    internal string rng_stream { get; init; } = "";
    internal DiceExpressionImportModel roll { get; init; } = null!;
    internal string compare { get; init; } = "";
    internal int threshold { get; init; }
}

internal sealed class EquipmentFatalInterceptImportModel
{
    internal string intercept_id { get; init; } = "";
    internal int resolution_order { get; init; }
    internal int protection_priority { get; init; }
    internal string usage_period_kind { get; init; } = "";
    internal int max_attempts_per_period { get; init; }
    internal bool consume_on_attempt { get; init; }
    internal EquipmentRollGateImportModel roll_gate { get; init; } = null!;
    internal string recovery_kind { get; init; } = "";
    internal DiceExpressionImportModel recovery_dice { get; init; } = null!;
    internal int recovery_percent_basis_points { get; init; }
    internal IReadOnlyList<EquipmentAbilityActionImportModel> success_actions { get; init; } = Array.Empty<EquipmentAbilityActionImportModel>();
}

internal sealed class EquipmentMitigationAuraImportModel
{
    internal string aura_id { get; init; } = "";
    internal int radius { get; init; }
    internal string target_team_filter { get; init; } = "";
    internal string damage_tag { get; init; } = "";
    internal string mitigation_tier { get; init; } = "";
    internal string label { get; init; } = "";
}

internal sealed class EquipmentMovementTrailImportModel
{
    internal string trail_id { get; init; } = "";
    internal string replacement_group_id { get; init; } = "";
    internal int priority { get; init; }
    internal string required_skill_id { get; init; } = "";
    internal int duration_tu { get; init; }
    internal string target_team_filter { get; init; } = "";
    internal DiceExpressionImportModel damage_dice { get; init; } = null!;
    internal string damage_tag { get; init; } = "";
    internal IReadOnlyList<string> damage_tags { get; init; } = Array.Empty<string>();
    internal string display_name { get; init; } = "";
}

internal sealed class EquipmentOutcomeTableImportModel
{
    internal string table_id { get; init; } = "";
    internal DiceExpressionImportModel roll { get; init; } = null!;
    internal IReadOnlyList<EquipmentOutcomeEntryImportModel> entries { get; init; } = Array.Empty<EquipmentOutcomeEntryImportModel>();
}

internal sealed class EquipmentOutcomeEntryImportModel
{
    internal int min_roll { get; init; }
    internal int max_roll { get; init; }
    internal IReadOnlyList<EquipmentAbilityActionImportModel> actions { get; init; } = Array.Empty<EquipmentAbilityActionImportModel>();
}

internal sealed class EquipmentAbilityStateSchemaImportModel
{
    internal string state_key { get; init; } = "";
    internal string owner_scope { get; init; } = "";
    internal string value_kind { get; init; } = "";
    internal int initial_int_value { get; init; }
    internal int max_int_value { get; init; }
    internal string reset_timing { get; init; } = "";
    internal bool persist_outside_battle { get; init; }
    internal bool visible_to_ui { get; init; }
    internal string sync_source_state_key { get; init; } = "";
    internal string sync_aggregation { get; init; } = "";
    internal int sync_int_literal { get; init; }
}

internal sealed class EquipmentGrantedActionImportModel
{
    internal string granted_action_id { get; init; } = "";
    internal string granted_kind { get; init; } = "";
    internal string skill_id { get; init; } = "";
    internal int skill_level { get; init; }
    internal string usage_period_kind { get; init; } = "";
    internal int max_uses_per_period { get; init; }
    internal string display_category { get; init; } = "";
    internal int display_priority { get; init; }
    internal EquipmentAbilityConditionGroupImportModel availability_conditions { get; init; } = null!;
}

internal sealed class EquipmentWorldEffectImportModel
{
    internal string world_effect_id { get; init; } = "";
    internal string trigger { get; init; } = "";
    internal string timing { get; init; } = "";
    internal EquipmentAbilityConditionGroupImportModel condition_group { get; init; } = null!;
    internal IReadOnlyList<EquipmentAbilityActionImportModel> actions { get; init; } = Array.Empty<EquipmentAbilityActionImportModel>();
}

internal sealed class ScheduleAreaEffectActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string anchor_selector { get; init; } = "";
    internal int delay_tu { get; init; }
    internal string terrain_effect_id { get; init; } = "";
    internal string area_pattern { get; init; } = "";
    internal int area_value { get; init; }
    internal string lifetime_policy { get; init; } = "";
    internal string effect_type { get; init; } = "";
    internal string target_team_filter { get; init; } = "";
    internal string stack_behavior { get; init; } = "";
    internal string display_name { get; init; } = "";
    internal string render_overlay_id { get; init; } = "";
    internal int overlay_priority { get; init; }
    internal string contact_status_id { get; init; } = "";
    internal int contact_status_duration_tu { get; init; }
    internal string contact_stack_behavior { get; init; } = "";
    internal int contact_stack_limit { get; init; }
    internal string contact_status_display_label { get; init; } = "";
    internal bool contact_counts_as_debuff_override { get; init; }
    internal bool contact_counts_as_debuff { get; init; }
    internal bool contact_undispellable { get; init; }
    internal bool contact_dispellable_magic { get; init; }
    internal bool contact_dispellable_harmful_magic { get; init; }
    internal bool contact_dispellable_beneficial_magic { get; init; }
    internal int contact_save_dc { get; init; }
    internal string contact_save_ability { get; init; } = "";
    internal string contact_save_tag { get; init; } = "";
    internal bool contact_apply_on_save_failure { get; init; }
    internal int contact_tick_interval_tu { get; init; }
    internal int contact_timeline_damage_dice_count { get; init; }
    internal int contact_timeline_damage_dice_sides { get; init; }
    internal int contact_timeline_damage_flat_bonus { get; init; }
    internal string contact_blocked_by_trait_id { get; init; } = "";
}

internal sealed class ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string anchor_selector { get; init; } = "";
    internal string terrain_effect_id { get; init; } = "";
    internal int move_cost_delta { get; init; }
    internal string target_team_filter { get; init; } = "";
    internal string stack_behavior { get; init; } = "";
    internal string display_name { get; init; } = "";
    internal string render_overlay_id { get; init; } = "";
    internal int overlay_priority { get; init; }
    internal string check_attribute_modifier_id { get; init; } = "";
    internal string check_compare { get; init; } = "";
    internal int check_threshold { get; init; }
    internal bool natural_twenty_auto_success { get; init; }
    internal bool natural_one_auto_failure { get; init; }
}

internal sealed class ApplyEdgeFeatureActionPayloadImportModel : IEquipmentAbilityActionPayloadImportModel
{
    internal string from_selector { get; init; } = "";
    internal string to_selector { get; init; } = "";
    internal int duration_tu { get; init; }
    internal int max_active_edges { get; init; }
    internal bool refresh_existing { get; init; }
    internal bool require_adjacent { get; init; }
    internal string feature_kind { get; init; } = "";
    internal string render_kind { get; init; } = "";
    internal int render_layers { get; init; }
    internal bool blocks_move { get; init; }
    internal bool blocks_occupancy { get; init; }
    internal bool blocks_los { get; init; }
    internal string interaction_kind { get; init; } = "";
    internal string state_tag { get; init; } = "";
}
