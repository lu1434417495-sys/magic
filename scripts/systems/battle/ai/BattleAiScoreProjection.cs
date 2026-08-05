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
        BattleAiScoreProfileDefinition profile,
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

    internal static Dictionary<string, object> BuildProfilePlain(
        BattleAiScoreProfileDefinition profile
    ) =>
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
        result["layered_barrier_projection"] =
            input.layered_barrier_projection?.ToTraceDictionary()
            ?? new Dictionary<string, object>(StringComparer.Ordinal);
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
        result["path_step_hit_counts_by_unit_id"] = BuildStringNameIntMap(
            input.path_step_hit_counts_by_unit_id
        );
        return result;
    }

    private static Dictionary<string, object> ToPlainDictionary(
        BattleAiScoreProfileDefinition profile
    )
    {
        if (profile == null)
            return new Dictionary<string, object>(StringComparer.Ordinal);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["damage_weight"] = profile.DamageWeight,
            ["heal_weight"] = profile.HealWeight,
            ["status_weight"] = profile.StatusWeight,
            ["terrain_weight"] = profile.TerrainWeight,
            ["height_weight"] = profile.HeightWeight,
            ["lethal_target_weight"] = profile.LethalTargetWeight,
            ["lethal_threat_target_weight"] = profile.LethalThreatTargetWeight,
            ["target_count_weight"] = profile.TargetCountWeight,
            ["friendly_fire_damage_weight"] = profile.FriendlyFireDamageWeight,
            ["friendly_fire_target_weight"] = profile.FriendlyFireTargetWeight,
            ["friendly_control_target_weight"] = profile.FriendlyControlTargetWeight,
            ["friendly_lethal_target_weight"] = profile.FriendlyLethalTargetWeight,
            ["ap_cost_weight"] = profile.ApCostWeight,
            ["mp_cost_weight"] = profile.MpCostWeight,
            ["stamina_cost_weight"] = profile.StaminaCostWeight,
            ["aura_cost_weight"] = profile.AuraCostWeight,
            ["cooldown_weight"] = profile.CooldownWeight,
            ["delayed_resolution_cost_per_5_tu"] =
                profile.DelayedResolutionCostPer5Tu,
            ["movement_cost_weight"] = profile.MovementCostWeight,
            ["mp_reserve_floor_bp"] = profile.MpReserveFloorBp,
            ["mp_reserve_pressure_weight"] = profile.MpReservePressureWeight,
            ["mp_reserve_breach_penalty"] = profile.MpReserveBreachPenalty,
            ["stamina_reserve_floor_bp"] = profile.StaminaReserveFloorBp,
            ["stamina_reserve_pressure_weight"] = profile.StaminaReservePressureWeight,
            ["stamina_reserve_breach_penalty"] = profile.StaminaReserveBreachPenalty,
            ["aura_reserve_floor_bp"] = profile.AuraReserveFloorBp,
            ["aura_reserve_pressure_weight"] = profile.AuraReservePressureWeight,
            ["aura_reserve_breach_penalty"] = profile.AuraReserveBreachPenalty,
            ["resource_conservation_weight"] = profile.ResourceConservationWeight,
            ["position_base_score"] = profile.PositionBaseScore,
            ["position_distance_step"] = profile.PositionDistanceStep,
            ["position_undershoot_penalty"] = profile.PositionUndershootPenalty,
            ["position_overshoot_penalty"] = profile.PositionOvershootPenalty,
            ["survival_margin_gain_weight"] = profile.SurvivalMarginGainWeight,
            ["post_action_threat_damage_weight"] = profile.PostActionThreatDamageWeight,
            ["post_action_threat_count_weight"] = profile.PostActionThreatCountWeight,
            ["lethal_survival_risk_penalty"] = profile.LethalSurvivalRiskPenalty,
            ["incoming_threat_relief_weight"] = profile.IncomingThreatReliefWeight,
            ["low_hp_urgency_threshold_bp"] = profile.LowHpUrgencyThresholdBp,
            ["low_hp_urgency_weight"] = profile.LowHpUrgencyWeight,
            ["execute_target_hp_threshold_bp"] = profile.ExecuteTargetHpThresholdBp,
            ["execute_bonus_weight"] = profile.ExecuteBonusWeight,
            ["overkill_damage_penalty_weight"] = profile.OverkillDamagePenaltyWeight,
            ["role_threat_min_effective_range"] = profile.RoleThreatMinEffectiveRange,
            ["role_threat_distance_window"] = profile.RoleThreatDistanceWindow,
            ["role_threat_max_approach_distance"] = profile.RoleThreatMaxApproachDistance,
            ["role_threat_max_contact_range"] = profile.RoleThreatMaxContactRange,
            ["role_threat_in_range_score_step"] = profile.RoleThreatInRangeScoreStep,
            ["enemy_target_count_weight"] = profile.EnemyTargetCountWeight,
            ["chain_enemy_target_weight"] = profile.ChainEnemyTargetWeight,
            ["focus_fire_wounded_target_weight"] = profile.FocusFireWoundedTargetWeight,
            ["hit_rate_reliability_weight"] = profile.HitRateReliabilityWeight,
            ["save_reliable_damage_weight"] = profile.SaveReliableDamageWeight,
            ["shield_absorbed_weight"] = profile.ShieldAbsorbedWeight,
            ["control_weight"] = profile.ControlWeight,
            ["ground_control_weight"] = profile.GroundControlWeight,
            ["status_redundancy_penalty"] = profile.StatusRedundancyPenalty,
            ["position_objective_weight"] = profile.PositionObjectiveWeight,
            ["safe_distance_adherence_weight"] = profile.SafeDistanceAdherenceWeight,
            ["threat_healer_bias_basis_points"] = profile.ThreatHealerBiasBasisPoints,
            ["threat_control_bias_basis_points"] = profile.ThreatControlBiasBasisPoints,
            ["threat_ranged_bias_basis_points"] = profile.ThreatRangedBiasBasisPoints,
            ["threat_range_step_bias_basis_points"] = profile.ThreatRangeStepBiasBasisPoints,
            ["threat_multiplier_cap_basis_points"] = profile.ThreatMultiplierCapBasisPoints,
            ["meteor_high_priority_threat_multiplier_bp"] =
                profile.MeteorHighPriorityThreatMultiplierBp,
            ["meteor_high_priority_damage_hp_percent"] =
                profile.MeteorHighPriorityDamageHpPercent,
            ["meteor_high_priority_target_priority_score"] =
                profile.MeteorHighPriorityTargetPriorityScore,
            ["meteor_top_threat_rank"] = profile.MeteorTopThreatRank,
            ["meteor_friendly_fire_profile"] = profile.MeteorFriendlyFireProfile.ToString(),
            ["meteor_friendly_fire_soft_expected_hp_percent"] =
                profile.MeteorFriendlyFireSoftExpectedHpPercent,
            ["meteor_friendly_fire_hard_expected_hp_percent"] =
                profile.MeteorFriendlyFireHardExpectedHpPercent,
            ["meteor_friendly_fire_hard_worst_case_hp_percent"] =
                profile.MeteorFriendlyFireHardWorstCaseHpPercent,
            ["action_base_scores"] = CloneStringNameIntMap(profile.ActionBaseScores),
            ["default_bucket_priority"] = profile.DefaultBucketPriority,
            ["bucket_priorities"] = CloneStringNameIntMap(profile.BucketPriorities),
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

    private static Dictionary<string, object> BuildStringNameIntMap(
        IEnumerable<KeyValuePair<StringName, int>> values
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<StringName, int> entry
            in values ?? Array.Empty<KeyValuePair<StringName, int>>()
        )
        {
            string key = entry.Key.ToString();
            if (!string.IsNullOrEmpty(key))
                result[key] = entry.Value;
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
