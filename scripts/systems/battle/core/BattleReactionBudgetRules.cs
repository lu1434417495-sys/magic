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
                % BattleTimeRules.TuGranularity
                != 0
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(config),
                "reaction interval must use timeline granularity"
            );
        }
    }
    internal static void InitializeUnitForAdmission(
            BattleState state,
            BattleUnitState unit
        )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(unit);
        int currentTu = Math.Max(
            state.timeline?.current_tu ?? 0,
            0
        );
        BattleReactionBudgetConfig config =
            BattleReactionBudgetRules.EngineDefault;
        BattleReactionBudgetRules.Validate(config);
        if (!unit.CaptureReactionRawTyped().OwnerPresent)
        {
            unit.InitializeReactionBudgetTyped(
                currentTu,
                config,
                startFull: true
            );
        }
        if (
            !unit
                .CaptureCounterattackCapabilitiesRawTyped()
                .OwnerPresent
        )
        {
            unit.ReplaceCounterattackCapabilitiesTyped(
                Array.Empty<BattleCounterattackCapability>()
            );
        }
    }

}
