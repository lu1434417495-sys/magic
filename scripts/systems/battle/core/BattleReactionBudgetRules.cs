using System;

internal static class BattleReactionBudgetRules
{
    internal static BattleReactionBudgetConfig EngineDefault =>
        new(ChargeCapacity: 1, RechargeIntervalTu: 60);

    internal static void Validate(
        BattleReactionBudgetConfig config
    )
    {
        if (config.ChargeCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        if (
            config.RechargeIntervalTu <= 0
            || config.RechargeIntervalTu
                % BattleTimelineState.TuGranularity
                != 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "reaction interval must use timeline granularity"
            );
        }
    }
}
