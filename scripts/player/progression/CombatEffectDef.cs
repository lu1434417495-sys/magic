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
    private static readonly StringName TriggerEventCriticalHit = "critical_hit";
    private static readonly StringName TriggerEventOrdinaryHit = "ordinary_hit";
    private static readonly StringName TriggerEventSecondaryHit = "secondary_hit";
    private static readonly StringName TriggerConditionBattleStart = "battle_start";
    private static readonly StringName TriggerConditionOnFatalDamage = "on_fatal_damage";
    private static readonly StringName LifetimePolicyTimed = "timed";
    private static readonly StringName LifetimePolicyBattle = "battle";

    private const double MinJumpArcRatio = 0.15;

    public static double MIN_JUMP_ARC_RATIO() => MinJumpArcRatio;

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
        get => ToLifetimePolicy(lifetime_policy);
        set => lifetime_policy = ToStringName(value);
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
    public Godot.Collections.Array effect_categories { get; set; } = new();

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
    public StringName trigger_event { get; set; } = "";
    internal CombatEffectTriggerEvent TriggerEventKind
    {
        get => ToTriggerEvent(trigger_event);
        set => trigger_event = ToStringName(value);
    }

    [Export]
    public StringName trigger_condition { get; set; } = "";
    internal CombatEffectTriggerCondition TriggerConditionKind
    {
        get => ToTriggerCondition(trigger_condition);
        set => trigger_condition = ToStringName(value);
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

    internal static CombatEffectTriggerEvent ToTriggerEvent(StringName value)
    {
        if (value == "")
            return CombatEffectTriggerEvent.None;
        if (value == TriggerEventCriticalHit)
            return CombatEffectTriggerEvent.CriticalHit;
        if (value == TriggerEventOrdinaryHit)
            return CombatEffectTriggerEvent.OrdinaryHit;
        if (value == TriggerEventSecondaryHit)
            return CombatEffectTriggerEvent.SecondaryHit;
        return CombatEffectTriggerEvent.Unknown;
    }

    internal static CombatEffectTriggerCondition ToTriggerCondition(StringName value)
    {
        if (value == "")
            return CombatEffectTriggerCondition.None;
        if (value == TriggerConditionBattleStart)
            return CombatEffectTriggerCondition.BattleStart;
        if (value == TriggerConditionOnFatalDamage)
            return CombatEffectTriggerCondition.OnFatalDamage;
        return CombatEffectTriggerCondition.Unknown;
    }

    internal static CombatEffectLifetimePolicy ToLifetimePolicy(StringName value)
    {
        if (value == LifetimePolicyTimed)
            return CombatEffectLifetimePolicy.Timed;
        if (value == LifetimePolicyBattle)
            return CombatEffectLifetimePolicy.Battle;
        return CombatEffectLifetimePolicy.Unknown;
    }

    internal static StringName ToStringName(CombatEffectTriggerEvent triggerEvent)
    {
        return triggerEvent switch
        {
            CombatEffectTriggerEvent.None => "",
            CombatEffectTriggerEvent.CriticalHit => TriggerEventCriticalHit,
            CombatEffectTriggerEvent.OrdinaryHit => TriggerEventOrdinaryHit,
            CombatEffectTriggerEvent.SecondaryHit => TriggerEventSecondaryHit,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatEffectTriggerCondition triggerCondition)
    {
        return triggerCondition switch
        {
            CombatEffectTriggerCondition.None => "",
            CombatEffectTriggerCondition.BattleStart => TriggerConditionBattleStart,
            CombatEffectTriggerCondition.OnFatalDamage => TriggerConditionOnFatalDamage,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatEffectLifetimePolicy lifetimePolicy)
    {
        return lifetimePolicy switch
        {
            CombatEffectLifetimePolicy.Timed => LifetimePolicyTimed,
            CombatEffectLifetimePolicy.Battle => LifetimePolicyBattle,
            _ => "",
        };
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
    public Godot.Collections.Array<StringName> save_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> effect_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary @params { get; set; } = new();

    internal CombatEffectDef DuplicateForRuntime(
        GodotTransientResourceScope owner,
        string reason
    )
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));

        var copy = (CombatEffectDef)Duplicate(true);

        if (copy == null)
            return null;

        copy.@params =
            copy.@params != null
                ? copy.@params.Duplicate(true)
                : new Godot.Collections.Dictionary();
        copy.accuracy_modifier_spec = accuracy_modifier_spec?.Clone();

        return owner.Own(copy, reason);
    }

    internal int GetIntParamTyped(string key, int fallback = 0)
    {
        if (string.IsNullOrEmpty(key) || @params == null)
        {
            return fallback;
        }
        if (@params.ContainsKey(key))
        {
            Variant value = @params[key];
            return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
        }
        return fallback;
    }

    internal StringName GetStringNameParamTyped(string key, StringName fallback = default)
    {
        if (string.IsNullOrEmpty(key) || @params == null)
        {
            return fallback;
        }
        if (@params.ContainsKey(key))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(@params[key]);
            return normalized != "" ? normalized : fallback;
        }
        return fallback;
    }

    internal double GetFloatParamTyped(string key, double fallback = 0.0)
    {
        if (string.IsNullOrEmpty(key) || @params == null)
        {
            return fallback;
        }
        if (@params.ContainsKey(key))
        {
            Variant value = @params[key];
            return value.VariantType switch
            {
                Variant.Type.Int => value.AsInt64(),
                Variant.Type.Float => value.AsDouble(),
                _ => fallback,
            };
        }
        return fallback;
    }

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

    internal IReadOnlyList<StringName> GetStringNameListParamTyped(string key)
    {
        if (string.IsNullOrEmpty(key) || @params == null)
        {
            return System.Array.Empty<StringName>();
        }
        if (@params.ContainsKey(key))
        {
            return ProgressionDataUtils.to_string_name_array(@params[key]);
        }
        return System.Array.Empty<StringName>();
    }

    internal IReadOnlyDictionary<StringName, int> GetStringNameIntMapParamTyped(string key)
    {
        if (string.IsNullOrEmpty(key) || @params == null)
        {
            return new Dictionary<StringName, int>();
        }
        if (@params.ContainsKey(key))
        {
            Variant value = @params[key];
            if (value.VariantType != Variant.Type.Dictionary)
            {
                return new Dictionary<StringName, int>();
            }
            var result = new Dictionary<StringName, int>();
            foreach (Variant rawKey in value.AsGodotDictionary().Keys)
            {
                if (rawKey.VariantType != Variant.Type.StringName)
                {
                    continue;
                }
                Variant rawValue = value.AsGodotDictionary()[rawKey];
                if (rawValue.VariantType != Variant.Type.Int)
                {
                    continue;
                }
                StringName id = rawKey.AsStringName();
                if (id == "")
                {
                    continue;
                }
                result[id] = rawValue.AsInt32();
            }
            return result;
        }
        return new Dictionary<StringName, int>();
    }
}
