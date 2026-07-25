using System.Collections.Generic;
using Godot;

internal readonly struct BattleDamageResistanceReadView
{
    private static readonly BattleStringNameMap Empty = new();
    private readonly BattleStringNameMap _resistances;

    internal BattleDamageResistanceReadView(
        BattleStringNameMap resistances
    )
    {
        _resistances = resistances;
    }

    private BattleStringNameMap Resistances =>
        _resistances ?? Empty;

    internal bool IsPresent => _resistances != null;

    internal int Count => Resistances.Count;

    internal bool Contains(StringName damageTag) =>
        Resistances.ContainsKey(damageTag);

    internal StringName Get(
        StringName damageTag,
        StringName fallback = default
    ) =>
        Resistances.Get(damageTag, fallback);

    internal bool TryGetValue(
        StringName damageTag,
        out StringName mitigationTier
    ) =>
        Resistances.TryGetValue(damageTag, out mitigationTier);

    public Dictionary<StringName, StringName>.Enumerator
        GetEnumerator() =>
            Resistances.GetStructEnumerator();
}

internal readonly record struct BattleUnitDamageResistanceReadView(
    bool OwnerPresent,
    BattleDamageResistanceReadView Resistances
)
{
    internal static BattleUnitDamageResistanceReadView MissingOwner =>
        new(false, new BattleDamageResistanceReadView(null));
}

internal readonly record struct BattleUnitDamageResistanceSnapshot(
    bool OwnerPresent,
    BattleStringNameMap Resistances
)
{
    internal static BattleUnitDamageResistanceSnapshot Present(
        BattleStringNameMap resistances
    ) =>
        new(true, resistances);

    internal static BattleUnitDamageResistanceSnapshot MissingOwner =>
        new(false, null);
}

internal sealed class BattleUnitDamageResistanceState
{
    private BattleStringNameMap _resistances = new();

    internal BattleUnitDamageResistanceReadView GetReadView() =>
        new(
            true,
            new BattleDamageResistanceReadView(_resistances)
        );

    internal void ResetNormalized() =>
        _resistances = new BattleStringNameMap();

    internal void ReplaceNormalized(
        IReadOnlyDictionary<StringName, StringName> resistances
    )
    {
        var normalized = new BattleStringNameMap();
        normalized.ReplaceWithTyped(resistances);
        _resistances = normalized;
    }

    internal void MergeOverrideNormalized(
        IReadOnlyDictionary<StringName, StringName> resistances
    )
    {
        if (resistances == null)
            return;

        foreach (
            (StringName damageTag, StringName mitigationTier)
            in resistances
        )
        {
            SetNormalized(damageTag, mitigationTier);
        }
    }

    internal bool SetNormalized(
        StringName damageTag,
        StringName mitigationTier
    )
    {
        if (IsEmpty(damageTag) || IsEmpty(mitigationTier))
            return false;

        _resistances ??= new BattleStringNameMap();
        _resistances.Put(damageTag, mitigationTier);
        return true;
    }

    internal bool Contains(StringName damageTag) =>
        !IsEmpty(damageTag)
        && _resistances?.ContainsKey(damageTag) == true;

    internal StringName Get(
        StringName damageTag,
        StringName fallback = default
    ) =>
        !IsEmpty(damageTag)
        && _resistances != null
        && _resistances.TryGetValue(
            damageTag,
            out StringName mitigationTier
        )
            ? mitigationTier
            : fallback;

    internal bool TryGetValue(
        StringName damageTag,
        out StringName mitigationTier
    )
    {
        mitigationTier = default;
        return !IsEmpty(damageTag)
            && _resistances?.TryGetValue(
                damageTag,
                out mitigationTier
            ) == true;
    }

    internal Dictionary<StringName, StringName> CopyNormalized() =>
        _resistances?.ToTypedDictionary()
        ?? new Dictionary<StringName, StringName>();

    internal BattleUnitDamageResistanceSnapshot CaptureRaw() =>
        BattleUnitDamageResistanceSnapshot.Present(
            _resistances?.Clone()
        );

    internal void RestoreRaw(
        BattleUnitDamageResistanceSnapshot snapshot
    )
    {
        _resistances = snapshot.Resistances?.Clone();
    }

    internal BattleUnitDamageResistanceState DuplicateState() =>
        new()
        {
            _resistances =
                _resistances?.Clone() ?? new BattleStringNameMap(),
        };

    internal static BattleUnitDamageResistanceState FromRaw(
        BattleUnitDamageResistanceSnapshot snapshot
    )
    {
        var result = new BattleUnitDamageResistanceState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
