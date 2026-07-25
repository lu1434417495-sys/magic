using System.Collections;
using System.Collections.Generic;
using Godot;

internal readonly struct BattleSaveModifierTagReadView :
    IReadOnlyList<StringName>
{
    private static readonly StringNameList Empty = new();
    private readonly StringNameList _tags;

    internal BattleSaveModifierTagReadView(StringNameList tags)
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

internal readonly struct BattleSaveAbilityBonusReadView
{
    private static readonly BattleStringNameIntMap Empty = new();
    private readonly BattleStringNameIntMap _bonuses;

    internal BattleSaveAbilityBonusReadView(
        BattleStringNameIntMap bonuses
    )
    {
        _bonuses = bonuses;
    }

    private BattleStringNameIntMap Bonuses => _bonuses ?? Empty;

    internal bool IsPresent => _bonuses != null;

    internal int Count => Bonuses.Count;

    internal int Get(StringName ability, int fallback = 0) =>
        Bonuses.Get(ability, fallback);

    internal bool TryGetValue(StringName ability, out int bonus) =>
        Bonuses.TryGetValue(ability, out bonus);

    public Dictionary<StringName, int>.Enumerator GetEnumerator() =>
        Bonuses.GetStructEnumerator();
}

internal readonly record struct BattleUnitSaveModifierReadView(
    bool OwnerPresent,
    BattleSaveModifierTagReadView AdvantageTags,
    BattleSaveModifierTagReadView DisadvantageTags,
    BattleSaveModifierTagReadView ImmunityTags,
    BattleSaveAbilityBonusReadView BonusByAbility
)
{
    internal static BattleUnitSaveModifierReadView MissingOwner =>
        new(
            false,
            new BattleSaveModifierTagReadView(null),
            new BattleSaveModifierTagReadView(null),
            new BattleSaveModifierTagReadView(null),
            new BattleSaveAbilityBonusReadView(null)
        );
}

internal readonly record struct BattleUnitSaveModifierSnapshot(
    bool OwnerPresent,
    StringNameList AdvantageTags,
    StringNameList DisadvantageTags,
    StringNameList ImmunityTags,
    BattleStringNameIntMap BonusByAbility
)
{
    internal static BattleUnitSaveModifierSnapshot Present(
        StringNameList advantageTags,
        StringNameList disadvantageTags,
        StringNameList immunityTags,
        BattleStringNameIntMap bonusByAbility
    ) =>
        new(
            true,
            advantageTags,
            disadvantageTags,
            immunityTags,
            bonusByAbility
        );

    internal static BattleUnitSaveModifierSnapshot MissingOwner =>
        new(false, null, null, null, null);
}

internal sealed class BattleUnitSaveModifierState
{
    private StringNameList _advantageTags = new();
    private StringNameList _disadvantageTags = new();
    private StringNameList _immunityTags = new();
    private BattleStringNameIntMap _bonusByAbility = new();

    internal BattleUnitSaveModifierReadView GetReadView() =>
        new(
            true,
            new BattleSaveModifierTagReadView(_advantageTags),
            new BattleSaveModifierTagReadView(_disadvantageTags),
            new BattleSaveModifierTagReadView(_immunityTags),
            new BattleSaveAbilityBonusReadView(_bonusByAbility)
        );

    internal void ResetNormalized() =>
        ReplaceNormalized(null, null, null, null);

    internal void ReplaceNormalized(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags,
        IReadOnlyDictionary<StringName, int> bonusByAbility
    )
    {
        StringNameList normalizedAdvantageTags = NormalizeTags(
            advantageTags
        );
        StringNameList normalizedDisadvantageTags = NormalizeTags(
            disadvantageTags
        );
        StringNameList normalizedImmunityTags = NormalizeTags(
            immunityTags
        );
        BattleStringNameIntMap normalizedBonusByAbility =
            NormalizeBonuses(bonusByAbility);

        _advantageTags = normalizedAdvantageTags;
        _disadvantageTags = normalizedDisadvantageTags;
        _immunityTags = normalizedImmunityTags;
        _bonusByAbility = normalizedBonusByAbility;
    }

    internal void ReplaceTagsNormalized(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags
    )
    {
        StringNameList normalizedAdvantageTags = NormalizeTags(
            advantageTags
        );
        StringNameList normalizedDisadvantageTags = NormalizeTags(
            disadvantageTags
        );
        StringNameList normalizedImmunityTags = NormalizeTags(
            immunityTags
        );

        _advantageTags = normalizedAdvantageTags;
        _disadvantageTags = normalizedDisadvantageTags;
        _immunityTags = normalizedImmunityTags;
    }

    internal void ReplaceBonusesNormalized(
        IReadOnlyDictionary<StringName, int> bonusByAbility
    ) =>
        _bonusByAbility = NormalizeBonuses(bonusByAbility);

    internal void AppendTagsNormalized(
        IEnumerable<StringName> advantageTags,
        IEnumerable<StringName> disadvantageTags,
        IEnumerable<StringName> immunityTags
    )
    {
        AppendTagsNormalized(ref _advantageTags, advantageTags);
        AppendTagsNormalized(ref _disadvantageTags, disadvantageTags);
        AppendTagsNormalized(ref _immunityTags, immunityTags);
    }

    internal bool AddAdvantageNormalized(StringName tag) =>
        AddTagNormalized(ref _advantageTags, tag);

    internal bool AddDisadvantageNormalized(StringName tag) =>
        AddTagNormalized(ref _disadvantageTags, tag);

    internal bool AddImmunityNormalized(StringName tag) =>
        AddTagNormalized(ref _immunityTags, tag);

    internal bool ContainsAdvantage(StringName tag) =>
        !IsEmpty(tag) && _advantageTags?.Contains(tag) == true;

    internal bool ContainsDisadvantage(StringName tag) =>
        !IsEmpty(tag) && _disadvantageTags?.Contains(tag) == true;

    internal bool ContainsImmunity(StringName tag) =>
        !IsEmpty(tag) && _immunityTags?.Contains(tag) == true;

    internal int GetAbilityBonus(
        StringName ability,
        int fallback = 0
    ) =>
        !IsEmpty(ability)
        && _bonusByAbility != null
        && _bonusByAbility.TryGetValue(ability, out int bonus)
            ? bonus
            : fallback;

    internal bool AddAbilityBonusNormalized(
        StringName ability,
        int bonus
    )
    {
        if (IsEmpty(ability) || bonus == 0)
            return false;

        _bonusByAbility ??= new BattleStringNameIntMap();
        _bonusByAbility.TryGetValue(ability, out int existing);
        _bonusByAbility.Put(ability, existing + bonus);
        return true;
    }

    internal BattleUnitSaveModifierSnapshot CaptureRaw() =>
        BattleUnitSaveModifierSnapshot.Present(
            _advantageTags?.Duplicate(),
            _disadvantageTags?.Duplicate(),
            _immunityTags?.Duplicate(),
            _bonusByAbility?.Clone()
        );

    internal void RestoreRaw(
        BattleUnitSaveModifierSnapshot snapshot
    )
    {
        _advantageTags = snapshot.AdvantageTags?.Duplicate();
        _disadvantageTags = snapshot.DisadvantageTags?.Duplicate();
        _immunityTags = snapshot.ImmunityTags?.Duplicate();
        _bonusByAbility = snapshot.BonusByAbility?.Clone();
    }

    internal BattleUnitSaveModifierState DuplicateState() =>
        new()
        {
            _advantageTags =
                _advantageTags?.Duplicate() ?? new StringNameList(),
            _disadvantageTags =
                _disadvantageTags?.Duplicate() ?? new StringNameList(),
            _immunityTags =
                _immunityTags?.Duplicate() ?? new StringNameList(),
            _bonusByAbility =
                _bonusByAbility?.Clone() ?? new BattleStringNameIntMap(),
        };

    internal static BattleUnitSaveModifierState FromRaw(
        BattleUnitSaveModifierSnapshot snapshot
    )
    {
        var result = new BattleUnitSaveModifierState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static StringNameList NormalizeTags(
        IEnumerable<StringName> tags
    )
    {
        var normalized = new StringNameList();
        if (tags == null)
            return normalized;

        foreach (StringName tag in tags)
            AddTagNormalized(normalized, tag);
        return normalized;
    }

    private static BattleStringNameIntMap NormalizeBonuses(
        IReadOnlyDictionary<StringName, int> bonuses
    )
    {
        var normalized = new BattleStringNameIntMap();
        normalized.ReplaceWithTyped(bonuses);
        return normalized;
    }

    private static bool AddTagNormalized(
        ref StringNameList destination,
        StringName tag
    )
    {
        if (IsEmpty(tag))
            return false;

        destination ??= new StringNameList();
        return AddTagNormalized(destination, tag);
    }

    private static void AppendTagsNormalized(
        ref StringNameList destination,
        IEnumerable<StringName> tags
    )
    {
        if (tags == null)
            return;

        foreach (StringName tag in tags)
            AddTagNormalized(ref destination, tag);
    }

    private static bool AddTagNormalized(
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
