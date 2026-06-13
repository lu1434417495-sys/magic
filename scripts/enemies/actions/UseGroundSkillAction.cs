using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class UseGroundSkillAction : EnemyAiAction
{
    private sealed class GroundCandidatePrefilter
    {
        public bool ShouldEvaluate = true;
        public string RejectReason = "";
        public string DedupeKey = "";
        public int RawHitCount;
        public int AllyThreatHitCount;
        public List<Vector2I> EffectCoords = new();
        public List<StringName> HitUnitIds = new();
    }

    private sealed class GroundTargetCoordSet
    {
        public GroundTargetCoordSet(IEnumerable<Vector2I> coords)
        {
            Coords = new List<Vector2I>();
            foreach (Vector2I coord in coords ?? System.Array.Empty<Vector2I>())
            {
                Coords.Add(coord);
            }
        }

        public List<Vector2I> Coords { get; }

        public bool IsEmpty => Coords.Count == 0;

        public Vector2I FirstOrDefault() => Coords.Count > 0 ? Coords[0] : Vector2I.Zero;

        public List<Vector2I> ToSortedList()
        {
            var sorted = new List<Vector2I>(Coords);
            sorted.Sort(
                (left, right) =>
                {
                    int yComparison = left.Y.CompareTo(right.Y);
                    return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
                }
            );
            return sorted;
        }
    }

    private sealed class GroundSkillEffectSet
    {
        public List<CombatEffectDef> Effects { get; } = new();

        public bool IsEmpty => Effects.Count == 0;

        public void Add(CombatEffectDef effectDef)
        {
            if (effectDef != null)
            {
                Effects.Add(effectDef);
            }
        }

    }

    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public int minimum_hit_count { get; set; } = 1;

    [Export]
    public bool allow_empty_ground_control { get; set; } = false;

    [Export]
    public bool allow_ground_control_supplement_partial_hits { get; set; } = false;

    [Export]
    public int minimum_ground_control_score { get; set; } = 1;

    [Export]
    public int minimum_ally_threat_hit_count { get; set; } = 0;

    [Export]
    public int maximum_friendly_fire_target_count { get; set; } = 0;

    [Export]
    public bool allow_friendly_lethal { get; set; } = false;

    [Export]
    public int threat_minimum_safe_distance { get; set; } = 0;

    [Export]
    public int threat_safe_distance_margin { get; set; } = 0;

    [Export]
    public int desired_min_distance { get; set; } = -1;

    [Export]
    public int desired_max_distance { get; set; } = -1;

    [Export]
    public StringName distance_reference { get; set; } = "";
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:ground_skill");
        try
        {
            return _decide_impl(context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:ground_skill");
        }
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        if (!_has_explicit_distance_contract())
        {
            return null;
        }

        AiActionTrace actionTrace = _begin_action_trace(
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "ground_skill",
                ["minimum_hit_count"] = minimum_hit_count,
                ["allow_empty_ground_control"] = allow_empty_ground_control,
                ["allow_ground_control_supplement_partial_hits"] =
                    allow_ground_control_supplement_partial_hits,
                ["minimum_ground_control_score"] = minimum_ground_control_score,
                ["minimum_ally_threat_hit_count"] = minimum_ally_threat_hit_count,
                ["maximum_friendly_fire_target_count"] = maximum_friendly_fire_target_count,
                ["allow_friendly_lethal"] = allow_friendly_lethal,
                ["threat_minimum_safe_distance"] = threat_minimum_safe_distance,
                ["threat_safe_distance_margin"] = threat_safe_distance_margin,
                ["distance_reference"] = distance_reference.ToString(),
                ["desired_min_distance"] = desired_min_distance,
                ["desired_max_distance"] = desired_max_distance,
            }
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        BattleUnitState unitState = context.unit_state;
        var prefilterSeenKeys = new HashSet<string>();
        List<BattleUnitState> livingUnits = _collect_living_units(context);
        List<BattleUnitState> allyUnits =
            minimum_ally_threat_hit_count > 0
                ? _collect_units_by_filter_from_list(context, livingUnits, "ally")
                : new List<BattleUnitState>();

        foreach (StringName skillId in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(actionTrace, "skill_considered_count");
            SkillDef skillDef = _get_skill_def(context, skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile as CombatSkillDef;
            if (skillDef == null || combatProfile == null)
            {
                _trace_add_block_reason(actionTrace, "missing_skill_def");
                continue;
            }
            if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
            {
                _trace_add_block_reason(actionTrace, "non_ground_skill");
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = _get_skill_cast_block_reason(
                context,
                skillDef
            );
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                _trace_add_block_reason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }

            foreach (CombatCastVariantDef castVariant in _get_ground_options_typed(
                context,
                skillDef
            ))
            {
                if (castVariant == null || _is_charge_option(castVariant))
                {
                    continue;
                }
                GroundSkillEffectSet effectDefs = _collect_ground_skill_effect_defs(
                    skillDef,
                    castVariant
                );

                int effectiveSkillRange = BattleRangeService.GetEffectiveSkillRange(
                    unitState,
                    skillDef,
                    context.skill_catalog
                );
                bool usesRelocationDistance = BattleRangeService.IsGroundRelocationSkill(
                    skillDef
                );
                List<GroundTargetCoordSet> targetCoordSets;
                AiTraceRecorder.Enter("ground_skill:enumerate_targets");
                try
                {
                    targetCoordSets = EnumerateGroundTargetCoordSets(context, castVariant);
                }
                finally
                {
                    AiTraceRecorder.Exit("ground_skill:enumerate_targets");
                }
                foreach (GroundTargetCoordSet targetCoords in targetCoordSets)
                {
                    if (
                        !_is_ground_coord_set_within_cast_range(
                            context,
                            targetCoords,
                            effectiveSkillRange,
                            usesRelocationDistance
                        )
                    )
                    {
                        _trace_count_increment(actionTrace, "range_prefilter_reject_count");
                        continue;
                    }

                    GroundCandidatePrefilter prefilter;
                    AiTraceRecorder.Enter("ground_skill:prefilter");
                    try
                    {
                        prefilter = _build_ground_candidate_prefilter(
                            context,
                            skillDef,
                            combatProfile,
                            castVariant,
                            targetCoords,
                            effectDefs,
                            livingUnits,
                            allyUnits
                        );
                    }
                    finally
                    {
                        AiTraceRecorder.Exit("ground_skill:prefilter");
                    }
                    if (!prefilter.ShouldEvaluate)
                    {
                        _trace_add_block_reason(actionTrace, prefilter.RejectReason);
                        continue;
                    }
                    if (
                        !string.IsNullOrEmpty(prefilter.DedupeKey)
                        && !prefilterSeenKeys.Add(prefilter.DedupeKey)
                    )
                    {
                        _trace_add_block_reason(actionTrace, "duplicate_prefilter_hit_set");
                        continue;
                    }

                    _trace_count_increment(actionTrace, "evaluation_count");
                    BattleCommand command = _build_typed_ground_skill_command(
                        context,
                        skillId,
                        castVariant.variant_id,
                        targetCoords.ToSortedList()
                    );
                    BattlePreview preview;
                    AiTraceRecorder.Enter("ground_skill:fast_preview");
                    try
                    {
                        preview = _build_fast_ground_skill_preview(
                            context,
                            command,
                            prefilter.EffectCoords,
                            prefilter.HitUnitIds
                        );
                    }
                    finally
                    {
                        AiTraceRecorder.Exit("ground_skill:fast_preview");
                    }
                    if (preview?.allowed != true)
                    {
                        _trace_count_increment(actionTrace, "preview_reject_count");
                        continue;
                    }

                    var previewTargetIds = preview.TargetUnitIdsTyped;
                    int rawHitCount = previewTargetIds.Count;
                    int allyThreatHitCount = prefilter.AllyThreatHitCount;
                    if (
                        minimum_ally_threat_hit_count > 0
                        && allyThreatHitCount < minimum_ally_threat_hit_count
                    )
                    {
                        _trace_add_block_reason(actionTrace, "minimum_ally_threat_hit_count");
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = _build_position_metadata(
                        context,
                        command,
                        skillDef
                    );
                    positionMetadata["action_label"] = _format_skill_variant_label(
                        skillDef,
                        castVariant
                    );
                    BattleAiScoreInput scoreInput;
                    AiTraceRecorder.Enter("ground_skill:score_input");
                    try
                    {
                        scoreInput = _build_typed_skill_score_input(
                            context,
                            skillDef,
                            command,
                            preview,
                            effectDefs.Effects,
                            positionMetadata
                        );
                    }
                    finally
                    {
                        AiTraceRecorder.Exit("ground_skill:score_input");
                    }

                    if (scoreInput == null)
                    {
                        if (fallbackDecision == null && rawHitCount > 0)
                        {
                            fallbackDecision = _create_decision(
                                command,
                                $"{unitState?.display_name} 准备用 {skillDef.display_name} 覆盖 {rawHitCount} 个单位。"
                            );
                        }
                        _trace_offer_candidate(
                            actionTrace,
                            _build_candidate_summary(
                                _format_skill_variant_label(skillDef, castVariant),
                                command,
                                null,
                                new Dictionary<string, object>(StringComparer.Ordinal)
                                {
                                    ["raw_hit_count"] = rawHitCount,
                                    ["ally_threat_hit_count"] = allyThreatHitCount,
                                    ["prefilter_raw_hit_count"] = prefilter.RawHitCount,
                                    ["prefilter_ally_threat_hit_count"] =
                                        prefilter.AllyThreatHitCount,
                                    ["skill_id"] = skillId.ToString(),
                                }
                            )
                        );
                        continue;
                    }

                    if (!PassesMinimumEffectiveTargetOrGroundControl(scoreInput))
                    {
                        _trace_add_block_reason(
                            actionTrace,
                            _resolve_minimum_hit_block_reason(scoreInput)
                        );
                        continue;
                    }
                    if (!PassesFriendlyFireLimits(scoreInput))
                    {
                        _trace_add_block_reason(actionTrace, "friendly_fire_limit");
                        continue;
                    }
                    if (!_passes_candidate_value_floor(scoreInput))
                    {
                        _trace_add_block_reason(actionTrace, "negative_zero_damage_no_threat_gain");
                        continue;
                    }

                    _trace_offer_candidate(
                        actionTrace,
                        _build_candidate_summary(
                            _format_skill_variant_label(skillDef, castVariant),
                            command,
                            scoreInput,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["raw_hit_count"] = rawHitCount,
                                ["effective_hit_count"] = scoreInput.effective_target_count,
                                ["ally_threat_hit_count"] = allyThreatHitCount,
                                ["prefilter_raw_hit_count"] = prefilter.RawHitCount,
                                ["prefilter_ally_threat_hit_count"] =
                                    prefilter.AllyThreatHitCount,
                                ["allow_empty_ground_control"] = allow_empty_ground_control,
                                ["allow_ground_control_supplement_partial_hits"] =
                                    allow_ground_control_supplement_partial_hits,
                                ["estimated_ground_control_cell_count"] =
                                    scoreInput.estimated_ground_control_cell_count,
                                ["ground_control_score"] = scoreInput.ground_control_score,
                                ["acceptance_reason"] = _resolve_candidate_acceptance_reason(
                                    scoreInput
                                ),
                                ["skill_id"] = skillId.ToString(),
                            }
                        )
                    );

                    if (!_is_better_skill_score_input(scoreInput, bestScoreInput))
                    {
                        continue;
                    }
                    bestScoreInput = scoreInput;
                    bestDecision = _create_scored_decision(
                        command,
                        scoreInput,
                        _build_decision_reason(context, skillDef, scoreInput)
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        _finalize_action_trace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private bool _is_ground_coord_set_within_cast_range(
        BattleAiContext context,
        GroundTargetCoordSet targetCoords,
        int effectiveSkillRange,
        bool usesRelocationDistance
    )
    {
        BattleGridService gridService = context?.grid_service;
        BattleUnitState unitState = context?.unit_state;
        if (context == null || gridService == null || unitState == null)
        {
            return true;
        }

        IEnumerable<Vector2I> candidateCoords =
            targetCoords?.Coords ?? (IEnumerable<Vector2I>)System.Array.Empty<Vector2I>();
        foreach (Vector2I coord in candidateCoords)
        {
            int distance = usesRelocationDistance
                ? gridService.GetChebyshevDistance(unitState.coord, coord)
                : gridService.GetDistanceFromUnitToCoord(unitState, coord);
            if (distance > effectiveSkillRange)
            {
                return false;
            }
        }
        return true;
    }

    private GroundCandidatePrefilter _build_ground_candidate_prefilter(
        BattleAiContext context,
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        CombatCastVariantDef castVariant,
        GroundTargetCoordSet targetCoords,
        GroundSkillEffectSet effectDefs,
        IReadOnlyList<BattleUnitState> livingUnits,
        IReadOnlyList<BattleUnitState> allyUnits
    )
    {
        var result = new GroundCandidatePrefilter();
        if (
            context?.state == null
            || context?.grid_service == null
            || context?.unit_state == null
            || skillDef == null
            || combatProfile == null
        )
        {
            return result;
        }

        List<Vector2I> effectCoords = _build_prefilter_effect_coords(
            context,
            skillDef,
            targetCoords
        );
        result.EffectCoords = effectCoords;
        var effectCoordSet = new HashSet<Vector2I>(effectCoords);
        var hitUnitIds = new List<string>();
        var hitUnits = new List<BattleUnitState>();
        foreach (
            BattleUnitState targetUnit in livingUnits ?? (IReadOnlyList<BattleUnitState>)Array.Empty<BattleUnitState>()
        )
        {
            if (targetUnit == null || !targetUnit.is_alive)
            {
                continue;
            }
            if (!_unit_intersects_coords(targetUnit, effectCoordSet))
            {
                continue;
            }
            if (!_unit_matches_any_ground_effect(context, targetUnit, combatProfile, effectDefs))
            {
                continue;
            }
            hitUnits.Add(targetUnit);
            hitUnitIds.Add(targetUnit.unit_id.ToString());
            result.HitUnitIds.Add(targetUnit.unit_id);
        }

        hitUnitIds.Sort(StringComparer.Ordinal);
        result.RawHitCount = hitUnitIds.Count;
        if (minimum_ally_threat_hit_count > 0)
        {
            IReadOnlyList<BattleUnitState> allies =
                allyUnits ?? (IReadOnlyList<BattleUnitState>)Array.Empty<BattleUnitState>();
            foreach (BattleUnitState targetUnit in hitUnits)
            {
                if (targetUnit.faction_id == context.unit_state.faction_id)
                {
                    continue;
                }
                if (_is_target_threatening_any_ally(context, targetUnit, allies))
                {
                    result.AllyThreatHitCount += 1;
                }
            }
        }

        if (result.RawHitCount == 0 && !allow_empty_ground_control)
        {
            result.ShouldEvaluate = false;
            result.RejectReason = "prefilter_no_targets";
            return result;
        }
        if (
            result.RawHitCount < minimum_hit_count
            && !allow_empty_ground_control
            && !allow_ground_control_supplement_partial_hits
        )
        {
            result.ShouldEvaluate = false;
            result.RejectReason = "prefilter_minimum_hit_count";
            return result;
        }
        if (
            minimum_ally_threat_hit_count > 0
            && result.AllyThreatHitCount < minimum_ally_threat_hit_count
        )
        {
            result.ShouldEvaluate = false;
            result.RejectReason = "prefilter_minimum_ally_threat_hit_count";
            return result;
        }

        if (!allow_empty_ground_control && result.RawHitCount > 0)
        {
            Vector2I direction = _resolve_prefilter_direction(context, targetCoords);
            result.DedupeKey =
                $"{skillDef.skill_id}|{castVariant?.variant_id}|{Math.Sign(direction.X)},{Math.Sign(direction.Y)}|{string.Join(",", hitUnitIds)}";
        }
        return result;
    }

    private List<Vector2I> _build_prefilter_effect_coords(
        BattleAiContext context,
        SkillDef skillDef,
        GroundTargetCoordSet targetCoords
    )
    {
        var result = new List<Vector2I>();
        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (context?.grid_service == null || context?.state == null || combatProfile == null)
        {
            return result;
        }
        int skillLevel = _get_skill_level(context.unit_state, skillDef.skill_id);
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(
                context?.skill_catalog,
                skillDef,
                skillLevel
            );
        StringName areaPattern = effectiveProfile.AreaPattern;
        int areaValue = Mathf.Max(effectiveProfile.AreaValue, 0);
        var seen = new HashSet<Vector2I>();
        IEnumerable<Vector2I> targetCoordValues =
            targetCoords?.Coords ?? (IEnumerable<Vector2I>)System.Array.Empty<Vector2I>();
        foreach (Vector2I targetCoord in targetCoordValues)
        {
            Vector2I direction = context.unit_state != null
                ? targetCoord - context.unit_state.coord
                : Vector2I.Zero;
            foreach (
                Vector2I effectCoord in context.grid_service.GetAreaCoords(
                    context.state,
                    targetCoord,
                    areaPattern,
                    areaValue,
                    direction
                )
            )
            {
                if (seen.Add(effectCoord))
                {
                    result.Add(effectCoord);
                }
            }
        }
        return result;
    }

    private static Vector2I _resolve_prefilter_direction(
        BattleAiContext context,
        GroundTargetCoordSet targetCoords
    )
    {
        if (context?.unit_state == null || targetCoords == null || targetCoords.IsEmpty)
        {
            return Vector2I.Zero;
        }
        return targetCoords.FirstOrDefault() - context.unit_state.coord;
    }

    private static bool _unit_intersects_coords(
        BattleUnitState unitState,
        HashSet<Vector2I> coordSet
    )
    {
        if (unitState == null || coordSet == null || coordSet.Count == 0)
        {
            return false;
        }
        unitState.RefreshFootprint();
        foreach (Vector2I occupiedCoord in unitState.occupied_coords)
        {
            if (coordSet.Contains(occupiedCoord))
            {
                return true;
            }
        }
        return false;
    }

    private static List<BattleUnitState> _collect_living_units(BattleAiContext context)
    {
        var result = new List<BattleUnitState>();
        if (context?.state == null)
        {
            return result;
        }
        foreach (BattleUnitState unitState in context.state.GetUnitsTyped())
        {
            if (unitState != null && unitState.is_alive)
            {
                result.Add(unitState);
            }
        }
        return result;
    }

    private List<BattleUnitState> _collect_units_by_filter_from_list(
        BattleAiContext context,
        IEnumerable<BattleUnitState> units,
        StringName targetFilter
    )
    {
        var result = new List<BattleUnitState>();
        foreach (BattleUnitState unitState in units ?? Array.Empty<BattleUnitState>())
        {
            if (unitState != null && unitState.is_alive && _matches_target_filter(context, unitState, targetFilter))
            {
                result.Add(unitState);
            }
        }
        return result;
    }

    private bool _unit_matches_any_ground_effect(
        BattleAiContext context,
        BattleUnitState targetUnit,
        CombatSkillDef combatProfile,
        GroundSkillEffectSet effectDefs
    )
    {
        if (targetUnit == null || combatProfile == null)
        {
            return false;
        }
        if (effectDefs == null || effectDefs.IsEmpty)
        {
            return _matches_target_filter(context, targetUnit, combatProfile.target_team_filter);
        }
        foreach (CombatEffectDef effectDef in effectDefs.Effects)
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName targetFilter = effectDef.effect_target_team_filter != ""
                ? effectDef.effect_target_team_filter
                : combatProfile.target_team_filter;
            if (_matches_target_filter(context, targetUnit, targetFilter))
            {
                return true;
            }
        }
        return false;
    }

    internal bool PassesMinimumEffectiveTargetOrGroundControl(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (scoreInput.effective_target_count >= minimum_hit_count)
        {
            return true;
        }
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return true;
        }
        return _is_ground_control_supplement_candidate(scoreInput);
    }

    private bool _is_empty_ground_control_candidate(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null || !allow_empty_ground_control)
        {
            return false;
        }
        if (scoreInput.effective_target_count != 0)
        {
            return false;
        }
        if (scoreInput.estimated_ground_control_cell_count <= 0)
        {
            return false;
        }
        return scoreInput.ground_control_score >= minimum_ground_control_score;
    }

    private bool _is_ground_control_supplement_candidate(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null || !allow_ground_control_supplement_partial_hits)
        {
            return false;
        }
        int effectiveTargetCount = scoreInput.effective_target_count;
        if (effectiveTargetCount <= 0 || effectiveTargetCount >= minimum_hit_count)
        {
            return false;
        }
        if (scoreInput.estimated_ground_control_cell_count <= 0)
        {
            return false;
        }
        return scoreInput.ground_control_score >= minimum_ground_control_score;
    }

    private string _resolve_minimum_hit_block_reason(BattleAiScoreInput scoreInput)
    {
        if (scoreInput != null)
        {
            int effectiveTargetCount = scoreInput.effective_target_count;
            int groundCellCount = scoreInput.estimated_ground_control_cell_count;
            if (effectiveTargetCount == 0 && groundCellCount > 0)
            {
                if (!allow_empty_ground_control)
                {
                    return "empty_ground_control_not_allowed";
                }
                if (
                    scoreInput.ground_control_score < minimum_ground_control_score
                )
                {
                    return "minimum_ground_control_score";
                }
            }
            else if (effectiveTargetCount == 0 && allow_empty_ground_control)
            {
                return "no_ground_control_score";
            }
            else if (
                effectiveTargetCount > 0
                && effectiveTargetCount < minimum_hit_count
                && groundCellCount > 0
            )
            {
                if (!allow_ground_control_supplement_partial_hits)
                {
                    return "ground_control_supplement_not_allowed";
                }
                if (
                    scoreInput.ground_control_score < minimum_ground_control_score
                )
                {
                    return "minimum_ground_control_score";
                }
            }
        }
        return "minimum_effective_hit_count";
    }

    private string _resolve_candidate_acceptance_reason(BattleAiScoreInput scoreInput)
    {
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return "ground_control";
        }
        if (_is_ground_control_supplement_candidate(scoreInput))
        {
            return "ground_control_supplement";
        }
        return "effective_targets";
    }

    private string _build_decision_reason(
        BattleAiContext context,
        SkillDef skillDef,
        BattleAiScoreInput scoreInput
    )
    {
        string unitName = context?.unit_state?.display_name ?? "";
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDef.display_name} 控制 {scoreInput.estimated_ground_control_cell_count} 个地格（评分 {scoreInput.total_score}）。";
        }
        if (_is_ground_control_supplement_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDef.display_name} 覆盖 {scoreInput.effective_target_count} 个有效目标并控制 {scoreInput.estimated_ground_control_cell_count} 个地格（评分 {scoreInput.total_score}）。";
        }
        return $"{unitName} 准备用 {skillDef.display_name} 覆盖 {scoreInput.effective_target_count} 个有效目标（评分 {scoreInput.total_score}）。";
    }

    internal bool PassesFriendlyFireLimits(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        string rejectReason = scoreInput.friendly_fire_reject_reason;
        if (!string.IsNullOrEmpty(rejectReason))
        {
            return false;
        }
        if (_is_meteor_special_score_input(scoreInput))
        {
            return true;
        }
        if (
            scoreInput.estimated_friendly_fire_target_count > maximum_friendly_fire_target_count
        )
        {
            return false;
        }
        if (
            !allow_friendly_lethal
            && scoreInput.estimated_friendly_lethal_target_count > 0
        )
        {
            return false;
        }
        return true;
    }

    private static bool _passes_candidate_value_floor(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (scoreInput.total_score >= 0)
        {
            return true;
        }
        if (
            scoreInput.estimated_damage > 0
            || scoreInput.estimated_enemy_damage > 0
            || scoreInput.estimated_healing > 0
            || scoreInput.estimated_enemy_healing > 0
            || scoreInput.estimated_lethal_target_count > 0
            || scoreInput.estimated_lethal_threat_target_count > 0
        )
        {
            return true;
        }
        if (!scoreInput.has_post_action_threat_projection)
        {
            return false;
        }
        if (scoreInput.pre_action_is_lethal_survival_risk && !scoreInput.post_action_is_lethal_survival_risk)
        {
            return true;
        }
        return scoreInput.pre_action_threat_expected_damage
                > scoreInput.post_action_remaining_threat_expected_damage
            && scoreInput.post_action_survival_margin >= 0;
    }

    private static bool _is_meteor_special_score_input(BattleAiScoreInput scoreInput)
    {
        return scoreInput.special_profile_preview_facts?.profile_id == "meteor_swarm";
    }

    private Dictionary<string, object> _build_position_metadata(
        BattleAiContext context,
        BattleCommand command,
        SkillDef skillDef
    )
    {
        Dictionary<string, object> metadata = _resolve_desired_distance_contract_typed(
            context,
            skillDef
        );
        if (DistanceReferenceKind == EnemyAiDistanceReference.TargetCoord)
        {
            metadata["position_objective_kind"] = "cast_distance";
            metadata["position_target_coord"] =
                command != null ? command.target_coord : new Vector2I(-1, -1);
        }
        else if (DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontlineUnit = _resolve_enemy_frontline_unit(context);
            if (frontlineUnit != null)
            {
                metadata["position_target_unit_id"] = frontlineUnit.unit_id;
            }
            else
            {
                metadata["position_objective_kind"] = "none";
            }
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private bool _is_target_threatening_any_ally(
        BattleAiContext context,
        BattleUnitState targetUnit,
        IEnumerable<BattleUnitState> allies
    )
    {
        if (context == null || targetUnit == null)
        {
            return false;
        }
        int safeDistance = _resolve_target_safe_distance(
            context,
            targetUnit,
            threat_minimum_safe_distance,
            threat_safe_distance_margin
        );
        foreach (BattleUnitState allyUnit in allies ?? System.Array.Empty<BattleUnitState>())
        {
            if (allyUnit == null || !allyUnit.is_alive)
            {
                continue;
            }
            if (_distance_between_units(context, targetUnit, allyUnit) <= safeDistance)
            {
                return true;
            }
        }
        return false;
    }

    private static GroundSkillEffectSet _collect_ground_skill_effect_defs(
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        var effectDefs = new GroundSkillEffectSet();
        CombatSkillDef combatProfile = skillDef?.combat_profile as CombatSkillDef;
        if (combatProfile != null)
        {
            if (combatProfile.cast_variants.Count == 0)
            {
                if (castVariant != null)
                {
                    AppendEffects(effectDefs, castVariant.effect_defs);
                }
                else
                {
                    AppendEffects(effectDefs, combatProfile.effect_defs);
                }
                return effectDefs;
            }
            AppendEffects(effectDefs, combatProfile.effect_defs);
        }
        if (castVariant != null)
        {
            AppendEffects(effectDefs, castVariant.effect_defs);
        }
        return effectDefs;
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

    private bool _has_explicit_distance_contract()
    {
        return desired_min_distance >= 0
            && desired_max_distance >= desired_min_distance
            && (
                DistanceReferenceKind == EnemyAiDistanceReference.TargetCoord
                || DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} must declare at least one skill_id.");
        }
        if (minimum_hit_count <= 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} minimum_hit_count must be >= 1.");
        }
        if (minimum_ground_control_score <= 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} minimum_ground_control_score must be >= 1."
            );
        }
        if (minimum_ally_threat_hit_count < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} minimum_ally_threat_hit_count must be >= 0."
            );
        }
        if (maximum_friendly_fire_target_count < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} maximum_friendly_fire_target_count must be >= 0."
            );
        }
        if (threat_minimum_safe_distance < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} threat_minimum_safe_distance must be >= 0."
            );
        }
        if (threat_safe_distance_margin < 0)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} threat_safe_distance_margin must be >= 0."
            );
        }
        if (desired_min_distance < 0)
        {
            errors.Add($"UseGroundSkillAction {action_id} desired_min_distance must be >= 0.");
        }
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.TargetCoord
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
        {
            errors.Add(
                $"UseGroundSkillAction {action_id} distance_reference must be target_coord or enemy_frontline."
            );
        }
        return errors;
    }

    private List<GroundTargetCoordSet> EnumerateGroundTargetCoordSets(
        BattleAiContext context,
        CombatCastVariantDef castVariant
    )
    {
        var result = new List<GroundTargetCoordSet>();
        foreach (
            List<Vector2I> coords in _enumerate_ground_target_coord_sets_typed(
                context,
                castVariant
            )
        )
        {
            var coordSet = new GroundTargetCoordSet(coords);
            if (!coordSet.IsEmpty)
            {
                result.Add(coordSet);
            }
        }
        return result;
    }

    private static void AppendEffects(
        GroundSkillEffectSet target,
        IEnumerable<CombatEffectDef> effects
    )
    {
        if (target == null || effects == null)
        {
            return;
        }
        foreach (CombatEffectDef effectDef in effects)
        {
            target.Add(effectDef);
        }
    }

}
