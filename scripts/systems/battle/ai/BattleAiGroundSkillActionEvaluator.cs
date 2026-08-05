using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiGroundSkillActionEvaluator
{
    private static readonly StringName EmptyStringName = "";

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly Dictionary<StringName, CombatCastVariantDefinition> _implicitGroundOptionDefinitionsBySkillId = new();
    private UseGroundSkillActionDefinition _action;

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
        public List<CombatEffectDefinition> Effects { get; } = new();

        public bool IsEmpty => Effects.Count == 0;

        public void Add(CombatEffectDefinition effectDefinition)
        {
            if (effectDefinition != null)
            {
                Effects.Add(effectDefinition);
            }
        }

    }


    internal BattleAiDecision Evaluate(
        UseGroundSkillActionDefinition action,
        BattleAiContext context
    )
    {
        _action = action;
        if (_action == null || context == null)
        {
            return null;
        }

        AiTraceRecorder.Enter("decide:ground_skill");
        try
        {
            return DecideImpl(context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:ground_skill");
            _action = null;
        }
    }

    internal bool PassesMinimumEffectiveTargetOrGroundControl(
        UseGroundSkillActionDefinition action,
        BattleAiScoreInput scoreInput
    )
    {
        _action = action;
        try
        {
            return PassesMinimumEffectiveTargetOrGroundControl(scoreInput);
        }
        finally
        {
            _action = null;
        }
    }

    internal bool PassesFriendlyFireLimits(
        UseGroundSkillActionDefinition action,
        BattleAiScoreInput scoreInput
    )
    {
        _action = action;
        try
        {
            return PassesFriendlyFireLimits(scoreInput);
        }
        finally
        {
            _action = null;
        }
    }

    private BattleAiDecision DecideImpl(BattleAiContext context)
    {
        if (!_has_explicit_distance_contract())
        {
            return null;
        }

        AiActionTrace actionTrace = context?.trace_enabled == true
            ? BeginActionTrace(
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
            )
            : null;

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

        foreach (BattleAvailableSkillEntry skillEntry in ResolveAvailableSkillEntries(context, skill_ids))
        {
            StringName skillId = skillEntry.EntryRef.SkillId;
            TraceCountIncrement(actionTrace, "skill_considered_count");
            SkillDefinition skillDefinition = GetSkillDefinition(context, skillEntry);
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (skillDefinition == null || combatProfile == null)
            {
                TraceAddBlockReason(actionTrace, "missing_skill_definition");
                continue;
            }
            if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
            {
                TraceAddBlockReason(actionTrace, "non_ground_skill");
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = GetSkillCastBlockReason(
                context,
                skillDefinition
            );
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                TraceAddBlockReason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }

            foreach (CombatCastVariantDefinition castVariant in GetGroundOptionDefinitions(
                context,
                skillDefinition,
                skillEntry.SkillLevel
            ))
            {
                if (castVariant == null || IsChargeOption(castVariant))
                {
                    continue;
                }
                GroundSkillEffectSet effectDefs = CollectGroundSkillEffectDefs(
                    skillDefinition,
                    castVariant
                );

                int effectiveSkillRange = BattleRangeService.GetEffectiveSkillRange(
                    unitState,
                    skillDefinition,
                    context.skill_catalog
                );
                bool usesRelocationDistance = BattleRangeService.IsGroundRelocationSkill(
                    skillDefinition
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
                        TraceCountIncrement(actionTrace, "range_prefilter_reject_count");
                        continue;
                    }

                    GroundCandidatePrefilter prefilter;
                    AiTraceRecorder.Enter("ground_skill:prefilter");
                    try
                    {
                        prefilter = _build_ground_candidate_prefilter(
                            context,
                            skillDefinition,
                            combatProfile,
                            castVariant,
                            targetCoords,
                            effectDefs,
                            skillEntry.SkillLevel,
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
                        TraceAddBlockReason(actionTrace, prefilter.RejectReason);
                        continue;
                    }
                    if (
                        !string.IsNullOrEmpty(prefilter.DedupeKey)
                        && !prefilterSeenKeys.Add(prefilter.DedupeKey)
                    )
                    {
                        TraceAddBlockReason(actionTrace, "duplicate_prefilter_hit_set");
                        continue;
                    }

                    TraceCountIncrement(actionTrace, "evaluation_count");
                    BattleCommand command = BuildTypedGroundSkillCommand(
                        context,
                        skillEntry,
                        castVariant.VariantId,
                        targetCoords.ToSortedList()
                    );
                    BattlePreview preview;
                    AiTraceRecorder.Enter("ground_skill:formal_preview");
                    try
                    {
                        preview = context.PreviewCommand(command);
                    }
                    finally
                    {
                        AiTraceRecorder.Exit("ground_skill:formal_preview");
                    }
                    if (preview == null)
                    {
                        AiTraceRecorder.Enter("ground_skill:fast_preview");
                        try
                        {
                            preview = BuildFastGroundSkillPreview(
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
                    }
                    if (preview?.allowed != true)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count");
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
                        TraceAddBlockReason(actionTrace, "minimum_ally_threat_hit_count");
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = _build_position_metadata(
                        context,
                        command,
                        skillDefinition
                    );
                    positionMetadata["action_label"] = FormatSkillVariantLabel(
                        skillDefinition,
                        castVariant
                    );
                    BattleAiScoreInput scoreInput;
                    AiTraceRecorder.Enter("ground_skill:score_input");
                    try
                    {
                        scoreInput = BuildTypedSkillScoreInput(
                            context,
                            skillDefinition,
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
                            fallbackDecision = CreateDecision(
                                command,
                                $"{unitState?.display_name} 准备用 {skillDefinition.DisplayName} 覆盖 {rawHitCount} 个单位。"
                            );
                        }
                        if (actionTrace != null)
                        {
                            TraceOfferCandidate(
                                actionTrace,
                                BuildCandidateSummary(
                                    FormatSkillVariantLabel(skillDefinition, castVariant),
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
                        }
                        continue;
                    }

                    if (!PassesMinimumEffectiveTargetOrGroundControl(scoreInput))
                    {
                        TraceAddBlockReason(
                            actionTrace,
                            _resolve_minimum_hit_block_reason(scoreInput)
                        );
                        continue;
                    }
                    if (!PassesFriendlyFireLimits(scoreInput))
                    {
                        TraceAddBlockReason(actionTrace, "friendly_fire_limit");
                        continue;
                    }
                    if (!_passes_candidate_value_floor(scoreInput))
                    {
                        TraceAddBlockReason(actionTrace, "negative_zero_damage_no_threat_gain");
                        continue;
                    }

                    if (actionTrace != null)
                    {
                        TraceOfferCandidate(
                            actionTrace,
                            BuildCandidateSummary(
                                FormatSkillVariantLabel(skillDefinition, castVariant),
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
                    }

                    if (!IsBetterSkillScoreInput(scoreInput, bestScoreInput))
                    {
                        continue;
                    }
                    bestScoreInput = scoreInput;
                    bestDecision = CreateScoredDecision(
                        command,
                        scoreInput,
                        _build_decision_reason(context, skillDefinition, scoreInput)
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision);
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
                ? gridService.GetChebyshevDistance(
                    unitState.GetAnchorCoord(),
                    coord
                )
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
        SkillDefinition skillDefinition,
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant,
        GroundTargetCoordSet targetCoords,
        GroundSkillEffectSet effectDefs,
        int skillLevel,
        IReadOnlyList<BattleUnitState> livingUnits,
        IReadOnlyList<BattleUnitState> allyUnits
    )
    {
        var result = new GroundCandidatePrefilter();
        if (
            context?.state == null
            || context?.grid_service == null
            || context?.unit_state == null
            || skillDefinition == null
            || combatProfile == null
        )
        {
            return result;
        }

        List<Vector2I> effectCoords = _build_prefilter_effect_coords(
            context,
            skillDefinition,
            skillLevel,
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
            if (targetUnit == null || !targetUnit.IsAlive())
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
                $"{skillDefinition.SkillId}|{castVariant?.VariantId}|{Math.Sign(direction.X)},{Math.Sign(direction.Y)}|{string.Join(",", hitUnitIds)}";
        }
        return result;
    }

    private List<Vector2I> _build_prefilter_effect_coords(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        int skillLevel,
        GroundTargetCoordSet targetCoords
    )
    {
        var result = new List<Vector2I>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (context?.grid_service == null || context?.state == null || combatProfile == null)
        {
            return result;
        }
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(
                skillDefinition.SkillId,
                skillLevel
            ) ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        StringName areaPattern = effectiveDefinition.AreaPattern;
        int areaValue = Mathf.Max(effectiveDefinition.AreaValue, 0);
        var seen = new HashSet<Vector2I>();
        IEnumerable<Vector2I> targetCoordValues =
            targetCoords?.Coords ?? (IEnumerable<Vector2I>)System.Array.Empty<Vector2I>();
        foreach (Vector2I targetCoord in targetCoordValues)
        {
            Vector2I direction = context.unit_state != null
                ? targetCoord - context.unit_state.GetAnchorCoord()
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
        return targetCoords.FirstOrDefault()
            - context.unit_state.GetAnchorCoord();
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
        foreach (
            Vector2I occupiedCoord in unitState.GetOccupiedCoordsReadViewTyped()
        )
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
            if (unitState != null && unitState.IsAlive())
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
            if (unitState != null && unitState.IsAlive() && MatchesTargetFilter(context, unitState, targetFilter))
            {
                result.Add(unitState);
            }
        }
        return result;
    }

    private bool _unit_matches_any_ground_effect(
        BattleAiContext context,
        BattleUnitState targetUnit,
        CombatSkillDefinition combatProfile,
        GroundSkillEffectSet effectDefs
    )
    {
        if (targetUnit == null || combatProfile == null)
        {
            return false;
        }
        if (effectDefs == null || effectDefs.IsEmpty)
        {
            return MatchesTargetFilter(context, targetUnit, combatProfile.TargetTeamFilter);
        }
        foreach (CombatEffectDefinition effectDef in effectDefs.Effects)
        {
            if (effectDef == null)
            {
                continue;
            }
            StringName targetFilter = effectDef.EffectTargetTeamFilter != ""
                ? effectDef.EffectTargetTeamFilter
                : combatProfile.TargetTeamFilter;
            if (
                MatchesTargetFilter(context, targetUnit, targetFilter)
                && BattleEffectTargetRequirementRules.IsSatisfied(effectDef, targetUnit)
            )
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
        SkillDefinition skillDefinition,
        BattleAiScoreInput scoreInput
    )
    {
        string unitName = context?.unit_state?.display_name ?? "";
        if (_is_empty_ground_control_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDefinition.DisplayName} 控制 {scoreInput.estimated_ground_control_cell_count} 个地格（评分 {scoreInput.total_score}）。";
        }
        if (_is_ground_control_supplement_candidate(scoreInput))
        {
            return $"{unitName} 准备用 {skillDefinition.DisplayName} 覆盖 {scoreInput.effective_target_count} 个有效目标并控制 {scoreInput.estimated_ground_control_cell_count} 个地格（评分 {scoreInput.total_score}）。";
        }
        return $"{unitName} 准备用 {skillDefinition.DisplayName} 覆盖 {scoreInput.effective_target_count} 个有效目标（评分 {scoreInput.total_score}）。";
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
            || scoreInput.estimated_status_count > 0
            || scoreInput.estimated_control_count > 0
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
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContractTyped(
            context,
            skillDefinition
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
        int safeDistance = ResolveTargetSafeDistance(
            context,
            targetUnit,
            threat_minimum_safe_distance,
            threat_safe_distance_margin
        );
        foreach (BattleUnitState allyUnit in allies ?? System.Array.Empty<BattleUnitState>())
        {
            if (allyUnit == null || !allyUnit.IsAlive())
            {
                continue;
            }
            if (DistanceBetweenUnits(context, targetUnit, allyUnit) <= safeDistance)
            {
                return true;
            }
        }
        return false;
    }

    private static GroundSkillEffectSet CollectGroundSkillEffectDefs(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var effectDefs = new GroundSkillEffectSet();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile != null)
        {
            if (combatProfile.CastVariants.Count == 0)
            {
                if (castVariant != null)
                {
                    AppendEffects(effectDefs, castVariant.EffectDefinitions);
                }
                else
                {
                    AppendEffects(effectDefs, combatProfile.EffectDefinitions);
                }
                return effectDefs;
            }
            AppendEffects(effectDefs, combatProfile.EffectDefinitions);
        }
        if (castVariant != null)
        {
            AppendEffects(effectDefs, castVariant.EffectDefinitions);
        }
        return effectDefs;
    }

    private BattleUnitState _resolve_enemy_frontline_unit(BattleAiContext context)
    {
        List<BattleUnitState> targets = SortTargetUnitsTyped(
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

    private List<GroundTargetCoordSet> EnumerateGroundTargetCoordSets(
        BattleAiContext context,
        CombatCastVariantDefinition castVariant
    )
    {
        var result = new List<GroundTargetCoordSet>();
        foreach (
            List<Vector2I> coords in EnumerateGroundTargetCoordSetsTyped(
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
        IEnumerable<CombatEffectDefinition> effects
    )
    {
        if (target == null || effects == null)
        {
            return;
        }
        foreach (CombatEffectDefinition effectDef in effects)
        {
            target.Add(effectDef);
        }
    }

    private IReadOnlyList<StringName> skill_ids =>
        _action?.SkillIds ?? System.Array.Empty<StringName>();
    private int minimum_hit_count => _action?.MinimumHitCount ?? 1;
    private bool allow_empty_ground_control => _action?.AllowEmptyGroundControl ?? false;
    private bool allow_ground_control_supplement_partial_hits =>
        _action?.AllowGroundControlSupplementPartialHits ?? false;
    private int minimum_ground_control_score => _action?.MinimumGroundControlScore ?? 1;
    private int minimum_ally_threat_hit_count => _action?.MinimumAllyThreatHitCount ?? 0;
    private int maximum_friendly_fire_target_count =>
        _action?.MaximumFriendlyFireTargetCount ?? 0;
    private bool allow_friendly_lethal => _action?.AllowFriendlyLethal ?? false;
    private int threat_minimum_safe_distance => _action?.ThreatMinimumSafeDistance ?? 0;
    private int threat_safe_distance_margin => _action?.ThreatSafeDistanceMargin ?? 0;
    private int desired_min_distance => _action?.DesiredMinDistance ?? -1;
    private int desired_max_distance => _action?.DesiredMaxDistance ?? -1;
    private StringName distance_reference => _action?.DistanceReference ?? EmptyStringName;
    private EnemyAiDistanceReference DistanceReferenceKind =>
        _action?.DistanceReferenceKind ?? EnemyAiDistanceReference.None;

    private IEnumerable<BattleAvailableSkillEntry> ResolveAvailableSkillEntries(
        BattleAiContext context,
        IEnumerable<StringName> preferredSkillIds
    ) => _helper.ResolveAvailableSkillEntries(context, preferredSkillIds);

    private SkillDefinition GetSkillDefinition(BattleAiContext context, StringName skillId) =>
        _helper.GetSkillDefinition(context, skillId);

    private SkillDefinition GetSkillDefinition(
        BattleAiContext context,
        BattleAvailableSkillEntry entry
    ) => _helper.GetSkillDefinition(context, entry);

    private BattleSkillCastBlockReasonKind GetSkillCastBlockReason(
        BattleAiContext context,
        SkillDefinition skillDefinition
    ) => _helper.GetSkillCastBlockReason(context, skillDefinition);

    internal static BattleCommand BuildTypedGroundSkillCommand(
        BattleAiContext context,
        BattleAvailableSkillEntry skillEntry,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    )
    {
        if (context?.unit_state == null || skillEntry == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_entry_id = skillEntry.EntryRef.SkillEntryId,
            skill_id = skillEntry.EntryRef.SkillId,
            skill_variant_id = skillVariantId,
        };
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            command.AddTargetCoord(coord);
            if (command.target_coord == new Vector2I(-1, -1))
            {
                command.target_coord = coord;
            }
        }
        return command;
    }

    internal static BattlePreview BuildFastGroundSkillPreview(
        BattleAiContext context,
        BattleCommand command,
        IEnumerable<Vector2I> previewCoords,
        IEnumerable<StringName> targetUnitIds
    )
    {
        var preview = new BattlePreview();
        if (context?.unit_state == null || command == null)
        {
            return preview;
        }
        var seenCoords = new HashSet<Vector2I>();
        foreach (Vector2I coord in previewCoords ?? Array.Empty<Vector2I>())
        {
            if (seenCoords.Add(coord))
            {
                preview.AddTargetCoord(coord);
            }
        }
        var seenUnitIds = new HashSet<StringName>();
        foreach (StringName unitId in targetUnitIds ?? Array.Empty<StringName>())
        {
            if (unitId != "" && seenUnitIds.Add(unitId))
            {
                preview.AddTargetUnitId(unitId);
            }
        }
        preview.resolved_anchor_coord = command.target_coord;
        preview.allowed = preview.TargetCoordsTyped.Count > 0 || preview.TargetUnitIdsTyped.Count > 0;
        return preview;
    }

    private BattleAiScoreInput BuildTypedSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDefinition> effectDefinitions = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (context == null || skillDefinition == null)
            return null;
        Dictionary<string, object> scoreMetadata = CloneMetadata(metadata);
        scoreMetadata["score_bucket_id"] = _action?.ScoreBucketId ?? EmptyStringName;
        scoreMetadata["action_kind"] = ReadMetadataStringName(
            scoreMetadata,
            "action_kind",
            new StringName("skill")
        );
        scoreMetadata["action_intent"] = ResolveMetadataActionIntent(
            scoreMetadata,
            ResolveDefaultSkillActionIntent(skillDefinition, effectDefinitions)
        );
        scoreMetadata["action_label"] = ReadMetadataString(
            scoreMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDefinition.DisplayName)
                ? skillDefinition.DisplayName
                : (_action?.ActionId ?? EmptyStringName).ToString()
        );
        scoreMetadata = context.MergeCurrentActionMetadataTyped(scoreMetadata);
        scoreMetadata["score_bucket_id"] = ReadMetadataStringName(
            scoreMetadata,
            "score_bucket_id",
            _action?.ScoreBucketId ?? EmptyStringName
        );
        return context.BuildSkillScoreInputTyped(
            skillDefinition,
            command,
            preview,
            effectDefinitions,
            scoreMetadata
        );
    }

    private StringName ResolveDefaultSkillActionIntent(
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        StringName actionIntent = _action?.ActionIntent ?? EmptyStringName;
        if (
            BattleAiActionIntent.IsValid(actionIntent)
            && actionIntent != BattleAiActionIntent.Positioning
        )
        {
            return actionIntent;
        }
        return BattleAiActionIntent.InferForSkill(skillDefinition, effectDefinitions);
    }

    private static StringName ResolveMetadataActionIntent(
        IReadOnlyDictionary<string, object> metadata,
        StringName fallback
    )
    {
        StringName metadataIntent = ReadMetadataStringName(metadata, "action_intent", "");
        if (BattleAiActionIntent.IsValid(metadataIntent))
        {
            return metadataIntent;
        }
        return BattleAiActionIntent.IsValid(fallback) ? fallback : "";
    }

    private BattleAiDecision CreateDecision(BattleCommand command, string reasonText = "") =>
        EnemyAiActionHelper.CreateDecision(
            _action?.ActionId ?? EmptyStringName,
            _action?.ScoreBucketId ?? EmptyStringName,
            command,
            reasonText
        );

    private BattleAiDecision CreateScoredDecision(
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText = ""
    ) =>
        EnemyAiActionHelper.CreateScoredDecision(
            _action?.ActionId ?? EmptyStringName,
            _action?.ScoreBucketId ?? EmptyStringName,
            command,
            scoreInput,
            reasonText
        );

    private bool IsBetterSkillScoreInput(BattleAiScoreInput candidate, BattleAiScoreInput best) =>
        BattleAiDecisionEngine.IsBetterScoreInputTyped(candidate, best);

    private List<BattleUnitState> SortTargetUnitsTyped(
        BattleAiContext context,
        StringName targetFilter,
        StringName selector
    ) => _helper.SortTargetUnits(context, targetFilter, selector);

    private static bool MatchesTargetFilter(
        BattleAiContext context,
        BattleUnitState unitState,
        StringName targetFilter
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || unitState == null)
        {
            return false;
        }
        return BattleTargetTeamRules.IsUnitValidForFilter(
            actor,
            unitState,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
            )
        );
    }

    private Dictionary<string, object> ResolveDesiredDistanceContractTyped(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        int configuredMinDistance = desired_min_distance;
        int configuredMaxDistance = desired_max_distance;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMaxDistance = configuredMaxDistance;
        if (effectiveAttackRange >= 0)
            resolvedMaxDistance = effectiveAttackRange;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private static int ResolveEffectiveAttackRange(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDefinition == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillDistanceContractRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
    }

    private int ResolveTargetSafeDistance(
        BattleAiContext context,
        BattleUnitState targetUnit,
        int minimumSafeDistance,
        int safeDistanceMargin = 1
    )
    {
        int resolvedMinimum = Mathf.Max(minimumSafeDistance, 0);
        int threatRange = ResolveUnitEffectiveThreatRange(context, targetUnit);
        return threatRange <= 0
            ? resolvedMinimum
            : Mathf.Max(resolvedMinimum, threatRange + Mathf.Max(safeDistanceMargin, 0));
    }

    private int ResolveUnitEffectiveThreatRange(BattleAiContext context, BattleUnitState threatUnit)
    {
        if (context == null || threatUnit == null)
            return -1;
        int bestRange = -1;
        foreach (
            StringName rawSkillId in threatUnit.GetKnownActiveSkillsViewTyped()
        )
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
                continue;
            SkillDefinition skillDefinition = GetSkillDefinition(context, skillId);
            if (!IsHostileThreatSkill(skillDefinition))
                continue;
            bestRange = Mathf.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillThreatRange(
                    threatUnit,
                    skillDefinition,
                    context.skill_catalog
                )
            );
        }
        if (bestRange < 0)
            bestRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        return bestRange;
    }

    private static int DistanceBetweenUnits(
        BattleAiContext context,
        BattleUnitState firstUnit,
        BattleUnitState secondUnit
    ) =>
        context?.grid_service != null
            ? context.grid_service.GetDistanceBetweenUnits(firstUnit, secondUnit)
            : 999999;

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.KnowsActiveSkill(skillId)
                ? 1
                : 0;
    }

    private static bool IsHostileThreatSkill(SkillDefinition skillDefinition)
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        if (
            combatProfile.TargetFilterKind == BattleTargetFilter.Ally
            || combatProfile.TargetFilterKind == BattleTargetFilter.Self
        )
        {
            return false;
        }
        if (
            SkillHasTag(skillDefinition, "output")
            || SkillHasTag(skillDefinition, "melee")
            || SkillHasTag(skillDefinition, "bow")
            || SkillHasTag(skillDefinition, "weapon")
        )
        {
            return true;
        }
        if (EffectListHasHostileThreat(combatProfile.EffectDefinitions))
            return true;
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (castVariant != null && EffectListHasHostileThreat(castVariant.EffectDefinitions))
                return true;
        }
        return false;
    }

    private static bool SkillHasTag(SkillDefinition skillDefinition, StringName expectedTag)
    {
        return skillDefinition != null && expectedTag != "" && skillDefinition.HasTag(expectedTag);
    }

    private static bool EffectListHasHostileThreat(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effectDefinition == null)
                continue;
            if (
                effectDefinition.EffectKind == BattleEffectKind.Damage
                || effectDefinition.EffectKind == BattleEffectKind.ChainDamage
                || effectDefinition.EffectKind == BattleEffectKind.Execute
                || effectDefinition.EffectKind == BattleEffectKind.Charge
                || effectDefinition.EffectKind == BattleEffectKind.ForcedMove
                || effectDefinition.EffectKind == BattleEffectKind.PathStepAoe
                || effectDefinition.EffectKind == BattleEffectKind.Status
            )
            {
                return true;
            }
        }
        return false;
    }

    internal List<CombatCastVariantDefinition> GetGroundOptionDefinitions(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        var options = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null || combatProfile.TargetModeKind != BattleTargetMode.Ground)
            return options;
        if (combatProfile.CastVariants.Count == 0)
        {
            options.Add(BuildImplicitGroundOptionDefinition(skillDefinition));
            return options;
        }
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                options.Add(castVariant);
        }
        return options;
    }

    private CombatCastVariantDefinition BuildImplicitGroundOptionDefinition(
        SkillDefinition skillDefinition
    )
    {
        StringName skillId = ProgressionDataUtils.to_string_name(
            skillDefinition?.SkillId ?? EmptyStringName
        );
        if (
            skillId != ""
            && _implicitGroundOptionDefinitionsBySkillId.TryGetValue(
                skillId,
                out CombatCastVariantDefinition cachedOption
            )
        )
        {
            return cachedOption;
        }

        CombatSkillDefinition profile = skillDefinition?.CombatProfile;
        var option = new CombatCastVariantDefinition(
            "",
            "",
            "",
            0,
            BattleTypedNames.ToStringName(BattleTargetMode.Ground),
            CombatSkillTargetingContentRules.ToFootprintPatternId(
                CombatCastFootprintPattern.Single
            ),
            1,
            Array.Empty<StringName>(),
            profile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            null
        );
        if (skillId != "")
            _implicitGroundOptionDefinitionsBySkillId[skillId] = option;
        return option;
    }

    internal static bool IsChargeOption(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return false;
        foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effectDefinition != null && effectDefinition.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    internal List<List<Vector2I>> EnumerateGroundTargetCoordSetsTyped(
        BattleAiContext context,
        CombatCastVariantDefinition castVariant
    )
    {
        var result = new List<List<Vector2I>>();
        if (context?.state == null || context?.grid_service == null || castVariant == null)
            return result;
        BattleState state = context.state;
        BattleGridService gridService = context.grid_service;
        var seen = new HashSet<string>();
        if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Line2)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            for (int x = 0; x < state.map_size.X; x++)
            {
                var first = new Vector2I(x, y);
                foreach (Vector2I direction in new[] { Vector2I.Right, Vector2I.Down })
                {
                    Vector2I second = first + direction;
                    if (!gridService.IsInside(state, second))
                        continue;
                    List<Vector2I> pair = SortCoords(new[] { first, second });
                    string key = CoordSetKey(pair);
                    if (!seen.Add(key))
                        continue;
                    result.Add(pair);
                }
            }
        }
        else if (castVariant.FootprintPatternKind == CombatCastFootprintPattern.Square2)
        {
            for (int y = 0; y < Mathf.Max(state.map_size.Y - 1, 0); y++)
            for (int x = 0; x < Mathf.Max(state.map_size.X - 1, 0); x++)
            {
                List<Vector2I> coords = SortCoords(
                    new Vector2I[]
                    {
                        new(x, y),
                        new(x + 1, y),
                        new(x, y + 1),
                        new(x + 1, y + 1),
                    }
                );
                string key = CoordSetKey(coords);
                if (!seen.Add(key))
                    continue;
                result.Add(coords);
            }
        }
        else
        {
            for (int y = 0; y < state.map_size.Y; y++)
            for (int x = 0; x < state.map_size.X; x++)
                result.Add(new List<Vector2I> { new(x, y) });
        }
        return result;
    }

    private static List<Vector2I> SortCoords(IEnumerable<Vector2I> coords)
    {
        var sorted = new List<Vector2I>();
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            sorted.Add(coord);
        }
        sorted.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
        return sorted;
    }

    private static string CoordSetKey(IEnumerable<Vector2I> coords)
    {
        var parts = new List<string>();
        foreach (Vector2I coord in coords ?? Array.Empty<Vector2I>())
        {
            parts.Add($"{coord.X},{coord.Y}");
        }
        return string.Join("|", parts);
    }

    private AiActionTrace BeginActionTrace(
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        Dictionary<string, object> traceMetadata =
            context != null ? context.MergeCurrentActionMetadataTyped(metadata) : CloneMetadata(metadata);
        StringName resolvedScoreBucket = ProgressionDataUtils.to_string_name(
            traceMetadata.ContainsKey("score_bucket_id")
                ? traceMetadata["score_bucket_id"]
                : (_action?.ScoreBucketId ?? EmptyStringName)
        );
        return EnemyAiActionHelper.BeginActionTrace(
            _action?.ActionId ?? EmptyStringName,
            resolvedScoreBucket,
            context,
            traceMetadata
        );
    }

    private static void TraceCountIncrement(AiActionTrace actionTrace, string key, int amount = 1) =>
        EnemyAiActionHelper.TraceCountIncrement(actionTrace, key, amount);

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey) =>
        EnemyAiActionHelper.TraceAddBlockReason(actionTrace, reasonKey);

    private static void TraceOfferCandidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary,
        int keepCount = 5
    ) => EnemyAiActionHelper.TraceOfferCandidate(actionTrace, candidateSummary, keepCount);

    private static StringName FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision = null
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, bestDecision);

    private static AiCandidateSummary BuildCandidateSummary(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        IReadOnlyDictionary<string, object> extra = null
    ) => EnemyAiActionHelper.BuildCandidateSummary(label, command, scoreInput, extra);

    private static string FormatSkillVariantLabel(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    ) => EnemyAiActionHelper.FormatSkillVariantLabel(skillDefinition, castVariant);

    private static Dictionary<string, object> CloneMetadata(
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (metadata == null)
            return result;
        foreach (KeyValuePair<string, object> entry in metadata)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static StringName ReadMetadataStringName(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        StringName fallback = default
    )
    {
        if (
            metadata == null
            || string.IsNullOrEmpty(key)
            || !metadata.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback,
        };
    }

    private static string ReadMetadataString(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        string fallback = ""
    )
    {
        if (
            metadata == null
            || string.IsNullOrEmpty(key)
            || !metadata.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        return value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            Variant variant when variant.VariantType == Variant.Type.String => variant.AsString(),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName().ToString(),
            _ => value.ToString() ?? fallback,
        };
    }
}
