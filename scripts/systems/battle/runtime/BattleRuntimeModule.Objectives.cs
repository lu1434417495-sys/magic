using System;

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
