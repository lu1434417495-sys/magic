using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal enum BattleAiMeteorFriendlyFireProfile
{
    Unknown,
    Default,
    Reckless,
}

public partial class BattleAiScoreProfile : Resource
{
    private static readonly KeyValuePair<StringName, int>[] DefaultActionBaseScoreEntries =
    {
        new("skill", 0),
        new("move", 20),
        new("retreat", 35),
        new("wait", -40),
    };

    private static readonly KeyValuePair<StringName, int>[] DefaultBucketPriorityEntries =
    {
        new("mist_support", 120),
        new("mist_control", 110),
        new("mist_offense", 100),
        new("frontline_guard", 130),
        new("harrier_pressure", 100),
        new("charge_open", 100),
        new("archer_survival", 150),
        new("archer_positioning", 110),
        new("archer_pressure", 90),
    };

    private readonly Dictionary<StringName, int> _actionBaseScores = new();
    private readonly Dictionary<StringName, int> _bucketPriorities = new();

    private const int ThreatMultiplierBasisPointsDenominator = 10000;

    public BattleAiScoreProfile()
    {
        SetActionBaseScores(DefaultActionBaseScoreEntries);
        SetBucketPriorities(DefaultBucketPriorityEntries);
    }

    [Export]
    public int damage_weight = 10;

    [Export]
    public int heal_weight = 8;

    [Export]
    public int status_weight = 25;

    [Export]
    public int terrain_weight = 15;

    [Export]
    public int height_weight = 12;

    [Export]
    public int lethal_target_weight = 500;

    [Export]
    public int lethal_threat_target_weight = 900;

    [Export]
    public int target_count_weight = 40;

    [Export]
    public int friendly_fire_damage_weight = 35;

    [Export]
    public int friendly_fire_target_weight = 250;

    [Export]
    public int friendly_control_target_weight = 350;

    [Export]
    public int friendly_lethal_target_weight = 5000;

    [Export]
    public int ap_cost_weight = 25;

    [Export]
    public int mp_cost_weight = 15;

    [Export]
    public int stamina_cost_weight = 2;

    [Export]
    public int aura_cost_weight = 35;

    [Export]
    public int cooldown_weight = 8;

    [Export]
    public int movement_cost_weight = 18;

    [Export]
    public int mp_reserve_floor_bp = 0;

    [Export]
    public int mp_reserve_pressure_weight = 0;

    [Export]
    public int mp_reserve_breach_penalty = 0;

    [Export]
    public int stamina_reserve_floor_bp = 0;

    [Export]
    public int stamina_reserve_pressure_weight = 0;

    [Export]
    public int stamina_reserve_breach_penalty = 0;

    [Export]
    public int aura_reserve_floor_bp = 0;

    [Export]
    public int aura_reserve_pressure_weight = 0;

    [Export]
    public int aura_reserve_breach_penalty = 0;

    [Export]
    public int resource_conservation_weight = 100;

    [Export]
    public int position_base_score = 60;

    [Export]
    public int position_distance_step = 4;

    [Export]
    public int position_undershoot_penalty = 15;

    [Export]
    public int position_overshoot_penalty = 12;

    [Export]
    public int survival_margin_gain_weight = 0;

    [Export]
    public int post_action_threat_damage_weight = 0;

    [Export]
    public int post_action_threat_count_weight = 0;

    [Export]
    public int lethal_survival_risk_penalty = 0;

    [Export]
    public int incoming_threat_relief_weight = 0;

    [Export]
    public int low_hp_urgency_threshold_bp = 0;

    [Export]
    public int low_hp_urgency_weight = 0;

    [Export]
    public int execute_target_hp_threshold_bp = 0;

    [Export]
    public int execute_bonus_weight = 0;

    [Export]
    public int overkill_damage_penalty_weight = 0;

    [Export]
    public int role_threat_min_effective_range = 4;

    [Export]
    public int role_threat_distance_window = 4;

    [Export]
    public int role_threat_max_approach_distance = 7;

    [Export]
    public int role_threat_max_contact_range = 2;

    [Export]
    public int role_threat_in_range_score_step = 10;

    [Export]
    public int enemy_target_count_weight = 0;

    [Export]
    public int chain_enemy_target_weight = 0;

    [Export]
    public int focus_fire_wounded_target_weight = 0;

    [Export]
    public int hit_rate_reliability_weight = 0;

    [Export]
    public int save_reliable_damage_weight = 0;

    [Export]
    public int shield_absorbed_weight = 2;

