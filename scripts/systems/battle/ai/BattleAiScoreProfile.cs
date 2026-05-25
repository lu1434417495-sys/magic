using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiScoreProfile : Resource
{
    public const int THREAT_MULTIPLIER_BASIS_POINTS_DENOMINATOR = 10000;

    [Export] public int damage_weight = 10;
    [Export] public int heal_weight = 8;
    [Export] public int status_weight = 25;
    [Export] public int terrain_weight = 15;
    [Export] public int height_weight = 12;
    [Export] public int lethal_target_weight = 500;
    [Export] public int lethal_threat_target_weight = 900;
    [Export] public int target_count_weight = 40;
    [Export] public int friendly_fire_damage_weight = 35;
    [Export] public int friendly_fire_target_weight = 250;
    [Export] public int friendly_control_target_weight = 350;
    [Export] public int friendly_lethal_target_weight = 5000;
    [Export] public int ap_cost_weight = 25;
    [Export] public int mp_cost_weight = 15;
    [Export] public int stamina_cost_weight = 2;
    [Export] public int aura_cost_weight = 35;
    [Export] public int cooldown_weight = 8;
    [Export] public int movement_cost_weight = 18;
    [Export] public int position_base_score = 60;
    [Export] public int position_distance_step = 4;
    [Export] public int position_undershoot_penalty = 15;
    [Export] public int position_overshoot_penalty = 12;
    [Export] public int threat_healer_bias_basis_points = 1500;
    [Export] public int threat_control_bias_basis_points = 500;
    [Export] public int threat_ranged_bias_basis_points = 800;
    [Export] public int threat_range_step_bias_basis_points = 200;
    [Export] public int threat_multiplier_cap_basis_points = 15000;
    [Export] public int meteor_high_priority_threat_multiplier_bp = 11000;
    [Export] public int meteor_high_priority_damage_hp_percent = 35;
    [Export] public int meteor_high_priority_target_priority_score = 250;
    [Export] public int meteor_top_threat_rank = 1;
    [Export] public StringName meteor_friendly_fire_profile = "default";
    [Export] public int meteor_friendly_fire_soft_expected_hp_percent = 10;
    [Export] public int meteor_friendly_fire_hard_expected_hp_percent = 25;
    [Export] public int meteor_friendly_fire_hard_worst_case_hp_percent = 50;
    [Export] public GDictionary action_base_scores = new()
    {
        ["skill"] = 0,
        ["move"] = 20,
        ["retreat"] = 35,
        ["wait"] = -40,
    };
    [Export] public int default_bucket_priority;
    [Export] public GDictionary bucket_priorities = new()
    {
        ["mist_support"] = 120,
        ["mist_control"] = 110,
        ["mist_offense"] = 100,
        ["frontline_guard"] = 130,
        ["harrier_pressure"] = 100,
        ["charge_open"] = 100,
        ["archer_survival"] = 150,
        ["archer_positioning"] = 110,
        ["archer_pressure"] = 90,
    };

    public int get_action_base_score(StringName action_kind)
    {
        string actionKey = action_kind.ToString();
        if (action_base_scores.ContainsKey(actionKey))
        {
            return action_base_scores[actionKey].AsInt32();
        }
        return action_base_scores.ContainsKey("skill") ? action_base_scores["skill"].AsInt32() : 0;
    }

    public int get_bucket_priority(StringName bucket_id)
    {
        string bucketKey = bucket_id.ToString();
        if (bucket_priorities.ContainsKey(bucketKey))
        {
            return bucket_priorities[bucketKey].AsInt32();
        }
        return default_bucket_priority;
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["damage_weight"] = damage_weight,
            ["heal_weight"] = heal_weight,
            ["status_weight"] = status_weight,
            ["terrain_weight"] = terrain_weight,
            ["height_weight"] = height_weight,
            ["lethal_target_weight"] = lethal_target_weight,
            ["lethal_threat_target_weight"] = lethal_threat_target_weight,
            ["target_count_weight"] = target_count_weight,
            ["friendly_fire_damage_weight"] = friendly_fire_damage_weight,
            ["friendly_fire_target_weight"] = friendly_fire_target_weight,
            ["friendly_control_target_weight"] = friendly_control_target_weight,
            ["friendly_lethal_target_weight"] = friendly_lethal_target_weight,
            ["ap_cost_weight"] = ap_cost_weight,
            ["mp_cost_weight"] = mp_cost_weight,
            ["stamina_cost_weight"] = stamina_cost_weight,
            ["aura_cost_weight"] = aura_cost_weight,
            ["cooldown_weight"] = cooldown_weight,
            ["movement_cost_weight"] = movement_cost_weight,
            ["position_base_score"] = position_base_score,
            ["position_distance_step"] = position_distance_step,
            ["position_undershoot_penalty"] = position_undershoot_penalty,
            ["position_overshoot_penalty"] = position_overshoot_penalty,
            ["threat_healer_bias_basis_points"] = threat_healer_bias_basis_points,
            ["threat_control_bias_basis_points"] = threat_control_bias_basis_points,
            ["threat_ranged_bias_basis_points"] = threat_ranged_bias_basis_points,
            ["threat_range_step_bias_basis_points"] = threat_range_step_bias_basis_points,
            ["threat_multiplier_cap_basis_points"] = threat_multiplier_cap_basis_points,
            ["meteor_high_priority_threat_multiplier_bp"] = meteor_high_priority_threat_multiplier_bp,
            ["meteor_high_priority_damage_hp_percent"] = meteor_high_priority_damage_hp_percent,
            ["meteor_high_priority_target_priority_score"] = meteor_high_priority_target_priority_score,
            ["meteor_top_threat_rank"] = meteor_top_threat_rank,
            ["meteor_friendly_fire_profile"] = meteor_friendly_fire_profile.ToString(),
            ["meteor_friendly_fire_soft_expected_hp_percent"] = meteor_friendly_fire_soft_expected_hp_percent,
            ["meteor_friendly_fire_hard_expected_hp_percent"] = meteor_friendly_fire_hard_expected_hp_percent,
            ["meteor_friendly_fire_hard_worst_case_hp_percent"] = meteor_friendly_fire_hard_worst_case_hp_percent,
            ["action_base_scores"] = action_base_scores.Duplicate(true),
            ["default_bucket_priority"] = default_bucket_priority,
            ["bucket_priorities"] = bucket_priorities.Duplicate(true),
        };
    }
}
