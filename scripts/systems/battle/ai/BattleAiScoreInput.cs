using System.Collections.Generic;
using DamageEstimateBreakdown = BattleAiScoreService.DamageEstimateBreakdown;
using DamageSaveEstimate = BattleAiScoreService.DamageSaveEstimate;
using Godot;
using GArray = Godot.Collections.Array;

public sealed class BattleAiScoreInput
{
    public BattleCommand command { get; set; }
    public SkillDef skill_def { get; set; }
    public BattlePreview preview { get; set; }
    public StringName action_kind { get; set; } = "skill";
    public string action_label { get; set; } = "";
    public StringName action_intent { get; set; } = "";
    public StringName score_bucket_id { get; set; } = "";
    public int score_bucket_priority { get; set; } = 0;
    public BattleAiScoreRuntimeMetadata runtime_action_metadata { get; set; } = new();
    public Vector2I primary_coord { get; set; } = new Vector2I(-1, -1);
    public List<StringName> target_unit_ids { get; set; } = new();
    public List<Vector2I> target_coords { get; set; } = new();
    public int target_count { get; set; } = 0;
    public List<StringName> random_chain_candidate_unit_ids { get; set; } = new();
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
    public List<StringName> estimated_lethal_target_ids { get; set; } = new();
    public List<StringName> estimated_lethal_threat_target_ids { get; set; } = new();
    public List<StringName> estimated_control_target_ids { get; set; } = new();
    public List<StringName> estimated_control_threat_target_ids { get; set; } = new();
    public int estimated_friendly_fire_target_count { get; set; } = 0;
    public int estimated_friendly_fire_damage { get; set; } = 0;
    public int estimated_friendly_control_target_count { get; set; } = 0;
    public int estimated_friendly_lethal_target_count { get; set; } = 0;
    public int estimated_chain_target_count { get; set; } = 0;
    public int estimated_chain_enemy_target_count { get; set; } = 0;
    public int estimated_chain_ally_target_count { get; set; } = 0;
    public int estimated_hit_rate_percent { get; set; } = 100;
    public Dictionary<StringName, List<DamageSaveEstimate>> save_estimates_by_target_id { get; set; } = new();
    public Dictionary<StringName, List<DamageEstimateBreakdown>> damage_estimates_by_target_id { get; set; } = new();
    public BattleSpecialProfilePreviewFacts special_profile_preview_facts { get; set; }
    public List<MeteorSwarmNumericSummary> target_numeric_summary { get; set; } = new();
    public List<MeteorSwarmNumericSummary> friendly_fire_numeric_summary { get; set; } = new();
    public string friendly_fire_reject_reason { get; set; } = "";
    public StringName meteor_use_case { get; set; } = "";
    public List<StringName> high_priority_target_ids { get; set; } = new();
    public Dictionary<StringName, List<string>> high_priority_reasons { get; set; } = new();
    public string low_value_penalty_reason { get; set; } = "";
    public List<BattleAttackRollModifierSpec> attack_roll_modifier_breakdown { get; set; } = new();
    public int hit_payoff_score { get; set; } = 0;
    public int target_priority_score { get; set; } = 0;
    public int friendly_fire_penalty_score { get; set; } = 0;
    public int path_step_hit_count { get; set; } = 0;
    public int path_step_unique_target_count { get; set; } = 0;
    public Dictionary<StringName, int> path_step_hit_counts_by_unit_id { get; set; } = new();
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
    public List<StringName> pre_action_threat_unit_ids { get; set; } = new();
    public int pre_action_threat_count { get; set; } = 0;
    public int pre_action_threat_expected_damage { get; set; } = 0;
    public int pre_action_survival_margin { get; set; } = 0;
    public bool pre_action_is_lethal_survival_risk { get; set; } = false;
    public List<StringName> post_action_remaining_threat_unit_ids { get; set; } = new();
    public int post_action_remaining_threat_count { get; set; } = 0;
    public int post_action_remaining_threat_expected_damage { get; set; } = 0;
    public int post_action_survival_margin { get; set; } = 0;
    public bool post_action_is_lethal_survival_risk { get; set; } = false;
    public int total_score { get; set; } = 0;

