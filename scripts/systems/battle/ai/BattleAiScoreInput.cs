using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiScoreInput : RefCounted
{
    public BattleCommand command { get; set; }
    public SkillDef skill_def { get; set; }
    public BattlePreview preview { get; set; }
    public StringName action_kind { get; set; } = "skill";
    public string action_label { get; set; } = "";
    public StringName action_intent { get; set; } = "";
    public StringName score_bucket_id { get; set; } = "";
    public int score_bucket_priority { get; set; } = 0;
    public GDictionary runtime_action_metadata { get; set; } = new();
    public Vector2I primary_coord { get; set; } = new Vector2I(-1, -1);
    public Godot.Collections.Array<StringName> target_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> target_coords { get; set; } = new();
    public int target_count { get; set; } = 0;
    public Godot.Collections.Array<StringName> random_chain_candidate_unit_ids { get; set; } =
        new();
    public int random_chain_candidate_pool_count { get; set; } = 0;
    public int random_chain_max_hits_per_target { get; set; } = 0;
    public int random_chain_max_attempt_count { get; set; } = 0;
    public StringName random_chain_selection_policy { get; set; } = "";
    public StringName random_chain_pool_refresh_policy { get; set; } = "";
    public StringName random_chain_score_estimate_policy { get; set; } = "";
    public int effective_target_count { get; set; } = 0;
    public int enemy_target_count { get; set; } = 0;
    public int ally_target_count { get; set; } = 0;
    public int estimated_damage { get; set; } = 0;
    public int estimated_post_save_damage { get; set; } = 0;
    public int estimated_shield_absorbed { get; set; } = 0;
    public int estimated_healing { get; set; } = 0;
    public int estimated_enemy_damage { get; set; } = 0;
    public int estimated_ally_damage { get; set; } = 0;
    public int estimated_enemy_healing { get; set; } = 0;
    public int estimated_ally_healing { get; set; } = 0;
    public int estimated_status_count { get; set; } = 0;
    public int estimated_control_count { get; set; } = 0;
    public int estimated_terrain_effect_count { get; set; } = 0;
    public int estimated_height_delta { get; set; } = 0;
    public int estimated_ground_control_cell_count { get; set; } = 0;
    public int ground_control_score { get; set; } = 0;
    public int estimated_lethal_target_count { get; set; } = 0;
    public int estimated_lethal_threat_target_count { get; set; } = 0;
    public Godot.Collections.Array<StringName> estimated_lethal_target_ids { get; set; } = new();
    public Godot.Collections.Array<StringName> estimated_lethal_threat_target_ids { get; set; } =
        new();
    public Godot.Collections.Array<StringName> estimated_control_target_ids { get; set; } = new();
    public Godot.Collections.Array<StringName> estimated_control_threat_target_ids { get; set; } =
        new();
    public int estimated_friendly_fire_target_count { get; set; } = 0;
    public int estimated_friendly_fire_damage { get; set; } = 0;
    public int estimated_friendly_control_target_count { get; set; } = 0;
    public int estimated_friendly_lethal_target_count { get; set; } = 0;
    public int estimated_chain_target_count { get; set; } = 0;
    public int estimated_chain_enemy_target_count { get; set; } = 0;
    public int estimated_chain_ally_target_count { get; set; } = 0;
    public int estimated_hit_rate_percent { get; set; } = 100;
    public GDictionary save_estimates_by_target_id { get; set; } = new();
    public GDictionary damage_estimates_by_target_id { get; set; } = new();
    public GDictionary special_profile_preview_facts { get; set; } = new();
    public GArray target_numeric_summary { get; set; } = new();
    public GArray friendly_fire_numeric_summary { get; set; } = new();
    public string friendly_fire_reject_reason { get; set; } = "";
    public StringName meteor_use_case { get; set; } = "";
    public Godot.Collections.Array<StringName> high_priority_target_ids { get; set; } = new();
    public GDictionary high_priority_reasons { get; set; } = new();
    public string low_value_penalty_reason { get; set; } = "";
    public GArray attack_roll_modifier_breakdown { get; set; } = new();
    public int hit_payoff_score { get; set; } = 0;
    public int target_priority_score { get; set; } = 0;
    public int friendly_fire_penalty_score { get; set; } = 0;
    public int path_step_hit_count { get; set; } = 0;
    public int path_step_unique_target_count { get; set; } = 0;
    public GDictionary path_step_hit_counts_by_unit_id { get; set; } = new();
    public int path_step_payoff_score { get; set; } = 0;
    public int ap_cost { get; set; } = 0;
    public int mp_cost { get; set; } = 0;
    public int stamina_cost { get; set; } = 0;
    public int aura_cost { get; set; } = 0;
    public int cooldown_tu { get; set; } = 0;
    public int resource_cost_score { get; set; } = 0;
    public int move_cost { get; set; } = 0;
    public StringName position_objective_kind { get; set; } = "cast_distance";
    public int desired_min_distance { get; set; } = -1;
    public int desired_max_distance { get; set; } = -1;
    public Vector2I position_anchor_coord { get; set; } = new Vector2I(-1, -1);
    public int distance_to_primary_coord { get; set; } = -1;
    public int position_current_distance { get; set; } = -1;
    public int position_safe_distance { get; set; } = -1;
    public int position_objective_score { get; set; } = 0;
    public bool has_post_action_threat_projection { get; set; } = false;
    public Vector2I projected_actor_coord { get; set; } = new Vector2I(-1, -1);
    public Godot.Collections.Array<StringName> pre_action_threat_unit_ids { get; set; } = new();
    public int pre_action_threat_count { get; set; } = 0;
    public int pre_action_threat_expected_damage { get; set; } = 0;
    public int pre_action_survival_margin { get; set; } = 0;
    public bool pre_action_is_lethal_survival_risk { get; set; } = false;
    public Godot.Collections.Array<StringName> post_action_remaining_threat_unit_ids { get; set; } =
        new();
    public int post_action_remaining_threat_count { get; set; } = 0;
    public int post_action_remaining_threat_expected_damage { get; set; } = 0;
    public int post_action_survival_margin { get; set; } = 0;
    public bool post_action_is_lethal_survival_risk { get; set; } = false;
    public int total_score { get; set; } = 0;

    private bool _sealed;
    private string _sealed_fingerprint = "";

    public void seal()
    {
        _sealed_fingerprint = FingerprintDictionary(to_dict());
        _sealed = true;
    }

    public bool is_sealed()
    {
        return _sealed && !string.IsNullOrEmpty(_sealed_fingerprint);
    }

    public bool matches_sealed_fingerprint()
    {
        return is_sealed() && _sealed_fingerprint == FingerprintDictionary(to_dict());
    }

    public int[] to_move_to_range_ordering_facts()
    {
        return new[]
        {
            estimated_friendly_lethal_target_count,
            estimated_friendly_fire_target_count,
            friendly_fire_penalty_score,
            has_post_action_threat_projection ? 1 : 0,
            post_action_is_lethal_survival_risk ? 1 : 0,
            estimated_lethal_threat_target_count,
            estimated_lethal_target_count,
            IsEmergencySurvivalScore() ? 1 : 0,
            total_score,
            hit_payoff_score,
            effective_target_count,
            resource_cost_score,
            score_bucket_priority,
            target_count,
            position_objective_score,
            post_action_remaining_threat_count,
            post_action_remaining_threat_expected_damage,
            post_action_survival_margin,
            distance_to_primary_coord,
            desired_min_distance,
            desired_max_distance,
        };
    }

    private bool IsEmergencySurvivalScore()
    {
        if (score_bucket_id != "archer_survival")
        {
            return false;
        }
        if (has_post_action_threat_projection)
        {
            if (pre_action_is_lethal_survival_risk && !post_action_is_lethal_survival_risk)
            {
                return true;
            }
            if (
                pre_action_threat_expected_damage > post_action_remaining_threat_expected_damage
                && post_action_survival_margin >= 0
            )
            {
                return true;
            }
        }
        if (
            target_count > 0
            || effective_target_count > 0
            || enemy_target_count > 0
            || ally_target_count > 0
        )
        {
            return false;
        }
        if (estimated_damage != 0 || estimated_control_count != 0)
        {
            return false;
        }
        if (position_current_distance >= 0 && position_safe_distance > 0)
        {
            int currentGap = position_safe_distance - position_current_distance;
            if (currentGap < 2)
            {
                return false;
            }
            if (distance_to_primary_coord >= 0)
            {
                return distance_to_primary_coord >= position_safe_distance;
            }
        }
        return position_objective_score > 0;
    }

    public GDictionary to_dict()
    {
        string resolved_skill_id = "";
        if (command != null && command.skill_id != "")
        {
            resolved_skill_id = command.skill_id.ToString();
        }
        else if (runtime_action_metadata.ContainsKey("skill_id"))
        {
            resolved_skill_id = runtime_action_metadata["skill_id"].ToString();
        }
        else if (skill_def != null)
        {
            resolved_skill_id = skill_def.skill_id.ToString();
        }

        string resolved_command_unit_id = "";
        if (command != null && command.unit_id != "")
        {
            resolved_command_unit_id = command.unit_id.ToString();
        }

        return new GDictionary
        {
            ["action_kind"] = action_kind.ToString(),
            ["action_label"] = action_label,
            ["action_intent"] = action_intent.ToString(),
            ["score_bucket_id"] = score_bucket_id.ToString(),
            ["score_bucket_priority"] = score_bucket_priority,
            ["runtime_action_metadata"] = runtime_action_metadata.Duplicate(true),
            ["command_type"] = command != null ? command.command_type.ToString() : "",
            ["command_unit_id"] = resolved_command_unit_id,
            ["skill_id"] = resolved_skill_id,
            ["primary_coord"] = primary_coord,
            ["target_unit_ids"] = target_unit_ids.Duplicate(),
            ["target_coords"] = target_coords.Duplicate(),
            ["target_count"] = target_count,
            ["random_chain_candidate_unit_ids"] = random_chain_candidate_unit_ids.Duplicate(),
            ["random_chain_candidate_pool_count"] = random_chain_candidate_pool_count,
            ["random_chain_max_hits_per_target"] = random_chain_max_hits_per_target,
            ["random_chain_max_attempt_count"] = random_chain_max_attempt_count,
            ["random_chain_selection_policy"] = random_chain_selection_policy.ToString(),
            ["random_chain_pool_refresh_policy"] = random_chain_pool_refresh_policy.ToString(),
            ["random_chain_score_estimate_policy"] = random_chain_score_estimate_policy.ToString(),
            ["effective_target_count"] = effective_target_count,
            ["enemy_target_count"] = enemy_target_count,
            ["ally_target_count"] = ally_target_count,
            ["estimated_damage"] = estimated_damage,
            ["estimated_post_save_damage"] = estimated_post_save_damage,
            ["estimated_shield_absorbed"] = estimated_shield_absorbed,
            ["estimated_healing"] = estimated_healing,
            ["estimated_enemy_damage"] = estimated_enemy_damage,
            ["estimated_ally_damage"] = estimated_ally_damage,
            ["estimated_enemy_healing"] = estimated_enemy_healing,
            ["estimated_ally_healing"] = estimated_ally_healing,
            ["estimated_status_count"] = estimated_status_count,
            ["estimated_control_count"] = estimated_control_count,
            ["estimated_terrain_effect_count"] = estimated_terrain_effect_count,
            ["estimated_height_delta"] = estimated_height_delta,
            ["estimated_ground_control_cell_count"] = estimated_ground_control_cell_count,
            ["ground_control_score"] = ground_control_score,
            ["estimated_lethal_target_count"] = estimated_lethal_target_count,
            ["estimated_lethal_threat_target_count"] = estimated_lethal_threat_target_count,
            ["estimated_lethal_target_ids"] = estimated_lethal_target_ids.Duplicate(),
            ["estimated_lethal_threat_target_ids"] = estimated_lethal_threat_target_ids.Duplicate(),
            ["estimated_control_target_ids"] = estimated_control_target_ids.Duplicate(),
            ["estimated_control_threat_target_ids"] =
                estimated_control_threat_target_ids.Duplicate(),
            ["estimated_friendly_fire_target_count"] = estimated_friendly_fire_target_count,
            ["estimated_friendly_fire_damage"] = estimated_friendly_fire_damage,
            ["estimated_friendly_control_target_count"] = estimated_friendly_control_target_count,
            ["estimated_friendly_lethal_target_count"] = estimated_friendly_lethal_target_count,
            ["estimated_chain_target_count"] = estimated_chain_target_count,
            ["estimated_chain_enemy_target_count"] = estimated_chain_enemy_target_count,
            ["estimated_chain_ally_target_count"] = estimated_chain_ally_target_count,
            ["estimated_hit_rate_percent"] = estimated_hit_rate_percent,
            ["save_estimates_by_target_id"] = save_estimates_by_target_id.Duplicate(true),
            ["damage_estimates_by_target_id"] = damage_estimates_by_target_id.Duplicate(true),
            ["special_profile_preview_facts"] = special_profile_preview_facts.Duplicate(true),
            ["target_numeric_summary"] = target_numeric_summary.Duplicate(true),
            ["friendly_fire_numeric_summary"] = friendly_fire_numeric_summary.Duplicate(true),
            ["friendly_fire_reject_reason"] = friendly_fire_reject_reason,
            ["meteor_use_case"] = meteor_use_case.ToString(),
            ["high_priority_target_ids"] = high_priority_target_ids.Duplicate(),
            ["high_priority_reasons"] = high_priority_reasons.Duplicate(true),
            ["low_value_penalty_reason"] = low_value_penalty_reason,
            ["attack_roll_modifier_breakdown"] = attack_roll_modifier_breakdown.Duplicate(true),
            ["hit_payoff_score"] = hit_payoff_score,
            ["target_priority_score"] = target_priority_score,
            ["friendly_fire_penalty_score"] = friendly_fire_penalty_score,
            ["path_step_hit_count"] = path_step_hit_count,
            ["path_step_unique_target_count"] = path_step_unique_target_count,
            ["path_step_hit_counts_by_unit_id"] = path_step_hit_counts_by_unit_id.Duplicate(true),
            ["path_step_payoff_score"] = path_step_payoff_score,
            ["ap_cost"] = ap_cost,
            ["mp_cost"] = mp_cost,
            ["stamina_cost"] = stamina_cost,
            ["aura_cost"] = aura_cost,
            ["cooldown_tu"] = cooldown_tu,
            ["resource_cost_score"] = resource_cost_score,
            ["move_cost"] = move_cost,
            ["position_objective_kind"] = position_objective_kind.ToString(),
            ["desired_min_distance"] = desired_min_distance,
            ["desired_max_distance"] = desired_max_distance,
            ["position_anchor_coord"] = position_anchor_coord,
            ["distance_to_primary_coord"] = distance_to_primary_coord,
            ["position_current_distance"] = position_current_distance,
            ["position_safe_distance"] = position_safe_distance,
            ["position_objective_score"] = position_objective_score,
            ["has_post_action_threat_projection"] = has_post_action_threat_projection,
            ["projected_actor_coord"] = projected_actor_coord,
            ["pre_action_threat_unit_ids"] = pre_action_threat_unit_ids.Duplicate(),
            ["pre_action_threat_count"] = pre_action_threat_count,
            ["pre_action_threat_expected_damage"] = pre_action_threat_expected_damage,
            ["pre_action_survival_margin"] = pre_action_survival_margin,
            ["pre_action_is_lethal_survival_risk"] = pre_action_is_lethal_survival_risk,
            ["post_action_remaining_threat_unit_ids"] =
                post_action_remaining_threat_unit_ids.Duplicate(),
            ["post_action_remaining_threat_count"] = post_action_remaining_threat_count,
            ["post_action_remaining_threat_expected_damage"] =
                post_action_remaining_threat_expected_damage,
            ["post_action_survival_margin"] = post_action_survival_margin,
            ["post_action_is_lethal_survival_risk"] = post_action_is_lethal_survival_risk,
            ["total_score"] = total_score,
        };
    }

    private static string FingerprintDictionary(GDictionary dictionary)
    {
        var builder = new System.Text.StringBuilder();
        AppendDictionaryFingerprint(builder, dictionary ?? new GDictionary());
        return builder.ToString();
    }

    private static void AppendDictionaryFingerprint(
        System.Text.StringBuilder builder,
        GDictionary dictionary
    )
    {
        builder.Append('{');
        var entries = new System.Collections.Generic.List<(
            string KeyFingerprint,
            string ValueFingerprint
        )>();
        foreach (var key in dictionary.Keys)
        {
            entries.Add((FingerprintKey(key), FingerprintValue(dictionary[key])));
        }
        entries.Sort(
            (left, right) => string.CompareOrdinal(left.KeyFingerprint, right.KeyFingerprint)
        );

        bool first = true;
        foreach ((string keyFingerprint, string valueFingerprint) in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append(keyFingerprint);
            builder.Append(':');
            builder.Append(valueFingerprint);
        }
        builder.Append('}');
    }

    private static string FingerprintKey(object key)
    {
        var builder = new System.Text.StringBuilder();
        AppendValueFingerprint(builder, key);
        return builder.ToString();
    }

    private static string FingerprintValue(object value)
    {
        var builder = new System.Text.StringBuilder();
        AppendValueFingerprint(builder, value);
        return builder.ToString();
    }

    private static void AppendArrayFingerprint(System.Text.StringBuilder builder, GArray array)
    {
        builder.Append('[');
        bool first = true;
        foreach (var value in array)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            AppendValueFingerprint(builder, value);
        }
        builder.Append(']');
    }

    private static void AppendValueFingerprint(System.Text.StringBuilder builder, object payload)
    {
        if (payload is not Variant value)
        {
            if (payload is GDictionary dictionary)
            {
                builder.Append((int)Variant.Type.Dictionary);
                builder.Append('=');
                AppendDictionaryFingerprint(builder, dictionary);
                return;
            }
            if (payload is GArray array)
            {
                builder.Append((int)Variant.Type.Array);
                builder.Append('=');
                AppendArrayFingerprint(builder, array);
                return;
            }
            AppendTextFingerprint(builder, payload?.ToString() ?? "");
            return;
        }

        builder.Append((int)value.VariantType);
        builder.Append('=');
        switch (value.VariantType)
        {
            case Variant.Type.Nil:
                builder.Append("nil");
                return;
            case Variant.Type.Bool:
                builder.Append(value.AsBool() ? "true" : "false");
                return;
            case Variant.Type.Int:
                builder.Append(value.AsInt64());
                return;
            case Variant.Type.Float:
                builder.Append(
                    value
                        .AsDouble()
                        .ToString("R", System.Globalization.CultureInfo.GetCultureInfo(""))
                );
                return;
            case Variant.Type.String:
                AppendTextFingerprint(builder, value.AsString());
                return;
            case Variant.Type.StringName:
                AppendTextFingerprint(builder, value.AsStringName().ToString());
                return;
            case Variant.Type.Vector2I:
                Vector2I vector = value.AsVector2I();
                builder.Append(vector.X);
                builder.Append(',');
                builder.Append(vector.Y);
                return;
            case Variant.Type.Array:
                AppendArrayFingerprint(builder, value.AsGodotArray());
                return;
            case Variant.Type.Dictionary:
                AppendDictionaryFingerprint(builder, value.AsGodotDictionary());
                return;
            default:
                AppendTextFingerprint(builder, value.ToString());
                return;
        }
    }

    private static void AppendTextFingerprint(System.Text.StringBuilder builder, string value)
    {
        value ??= "";
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
    }
}
