using System;

internal readonly record struct BattleUnitActionClockSnapshot(
    bool OwnerPresent,
    int ActionProgress,
    int ActionThreshold,
    int ActionProgressRateRemainder
)
{
    internal static BattleUnitActionClockSnapshot Present(
        int actionProgress,
        int actionThreshold,
        int actionProgressRateRemainder
    ) =>
        new(
            true,
            actionProgress,
            actionThreshold,
            actionProgressRateRemainder
        );

    internal static BattleUnitActionClockSnapshot MissingOwner =>
        new(false, 0, 0, 0);
}

internal sealed class BattleUnitActionClockState
{
    private int _actionProgress;
    private int _actionThreshold = BattleUnitState.DefaultActionThreshold;
    private int _actionProgressRateRemainder;

    internal int GetProgress() => _actionProgress;

    internal void SetProgressRaw(int value) => _actionProgress = value;

    internal int GetThreshold() => _actionThreshold;

    internal void SetThresholdRaw(int value) => _actionThreshold = value;

    internal int GetProgressRateRemainder() =>
        _actionProgressRateRemainder;

    internal int ConsumeRateScaledGain(int baseProgressDelta, int ratePercent)
    {
        if (baseProgressDelta <= 0 || ratePercent <= 0)
            return 0;

        int raw =
            baseProgressDelta * ratePercent
            + Math.Max(_actionProgressRateRemainder, 0);
        _actionProgressRateRemainder = raw % 100;
        return raw / 100;
    }

    internal bool AdvanceAndConsumeThresholds(
        int progressGain,
        int positiveThreshold
    )
    {
        if (positiveThreshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(positiveThreshold));

        _actionProgress += progressGain;
        bool crossedThreshold = false;
        while (_actionProgress >= positiveThreshold)
        {
            _actionProgress -= positiveThreshold;
            crossedThreshold = true;
        }
        return crossedThreshold;
    }

    internal BattleUnitActionClockSnapshot CaptureRaw() =>
        BattleUnitActionClockSnapshot.Present(
            _actionProgress,
            _actionThreshold,
            _actionProgressRateRemainder
        );

    internal void RestoreRaw(BattleUnitActionClockSnapshot snapshot)
    {
        _actionProgress = snapshot.ActionProgress;
        _actionThreshold = snapshot.ActionThreshold;
        _actionProgressRateRemainder =
            snapshot.ActionProgressRateRemainder;
    }

    internal BattleUnitActionClockState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitActionClockState FromRaw(
        BattleUnitActionClockSnapshot snapshot
    )
    {
        var result = new BattleUnitActionClockState();
        result.RestoreRaw(snapshot);
        return result;
    }
}
