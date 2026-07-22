public enum BattleSimTerminationKind
{
    InvalidRuntime = 0,
    BattleEnded = 1,
    IdleStall = 2,
    IterationBudgetExhausted = 3,
}

internal static class BattleSimTerminationKindCodec
{
    internal static string ToWireValue(BattleSimTerminationKind kind) =>
        kind switch
        {
            BattleSimTerminationKind.BattleEnded => "battle_ended",
            BattleSimTerminationKind.IdleStall => "idle_stall",
            BattleSimTerminationKind.IterationBudgetExhausted =>
                "iteration_budget_exhausted",
            _ => "invalid_runtime",
        };
}
