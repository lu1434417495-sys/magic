using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRetreatActionEvaluator
{
    private readonly BattleAiTypedActionHelper _helper = new();

    internal BattleAiDecision Evaluate(RetreatActionDefinition action, BattleAiContext context)
    {
        if (action == null || context?.unit_state == null)
            return null;
        AiTraceRecorder.Enter("decide:retreat");
        try
        {
            return EvaluateImpl(action, context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:retreat");
        }
    }

    private BattleAiDecision EvaluateImpl(
        RetreatActionDefinition action,
        BattleAiContext context
    )
    {
        AiActionTrace trace = context?.trace_enabled == true
            ? EnemyAiActionHelper.BeginActionTrace(
                action.ActionId,
                action.ScoreBucketId,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "retreat",
                    ["target_selector"] = action.TargetSelector.ToString(),
                    ["minimum_safe_distance"] = action.MinimumSafeDistance,
                    ["use_dynamic_threat_safe_distance"] = action.UseDynamicThreatSafeDistance,
                    ["safe_distance_margin"] = action.SafeDistanceMargin,
                }
            )
            : null;
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            action.TargetSelector
        );
        if (targets.Count == 0)
            return Fail(context, trace, "no_valid_targets");

        BattleUnitState actor = context.unit_state;
        BattleUnitState dynamicFocusTarget = action.UseDynamicThreatSafeDistance
            ? BattleAiActionEvaluatorUtilities.SelectMostUnsafeTarget(
                context,
                targets,
                actor.GetAnchorCoord(),
                action.MinimumSafeDistance,
                action.SafeDistanceMargin,
                _helper
            )
            : null;
        if (action.UseDynamicThreatSafeDistance && dynamicFocusTarget == null)
            return Fail(context, trace, "no_valid_targets");

        if (BattleAiActionEvaluatorUtilities.IsUnitMovementBlocked(context, actor))
            return Fail(context, trace, "movement_blocked");
        int moveBudget = BattleAiActionEvaluatorUtilities.ResolveCurrentMoveBudget(actor);
        if (moveBudget <= 0)
        {
            return Fail(
                context,
                trace,
                BattleAiActionEvaluatorUtilities.IsNormalMovementLocked(actor)
                    ? "movement_locked"
                    : "no_move_budget"
            );
        }

        IEnumerable<BattleUnitState> focusTargets = action.UseDynamicThreatSafeDistance
            ? new[] { dynamicFocusTarget }
            : targets;
        foreach (BattleUnitState focusTarget in focusTargets)
        {
            if (focusTarget == null)
                continue;
            int resolvedSafeDistance = action.UseDynamicThreatSafeDistance
                ? BattleAiActionEvaluatorUtilities.ResolveTargetSafeDistance(
                    context,
                    focusTarget,
                    action.MinimumSafeDistance,
                    action.SafeDistanceMargin,
                    _helper
                )
                : action.MinimumSafeDistance;
            int threatAttackRange = BattleAiActionEvaluatorUtilities.ResolveUnitEffectiveThreatRange(
                context,
                focusTarget,
                _helper
            );
            if (trace != null)
            {
                trace.Metadata["focus_target_unit_id"] = focusTarget.unit_id.ToString();
                trace.Metadata["threat_attack_range"] = threatAttackRange;
                trace.Metadata["resolved_safe_distance"] = resolvedSafeDistance;
            }

            BattleAiScoreInput bestScoreInput = BattleAiActionEvaluatorUtilities.BuildActionScoreInput(
                action,
                context,
                "retreat",
                action.ActionId.ToString(),
                null,
                null,
                BuildPositionMetadata(
                    focusTarget,
                    actor.GetAnchorCoord(),
                    resolvedSafeDistance,
                    includeMoveCost: true
                )
            );
            BattleAiDecision bestDecision = null;
            BattleGridService grid = context.grid_service;
            BattleState state = context.state;
            foreach (
                Vector2I neighbor in grid.GetNeighbors4(
                    state,
                    actor.GetAnchorCoord()
                )
            )
            {
                if (
                    !grid.CanTraverse(
                        state,
                        actor.GetAnchorCoord(),
                        neighbor,
                        actor
                    )
                )
                    continue;
                int moveCost = Mathf.Max(context.GetMoveCost(actor, neighbor), 1);
                if (moveCost > moveBudget)
                    continue;
                EnemyAiActionHelper.TraceCountIncrement(trace, "evaluation_count");
                BattleCommand command = EnemyAiActionHelper.BuildMoveCommand(context, neighbor);
                BattlePreview preview = BattleAiActionEvaluatorUtilities.BuildFastMovePreview(
                    context,
                    neighbor,
                    moveCost
                );
                if (preview?.allowed != true)
                {
                    EnemyAiActionHelper.TraceCountIncrement(trace, "preview_reject_count");
                    continue;
                }
                BattleAiScoreInput scoreInput = BattleAiActionEvaluatorUtilities.BuildActionScoreInput(
                    action,
                    context,
                    "retreat",
                    action.ActionId.ToString(),
                    command,
                    preview,
                    BuildPositionMetadata(
                        focusTarget,
                        neighbor,
                        resolvedSafeDistance,
                        includeMoveCost: false
                    )
                );
                if (trace != null)
                {
                    EnemyAiActionHelper.TraceOfferCandidate(
                        trace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            $"retreat_to_{neighbor.X}_{neighbor.Y}",
                            command,
                            scoreInput,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["predicted_distance"] =
                                    BattleAiActionEvaluatorUtilities.ScoreDistanceToPrimaryCoord(
                                        scoreInput
                                    ),
                                ["resolved_safe_distance"] = resolvedSafeDistance,
                                ["threat_attack_range"] = threatAttackRange,
                            }
                        )
                    );
                }
                if (!BattleAiDecisionEngine.IsBetterScoreInputTyped(scoreInput, bestScoreInput))
                    continue;
                bestScoreInput = scoreInput;
                bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                    action.ActionId,
                    action.ScoreBucketId,
                    command,
                    scoreInput,
                    $"{actor.display_name} 准备与 {focusTarget.display_name} 拉开到 {BattleAiActionEvaluatorUtilities.ScoreDistanceToPrimaryCoord(scoreInput)} 格（评分 {BattleAiActionEvaluatorUtilities.ScoreTotal(scoreInput)}）。"
                );
            }
            if (bestDecision != null)
            {
                EnemyAiActionHelper.FinalizeActionTrace(context, trace, bestDecision);
                return bestDecision;
            }
        }

        EnemyAiActionHelper.FinalizeActionTrace(context, trace);
        return null;
    }

    private static Dictionary<string, object> BuildPositionMetadata(
        BattleUnitState focusTarget,
        Vector2I anchor,
        int safeDistance,
        bool includeMoveCost
    )
    {
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["position_target_unit_id"] = focusTarget?.unit_id ?? new StringName(""),
            ["position_anchor_coord"] = anchor,
            ["desired_min_distance"] = safeDistance,
            ["desired_max_distance"] = safeDistance,
            ["position_objective_kind"] = "distance_band_progress",
        };
        if (includeMoveCost)
            metadata["move_cost"] = 0;
        return metadata;
    }

    private static BattleAiDecision Fail(
        BattleAiContext context,
        AiActionTrace trace,
        string reason
    )
    {
        EnemyAiActionHelper.TraceAddBlockReason(trace, reason);
        EnemyAiActionHelper.FinalizeActionTrace(context, trace);
        return null;
    }
}
