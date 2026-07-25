using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleCreatureTypeTagReadView :
    IReadOnlyList<StringName>
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _tags;

    internal BattleCreatureTypeTagReadView(StringNameList tags)
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

internal readonly record struct BattleUnitCreatureTypeReadView(
    bool OwnerPresent,
    BattleCreatureTypeTagReadView Tags
)
{
    internal static BattleUnitCreatureTypeReadView MissingOwner =>
        new(false, new BattleCreatureTypeTagReadView(null));
}

internal readonly record struct BattleUnitCreatureTypeSnapshot(
    bool OwnerPresent,
    StringNameList Tags
)
{
    internal static BattleUnitCreatureTypeSnapshot Present(
        StringNameList tags
    ) =>
        new(true, tags);

    internal static BattleUnitCreatureTypeSnapshot MissingOwner =>
        new(false, null);
}

internal sealed class BattleUnitCreatureTypeState
{
    private StringNameList _tags = new();

    internal BattleUnitCreatureTypeReadView GetReadView() =>
        new(true, new BattleCreatureTypeTagReadView(_tags));

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

    internal BattleUnitCreatureTypeSnapshot CaptureRaw() =>
        BattleUnitCreatureTypeSnapshot.Present(_tags?.Duplicate());

    internal void RestoreRaw(BattleUnitCreatureTypeSnapshot snapshot)
    {
        _tags = snapshot.Tags?.Duplicate();
    }

    internal BattleUnitCreatureTypeState DuplicateState() =>
        new()
        {
            _tags = _tags?.Duplicate() ?? new StringNameList(),
        };

    internal static BattleUnitCreatureTypeState FromRaw(
        BattleUnitCreatureTypeSnapshot snapshot
    )
    {
        var result = new BattleUnitCreatureTypeState();
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
