using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleVisionProficiencyTagReadView :
    IReadOnlyList<StringName>
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _tags;

    internal BattleVisionProficiencyTagReadView(StringNameList tags)
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

internal readonly record struct BattleUnitVisionProficiencyReadView(
    bool OwnerPresent,
    BattleVisionProficiencyTagReadView VisionTags,
    BattleVisionProficiencyTagReadView ProficiencyTags
)
{
    internal static BattleUnitVisionProficiencyReadView MissingOwner =>
        new(
            false,
            new BattleVisionProficiencyTagReadView(null),
            new BattleVisionProficiencyTagReadView(null)
        );
}

internal readonly record struct BattleUnitVisionProficiencySnapshot(
    bool OwnerPresent,
    StringNameList VisionTags,
    StringNameList ProficiencyTags
)
{
    internal static BattleUnitVisionProficiencySnapshot Present(
        StringNameList visionTags,
        StringNameList proficiencyTags
    ) =>
        new(true, visionTags, proficiencyTags);

    internal static BattleUnitVisionProficiencySnapshot MissingOwner =>
        new(false, null, null);
}

internal sealed class BattleUnitVisionProficiencyState
{
    private StringNameList _visionTags = new();
    private StringNameList _proficiencyTags = new();

    internal BattleUnitVisionProficiencyReadView GetReadView() =>
        new(
            true,
            new BattleVisionProficiencyTagReadView(_visionTags),
            new BattleVisionProficiencyTagReadView(_proficiencyTags)
        );

    internal bool ContainsVision(StringName tag) =>
        !IsEmpty(tag) && _visionTags?.Contains(tag) == true;

    internal bool ContainsProficiency(StringName tag) =>
        !IsEmpty(tag) && _proficiencyTags?.Contains(tag) == true;

    internal void ReplaceNormalized(
        IEnumerable<StringName> visionTags,
        IEnumerable<StringName> proficiencyTags
    )
    {
        StringNameList normalizedVisionTags = Normalize(visionTags);
        StringNameList normalizedProficiencyTags = Normalize(proficiencyTags);
        _visionTags = normalizedVisionTags;
        _proficiencyTags = normalizedProficiencyTags;
    }

    internal void ResetNormalized() =>
        ReplaceNormalized(null, null);

    internal bool AddVisionNormalized(StringName tag)
    {
        if (IsEmpty(tag))
            return false;

        _visionTags ??= new StringNameList();
        return AddNormalized(_visionTags, tag);
    }

    internal bool AddProficiencyNormalized(StringName tag)
    {
        if (IsEmpty(tag))
            return false;

        _proficiencyTags ??= new StringNameList();
        return AddNormalized(_proficiencyTags, tag);
    }

    internal BattleUnitVisionProficiencySnapshot CaptureRaw() =>
        BattleUnitVisionProficiencySnapshot.Present(
            _visionTags?.Duplicate(),
            _proficiencyTags?.Duplicate()
        );

    internal void RestoreRaw(BattleUnitVisionProficiencySnapshot snapshot)
    {
        _visionTags = snapshot.VisionTags?.Duplicate();
        _proficiencyTags = snapshot.ProficiencyTags?.Duplicate();
    }

    internal BattleUnitVisionProficiencyState DuplicateState() =>
        new()
        {
            _visionTags = _visionTags?.Duplicate() ?? new StringNameList(),
            _proficiencyTags =
                _proficiencyTags?.Duplicate()
                ?? new StringNameList(),
        };

    internal static BattleUnitVisionProficiencyState FromRaw(
        BattleUnitVisionProficiencySnapshot snapshot
    )
    {
        var result = new BattleUnitVisionProficiencyState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static StringNameList Normalize(
        IEnumerable<StringName> tags
    )
    {
        var normalized = new StringNameList();
        if (tags == null)
            return normalized;

        foreach (StringName tag in tags)
            AddNormalized(normalized, tag);
        return normalized;
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
