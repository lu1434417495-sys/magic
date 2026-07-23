using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal sealed class BattleAiObjectiveActionEvaluator
{
    internal BattleAiDecision Evaluate(BattleAiContext context)
    {
        if (context?.state?.ObjectiveRuntimeState == null || context.unit_state == null)
            return null;

        return context.state.ObjectiveRuntimeState switch
        {
            BattleRescueObjectiveRuntimeState rescueObjective =>
                EvaluateRescue(context, rescueObjective),
            BattleEscapeObjectiveRuntimeState escapeObjective =>
                EvaluateEscape(context, escapeObjective),
            BattleEscortObjectiveRuntimeState escortObjective =>
                EvaluateEscort(context, escortObjective),
            BattleDefenseObjectiveRuntimeState defenseObjective =>
                EvaluateDefense(context, defenseObjective),
            BattleInterceptObjectiveRuntimeState interceptObjective =>
                EvaluateIntercept(context, interceptObjective),
            BattleNodeOperationObjectiveRuntimeState nodeOperationObjective =>
                EvaluateNodeOperation(context, nodeOperationObjective),
            BattleControlObjectiveRuntimeState controlObjective =>
                EvaluateControl(context, controlObjective),
            _ => null,
        };
    }

    private static BattleAiDecision EvaluateRescue(
        BattleAiContext context,
        BattleRescueObjectiveRuntimeState objective
    )
    {
        if (context.unit_state.unit_id != objective.TargetUnitId)
            return null;
        return BuildWaitDecision(
            context,
            "objective_rescue_wait",
            "objective_rescue",
            objective.TargetSecured
                ? $"{context.unit_state.display_name} 已获救，等待战斗结算。"
                : $"{context.unit_state.display_name} 正在等待救援。"
        );
    }

    private static BattleAiDecision EvaluateEscape(
        BattleAiContext context,
        BattleEscapeObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        if (!objective.RequiredUnitIds.Contains(actor.unit_id))
            return null;
        if (BattleObjectiveEvaluationService.IsUnitFullyInsideExit(actor, objective))
        {
            return BuildWaitDecision(
                context,
                "objective_escape_hold",
                "objective_escape",
                $"{actor.display_name} 已进入撤离区，原地等待队友。"
            );
        }
        return BuildExitMoveDecision(
            context,
            objective.ExitCoords,
            objective.ContainsExitCoord,
            "objective_escape_move",
            "objective_escape",
            $"{actor.display_name} 向撤离区移动。",
            true,
            "objective_escape_wait",
            "objective_escape",
            $"{actor.display_name} 暂时无法向撤离区移动。"
        );
    }

    private static BattleAiDecision EvaluateEscort(
        BattleAiContext context,
        BattleEscortObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        if (actor.unit_id != objective.TargetUnitId)
            return null;
        if (BattleObjectiveEvaluationService.IsUnitFullyInsideExit(actor, objective))
        {
            return BuildWaitDecision(
                context,
                "objective_escort_hold",
                "objective_escort",
                $"{actor.display_name} 已抵达护送出口。"
            );
        }
        return BuildExitMoveDecision(
            context,
            objective.ExitCoords,
            objective.ContainsExitCoord,
            "objective_escort_move",
            "objective_escort",
            $"{actor.display_name} 沿护送路线向出口移动。",
            false,
            "objective_escort_wait",
            "objective_escort",
            $"{actor.display_name} 的护送路线受阻，等待重新寻路。"
        );
    }

    private static BattleAiDecision EvaluateIntercept(
        BattleAiContext context,
        BattleInterceptObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        if (actor.unit_id != objective.TargetUnitId)
            return null;
        if (BattleObjectiveEvaluationService.IsUnitFullyInsideExit(actor, objective))
        {
            return BuildWaitDecision(
                context,
                "objective_intercept_hold",
                "objective_intercept",
                $"{actor.display_name} 已抵达逃脱区。"
            );
        }
        return BuildExitMoveDecision(
            context,
            objective.ExitCoords,
            objective.ContainsExitCoord,
            "objective_intercept_move",
            "objective_intercept",
            $"{actor.display_name} 正在向逃脱区突围。",
            false,
            "objective_intercept_wait",
            "objective_intercept",
            $"{actor.display_name} 的逃脱路线受阻，等待重新寻路。"
        );
    }

    private static BattleAiDecision EvaluateDefense(
        BattleAiContext context,
        BattleDefenseObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        if (actor.unit_id != objective.TargetUnitId)
            return null;
        return BuildWaitDecision(
            context,
            "objective_defense_hold",
            "objective_defense",
            $"{actor.display_name} 正在坚守防守位置。"
        );
    }

    private static BattleAiDecision EvaluateNodeOperation(
        BattleAiContext context,
        BattleNodeOperationObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        if (
            !objective.RequiredPartyUnitIds.Contains(actor.unit_id)
            || objective.AllNodesCompleted
        )
        {
            return null;
        }

        foreach (
            BattleOperationNodeRuntimeState node in
            objective.OperationNodes.Where(node => !node.IsCompleted)
        )
        {
            if (
                actor.current_ap > 0
                && BattleGridDistanceService.GetDistanceFromUnitToCoord(
                    actor,
                    node.Coord
                ) <= 1
            )
            {
                var interactCommand = new BattleCommand
                {
                    CommandKind = BattleCommandKind.Interact,
                    unit_id = actor.unit_id,
                    target_coord = node.Coord,
                };
                BattlePreview preview =
                    context.preview_command_callback?.Invoke(interactCommand);
                if (
                    context.preview_command_callback == null
                    || preview?.allowed == true
                )
                {
                    return EnemyAiActionHelper.CreateDecision(
                        "objective_node_operation_interact",
                        "objective_node_operation",
                        interactCommand,
                        $"{actor.display_name} 操作 {node.DisplayName}。"
                    );
                }
            }
        }

        if (actor.current_move_points <= 0 || context.ai_query_service == null)
            return null;

        var anchors = new List<Vector2I>();
        Vector2I[] offsets =
        {
            Vector2I.Zero,
            Vector2I.Left,
            Vector2I.Right,
            Vector2I.Up,
            Vector2I.Down,
        };
        foreach (
            BattleOperationNodeRuntimeState node in
            objective.OperationNodes.Where(node => !node.IsCompleted)
        )
        {
            foreach (Vector2I offset in offsets)
            {
                Vector2I candidate = node.Coord + offset;
                BattleCellState cell = context.state.GetCell(candidate);
                if (cell?.passable == true && !anchors.Contains(candidate))
                    anchors.Add(candidate);
            }
        }
        MovementReachabilityResult pathResult =
            context.ai_query_service.GetCurrentTurnPathToBestAnchor(
                anchors,
                actor.current_move_points
            );
        if (pathResult?.Ok != true || !pathResult.TargetCoordValid)
            return null;

        BattleCommand moveCommand = EnemyAiActionHelper.BuildMoveCommand(
            context,
            pathResult.TargetCoord
        );
        BattlePreview movePreview =
            context.preview_command_callback?.Invoke(moveCommand);
        if (
            context.preview_command_callback != null
            && movePreview?.allowed != true
        )
        {
            return null;
        }
        return EnemyAiActionHelper.CreateDecision(
            "objective_node_operation_move",
            "objective_node_operation",
            moveCommand,
            $"{actor.display_name} 向未完成的任务节点移动。"
        );
    }

    private static BattleAiDecision EvaluateControl(
        BattleAiContext context,
        BattleControlObjectiveRuntimeState objective
    )
    {
        BattleUnitState actor = context.unit_state;
        bool isPlayer = context.state.GetAllyUnitIdsTyped().Contains(actor.unit_id);
        bool isHostile = context.state.GetEnemyUnitIdsTyped().Contains(actor.unit_id);
        if (!isPlayer && !isHostile)
            return null;

        BattleControlZoneOccupancyKind ownOccupancy =
            isPlayer
                ? BattleControlZoneOccupancyKind.Player
                : BattleControlZoneOccupancyKind.Hostile;
        BattleControlZoneOccupancyKind opposingOccupancy =
            isPlayer
                ? BattleControlZoneOccupancyKind.Hostile
                : BattleControlZoneOccupancyKind.Player;
        foreach (BattleControlZoneRuntimeState zone in objective.ControlZones)
        {
            if (
                BattleControlObjectiveRules.IsUnitFullyInside(actor, zone)
                && BattleControlObjectiveRules.ResolveOccupancy(
                    context.state,
                    zone
                ) == BattleControlZoneOccupancyKind.Contested
            )
            {
                return null;
            }
        }

        BattleControlZoneOccupancyKind[] priorities =
        {
            opposingOccupancy,
            BattleControlZoneOccupancyKind.Neutral,
            BattleControlZoneOccupancyKind.Contested,
        };
        foreach (BattleControlZoneOccupancyKind priority in priorities)
        {
            BattleAiDecision decision = TryBuildControlMoveDecision(
                context,
                objective,
                priority
            );
            if (decision != null)
                return decision;
        }

        foreach (BattleControlZoneRuntimeState zone in objective.ControlZones)
        {
            if (
                BattleControlObjectiveRules.IsUnitFullyInside(actor, zone)
                && BattleControlObjectiveRules.ResolveOccupancy(
                    context.state,
                    zone
                ) == ownOccupancy
            )
            {
                return BuildWaitDecision(
                    context,
                    "objective_control_hold",
                    "objective_control",
                    $"{actor.display_name} 正在守住 {zone.DisplayName}。"
                );
            }
        }
        return null;
    }

    private static BattleAiDecision TryBuildControlMoveDecision(
        BattleAiContext context,
        BattleControlObjectiveRuntimeState objective,
        BattleControlZoneOccupancyKind desiredOccupancy
    )
    {
        BattleUnitState actor = context.unit_state;
        if (actor.current_move_points <= 0 || context.ai_query_service == null)
            return null;

        var anchors = new List<Vector2I>();
        foreach (BattleControlZoneRuntimeState zone in objective.ControlZones)
        {
            if (
                BattleControlObjectiveRules.ResolveOccupancy(
                    context.state,
                    zone
                ) != desiredOccupancy
                || BattleControlObjectiveRules.IsUnitFullyInside(actor, zone)
            )
            {
                continue;
            }
            foreach (
                Vector2I anchor in ResolveExitLandingAnchors(
                    context.state,
                    actor,
                    zone.Coords,
                    zone.ContainsCoord
                )
            )
            {
                if (!anchors.Contains(anchor))
                    anchors.Add(anchor);
            }
        }
        if (anchors.Count == 0)
            return null;

        MovementReachabilityResult pathResult =
            context.ai_query_service.GetCurrentTurnPathToBestAnchor(
                anchors,
                actor.current_move_points
            );
        if (pathResult?.Ok != true || !pathResult.TargetCoordValid)
            return null;

        BattleCommand moveCommand = EnemyAiActionHelper.BuildMoveCommand(
            context,
            pathResult.TargetCoord
        );
        BattlePreview preview = context.preview_command_callback?.Invoke(
            moveCommand
        );
        if (
            context.preview_command_callback != null
            && preview?.allowed != true
        )
        {
            return null;
        }
        return EnemyAiActionHelper.CreateDecision(
            "objective_control_move",
            "objective_control",
            moveCommand,
            $"{actor.display_name} 向争夺区移动。"
        );
    }

    private static BattleAiDecision BuildExitMoveDecision(
        BattleAiContext context,
        IReadOnlyList<Vector2I> exitCoords,
        Func<Vector2I, bool> containsExitCoord,
        StringName actionId,
        StringName sourceId,
        string moveText,
        bool fallbackToCombat,
        StringName waitActionId,
        StringName waitSourceId,
        string waitText
    )
    {
        BattleUnitState actor = context.unit_state;
        if (actor.current_move_points <= 0 || context.ai_query_service == null)
        {
            return fallbackToCombat
                ? null
                : BuildWaitDecision(
                    context,
                    waitActionId,
                    waitSourceId,
                    waitText
                );
        }
        IReadOnlyList<Vector2I> landingAnchors = ResolveExitLandingAnchors(
            context.state,
            actor,
            exitCoords,
            containsExitCoord
        );
        MovementReachabilityResult pathResult =
            context.ai_query_service.GetCurrentTurnPathToBestAnchor(
                landingAnchors,
                actor.current_move_points
            );
        if (pathResult?.Ok != true || !pathResult.TargetCoordValid)
        {
            return fallbackToCombat
                ? null
                : BuildWaitDecision(
                    context,
                    waitActionId,
                    waitSourceId,
                    waitText
                );
        }

        BattleCommand moveCommand = EnemyAiActionHelper.BuildMoveCommand(
            context,
            pathResult.TargetCoord
        );
        BattlePreview preview = context.preview_command_callback?.Invoke(moveCommand);
        if (context.preview_command_callback != null && preview?.allowed != true)
        {
            return fallbackToCombat
                ? null
                : BuildWaitDecision(
                    context,
                    waitActionId,
                    waitSourceId,
                    waitText
                );
        }
        return EnemyAiActionHelper.CreateDecision(
            actionId,
            sourceId,
            moveCommand,
            moveText
        );
    }

    private static BattleAiDecision BuildWaitDecision(
        BattleAiContext context,
        StringName actionId,
        StringName sourceId,
        string reasonText
    ) =>
        EnemyAiActionHelper.CreateDecision(
            actionId,
            sourceId,
            EnemyAiActionHelper.BuildWaitCommand(context),
            reasonText
        );

    private static IReadOnlyList<Vector2I> ResolveExitLandingAnchors(
        BattleState state,
        BattleUnitState actor,
        IReadOnlyList<Vector2I> exitCoords,
        Func<Vector2I, bool> containsExitCoord
    )
    {
        var anchors = new List<Vector2I>();
        if (
            state == null
            || actor == null
            || exitCoords == null
            || containsExitCoord == null
        )
            return anchors;
        Vector2I footprint = actor.footprint_size;
        if (footprint.X <= 0 || footprint.Y <= 0)
        {
            footprint = BattleUnitState.GetFootprintSizeForBodySize(
                actor.body_size
            );
        }
        footprint = new Vector2I(
            Math.Max(footprint.X, 1),
            Math.Max(footprint.Y, 1)
        );

        foreach (Vector2I exitCoord in exitCoords)
        {
            bool inside = true;
            for (int offsetY = 0; offsetY < footprint.Y && inside; offsetY++)
            {
                for (int offsetX = 0; offsetX < footprint.X; offsetX++)
                {
                    if (
                        !containsExitCoord(
                            exitCoord + new Vector2I(offsetX, offsetY)
                        )
                    )
                    {
                        inside = false;
                        break;
                    }
                }
            }
            if (inside)
                anchors.Add(exitCoord);
        }
        return anchors;
    }
}
