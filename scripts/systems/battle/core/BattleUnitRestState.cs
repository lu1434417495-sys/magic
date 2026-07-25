internal readonly record struct BattleUnitRestSnapshot(
    bool OwnerPresent,
    bool IsResting
)
{
    internal static BattleUnitRestSnapshot Present(bool isResting) =>
        new(true, isResting);

    internal static BattleUnitRestSnapshot MissingOwner =>
        new(false, false);
}

internal sealed class BattleUnitRestState
{
    private bool _isResting;

    internal bool IsResting() => _isResting;

    internal void MarkResting() => _isResting = true;

    internal void ClearResting() => _isResting = false;

    internal BattleUnitRestSnapshot CaptureRaw() =>
        BattleUnitRestSnapshot.Present(_isResting);

    internal void RestoreRaw(BattleUnitRestSnapshot snapshot) =>
        _isResting = snapshot.IsResting;

    internal BattleUnitRestState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitRestState FromRaw(
        BattleUnitRestSnapshot snapshot
    )
    {
        var result = new BattleUnitRestState();
        result.RestoreRaw(snapshot);
        return result;
    }
}
