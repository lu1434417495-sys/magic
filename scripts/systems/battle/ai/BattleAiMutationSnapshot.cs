using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMutationSnapshot
{
    private readonly SnapshotState _state;

    private BattleAiMutationSnapshot(SnapshotState state)
    {
        _state = state ?? SnapshotState.Empty();
    }

    internal bool IsEmpty => _state.IsEmpty;

    internal static BattleAiMutationSnapshot Empty() =>
        new(SnapshotState.Empty());

    internal static BattleAiMutationSnapshot Capture(BattleAiContext context) =>
        new(SnapshotState.Capture(context));

    internal List<string> Validate(
        BattleAiContext context,
        StringName activeUnitId
    ) => _state.Validate(context, activeUnitId);

    internal bool MatchesCurrentState(BattleAiContext context) =>
        _state.MatchesCurrentState(context);

    internal List<string> CompareCurrentState(BattleAiContext context) =>
        _state.CompareCurrentState(context);
}
