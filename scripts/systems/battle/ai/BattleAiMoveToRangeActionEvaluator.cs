using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMoveToRangeActionEvaluator
{
    private static readonly StringName ScreeningNoneName = "none";
    private static readonly StringName ScreeningRangedAllyName = "ranged_ally";
    private static readonly StringName AiEvaluationInlineDecideName = "inline_decide";
    private static readonly StringName AiEvaluationCandidateRequestName = "candidate_request";
    private const int HpBasisPointsDenominator = 10000;
    private const int DefaultMaxCandidateCount = 12;
    private const int DefaultAoeSetupMaxCandidateCount = 32;
    private const int ScreeningPathUnreachableCost = int.MaxValue;
    private const int PathTreeFilterMinDestinations = 4;

    private readonly BattleAiTypedActionHelper _helper = new();
    private MoveToRangeActionDefinition _action;
    private StringName action_id => _action?.ActionId ?? new StringName("");
    private StringName score_bucket_id => _action?.ScoreBucketId ?? new StringName("");
    private StringName action_intent => _action?.ActionIntent ?? new StringName("");

    internal enum MoveToRangeAiEvaluationMode
    {
        Unknown,
        InlineDecide,
        CandidateRequest,
    }

    internal enum MoveToRangeScreeningMode
    {
        Unknown,
        None,
        RangedAlly,
    }

    private sealed class ScreeningContext
    {
        public bool Enabled;
        public string Reason = "";
        public List<ScreeningThreatEntry> ThreatEntries = new();
        public Dictionary<Vector2I, ScreeningMetrics> AnchorMetricsCache = new();
        public Dictionary<ScreeningContactDestinationsKey, List<Vector2I>> ContactDestinationsCache =
            new();

        public static ScreeningContext Disabled(string reason = "")
        {
            return new ScreeningContext { Enabled = false, Reason = reason };
        }

    }

    private readonly record struct ScreeningContactDestinationsKey(
        StringName ThreatUnitId,
        StringName ProtectedUnitId,
        int ContactRange,
        bool UseBlocker,
        Vector2I BlockerAnchor
    );

    private sealed class ScreeningThreatEntry
    {
        public BattleUnitState ThreatUnit;
        public BattleUnitState ProtectedUnit;
        public int ContactRange;
        public int ThreatDistance;
        public int ThreatReach;
        public int BasePathCost;

    }

    private sealed class MoveDistanceContract
    {
        public int ConfiguredMinDistance;
        public int ConfiguredMaxDistance;
        public int DesiredMinDistance;
        public int DesiredMaxDistance;
        public int EffectiveAttackRange = -1;

        public static MoveDistanceContract FromMetadata(
            IReadOnlyDictionary<string, object> source,
            int fallbackMin,
            int fallbackMax
        )
        {
            source ??= new Dictionary<string, object>(StringComparer.Ordinal);
            int configuredMin = ReadMetadataInt(
                source,
                "configured_desired_min_distance",
                fallbackMin
            );
            int configuredMax = ReadMetadataInt(
                source,
                "configured_desired_max_distance",
                fallbackMax
            );
            int desiredMin = ReadMetadataInt(source, "desired_min_distance", configuredMin);
            int desiredMax = ReadMetadataInt(source, "desired_max_distance", configuredMax);
            return new MoveDistanceContract
            {
                ConfiguredMinDistance = configuredMin,
                ConfiguredMaxDistance = configuredMax,
                DesiredMinDistance = desiredMin,
                DesiredMaxDistance = desiredMax,
                EffectiveAttackRange = ReadMetadataInt(source, "effective_attack_range", -1),
            };
        }

        private static int ReadMetadataInt(
            IReadOnlyDictionary<string, object> source,
            string key,
            int fallback
        )
        {
            if (source == null || !source.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }
            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                float floatValue => (int)floatValue,
                double doubleValue => (int)doubleValue,
                bool boolValue => boolValue ? 1 : 0,
                string textValue when int.TryParse(textValue, out int parsed) => parsed,
                StringName stringNameValue
                    when int.TryParse(stringNameValue.ToString(), out int parsed) => parsed,
                _ => fallback,
            };
        }
    }

    private sealed class MovePathTreeCosts
    {
        private readonly Dictionary<Vector2I, int> _costs = new();
        private readonly Dictionary<Vector2I, Vector2I> _previous = new();
        private readonly Dictionary<Vector2I, int> _steps = new();

        public bool IsEmpty => _costs.Count == 0;

        public bool TryResolvePath(
            Vector2I fromCoord,
            Vector2I destination,
            out int cost,
            out List<Vector2I> path
        )
        {
            path = new List<Vector2I>();
            if (!_costs.TryGetValue(destination, out cost))
            {
                return false;
            }

            path.Add(destination);
            Vector2I cursor = destination;
            int guard = _steps.TryGetValue(destination, out int stepCount)
                ? stepCount + 1
                : _costs.Count + 1;
            while (cursor != fromCoord)
            {
                if (guard-- <= 0 || !_previous.TryGetValue(cursor, out Vector2I previousCoord))
                {
                    path.Clear();
                    return false;
                }
                cursor = previousCoord;
                path.Add(cursor);
            }
            path.Reverse();
            return path.Count > 0;
        }

        public static MovePathTreeCosts FromPathTreeResult(BattleMovePathTreeResult source)
        {
            var result = new MovePathTreeCosts();
            if (source == null)
            {
                return result;
            }
            foreach ((Vector2I coord, int cost) in source.Costs)
            {
                result._costs[coord] = cost;
            }
            foreach ((Vector2I coord, Vector2I previousCoord) in source.Previous)
            {
                result._previous[coord] = previousCoord;
            }
            foreach ((Vector2I coord, int stepCount) in source.Steps)
            {
                result._steps[coord] = stepCount;
            }
            return result;
        }
    }

    private sealed class PathProgressCandidate
    {
        public BattleAiDecision Decision;
        public BattleAiScoreInput ScoreInput;
        public int PathCost;
        public int PathLength;
        public HashSet<Vector2I> ProgressAnchors = new();
    }

    private sealed class MovePathSearchBudget
    {
        public int MaxCost;
        public int MaxNodes;
        public int MaxDestinations;
        public int PathTreeMinDestinationCount;
        public bool IncludeOrigin;
        public bool PreferProgress;

        public MoveToRangePathSearchBudget ToCandidateBudget()
        {
            return new MoveToRangePathSearchBudget
            {
                MaxCost = MaxCost,
                MaxNodes = MaxNodes,
                MaxDestinations = MaxDestinations,
                PathTreeMinDestinationCount = PathTreeMinDestinationCount,
                IncludeOrigin = IncludeOrigin,
                PreferProgress = PreferProgress,
            };
        }

        public static MovePathSearchBudget FromSnapshot(
            BattleMovementQueryService.PathSearchBudgetSnapshot source
        )
        {
            return new MovePathSearchBudget
            {
                MaxCost = source.MaxCost,
                MaxNodes = source.MaxNodes,
                MaxDestinations = source.MaxDestinations,
                PathTreeMinDestinationCount = source.PathTreeMinDestinationCount,
                IncludeOrigin = source.IncludeOrigin,
                PreferProgress = source.PreferProgress,
            };
        }
    }

    private sealed class MoveSkillRecord
    {
        public BattleTargetMode TargetMode = BattleTargetMode.Unknown;
        public BattleTargetSelectionMode TargetSelectionMode = BattleTargetSelectionMode.Unknown;
        public BattleAreaPattern AreaPattern = BattleAreaPattern.Unknown;
        public int AreaValue;
        public int ActorEffectiveRange = -1;
        public int ActorEffectiveCastRange = -1;

        public bool IsGroundAoe =>
            TargetMode == BattleTargetMode.Ground
            && AreaValue > 0
            && AreaPattern != BattleAreaPattern.Unknown
            && AreaPattern != BattleAreaPattern.Single
            && AreaPattern != BattleAreaPattern.Self;

        public bool IsRandomChain =>
            TargetMode == BattleTargetMode.Unit
            && TargetSelectionMode == BattleTargetSelectionMode.RandomChain;

        public bool IsSetupSkill => IsGroundAoe || IsRandomChain;

        public static MoveSkillRecord FromSkillRecord(BattleAiQueryService.SkillRecord source)
        {
            if (source == null)
            {
                return new MoveSkillRecord();
            }
            return new MoveSkillRecord
            {
                TargetMode = source.target_mode,
                TargetSelectionMode = source.target_selection_mode,
                AreaPattern = source.area_pattern,
                AreaValue = source.area_value,
                ActorEffectiveRange = source.actor_effective_range,
                ActorEffectiveCastRange = source.actor_effective_cast_range,
            };
        }
    }

    private sealed class ScreeningMetrics
    {
        public int Bonus;
        public string ThreatUnitId = "";
        public string ProtectedUnitId = "";
        public int AnchorToThreat;
        public int AnchorToProtected;
        public int ThreatDistance;
        public int BasePathCost = ScreeningPathUnreachableCost;
        public int BlockedPathCost = ScreeningPathUnreachableCost;
        public int PathCostDelta;
        public bool HardBlock;
        public bool OnShortestPath;
        public bool KeepsContact;
        public bool CanCounterattack;
        public int Penalty;
        public int CurrentBonus;
        public int CandidateBonus;
        public int LostBonus;
        public int UncappedBonus;
        public bool DistanceBandCapped;

        public ScreeningMetrics Clone()
        {
            return (ScreeningMetrics)MemberwiseClone();
        }

    }

    private StringName ai_evaluation_mode => _action?.AiEvaluationMode ?? AiEvaluationInlineDecideName;
    private StringName target_selector => _action?.TargetSelector ?? new StringName("nearest_enemy");
    private int desired_min_distance => _action?.DesiredMinDistance ?? 1;
    private int desired_max_distance => _action?.DesiredMaxDistance ?? 1;
    private IReadOnlyList<StringName> range_skill_ids =>
        _action?.RangeSkillIds ?? Array.Empty<StringName>();
    private StringName screening_mode => _action?.ScreeningMode ?? ScreeningNoneName;
    private bool enable_aoe_setup_positioning => _action?.EnableAoeSetupPositioning ?? true;
    private int aoe_setup_min_target_count => _action?.AoeSetupMinTargetCount ?? 2;
    private int aoe_setup_target_count_weight => _action?.AoeSetupTargetCountWeight ?? 140;
    private int aoe_setup_improvement_weight => _action?.AoeSetupImprovementWeight ?? 220;
    private int aoe_setup_friendly_fire_penalty => _action?.AoeSetupFriendlyFirePenalty ?? 1000;
    private int screening_min_hp_basis_points => _action?.ScreeningMinHpBasisPoints ?? 4000;
    private int screening_ally_min_attack_range => _action?.ScreeningAllyMinAttackRange ?? 4;
    private int screening_enemy_max_contact_range => _action?.ScreeningEnemyMaxContactRange ?? 2;
    private int screening_threat_distance_buffer => _action?.ScreeningThreatDistanceBuffer ?? 2;
    private int screening_path_bonus => _action?.ScreeningPathBonus ?? 45;

    private MoveToRangeAiEvaluationMode AiEvaluationModeKind =>
        ToAiEvaluationMode(ai_evaluation_mode);

    private MoveToRangeScreeningMode ScreeningModeKind => ToScreeningMode(screening_mode);

    internal BattleAiDecision Evaluate(
        MoveToRangeActionDefinition action,
        BattleAiContext context,
        bool forceCandidateRequestEvaluation = false
    )
    {
        _action = action;
        if (_action == null || context == null)
        {
            return null;
        }
        try
        {
            using BattleAiTraceSpan trace = new("decide:move_to_range");
            if (UsesCandidateRequest(forceCandidateRequestEvaluation))
            {
                BattleAiCandidateRequest request = BuildCandidateRequest(
                    context.ai_query_service
                );
                return request != null ? context.EvaluateCandidateRequest(request) : null;
            }
            return DecideImpl(context);
        }
        finally
        {
            _action = null;
        }
    }

    private bool UsesCandidateRequest(bool forceCandidateRequestEvaluation)
    {
        return forceCandidateRequestEvaluation
            || AiEvaluationModeKind == MoveToRangeAiEvaluationMode.CandidateRequest;
    }

    private BattleAiCandidateRequest BuildCandidateRequest(BattleAiQueryService query)
    {
        if (query == null)
        {
            return null;
        }

        StringName actorId = query.GetActorId();
        BattleAiUnitSnapshot actorSnapshot = query.GetActorSnapshot();
        BattleAiUnitSnapshot focusTarget = ResolveFocusTarget(query, actorSnapshot);
        if (actorId == "" || actorSnapshot == null || focusTarget == null)
        {
            return null;
        }

        bool aoeSetupEnabled = HasGroundAoeSetupSkill(query);
        int effectiveAttackRange = ResolveEffectiveAttackRange(query, aoeSetupEnabled);
        int resolvedMinDistance = Mathf.Max(desired_min_distance, 0);
        int resolvedMaxDistance = desired_max_distance;
        if (effectiveAttackRange >= 0)
        {
            resolvedMaxDistance = effectiveAttackRange;
        }
        resolvedMaxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance);

        int maxCandidateCount = ResolveMaxCandidateCount(actorSnapshot);
        if (aoeSetupEnabled)
        {
            maxCandidateCount = Mathf.Max(maxCandidateCount, DefaultAoeSetupMaxCandidateCount);
        }
        MovePathSearchBudget pathBudget = BuildPathBudget(query, actorId, maxCandidateCount);
        if (pathBudget.MaxDestinations > 0)
        {
            maxCandidateCount = Mathf.Min(maxCandidateCount, pathBudget.MaxDestinations);
        }

        var request = new BattleAiCandidateRequest
        {
            FamilyId = "move_to_range",
            ActionId = action_id,
            ActionLabel = action_id.ToString(),
            ActionIntent = action_intent,
            ScoreBucketId = score_bucket_id,
            ActorUnitId = actorId,
            FocusTargetUnitId = focusTarget.unit_id,
            DesiredMinDistance = resolvedMinDistance,
            DesiredMaxDistance = resolvedMaxDistance,
            MaxCandidateCount = Mathf.Max(maxCandidateCount, 1),
        };
        request.SetMoveToRangeSections(
            pathBudget.ToCandidateBudget(),
            new MoveToRangeTacticalParams
            {
                TargetSelector = target_selector,
                RangeSkillIds = new List<StringName>(range_skill_ids),
                PositionObjectiveKind = "distance_band_progress",
                AoeSetupEnabled = aoeSetupEnabled,
                AoeSetupMinTargetCount = Mathf.Max(aoe_setup_min_target_count, 1),
                AoeSetupTargetCountWeight = Mathf.Max(aoe_setup_target_count_weight, 0),
                AoeSetupImprovementWeight = Mathf.Max(aoe_setup_improvement_weight, 0),
                AoeSetupFriendlyFirePenalty = Mathf.Max(
                    aoe_setup_friendly_fire_penalty,
                    0
                ),
            },
            new MoveToRangeRuntimeMetadata
            {
                ConfiguredDesiredMinDistance = desired_min_distance,
                ConfiguredDesiredMaxDistance = desired_max_distance,
                EffectiveAttackRange = effectiveAttackRange,
            }
        );
        return request;
    }

    private BattleAiDecision DecideImpl(BattleAiContext context)
    {
        MoveDistanceContract distanceContract = ResolveMoveDistanceContract(context);
        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        AiActionTrace actionTrace = context?.trace_enabled == true
            ? _begin_action_trace(
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "move_to_range",
                    ["target_selector"] = target_selector.ToString(),
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["configured_desired_min_distance"] = desired_min_distance,
                    ["configured_desired_max_distance"] = desired_max_distance,
                    ["effective_attack_range"] = distanceContract.EffectiveAttackRange,
                    ["range_skill_ids"] = new List<StringName>(range_skill_ids),
                    ["screening_mode"] = screening_mode.ToString(),
                }
            )
            : null;

        List<BattleUnitState> targets = _sort_target_units_typed(context, "enemy", target_selector);
        if (targets.Count == 0)
        {
            _trace_add_block_reason(actionTrace, "no_valid_targets");
            _finalize_action_trace(context, actionTrace);
            return null;
        }

        var focusTarget = targets[0];
        var actor = GetContextUnit(context);
        if (actor == null || focusTarget == null)
        {
            _trace_add_block_reason(actionTrace, "missing_context");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        if (_is_unit_movement_blocked(context, actor))
        {
            _trace_add_block_reason(actionTrace, "movement_blocked");
            _finalize_action_trace(context, actionTrace);
            return null;
        }
        int moveBudget = _resolve_current_move_budget(actor);
        if (moveBudget <= 0)
        {
            _trace_add_block_reason(
                actionTrace,
                _is_normal_movement_locked(actor) ? "movement_locked" : "no_move_budget"
            );
            _finalize_action_trace(context, actionTrace);
            return null;
        }

        ScreeningContext screeningContext = BuildScreeningContext(context);
        BattleAiScoreInput currentScoreInput = _build_typed_action_score_input(
            context,
            "move",
            action_id.ToString(),
            null,
            null,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["position_target_unit_id"] = focusTarget.unit_id,
                ["position_anchor_coord"] = actor.coord,
                ["desired_min_distance"] = resolvedMinDistance,
                ["desired_max_distance"] = resolvedMaxDistance,
                ["position_objective_kind"] = new StringName("distance_band_progress"),
                ["move_cost"] = 0,
            }
        );
        ApplyScreeningScore(context, currentScoreInput, actor.coord, screeningContext);

        PathProgressCandidate pathProgressCandidate = BuildPathProgressDecision(
            context,
            focusTarget,
            actionTrace,
            distanceContract,
            screeningContext
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = currentScoreInput;
        int bestPathCost = 0;
        int bestPathLength = 0;
        if (
            pathProgressCandidate != null
            && MoveToRangeScoreOrdering.IsBetterCandidate(
                pathProgressCandidate.ScoreInput,
                pathProgressCandidate.PathCost,
                pathProgressCandidate.PathLength,
                bestScoreInput,
                bestPathCost,
                bestPathLength
            )
        )
        {
            bestScoreInput = pathProgressCandidate.ScoreInput;
            bestPathCost = pathProgressCandidate.PathCost;
            bestPathLength = pathProgressCandidate.PathLength;
            bestDecision = pathProgressCandidate.Decision;
        }

        foreach (Vector2I neighbor in CollectReachableMoveCandidates(context))
        {
            _trace_count_increment(actionTrace, "evaluation_count", 1);
            if (
                !ShouldReachableCandidateChallengePathProgress(
                    pathProgressCandidate,
                    neighbor
                )
            )
            {
                _trace_count_increment(
                    actionTrace,
                    "path_progress_reachable_guard_skip_count",
                    1
                );
                continue;
            }

            BattleCommand command = _build_move_command(context, neighbor);
            BattlePreview preview = _build_fast_typed_move_preview(context, neighbor);
            if (preview?.allowed != true)
            {
                _trace_count_increment(actionTrace, "preview_reject_count", 1);
                continue;
            }

            BattleAiScoreInput scoreInput = _build_typed_action_score_input(
                context,
                "move",
                action_id.ToString(),
                command,
                preview,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["position_target_unit_id"] = focusTarget.unit_id,
                    ["position_anchor_coord"] = neighbor,
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["position_objective_kind"] = new StringName("distance_band_progress"),
                }
            );
            ScreeningMetrics screeningMetrics = ApplyScreeningScore(
                context,
                scoreInput,
                neighbor,
                screeningContext
            );
            if (actionTrace != null)
            {
                _trace_offer_candidate(
                    actionTrace,
                    _build_candidate_summary(
                        $"move_to_{neighbor.X}_{neighbor.Y}",
                        command,
                        scoreInput,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["predicted_distance"] = scoreInput is BattleAiScoreInput typed
                                ? typed.distance_to_primary_coord
                                : -1,
                            ["screening_bonus"] = screeningMetrics.Bonus,
                            ["screening_penalty"] = screeningMetrics.Penalty,
                            ["screening_threat_unit_id"] = screeningMetrics.ThreatUnitId,
                            ["screening_protected_unit_id"] = screeningMetrics.ProtectedUnitId,
                            ["screening_path_cost_delta"] = screeningMetrics.PathCostDelta,
                            ["screening_base_path_cost"] = screeningMetrics.BasePathCost,
                            ["screening_blocked_path_cost"] = screeningMetrics.BlockedPathCost,
                            ["screening_current_bonus"] = screeningMetrics.CurrentBonus,
                            ["screening_candidate_bonus"] = screeningMetrics.CandidateBonus,
                            ["screening_uncapped_bonus"] = screeningMetrics.UncappedBonus,
                            ["screening_on_shortest_path"] = screeningMetrics.OnShortestPath,
                            ["screening_keeps_contact"] = screeningMetrics.KeepsContact,
                            ["screening_can_counterattack"] = screeningMetrics.CanCounterattack,
                            ["screening_hard_block"] = screeningMetrics.HardBlock,
                            ["screening_distance_band_capped"] =
                                screeningMetrics.DistanceBandCapped,
                        }
                    )
                );
            }

            if (
                !MoveToRangeScoreOrdering.IsBetterCandidate(
                    scoreInput,
                    MoveToRangeScoreOrdering.InfiniteTieBreaker,
                    MoveToRangeScoreOrdering.InfiniteTieBreaker,
                    bestScoreInput,
                    bestPathCost,
                    bestPathLength
                )
            )
            {
                continue;
            }

            bestScoreInput = scoreInput;
            bestPathCost = MoveToRangeScoreOrdering.InfiniteTieBreaker;
            bestPathLength = MoveToRangeScoreOrdering.InfiniteTieBreaker;
            int distance = scoreInput is BattleAiScoreInput moveScore
                ? moveScore.distance_to_primary_coord
                : -1;
            bestDecision = _create_scored_decision(
                command,
                scoreInput,
                $"{actor.display_name} 准备调整到距离 {focusTarget.display_name} {distance} 格（评分 {_score_total(scoreInput)}）。"
            );
        }

        if (bestDecision == null && pathProgressCandidate?.Decision != null)
        {
            bestDecision = pathProgressCandidate.Decision;
        }
        _finalize_action_trace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    private static bool ShouldReachableCandidateChallengePathProgress(
        PathProgressCandidate pathProgressCandidate,
        Vector2I candidateCoord
    )
    {
        if (pathProgressCandidate?.Decision == null)
        {
            return true;
        }
        return pathProgressCandidate.ProgressAnchors.Contains(candidateCoord);
    }

    private List<Vector2I> CollectReachableMoveCandidates(BattleAiContext context)
    {
        using BattleAiTraceSpan trace = new("_collect_reachable_move_candidates");
        return CollectReachableMoveCandidatesImpl(context);
    }

    private List<Vector2I> CollectReachableMoveCandidatesImpl(
        BattleAiContext context
    )
    {
        var candidates = new List<Vector2I>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return candidates;
        }
        if (_is_unit_movement_blocked(context, actor))
        {
            return candidates;
        }

        var seen = new HashSet<Vector2I>();
        Vector2I origin = actor.coord;
        int maxMovePoints = _resolve_current_move_budget(actor);
        if (maxMovePoints <= 0)
        {
            return candidates;
        }
        var frontier = new Queue<(Vector2I Coord, int Cost)>();
        var bestCosts = new Dictionary<Vector2I, int> { [origin] = 0 };
        frontier.Enqueue((origin, 0));
        while (frontier.Count > 0)
        {
            (Vector2I currentCoord, int currentCost) = frontier.Dequeue();
            if (
                !bestCosts.TryGetValue(currentCoord, out int bestCurrentCost)
                || currentCost != bestCurrentCost
            )
            {
                continue;
            }
            foreach (Vector2I neighbor in grid.GetNeighbors4(state, currentCoord))
            {
                if (!grid.CanUnitStepBetweenAnchors(state, actor, currentCoord, neighbor))
                {
                    continue;
                }
                int nextCost = currentCost + GetMoveCost(context, actor, neighbor);
                if (nextCost > maxMovePoints)
                {
                    continue;
                }
                if (
                    bestCosts.TryGetValue(neighbor, out int bestNeighborCost)
                    && nextCost >= bestNeighborCost
                )
                {
                    continue;
                }
                bestCosts[neighbor] = nextCost;
                frontier.Enqueue((neighbor, nextCost));
                if (seen.Add(neighbor))
                {
                    candidates.Add(neighbor);
                }
            }
        }

        candidates.Sort(
            (left, right) =>
            {
                int leftDistance = grid.GetDistance(origin, left);
                int rightDistance = grid.GetDistance(origin, right);
                if (leftDistance == rightDistance)
                {
                    if (left.Y != right.Y)
                    {
                        return left.Y.CompareTo(right.Y);
                    }
                    return left.X.CompareTo(right.X);
                }
                return rightDistance.CompareTo(leftDistance);
            }
        );
        return candidates;
    }

    private ScreeningContext BuildScreeningContext(BattleAiContext context)
    {
        using BattleAiTraceSpan trace = new("build_screening_context");
        return BuildScreeningContextImpl(context);
    }

    private ScreeningMetrics ApplyScreeningScore(
        BattleAiContext context,
        BattleAiScoreInput scoreInput,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        using BattleAiTraceSpan trace = new("apply_screening_score");
        return ApplyScreeningScoreImpl(
            context,
            scoreInput,
            anchorCoord,
            screeningContext
        );
    }

    private ScreeningMetrics ApplyScreeningScoreImpl(
        BattleAiContext context,
        BattleAiScoreInput scoreInput,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        ScreeningMetrics metrics = BuildScreeningMetrics(
            context,
            anchorCoord,
            screeningContext,
            scoreInput
        );
        if (scoreInput is BattleAiScoreInput typed)
        {
            typed.total_score += metrics.Bonus;
            typed.position_objective_score += metrics.Bonus;
        }
        return metrics;
    }

    private ScreeningContext BuildScreeningContextImpl(BattleAiContext context)
    {
        if (ScreeningModeKind != MoveToRangeScreeningMode.RangedAlly)
        {
            return ScreeningContext.Disabled();
        }
        if (
            GetContextState(context) == null
            || GetContextUnit(context) == null
            || GetContextGrid(context) == null
        )
        {
            return ScreeningContext.Disabled();
        }
        if (
            _get_hp_basis_points(GetContextUnit(context))
            < Mathf.Max(screening_min_hp_basis_points, 0)
        )
        {
            return ScreeningContext.Disabled("low_hp");
        }

        List<BattleUnitState> protectedAllies = CollectScreeningProtectedAllies(context);
        if (protectedAllies.Count == 0)
        {
            return ScreeningContext.Disabled("no_protected_allies");
        }

        List<ScreeningThreatEntry> threatEntries = CollectScreeningThreatEntries(
            context,
            protectedAllies
        );
        if (threatEntries.Count == 0)
        {
            return ScreeningContext.Disabled("no_contact_threats");
        }

        return new ScreeningContext
        {
            Enabled = true,
            ThreatEntries = threatEntries,
        };
    }

    private static MoveToRangeAiEvaluationMode ToAiEvaluationMode(StringName mode)
    {
        if (mode == AiEvaluationInlineDecideName)
        {
            return MoveToRangeAiEvaluationMode.InlineDecide;
        }
        if (mode == AiEvaluationCandidateRequestName)
        {
            return MoveToRangeAiEvaluationMode.CandidateRequest;
        }
        return MoveToRangeAiEvaluationMode.Unknown;
    }

    private static MoveToRangeScreeningMode ToScreeningMode(StringName mode)
    {
        if (mode == ScreeningNoneName)
        {
            return MoveToRangeScreeningMode.None;
        }
        if (mode == ScreeningRangedAllyName)
        {
            return MoveToRangeScreeningMode.RangedAlly;
        }
        return MoveToRangeScreeningMode.Unknown;
    }

    internal static StringName ToStringName(MoveToRangeAiEvaluationMode mode)
    {
        return mode switch
        {
            MoveToRangeAiEvaluationMode.InlineDecide => AiEvaluationInlineDecideName,
            MoveToRangeAiEvaluationMode.CandidateRequest => AiEvaluationCandidateRequestName,
            _ => "",
        };
    }

    internal static StringName ToStringName(MoveToRangeScreeningMode mode)
    {
        return mode switch
        {
            MoveToRangeScreeningMode.None => ScreeningNoneName,
            MoveToRangeScreeningMode.RangedAlly => ScreeningRangedAllyName,
            _ => "",
        };
    }

    private List<BattleUnitState> CollectScreeningProtectedAllies(BattleAiContext context)
    {
        var allies = new List<BattleUnitState>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        if (state == null || actor == null)
        {
            return allies;
        }

        foreach (BattleUnitState allyUnit in state.GetUnitsTyped())
        {
            if (allyUnit == null || !allyUnit.is_alive)
            {
                continue;
            }
            if (allyUnit.unit_id == actor.unit_id || allyUnit.faction_id != actor.faction_id)
            {
                continue;
            }
            int allyAttackRange = _resolve_unit_effective_threat_range(context, allyUnit);
            if (allyAttackRange < Mathf.Max(screening_ally_min_attack_range, 1))
            {
                continue;
            }
            allies.Add(allyUnit);
        }
        return allies;
    }

    private List<ScreeningThreatEntry> CollectScreeningThreatEntries(
        BattleAiContext context,
        List<BattleUnitState> protectedAllies
    )
    {
        var threatEntries = new List<ScreeningThreatEntry>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        if (state == null || actor == null)
        {
            return threatEntries;
        }

        foreach (BattleUnitState threatUnit in state.GetUnitsTyped())
        {
            if (threatUnit == null || !threatUnit.is_alive)
            {
                continue;
            }
            if (threatUnit.faction_id == actor.faction_id)
            {
                continue;
            }

            int contactRange = ResolveScreeningUnitContactThreatRange(context, threatUnit);
            if (contactRange <= 0)
            {
                continue;
            }

            foreach (BattleUnitState protectedUnit in protectedAllies)
            {
                if (protectedUnit == null)
                {
                    continue;
                }
                int threatDistance = _distance_between_units(context, threatUnit, protectedUnit);
                int threatReach =
                    contactRange
                    + Mathf.Max(threatUnit.current_move_points, 0)
                    + Mathf.Max(screening_threat_distance_buffer, 0);
                if (threatDistance > threatReach)
                {
                    continue;
                }
                int basePathCost = ResolveScreeningThreatPathCost(
                    context,
                    threatUnit,
                    protectedUnit,
                    contactRange,
                    new Vector2I(-999999, -999999),
                    false
                );
                if (basePathCost >= ScreeningPathUnreachableCost)
                {
                    continue;
                }
                if (basePathCost > threatReach)
                {
                    continue;
                }
                threatEntries.Add(
                    new ScreeningThreatEntry
                    {
                        ThreatUnit = threatUnit,
                        ProtectedUnit = protectedUnit,
                        ContactRange = contactRange,
                        ThreatDistance = threatDistance,
                        ThreatReach = threatReach,
                        BasePathCost = basePathCost,
                    }
                );
            }
        }
        return threatEntries;
    }

    private int ResolveScreeningUnitContactThreatRange(
        BattleAiContext context,
        BattleUnitState threatUnit
    )
    {
        if (context == null || threatUnit == null)
        {
            return -1;
        }
        int bestRange = -1;
        foreach (StringName rawSkillId in threatUnit.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
            {
                continue;
            }
            SkillDefinition skillDefinition = context.GetSkillDefinitionTyped(skillId);
            if (skillDefinition?.CombatProfile is not CombatSkillDefinition combatProfile)
            {
                continue;
            }
            if (
                combatProfile.TargetFilterKind == BattleTargetFilter.Ally
                || combatProfile.TargetFilterKind == BattleTargetFilter.Self
            )
            {
                continue;
            }
            if (
                !_skill_has_tag(skillDefinition, "melee")
                && !_skill_has_tag(skillDefinition, "weapon")
            )
            {
                continue;
            }
            int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
                threatUnit,
                skillDefinition,
                context.skill_catalog
            );
            if (effectiveRange > Mathf.Max(screening_enemy_max_contact_range, 1))
            {
                continue;
            }
            bestRange = Mathf.Max(bestRange, effectiveRange);
        }

        int weaponRange = BattleRangeService.GetWeaponAttackRange(threatUnit);
        if (weaponRange > 0 && weaponRange <= Mathf.Max(screening_enemy_max_contact_range, 1))
        {
            bestRange = Mathf.Max(bestRange, weaponRange);
        }
        return bestRange;
    }

    private ScreeningMetrics BuildScreeningMetrics(
        BattleAiContext context,
        Vector2I anchorCoord,
        ScreeningContext screeningContext,
        BattleAiScoreInput scoreInput
    )
    {
        if (GetContextUnit(context) == null || screeningContext == null || !screeningContext.Enabled)
        {
            return new ScreeningMetrics();
        }

        ScreeningMetrics bestMetrics = new();
        ScreeningMetrics currentMetrics = BuildBestScreeningAnchorMetrics(
            context,
            GetContextUnit(context).coord,
            screeningContext
        );
        ScreeningMetrics candidateMetrics = BuildBestScreeningAnchorMetrics(
            context,
            anchorCoord,
            screeningContext
        );
        ApplyScreeningDistanceBandCap(candidateMetrics, scoreInput);

        int candidateBonus = candidateMetrics.Bonus;
        int currentBonus = currentMetrics.Bonus;
        if (candidateBonus <= currentBonus)
        {
            if (currentBonus > 0 && candidateBonus < currentBonus)
            {
                int penalty = Mathf.Min(
                    currentBonus - candidateBonus,
                    Mathf.Max(Mathf.Max(screening_path_bonus, 0) / 2, 1)
                );
                ScreeningMetrics penaltyMetrics = candidateMetrics.Clone();
                if (string.IsNullOrEmpty(penaltyMetrics.ThreatUnitId))
                {
                    penaltyMetrics.ThreatUnitId = currentMetrics.ThreatUnitId;
                }
                if (string.IsNullOrEmpty(penaltyMetrics.ProtectedUnitId))
                {
                    penaltyMetrics.ProtectedUnitId = currentMetrics.ProtectedUnitId;
                }
                penaltyMetrics.Bonus = -penalty;
                penaltyMetrics.Penalty = penalty;
                penaltyMetrics.CurrentBonus = currentBonus;
                penaltyMetrics.CandidateBonus = candidateBonus;
                penaltyMetrics.LostBonus = currentBonus - candidateBonus;
                return penaltyMetrics;
            }
            return bestMetrics;
        }

        bestMetrics = candidateMetrics.Clone();
        bestMetrics.Bonus = candidateBonus - currentBonus;
        bestMetrics.CurrentBonus = currentBonus;
        bestMetrics.CandidateBonus = candidateBonus;
        return bestMetrics;
    }

    private ScreeningMetrics BuildBestScreeningAnchorMetrics(
        BattleAiContext context,
        Vector2I anchorCoord,
        ScreeningContext screeningContext
    )
    {
        if (screeningContext == null)
        {
            return new ScreeningMetrics();
        }
        if (screeningContext.AnchorMetricsCache.TryGetValue(anchorCoord, out ScreeningMetrics cached))
        {
            return cached.Clone();
        }

        ScreeningMetrics bestMetrics = new();
        BattleUnitState actor = GetContextUnit(context);
        foreach (ScreeningThreatEntry entry in screeningContext.ThreatEntries)
        {
            BattleUnitState threatUnit = entry?.ThreatUnit;
            BattleUnitState protectedUnit = entry?.ProtectedUnit;
            if (actor == null || threatUnit == null || protectedUnit == null)
            {
                continue;
            }

            int anchorToThreat = _distance_from_anchor_to_unit(
                context,
                actor,
                anchorCoord,
                threatUnit
            );
            int anchorToProtected = _distance_from_anchor_to_unit(
                context,
                actor,
                anchorCoord,
                protectedUnit
            );
            bool onShortestPath = anchorToThreat + anchorToProtected == entry.ThreatDistance;
            bool keepsContact =
                anchorToThreat <= Mathf.Max(desired_max_distance, entry.ContactRange);
            int ownContactRange = ResolveScreeningUnitContactThreatRange(context, actor);
            if (ownContactRange <= 0)
            {
                ownContactRange = Mathf.Max(desired_max_distance, 1);
            }
            bool canCounterattack = anchorToThreat <= ownContactRange;
            int blockedPathCost = ResolveScreeningThreatPathCost(
                context,
                threatUnit,
                protectedUnit,
                entry.ContactRange,
                anchorCoord,
                true,
                screeningContext
            );
            int pathCostDelta = CalculateScreeningPathCostDelta(
                entry.BasePathCost,
                blockedPathCost,
                entry.ThreatReach
            );
            bool increasesPathCost = pathCostDelta > 0;
            bool hardBlock = blockedPathCost >= ScreeningPathUnreachableCost;
            bool canProjectPressure = keepsContact || canCounterattack;
            if (!increasesPathCost && !canProjectPressure)
            {
                continue;
            }

            int bonus = 0;
            if (increasesPathCost)
            {
                if (canProjectPressure)
                {
                    bonus += Mathf.Max(screening_path_bonus, 0);
                    if (pathCostDelta > 1)
                    {
                        bonus +=
                            Mathf.Min(pathCostDelta - 1, 2)
                            * (Mathf.Max(screening_path_bonus, 0) / 3);
                    }
                }
                else
                {
                    if (pathCostDelta < 2 && !hardBlock)
                    {
                        continue;
                    }
                    bonus += Mathf.Max(screening_path_bonus, 0) / 2;
                    if (hardBlock)
                    {
                        bonus += Mathf.Max(screening_path_bonus, 0) / 3;
                    }
                    else if (pathCostDelta > 2)
                    {
                        bonus +=
                            Mathf.Min(pathCostDelta - 2, 2)
                            * (Mathf.Max(screening_path_bonus, 0) / 6);
                    }
                }
            }
            if (keepsContact)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            else if (canCounterattack)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            if (onShortestPath && canProjectPressure && !increasesPathCost)
            {
                bonus += Mathf.Max(screening_path_bonus, 0) / 3;
            }
            if (bonus <= 0)
            {
                continue;
            }
            if (bonus < bestMetrics.Bonus)
            {
                continue;
            }
            if (bonus == bestMetrics.Bonus && pathCostDelta <= bestMetrics.PathCostDelta)
            {
                continue;
            }
            bestMetrics = new ScreeningMetrics
            {
                Bonus = bonus,
                ThreatUnitId = threatUnit.unit_id.ToString(),
                ProtectedUnitId = protectedUnit.unit_id.ToString(),
                AnchorToThreat = anchorToThreat,
                AnchorToProtected = anchorToProtected,
                ThreatDistance = entry.ThreatDistance,
                BasePathCost = entry.BasePathCost,
                BlockedPathCost = blockedPathCost,
                PathCostDelta = pathCostDelta,
                HardBlock = hardBlock,
                OnShortestPath = onShortestPath,
                KeepsContact = keepsContact,
                CanCounterattack = canCounterattack,
            };
        }

        screeningContext.AnchorMetricsCache[anchorCoord] = bestMetrics.Clone();
        return bestMetrics.Clone();
    }

    private void ApplyScreeningDistanceBandCap(ScreeningMetrics metrics, BattleAiScoreInput scoreInput)
    {
        if (metrics == null || scoreInput == null)
        {
            return;
        }
        if (metrics.HardBlock)
        {
            return;
        }
        if (GetScoreInputDistanceGap(scoreInput) <= 0)
        {
            return;
        }
        int bonus = metrics.Bonus;
        int cap = Mathf.Max(screening_path_bonus, 0) / 3;
        if (cap <= 0 || bonus <= cap)
        {
            return;
        }
        metrics.UncappedBonus = bonus;
        metrics.Bonus = cap;
        metrics.DistanceBandCapped = true;
    }

    private static int CalculateScreeningPathCostDelta(
        int basePathCost,
        int blockedPathCost,
        int threatReach
    )
    {
        if (basePathCost >= ScreeningPathUnreachableCost)
        {
            return 0;
        }
        if (blockedPathCost >= ScreeningPathUnreachableCost)
        {
            return Mathf.Max(threatReach - basePathCost + 1, 1);
        }
        return Mathf.Max(blockedPathCost - basePathCost, 0);
    }

    private int ResolveScreeningThreatPathCost(
        BattleAiContext context,
        BattleUnitState threatUnit,
        BattleUnitState protectedUnit,
        int contactRange,
        Vector2I blockerAnchor,
        bool useBlocker,
        ScreeningContext screeningContext = null
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (
            state == null
            || actor == null
            || grid == null
            || threatUnit == null
            || protectedUnit == null
        )
        {
            return ScreeningPathUnreachableCost;
        }

        List<Vector2I> blockerCoords = useBlocker
            ? new List<Vector2I>(grid.GetUnitTargetCoords(actor, blockerAnchor))
            : new List<Vector2I>();
        List<Vector2I> restoreCoords = new();
        actor.RefreshFootprint();
        foreach (Vector2I coord in actor.occupied_coords)
        {
            restoreCoords.Add(coord);
        }
        foreach (Vector2I coord in blockerCoords)
        {
            restoreCoords.Add(coord);
        }

        Dictionary<Vector2I, StringName> occupantSnapshot = SnapshotScreeningOccupants(
            context,
            restoreCoords
        );
        try
        {
            grid.SetOccupantsTyped(state, actor.occupied_coords, "", mark_revision: false);
            if (useBlocker)
            {
                grid.SetOccupantsTyped(state, blockerCoords, actor.unit_id, mark_revision: false);
            }

            IReadOnlyList<Vector2I> destinations =
                CollectScreeningThreatContactDestinationList(
                    context,
                    threatUnit,
                    protectedUnit,
                    contactRange,
                    useBlocker,
                    blockerAnchor,
                    screeningContext
                );
            if (destinations.Count == 0)
            {
                return ScreeningPathUnreachableCost;
            }

            int bestCost = ScreeningPathUnreachableCost;
            int pathBudget = BuildPathSearchBudget(context);
            foreach (Vector2I destination in destinations)
            {
                BattleMovePathResult resolvedPath = grid.ResolveUnitMovePathTyped(
                    state,
                    threatUnit,
                    threatUnit.coord,
                    destination,
                    pathBudget,
                    BuildMoveCostProvider(context)
                );
                if (!resolvedPath.Allowed)
                {
                    continue;
                }
                bestCost = Mathf.Min(bestCost, resolvedPath.Cost);
            }
            return bestCost;
        }
        finally
        {
            RestoreScreeningOccupants(context, occupantSnapshot);
        }
    }

    private IReadOnlyList<Vector2I> CollectScreeningThreatContactDestinationList(
        BattleAiContext context,
        BattleUnitState threatUnit,
        BattleUnitState protectedUnit,
        int contactRange,
        bool useBlocker,
        Vector2I blockerAnchor,
        ScreeningContext screeningContext
    )
    {
        if (screeningContext != null && threatUnit != null && protectedUnit != null)
        {
            var cacheKey = new ScreeningContactDestinationsKey(
                threatUnit.unit_id,
                protectedUnit.unit_id,
                Mathf.Max(contactRange, 1),
                useBlocker,
                useBlocker ? blockerAnchor : new Vector2I(-1, -1)
            );
            if (
                screeningContext.ContactDestinationsCache.TryGetValue(
                    cacheKey,
                    out List<Vector2I> cached
                )
            )
            {
                return cached;
            }
            List<Vector2I> resolved = CollectScreeningThreatContactDestinations(
                context,
                threatUnit,
                protectedUnit,
                contactRange
            );
            screeningContext.ContactDestinationsCache[cacheKey] = resolved;
            return resolved;
        }
        return CollectScreeningThreatContactDestinations(
            context,
            threatUnit,
            protectedUnit,
            contactRange
        );
    }

    private List<Vector2I> CollectScreeningThreatContactDestinations(
        BattleAiContext context,
        BattleUnitState threatUnit,
        BattleUnitState protectedUnit,
        int contactRange
    )
    {
        var destinations = new List<Vector2I>();
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null || threatUnit == null || protectedUnit == null)
        {
            return destinations;
        }

        int resolvedContactRange = Mathf.Max(contactRange, 1);
        var seen = new HashSet<Vector2I>();
        protectedUnit.RefreshFootprint();
        foreach (Vector2I occupiedCoord in protectedUnit.occupied_coords)
        {
            for (
                int y = occupiedCoord.Y - resolvedContactRange;
                y <= occupiedCoord.Y + resolvedContactRange;
                y += 1
            )
            {
                for (
                    int x = occupiedCoord.X - resolvedContactRange;
                    x <= occupiedCoord.X + resolvedContactRange;
                    x += 1
                )
                {
                    Vector2I coord = new(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!grid.IsInside(state, coord))
                    {
                        continue;
                    }
                    int distance = _distance_from_anchor_to_unit(
                        context,
                        threatUnit,
                        coord,
                        protectedUnit
                    );
                    if (distance <= 0 || distance > resolvedContactRange)
                    {
                        continue;
                    }
                    if (
                        !grid.CanPlaceFootprint(
                            state,
                            coord,
                            threatUnit.footprint_size,
                            threatUnit.unit_id,
                            threatUnit
                        )
                    )
                    {
                        continue;
                    }
                    destinations.Add(coord);
                }
            }
        }

        destinations.Sort(
            (left, right) =>
            {
                int leftDistance = grid.GetDistance(threatUnit.coord, left);
                int rightDistance = grid.GetDistance(threatUnit.coord, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                if (left.Y != right.Y)
                {
                    return left.Y.CompareTo(right.Y);
                }
                return left.X.CompareTo(right.X);
            }
        );
        return destinations;
    }

    private Dictionary<Vector2I, StringName> SnapshotScreeningOccupants(
        BattleAiContext context,
        IEnumerable<Vector2I> coords
    )
    {
        var snapshot = new Dictionary<Vector2I, StringName>();
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null)
        {
            return snapshot;
        }
        foreach (Vector2I coord in coords)
        {
            if (snapshot.ContainsKey(coord))
            {
                continue;
            }
            BattleCellState cell = grid.GetCellState(state, coord);
            if (cell == null)
            {
                continue;
            }
            snapshot[coord] = cell.occupant_unit_id;
        }
        return snapshot;
    }

    private void RestoreScreeningOccupants(
        BattleAiContext context,
        Dictionary<Vector2I, StringName> snapshot
    )
    {
        BattleState state = GetContextState(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || grid == null || snapshot == null)
        {
            return;
        }
        foreach (KeyValuePair<Vector2I, StringName> entry in snapshot)
        {
            grid.SetOccupant(state, entry.Key, entry.Value, mark_revision: false);
        }
    }

    private PathProgressCandidate BuildPathProgressDecision(
        BattleAiContext context,
        BattleUnitState focusTarget,
        AiActionTrace actionTrace,
        MoveDistanceContract distanceContract,
        ScreeningContext screeningContext = null
    )
    {
        using BattleAiTraceSpan trace = new("_build_path_progress_decision");
        return BuildPathProgressDecisionImpl(
            context,
            focusTarget,
            actionTrace,
            distanceContract,
            screeningContext
        );
    }

    private PathProgressCandidate BuildPathProgressDecisionImpl(
        BattleAiContext context,
        BattleUnitState focusTarget,
        AiActionTrace actionTrace,
        MoveDistanceContract distanceContract,
        ScreeningContext screeningContext
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return null;
        }
        if (focusTarget == null || _is_unit_movement_blocked(context, actor))
        {
            return null;
        }
        int moveBudget = _resolve_current_move_budget(actor);
        if (moveBudget <= 0)
        {
            return null;
        }

        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        int currentDistance = _distance_from_anchor_to_unit(
            context,
            actor,
            actor.coord,
            focusTarget
        );
        if (currentDistance >= resolvedMinDistance && currentDistance <= resolvedMaxDistance)
        {
            return null;
        }

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        int bestPathCost = MoveToRangeScoreOrdering.InfiniteTieBreaker;
        int bestPathLength = MoveToRangeScoreOrdering.InfiniteTieBreaker;
        var progressAnchors = new HashSet<Vector2I>();
        int pathSearchBudget = BuildPathSearchBudget(context);
        List<Vector2I> destinations = CollectDistanceBandDestinations(
            context,
            focusTarget,
            distanceContract
        );
        MovePathTreeCosts pathTreeCosts = new();
        if (destinations.Count >= PathTreeFilterMinDestinations)
        {
            BattleMovePathTreeResult pathTree;
            using (new BattleAiTraceSpan("grid_service.build_unit_move_path_tree"))
            {
                pathTree = grid.BuildUnitMovePathTreeTyped(
                    state,
                    actor,
                    actor.coord,
                    pathSearchBudget,
                    BuildMoveCostProvider(context)
                );
            }
            pathTreeCosts = MovePathTreeCosts.FromPathTreeResult(pathTree);
        }

        foreach (Vector2I destination in destinations)
        {
            int pathCost = 0;
            int pathLength = 0;
            IReadOnlyList<Vector2I> path = null;
            if (!pathTreeCosts.IsEmpty)
            {
                if (
                    !pathTreeCosts.TryResolvePath(
                        actor.coord,
                        destination,
                        out pathCost,
                        out List<Vector2I> treePath
                    )
                )
                {
                    _trace_count_increment(actionTrace, "path_tree_unreachable_skip_count", 1);
                    continue;
                }
                path = treePath;
            }
            else
            {
                BattleMovePathResult resolvedPath;
                using (new BattleAiTraceSpan("grid_service.resolve_unit_move_path"))
                {
                    resolvedPath = grid.ResolveUnitMovePathTyped(
                        state,
                        actor,
                        actor.coord,
                        destination,
                        pathSearchBudget,
                        BuildMoveCostProvider(context)
                    );
                }
                if (!resolvedPath.Allowed)
                {
                    continue;
                }
                pathCost = resolvedPath.Cost;
                path = resolvedPath.Path;
            }
            pathLength = path?.Count ?? 0;

            Vector2I moveTarget = ResolveCurrentTurnPathTarget(context, path);
            if (moveTarget == actor.coord)
            {
                continue;
            }
            int currentTurnMoveCost = ResolveCurrentTurnPathMoveCost(
                context,
                actor,
                path,
                moveTarget
            );
            if (currentTurnMoveCost <= 0)
            {
                continue;
            }

            BattleCommand command = _build_move_command(context, moveTarget);
            BattlePreview preview = _build_fast_typed_move_preview(
                context,
                moveTarget,
                currentTurnMoveCost
            );
            if (preview?.allowed != true)
            {
                _trace_count_increment(actionTrace, "preview_reject_count", 1);
                continue;
            }
            progressAnchors.Add(moveTarget);

            BattleAiScoreInput scoreInput = _build_typed_action_score_input(
                context,
                "move",
                action_id.ToString(),
                command,
                preview,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["position_target_unit_id"] = focusTarget.unit_id,
                    ["position_anchor_coord"] = moveTarget,
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["position_objective_kind"] = new StringName("distance_band_progress"),
                    ["action_base_score"] = 60,
                }
            );
            ScreeningMetrics screeningMetrics = ApplyScreeningScore(
                context,
                scoreInput,
                moveTarget,
                screeningContext
            );
            if (actionTrace != null)
            {
                _trace_offer_candidate(
                    actionTrace,
                    _build_candidate_summary(
                        $"path_to_{destination.X}_{destination.Y}_via_{moveTarget.X}_{moveTarget.Y}",
                        command,
                        scoreInput,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["path_cost"] = pathCost,
                            ["turn_move_cost"] = currentTurnMoveCost,
                            ["path_length"] = pathLength,
                            ["path_destination"] = destination,
                            ["screening_bonus"] = screeningMetrics.Bonus,
                            ["screening_penalty"] = screeningMetrics.Penalty,
                            ["screening_threat_unit_id"] = screeningMetrics.ThreatUnitId,
                            ["screening_protected_unit_id"] = screeningMetrics.ProtectedUnitId,
                            ["screening_path_cost_delta"] = screeningMetrics.PathCostDelta,
                            ["screening_base_path_cost"] = screeningMetrics.BasePathCost,
                            ["screening_blocked_path_cost"] = screeningMetrics.BlockedPathCost,
                            ["screening_current_bonus"] = screeningMetrics.CurrentBonus,
                            ["screening_candidate_bonus"] = screeningMetrics.CandidateBonus,
                            ["screening_uncapped_bonus"] = screeningMetrics.UncappedBonus,
                            ["screening_on_shortest_path"] = screeningMetrics.OnShortestPath,
                            ["screening_keeps_contact"] = screeningMetrics.KeepsContact,
                            ["screening_can_counterattack"] = screeningMetrics.CanCounterattack,
                            ["screening_hard_block"] = screeningMetrics.HardBlock,
                            ["screening_distance_band_capped"] =
                                screeningMetrics.DistanceBandCapped,
                        }
                    )
                );
            }

            if (
                !MoveToRangeScoreOrdering.IsBetterCandidate(
                    scoreInput,
                    pathCost,
                    pathLength,
                    bestScoreInput,
                    bestPathCost,
                    bestPathLength
                )
            )
            {
                continue;
            }
            bestScoreInput = scoreInput;
            bestPathCost = pathCost;
            bestPathLength = pathLength;
            bestDecision = _create_scored_decision(
                command,
                scoreInput,
                $"{actor.display_name} 准备绕路逼近 {focusTarget.display_name}（路径成本 {pathCost}，评分 {_score_total(scoreInput)}）。"
            );
        }
        if (bestDecision == null)
        {
            return null;
        }
        return new PathProgressCandidate
        {
            Decision = bestDecision,
            ScoreInput = bestScoreInput,
            PathCost = bestPathCost,
            PathLength = bestPathLength,
            ProgressAnchors = progressAnchors,
        };
    }

    private List<Vector2I> CollectDistanceBandDestinations(
        BattleAiContext context,
        BattleUnitState focusTarget,
        MoveDistanceContract distanceContract
    )
    {
        using BattleAiTraceSpan trace = new("_collect_distance_band_destinations");
        return CollectDistanceBandDestinationsImpl(
            context,
            focusTarget,
            distanceContract
        );
    }

    private List<Vector2I> CollectDistanceBandDestinationsImpl(
        BattleAiContext context,
        BattleUnitState focusTarget,
        MoveDistanceContract distanceContract
    )
    {
        var destinations = new List<Vector2I>();
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null || focusTarget == null)
        {
            return destinations;
        }

        int resolvedMinDistance = distanceContract.DesiredMinDistance;
        int resolvedMaxDistance = distanceContract.DesiredMaxDistance;
        int maxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance);
        var seen = new HashSet<Vector2I>();
        focusTarget.RefreshFootprint();
        foreach (Vector2I occupiedCoord in focusTarget.occupied_coords)
        {
            for (int y = occupiedCoord.Y - maxDistance; y <= occupiedCoord.Y + maxDistance; y++)
            {
                for (int x = occupiedCoord.X - maxDistance; x <= occupiedCoord.X + maxDistance; x++)
                {
                    Vector2I coord = new(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!grid.IsInside(state, coord))
                    {
                        continue;
                    }
                    int distance = _distance_from_anchor_to_unit(
                        context,
                        actor,
                        coord,
                        focusTarget
                    );
                    if (distance < resolvedMinDistance || distance > resolvedMaxDistance)
                    {
                        continue;
                    }
                    destinations.Add(coord);
                }
            }
        }

        destinations.Sort(
            (left, right) =>
            {
                int leftDistance = grid.GetDistance(actor.coord, left);
                int rightDistance = grid.GetDistance(actor.coord, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                if (left.Y != right.Y)
                {
                    return left.Y.CompareTo(right.Y);
                }
                return left.X.CompareTo(right.X);
            }
        );
        return destinations;
    }

    private Vector2I ResolveCurrentTurnPathTarget(
        BattleAiContext context,
        IReadOnlyList<Vector2I> path
    )
    {
        using BattleAiTraceSpan trace = new("_resolve_current_turn_path_target");
        return ResolveCurrentTurnPathTargetImpl(context, path);
    }

    private Vector2I ResolveCurrentTurnPathTargetImpl(
        BattleAiContext context,
        IReadOnlyList<Vector2I> path
    )
    {
        BattleState state = GetContextState(context);
        BattleUnitState actor = GetContextUnit(context);
        BattleGridService grid = GetContextGrid(context);
        if (state == null || actor == null || grid == null)
        {
            return new Vector2I(-1, -1);
        }
        if (path == null || path.Count <= 1)
        {
            return actor.coord;
        }
        int spentCost = 0;
        int maxMovePoints = _resolve_current_move_budget(actor);
        if (maxMovePoints <= 0)
        {
            return actor.coord;
        }
        Vector2I bestCoord = actor.coord;
        for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
        {
            Vector2I nextCoord = path[pathIndex];
            int stepCost = GetMoveCost(context, actor, nextCoord);
            if (spentCost + stepCost > maxMovePoints)
            {
                break;
            }
            spentCost += stepCost;
            bestCoord = nextCoord;
        }
        return bestCoord;
    }

    private int ResolveCurrentTurnPathMoveCost(
        BattleAiContext context,
        BattleUnitState actor,
        IReadOnlyList<Vector2I> path,
        Vector2I moveTarget
    )
    {
        if (context == null || actor == null || path == null || path.Count <= 1)
        {
            return 0;
        }
        int spentCost = 0;
        int maxMovePoints = _resolve_current_move_budget(actor);
        if (maxMovePoints <= 0)
        {
            return 0;
        }
        for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
        {
            Vector2I nextCoord = path[pathIndex];
            int stepCost = GetMoveCost(context, actor, nextCoord);
            if (spentCost + stepCost > maxMovePoints)
            {
                return 0;
            }
            spentCost += stepCost;
            if (nextCoord == moveTarget)
            {
                return spentCost;
            }
        }
        return 0;
    }

    private static int BuildPathSearchBudget(BattleAiContext context)
    {
        BattleState state = GetContextState(context);
        if (state == null)
        {
            return 32;
        }
        return Mathf.Max(state.map_size.X * state.map_size.Y, state.map_size.X + state.map_size.Y);
    }

    private static int GetScoreInputDistanceGap(BattleAiScoreInput scoreInput)
    {
        if (scoreInput is not BattleAiScoreInput typed)
        {
            return -1;
        }
        int distanceValue = typed.distance_to_primary_coord;
        int minDistance = typed.desired_min_distance;
        int maxDistance = typed.desired_max_distance;
        if (distanceValue < 0 || minDistance < 0 || maxDistance < minDistance)
        {
            return -1;
        }
        if (distanceValue < minDistance)
        {
            return minDistance - distanceValue;
        }
        if (distanceValue > maxDistance)
        {
            return distanceValue - maxDistance;
        }
        return 0;
    }

    private static BattleState GetContextState(BattleAiContext context)
    {
        return context?.state;
    }

    private static BattleUnitState GetContextUnit(BattleAiContext context)
    {
        return context?.unit_state;
    }

    private static BattleGridService GetContextGrid(BattleAiContext context)
    {
        return context?.grid_service;
    }

    private static int GetMoveCost(
        BattleAiContext context,
        BattleUnitState unitState,
        Vector2I targetCoord
    )
    {
        return context != null ? context.GetMoveCost(unitState, targetCoord) : 1;
    }

    private static System.Func<BattleUnitState, Vector2I, int> BuildMoveCostProvider(
        BattleAiContext context
    )
    {
        return context != null
            ? (unitState, targetCoord) => context.GetMoveCost(unitState, targetCoord)
            : null;
    }

    private static BattleAiUnitSnapshot ResolveFocusTarget(
        BattleAiQueryService query,
        BattleAiUnitSnapshot actorSnapshot
    )
    {
        IReadOnlyList<BattleAiUnitSnapshot> targetValues =
            query.GetLivingUnitSnapshotsTyped("enemy");
        if (targetValues.Count == 0)
        {
            return null;
        }
        var targets = new System.Collections.Generic.List<BattleAiUnitSnapshot>();
        foreach (BattleAiUnitSnapshot target in targetValues)
        {
            if (target != null)
            {
                targets.Add(target);
            }
        }
        targets.Sort(
            (left, right) =>
            {
                int leftDistance = DistanceFromActor(query, actorSnapshot, left);
                int rightDistance = DistanceFromActor(query, actorSnapshot, right);
                if (leftDistance != rightDistance)
                {
                    return leftDistance.CompareTo(rightDistance);
                }
                int leftHp = left.current_hp;
                int rightHp = right.current_hp;
                if (leftHp != rightHp)
                {
                    return leftHp.CompareTo(rightHp);
                }
                return left.unit_id.ToString().CompareTo(right.unit_id.ToString());
            }
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static int DistanceFromActor(
        BattleAiQueryService query,
        BattleAiUnitSnapshot actorSnapshot,
        BattleAiUnitSnapshot targetSnapshot
    )
    {
        if (query == null || actorSnapshot == null || targetSnapshot == null)
        {
            return int.MaxValue;
        }
        return query.DistanceFromAnchorToTarget(
            actorSnapshot.coord,
            actorSnapshot.footprint_size,
            targetSnapshot.unit_id
        );
    }

    private MoveDistanceContract ResolveMoveDistanceContract(BattleAiContext context)
    {
        IReadOnlyDictionary<string, object> rawDistanceContract =
            _resolve_desired_distance_contract_typed(
            context,
            (SkillDefinition)null,
            range_skill_ids
        );
        MoveDistanceContract distanceContract = MoveDistanceContract.FromMetadata(
            rawDistanceContract,
            desired_min_distance,
            desired_max_distance
        );
        int effectiveAttackRange = ResolveEffectiveAttackRange(context);
        if (effectiveAttackRange < 0)
        {
            return distanceContract;
        }

        int configuredMin = distanceContract.ConfiguredMinDistance;
        int configuredMax = distanceContract.ConfiguredMaxDistance;
        int resolvedMin = Mathf.Max(configuredMin, 0);
        int resolvedMax = configuredMax;
        if (effectiveAttackRange >= 0)
        {
            resolvedMax = effectiveAttackRange;
        }
        if (resolvedMax >= 0 && resolvedMin > resolvedMax)
        {
            resolvedMin = resolvedMax;
        }

        distanceContract.DesiredMinDistance = resolvedMin;
        distanceContract.DesiredMaxDistance = Mathf.Max(resolvedMax, resolvedMin);
        distanceContract.EffectiveAttackRange = effectiveAttackRange;
        return distanceContract;
    }

    private int ResolveEffectiveAttackRange(BattleAiContext context)
    {
        if (context?.unit_state == null)
        {
            return -1;
        }

        int bestGroundAoeCastRange = -1;
        int bestFallbackRange = -1;
        foreach (StringName skillId in _resolve_known_skill_ids(context, range_skill_ids))
        {
            if (skillId == "")
            {
                continue;
            }
            SkillDefinition skillDefinition = _get_skill_definition(context, skillId);
            if (skillDefinition?.CombatProfile == null)
            {
                continue;
            }
            if (
                BattleSkillCastBlockReasonKinds.IsBlocked(
                    _get_skill_cast_block_reason(context, skillDefinition)
                )
            )
            {
                continue;
            }
            if (
                enable_aoe_setup_positioning
                && IsGroundAoeSkill(context.unit_state, skillDefinition, context.skill_catalog)
            )
            {
                bestGroundAoeCastRange = Mathf.Max(
                    bestGroundAoeCastRange,
                    BattleRangeService.GetEffectiveSkillRange(
                        context.unit_state,
                        skillDefinition,
                        context.skill_catalog
                    )
                );
            }
            bestFallbackRange = Mathf.Max(
                bestFallbackRange,
                BattleRangeService.GetEffectiveSkillDistanceContractRange(
                    context.unit_state,
                    skillDefinition,
                    context.skill_catalog
                )
            );
        }
        return bestGroundAoeCastRange >= 0 ? bestGroundAoeCastRange : bestFallbackRange;
    }

    private int ResolveEffectiveAttackRange(BattleAiQueryService query, bool useGroundAoeCastRange)
    {
        int bestRange = -1;
        foreach (StringName skillId in range_skill_ids)
        {
            if (skillId == "")
            {
                continue;
            }
            if (!query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord skillRecord))
            {
                continue;
            }
            MoveSkillRecord record = MoveSkillRecord.FromSkillRecord(skillRecord);
            if (record.TargetMode == BattleTargetMode.Unknown && record.ActorEffectiveRange < 0)
            {
                continue;
            }
            int range = useGroundAoeCastRange && record.IsGroundAoe
                ? record.ActorEffectiveCastRange
                : record.ActorEffectiveRange;
            bestRange = Mathf.Max(bestRange, range);
        }
        return bestRange;
    }

    private bool HasGroundAoeSetupSkill(BattleAiQueryService query)
    {
        if (!enable_aoe_setup_positioning || query == null)
        {
            return false;
        }
        foreach (StringName skillId in range_skill_ids)
        {
            if (skillId == "")
            {
                continue;
            }
            if (
                query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord record)
                && MoveSkillRecord.FromSkillRecord(record).IsSetupSkill
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGroundAoeSkill(
        BattleUnitState actor,
        SkillDefinition skillDefinition,
        ISkillCatalog skillCatalog
    )
    {
        if (actor == null || skillDefinition?.CombatProfile == null)
        {
            return false;
        }
        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
        {
            return false;
        }
        int knownSkillLevel = actor.GetKnownSkillLevelTyped(skillDefinition.SkillId);
        int skillLevel = knownSkillLevel > 0
            ? knownSkillLevel
            : actor.known_active_skill_ids.Contains(skillDefinition.SkillId)
                ? 1
                : 0;
        SkillEffectiveCombatDefinition effectiveDefinition =
            skillCatalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        StringName areaPattern = effectiveDefinition.AreaPattern;
        int areaValue = effectiveDefinition.AreaValue;
        BattleAreaPattern areaPatternKind = BattleTypedNames.ToAreaPattern(areaPattern);
        return areaValue > 0
            && areaPatternKind != BattleAreaPattern.Unknown
            && areaPatternKind != BattleAreaPattern.Single
            && areaPatternKind != BattleAreaPattern.Self;
    }

    private static int ResolveMaxCandidateCount(BattleAiUnitSnapshot actorSnapshot)
    {
        int movePoints = Mathf.Max(actorSnapshot?.current_move_points ?? 0, 0);
        return Mathf.Max(DefaultMaxCandidateCount, movePoints * 4);
    }

    private static MovePathSearchBudget BuildPathBudget(
        BattleAiQueryService query,
        StringName actorId,
        int maxCandidateCount
    )
    {
        BattleMovementQueryService movementQuery = query.GetMovementQueryService();
        if (movementQuery != null)
        {
            BattleMovementQueryService.MovementQueryOptions options =
                BattleMovementQueryService.MovementQueryOptions.ForPathSearchBudget(
                    maxCandidateCount,
                    includeOrigin: false,
                    preferProgress: true
                );
            if (movementQuery.TryBuildPathSearchBudgetTyped(
                actorId,
                options,
                out BattleMovementQueryService.PathSearchBudgetSnapshot budget
            ))
            {
                return MovePathSearchBudget.FromSnapshot(budget);
            }
        }
        return new MovePathSearchBudget
        {
            MaxCost = 0,
            MaxNodes = 0,
            MaxDestinations = maxCandidateCount,
            PathTreeMinDestinationCount = 0,
            IncludeOrigin = false,
            PreferProgress = true,
        };
    }

    private AiActionTrace _begin_action_trace(
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        Dictionary<string, object> traceMetadata = context != null
            ? context.MergeCurrentActionMetadataTyped(metadata)
            : CloneMetadata(metadata);
        StringName resolvedBucket = traceMetadata.TryGetValue(
            "score_bucket_id",
            out object bucketValue
        )
            ? ProgressionDataUtils.to_string_name(bucketValue)
            : score_bucket_id;
        return EnemyAiActionHelper.BeginActionTrace(
            action_id,
            resolvedBucket,
            context,
            traceMetadata
        );
    }

    private static Dictionary<string, object> CloneMetadata(
        IReadOnlyDictionary<string, object> metadata
    )
    {
        var copy = new Dictionary<string, object>(StringComparer.Ordinal);
        if (metadata == null)
            return copy;
        foreach ((string key, object value) in metadata)
        {
            if (!string.IsNullOrEmpty(key))
                copy[key] = value;
        }
        return copy;
    }

    private BattleAiScoreInput _build_typed_action_score_input(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    ) => BattleAiActionEvaluatorUtilities.BuildActionScoreInput(
        _action,
        context,
        actionKind,
        actionLabel,
        command,
        preview,
        metadata
    );

    private static BattleCommand _build_move_command(
        BattleAiContext context,
        Vector2I targetCoord
    ) => EnemyAiActionHelper.BuildMoveCommand(context, targetCoord);

    private static BattlePreview _build_fast_typed_move_preview(
        BattleAiContext context,
        Vector2I targetCoord,
        int moveCost = -1
    ) => BattleAiActionEvaluatorUtilities.BuildFastMovePreview(
        context,
        targetCoord,
        moveCost
    );

    private BattleAiDecision _create_scored_decision(
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string reasonText = ""
    ) => EnemyAiActionHelper.CreateScoredDecision(
        action_id,
        score_bucket_id,
        command,
        scoreInput,
        reasonText
    );

    private static AiCandidateSummary _build_candidate_summary(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        IReadOnlyDictionary<string, object> extra = null
    ) => EnemyAiActionHelper.BuildCandidateSummary(label, command, scoreInput, extra);

    private static void _trace_count_increment(
        AiActionTrace trace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.TraceCountIncrement(trace, key, amount);

    private static void _trace_add_block_reason(AiActionTrace trace, string reason) =>
        EnemyAiActionHelper.TraceAddBlockReason(trace, reason);

    private static void _trace_offer_candidate(
        AiActionTrace trace,
        AiCandidateSummary candidate,
        int keepCount = 5
    ) => EnemyAiActionHelper.TraceOfferCandidate(trace, candidate, keepCount);

    private static StringName _finalize_action_trace(
        BattleAiContext context,
        AiActionTrace trace,
        BattleAiDecision decision = null
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, trace, decision);

    private List<BattleUnitState> _sort_target_units_typed(
        BattleAiContext context,
        StringName targetFilter,
        StringName selector
    ) => _helper.SortTargetUnits(context, targetFilter, selector);

    private static bool _is_unit_movement_blocked(
        BattleAiContext context,
        BattleUnitState unit
    ) => BattleAiActionEvaluatorUtilities.IsUnitMovementBlocked(context, unit);

    private static int _resolve_current_move_budget(BattleUnitState unit) =>
        BattleAiActionEvaluatorUtilities.ResolveCurrentMoveBudget(unit);

    private static bool _is_normal_movement_locked(BattleUnitState unit) =>
        BattleAiActionEvaluatorUtilities.IsNormalMovementLocked(unit);

    private static int _get_hp_basis_points(BattleUnitState unit) =>
        BattleAiActionEvaluatorUtilities.GetHpBasisPoints(unit);

    private int _resolve_unit_effective_threat_range(
        BattleAiContext context,
        BattleUnitState target
    ) => BattleAiActionEvaluatorUtilities.ResolveUnitEffectiveThreatRange(
        context,
        target,
        _helper
    );

    private static int _distance_between_units(
        BattleAiContext context,
        BattleUnitState first,
        BattleUnitState second
    ) => BattleAiActionEvaluatorUtilities.DistanceBetweenUnits(context, first, second);

    private static int _distance_from_anchor_to_unit(
        BattleAiContext context,
        BattleUnitState actor,
        Vector2I anchor,
        BattleUnitState target
    ) => BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
        context,
        actor,
        anchor,
        target
    );

    private static bool _skill_has_tag(
        SkillDefinition skillDefinition,
        StringName expectedTag
    ) => skillDefinition != null && expectedTag != "" && skillDefinition.HasTag(expectedTag);

    private SkillDefinition _get_skill_definition(
        BattleAiContext context,
        StringName skillId
    ) => _helper.GetSkillDefinition(context, skillId);

    private BattleSkillCastBlockReasonKind _get_skill_cast_block_reason(
        BattleAiContext context,
        SkillDefinition skillDefinition
    ) => _helper.GetSkillCastBlockReason(context, skillDefinition);

    private List<StringName> _resolve_known_skill_ids(
        BattleAiContext context,
        IEnumerable<StringName> preferredSkillIds
    ) => _helper.ResolveKnownSkillIds(context, preferredSkillIds);

    private Dictionary<string, object> _resolve_desired_distance_contract_typed(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        IEnumerable<StringName> rangeSkillIds
    ) => BattleAiActionEvaluatorUtilities.ResolveDesiredDistanceContract(
        context,
        desired_min_distance,
        desired_max_distance,
        rangeSkillIds,
        _helper
    );

    private static int _score_total(BattleAiScoreInput scoreInput) =>
        BattleAiActionEvaluatorUtilities.ScoreTotal(scoreInput);
}
