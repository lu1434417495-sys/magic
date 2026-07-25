internal readonly record struct BattleUnitTurnSnapshot(
    bool OwnerPresent,
    bool HasTakenActionThisTurn,
    bool HasMovedThisTurn,
    bool CanUseLockedMovePointsThisTurn,
    bool CastingExhausted
)
{
    internal static BattleUnitTurnSnapshot Present(
        bool hasTakenActionThisTurn,
        bool hasMovedThisTurn,
        bool canUseLockedMovePointsThisTurn,
        bool castingExhausted
    ) =>
        new(
            true,
            hasTakenActionThisTurn,
            hasMovedThisTurn,
            canUseLockedMovePointsThisTurn,
            castingExhausted
        );

    internal static BattleUnitTurnSnapshot MissingOwner =>
        new(false, false, false, false, false);
}

internal sealed class BattleUnitTurnState
{
    private bool _hasTakenActionThisTurn;
    private bool _hasMovedThisTurn;
    private bool _canUseLockedMovePointsThisTurn;
    private bool _castingExhausted;

    internal bool HasTakenActionThisTurn() => _hasTakenActionThisTurn;

    internal bool HasMovedThisTurn() => _hasMovedThisTurn;

    internal bool CanUseLockedMovePointsThisTurn() =>
        _canUseLockedMovePointsThisTurn;

    internal bool IsCastingExhausted() => _castingExhausted;

    internal bool IsNormalMovementLocked() =>
        _hasTakenActionThisTurn || _hasMovedThisTurn;

    internal void MarkActionTaken() => _hasTakenActionThisTurn = true;

    internal void MarkMoved() => _hasMovedThisTurn = true;

    internal void GrantLockedMovePoints() =>
        _canUseLockedMovePointsThisTurn = true;

    internal void MarkCastingExhausted() => _castingExhausted = true;

    internal void ResetForTurnStart()
    {
        _hasTakenActionThisTurn = false;
        _hasMovedThisTurn = false;
        _canUseLockedMovePointsThisTurn = false;
        _castingExhausted = false;
    }

    internal void ClearCastingExhaustion() => _castingExhausted = false;

    internal BattleUnitTurnSnapshot CaptureRaw() =>
        BattleUnitTurnSnapshot.Present(
            _hasTakenActionThisTurn,
            _hasMovedThisTurn,
            _canUseLockedMovePointsThisTurn,
            _castingExhausted
        );

    internal void RestoreRaw(BattleUnitTurnSnapshot snapshot)
    {
        _hasTakenActionThisTurn = snapshot.HasTakenActionThisTurn;
        _hasMovedThisTurn = snapshot.HasMovedThisTurn;
        _canUseLockedMovePointsThisTurn =
            snapshot.CanUseLockedMovePointsThisTurn;
        _castingExhausted = snapshot.CastingExhausted;
    }

    internal BattleUnitTurnState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitTurnState FromRaw(BattleUnitTurnSnapshot snapshot)
    {
        var result = new BattleUnitTurnState();
        result.RestoreRaw(snapshot);
        return result;
    }
}