    private bool _sealed;
    private string _sealed_fingerprint = "";

    internal void Seal()
    {
        _sealed_fingerprint = FingerprintTypedState();
        _sealed = true;
    }

    internal bool IsSealed()
    {
        return _sealed && !string.IsNullOrEmpty(_sealed_fingerprint);
    }

    internal bool MatchesSealedFingerprint()
    {
        return IsSealed() && _sealed_fingerprint == FingerprintTypedState();
    }

    internal int[] ToMoveToRangeOrderingFacts()
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

    internal Dictionary<string, object> ToTraceDictionary()
    {
        string resolvedSkillId = "";
        if (command != null && command.skill_id != "")
        {
            resolvedSkillId = command.skill_id.ToString();
        }
        else if (runtime_action_metadata != null && runtime_action_metadata.skill_id != "")
        {
            resolvedSkillId = runtime_action_metadata.skill_id.ToString();
        }
        else if (skill_def != null)
        {
            resolvedSkillId = skill_def.skill_id.ToString();
        }

        string resolvedCommandUnitId = "";
        if (command != null && command.unit_id != "")
        {
            resolvedCommandUnitId = command.unit_id.ToString();
        }

        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["action_kind"] = action_kind.ToString(),
            ["action_label"] = action_label ?? "",
            ["action_intent"] = action_intent.ToString(),
            ["score_bucket_id"] = score_bucket_id.ToString(),
            ["score_bucket_priority"] = score_bucket_priority,
            ["runtime_action_metadata"] = ToTraceDictionary(runtime_action_metadata),
            ["command_type"] = command != null ? command.command_type.ToString() : "",
            ["command_unit_id"] = resolvedCommandUnitId,
            ["skill_id"] = resolvedSkillId,
            ["primary_coord"] = primary_coord,
            ["target_unit_ids"] = CloneStringNameList(target_unit_ids),
            ["target_coords"] = CloneVector2IList(target_coords),
            ["target_count"] = target_count,
            ["random_chain_candidate_unit_ids"] = CloneStringNameList(
                random_chain_candidate_unit_ids
            ),
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
            ["estimated_lethal_target_ids"] = CloneStringNameList(estimated_lethal_target_ids),
            ["estimated_lethal_threat_target_ids"] = CloneStringNameList(
                estimated_lethal_threat_target_ids
            ),
            ["estimated_control_target_ids"] = CloneStringNameList(
                estimated_control_target_ids
            ),
            ["estimated_control_threat_target_ids"] = CloneStringNameList(
                estimated_control_threat_target_ids
            ),
            ["estimated_friendly_fire_target_count"] = estimated_friendly_fire_target_count,
            ["estimated_friendly_fire_damage"] = estimated_friendly_fire_damage,
            ["estimated_friendly_control_target_count"] = estimated_friendly_control_target_count,
            ["estimated_friendly_lethal_target_count"] = estimated_friendly_lethal_target_count,
            ["estimated_chain_target_count"] = estimated_chain_target_count,
            ["estimated_chain_enemy_target_count"] = estimated_chain_enemy_target_count,
            ["estimated_chain_ally_target_count"] = estimated_chain_ally_target_count,
            ["estimated_hit_rate_percent"] = estimated_hit_rate_percent,
            ["save_estimates_by_target_id"] = ToTraceSaveEstimateDictionary(
                save_estimates_by_target_id
            ),
            ["damage_estimates_by_target_id"] = ToTraceDamageEstimateDictionary(
                damage_estimates_by_target_id
            ),
            ["special_profile_preview_facts"] = ToTraceDictionary(
                special_profile_preview_facts
            ),
            ["target_numeric_summary"] = ToTraceNumericSummaryList(target_numeric_summary),
            ["friendly_fire_numeric_summary"] = ToTraceNumericSummaryList(
                friendly_fire_numeric_summary
            ),
            ["friendly_fire_reject_reason"] = friendly_fire_reject_reason ?? "",
            ["meteor_use_case"] = meteor_use_case.ToString(),
            ["high_priority_target_ids"] = CloneStringNameList(high_priority_target_ids),
            ["high_priority_reasons"] = ToTraceHighPriorityReasonDictionary(
                high_priority_reasons
            ),
            ["low_value_penalty_reason"] = low_value_penalty_reason ?? "",
            ["attack_roll_modifier_breakdown"] = ToTraceAttackRollModifierBreakdownList(
                attack_roll_modifier_breakdown
            ),
            ["hit_payoff_score"] = hit_payoff_score,
            ["target_priority_score"] = target_priority_score,
            ["friendly_fire_penalty_score"] = friendly_fire_penalty_score,
            ["path_step_hit_count"] = path_step_hit_count,
            ["path_step_unique_target_count"] = path_step_unique_target_count,
            ["path_step_hit_counts_by_unit_id"] = ToTraceStringNameIntDictionary(
                path_step_hit_counts_by_unit_id
            ),
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
            ["pre_action_threat_unit_ids"] = CloneStringNameList(pre_action_threat_unit_ids),
            ["pre_action_threat_count"] = pre_action_threat_count,
            ["pre_action_threat_expected_damage"] = pre_action_threat_expected_damage,
            ["pre_action_survival_margin"] = pre_action_survival_margin,
            ["pre_action_is_lethal_survival_risk"] = pre_action_is_lethal_survival_risk,
            ["post_action_remaining_threat_unit_ids"] = CloneStringNameList(
                post_action_remaining_threat_unit_ids
            ),
            ["post_action_remaining_threat_count"] = post_action_remaining_threat_count,
            ["post_action_remaining_threat_expected_damage"] =
                post_action_remaining_threat_expected_damage,
            ["post_action_survival_margin"] = post_action_survival_margin,
            ["post_action_is_lethal_survival_risk"] = post_action_is_lethal_survival_risk,
            ["total_score"] = total_score,
        };
    }

    private string FingerprintTypedState()
    {
        string resolvedSkillId = "";
        if (command != null && command.skill_id != "")
        {
            resolvedSkillId = command.skill_id.ToString();
        }
        else if (runtime_action_metadata != null && runtime_action_metadata.skill_id != "")
        {
            resolvedSkillId = runtime_action_metadata.skill_id.ToString();
        }
        else if (skill_def != null)
        {
            resolvedSkillId = skill_def.skill_id.ToString();
        }

        var builder = new System.Text.StringBuilder();
        builder.Append('{');
        AppendNamedValueFingerprint(builder, "action_kind", action_kind);
        AppendNamedValueFingerprint(builder, "action_label", action_label);
        AppendNamedValueFingerprint(builder, "action_intent", action_intent);
        AppendNamedValueFingerprint(builder, "score_bucket_id", score_bucket_id);
        AppendNamedValueFingerprint(builder, "score_bucket_priority", score_bucket_priority);
        AppendNamedValueFingerprint(builder, "resolved_skill_id", resolvedSkillId);
        AppendNamedCommandFingerprint(builder, "command", command);
        AppendNamedValueFingerprint(
            builder,
            "runtime_action_metadata",
            runtime_action_metadata
        );
        AppendNamedValueFingerprint(builder, "primary_coord", primary_coord);
        AppendNamedValueFingerprint(builder, "target_unit_ids", target_unit_ids);
        AppendNamedValueFingerprint(builder, "target_coords", target_coords);
        AppendNamedValueFingerprint(builder, "target_count", target_count);
        AppendNamedValueFingerprint(
            builder,
            "random_chain_candidate_unit_ids",
            random_chain_candidate_unit_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_candidate_pool_count",
            random_chain_candidate_pool_count
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_max_hits_per_target",
            random_chain_max_hits_per_target
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_max_attempt_count",
            random_chain_max_attempt_count
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_selection_policy",
            random_chain_selection_policy
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_pool_refresh_policy",
            random_chain_pool_refresh_policy
        );
        AppendNamedValueFingerprint(
            builder,
            "random_chain_score_estimate_policy",
            random_chain_score_estimate_policy
        );
        AppendNamedValueFingerprint(builder, "effective_target_count", effective_target_count);
        AppendNamedValueFingerprint(builder, "enemy_target_count", enemy_target_count);
        AppendNamedValueFingerprint(builder, "ally_target_count", ally_target_count);
        AppendNamedValueFingerprint(builder, "estimated_damage", estimated_damage);
        AppendNamedValueFingerprint(
            builder,
            "estimated_post_save_damage",
            estimated_post_save_damage
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_shield_absorbed",
            estimated_shield_absorbed
        );
        AppendNamedValueFingerprint(builder, "estimated_healing", estimated_healing);
        AppendNamedValueFingerprint(
            builder,
            "estimated_enemy_damage",
            estimated_enemy_damage
        );
        AppendNamedValueFingerprint(builder, "estimated_ally_damage", estimated_ally_damage);
        AppendNamedValueFingerprint(
            builder,
            "estimated_enemy_healing",
            estimated_enemy_healing
        );
        AppendNamedValueFingerprint(builder, "estimated_ally_healing", estimated_ally_healing);
        AppendNamedValueFingerprint(
            builder,
            "estimated_status_count",
            estimated_status_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_control_count",
            estimated_control_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_terrain_effect_count",
            estimated_terrain_effect_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_height_delta",
            estimated_height_delta
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_ground_control_cell_count",
            estimated_ground_control_cell_count
        );
        AppendNamedValueFingerprint(builder, "ground_control_score", ground_control_score);
        AppendNamedValueFingerprint(
            builder,
            "estimated_lethal_target_count",
            estimated_lethal_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_lethal_threat_target_count",
            estimated_lethal_threat_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_lethal_target_ids",
            estimated_lethal_target_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_lethal_threat_target_ids",
            estimated_lethal_threat_target_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_control_target_ids",
            estimated_control_target_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_control_threat_target_ids",
            estimated_control_threat_target_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_friendly_fire_target_count",
            estimated_friendly_fire_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_friendly_fire_damage",
            estimated_friendly_fire_damage
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_friendly_control_target_count",
            estimated_friendly_control_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_friendly_lethal_target_count",
            estimated_friendly_lethal_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_chain_target_count",
            estimated_chain_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_chain_enemy_target_count",
            estimated_chain_enemy_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_chain_ally_target_count",
            estimated_chain_ally_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "estimated_hit_rate_percent",
            estimated_hit_rate_percent
        );
        AppendNamedValueFingerprint(
            builder,
            "save_estimates_by_target_id",
            save_estimates_by_target_id
        );
        AppendNamedValueFingerprint(
            builder,
            "damage_estimates_by_target_id",
            damage_estimates_by_target_id
        );
        AppendNamedValueFingerprint(
            builder,
            "special_profile_preview_facts",
            special_profile_preview_facts
        );
        AppendNamedValueFingerprint(builder, "target_numeric_summary", target_numeric_summary);
        AppendNamedValueFingerprint(
            builder,
            "friendly_fire_numeric_summary",
            friendly_fire_numeric_summary
        );
        AppendNamedValueFingerprint(
            builder,
            "friendly_fire_reject_reason",
            friendly_fire_reject_reason
        );
        AppendNamedValueFingerprint(builder, "meteor_use_case", meteor_use_case);
        AppendNamedValueFingerprint(
            builder,
            "high_priority_target_ids",
            high_priority_target_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "high_priority_reasons",
            high_priority_reasons
        );
        AppendNamedValueFingerprint(
            builder,
            "low_value_penalty_reason",
            low_value_penalty_reason
        );
        AppendNamedValueFingerprint(
            builder,
            "attack_roll_modifier_breakdown",
            attack_roll_modifier_breakdown
        );
        AppendNamedValueFingerprint(builder, "hit_payoff_score", hit_payoff_score);
        AppendNamedValueFingerprint(builder, "target_priority_score", target_priority_score);
        AppendNamedValueFingerprint(
            builder,
            "friendly_fire_penalty_score",
            friendly_fire_penalty_score
        );
        AppendNamedValueFingerprint(builder, "path_step_hit_count", path_step_hit_count);
        AppendNamedValueFingerprint(
            builder,
            "path_step_unique_target_count",
            path_step_unique_target_count
        );
        AppendNamedValueFingerprint(
            builder,
            "path_step_hit_counts_by_unit_id",
            path_step_hit_counts_by_unit_id
        );
        AppendNamedValueFingerprint(builder, "path_step_payoff_score", path_step_payoff_score);
        AppendNamedValueFingerprint(builder, "ap_cost", ap_cost);
        AppendNamedValueFingerprint(builder, "mp_cost", mp_cost);
        AppendNamedValueFingerprint(builder, "stamina_cost", stamina_cost);
        AppendNamedValueFingerprint(builder, "aura_cost", aura_cost);
        AppendNamedValueFingerprint(builder, "cooldown_tu", cooldown_tu);
        AppendNamedValueFingerprint(builder, "resource_cost_score", resource_cost_score);
        AppendNamedValueFingerprint(builder, "move_cost", move_cost);
        AppendNamedValueFingerprint(
            builder,
            "position_objective_kind",
            position_objective_kind
        );
        AppendNamedValueFingerprint(builder, "desired_min_distance", desired_min_distance);
        AppendNamedValueFingerprint(builder, "desired_max_distance", desired_max_distance);
        AppendNamedValueFingerprint(builder, "position_anchor_coord", position_anchor_coord);
        AppendNamedValueFingerprint(
            builder,
            "distance_to_primary_coord",
            distance_to_primary_coord
        );
        AppendNamedValueFingerprint(
            builder,
            "position_current_distance",
            position_current_distance
        );
        AppendNamedValueFingerprint(
            builder,
            "position_safe_distance",
            position_safe_distance
        );
        AppendNamedValueFingerprint(
            builder,
            "position_objective_score",
            position_objective_score
        );
        AppendNamedValueFingerprint(
            builder,
            "has_post_action_threat_projection",
            has_post_action_threat_projection
        );
        AppendNamedValueFingerprint(builder, "projected_actor_coord", projected_actor_coord);
        AppendNamedValueFingerprint(
            builder,
            "pre_action_threat_unit_ids",
            pre_action_threat_unit_ids
        );
        AppendNamedValueFingerprint(builder, "pre_action_threat_count", pre_action_threat_count);
        AppendNamedValueFingerprint(
            builder,
            "pre_action_threat_expected_damage",
            pre_action_threat_expected_damage
        );
        AppendNamedValueFingerprint(
            builder,
            "pre_action_survival_margin",
            pre_action_survival_margin
        );
        AppendNamedValueFingerprint(
            builder,
            "pre_action_is_lethal_survival_risk",
            pre_action_is_lethal_survival_risk
        );
        AppendNamedValueFingerprint(
            builder,
            "post_action_remaining_threat_unit_ids",
            post_action_remaining_threat_unit_ids
        );
        AppendNamedValueFingerprint(
            builder,
            "post_action_remaining_threat_count",
            post_action_remaining_threat_count
        );
        AppendNamedValueFingerprint(
            builder,
            "post_action_remaining_threat_expected_damage",
            post_action_remaining_threat_expected_damage
        );
        AppendNamedValueFingerprint(
            builder,
            "post_action_survival_margin",
            post_action_survival_margin
        );
        AppendNamedValueFingerprint(
            builder,
            "post_action_is_lethal_survival_risk",
            post_action_is_lethal_survival_risk
        );
        AppendNamedValueFingerprint(builder, "total_score", total_score);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendNamedCommandFingerprint(
        System.Text.StringBuilder builder,
        string name,
        BattleCommand value
    )
    {
        AppendNamedValueFingerprint(
            builder,
            name,
            value == null
                ? null
                : new object[]
                {
                    AiCommandSummary.FromCommand(value),
                    value.equipment_operation,
                    value.equipment_slot_id,
                    value.equipment_item_id,
                    value.equipment_instance_id,
                    value.equipment_instance,
                    CopyCommandSlotIds(value.EquipmentOccupiedSlotIdsTyped),
                }
        );
    }

    private static List<StringName> CopyCommandSlotIds(
        IEnumerable<StringName> values
    )
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(ProgressionDataUtils.to_string_name(value));
        }
        return result;
    }

    private static void AppendNamedValueFingerprint(
        System.Text.StringBuilder builder,
        string name,
        object value
    )
    {
        builder.Append(name);
        builder.Append(':');
        AppendValueFingerprint(builder, value);
        builder.Append(';');
    }

    private static void AppendValueFingerprint(
        System.Text.StringBuilder builder,
        object value
    )
    {
        if (value == null)
        {
            AppendTextFingerprint(builder, "");
            return;
        }

        switch (value)
        {
            case string text:
                AppendTextFingerprint(builder, text);
                return;
            case StringName stringName:
                AppendTextFingerprint(builder, stringName.ToString());
                return;
            case bool boolean:
                AppendTextFingerprint(builder, boolean ? "true" : "false");
                return;
            case int intValue:
                AppendTextFingerprint(builder, intValue.ToString());
                return;
            case long longValue:
                AppendTextFingerprint(builder, longValue.ToString());
                return;
            case Vector2I vector:
                AppendTextFingerprint(builder, $"{vector.X},{vector.Y}");
                return;
            case System.Collections.IDictionary dictionary:
                AppendTypedDictionaryFingerprint(builder, dictionary);
                return;
            case System.Collections.IEnumerable enumerable:
                AppendEnumerableFingerprint(builder, enumerable);
                return;
        }

        System.Type type = value.GetType();
        if (type.IsEnum)
        {
            AppendTextFingerprint(builder, value.ToString());
            return;
        }
        if (type.IsPrimitive)
        {
            AppendTextFingerprint(builder, value.ToString());
            return;
        }

        AppendReflectedObjectFingerprint(builder, value, type);
    }

    private static void AppendTypedDictionaryFingerprint(
        System.Text.StringBuilder builder,
        System.Collections.IDictionary dictionary
    )
    {
        builder.Append("map{");
        var entries = new List<(string Key, string ValueFingerprint)>();
        foreach (System.Collections.DictionaryEntry entry in dictionary)
        {
            string key = entry.Key?.ToString() ?? "";
            var valueBuilder = new System.Text.StringBuilder();
            AppendValueFingerprint(valueBuilder, entry.Value);
            entries.Add((key, valueBuilder.ToString()));
        }
        entries.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        bool first = true;
        foreach ((string key, string valueFingerprint) in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append(key);
            builder.Append(':');
            builder.Append(valueFingerprint);
        }
        builder.Append('}');
    }

    private static void AppendEnumerableFingerprint(
        System.Text.StringBuilder builder,
        System.Collections.IEnumerable enumerable
    )
    {
        builder.Append('[');
        bool first = true;
        foreach (object item in enumerable)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            AppendValueFingerprint(builder, item);
        }
        builder.Append(']');
    }

    private static void AppendReflectedObjectFingerprint(
        System.Text.StringBuilder builder,
        object value,
        System.Type type
    )
    {
        builder.Append(type.FullName ?? type.Name);
        builder.Append('{');
        var entries = new List<(string Name, string ValueFingerprint)>();
        foreach (System.Reflection.PropertyInfo property in type.GetProperties(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        ))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }
            var valueBuilder = new System.Text.StringBuilder();
            AppendValueFingerprint(valueBuilder, property.GetValue(value));
            entries.Add((property.Name, valueBuilder.ToString()));
        }
        foreach (System.Reflection.FieldInfo field in type.GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        ))
        {
            var valueBuilder = new System.Text.StringBuilder();
            AppendValueFingerprint(valueBuilder, field.GetValue(value));
            entries.Add((field.Name, valueBuilder.ToString()));
        }
        entries.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        bool first = true;
        foreach ((string name, string valueFingerprint) in entries)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append(name);
            builder.Append(':');
            builder.Append(valueFingerprint);
        }
        builder.Append('}');
    }

    private static void AppendTextFingerprint(System.Text.StringBuilder builder, string value)
    {
        value ??= "";
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
    }

    private static Dictionary<string, object> ToTraceDictionary(
        BattleAiScoreRuntimeMetadata metadata
    )
    {
        return metadata?.ToTraceDictionary()
            ?? new Dictionary<string, object>(System.StringComparer.Ordinal);
    }

    private static Dictionary<string, object> ToTraceDictionary(
        BattleSpecialProfilePreviewFacts previewFacts
    )
    {
        return previewFacts?.ToTraceDictionary()
            ?? new Dictionary<string, object>(System.StringComparer.Ordinal);
    }

    private static List<StringName> CloneStringNameList(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (!string.IsNullOrEmpty(normalized.ToString()))
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static List<Vector2I> CloneVector2IList(IEnumerable<Vector2I> values)
    {
        var result = new List<Vector2I>();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static List<string> CloneStringList(IEnumerable<string> values)
    {
        var result = new List<string>();
        if (values == null)
        {
            return result;
        }
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static Dictionary<string, int> ToTraceStringNameIntDictionary(
        IEnumerable<KeyValuePair<StringName, int>> values
    )
    {
        var result = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, int value) in values)
        {
            string normalizedKey = key.ToString();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                result[normalizedKey] = value;
            }
        }
        return result;
    }

    private static Dictionary<string, object> ToTraceHighPriorityReasonDictionary(
        IEnumerable<KeyValuePair<StringName, List<string>>> values
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, List<string> reasons) in values)
        {
            string normalizedKey = key.ToString();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                result[normalizedKey] = CloneStringList(reasons);
            }
        }
        return result;
    }

    private static Dictionary<string, object> ToTraceSaveEstimateDictionary(
        IEnumerable<KeyValuePair<StringName, List<DamageSaveEstimate>>> values
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, List<DamageSaveEstimate> estimates) in values)
        {
            string normalizedKey = key.ToString();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                result[normalizedKey] = ToTraceEstimateList(estimates);
            }
        }
        return result;
    }

    private static Dictionary<string, object> ToTraceDamageEstimateDictionary(
        IEnumerable<KeyValuePair<StringName, List<DamageEstimateBreakdown>>> values
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (values == null)
        {
            return result;
        }
        foreach ((StringName key, List<DamageEstimateBreakdown> estimates) in values)
        {
            string normalizedKey = key.ToString();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                result[normalizedKey] = ToTraceEstimateList(estimates);
            }
        }
        return result;
    }

    private static List<object> ToTraceEstimateList(IEnumerable<DamageSaveEstimate> values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (DamageSaveEstimate value in values)
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }

    private static List<object> ToTraceEstimateList(IEnumerable<DamageEstimateBreakdown> values)
    {
        var result = new List<object>();
        if (values == null)
        {
            return result;
        }
        foreach (DamageEstimateBreakdown value in values)
        {
            if (value != null)
            {
                result.Add(value.ToTraceDictionary());
            }
        }
        return result;
    }

    private static List<object> ToTraceNumericSummaryList(
        IEnumerable<MeteorSwarmNumericSummary> summaries
    )
    {
        var result = new List<object>();
        if (summaries == null)
        {
            return result;
        }
        foreach (MeteorSwarmNumericSummary summary in summaries)
        {
            if (summary != null)
            {
                result.Add(summary.ToTraceDictionary());
            }
        }
        return result;
    }

    private static List<object> ToTraceAttackRollModifierBreakdownList(
        IEnumerable<BattleAttackRollModifierSpec> specs
    )
    {
        var result = new List<object>();
        foreach (
            BattleAttackRollModifierSpec spec
            in specs ?? System.Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (spec != null)
            {
                result.Add(spec.ToTraceDictionary());
            }
        }
        return result;
    }

    private static GArray ToStringArray(IEnumerable<string> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }
        return result;
    }
}
