using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct BattleUnitKnownSkillSnapshot(
    bool OwnerPresent,
    StringNameList ActiveSkillIds,
    BattleStringNameIntMap SkillLevels,
    BattleStringNameIntMap LockHitBonuses
)
{
    internal static BattleUnitKnownSkillSnapshot Present(
        StringNameList activeSkillIds,
        BattleStringNameIntMap skillLevels,
        BattleStringNameIntMap lockHitBonuses
    ) =>
        new(
            true,
            activeSkillIds,
            skillLevels,
            lockHitBonuses
        );

    internal static BattleUnitKnownSkillSnapshot MissingOwner =>
        new(false, null, null, null);
}

internal readonly struct BattleKnownActiveSkillReadView
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _values;

    internal BattleKnownActiveSkillReadView(StringNameList values)
    {
        _values = values;
    }

    private StringNameList Values => _values ?? Empty;

    internal bool IsPresent => _values != null;

    internal int Count => Values.Count;

    internal StringName this[int index] => Values[index];

    internal bool Contains(StringName skillId) =>
        !IsEmpty(skillId) && Values.Contains(skillId);

    internal bool TryGetFirst(out StringName skillId)
    {
        if (Values.Count == 0)
        {
            skillId = new StringName("");
            return false;
        }

        skillId = Values[0];
        return true;
    }

    public List<StringName>.Enumerator GetEnumerator() =>
        Values.GetEnumerator();

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}

internal readonly struct BattleKnownSkillLevelReadView
{
    private static readonly BattleStringNameIntMap Empty = new();
    private readonly BattleStringNameIntMap _values;

    internal BattleKnownSkillLevelReadView(BattleStringNameIntMap values)
    {
        _values = values;
    }

    private BattleStringNameIntMap Values => _values ?? Empty;

    internal bool IsPresent => _values != null;

    internal int Count => Values.Count;

    public Dictionary<StringName, int>.Enumerator GetEnumerator() =>
        Values.GetStructEnumerator();
}

internal readonly record struct BattleUnitKnownSkillReadView(
    bool OwnerPresent,
    BattleKnownActiveSkillReadView ActiveSkills,
    BattleKnownSkillLevelReadView SkillLevels,
    BattleKnownSkillLevelReadView LockHitBonuses
)
{
    internal static BattleUnitKnownSkillReadView MissingOwner =>
        new(
            false,
            new BattleKnownActiveSkillReadView(null),
            new BattleKnownSkillLevelReadView(null),
            new BattleKnownSkillLevelReadView(null)
        );
}

internal sealed class BattleUnitKnownSkillState
{
    private StringNameList _activeSkillIds = new();
    private BattleStringNameIntMap _skillLevels = new();
    private BattleStringNameIntMap _lockHitBonuses = new();

    internal BattleKnownActiveSkillReadView GetActiveSkillsView() =>
        new(_activeSkillIds);

    internal BattleUnitKnownSkillReadView GetReadView() =>
        new(
            true,
            new BattleKnownActiveSkillReadView(_activeSkillIds),
            new BattleKnownSkillLevelReadView(_skillLevels),
            new BattleKnownSkillLevelReadView(_lockHitBonuses)
        );

    internal bool KnowsActiveSkill(StringName skillId) =>
        !IsEmpty(skillId)
        && _activeSkillIds != null
        && _activeSkillIds.Contains(skillId);

    internal bool TryGetFirstActiveSkill(out StringName skillId) =>
        GetActiveSkillsView().TryGetFirst(out skillId);

    internal void ReplaceActiveSkillsNormalized(
        IEnumerable<StringName> skillIds
    )
    {
        _activeSkillIds = new StringNameList();
        if (skillIds == null)
            return;

        HashSet<StringName> seen = new();
        foreach (StringName rawSkillId in skillIds)
        {
            StringName skillId = Normalize(rawSkillId);
            if (IsEmpty(skillId) || !seen.Add(skillId))
                continue;
            _activeSkillIds.Add(skillId);
        }
    }

    internal void AddActiveSkillNormalized(StringName rawSkillId)
    {
        StringName skillId = Normalize(rawSkillId);
        if (IsEmpty(skillId))
            return;

        _activeSkillIds ??= new StringNameList();
        if (!_activeSkillIds.Contains(skillId))
            _activeSkillIds.Add(skillId);
    }

    internal int GetSkillLevel(StringName skillId, int fallback = 0) =>
        !IsEmpty(skillId)
        && _skillLevels != null
        && _skillLevels.ContainsKey(skillId)
            ? _skillLevels.Get(skillId, fallback)
            : fallback;

