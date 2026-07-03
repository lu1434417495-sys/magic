using Godot;

[GlobalClass]
public sealed partial class EquipmentAbilityContentPackDef : Resource
{
    [Export] public StringName pack_id { get; set; } = "";
    [Export] public int schema_version { get; set; } = 1;
    [Export] public int load_order { get; set; }
    [Export] public Godot.Collections.Array<StringName> dependencies { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAbilityBindingDef> bindings { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilityBindingDef : Resource
{
    [Export] public StringName binding_id { get; set; } = "";
    [Export] public StringName trait_id { get; set; } = "";
    [Export] public StringName override_mode { get; set; } = "add";
    [Export] public StringName replaces_binding_id { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> allowed_source_kinds { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> required_trait_categories { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> required_item_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> supported_equipment_type_ids { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAbilitySourceTraceDef> source_traces { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAbilityStateSchemaDef> state_schemas { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAbilityReactionDef> reactions { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentGrantedActionDef> granted_actions { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentWeaponProfileOverlayDef> weapon_profile_overlays { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentWorldEffectDef> world_effects { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilitySourceTraceDef : Resource
{
    [Export] public StringName source_kind { get; set; } = "";
    [Export] public string source_file { get; set; } = "";
    [Export] public StringName item_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export] public int bullet_index { get; set; }
    [Export] public string bullet_title { get; set; } = "";
    [Export] public string bullet_text { get; set; } = "";
    [Export] public StringName mechanism_family { get; set; } = "";
    [Export] public StringName coverage_status { get; set; } = "";
    [Export] public StringName phase { get; set; } = "";
    [Export] public StringName test_id { get; set; } = "";
    [Export] public string note { get; set; } = "";
}

[GlobalClass]
public sealed partial class EquipmentAbilityReactionDef : Resource
{
    [Export] public StringName reaction_id { get; set; } = "";
    [Export] public StringName trigger { get; set; } = "";
    [Export] public StringName timing { get; set; } = "";
    [Export] public int priority { get; set; }
    [Export] public StringName once_scope { get; set; } = "";
    [Export] public bool requires_player_confirmation { get; set; }
    [Export] public EquipmentAbilityConditionGroupDef condition_group { get; set; }
    [Export] public EquipmentRollGateDef roll_gate { get; set; }
    [Export] public EquipmentOutcomeTableDef outcome_table { get; set; }
    [Export] public Godot.Collections.Array<EquipmentAbilityActionDef> actions { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilityConditionGroupDef : Resource
{
    [Export] public StringName mode { get; set; } = "";
    [Export] public bool negate { get; set; }
    [Export] public Godot.Collections.Array<EquipmentAbilityConditionDef> conditions { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAbilityConditionGroupDef> groups { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilityConditionDef : Resource
{
    [Export] public StringName condition_id { get; set; } = "";
    [Export] public StringName kind { get; set; } = "";
    [Export] public Resource payload { get; set; }
}

[GlobalClass]
public sealed partial class HasStatusConditionPayloadDef : Resource
{
    [Export] public StringName subject { get; set; } = "";
    [Export] public StringName status_id { get; set; } = "";
}

[GlobalClass]
public sealed partial class CompareFactConditionPayloadDef : Resource
{
    [Export] public EquipmentAbilityFactQueryDef left { get; set; }
    [Export] public StringName compare { get; set; } = "";
    [Export] public EquipmentAbilityFactQueryDef right { get; set; }
}

[GlobalClass]
public sealed partial class HasEquipmentTagConditionPayloadDef : Resource
{
    [Export] public StringName subject { get; set; } = "";
    [Export] public StringName equipment_selector { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> all_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> any_tags { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilityFactQueryDef : Resource
{
    [Export] public StringName query_kind { get; set; } = "";
    [Export] public StringName fact_id { get; set; } = "";
    [Export] public StringName subject { get; set; } = "";
    [Export] public StringName binding_id { get; set; } = "";
    [Export] public StringName state_key { get; set; } = "";
    [Export] public StringName status_id { get; set; } = "";
    [Export] public StringName attribute_id { get; set; } = "";
    [Export] public StringName aggregation { get; set; } = "";
    [Export] public StringName value_kind { get; set; } = "";
    [Export] public bool bool_literal { get; set; }
    [Export] public int int_literal { get; set; }
    [Export] public float float_literal { get; set; }
    [Export] public StringName string_name_literal { get; set; } = "";
}

[GlobalClass]
public sealed partial class DiceExpressionDef : Resource
{
    [Export] public Godot.Collections.Array<DiceExpressionTermDef> terms { get; set; } = new();
    [Export] public int flat_bonus { get; set; }
    [Export] public StringName preview_policy { get; set; } = "";
}

[GlobalClass]
public sealed partial class DiceExpressionTermDef : Resource
{
    [Export] public int dice_count { get; set; }
    [Export] public int dice_sides { get; set; }
    [Export] public EquipmentAbilityFactQueryDef count_bonus_fact { get; set; }
    [Export] public float count_bonus_multiplier { get; set; }
    [Export] public int max_dice_count { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentAbilityActionDef : Resource
{
    [Export] public StringName action_id { get; set; } = "";
    [Export] public StringName kind { get; set; } = "";
    [Export] public Resource payload { get; set; }
    [Export] public EquipmentAbilityConditionGroupDef condition_group { get; set; }
    [Export] public EquipmentRollGateDef roll_gate { get; set; }
}

[GlobalClass]
public sealed partial class AddDamageDiceActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public DiceExpressionDef dice { get; set; }
    [Export] public StringName damage_type { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> damage_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> mitigation_bypass_damage_tags { get; set; } =
        new();
    [Export] public Godot.Collections.Array<StringName> mitigation_bypass_tiers { get; set; } =
        new();
}

[GlobalClass]
public sealed partial class DealDamageActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public DiceExpressionDef dice { get; set; }
    [Export] public StringName damage_type { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> damage_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> mitigation_bypass_damage_tags { get; set; } =
        new();
    [Export] public Godot.Collections.Array<StringName> mitigation_bypass_tiers { get; set; } =
        new();
}

[GlobalClass]
public sealed partial class AttackRollBonusActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public int bonus { get; set; }
    [Export] public StringName stack_mode { get; set; } = "max";
    [Export] public string label { get; set; } = "";
}

[GlobalClass]
public sealed partial class AttackRollAdvantageActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName mode { get; set; } = "advantage";
    [Export] public StringName stack_mode { get; set; } = "max";
    [Export] public string label { get; set; } = "";
}

[GlobalClass]
public sealed partial class DamageRollModeOverrideActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName roll_mode { get; set; } = "maximum";
    [Export] public StringName stack_mode { get; set; } = "max";
    [Export] public string label { get; set; } = "";
}

[GlobalClass]
public sealed partial class LootQuantityMultiplierActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public int multiplier_percent { get; set; } = 100;
    [Export] public Godot.Collections.Array<StringName> affected_drop_kinds { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> any_item_tags { get; set; } = new();
}

[GlobalClass]
public sealed partial class ApplyStatusActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName status_id { get; set; } = "";
    [Export] public int duration_turns { get; set; }
    [Export] public int duration_tu { get; set; }
    [Export] public int stack_delta { get; set; }
    [Export] public StringName stack_behavior { get; set; } = "refresh";
    [Export] public int stack_limit { get; set; }
    [Export] public string display_label { get; set; } = "";
    [Export] public int attack_roll_penalty { get; set; } = -1;
    [Export] public int source_bound_attack_roll_penalty { get; set; }
    [Export] public int source_bound_attack_roll_penalty_min_stacks { get; set; } = 1;
    [Export] public int source_bound_incoming_attack_roll_bonus_per_stack { get; set; }
    [Export] public int source_bound_incoming_attack_roll_bonus_min_stacks { get; set; } = 1;
    [Export] public int move_point_capacity_delta { get; set; }
    [Export] public bool counts_as_debuff_override { get; set; }
    [Export] public bool counts_as_debuff { get; set; }
    [Export] public bool undispellable { get; set; }
    [Export] public bool dispellable_magic { get; set; }
    [Export] public bool dispellable_harmful_magic { get; set; }
    [Export] public bool dispellable_beneficial_magic { get; set; }
    [Export] public int tick_interval_tu { get; set; }
    [Export] public int timeline_damage_dice_count { get; set; }
    [Export] public int timeline_damage_dice_sides { get; set; }
    [Export] public int timeline_damage_flat_bonus { get; set; }
    [Export] public int save_dc { get; set; }
    [Export] public StringName save_ability { get; set; } = "";
    [Export] public StringName save_tag { get; set; } = "";
    [Export] public bool apply_on_save_failure { get; set; }
}

[GlobalClass]
public sealed partial class ModifyAbilityStateActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName binding_id { get; set; } = "";
    [Export] public StringName state_key { get; set; } = "";
    [Export] public StringName operation { get; set; } = "";
    [Export] public int int_delta { get; set; }
}

[GlobalClass]
public sealed partial class MarkTargetActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName state_key { get; set; } = "";
    [Export] public int stack_delta { get; set; }
    [Export] public bool remove_on_source_missing { get; set; }
    [Export] public bool remove_on_target_defeated { get; set; }
    [Export] public bool unique_per_source { get; set; } = true;
    [Export] public StringName mirror_status_id { get; set; } = "";
    [Export] public int mirror_status_duration_tu { get; set; }
    [Export] public StringName mirror_status_stack_behavior { get; set; } = "refresh";
    [Export] public int mirror_status_stack_limit { get; set; } = 1;
    [Export] public string mirror_status_display_label { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> clear_status_ids_on_replace { get; set; } =
        new();
}

[GlobalClass]
public sealed partial class ClearStatusActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public StringName status_id { get; set; } = "";
    [Export] public StringName mark_binding_id { get; set; } = "";
    [Export] public StringName mark_state_key { get; set; } = "";
    [Export] public bool require_source_unit_match { get; set; }
}

[GlobalClass]
public sealed partial class GrantSkillActionPayloadDef : Resource
{
    [Export] public StringName skill_id { get; set; } = "";
    [Export] public int skill_level { get; set; }
    [Export] public StringName availability_state_key { get; set; } = "";
}

[GlobalClass]
public sealed partial class EquipmentSlotWeightDef : Resource
{
    [Export] public StringName slot_id { get; set; } = "";
    [Export] public int weight { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentDurabilityDamageActionPayloadDef : Resource
{
    [Export] public StringName target_selector { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> target_slots { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentSlotWeightDef> slot_weights { get; set; } =
        new();
    [Export] public Godot.Collections.Array<StringName> required_item_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> required_equipment_type_ids { get; set; } = new();
    [Export] public int durability_loss { get; set; }
    [Export] public StringName save_tag { get; set; } = "";
    [Export] public int save_dc { get; set; }
    [Export] public bool require_attack_success { get; set; }
    [Export] public int max_damaged_items { get; set; } = 1;
    [Export] public int max_target_rarity { get; set; } = -1;
}

[GlobalClass]
public sealed partial class EquipmentAttackDefenseModifierDef : Resource
{
    [Export] public StringName modifier_id { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> ignored_ac_components { get; set; } = new();
    [Export] public Godot.Collections.Array<EquipmentAcComponentMultiplierDef> ac_component_multipliers { get; set; } = new();
    [Export] public bool lock_dodge_bonus { get; set; }
    [Export] public StringName required_target_equipment_selector { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> required_target_item_tags { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> required_target_equipment_type_ids { get; set; } = new();
    [Export] public StringName cover_policy { get; set; } = "";
    [Export] public StringName projectile_obstacle_policy { get; set; } = "";
    [Export] public StringName trace_label { get; set; } = "";
}

[GlobalClass]
public sealed partial class EquipmentAcComponentMultiplierDef : Resource
{
    [Export] public StringName ac_component_id { get; set; } = "";
    [Export] public int multiplier_percent { get; set; }
    [Export] public StringName stack_mode { get; set; } = "";
}

[GlobalClass]
public sealed partial class EquipmentWeaponProfileOverlayDef : Resource
{
    [Export] public StringName overlay_id { get; set; } = "";
    [Export] public int priority { get; set; }
    [Export] public EquipmentAbilityConditionGroupDef condition_group { get; set; }
    [Export] public bool require_equipped_weapon { get; set; }
    [Export] public Godot.Collections.Array<StringName> required_weapon_families { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> required_weapon_type_ids { get; set; } = new();
    [Export] public int attack_range_delta { get; set; }
    [Export] public int min_attack_range { get; set; }
    [Export] public int max_attack_range { get; set; }
    [Export] public EquipmentWeaponDiceOverlayDef one_handed_dice_overlay { get; set; }
    [Export] public EquipmentWeaponDiceOverlayDef two_handed_dice_overlay { get; set; }
    [Export] public StringName physical_damage_tag_override { get; set; } = "";
    [Export] public StringName grip_override { get; set; } = "";
    [Export] public bool uses_two_hands_override { get; set; }
    [Export] public bool is_versatile_override { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentWeaponDiceOverlayDef : Resource
{
    [Export] public StringName mode { get; set; } = "";
    [Export] public int dice_count_delta { get; set; }
    [Export] public int dice_sides_override { get; set; }
    [Export] public int flat_bonus_delta { get; set; }
    [Export] public DiceExpressionDef dice_override { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentRollGateDef : Resource
{
    [Export] public StringName rng_stream { get; set; } = "";
    [Export] public DiceExpressionDef roll { get; set; }
    [Export] public StringName compare { get; set; } = "";
    [Export] public int threshold { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentOutcomeTableDef : Resource
{
    [Export] public StringName table_id { get; set; } = "";
    [Export] public DiceExpressionDef roll { get; set; }
    [Export] public Godot.Collections.Array<EquipmentOutcomeEntryDef> entries { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentOutcomeEntryDef : Resource
{
    [Export] public int min_roll { get; set; }
    [Export] public int max_roll { get; set; }
    [Export] public Godot.Collections.Array<EquipmentAbilityActionDef> actions { get; set; } = new();
}

[GlobalClass]
public sealed partial class EquipmentAbilityStateSchemaDef : Resource
{
    [Export] public StringName state_key { get; set; } = "";
    [Export] public StringName owner_scope { get; set; } = "";
    [Export] public StringName value_kind { get; set; } = "";
    [Export] public int initial_int_value { get; set; }
    [Export] public int max_int_value { get; set; }
    [Export] public StringName reset_timing { get; set; } = "";
    [Export] public bool persist_outside_battle { get; set; }
    [Export] public bool visible_to_ui { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentGrantedActionDef : Resource
{
    [Export] public StringName granted_action_id { get; set; } = "";
    [Export] public StringName granted_kind { get; set; } = "";
    [Export] public StringName skill_id { get; set; } = "";
    [Export] public int skill_level { get; set; }
    [Export] public StringName usage_period_kind { get; set; } = "";
    [Export] public int max_uses_per_period { get; set; }
    [Export] public StringName display_category { get; set; } = "";
    [Export] public int display_priority { get; set; }
    [Export] public EquipmentAbilityConditionGroupDef availability_conditions { get; set; }
}

[GlobalClass]
public sealed partial class EquipmentWorldEffectDef : Resource
{
    [Export] public StringName world_effect_id { get; set; } = "";
    [Export] public StringName trigger { get; set; } = "";
    [Export] public StringName timing { get; set; } = "";
    [Export] public EquipmentAbilityConditionGroupDef condition_group { get; set; }
    [Export] public Godot.Collections.Array<EquipmentAbilityActionDef> actions { get; set; } = new();
}
