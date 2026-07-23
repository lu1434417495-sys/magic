using System;
using System.Collections.Generic;
using Godot;

internal enum CombatEffectTriggerEvent
{
    Unknown = 0,
    None,
    CriticalHit,
    OrdinaryHit,
    SecondaryHit,
}

internal enum CombatEffectTriggerCondition
{
    Unknown = 0,
    None,
    BattleStart,
    OnFatalDamage,
}

internal enum CombatEffectLifetimePolicy
{
    Unknown = 0,
    Timed,
    Battle,
}

[GlobalClass]
public partial class CombatEffectDef : Resource
{
    [Export]
    public StringName effect_type { get; set; } = "";
    internal BattleEffectKind EffectKind
    {
        get => BattleTypedNames.ToEffectKind(effect_type);
        set => effect_type = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName tick_effect_type { get; set; } = "";
    internal BattleEffectKind TickEffectKind
    {
        get => BattleTypedNames.ToEffectKind(tick_effect_type);
        set => tick_effect_type = BattleTypedNames.ToStringName(value);
    }

    internal BattleTerrainEffectRuntimeKind TerrainTickEffectKind
    {
        get => BattleTypedNames.ToTerrainEffectRuntimeKind(tick_effect_type);
        set => tick_effect_type = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public StringName lifetime_policy { get; set; } = "timed";
    internal CombatEffectLifetimePolicy LifetimePolicyKind
    {
        get => CombatEffectContentRules.ToLifetimePolicy(lifetime_policy);
        set => lifetime_policy = CombatEffectContentRules.ToStringName(value);
    }

    [Export]
    public int power { get; set; }

    [Export]
    public int move_cost_delta { get; set; }

    [Export]
    public StringName render_overlay_id { get; set; } = "";

    [Export]
    public int overlay_priority { get; set; }

    [Export]
    public string display_name { get; set; } = "";

    public BattleAttackRollModifierSpec accuracy_modifier_spec { get; set; }

    [Export]
    public StringName does_not_stack_with_status_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> does_not_stack_with_status_ids { get; set; } =
        new();

    [Export]
    public int min_skill_level { get; set; }

    [Export]
    public int max_skill_level { get; set; } = -1;

    [Export]
    public int damage_ratio_percent { get; set; } = 100;

    [Export]
    public double pre_resistance_damage_multiplier { get; set; } = 1.0;

    [Export]
    public StringName damage_tag { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> damage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> mitigation_bypass_damage_tags { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<StringName> mitigation_bypass_tiers { get; set; } =
        new();

    [Export]
    public StringName damage_category { get; set; } = "";

    [Export]
    public StringName dr_bypass_tag { get; set; } = "";

    [Export]
    public int hp_ratio_threshold_percent { get; set; }

    [Export]
    public int dice_count { get; set; }

    [Export]
    public int dice_sides { get; set; }

    [Export]
    public int dice_bonus { get; set; }

    [Export]
    public int dice_sides_base { get; set; }

    [Export]
    public int dice_sides_per_constitution_mod { get; set; }

    [Export]
    public int dice_sides_per_willpower_mod { get; set; }

    [Export]
    public int bonus_damage_dice_count { get; set; }

    [Export]
    public int bonus_damage_dice_sides { get; set; }

    [Export]
    public int bonus_damage_dice_bonus { get; set; }

    [Export]
    public int source_bound_weapon_bonus_damage_dice_count { get; set; }

    [Export]
    public int source_bound_weapon_bonus_damage_dice_sides { get; set; }

    [Export]
    public int source_bound_weapon_bonus_damage_dice_bonus { get; set; }

    [Export]
    public bool add_weapon_dice { get; set; }

    [Export]
    public bool requires_weapon { get; set; }

    [Export]
    public bool use_weapon_physical_damage_tag { get; set; }

    [Export]
    public bool resolve_as_weapon_attack { get; set; }

    [Export]
    public bool allow_repeat_hits_across_steps { get; set; }

    [Export]
    public bool prevent_repeat_target { get; set; } = true;

    [Export]
    public bool stop_on_miss { get; set; } = true;

    [Export]
    public bool stop_on_target_down { get; set; } = true;

    [Export]
    public bool remove_harmful { get; set; }

    [Export]
    public bool remove_harmful_from_allies { get; set; } = true;

    [Export]
    public bool remove_beneficial { get; set; }

    [Export]
    public bool remove_beneficial_from_enemies { get; set; } = true;

    [Export]
    public bool require_damage_applied { get; set; }

    [Export]
    public int max_status_removed { get; set; }

    [Export]
    public int min_hp_after_damage { get; set; } = 1;

    [Export]
    public int death_prevention_priority { get; set; }

    [Export]
    public int threshold_base_value { get; set; }

    [Export]
    public int threshold_level_anchor { get; set; } = 17;

    [Export]
    public int threshold_level_bonus_per_delta { get; set; } = 5;

    [Export]
    public int threshold_max_hp_ratio_percent { get; set; } = 20;

    [Export]
    public int threshold_cap_max_hp_ratio_percent { get; set; } = 50;

    [Export]
    public int soul_fracture_duration_tu { get; set; }

    [Export]
    public int heal_multiplier_percent { get; set; } = 100;

    [Export]
    public int shield_gain_multiplier_percent { get; set; } = 100;

    [Export]
    public int attack_roll_penalty { get; set; } = -1;

    [Export]
    public int attack_roll_bonus { get; set; }

    [Export]
    public bool attack_roll_advantage { get; set; }

    [Export]
    public bool consume_on_next_attack_check { get; set; }

    [Export]
    public bool consume_on_next_save { get; set; }

    [Export]
    public bool undispellable { get; set; }

    [Export]
    public bool dispellable_magic { get; set; }

    [Export]
    public bool dispellable_harmful_magic { get; set; }

    [Export]
    public bool dispellable_beneficial_magic { get; set; }

    [Export]
    public StringName mitigation_tier { get; set; } = "";

    [Export]
    public int secondary_hit_dc_base { get; set; } = 10;

    [Export]
    public int debuff_count_threshold { get; set; } = 3;

    [Export]
    public int base_heal { get; set; } = 8;

    [Export]
    public int heal_per_level { get; set; } = 4;

    [Export]
    public int con_mod_base { get; set; } = 2;

    [Export]
    public int con_mod_per_2_levels { get; set; } = 1;

    public int skill_level { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<StringName> effect_categories { get; set; } = new();

    [Export]
    public StringName effect_target_team_filter { get; set; } = "";

    [Export]
    public StringName status_id { get; set; } = "";

    [Export]
    public int applied_status_duration_tu { get; set; }

    [Export]
    public StringName terrain_effect_id { get; set; } = "";

    [Export]
    public StringName terrain_replace_to { get; set; } = "";

    [Export]
    public int height_delta { get; set; }

    [Export]
    public StringName body_size_category { get; set; } = "";

    [Export]
    public StringName forced_move_mode { get; set; } = "";
    internal BattleForcedMoveMode ForcedMoveModeKind
    {
        get => BattleTypedNames.ToForcedMoveMode(forced_move_mode);
        set => forced_move_mode = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public int forced_move_distance { get; set; }

    [Export]
    public int charge_trap_immunity_min_skill_level { get; set; } = -1;

    [Export]
    public int jump_base_budget { get; set; }

    [Export]
    public double jump_str_scale { get; set; }

    [Export]
    public double jump_arc_ratio { get; set; }

    [Export]
    public int jump_range_multiplier { get; set; } = 1;

    [Export]
    public int duration_tu { get; set; }

    [Export]
    public int tick_interval_tu { get; set; }

    [Export]
    public StringName stack_behavior { get; set; } = "refresh";

    [Export]
    public int stack_limit { get; set; }

    [Export]
    public StringName bonus_condition { get; set; } = "";

    [Export]
    public StringName bonus_condition_creature_type_tag { get; set; } = "";

    [Export]
    public StringName trigger_event { get; set; } = "";
    internal CombatEffectTriggerEvent TriggerEventKind
    {
        get => CombatEffectContentRules.ToTriggerEvent(trigger_event);
        set => trigger_event = CombatEffectContentRules.ToStringName(value);
    }

    [Export]
    public StringName trigger_condition { get; set; } = "";
    internal CombatEffectTriggerCondition TriggerConditionKind
    {
        get => CombatEffectContentRules.ToTriggerCondition(trigger_condition);
        set => trigger_condition = CombatEffectContentRules.ToStringName(value);
    }

    [Export]
    public StringName trigger_status_id { get; set; } = "";

    [Export]
    public int save_dc { get; set; }

    [Export]
    public StringName save_dc_mode { get; set; } = "static";

    internal BattleSaveDcMode SaveDcModeKind
    {
        get => BattleSaveContentRules.ToSaveDcMode(save_dc_mode);
        set => save_dc_mode = BattleSaveContentRules.ToStringName(value);
    }

    [Export]
    public StringName save_dc_source_ability { get; set; } = "";

    [Export]
    public StringName save_ability { get; set; } = "";

    [Export]
    public StringName save_failure_status_id { get; set; } = "";

    [Export]
    public bool save_partial_on_success { get; set; }

    [Export]
    public StringName save_tag { get; set; } = "";

    [Export]
    public StringName consumed_status_id { get; set; } = "";

    [Export]
    public StringName required_target_status_id { get; set; } = "";

    [Export]
    public int required_target_status_min_stacks { get; set; }

    [Export]
    public StringName required_target_status_source_selector { get; set; } = "";

    [Export]
    public int dice_per_consumed_stack { get; set; }

    [Export]
    public int dice_sides_per_stack { get; set; }

    [Export]
    public int ap_gain { get; set; }

    [Export]
    public int free_move_points_gain { get; set; }

    [Export]
    public bool counts_as_debuff_override { get; set; }

    [Export]
    public bool counts_as_debuff { get; set; }

    [Export]
    public bool lock_counterattack { get; set; }

    [Export]
    public bool lock_guard { get; set; }

    [Export]
    public bool lock_dodge_bonus { get; set; }

    [Export]
    public bool lock_crit { get; set; }

    [Export]
    public int save_bonus { get; set; }

    [Export]
    public int control_save_bonus { get; set; }

    [Export]
    public int passive_reduction { get; set; }

    [Export]
    public int content_dr { get; set; }

    [Export]
    public int guard_block { get; set; }

    [Export]
    public int range_bonus { get; set; }

    [Export]
    public int main_skill_lock_other_debuff_count { get; set; }

    [Export]
    public Godot.Collections.Array<StringName> save_advantage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_disadvantage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_immunity_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> effect_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CombatEffectSlotWeightDef> equipment_durability_slot_weights { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<CombatDamageSegmentDef> extra_damage_segments { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<CombatTargetDamageMultiplierRuleDef> target_damage_multiplier_rules { get; set; } =
        new();

    [Export]
    public Godot.Collections.Dictionary @params { get; set; } = new();

    internal bool HasEffectTagTyped(StringName tag)
    {
        if (tag == "" || effect_tags == null)
        {
            return false;
        }
        foreach (StringName effectTag in effect_tags)
        {
            if (effectTag == tag)
            {
                return true;
            }
        }
        return false;
    }

}
