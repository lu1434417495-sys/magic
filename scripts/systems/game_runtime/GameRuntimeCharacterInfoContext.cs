using System;
using System.Collections.Generic;
using Godot;

internal enum GameRuntimeCharacterInfoSource
{
    World,
    Battle,
}

internal enum GameRuntimeCharacterInfoEntryKind
{
    Pair,
    Text,
}

internal sealed class GameRuntimeCharacterInfoEntry
{
    private GameRuntimeCharacterInfoEntry(
        GameRuntimeCharacterInfoEntryKind kind,
        string label,
        string value,
        string tooltip,
        string text
    )
    {
        Kind = kind;
        Label = label ?? "";
        Value = value ?? "";
        Tooltip = tooltip ?? "";
        Text = text ?? "";
    }

    internal GameRuntimeCharacterInfoEntryKind Kind { get; }

    internal string Label { get; }

    internal string Value { get; }

    internal string Tooltip { get; }

    internal string Text { get; }

    internal static GameRuntimeCharacterInfoEntry Pair(
        string label,
        string value,
        string tooltip = ""
    ) =>
        new(GameRuntimeCharacterInfoEntryKind.Pair, label, value, tooltip, "");

    internal static GameRuntimeCharacterInfoEntry TextEntry(string text) =>
        new(GameRuntimeCharacterInfoEntryKind.Text, "", "", "", text);

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        if (Kind == GameRuntimeCharacterInfoEntryKind.Text)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["text"] = Text,
            };
        }

        var result = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["label"] = Label,
            ["value"] = Value,
        };
        if (!string.IsNullOrEmpty(Tooltip))
            result["tooltip"] = Tooltip;
        return result;
    }
}

internal sealed class GameRuntimeCharacterInfoSection
{
    internal GameRuntimeCharacterInfoSection(
        string title,
        IEnumerable<GameRuntimeCharacterInfoEntry> entries
    )
    {
        Title = title ?? "";
        var copy = new List<GameRuntimeCharacterInfoEntry>();
        if (entries != null)
        {
            foreach (GameRuntimeCharacterInfoEntry entry in entries)
            {
                if (entry != null)
                    copy.Add(entry);
            }
        }
        Entries = copy.AsReadOnly();
    }

    internal string Title { get; }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> Entries { get; }

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        var entries = new List<object>();
        foreach (GameRuntimeCharacterInfoEntry entry in Entries)
            entries.Add(entry.BuildSnapshotPlain());
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["title"] = Title,
            ["entries"] = entries,
        };
    }
}

internal sealed class GameRuntimeCharacterInfoFate
{
    internal GameRuntimeCharacterInfoFate(
        int hiddenLuckAtBirth,
        int faithLuckBonus,
        int fortuneMarked,
        int doomMarked,
        int doomAuthority
    )
    {
        HiddenLuckAtBirth = hiddenLuckAtBirth;
        FaithLuckBonus = faithLuckBonus;
        FortuneMarked = fortuneMarked;
        DoomMarked = doomMarked;
        DoomAuthority = doomAuthority;
    }

    internal int HiddenLuckAtBirth { get; }

    internal int FaithLuckBonus { get; }

    internal int EffectiveLuck =>
        Math.Clamp(
            HiddenLuckAtBirth + FaithLuckBonus,
            UnitBaseAttributes.EffectiveLuckMin,
            UnitBaseAttributes.EffectiveLuckMax
        );

    internal int FortuneMarked { get; }

    internal int DoomMarked { get; }

    internal int DoomAuthority { get; }

    internal bool HasMisfortune => DoomAuthority > 0;

    // RuntimePlainPayload normalized the former Godot Variant integers to Int64.
    // Keep that exact plain-snapshot contract while the typed owner uses int values.
    internal Dictionary<string, object> BuildSnapshotPlain() =>
        new(StringComparer.Ordinal)
        {
            ["hidden_luck_at_birth"] = (long)HiddenLuckAtBirth,
            ["faith_luck_bonus"] = (long)FaithLuckBonus,
            ["effective_luck"] = (long)EffectiveLuck,
            ["fortune_marked"] = (long)FortuneMarked,
            ["doom_marked"] = (long)DoomMarked,
            ["doom_authority"] = (long)DoomAuthority,
            ["has_misfortune"] = HasMisfortune,
        };
}

internal sealed class GameRuntimeCharacterInfoContext
{
    internal GameRuntimeCharacterInfoContext(
        GameRuntimeCharacterInfoSource source,
        string displayName,
        string metaLabel,
        string statusLabel,
        IEnumerable<GameRuntimeCharacterInfoSection> sections,
        StringName unitId = default,
        StringName memberId = default,
        GameRuntimeCharacterInfoFate fate = null
    )
    {
        Source = source;
        DisplayName = displayName ?? "";
        MetaLabel = metaLabel ?? "";
        StatusLabel = statusLabel ?? "";
        UnitId = unitId;
        MemberId = memberId;
        Fate = fate;

        var copy = new List<GameRuntimeCharacterInfoSection>();
        if (sections != null)
        {
            foreach (GameRuntimeCharacterInfoSection section in sections)
            {
                if (section != null)
                    copy.Add(section);
            }
        }
        Sections = copy.AsReadOnly();
    }

    internal GameRuntimeCharacterInfoSource Source { get; }

    internal string DisplayName { get; }

    internal StringName UnitId { get; }

    internal StringName MemberId { get; }

    internal string MetaLabel { get; }

    internal string StatusLabel { get; }

    internal IReadOnlyList<GameRuntimeCharacterInfoSection> Sections { get; }

    internal GameRuntimeCharacterInfoFate Fate { get; }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var sections = new List<object>();
        foreach (GameRuntimeCharacterInfoSection section in Sections)
            sections.Add(section.BuildSnapshotPlain());

        var result = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["display_name"] = DisplayName,
            ["meta_label"] = MetaLabel,
            ["sections"] = sections,
            ["status_label"] = StatusLabel,
            ["source"] = SourceToPayloadValue(Source),
        };
        string unitId = NormalizeOptionalId(UnitId);
        if (!string.IsNullOrEmpty(unitId))
            result["unit_id"] = unitId;
        string memberId = NormalizeOptionalId(MemberId);
        if (!string.IsNullOrEmpty(memberId))
            result["member_id"] = memberId;
        if (Fate != null)
            result["fate"] = Fate.BuildSnapshotPlain();
        return result;
    }

    private static string NormalizeOptionalId(StringName value) =>
        value == null ? "" : value.ToString();

    private static string SourceToPayloadValue(GameRuntimeCharacterInfoSource source) =>
        source switch
        {
            GameRuntimeCharacterInfoSource.World => "world",
            GameRuntimeCharacterInfoSource.Battle => "battle",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };
}
