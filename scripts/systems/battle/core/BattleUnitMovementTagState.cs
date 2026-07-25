using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleMovementTagReadView :
    IReadOnlyList<StringName>
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _tags;

    internal BattleMovementTagReadView(StringNameList tags)
    {
        _tags = tags;
    }

    private StringNameList Tags => _tags ?? Empty;

    internal bool IsPresent => _tags != null;

    public int Count => Tags.Count;

    public StringName this[int index] => Tags[index];

    internal bool Contains(StringName tag) => Tags.Contains(tag);

    public List<StringName>.Enumerator GetEnumerator() =>
        Tags.GetEnumerator();

    IEnumerator<StringName> IEnumerable<StringName>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal readonly record struct BattleUnitMovementTagReadView(
    bool OwnerPresent,
    BattleMovementTagReadView Tags
)
{
    internal static BattleUnitMovementTagReadView MissingOwner =>
        new(false, new BattleMovementTagReadView(null));
}

internal readonly record struct BattleUnitMovementTagSnapshot(
    bool OwnerPresent,
    StringNameList Tags
)
{
    internal static BattleUnitMovementTagSnapshot Present(
        StringNameList tags
    ) =>
        new(true, tags);

    internal static BattleUnitMovementTagSnapshot MissingOwner =>
        new(false, null);
}

internal sealed class BattleUnitMovementTagState
{
    private StringNameList _tags = new();

    internal BattleUnitMovementTagReadView GetReadView() =>
        new(true, new BattleMovementTagReadView(_tags));

    internal bool Contains(StringName tag) =>
        !IsEmpty(tag) && _tags?.Contains(tag) == true;

    internal void ReplaceNormalized(IEnumerable<StringName> tags)
    {
        var normalized = new StringNameList();
        if (tags != null)
        {
            foreach (StringName tag in tags)
                AddNormalized(normalized, tag);
        }
        _tags = normalized;
    }

    internal bool AddNormalized(StringName tag)
    {
        if (IsEmpty(tag))
            return false;

        _tags ??= new StringNameList();
        return AddNormalized(_tags, tag);
    }

    internal BattleUnitMovementTagSnapshot CaptureRaw() =>
        BattleUnitMovementTagSnapshot.Present(_tags?.Duplicate());

    internal void RestoreRaw(BattleUnitMovementTagSnapshot snapshot)
    {
        _tags = snapshot.Tags?.Duplicate();
    }

    internal BattleUnitMovementTagState DuplicateState() =>
        new()
        {
            _tags = _tags?.Duplicate() ?? new StringNameList(),
        };

    internal static BattleUnitMovementTagState FromRaw(
        BattleUnitMovementTagSnapshot snapshot
    )
    {
        var result = new BattleUnitMovementTagState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static bool AddNormalized(
        StringNameList destination,
        StringName tag
    )
    {
        if (IsEmpty(tag) || destination.Contains(tag))
            return false;

        destination.Add(tag);
        return true;
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
