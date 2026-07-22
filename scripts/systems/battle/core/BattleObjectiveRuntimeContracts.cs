using System;
using Godot;

internal enum BattleOutcomeKind
{
    Unknown = 0,
    PlayerSuccess,
    PlayerFailure,
    Draw,
}

internal enum BattleEndReasonKind
{
    None = 0,
    EliminationHostilesDefeated,
    EliminationAlliesDefeated,
    EliminationMutualDestruction,
}

internal enum BattleObjectiveEvaluationKind
{
    Invalid = 0,
    InProgress,
    Terminal,
}

internal enum BattleOutcomeFlushResult
{
    InvalidObjective = 0,
    NoChange,
    DecisionLatched,
    Completed,
    AlreadyCompleted,
}

internal abstract class BattleObjectiveRuntimeState
{
    protected BattleObjectiveRuntimeState(BattleObjectiveMode mode)
    {
        Mode = mode;
    }

    internal BattleObjectiveMode Mode { get; }

    internal abstract BattleObjectiveRuntimeState DuplicateState();
}

internal sealed class BattleEliminationObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    internal BattleEliminationObjectiveRuntimeState()
        : base(BattleObjectiveMode.Elimination) { }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleEliminationObjectiveRuntimeState();
}

internal sealed class BattleFinalDecision
{
    internal BattleFinalDecision(
        BattleObjectiveMode objectiveMode,
        BattleOutcomeKind outcome,
        BattleEndReasonKind endReason,
        int decisionTu
    )
    {
        if (objectiveMode == BattleObjectiveMode.Unknown)
            throw new ArgumentOutOfRangeException(nameof(objectiveMode));
        if (outcome == BattleOutcomeKind.Unknown)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (endReason == BattleEndReasonKind.None)
            throw new ArgumentOutOfRangeException(nameof(endReason));
        if (decisionTu < 0)
            throw new ArgumentOutOfRangeException(nameof(decisionTu));
        if (!IsSupportedCombination(objectiveMode, outcome, endReason))
        {
            throw new ArgumentException(
                $"Unsupported battle final decision combination: {objectiveMode}/{outcome}/{endReason}."
            );
        }
        ObjectiveMode = objectiveMode;
        Outcome = outcome;
        EndReason = endReason;
        DecisionTu = decisionTu;
    }

    internal BattleObjectiveMode ObjectiveMode { get; }
    internal BattleOutcomeKind Outcome { get; }
    internal BattleEndReasonKind EndReason { get; }
    internal int DecisionTu { get; }

    internal StringName WinnerFactionId =>
        BattleObjectiveRuntimeCodec.ToWinnerFactionId(Outcome);

    internal BattleFinalDecision DuplicateState() =>
        new(ObjectiveMode, Outcome, EndReason, DecisionTu);

    private static bool IsSupportedCombination(
        BattleObjectiveMode objectiveMode,
        BattleOutcomeKind outcome,
        BattleEndReasonKind endReason
    )
    {
        // P0 only owns elimination semantics. Future objective evaluators must add
        // their own outcome/reason matrix before their decisions can be constructed.
        if (objectiveMode != BattleObjectiveMode.Elimination)
            return false;
        return (outcome, endReason) switch
        {
            (
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EliminationHostilesDefeated
            ) => true,
            (
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EliminationAlliesDefeated
            ) => true,
            (
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.EliminationMutualDestruction
            ) => true,
            _ => false,
        };
    }
}

internal readonly struct BattleObjectiveEvaluationResult
{
    private BattleObjectiveEvaluationResult(
        BattleObjectiveEvaluationKind kind,
        BattleFinalDecision decision
    )
    {
        Kind = kind;
        Decision = decision;
    }

    internal BattleObjectiveEvaluationKind Kind { get; }
    internal BattleFinalDecision Decision { get; }

    internal static BattleObjectiveEvaluationResult Invalid() =>
        new(BattleObjectiveEvaluationKind.Invalid, null);

    internal static BattleObjectiveEvaluationResult InProgress() =>
        new(BattleObjectiveEvaluationKind.InProgress, null);

    internal static BattleObjectiveEvaluationResult Terminal(
        BattleFinalDecision decision
    ) =>
        new(
            BattleObjectiveEvaluationKind.Terminal,
            decision ?? throw new ArgumentNullException(nameof(decision))
        );
}
