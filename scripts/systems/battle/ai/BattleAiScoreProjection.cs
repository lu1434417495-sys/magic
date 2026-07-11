using System;
using System.Collections.Generic;
using Godot;
using DamageEstimateBreakdown = BattleAiScoreService.DamageEstimateBreakdown;
using DamageSaveEstimate = BattleAiScoreService.DamageSaveEstimate;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleAiScoreProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(BattleAiScoreInput input)
    {
        return TraceDictionaryProjection.BuildLease(
            ToPlainDictionary(input),
            "battle_ai_score",
            LifetimeDomain.Request,
            "BattleAiScoreProjection.BuildLease"
        );
    }

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleAiScoreInput input,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return TraceDictionaryProjection.WriteDictionary(
            lease,
            ToPlainDictionary(input),
            reason
        );
    }

    internal static GDictionary WriteProfile<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleAiScoreProfile profile,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return TraceDictionaryProjection.WriteDictionary(
            lease,
            ToPlainDictionary(profile),
            reason
        );
    }

    internal static Dictionary<string, object> BuildPlain(BattleAiScoreInput input) =>
        ToPlainDictionary(input);

    internal static Dictionary<string, object> BuildProfilePlain(BattleAiScoreProfile profile) =>
        ToPlainDictionary(profile);

    private static Dictionary<string, object> ToPlainDictionary(BattleAiScoreInput input)
    {
        if (input == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        Dictionary<string, object> result = input.ToTraceDictionary();
        result["runtime_action_metadata"] =
            input.runtime_action_metadata?.ToTraceDictionary()
            ?? new Dictionary<string, object>(StringComparer.Ordinal);
        result["save_estimates_by_target_id"] = BuildSaveEstimateMap(
            input.save_estimates_by_target_id
        );
        result["damage_estimates_by_target_id"] = BuildDamageEstimateMap(
            input.damage_estimates_by_target_id
        );
        result["special_profile_preview_facts"] = MeteorSwarmProjection.BuildPlain(
            input.special_profile_preview_facts
        );
        result["target_numeric_summary"] = MeteorSwarmProjection.BuildNumericSummaryListPlain(
            input.target_numeric_summary
        );
        result["friendly_fire_numeric_summary"] =
            MeteorSwarmProjection.BuildNumericSummaryListPlain(
                input.friendly_fire_numeric_summary
            );
        result["attack_roll_modifier_breakdown"] = BuildModifierList(
            input.attack_roll_modifier_breakdown
        );
        return result;
    }

    private static Dictionary<string, object> ToPlainDictionary(BattleAiScoreProfile profile)
    {
        if (profile == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["damage_weight"] = profile.damage_weight,
            ["heal_weight"] = profile.heal_weight,
            ["status_weight"] = profile.status_weight,
            ["terrain_weight"] = profile.terrain_weight,
            ["height_weight"] = profile.height_weight,
            ["lethal_target_weight"] = profile.lethal_target_weight,
            ["lethal_threat_target_weight"] = profile.lethal_threat_target_weight,
            ["target_count_weight"] = profile.target_count_weight,
            ["friendly_fire_damage_weight"] = profile.friendly_fire_damage_weight,
            ["friendly_fire_target_weight"] = profile.friendly_fire_target_weight,
            ["friendly_control_target_weight"] = profile.friendly_control_target_weight,
            ["friendly_lethal_target_weight"] = profile.friendly_lethal_target_weight,
            ["ap_cost_weight"] = profile.ap_cost_weight,
            ["mp_cost_weight"] = profile.mp_cost_weight,
            ["stamina_cost_weight"] = profile.stamina_cost_weight,
            ["aura_cost_weight"] = profile.aura_cost_weight,
            ["cooldown_weight"] = profile.cooldown_weight,
            ["movement_cost_weight"] = profile.movement_cost_weight,
            ["mp_reserve_floor_bp"] = profile.mp_reserve_floor_bp,
            ["mp_reserve_pressure_weight"] = profile.mp_reserve_pressure_weight,
            ["mp_reserve_breach_penalty"] = profile.mp_reserve_breach_penalty,
            ["stamina_reserve_floor_bp"] = profile.stamina_reserve_floor_bp,
            ["stamina_reserve_pressure_weight"] = profile.stamina_reserve_pressure_weight,
            ["stamina_reserve_breach_penalty"] = profile.stamina_reserve_breach_penalty,
            ["aura_reserve_floor_bp"] = profile.aura_reserve_floor_bp,
            ["aura_reserve_pressure_weight"] = profile.aura_reserve_pressure_weight,
            ["aura_reserve_breach_penalty"] = profile.aura_reserve_breach_penalty,
            ["resource_conservation_weight"] = profile.resource_conservation_weight,
            ["position_base_score"] = profile.position_base_score,
            ["position_distance_step"] = profile.position_distance_step,
            ["position_undershoot_penalty"] = profile.position_undershoot_penalty,
            ["position_overshoot_penalty"] = profile.position_overshoot_penalty,
            ["survival_margin_gain_weight"] = profile.survival_margin_gain_weight,
            ["post_action_threat_damage_weight"] = profile.post_action_threat_damage_weight,
            ["post_action_threat_count_weight"] = profile.post_action_threat_count_weight,
            ["lethal_survival_risk_penalty"] = profile.lethal_survival_risk_penalty,
            ["incoming_threat_relief_weight"] = profile.incoming_threat_relief_weight,
            ["low_hp_urgency_threshold_bp"] = profile.low_hp_urgency_threshold_bp,
            ["low_hp_urgency_weight"] = profile.low_hp_urgency_weight,
            ["execute_target_hp_threshold_bp"] = profile.execute_target_hp_threshold_bp,
            ["execute_bonus_weight"] = profile.execute_bonus_weight,
            ["overkill_damage_penalty_weight"] = profile.overkill_damage_penalty_weight,
            ["role_threat_min_effective_range"] = profile.role_threat_min_effective_range,
            ["role_threat_distance_window"] = profile.role_threat_distance_window,
            ["role_threat_max_approach_distance"] = profile.role_threat_max_approach_distance,
            ["role_threat_max_contact_range"] = profile.role_threat_max_contact_range,
            ["role_threat_in_range_score_step"] = profile.role_threat_in_range_score_step,
            ["enemy_target_count_weight"] = profile.enemy_target_count_weight,
            ["chain_enemy_target_weight"] = profile.chain_enemy_target_weight,
            ["focus_fire_wounded_target_weight"] = profile.focus_fire_wounded_target_weight,
            ["hit_rate_reliability_weight"] = profile.hit_rate_reliability_weight,
            ["save_reliable_damage_weight"] = profile.save_reliable_damage_weight,
            ["shield_absorbed_weight"] = profile.shield_absorbed_weight,
            ["control_weight"] = profile.control_weight,
            ["ground_control_weight"] = profile.ground_control_weight,
            ["status_redundancy_penalty"] = profile.status_redundancy_penalty,
            ["position_objective_weight"] = profile.position_objective_weight,
            ["safe_distance_adherence_weight"] = profile.safe_distance_adherence_weight,
            ["threat_healer_bias_basis_points"] = profile.threat_healer_bias_basis_points,
            ["threat_control_bias_basis_points"] = profile.threat_control_bias_basis_points,
            ["threat_ranged_bias_basis_points"] = profile.threat_ranged_bias_basis_points,
            ["threat_range_step_bias_basis_points"] = profile.threat_range_step_bias_basis_points,
            ["threat_multiplier_cap_basis_points"] = profile.threat_multiplier_cap_basis_points,
            ["meteor_high_priority_threat_multiplier_bp"] =
                profile.meteor_high_priority_threat_multiplier_bp,
            ["meteor_high_priority_damage_hp_percent"] =
                profile.meteor_high_priority_damage_hp_percent,
            ["meteor_high_priority_target_priority_score"] =
                profile.meteor_high_priority_target_priority_score,
            ["meteor_top_threat_rank"] = profile.meteor_top_threat_rank,
            ["meteor_friendly_fire_profile"] = profile.meteor_friendly_fire_profile.ToString(),
            ["meteor_friendly_fire_soft_expected_hp_percent"] =
                profile.meteor_friendly_fire_soft_expected_hp_percent,
            ["meteor_friendly_fire_hard_expected_hp_percent"] =
                profile.meteor_friendly_fire_hard_expected_hp_percent,
            ["meteor_friendly_fire_hard_worst_case_hp_percent"] =
                profile.meteor_friendly_fire_hard_worst_case_hp_percent,
            ["action_base_scores"] = CloneStringNameIntMap(profile.ActionBaseScoresTyped),
            ["default_bucket_priority"] = profile.default_bucket_priority,
            ["bucket_priorities"] = CloneStringNameIntMap(profile.BucketPrioritiesTyped),
        };
    }

    private static Dictionary<string, object> BuildSaveEstimateMap(
        IEnumerable<KeyValuePair<StringName, List<DamageSaveEstimate>>> values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<StringName, List<DamageSaveEstimate>> entry
            in values ?? Array.Empty<KeyValuePair<StringName, List<DamageSaveEstimate>>>()
        )
        {
            string key = entry.Key.ToString();
            if (string.IsNullOrEmpty(key))
                continue;
            var estimates = new List<object>();
            foreach (DamageSaveEstimate estimate in entry.Value ?? new List<DamageSaveEstimate>())
            {
                if (estimate != null)
                    estimates.Add(estimate.ToTraceDictionary());
            }
            result[key] = estimates;
        }
        return result;
    }

    private static Dictionary<string, object> BuildDamageEstimateMap(
        IEnumerable<KeyValuePair<StringName, List<DamageEstimateBreakdown>>> values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<StringName, List<DamageEstimateBreakdown>> entry
            in values ?? Array.Empty<KeyValuePair<StringName, List<DamageEstimateBreakdown>>>()
        )
        {
            string key = entry.Key.ToString();
            if (string.IsNullOrEmpty(key))
                continue;
            var estimates = new List<object>();
            foreach (
                DamageEstimateBreakdown estimate
                in entry.Value ?? new List<DamageEstimateBreakdown>()
            )
            {
                if (estimate != null)
                    estimates.Add(estimate.ToTraceDictionary());
            }
            result[key] = estimates;
        }
        return result;
    }

    private static List<object> BuildModifierList(
        IEnumerable<BattleAttackRollModifierSpec> values
    )
    {
        var result = new List<object>();
        foreach (
            BattleAttackRollModifierSpec value
            in values ?? Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (value != null)
                result.Add(value.ToTraceDictionary());
        }
        return result;
    }

    private static Dictionary<StringName, int> CloneStringNameIntMap(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        var result = new Dictionary<StringName, int>();
        if (values == null)
            return result;
        foreach ((StringName key, int value) in values)
            if (key != "")
                result[key] = value;
        return result;
    }
}
