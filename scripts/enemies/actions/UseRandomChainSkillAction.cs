using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UseRandomChainSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = -1;

    [Export]
    public int desired_max_distance { get; set; } = -1;

    [Export]
    public StringName distance_reference { get; set; } =
        EnemyAiDistanceReferences.ToStringName(EnemyAiDistanceReference.CandidatePool);
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    [Export]
    public int minimum_candidate_count { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        if (!_has_explicit_distance_contract())
            return null;
        var actionTrace = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "random_chain_skill" },
                { "target_selection_mode", "random_chain" },
                { "target_selector", (string)target_selector },
                { "distance_reference", (string)distance_reference },
                { "desired_min_distance", desired_min_distance },
                { "desired_max_distance", desired_max_distance },
                { "selection_policy", "random_from_living_pool" },
                { "pool_refresh_policy", "before_each_attempt" },
                { "score_estimate_policy", "expected_value" },
                { "minimum_candidate_count", minimum_candidate_count },
            }
        );
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(actionTrace, "skill_considered_count", 1);
            var skillDef = _get_skill_def(context, sid);
            if (skillDef?.combat_profile == null || !_is_random_chain_skill(skillDef))
            {
                _trace_add_block_reason(
                    actionTrace,
                    skillDef == null ? "missing_skill_def" : "non_random_chain_skill"
                );
                continue;
            }
            var blockReason = _get_skill_cast_block_reason(context, skillDef);
            if (blockReason.Length > 0)
            {
                _trace_add_block_reason(actionTrace, blockReason);
                continue;
            }
            foreach (var cv in _get_random_chain_cast_variants(context, skillDef))
            {
                _trace_count_increment(actionTrace, "evaluation_count", 1);
                var command = _build_random_chain_skill_command(context, sid, cv);
                BattlePreview preview = _build_fast_random_chain_skill_preview(
                    context,
                    skillDef,
                    cv,
                    command
                );
                if (preview?.allowed != true)
                {
                    _trace_count_increment(actionTrace, "preview_reject_count", 1);
                    continue;
                }
                var candidateUnits = _resolve_candidate_units(context, preview, skillDef);
                if (candidateUnits.Count == 0)
                {
                    _trace_add_block_reason(actionTrace, "no_random_chain_candidates");
                    continue;
                }
                if (candidateUnits.Count < Mathf.Max(minimum_candidate_count, 1))
                {
                    _trace_add_block_reason(
                        actionTrace,
                        "minimum_random_chain_candidate_count"
                    );
                    continue;
                }
                RandomChainScoreMetadata scoreMetadata = BuildRandomChainScoreMetadata(
                    context,
                    candidateUnits,
                    skillDef,
                    _format_skill_variant_label(skillDef, cv)
                );
                scoreMetadata.ApplyToTrace(actionTrace);
                var scoreInput = _build_typed_skill_score_input(
                    context,
                    skillDef,
                    command,
                    preview,
                    _collect_random_chain_effect_defs(skillDef, cv),
                    scoreMetadata.ToScoreMetadata()
                );
                var ctxUnitState = context.unit_state;
                if (scoreInput == null)
                {
                    if (fallbackDecision == null)
                        fallbackDecision = _create_decision(
                            command,
                            $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {scoreMetadata.CandidatePoolUnitIds.Count} 个单位。"
                        );
                    _trace_offer_candidate(
                        actionTrace,
                        _build_candidate_summary(
                            _format_skill_variant_label(skillDef, cv),
                            command,
                            null,
                            scoreMetadata.ToCandidateSummaryMetadata(sid)
                        )
                    );
                    continue;
                }
                _trace_offer_candidate(
                    actionTrace,
                    _build_candidate_summary(
                        _format_skill_variant_label(skillDef, cv),
                        command,
                        scoreInput,
                        scoreMetadata.ToCandidateSummaryMetadata(sid)
                    )
                );
                if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                    continue;
                bestScoreInput = scoreInput;
                bestDecision = _create_scored_decision(
                    command,
                    scoreInput,
                    $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {scoreMetadata.CandidatePoolUnitIds.Count} 个单位（评分 {_score_total(scoreInput)}）。"
                );
            }
        }
        var resolved = bestDecision ?? fallbackDecision;
        _finalize_action_trace(context, actionTrace, resolved);
        return resolved;
    }

    private static bool _is_random_chain_skill(SkillDef sd) =>
        sd?.combat_profile != null
        && (sd.combat_profile as CombatSkillDef).TargetModeKind == BattleTargetMode.Unit
        && (sd.combat_profile as CombatSkillDef).TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;

    private List<CombatCastVariantDef> _get_random_chain_cast_variants(
        BattleAiContext context,
        SkillDef sd
    )
    {
        var r = new List<CombatCastVariantDef>();
        if (sd?.combat_profile == null)
            return r;
        var cp = sd.combat_profile as CombatSkillDef;
        if (cp.cast_variants.Count == 0)
        {
            r.Add(null);
            return r;
        }
        int sl = context?.unit_state != null ? _get_skill_level(context.unit_state, sd.skill_id) : 0;
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(context?.skill_catalog, sd, sl);
        foreach (var cv in effectiveProfile.UnlockedCastVariants)
            if (cv != null)
                r.Add(cv);
        return r;
    }

    private static BattleCommand _build_random_chain_skill_command(
        BattleAiContext context,
        StringName sid,
        CombatCastVariantDef cv
    )
    {
        if (context?.unit_state == null)
            return null;
        var cmd = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_id = sid,
            skill_variant_id = cv?.variant_id ?? new StringName(""),
        };
        return cmd;
    }

    private List<BattleUnitState> _resolve_candidate_units(
        BattleAiContext context,
        BattlePreview preview,
        SkillDef sd
    )
    {
        var cids = new HashSet<StringName>();
        if (preview != null)
            foreach (StringName ru in preview.RandomChainCandidateUnitIdsTyped)
            {
                var uid = ProgressionDataUtils.to_string_name(ru);
                if (uid != "")
                    cids.Add(uid);
            }
        if (cids.Count == 0)
            return new List<BattleUnitState>();
        List<BattleUnitState> sorted = _sort_target_units_typed(
            context,
            (sd.combat_profile as CombatSkillDef).target_team_filter,
            target_selector
        );
        var r = new List<BattleUnitState>();
        foreach (BattleUnitState su in sorted)
        {
            if (su != null && cids.Contains(su.unit_id))
                r.Add(su);
        }
        return r;
    }

    private static List<StringName> _candidate_unit_ids(
        IEnumerable<BattleUnitState> candidates
    )
    {
        var r = new List<StringName>();
        foreach (var c in candidates)
            if (c != null)
                r.Add(c.unit_id);
        return r;
    }

    private static List<CombatEffectDef> _collect_random_chain_effect_defs(
        SkillDef sd,
        CombatCastVariantDef cv
    )
    {
        var r = new List<CombatEffectDef>();
        if (sd?.combat_profile != null)
            foreach (var ed in (sd.combat_profile as CombatSkillDef).effect_defs)
                if (ed != null)
                    r.Add(ed);
        if (cv != null)
            foreach (var ed in cv.effect_defs)
                if (ed != null)
                    r.Add(ed);
        return r;
    }

    private RandomChainScoreMetadata BuildRandomChainScoreMetadata(
        BattleAiContext context,
        IReadOnlyList<BattleUnitState> candidates,
        SkillDef sd,
        string actionLabel
    )
    {
        RandomChainDistanceContract distanceContract = ResolveRandomChainDistanceContract(
            context,
            sd
        );
        var metadata = new RandomChainScoreMetadata
        {
            ActionLabel = actionLabel ?? "",
            CandidatePoolUnitIds = _candidate_unit_ids(candidates),
            DesiredMinDistance = distanceContract.DesiredMinDistance,
            DesiredMaxDistance = distanceContract.DesiredMaxDistance,
            ConfiguredDesiredMinDistance = distanceContract.ConfiguredDesiredMinDistance,
            ConfiguredDesiredMaxDistance = distanceContract.ConfiguredDesiredMaxDistance,
            EffectiveAttackRange = distanceContract.EffectiveAttackRange,
            MaxHitsPerTarget = Mathf.Max(
                (sd?.combat_profile as CombatSkillDef)?.max_hits_per_target ?? 1,
                1
            ),
        };
        metadata.MaxAttemptCount = Mathf.Max(
            metadata.CandidatePoolUnitIds.Count * metadata.MaxHitsPerTarget,
            1
        );
        if (DistanceReferenceKind == EnemyAiDistanceReference.CandidatePool)
        {
            var pc = candidates.Count > 0 ? candidates[0] : null;
            if (pc != null)
                metadata.PositionTargetUnitId = pc.unit_id;
            else
                metadata.PositionObjectiveKind = "none";
        }
        else if (DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            var fl = _resolve_enemy_frontline_unit(context);
            if (fl != null)
                metadata.PositionTargetUnitId = fl.unit_id;
            else
                metadata.PositionObjectiveKind = "none";
        }
        else
            metadata.PositionObjectiveKind = "none";
        return metadata;
    }

    private RandomChainDistanceContract ResolveRandomChainDistanceContract(
        BattleAiContext context,
        SkillDef skillDef
    )
    {
        int configuredMinDistance = desired_min_distance;
        int configuredMaxDistance = desired_max_distance;
        int effectiveAttackRange = _resolve_effective_attack_range(context, skillDef);
        int resolvedMaxDistance =
            effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
        {
            resolvedMinDistance = resolvedMaxDistance;
        }
        return new RandomChainDistanceContract
        {
            DesiredMinDistance = resolvedMinDistance,
            DesiredMaxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ConfiguredDesiredMinDistance = configuredMinDistance,
            ConfiguredDesiredMaxDistance = configuredMaxDistance,
            EffectiveAttackRange = effectiveAttackRange,
        };
    }

    private BattleUnitState _resolve_enemy_frontline_unit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _sort_target_units_typed(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private bool _has_explicit_distance_contract() =>
        desired_min_distance >= 0
        && desired_max_distance >= desired_min_distance
        && (
            DistanceReferenceKind == EnemyAiDistanceReference.CandidatePool
            || DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
        );

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            e.Add($"UseRandomChainSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            e.Add($"UseRandomChainSkillAction {action_id} is missing target_selector.");
        if (desired_min_distance < 0)
            e.Add($"UseRandomChainSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            e.Add(
                $"UseRandomChainSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.CandidatePool
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
            e.Add(
                $"UseRandomChainSkillAction {action_id} distance_reference must be candidate_pool or enemy_frontline."
            );
        if (minimum_candidate_count < 1)
            e.Add(
                $"UseRandomChainSkillAction {action_id} minimum_candidate_count must be >= 1."
            );
        return e;
    }

    private sealed class RandomChainDistanceContract
    {
        public int DesiredMinDistance = -1;
        public int DesiredMaxDistance = -1;
        public int ConfiguredDesiredMinDistance = -1;
        public int ConfiguredDesiredMaxDistance = -1;
        public int EffectiveAttackRange = -1;
    }

    private sealed class RandomChainScoreMetadata
    {
        private static readonly StringName ActionKind = "random_chain_skill";
        private static readonly StringName TargetSelectionMode = "random_chain";
        private static readonly StringName SelectionPolicy = "random_from_living_pool";
        private static readonly StringName PoolRefreshPolicy = "before_each_attempt";
        private static readonly StringName ScoreEstimatePolicy = "expected_value";

        public string ActionLabel = "";
        public List<StringName> CandidatePoolUnitIds = new();
        public int DesiredMinDistance = -1;
        public int DesiredMaxDistance = -1;
        public int ConfiguredDesiredMinDistance = -1;
        public int ConfiguredDesiredMaxDistance = -1;
        public int EffectiveAttackRange = -1;
        public StringName PositionTargetUnitId = "";
        public StringName PositionObjectiveKind = "";
        public int MaxHitsPerTarget = 1;
        public int MaxAttemptCount = 1;

        public Dictionary<string, object> ToScoreMetadata()
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = ActionKind,
                ["target_selection_mode"] = TargetSelectionMode,
                ["action_label"] = ActionLabel ?? "",
                ["candidate_pool_unit_ids"] = new List<StringName>(CandidatePoolUnitIds),
                ["candidate_pool_count"] = CandidatePoolUnitIds.Count,
                ["random_chain_max_hits_per_target"] = MaxHitsPerTarget,
                ["random_chain_max_attempt_count"] = MaxAttemptCount,
                ["random_chain_selection_policy"] = SelectionPolicy,
                ["random_chain_pool_refresh_policy"] = PoolRefreshPolicy,
                ["random_chain_score_estimate_policy"] = ScoreEstimatePolicy,
                ["desired_min_distance"] = DesiredMinDistance,
                ["desired_max_distance"] = DesiredMaxDistance,
                ["configured_desired_min_distance"] = ConfiguredDesiredMinDistance,
                ["configured_desired_max_distance"] = ConfiguredDesiredMaxDistance,
                ["effective_attack_range"] = EffectiveAttackRange,
            };
            if (PositionTargetUnitId != "")
            {
                result["position_target_unit_id"] = PositionTargetUnitId;
            }
            if (PositionObjectiveKind != "")
            {
                result["position_objective_kind"] = PositionObjectiveKind;
            }
            return result;
        }

        public Dictionary<string, object> ToCandidateSummaryMetadata(StringName skillId)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["skill_id"] = skillId.ToString(),
                ["candidate_pool_count"] = CandidatePoolUnitIds.Count,
                ["candidate_pool_unit_ids"] = StringifyUnitIdList(CandidatePoolUnitIds),
            };
        }

        public void ApplyToTrace(AiActionTrace actionTrace)
        {
            if (actionTrace == null || actionTrace.IsEmpty())
            {
                return;
            }
            actionTrace.Metadata["candidate_pool_count"] = CandidatePoolUnitIds.Count;
            actionTrace.Metadata["candidate_pool_unit_ids"] = StringifyUnitIdList(
                CandidatePoolUnitIds
            );
            actionTrace.Metadata["max_hits_per_target"] = MaxHitsPerTarget;
            actionTrace.Metadata["max_attempt_count"] = MaxAttemptCount;
        }

        private static List<string> StringifyUnitIdList(IEnumerable<StringName> ids)
        {
            var result = new List<string>();
            foreach (StringName id in ids ?? System.Array.Empty<StringName>())
            {
                result.Add(id.ToString());
            }
            return result;
        }
    }
}
