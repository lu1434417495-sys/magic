using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMutationSnapshot
{
    private readonly BattleAiMutationGuard.SnapshotState _state;

    private BattleAiMutationSnapshot(BattleAiMutationGuard.SnapshotState state)
    {
        _state = state ?? BattleAiMutationGuard.SnapshotState.Empty();
    }

    internal bool IsEmpty => _state.IsEmpty;

    internal static BattleAiMutationSnapshot Empty() =>
        new(BattleAiMutationGuard.SnapshotState.Empty());

    internal static BattleAiMutationSnapshot Capture(BattleAiContext context) =>
        new(BattleAiMutationGuard.SnapshotState.Capture(context));

    internal void Restore(BattleAiContext context)
    {
        _state.Restore(context);
    }

    internal List<string> ValidateAndRestore(
        BattleAiContext context,
        StringName activeUnitId
    ) => _state.ValidateAndRestore(context, activeUnitId);

    internal bool MatchesCurrentState(BattleAiContext context) =>
        _state.MatchesCurrentState(context);

    internal List<string> CompareCurrentState(BattleAiContext context) =>
        _state.CompareCurrentState(context);
}
