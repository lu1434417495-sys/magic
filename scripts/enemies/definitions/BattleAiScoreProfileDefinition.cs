using System.Collections.Generic;
using Godot;

public sealed record BattleAiScoreProfileDefinition
{
    private static readonly IReadOnlyDictionary<StringName, int> DefaultActionScores =
        EnemyDefinitionCollections.FreezeDictionary(
            new Dictionary<StringName, int>
            {
                ["skill"] = 0,
                ["move"] = 20,
                ["retreat"] = 35,
                ["wait"] = -40,
            }
        );
    private static readonly IReadOnlyDictionary<StringName, int> DefaultPriorities =
        EnemyDefinitionCollections.FreezeDictionary(
            new Dictionary<StringName, int>
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
            }
        );

    internal static BattleAiScoreProfileDefinition Default { get; } = new();

    internal int DamageWeight { get; init; } = 10;
    internal int HealWeight { get; init; } = 8;
    internal int StatusWeight { get; init; } = 25;
    internal int TerrainWeight { get; init; } = 15;
    internal int HeightWeight { get; init; } = 12;
    internal int LethalTargetWeight { get; init; } = 500;
    internal int LethalThreatTargetWeight { get; init; } = 900;
    internal int TargetCountWeight { get; init; } = 40;
    internal int FriendlyFireDamageWeight { get; init; } = 35;
    internal int FriendlyFireTargetWeight { get; init; } = 250;
    internal int FriendlyControlTargetWeight { get; init; } = 350;
    internal int FriendlyLethalTargetWeight { get; init; } = 5000;
    internal int ApCostWeight { get; init; } = 25;
    internal int MpCostWeight { get; init; } = 15;
    internal int StaminaCostWeight { get; init; } = 2;
    internal int AuraCostWeight { get; init; } = 35;
    internal int CooldownWeight { get; init; } = 8;
    internal int DelayedResolutionCostPer5Tu { get; init; } = 1;
    internal int MovementCostWeight { get; init; } = 18;
    internal int MpReserveFloorBp { get; init; }
    internal int MpReservePressureWeight { get; init; }
    internal int MpReserveBreachPenalty { get; init; }
    internal int StaminaReserveFloorBp { get; init; }
    internal int StaminaReservePressureWeight { get; init; }
    internal int StaminaReserveBreachPenalty { get; init; }
    internal int AuraReserveFloorBp { get; init; }
    internal int AuraReservePressureWeight { get; init; }
    internal int AuraReserveBreachPenalty { get; init; }
    internal int ResourceConservationWeight { get; init; } = 100;
    internal int PositionBaseScore { get; init; } = 60;
    internal int PositionDistanceStep { get; init; } = 4;
    internal int PositionUndershootPenalty { get; init; } = 15;
    internal int PositionOvershootPenalty { get; init; } = 12;
    internal int SurvivalMarginGainWeight { get; init; }
    internal int PostActionThreatDamageWeight { get; init; }
    internal int PostActionThreatCountWeight { get; init; }
    internal int LethalSurvivalRiskPenalty { get; init; }
    internal int IncomingThreatReliefWeight { get; init; }
    internal int LowHpUrgencyThresholdBp { get; init; }
    internal int LowHpUrgencyWeight { get; init; }
    internal int ExecuteTargetHpThresholdBp { get; init; }
    internal int ExecuteBonusWeight { get; init; }
    internal int OverkillDamagePenaltyWeight { get; init; }
    internal int RoleThreatMinEffectiveRange { get; init; } = 4;
    internal int RoleThreatDistanceWindow { get; init; } = 4;
    internal int RoleThreatMaxApproachDistance { get; init; } = 7;
    internal int RoleThreatMaxContactRange { get; init; } = 2;
    internal int RoleThreatInRangeScoreStep { get; init; } = 10;
    internal int EnemyTargetCountWeight { get; init; }
    internal int ChainEnemyTargetWeight { get; init; }
    internal int FocusFireWoundedTargetWeight { get; init; }
    internal int HitRateReliabilityWeight { get; init; }
    internal int SaveReliableDamageWeight { get; init; }
    internal int ShieldAbsorbedWeight { get; init; } = 2;
    internal int ControlWeight { get; init; }
    internal int GroundControlWeight { get; init; }
    internal int StatusRedundancyPenalty { get; init; }
    internal int PositionObjectiveWeight { get; init; } = 100;
    internal int SafeDistanceAdherenceWeight { get; init; }
    internal int ThreatHealerBiasBasisPoints { get; init; } = 1500;
    internal int ThreatControlBiasBasisPoints { get; init; } = 500;
    internal int ThreatRangedBiasBasisPoints { get; init; } = 800;
    internal int ThreatRangeStepBiasBasisPoints { get; init; } = 200;
    internal int ThreatMultiplierCapBasisPoints { get; init; } = 15000;
    internal int MeteorHighPriorityThreatMultiplierBp { get; init; } = 11000;
    internal int MeteorHighPriorityDamageHpPercent { get; init; } = 35;
    internal int MeteorHighPriorityTargetPriorityScore { get; init; } = 250;
    internal int MeteorTopThreatRank { get; init; } = 1;
    internal StringName MeteorFriendlyFireProfile { get; init; } = "default";
    internal BattleAiMeteorFriendlyFireProfile MeteorFriendlyFireProfileKind =>
        BattleAiScoreProfile.ToMeteorFriendlyFireProfile(MeteorFriendlyFireProfile);
    internal int MeteorFriendlyFireSoftExpectedHpPercent { get; init; } = 10;
    internal int MeteorFriendlyFireHardExpectedHpPercent { get; init; } = 25;
    internal int MeteorFriendlyFireHardWorstCaseHpPercent { get; init; } = 50;
    internal IReadOnlyDictionary<StringName, int> ActionBaseScores { get; init; } =
        DefaultActionScores;
    internal int DefaultBucketPriority { get; init; }
    internal IReadOnlyDictionary<StringName, int> BucketPriorities { get; init; } =
        DefaultPriorities;

    internal int GetActionBaseScore(StringName actionKind)
    {
        if (ActionBaseScores.TryGetValue(actionKind, out int score))
            return score;
        return ActionBaseScores.TryGetValue("skill", out int fallback) ? fallback : 0;
    }

    internal int GetBucketPriority(StringName bucketId) =>
        BucketPriorities.TryGetValue(bucketId, out int priority)
            ? priority
            : DefaultBucketPriority;

    internal BattleAiScoreProfileDefinition WithActionBaseScores(
        IReadOnlyDictionary<StringName, int> values
    ) => this with { ActionBaseScores = EnemyDefinitionCollections.FreezeDictionary(values) };

    internal BattleAiScoreProfileDefinition WithBucketPriorities(
        IReadOnlyDictionary<StringName, int> values
    ) => this with { BucketPriorities = EnemyDefinitionCollections.FreezeDictionary(values) };

    internal bool TryWithScalar(
        string path,
        int value,
        out BattleAiScoreProfileDefinition patched
    )
    {
        patched = path switch
        {
            "damage_weight" => this with { DamageWeight = value },
            "heal_weight" => this with { HealWeight = value },
            "status_weight" => this with { StatusWeight = value },
            "terrain_weight" => this with { TerrainWeight = value },
            "height_weight" => this with { HeightWeight = value },
            "lethal_target_weight" => this with { LethalTargetWeight = value },
            "lethal_threat_target_weight" => this with { LethalThreatTargetWeight = value },
            "target_count_weight" => this with { TargetCountWeight = value },
            "friendly_fire_damage_weight" => this with { FriendlyFireDamageWeight = value },
            "friendly_fire_target_weight" => this with { FriendlyFireTargetWeight = value },
            "friendly_control_target_weight" => this with { FriendlyControlTargetWeight = value },
            "friendly_lethal_target_weight" => this with { FriendlyLethalTargetWeight = value },
            "ap_cost_weight" => this with { ApCostWeight = value },
            "mp_cost_weight" => this with { MpCostWeight = value },
            "stamina_cost_weight" => this with { StaminaCostWeight = value },
            "aura_cost_weight" => this with { AuraCostWeight = value },
            "cooldown_weight" => this with { CooldownWeight = value },
            "delayed_resolution_cost_per_5_tu" => this with
            {
                DelayedResolutionCostPer5Tu = value,
            },
            "movement_cost_weight" => this with { MovementCostWeight = value },
            "mp_reserve_floor_bp" => this with { MpReserveFloorBp = value },
            "mp_reserve_pressure_weight" => this with { MpReservePressureWeight = value },
            "mp_reserve_breach_penalty" => this with { MpReserveBreachPenalty = value },
            "stamina_reserve_floor_bp" => this with { StaminaReserveFloorBp = value },
            "stamina_reserve_pressure_weight" => this with { StaminaReservePressureWeight = value },
            "stamina_reserve_breach_penalty" => this with { StaminaReserveBreachPenalty = value },
            "aura_reserve_floor_bp" => this with { AuraReserveFloorBp = value },
            "aura_reserve_pressure_weight" => this with { AuraReservePressureWeight = value },
            "aura_reserve_breach_penalty" => this with { AuraReserveBreachPenalty = value },
            "resource_conservation_weight" => this with { ResourceConservationWeight = value },
            "position_base_score" => this with { PositionBaseScore = value },
            "position_distance_step" => this with { PositionDistanceStep = value },
            "position_undershoot_penalty" => this with { PositionUndershootPenalty = value },
            "position_overshoot_penalty" => this with { PositionOvershootPenalty = value },
            "survival_margin_gain_weight" => this with { SurvivalMarginGainWeight = value },
            "post_action_threat_damage_weight" => this with { PostActionThreatDamageWeight = value },
            "post_action_threat_count_weight" => this with { PostActionThreatCountWeight = value },
            "lethal_survival_risk_penalty" => this with { LethalSurvivalRiskPenalty = value },
            "incoming_threat_relief_weight" => this with { IncomingThreatReliefWeight = value },
            "low_hp_urgency_threshold_bp" => this with { LowHpUrgencyThresholdBp = value },
            "low_hp_urgency_weight" => this with { LowHpUrgencyWeight = value },
            "execute_target_hp_threshold_bp" => this with { ExecuteTargetHpThresholdBp = value },
            "execute_bonus_weight" => this with { ExecuteBonusWeight = value },
            "overkill_damage_penalty_weight" => this with { OverkillDamagePenaltyWeight = value },
            "role_threat_min_effective_range" => this with { RoleThreatMinEffectiveRange = value },
            "role_threat_distance_window" => this with { RoleThreatDistanceWindow = value },
            "role_threat_max_approach_distance" => this with { RoleThreatMaxApproachDistance = value },
            "role_threat_max_contact_range" => this with { RoleThreatMaxContactRange = value },
            "role_threat_in_range_score_step" => this with { RoleThreatInRangeScoreStep = value },
            "enemy_target_count_weight" => this with { EnemyTargetCountWeight = value },
            "chain_enemy_target_weight" => this with { ChainEnemyTargetWeight = value },
            "focus_fire_wounded_target_weight" => this with { FocusFireWoundedTargetWeight = value },
            "hit_rate_reliability_weight" => this with { HitRateReliabilityWeight = value },
            "save_reliable_damage_weight" => this with { SaveReliableDamageWeight = value },
            "shield_absorbed_weight" => this with { ShieldAbsorbedWeight = value },
            "control_weight" => this with { ControlWeight = value },
            "ground_control_weight" => this with { GroundControlWeight = value },
            "status_redundancy_penalty" => this with { StatusRedundancyPenalty = value },
            "position_objective_weight" => this with { PositionObjectiveWeight = value },
            "safe_distance_adherence_weight" => this with { SafeDistanceAdherenceWeight = value },
            "threat_healer_bias_basis_points" => this with { ThreatHealerBiasBasisPoints = value },
            "threat_control_bias_basis_points" => this with { ThreatControlBiasBasisPoints = value },
            "threat_ranged_bias_basis_points" => this with { ThreatRangedBiasBasisPoints = value },
            "threat_range_step_bias_basis_points" => this with { ThreatRangeStepBiasBasisPoints = value },
            "threat_multiplier_cap_basis_points" => this with { ThreatMultiplierCapBasisPoints = value },
            "meteor_high_priority_threat_multiplier_bp" => this with { MeteorHighPriorityThreatMultiplierBp = value },
            "meteor_high_priority_damage_hp_percent" => this with { MeteorHighPriorityDamageHpPercent = value },
            "meteor_high_priority_target_priority_score" => this with { MeteorHighPriorityTargetPriorityScore = value },
            "meteor_top_threat_rank" => this with { MeteorTopThreatRank = value },
            "meteor_friendly_fire_soft_expected_hp_percent" => this with { MeteorFriendlyFireSoftExpectedHpPercent = value },
            "meteor_friendly_fire_hard_expected_hp_percent" => this with { MeteorFriendlyFireHardExpectedHpPercent = value },
            "meteor_friendly_fire_hard_worst_case_hp_percent" => this with { MeteorFriendlyFireHardWorstCaseHpPercent = value },
            "default_bucket_priority" => this with { DefaultBucketPriority = value },
            _ => null,
        };
        if (patched != null)
            return true;
        patched = this;
        return false;
    }

    internal static BattleAiScoreProfileDefinition FromResource(BattleAiScoreProfile source)
    {
        if (source == null)
            return Default;
        return new BattleAiScoreProfileDefinition
        {
            DamageWeight = source.damage_weight,
            HealWeight = source.heal_weight,
            StatusWeight = source.status_weight,
            TerrainWeight = source.terrain_weight,
            HeightWeight = source.height_weight,
            LethalTargetWeight = source.lethal_target_weight,
            LethalThreatTargetWeight = source.lethal_threat_target_weight,
            TargetCountWeight = source.target_count_weight,
            FriendlyFireDamageWeight = source.friendly_fire_damage_weight,
            FriendlyFireTargetWeight = source.friendly_fire_target_weight,
            FriendlyControlTargetWeight = source.friendly_control_target_weight,
            FriendlyLethalTargetWeight = source.friendly_lethal_target_weight,
            ApCostWeight = source.ap_cost_weight,
            MpCostWeight = source.mp_cost_weight,
            StaminaCostWeight = source.stamina_cost_weight,
            AuraCostWeight = source.aura_cost_weight,
            CooldownWeight = source.cooldown_weight,
            DelayedResolutionCostPer5Tu = source.delayed_resolution_cost_per_5_tu,
            MovementCostWeight = source.movement_cost_weight,
            MpReserveFloorBp = source.mp_reserve_floor_bp,
            MpReservePressureWeight = source.mp_reserve_pressure_weight,
            MpReserveBreachPenalty = source.mp_reserve_breach_penalty,
            StaminaReserveFloorBp = source.stamina_reserve_floor_bp,
            StaminaReservePressureWeight = source.stamina_reserve_pressure_weight,
            StaminaReserveBreachPenalty = source.stamina_reserve_breach_penalty,
            AuraReserveFloorBp = source.aura_reserve_floor_bp,
            AuraReservePressureWeight = source.aura_reserve_pressure_weight,
            AuraReserveBreachPenalty = source.aura_reserve_breach_penalty,
            ResourceConservationWeight = source.resource_conservation_weight,
            PositionBaseScore = source.position_base_score,
            PositionDistanceStep = source.position_distance_step,
            PositionUndershootPenalty = source.position_undershoot_penalty,
            PositionOvershootPenalty = source.position_overshoot_penalty,
            SurvivalMarginGainWeight = source.survival_margin_gain_weight,
            PostActionThreatDamageWeight = source.post_action_threat_damage_weight,
            PostActionThreatCountWeight = source.post_action_threat_count_weight,
            LethalSurvivalRiskPenalty = source.lethal_survival_risk_penalty,
            IncomingThreatReliefWeight = source.incoming_threat_relief_weight,
            LowHpUrgencyThresholdBp = source.low_hp_urgency_threshold_bp,
            LowHpUrgencyWeight = source.low_hp_urgency_weight,
            ExecuteTargetHpThresholdBp = source.execute_target_hp_threshold_bp,
            ExecuteBonusWeight = source.execute_bonus_weight,
            OverkillDamagePenaltyWeight = source.overkill_damage_penalty_weight,
            RoleThreatMinEffectiveRange = source.role_threat_min_effective_range,
            RoleThreatDistanceWindow = source.role_threat_distance_window,
            RoleThreatMaxApproachDistance = source.role_threat_max_approach_distance,
            RoleThreatMaxContactRange = source.role_threat_max_contact_range,
            RoleThreatInRangeScoreStep = source.role_threat_in_range_score_step,
            EnemyTargetCountWeight = source.enemy_target_count_weight,
            ChainEnemyTargetWeight = source.chain_enemy_target_weight,
            FocusFireWoundedTargetWeight = source.focus_fire_wounded_target_weight,
            HitRateReliabilityWeight = source.hit_rate_reliability_weight,
            SaveReliableDamageWeight = source.save_reliable_damage_weight,
            ShieldAbsorbedWeight = source.shield_absorbed_weight,
            ControlWeight = source.control_weight,
            GroundControlWeight = source.ground_control_weight,
            StatusRedundancyPenalty = source.status_redundancy_penalty,
            PositionObjectiveWeight = source.position_objective_weight,
            SafeDistanceAdherenceWeight = source.safe_distance_adherence_weight,
            ThreatHealerBiasBasisPoints = source.threat_healer_bias_basis_points,
            ThreatControlBiasBasisPoints = source.threat_control_bias_basis_points,
            ThreatRangedBiasBasisPoints = source.threat_ranged_bias_basis_points,
            ThreatRangeStepBiasBasisPoints = source.threat_range_step_bias_basis_points,
            ThreatMultiplierCapBasisPoints = source.threat_multiplier_cap_basis_points,
            MeteorHighPriorityThreatMultiplierBp = source.meteor_high_priority_threat_multiplier_bp,
            MeteorHighPriorityDamageHpPercent = source.meteor_high_priority_damage_hp_percent,
            MeteorHighPriorityTargetPriorityScore = source.meteor_high_priority_target_priority_score,
            MeteorTopThreatRank = source.meteor_top_threat_rank,
            MeteorFriendlyFireProfile = source.meteor_friendly_fire_profile,
            MeteorFriendlyFireSoftExpectedHpPercent = source.meteor_friendly_fire_soft_expected_hp_percent,
            MeteorFriendlyFireHardExpectedHpPercent = source.meteor_friendly_fire_hard_expected_hp_percent,
            MeteorFriendlyFireHardWorstCaseHpPercent = source.meteor_friendly_fire_hard_worst_case_hp_percent,
            ActionBaseScores = EnemyDefinitionCollections.FreezeDictionary(source.ActionBaseScoresTyped),
            DefaultBucketPriority = source.default_bucket_priority,
            BucketPriorities = EnemyDefinitionCollections.FreezeDictionary(source.BucketPrioritiesTyped),
        };
    }
}