    [Export]
    public int control_weight = 0;

    [Export]
    public int ground_control_weight = 0;

    [Export]
    public int status_redundancy_penalty = 0;

    [Export]
    public int position_objective_weight = 100;

    [Export]
    public int safe_distance_adherence_weight = 0;

    [Export]
    public int threat_healer_bias_basis_points = 1500;

    [Export]
    public int threat_control_bias_basis_points = 500;

    [Export]
    public int threat_ranged_bias_basis_points = 800;

    [Export]
    public int threat_range_step_bias_basis_points = 200;

    [Export]
    public int threat_multiplier_cap_basis_points = 15000;

    [Export]
    public int meteor_high_priority_threat_multiplier_bp = 11000;

    [Export]
    public int meteor_high_priority_damage_hp_percent = 35;

    [Export]
    public int meteor_high_priority_target_priority_score = 250;

    [Export]
    public int meteor_top_threat_rank = 1;

    [Export]
    public StringName meteor_friendly_fire_profile = "default";
    internal BattleAiMeteorFriendlyFireProfile MeteorFriendlyFireProfileKind
    {
        get => ToMeteorFriendlyFireProfile(meteor_friendly_fire_profile);
        set => meteor_friendly_fire_profile = ToStringName(value);
    }

    [Export]
    public int meteor_friendly_fire_soft_expected_hp_percent = 10;

    [Export]
    public int meteor_friendly_fire_hard_expected_hp_percent = 25;

    [Export]
    public int meteor_friendly_fire_hard_worst_case_hp_percent = 50;

    [Export]
    public GDictionary action_base_scores
    {
        get => ProjectScoreMap(_actionBaseScores);
        set => SetActionBaseScores(value);
    }

    [Export]
    public int default_bucket_priority;

    [Export]
    public GDictionary bucket_priorities
    {
        get => ProjectScoreMap(_bucketPriorities);
        set => SetBucketPriorities(value);
    }

    internal IReadOnlyDictionary<StringName, int> ActionBaseScoresTyped => _actionBaseScores;
    internal IReadOnlyDictionary<StringName, int> BucketPrioritiesTyped => _bucketPriorities;

    private void SetActionBaseScores(GDictionary values)
    {
        _actionBaseScores.Clear();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName key = rawKey.AsStringName();
            if (key == "")
                continue;
            _actionBaseScores[key] = values[rawKey].AsInt32();
        }
    }

    public void SetActionBaseScores(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _actionBaseScores.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "")
                continue;
            _actionBaseScores[pair.Key] = pair.Value;
        }
    }

    private void SetBucketPriorities(GDictionary values)
    {
        _bucketPriorities.Clear();
        if (values == null)
            return;
        foreach (Variant rawKey in values.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName key = rawKey.AsStringName();
            if (key == "")
                continue;
            _bucketPriorities[key] = values[rawKey].AsInt32();
        }
    }

    public void SetBucketPriorities(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _bucketPriorities.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "")
                continue;
            _bucketPriorities[pair.Key] = pair.Value;
        }
    }

    private static GDictionary ProjectScoreMap(Dictionary<StringName, int> values)
    {
        var projection = new GDictionary();
        if (values == null)
            return projection;
        foreach (KeyValuePair<StringName, int> pair in values)
            if (pair.Key != "")
                projection[pair.Key] = pair.Value;
        return projection;
    }

    public int GetActionBaseScore(StringName action_kind)
    {
        if (_actionBaseScores.TryGetValue(action_kind, out int score))
            return score;
        return _actionBaseScores.TryGetValue("skill", out int defaultScore) ? defaultScore : 0;
    }

    public int GetBucketPriority(StringName bucket_id)
    {
        if (_bucketPriorities.TryGetValue(bucket_id, out int priority))
            return priority;
        return default_bucket_priority;
    }

    internal static BattleAiMeteorFriendlyFireProfile ToMeteorFriendlyFireProfile(
        StringName value
    )
    {
        return value.ToString() switch
        {
            "default" => BattleAiMeteorFriendlyFireProfile.Default,
            "reckless" => BattleAiMeteorFriendlyFireProfile.Reckless,
            _ => BattleAiMeteorFriendlyFireProfile.Unknown,
        };
    }

    internal static StringName ToStringName(BattleAiMeteorFriendlyFireProfile value) =>
        value switch
        {
            BattleAiMeteorFriendlyFireProfile.Default => "default",
            BattleAiMeteorFriendlyFireProfile.Reckless => "reckless",
            _ => "",
        };

}
