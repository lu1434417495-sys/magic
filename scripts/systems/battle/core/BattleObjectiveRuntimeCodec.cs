internal static class BattleObjectiveRuntimeCodec
{
    internal static string ToWireValue(BattleObjectiveMode mode) =>
        mode switch
        {
            BattleObjectiveMode.Elimination => "elimination",
            BattleObjectiveMode.Boss => "boss",
            BattleObjectiveMode.Rescue => "rescue",
            BattleObjectiveMode.Escape => "escape",
            BattleObjectiveMode.Escort => "escort",
            BattleObjectiveMode.Defense => "defense",
            BattleObjectiveMode.Intercept => "intercept",
            BattleObjectiveMode.NodeOperation => "node_operation",
            BattleObjectiveMode.Control => "control",
            _ => "unknown",
        };

    internal static string ToWireValue(BattleOutcomeKind outcome) =>
        outcome switch
        {
            BattleOutcomeKind.PlayerSuccess => "player_success",
            BattleOutcomeKind.PlayerFailure => "player_failure",
            BattleOutcomeKind.Draw => "draw",
            _ => "unknown",
        };

    internal static string ToWinnerFactionId(BattleOutcomeKind outcome) =>
        outcome switch
        {
            BattleOutcomeKind.PlayerSuccess => "player",
            BattleOutcomeKind.PlayerFailure => "hostile",
            BattleOutcomeKind.Draw => "draw",
            _ => "",
        };

    internal static string ToWireValue(BattleEndReasonKind endReason) =>
        endReason switch
        {
            BattleEndReasonKind.EliminationHostilesDefeated =>
                "elimination_hostiles_defeated",
            BattleEndReasonKind.EliminationAlliesDefeated =>
                "elimination_allies_defeated",
            BattleEndReasonKind.EliminationMutualDestruction =>
                "elimination_mutual_destruction",
            _ => "none",
        };
}
