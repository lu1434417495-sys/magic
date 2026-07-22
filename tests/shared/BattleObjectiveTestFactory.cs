using System;
using Godot;

internal static class BattleObjectiveTestFactory
{
    internal static BattleFinalDecision CreateEliminationDecision(
        StringName winnerFactionId,
        int decisionTu = 0
    )
    {
        return winnerFactionId.ToString() switch
        {
            "player" => new BattleFinalDecision(
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EliminationHostilesDefeated,
                decisionTu
            ),
            "hostile" => new BattleFinalDecision(
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EliminationAlliesDefeated,
                decisionTu
            ),
            "draw" => new BattleFinalDecision(
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.EliminationMutualDestruction,
                decisionTu
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(winnerFactionId),
                winnerFactionId,
                "Expected player, hostile, or draw."
            ),
        };
    }

    internal static void SetEliminationDecision(
        BattleState state,
        StringName winnerFactionId,
        int decisionTu = 0
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ObjectiveRuntimeState == null)
            state.InitializeObjective(BattleEliminationObjectiveDefinition.Instance);
        if (!state.TryLatchFinalDecision(
            CreateEliminationDecision(winnerFactionId, decisionTu)
        ))
        {
            throw new InvalidOperationException(
                "Battle state already has a final decision."
            );
        }
    }

    internal static BattleResolutionResult CreateEliminationResolution(
        StringName winnerFactionId,
        int decisionTu = 0
    )
    {
        BattleResolutionResult result = new();
        result.SetFinalDecision(
            CreateEliminationDecision(winnerFactionId, decisionTu)
        );
        return result;
    }
}
