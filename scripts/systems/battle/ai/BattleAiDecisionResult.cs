using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiDecisionResult
{
    private BattleAiDecisionResult(
        BattleAiDecision decision,
        BattleAiTurnTraceProjection turnTrace
    )
    {
        Decision = decision;
        TurnTrace = turnTrace;
    }

    internal BattleAiDecision Decision { get; }

    internal BattleAiTurnTraceProjection TurnTrace { get; }

    internal static BattleAiDecisionResult Capture(
        BattleAiContext context,
        BattleAiDecision decision,
        bool captureTrace
    )
    {
        try
        {
            BattleAiTurnTraceProjection trace =
                captureTrace && context != null
                    ? CloneTurnTrace(context.BuildTurnTraceTyped(decision))
                    : null;
            return new BattleAiDecisionResult(CloneDecision(decision), trace);
        }
        finally
        {
            decision?.ClearOwnedRuntimeReferences();
        }
    }

    private static BattleAiDecision CloneDecision(BattleAiDecision source)
    {
        if (source == null)
            return null;

        BattleAiScoreInput scoreInput = CloneScoreInput(source.score_input);
        BattleAiScoreInput skillScoreInput = ReferenceEquals(
            source.skill_score_input,
            source.score_input
        )
            ? scoreInput
            : CloneScoreInput(source.skill_score_input);
        var result = new BattleAiDecision
        {
            command = CloneCommand(source.command),
            brain_id = source.brain_id,
            state_id = source.state_id,
            action_id = source.action_id,
            reason_text = source.reason_text ?? "",
            score_bucket_id = source.score_bucket_id,
            action_trace_id = source.action_trace_id,
            score_input = scoreInput,
            skill_score_input = skillScoreInput,
            Transition = CloneTransition(source.Transition),
        };
        result.StatePatch = BattleAiDecisionCommitter.BuildTypedStatePatch(result);
        return result;
    }

    private static BattleCommand CloneCommand(BattleCommand source)
    {
        if (source == null)
            return null;
        var result = new BattleCommand
        {
            command_type = source.command_type,
            unit_id = source.unit_id,
            skill_entry_id = source.skill_entry_id,
            skill_id = source.skill_id,
            skill_variant_id = source.skill_variant_id,
            windup_tier = source.windup_tier,
            target_unit_id = source.target_unit_id,
            source_retreat_direction = source.source_retreat_direction,
            target_coord = source.target_coord,
            equipment_operation = source.equipment_operation,
            equipment_slot_id = source.equipment_slot_id,
            equipment_item_id = source.equipment_item_id,
            equipment_instance_id = source.equipment_instance_id,
            equipment_instance = source.equipment_instance?.DuplicateState(),
        };
        result.SetTargetUnitIds(source.TargetUnitIdsTyped);
        result.SetTargetCoords(source.TargetCoordsTyped);
        result.SetEquipmentOccupiedSlotIds(source.EquipmentOccupiedSlotIdsTyped);
        return result;
    }

    private static BattleAiStateResolver.TransitionResult CloneTransition(
        BattleAiStateResolver.TransitionResult source
    )
    {
        if (source == null)
            return null;
        return new BattleAiStateResolver.TransitionResult(
            source.PreviousStateId,
            source.StateId,
            source.RuleId,
            source.Reason,
            new List<BattleAiStateResolver.TransitionConditionTrace>(source.MatchedConditions)
        );
    }

    private static BattleAiTurnTraceProjection CloneTurnTrace(
        BattleAiTurnTraceProjection source
    )
    {
        if (source == null)
            return null;
        return source.Clone();
    }

    internal static IReadOnlyList<AiActionTrace> CloneActionTraces(
        IReadOnlyList<AiActionTrace> source
    )
    {
        var result = new List<AiActionTrace>();
        foreach (AiActionTrace trace in source ?? Array.Empty<AiActionTrace>())
            result.Add(CloneActionTrace(trace));
        return result;
    }

    private static AiActionTrace CloneActionTrace(AiActionTrace source)
    {
        if (source == null)
            return new AiActionTrace();
        var result = new AiActionTrace(
            source.TraceId,
            source.ActionId,
            source.ScoreBucketId,
            RuntimePlainPayload.CloneDictionary(source.Metadata)
        )
        {
            EvaluationCount = source.EvaluationCount,
            BlockedCount = source.BlockedCount,
            PreviewRejectCount = source.PreviewRejectCount,
            CandidateCount = source.CandidateCount,
            Chosen = source.Chosen,
            BestReasonText = source.BestReasonText ?? "",
            BestCommand = source.BestCommand?.Clone() ?? new AiCommandSummary(),
            ChosenReasonText = source.ChosenReasonText ?? "",
            ChosenCommand = source.ChosenCommand?.Clone() ?? new AiCommandSummary(),
            GateRejected = source.GateRejected,
            GateRejectionReason = source.GateRejectionReason ?? "",
        };
        CopyMap(source.BlockReasons, result.BlockReasons);
        foreach (AiCandidateSummary candidate in source.TopCandidates)
            result.TopCandidates.Add(CloneCandidate(candidate));
        CopyPlainMap(source.BestScoreInput, result.BestScoreInput);
        CopyPlainMap(source.ChosenScoreInput, result.ChosenScoreInput);
        CopyMap(source.CandidateTraceCounters, result.CandidateTraceCounters);
        return result;
    }

    private static AiCandidateSummary CloneCandidate(AiCandidateSummary source)
    {
        if (source == null)
            return new AiCandidateSummary();
        return new AiCandidateSummary(
            source.Label,
            source.Command?.Clone() ?? new AiCommandSummary(),
            source.TotalScore,
            RuntimePlainPayload.CloneDictionary(source.ScoreInput),
            RuntimePlainPayload.CloneDictionary(source.ExtraFields)
        );
    }

    internal static BattleAiScoreInput CloneScoreInput(BattleAiScoreInput source)
    {
        if (source == null)
            return null;
        return new BattleAiScoreInput
        {
            command = CloneCommand(source.command),
            preview = null,
            skill_id = source.skill_id,
            action_kind = source.action_kind,
            action_label = source.action_label ?? "",
            action_intent = source.action_intent,
            score_bucket_id = source.score_bucket_id,
            score_bucket_priority = source.score_bucket_priority,
            runtime_action_metadata = source.runtime_action_metadata?.Clone() ?? new(),
            primary_coord = source.primary_coord,
            target_unit_ids = CloneList(source.target_unit_ids),
            target_coords = CloneList(source.target_coords),
            target_count = source.target_count,
            random_chain_candidate_unit_ids = CloneList(
                source.random_chain_candidate_unit_ids
            ),
            random_chain_candidate_pool_count = source.random_chain_candidate_pool_count,
            random_chain_max_hits_per_target = source.random_chain_max_hits_per_target,
            random_chain_max_attempt_count = source.random_chain_max_attempt_count,
            random_chain_selection_policy = source.random_chain_selection_policy,
            random_chain_pool_refresh_policy = source.random_chain_pool_refresh_policy,
            random_chain_score_estimate_policy = source.random_chain_score_estimate_policy,
            effective_target_count = source.effective_target_count,
            enemy_target_count = source.enemy_target_count,
            ally_target_count = source.ally_target_count,
            estimated_damage = source.estimated_damage,
            estimated_post_save_damage = source.estimated_post_save_damage,
            estimated_shield_absorbed = source.estimated_shield_absorbed,
            estimated_healing = source.estimated_healing,
            estimated_enemy_damage = source.estimated_enemy_damage,
            estimated_ally_damage = source.estimated_ally_damage,
            estimated_enemy_healing = source.estimated_enemy_healing,
            estimated_ally_healing = source.estimated_ally_healing,
            estimated_status_count = source.estimated_status_count,
            estimated_control_count = source.estimated_control_count,
            estimated_taunt_ally_damage_relief =
                source.estimated_taunt_ally_damage_relief,
            estimated_terrain_effect_count = source.estimated_terrain_effect_count,
            estimated_height_delta = source.estimated_height_delta,
            estimated_ground_control_cell_count = source.estimated_ground_control_cell_count,
            ground_control_score = source.ground_control_score,
            estimated_lethal_target_count = source.estimated_lethal_target_count,
            estimated_lethal_threat_target_count = source.estimated_lethal_threat_target_count,
            estimated_lethal_target_ids = CloneList(source.estimated_lethal_target_ids),
            estimated_lethal_threat_target_ids = CloneList(
                source.estimated_lethal_threat_target_ids
            ),
            execute_kill_probability_basis_points =
                source.execute_kill_probability_basis_points,
            execute_soul_fracture_applied = source.execute_soul_fracture_applied,
            estimated_control_target_ids = CloneList(source.estimated_control_target_ids),
            estimated_control_threat_target_ids = CloneList(
                source.estimated_control_threat_target_ids
            ),
            estimated_friendly_fire_target_count = source.estimated_friendly_fire_target_count,
            estimated_friendly_fire_damage = source.estimated_friendly_fire_damage,
            estimated_friendly_control_target_count =
                source.estimated_friendly_control_target_count,
            estimated_friendly_lethal_target_count =
                source.estimated_friendly_lethal_target_count,
            estimated_chain_target_count = source.estimated_chain_target_count,
            estimated_chain_enemy_target_count = source.estimated_chain_enemy_target_count,
            estimated_chain_ally_target_count = source.estimated_chain_ally_target_count,
            estimated_hit_rate_percent = source.estimated_hit_rate_percent,
            save_estimates_by_target_id = CloneListMap(
                source.save_estimates_by_target_id,
                value => value?.Clone()
            ),
            damage_estimates_by_target_id = CloneListMap(
                source.damage_estimates_by_target_id,
                value => value?.Clone()
            ),
            special_profile_preview_facts = CloneSpecialProfileFacts(
                source.special_profile_preview_facts
            ),
            layered_barrier_projection = source.layered_barrier_projection?.Clone(),
            target_numeric_summary = CloneNumericSummaries(source.target_numeric_summary),
            friendly_fire_numeric_summary = CloneNumericSummaries(
                source.friendly_fire_numeric_summary
            ),
            friendly_fire_reject_reason = source.friendly_fire_reject_reason ?? "",
            meteor_use_case = source.meteor_use_case,
            high_priority_target_ids = CloneList(source.high_priority_target_ids),
            high_priority_reasons = CloneListMap(
                source.high_priority_reasons,
                value => value ?? ""
            ),
            low_value_penalty_reason = source.low_value_penalty_reason ?? "",
            attack_roll_modifier_breakdown = CloneAttackModifiers(
                source.attack_roll_modifier_breakdown
            ),
            hit_payoff_score = source.hit_payoff_score,
            target_priority_score = source.target_priority_score,
            friendly_fire_penalty_score = source.friendly_fire_penalty_score,
            path_step_hit_count = source.path_step_hit_count,
            path_step_unique_target_count = source.path_step_unique_target_count,
            path_step_hit_counts_by_unit_id = new Dictionary<StringName, int>(
                source.path_step_hit_counts_by_unit_id
                    ?? new Dictionary<StringName, int>()
            ),
            path_step_payoff_score = source.path_step_payoff_score,
            ap_cost = source.ap_cost,
            mp_cost = source.mp_cost,
            stamina_cost = source.stamina_cost,
            aura_cost = source.aura_cost,
            cooldown_tu = source.cooldown_tu,
            resource_cost_score = source.resource_cost_score,
            delayed_resolution_tu = source.delayed_resolution_tu,
            delayed_resolution_score = source.delayed_resolution_score,
            move_cost = source.move_cost,
            position_objective_kind = source.position_objective_kind,
            desired_min_distance = source.desired_min_distance,
            desired_max_distance = source.desired_max_distance,
            position_anchor_coord = source.position_anchor_coord,
            distance_to_primary_coord = source.distance_to_primary_coord,
            position_current_distance = source.position_current_distance,
            position_safe_distance = source.position_safe_distance,
            position_objective_score = source.position_objective_score,
            has_post_action_threat_projection = source.has_post_action_threat_projection,
            projected_actor_coord = source.projected_actor_coord,
            pre_action_threat_unit_ids = CloneList(source.pre_action_threat_unit_ids),
            pre_action_threat_count = source.pre_action_threat_count,
            pre_action_threat_expected_damage = source.pre_action_threat_expected_damage,
            pre_action_survival_margin = source.pre_action_survival_margin,
            pre_action_is_lethal_survival_risk = source.pre_action_is_lethal_survival_risk,
            post_action_remaining_threat_unit_ids = CloneList(
                source.post_action_remaining_threat_unit_ids
            ),
            post_action_remaining_threat_count = source.post_action_remaining_threat_count,
            post_action_remaining_threat_expected_damage =
                source.post_action_remaining_threat_expected_damage,
            post_action_survival_margin = source.post_action_survival_margin,
            post_action_is_lethal_survival_risk = source.post_action_is_lethal_survival_risk,
            total_score = source.total_score,
        };
    }

    private static BattleSpecialProfilePreviewFacts CloneSpecialProfileFacts(
        BattleSpecialProfilePreviewFacts source
    )
    {
        if (source == null)
            return null;
        BattleSpecialProfilePreviewFacts result;
        if (source is MeteorSwarmPreviewFacts meteorSource)
        {
            result = new MeteorSwarmPreviewFacts
            {
                impact_count = meteorSource.impact_count,
                expected_target_count = meteorSource.expected_target_count,
                expected_terrain_effect_count = meteorSource.expected_terrain_effect_count,
                friendly_fire_risk_percent = meteorSource.friendly_fire_risk_percent,
                component_preview = CloneComponentFacts(meteorSource.component_preview),
                target_numeric_summaries = CloneNumericSummaries(
                    meteorSource.target_numeric_summaries
                ),
                friendly_fire_numeric_summaries = CloneNumericSummaries(
                    meteorSource.friendly_fire_numeric_summaries
                ),
            };
        }
        else
        {
            result = new BattleSpecialProfilePreviewFacts();
        }
        result.profile_id = source.profile_id;
        result.skill_id = source.skill_id;
        result.preview_fact_id = source.preview_fact_id;
        result.nominal_plan_signature = source.nominal_plan_signature ?? "";
        result.final_plan_signature = source.final_plan_signature ?? "";
        result.resolved_anchor_coord = source.resolved_anchor_coord;
        result.target_unit_ids = CloneList(source.target_unit_ids);
        result.target_coords = CloneList(source.target_coords);
        result.terrain_summary = CloneTerrainSummary(source.terrain_summary);
        result.friendly_fire_numeric_summary = CloneNumericSummaries(
            source.friendly_fire_numeric_summary
        );
        result.attack_roll_modifier_breakdown = CloneAttackModifiers(
            source.attack_roll_modifier_breakdown
        );
        return result;
    }

    private static List<MeteorSwarmComponentFact> CloneComponentFacts(
        IEnumerable<MeteorSwarmComponentFact> source
    )
    {
        var result = new List<MeteorSwarmComponentFact>();
        foreach (MeteorSwarmComponentFact value in source ?? Array.Empty<MeteorSwarmComponentFact>())
        {
            if (value == null)
                continue;
            result.Add(
                new MeteorSwarmComponentFact
                {
                    component_id = value.component_id,
                    role_label = value.role_label,
                    damage_tag = value.damage_tag,
                    base_power = value.base_power,
                    dice_count = value.dice_count,
                    dice_sides = value.dice_sides,
                    damage_scale = value.damage_scale,
                    save_profile_id = value.save_profile_id,
                    can_crit = value.can_crit,
                    mastery_weight = value.mastery_weight,
                    ring_min = value.ring_min,
                    ring_max = value.ring_max,
                    distance_from_anchor = value.distance_from_anchor,
                    damage = value.damage,
                    healing = value.healing,
                }
            );
        }
        return result;
    }

    private static MeteorSwarmTerrainSummaryFact CloneTerrainSummary(
        MeteorSwarmTerrainSummaryFact source
    ) =>
        source == null
            ? new MeteorSwarmTerrainSummaryFact()
            : new MeteorSwarmTerrainSummaryFact
            {
                coverage_shape_id = source.coverage_shape_id,
                radius = source.radius,
                affected_coord_count = source.affected_coord_count,
                terrain_effect_count = source.terrain_effect_count,
                crater_count = source.crater_count,
                rubble_count = source.rubble_count,
                dust_count = source.dust_count,
            };

    private static List<MeteorSwarmNumericSummary> CloneNumericSummaries(
        IEnumerable<MeteorSwarmNumericSummary> source
    )
    {
        var result = new List<MeteorSwarmNumericSummary>();
        foreach (MeteorSwarmNumericSummary value in source ?? Array.Empty<MeteorSwarmNumericSummary>())
        {
            if (value != null)
                result.Add(CloneNumericSummary(value));
        }
        return result;
    }

    private static MeteorSwarmNumericSummary CloneNumericSummary(
        MeteorSwarmNumericSummary source
    )
    {
        var components = new List<MeteorSwarmComponentBreakdownEntry>();
        foreach (
            MeteorSwarmComponentBreakdownEntry component
            in source.ComponentBreakdown ?? new List<MeteorSwarmComponentBreakdownEntry>()
        )
        {
            if (component != null)
                components.Add(CloneComponentBreakdown(component));
        }
        return new MeteorSwarmNumericSummary
        {
            CandidateAnchorCoord = source.CandidateAnchorCoord,
            TargetUnitId = source.TargetUnitId,
            AllyUnitId = source.AllyUnitId,
            TargetFactionId = source.TargetFactionId,
            IsAlly = source.IsAlly,
            DistanceFromAnchor = source.DistanceFromAnchor,
            ComponentExpectedDamage = source.ComponentExpectedDamage,
            ComponentWorstCaseDamage = source.ComponentWorstCaseDamage,
            ComponentBreakdown = components,
            LethalProbabilityPercent = source.LethalProbabilityPercent,
            SaveProfileIds = CloneList(source.SaveProfileIds),
            ResistanceTiersByDamageTag = new Dictionary<StringName, StringName>(
                source.ResistanceTiersByDamageTag
                    ?? new Dictionary<StringName, StringName>()
            ),
            ShieldHp = source.ShieldHp,
            GuardBlockEstimate = source.GuardBlockEstimate,
            StatusEffectIds = CloneList(source.StatusEffectIds),
            ApPenalty = source.ApPenalty,
            HostileTerrain = CloneHostileTerrain(source.HostileTerrain),
            ExpectedDamageHpPercent = source.ExpectedDamageHpPercent,
            WorstCaseDamageHpPercent = source.WorstCaseDamageHpPercent,
            HardReject = source.HardReject,
            SoftPenalty = source.SoftPenalty,
        };
    }

    private static MeteorSwarmComponentBreakdownEntry CloneComponentBreakdown(
        MeteorSwarmComponentBreakdownEntry source
    ) =>
        new()
        {
            ComponentId = source.ComponentId,
            RoleLabel = source.RoleLabel,
            DamageTag = source.DamageTag,
            ExpectedDamage = source.ExpectedDamage,
            WorstCaseDamage = source.WorstCaseDamage,
            PostSaveExpectedDamage = source.PostSaveExpectedDamage,
            PostSaveWorstCaseDamage = source.PostSaveWorstCaseDamage,
            PreSaveExpectedDamage = source.PreSaveExpectedDamage,
            PreSaveWorstCaseDamage = source.PreSaveWorstCaseDamage,
            ResistanceTier = source.ResistanceTier,
            SaveProfileId = source.SaveProfileId ?? "",
            SaveEstimate = ClonePreviewSaveEstimate(source.SaveEstimate),
            WorstSaveEstimate = ClonePreviewSaveEstimate(source.WorstSaveEstimate),
            HalfSourceLabels = CloneList(source.HalfSourceLabels),
            DoubleSourceLabels = CloneList(source.DoubleSourceLabels),
            ImmuneSourceLabels = CloneList(source.ImmuneSourceLabels),
            FixedMitigationSourceLabels = CloneList(source.FixedMitigationSourceLabels),
            ShieldAbsorbedEstimate = source.ShieldAbsorbedEstimate,
            ShieldAbsorbedWorst = source.ShieldAbsorbedWorst,
        };

    private static BattleDamagePreviewSaveEstimate ClonePreviewSaveEstimate(
        BattleDamagePreviewSaveEstimate source
    )
    {
        if (source == null)
            return BattleDamagePreviewSaveEstimate.None(0);
        return BattleDamagePreviewSaveEstimate.Create(
            source.HasSave,
            source.DamageBeforeSave,
            source.DamageAfterSave,
            source.DamageAfterSaveEstimate,
            source.DamageAfterSaveWorst,
            source.DamageOnSaveFailure,
            source.DamageOnSaveSuccess,
            source.SavePartialOnSuccess,
            source.SaveSuccessProbabilityBasisPoints,
            source.SaveSuccessRatePercent,
            source.SaveFailureProbabilityBasisPoints,
            source.Dc,
            source.Ability,
            source.SaveTag,
            source.AdvantageState,
            source.AbilityValue,
            source.AbilityModifier,
            source.Bonus,
            source.Immune,
            source.Sources != null
                ? new List<BattleSaveSource>(source.Sources)
                : Array.Empty<BattleSaveSource>()
        );
    }

    private static MeteorSwarmHostileTerrainConsequence CloneHostileTerrain(
        MeteorSwarmHostileTerrainConsequence source
    ) =>
        source == null
            ? new MeteorSwarmHostileTerrainConsequence()
            : new MeteorSwarmHostileTerrainConsequence
            {
                MoveCostDelta = source.MoveCostDelta,
                CreatesDust = source.CreatesDust,
                CreatesCrater = source.CreatesCrater,
                CreatesRubble = source.CreatesRubble,
            };

    private static List<BattleAttackRollModifierSpec> CloneAttackModifiers(
        IEnumerable<BattleAttackRollModifierSpec> source
    )
    {
        var result = new List<BattleAttackRollModifierSpec>();
        foreach (
            BattleAttackRollModifierSpec value
            in source ?? Array.Empty<BattleAttackRollModifierSpec>()
        )
        {
            if (value != null)
                result.Add(value.Clone());
        }
        return result;
    }

    private static List<T> CloneList<T>(IEnumerable<T> source) =>
        source != null ? new List<T>(source) : new List<T>();

    private static Dictionary<TKey, List<TValue>> CloneListMap<TKey, TValue>(
        IReadOnlyDictionary<TKey, List<TValue>> source,
        Func<TValue, TValue> clone
    )
    {
        var result = new Dictionary<TKey, List<TValue>>();
        if (source == null)
            return result;
        foreach ((TKey key, List<TValue> values) in source)
        {
            var copied = new List<TValue>();
            foreach (TValue value in values ?? new List<TValue>())
                copied.Add(clone != null ? clone(value) : value);
            result[key] = copied;
        }
        return result;
    }

    private static void CopyMap<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        Dictionary<TKey, TValue> target
    )
    {
        if (source == null || target == null)
            return;
        foreach ((TKey key, TValue value) in source)
            target[key] = value;
    }

    private static void CopyPlainMap(
        IReadOnlyDictionary<string, object> source,
        Dictionary<string, object> target
    )
    {
        if (source == null || target == null)
            return;
        foreach ((string key, object value) in RuntimePlainPayload.CloneDictionary(source))
            target[key] = value;
    }
}

internal readonly struct BattleAiTraceSpan : IDisposable
{
    private readonly StringName _name;
    private readonly bool _entered;

    internal BattleAiTraceSpan(StringName name)
    {
        _name = name;
        _entered = false;
        AiTraceRecorder.Enter(_name);
        _entered = true;
    }

    public void Dispose()
    {
        if (_entered)
            AiTraceRecorder.Exit(_name);
    }
}