    internal bool HasSkillLevel(StringName skillId) =>
        !IsEmpty(skillId)
        && _skillLevels != null
        && _skillLevels.ContainsKey(skillId);

    internal void ReplaceSkillLevelsNormalized(
        IReadOnlyDictionary<StringName, int> values,
        bool preserveZero
    )
    {
        _skillLevels = new BattleStringNameIntMap();
        if (values == null)
            return;

        foreach (KeyValuePair<StringName, int> entry in values)
            SetSkillLevelNormalized(entry.Key, entry.Value, preserveZero);
    }

    internal void SetSkillLevelNormalized(
        StringName skillId,
        int level,
        bool preserveZero
    )
    {
        if (IsEmpty(skillId))
            return;

        int normalizedLevel = Math.Max(level, 0);
        if (normalizedLevel <= 0 && !preserveZero)
        {
            _skillLevels?.Remove(skillId);
            return;
        }

        _skillLevels ??= new BattleStringNameIntMap();
        _skillLevels.Put(skillId, normalizedLevel);
    }

    internal void RemoveSkillLevel(StringName skillId)
    {
        if (!IsEmpty(skillId))
            _skillLevels?.Remove(skillId);
    }

    internal void CopySkillLevelEntriesTo(
        List<KeyValuePair<StringName, int>> destination
    )
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (_skillLevels == null)
            return;

        Dictionary<StringName, int>.Enumerator enumerator =
            _skillLevels.GetStructEnumerator();
        while (enumerator.MoveNext())
            destination.Add(enumerator.Current);
    }

    internal int GetLockHitBonus(StringName skillId, int fallback = 0) =>
        !IsEmpty(skillId)
        && _lockHitBonuses != null
        && _lockHitBonuses.TryGetValue(skillId, out int value)
            ? value
            : fallback;

    internal void ReplaceLockHitBonusesNormalized(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        _lockHitBonuses = new BattleStringNameIntMap();
        if (values == null)
            return;

        foreach (KeyValuePair<StringName, int> entry in values)
            SetLockHitBonusNormalized(entry.Key, entry.Value);
    }

    internal void SetLockHitBonusNormalized(
        StringName skillId,
        int bonus
    )
    {
        if (IsEmpty(skillId))
            return;

        int normalizedBonus = Math.Max(bonus, 0);
        if (normalizedBonus <= 0)
        {
            _lockHitBonuses?.Remove(skillId);
            return;
        }

        _lockHitBonuses ??= new BattleStringNameIntMap();
        _lockHitBonuses.Put(skillId, normalizedBonus);
    }

    internal List<StringName> SnapshotActiveSkills() =>
        _activeSkillIds == null
            ? new List<StringName>()
            : new List<StringName>(_activeSkillIds);

    internal Dictionary<StringName, int> SnapshotSkillLevels() =>
        _skillLevels?.ToTypedDictionary()
        ?? new Dictionary<StringName, int>();

    internal Dictionary<StringName, int> SnapshotLockHitBonuses() =>
        _lockHitBonuses?.ToTypedDictionary()
        ?? new Dictionary<StringName, int>();

    internal BattleUnitKnownSkillSnapshot CaptureRaw() =>
        BattleUnitKnownSkillSnapshot.Present(
            _activeSkillIds?.Duplicate(),
            _skillLevels?.Clone(),
            _lockHitBonuses?.Clone()
        );

    internal void RestoreRaw(BattleUnitKnownSkillSnapshot snapshot)
    {
        _activeSkillIds = snapshot.ActiveSkillIds?.Duplicate();
        _skillLevels = snapshot.SkillLevels?.Clone();
        _lockHitBonuses = snapshot.LockHitBonuses?.Clone();
    }

    internal BattleUnitKnownSkillState DuplicateState() =>
        new()
        {
            _activeSkillIds =
                _activeSkillIds?.Duplicate() ?? new StringNameList(),
            _skillLevels =
                _skillLevels?.Clone() ?? new BattleStringNameIntMap(),
            _lockHitBonuses =
                _lockHitBonuses?.Clone() ?? new BattleStringNameIntMap(),
        };

    internal static BattleUnitKnownSkillState FromRaw(
        BattleUnitKnownSkillSnapshot snapshot
    )
    {
        var result = new BattleUnitKnownSkillState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static StringName Normalize(StringName value) =>
        value ?? new StringName("");

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
