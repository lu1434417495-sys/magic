using System.Collections.Generic;
using Godot;

internal readonly record struct BattleUnitCombatResourceUnlockSnapshot(
    bool OwnerPresent,
    StringNameList ResourceIds
)
{
    internal static BattleUnitCombatResourceUnlockSnapshot Present(
        StringNameList resourceIds
    ) =>
        new(true, resourceIds);

    internal static BattleUnitCombatResourceUnlockSnapshot MissingOwner =>
        new(false, null);
}

internal readonly struct BattleCombatResourceUnlockReadView
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _values;

    internal BattleCombatResourceUnlockReadView(StringNameList values)
    {
        _values = values;
    }

    private StringNameList Values => _values ?? Empty;

    internal bool IsPresent => _values != null;

    internal int Count => Values.Count;

    internal StringName this[int index] => Values[index];

    internal bool Contains(StringName resourceId) =>
        Values.Contains(resourceId);

    public List<StringName>.Enumerator GetEnumerator() =>
        Values.GetEnumerator();
}

internal readonly record struct BattleUnitCombatResourceUnlockReadView(
    bool OwnerPresent,
    BattleCombatResourceUnlockReadView ResourceIds
)
{
    internal static BattleUnitCombatResourceUnlockReadView MissingOwner =>
        new(false, new BattleCombatResourceUnlockReadView(null));
}

internal sealed class BattleUnitCombatResourceUnlockState
{
    private StringNameList _resourceIds =
        new(CombatResourceIds.DefaultUnlocked);

    internal BattleUnitCombatResourceUnlockReadView GetReadView() =>
        new(
            true,
            new BattleCombatResourceUnlockReadView(_resourceIds)
        );

    internal bool Contains(StringName resourceId) =>
        _resourceIds?.Contains(resourceId) == true;

    internal bool Unlock(StringName resourceId)
    {
        if (!IsValid(resourceId))
            return false;

        _resourceIds ??= new StringNameList();
        if (_resourceIds.Contains(resourceId))
            return false;

        _resourceIds.Add(resourceId);
        return true;
    }

    internal void ReplaceNormalized(IEnumerable<StringName> resourceIds)
    {
        var normalized = new StringNameList();
        if (resourceIds != null)
        {
            foreach (StringName resourceId in resourceIds)
                AddNormalized(normalized, resourceId);
        }

        AppendDefaults(normalized);
        _resourceIds = normalized;
    }

    internal void SyncDefaults()
    {
        _resourceIds ??= new StringNameList();
        AppendDefaults(_resourceIds);
    }

    internal BattleUnitCombatResourceUnlockSnapshot CaptureRaw() =>
        BattleUnitCombatResourceUnlockSnapshot.Present(
            _resourceIds?.Duplicate()
        );

    internal void RestoreRaw(
        BattleUnitCombatResourceUnlockSnapshot snapshot
    )
    {
        _resourceIds = snapshot.ResourceIds?.Duplicate();
    }

    internal BattleUnitCombatResourceUnlockState DuplicateState() =>
        FromRaw(CaptureRaw());

    internal static BattleUnitCombatResourceUnlockState FromRaw(
        BattleUnitCombatResourceUnlockSnapshot snapshot
    )
    {
        var result = new BattleUnitCombatResourceUnlockState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static void AppendDefaults(StringNameList destination)
    {
        foreach (StringName resourceId in CombatResourceIds.DefaultUnlocked)
            AddNormalized(destination, resourceId);
    }

    private static bool AddNormalized(
        StringNameList destination,
        StringName resourceId
    )
    {
        if (!IsValid(resourceId) || destination.Contains(resourceId))
            return false;

        destination.Add(resourceId);
        return true;
    }

    private static bool IsValid(StringName resourceId) =>
        !IsEmpty(resourceId)
        && CombatResourceIds.ToResourceKind(resourceId)
            != CombatResourceIdKind.Unknown;

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
