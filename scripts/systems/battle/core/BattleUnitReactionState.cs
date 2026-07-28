using System;

internal readonly record struct BattleReactionBudgetConfig(
    int ChargeCapacity,
    int RechargeIntervalTu
);

internal readonly record struct BattleUnitReactionSnapshot(
    bool OwnerPresent,
    int ChargesRemaining,
    int ChargeCapacity,
    int RechargeIntervalTu,
    int NextRechargeAtTu
)
{
    internal static BattleUnitReactionSnapshot MissingOwner =>
        new(false, 0, 0, 0, 0);
}

internal sealed class BattleUnitReactionState
{
    private int _chargesRemaining;
    private int _chargeCapacity;
    private int _rechargeIntervalTu;
    private int _nextRechargeAtTu;

    internal void Initialize(
        int currentTu,
        BattleReactionBudgetConfig config,
        bool startFull
    )
    {
        if (currentTu < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTu));
        if (config.ChargeCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        if (config.RechargeIntervalTu <= 0)
            throw new ArgumentOutOfRangeException(nameof(config));
        _chargeCapacity = config.ChargeCapacity;
        _rechargeIntervalTu = config.RechargeIntervalTu;
        _chargesRemaining = startFull ? _chargeCapacity : 0;
        _nextRechargeAtTu = checked(
            currentTu + _rechargeIntervalTu
        );
    }

    internal bool HasCharge() => _chargesRemaining > 0;

    internal void ValidateConsumeChargeKnownAvailable()
    {
        if (_chargesRemaining <= 0)
        {
            throw new InvalidOperationException(
                "reaction charge is not available"
            );
        }
    }

    internal void CommitConsumeChargeKnownAvailable()
    {
        _chargesRemaining -= 1;
    }

    internal bool AdvanceAndRefill(int currentTu)
    {
        if (currentTu < 0)
            throw new ArgumentOutOfRangeException(nameof(currentTu));
        if (_rechargeIntervalTu <= 0)
        {
            throw new InvalidOperationException(
                "reaction state is not initialized"
            );
        }
        if (currentTu < _nextRechargeAtTu)
            return false;

        long crossed =
            ((long)currentTu - _nextRechargeAtTu)
            / _rechargeIntervalTu
            + 1L;
        int nextRechargeAtTu = checked(
            (int)(
                (long)_nextRechargeAtTu
                + crossed * _rechargeIntervalTu
            )
        );
        bool changed =
            nextRechargeAtTu != _nextRechargeAtTu
            || _chargesRemaining != _chargeCapacity;
        _nextRechargeAtTu = nextRechargeAtTu;
        _chargesRemaining = _chargeCapacity;
        return changed;
    }

    internal bool AdvanceFrozenAnchor(int elapsedTu)
    {
        if (elapsedTu < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedTu));
        if (elapsedTu == 0)
            return false;
        if (_rechargeIntervalTu <= 0)
        {
            throw new InvalidOperationException(
                "reaction state is not initialized"
            );
        }
        _nextRechargeAtTu = checked(
            (int)((long)_nextRechargeAtTu + elapsedTu)
        );
        return true;
    }

    internal BattleUnitReactionSnapshot CaptureRaw() =>
        new(
            true,
            _chargesRemaining,
            _chargeCapacity,
            _rechargeIntervalTu,
            _nextRechargeAtTu
        );

    internal void RestoreRaw(BattleUnitReactionSnapshot snapshot)
    {
        if (
            !snapshot.OwnerPresent
            || snapshot.ChargeCapacity < 0
            || snapshot.ChargesRemaining < 0
            || snapshot.ChargesRemaining > snapshot.ChargeCapacity
            || snapshot.RechargeIntervalTu <= 0
            || snapshot.NextRechargeAtTu < 0
        )
        {
            throw new ArgumentException(
                "reaction snapshot is invalid",
                nameof(snapshot)
            );
        }
        _chargesRemaining = snapshot.ChargesRemaining;
        _chargeCapacity = snapshot.ChargeCapacity;
        _rechargeIntervalTu = snapshot.RechargeIntervalTu;
        _nextRechargeAtTu = snapshot.NextRechargeAtTu;
    }

    internal BattleUnitReactionState DuplicateState()
    {
        var duplicate = new BattleUnitReactionState();
        duplicate.RestoreRaw(CaptureRaw());
        return duplicate;
    }
}
