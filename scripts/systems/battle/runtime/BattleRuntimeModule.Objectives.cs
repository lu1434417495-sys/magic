using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed partial class BattleRuntimeModule
{
    private readonly BattleObjectiveEvaluationService _objectiveEvaluationService = new();
    private int _objectiveMutationDepth;
    private bool _objectiveEvaluationDirty;
    private bool _objectiveFlushInProgress;

    internal bool InitializeBattleObjective(
        BattleObjectiveDefinition objectiveDefinition
    )
    {
        _objectiveMutationDepth = 0;
        _objectiveEvaluationDirty = true;
        _objectiveFlushInProgress = false;
        return _state?.InitializeObjective(objectiveDefinition) == true;
    }

    private bool TryPlaceScenarioActors(
        IReadOnlyList<BattleScenarioActorSpawnRequest> requests
    )
    {
        if (requests == null || requests.Count == 0)
            return true;
        var placedUnits = new List<BattleUnitState>();
        foreach (BattleScenarioActorSpawnRequest prototype in requests)
        {
            if (
                prototype?.Unit == null
                || !BattleObjectiveRuntimeStateFactory.TryResolveEdgeZoneCoords(
                    _state,
                    prototype.SpawnEdge,
                    prototype.SpawnDepth,
                    out List<Vector2I> spawnCoords
                )
            )
            {
                _spawnPlacementService._clear_spawn_placed_units(
                    placedUnits,
                    true
                );
                return false;
            }
            BattleScenarioActorSpawnRequest request =
                prototype.DuplicateForBattleStart();
            if (
                !_spawnPlacementService.PlaceUnitsTyped(
                    new[] { request.Unit },
                    spawnCoords,
                    true
                )
            )
            {
                _spawnPlacementService._clear_spawn_placed_units(
                    placedUnits,
                    true
                );
                return false;
            }
            placedUnits.Add(request.Unit);
        }
        return true;
    }

    internal void BeginObjectiveMutation()
    {
        checked
        {
            _objectiveMutationDepth++;
        }
        _objectiveEvaluationDirty = true;
    }

    internal BattleOutcomeFlushResult EndObjectiveMutation(
        BattleEventBatch batch,
        bool mutationCompleted = true
    )
    {
        if (_objectiveMutationDepth <= 0)
            throw new InvalidOperationException(
                "Battle objective mutation scope was ended without a matching begin."
            );
        _objectiveMutationDepth--;
        if (!mutationCompleted)
        {
            if (_objectiveMutationDepth == 0)
                _objectiveEvaluationDirty = false;
            return BattleOutcomeFlushResult.NoChange;
        }
        if (_objectiveMutationDepth > 0)
            return BattleOutcomeFlushResult.NoChange;
        return FlushBattleOutcomeEvaluation(batch);
    }

    internal void MarkObjectiveEvaluationDirty()
    {
        _objectiveEvaluationDirty = true;
    }

    internal void PreviewObjectiveInteraction(
        BattleUnitReadView activeUnit,
        BattleCommand command,
        BattlePreview preview
    )
    {
        if (preview == null)
            return;
        if (
            _state?.ObjectiveRuntimeState
                is BattleNodeOperationObjectiveRuntimeState
        )
        {
            if (
                !TryResolveNodeOperationInteraction(
                    activeUnit.UnitId,
                    command,
                    out _,
                    out BattleOperationNodeRuntimeState node,
                    out string nodeMessage
                )
            )
            {
                preview.AddLogLine(nodeMessage);
                return;
            }
            preview.allowed = true;
            preview.AddLogLine($"可以操作 {node.DisplayName}，消耗 1 点行动力。");
            return;
        }
        if (
            !TryResolveRescueInteraction(
                activeUnit.UnitId,
                command,
                out _,
                out BattleUnitState target,
                out string message
            )
        )
        {
            preview.AddLogLine(message);
            return;
        }
        preview.allowed = true;
        preview.AddLogLine($"可以解救 {target.display_name}，消耗 1 点行动力。");
    }

    internal void HandleObjectiveInteraction(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        if (batch == null)
            return;
        if (
            _state?.ObjectiveRuntimeState
                is BattleNodeOperationObjectiveRuntimeState
        )
        {
            HandleNodeOperationInteraction(activeUnit, command, batch);
            return;
        }
        if (
            !TryResolveRescueInteraction(
                activeUnit?.unit_id ?? "",
                command,
                out BattleRescueObjectiveRuntimeState rescueObjective,
                out BattleUnitState target,
                out string message
            )
        )
        {
            batch.AddLogLine(message);
            return;
        }
        if (!rescueObjective.TrySecureTarget())
        {
            batch.AddLogLine($"{target.display_name} 已经获救。");
            return;
        }
        activeUnit.current_ap = Math.Max(activeUnit.current_ap - 1, 0);
        _record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Interact),
            1
        );
        batch.AddChangedUnitId(activeUnit.unit_id);
        batch.MarkChanged(BattleChangeFlags.Objective);
        batch.AddLogLine($"{activeUnit.display_name} 解救了 {target.display_name}。");
        MarkObjectiveEvaluationDirty();
    }

    private void HandleNodeOperationInteraction(
        BattleUnitState activeUnit,
        BattleCommand command,
        BattleEventBatch batch
    )
    {
        if (
            !TryResolveNodeOperationInteraction(
                activeUnit?.unit_id ?? "",
                command,
                out BattleNodeOperationObjectiveRuntimeState objective,
                out BattleOperationNodeRuntimeState node,
                out string message
            )
        )
        {
            batch.AddLogLine(message);
            return;
        }
        if (!objective.TryCompleteNode(node.NodeId))
        {
            batch.AddLogLine($"{node.DisplayName} 已经完成作业。");
            return;
        }

        activeUnit.current_ap = Math.Max(activeUnit.current_ap - 1, 0);
        _record_action_issued(
            activeUnit,
            BattleTypedNames.ToStringName(BattleCommandKind.Interact),
            1
        );
        batch.AddChangedUnitId(activeUnit.unit_id);
        batch.MarkChanged(BattleChangeFlags.Objective);
        batch.AddLogLine(
            $"{activeUnit.display_name} 完成了 {node.DisplayName} 的节点作业。"
        );
        MarkObjectiveEvaluationDirty();
    }

    private bool TryResolveNodeOperationInteraction(
        StringName activeUnitId,
        BattleCommand command,
        out BattleNodeOperationObjectiveRuntimeState objective,
        out BattleOperationNodeRuntimeState node,
        out string message
    )
    {
        objective = _state?.ObjectiveRuntimeState
            as BattleNodeOperationObjectiveRuntimeState;
        node = null;
        message = "当前没有可执行的目标交互。";
        if (_state == null || objective == null || command == null)
            return false;
        if (_state.active_unit_id != activeUnitId)
        {
            message = "只有当前行动单位能够执行节点作业。";
            return false;
        }
        BattleUnitState activeUnit = _state.GetUnit(activeUnitId);
        if (
            activeUnit?.is_alive != true
            || activeUnit.current_ap <= 0
            || activeUnit.source_member_id == ""
            || !objective.RequiredPartyUnitIds.Contains(activeUnit.unit_id)
        )
        {
            message = "只有仍可行动的初始持久队员能够执行节点作业。";
            return false;
        }
        if (!objective.TryGetNodeAtCoord(command.target_coord, out node))
        {
            message = "该位置没有可操作的任务节点。";
            return false;
        }
        if (node.IsCompleted)
        {
            message = $"{node.DisplayName} 已经完成作业。";
            return false;
        }
        if (
            BattleGridDistanceService.GetDistanceFromUnitToCoord(
                activeUnit,
                node.Coord
            ) > 1
        )
        {
            message = $"需要移动到 {node.DisplayName} 同格或相邻位置才能操作。";
            return false;
        }
        return true;
    }

    private bool TryResolveRescueInteraction(
        StringName activeUnitId,
        BattleCommand command,
        out BattleRescueObjectiveRuntimeState rescueObjective,
        out BattleUnitState target,
        out string message
    )
    {
        rescueObjective = _state?.ObjectiveRuntimeState
            as BattleRescueObjectiveRuntimeState;
        target = null;
        message = "当前没有可执行的目标交互。";
        if (_state == null || rescueObjective == null || command == null)
            return false;
        if (_state.active_unit_id != activeUnitId)
        {
            message = "只有当前行动单位能够执行解救。";
            return false;
        }
        if (rescueObjective.TargetSecured)
        {
            message = "救援目标已经获救。";
            return false;
        }
        BattleUnitState activeUnit = _state.GetUnit(activeUnitId);
        target = _state.GetUnit(rescueObjective.TargetUnitId);
        if (
            activeUnit?.is_alive != true
            || activeUnit.current_ap <= 0
            || !rescueObjective.RequiredPartyUnitIds.Contains(activeUnit.unit_id)
            || activeUnit.source_member_id == ""
        )
        {
            message = "只有仍可行动的初始持久队员能够执行解救。";
            return false;
        }
        if (
            target?.is_alive != true
            || command.target_unit_id != rescueObjective.TargetUnitId
        )
        {
            message = "救援目标无效或已经倒下。";
            return false;
        }
        if (BattleGridDistanceService.GetDistanceBetweenUnits(activeUnit, target) > 1)
        {
            message = $"需要移动到 {target.display_name} 相邻位置才能解救。";
            return false;
        }
        return true;
    }

    internal BattleOutcomeFlushResult FlushBattleOutcomeEvaluation(
        BattleEventBatch batch
    )
    {
        if (_objectiveMutationDepth > 0)
            return BattleOutcomeFlushResult.NoChange;
        if (_state?.ObjectiveRuntimeState == null || batch == null)
            return BattleOutcomeFlushResult.InvalidObjective;
        if (_state.PhaseKind == BattlePhaseKind.BattleEnded)
            return BattleOutcomeFlushResult.AlreadyCompleted;
        if (_objectiveFlushInProgress)
            return BattleOutcomeFlushResult.NoChange;

        _objectiveFlushInProgress = true;
        try
        {
            if (_state.FinalDecision == null)
            {
                if (!_objectiveEvaluationDirty)
                    return BattleOutcomeFlushResult.NoChange;
                BattleObjectiveEvaluationResult evaluation =
                    _objectiveEvaluationService.Evaluate(_state);
                _objectiveEvaluationDirty = false;
                if (evaluation.Kind == BattleObjectiveEvaluationKind.Invalid)
                    return BattleOutcomeFlushResult.InvalidObjective;
                if (evaluation.Kind == BattleObjectiveEvaluationKind.InProgress)
                    return BattleOutcomeFlushResult.NoChange;
                if (!_state.TryLatchFinalDecision(evaluation.Decision))
                    return BattleOutcomeFlushResult.NoChange;

                if (_state.timeline != null)
                {
                    _state.timeline.frozen = true;
                    batch.MarkTimelineChanged();
                }
                batch.MarkChanged(BattleChangeFlags.Objective);
            }

            if (
                _state.ModalStateKind == BattleModalStateKind.PromotionChoice
                || _state.ModalStateKind == BattleModalStateKind.StartConfirm
            )
                return BattleOutcomeFlushResult.DecisionLatched;
            if (_state.ModalStateKind != BattleModalStateKind.None)
                throw new InvalidOperationException(
                    $"Cannot complete battle while modal state {_state.modal_state} is active."
                );

            return CompleteBattle(batch)
                ? BattleOutcomeFlushResult.Completed
                : BattleOutcomeFlushResult.AlreadyCompleted;
        }
        finally
        {
            _objectiveFlushInProgress = false;
        }
    }

    private bool CompleteBattle(BattleEventBatch batch)
    {
        if (
            _state == null
            || batch == null
            || _state.FinalDecision == null
            || _state.PhaseKind == BattlePhaseKind.BattleEnded
        )
            return false;
        if (_state.ModalStateKind == BattleModalStateKind.PromotionChoice)
            return false;

        // Build all data required by the terminal commit before changing phase. If
        // result construction fails, the latched decision remains retryable rather
        // than leaving a BattleEnded state without a consumable result.
        BattleResolutionResult resolutionResult = _build_battle_resolution_result();
        if (resolutionResult == null || !resolutionResult.IsTerminal)
        {
            throw new InvalidOperationException(
                "A latched battle final decision did not produce a terminal resolution result."
            );
        }
        _battle_rating_system?.RecordBattleWonAchievements();
        _battle_rating_system?.FinalizeBattleRatingRewards();

        _battle_resolution_result = resolutionResult;
        _battle_resolution_result_consumed = false;
        _state.PhaseKind = BattlePhaseKind.BattleEnded;
        _state.active_unit_id = "";
        if (_state.timeline != null)
        {
            _state.timeline.ready_unit_ids.Clear();
            _state.timeline.frozen = true;
        }

        batch.phase_changed = true;
        batch.battle_ended = true;
        batch.MarkChanged(BattleChangeFlags.Objective);
        string line = $"战斗结束，胜利方：{_state.winner_faction_id}。";
        batch.AddLogLine(line);
        _state.AppendLogEntry(line);
        return true;
    }
}
