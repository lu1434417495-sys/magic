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
            BattleEndReasonKind.BossTargetDefeated =>
                "boss_target_defeated",
            BattleEndReasonKind.BossPartyDefeated =>
                "boss_party_defeated",
            BattleEndReasonKind.BossMutualDestruction =>
                "boss_mutual_destruction",
            BattleEndReasonKind.RescueTargetSecured =>
                "rescue_target_secured",
            BattleEndReasonKind.RescueTargetDefeated =>
                "rescue_target_defeated",
            BattleEndReasonKind.RescuePartyDefeated =>
                "rescue_party_defeated",
            BattleEndReasonKind.EscapeRequiredUnitsReachedExit =>
                "escape_required_units_reached_exit",
            BattleEndReasonKind.EscapeRequiredUnitDefeated =>
                "escape_required_unit_defeated",
            BattleEndReasonKind.EscortTargetReachedExit =>
                "escort_target_reached_exit",
            BattleEndReasonKind.EscortTargetDefeated =>
                "escort_target_defeated",
            BattleEndReasonKind.EscortPartyDefeated =>
                "escort_party_defeated",
            BattleEndReasonKind.DefenseDeadlineReached =>
                "defense_deadline_reached",
            BattleEndReasonKind.DefenseTargetDefeated =>
                "defense_target_defeated",
            BattleEndReasonKind.DefensePartyDefeated =>
                "defense_party_defeated",
            BattleEndReasonKind.InterceptTargetDefeated =>
                "intercept_target_defeated",
            BattleEndReasonKind.InterceptTargetEscaped =>
                "intercept_target_escaped",
            BattleEndReasonKind.InterceptPartyDefeated =>
                "intercept_party_defeated",
            BattleEndReasonKind.InterceptMutualDestruction =>
                "intercept_mutual_destruction",
            BattleEndReasonKind.NodeOperationAllNodesCompleted =>
                "node_operation_all_nodes_completed",
            BattleEndReasonKind.NodeOperationPartyDefeated =>
                "node_operation_party_defeated",
            BattleEndReasonKind.ControlPlayerScoreReached =>
                "control_player_score_reached",
            BattleEndReasonKind.ControlHostileScoreReached =>
                "control_hostile_score_reached",
            BattleEndReasonKind.ControlScoresTied =>
                "control_scores_tied",
            BattleEndReasonKind.ControlPartyDefeated =>
                "control_party_defeated",
            _ => "none",
        };

    internal static string ToWireValue(BattleMapEdge edge) =>
        edge switch
        {
            BattleMapEdge.Left => "left",
            BattleMapEdge.Right => "right",
            BattleMapEdge.Top => "top",
            BattleMapEdge.Bottom => "bottom",
            _ => "unknown",
        };
}
