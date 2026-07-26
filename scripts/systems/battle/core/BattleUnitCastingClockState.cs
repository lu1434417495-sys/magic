using System;

internal readonly record struct BattleUnitCastingClockSnapshot(
    bool OwnerPresent,
    int CastProgressRateRemainder
)
{
    internal static BattleUnitCastingClockSnapshot Present(
        int castProgressRateRemainder
    ) =>
        new(true, castProgressRateRemainder);

    internal static BattleUnitCastingClockSnapshot MissingOwner =>
        new(false, 0);
}

internal sealed class BattleUnitCastingClockState
{
    private int _castProgressRateRemainder;

    internal int GetProgressRateRemainder() =>
        _castProgressRateRemainder;

    internal int ConsumeRateScaledGain(int baseProgressDelta, int ratePercent)
    {
        if (baseProgressDelta <= 0 || ratePercent <= 0)
            return 0;

        int raw =
            baseProgressDelta * ratePercent
            + Math.Max(_castProgressRateRemainder, 0);
        _castProgressRateRemainder = raw % 100;
        return raw / 100;
    }

    internal BattleUnitCastingClockSnapshot CaptureRaw() =>
        BattleUnitCastingClockSnapshot.Present(
            _castProgressRateRemainder
        );

    internal void RestoreRaw(BattleUnitCastingClockSnapshot snapshot)
    {
        _castProgressRateRemainder =
            snapshot.CastProgressRateRemainder;
    }

    internal BattleUnitCastingClockState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitCastingClockState FromRaw(
        BattleUnitCastingClockSnapshot snapshot
    )
    {
        var result = new BattleUnitCastingClockState();
        result.RestoreRaw(snapshot);
        return result;
    }
}
