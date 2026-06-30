using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal enum BattleSkillAvailabilityConsumer
{
    ManualSelection,
    PreviewExecution,
    Hud,
    TextSnapshot,
    AiPlanning,
    AiScoring,
    ScopedAutoCast,
}

internal sealed class BattleSkillAvailabilityQuery
{
    internal BattleUnitState User { get; init; }
    internal BattleSkillAvailabilityConsumer Consumer { get; init; }
    internal bool IncludeKnownSkills { get; init; } = true;
    internal bool IncludeEquipmentSkills { get; init; } = false;
    internal bool IncludeScopedAutoCast { get; init; } = false;
}

internal sealed class BattleAvailableSkillEntry
{
    internal BattleSkillEntryRef EntryRef { get; init; }
    internal SkillDefinition SkillDefinition { get; init; }
    internal int SkillLevel { get; init; }
    internal bool IsSelectable { get; init; } = true;
    internal StringName DisabledReason { get; init; } = "";
    internal IReadOnlyList<StringName> SuppressedSourceKeys { get; init; } =
        System.Array.Empty<StringName>();
}

internal sealed class BattleSkillAvailabilityView
{
    private static readonly IReadOnlyList<BattleAvailableSkillEntry> EmptyEntries =
        System.Array.Empty<BattleAvailableSkillEntry>();
    private readonly Dictionary<StringName, BattleAvailableSkillEntry> _entriesById;

    internal BattleSkillAvailabilityView(IReadOnlyList<BattleAvailableSkillEntry> skillEntries)
    {
        SkillEntries = skillEntries ?? EmptyEntries;
        _entriesById = new Dictionary<StringName, BattleAvailableSkillEntry>(SkillEntries.Count);
        foreach (BattleAvailableSkillEntry entry in SkillEntries)
        {
            if (entry == null || IsEmpty(entry.EntryRef.SkillEntryId))
            {
                continue;
            }
            _entriesById[entry.EntryRef.SkillEntryId] = entry;
        }
    }

    internal IReadOnlyList<BattleAvailableSkillEntry> SkillEntries { get; }

    internal bool TryResolveSkillEntry(
        StringName skillEntryId,
        out BattleAvailableSkillEntry entry
    )
    {
        entry = null;
        StringName normalized = NormalizeStringName(skillEntryId);
        return !IsEmpty(normalized) && _entriesById.TryGetValue(normalized, out entry);
    }

    private static StringName NormalizeStringName(StringName value) =>
        ProgressionDataUtils.to_string_name(value);

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}

internal sealed class BattleSkillAccessResult
{
    internal bool Allowed { get; init; }
    internal BattleAvailableSkillEntry Entry { get; init; }
    internal StringName ErrorCode { get; init; } = "";
    internal string Message { get; init; } = "";

    internal static BattleSkillAccessResult Allow(BattleAvailableSkillEntry entry) =>
        new() { Allowed = true, Entry = entry };

    internal static BattleSkillAccessResult Deny(StringName errorCode, string message) =>
        new()
        {
            Allowed = false,
            ErrorCode = errorCode,
            Message = message ?? "",
        };
}

internal sealed class BattleSkillAvailabilityService
{
    private static readonly IReadOnlyDictionary<StringName, SkillDefinition> EmptySkillDefinitions =
        new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>()
        );

    private readonly ISkillCatalog _skillCatalog;
    private readonly IReadOnlyDictionary<StringName, SkillDefinition> _skillDefinitions;

    internal BattleSkillAvailabilityService(
        ISkillCatalog skillCatalog,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        _skillCatalog = skillCatalog;
        _skillDefinitions = skillDefinitions ?? EmptySkillDefinitions;
    }

    internal BattleSkillAvailabilityService(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
        : this(null, skillDefinitions) { }

    internal BattleSkillAvailabilityView BuildView(BattleSkillAvailabilityQuery query)
    {
        var entries = new List<BattleAvailableSkillEntry>();
        BattleUnitState user = query?.User;
        if (user == null)
        {
            return new BattleSkillAvailabilityView(entries);
        }

        if (query == null || query.IncludeKnownSkills)
        {
            AddKnownSkillEntries(user, entries);
        }

        return new BattleSkillAvailabilityView(entries);
    }

    internal bool TryGetSkillEntryBySlot(
        BattleSkillAvailabilityQuery query,
        int slotIndex,
        out BattleAvailableSkillEntry entry
    )
    {
        entry = null;
        if (slotIndex < 0)
        {
            return false;
        }
        BattleSkillAvailabilityView view = BuildView(query);
        if (slotIndex >= view.SkillEntries.Count)
        {
            return false;
        }
        entry = view.SkillEntries[slotIndex];
        return entry != null;
    }

    internal bool TryResolveSkillEntry(
        BattleSkillAvailabilityQuery query,
        StringName skillEntryId,
        out BattleAvailableSkillEntry entry
    )
    {
        BattleSkillAvailabilityView view = BuildView(query);
        return view.TryResolveSkillEntry(skillEntryId, out entry);
    }

    internal int ResolveSkillEntryLevel(
        BattleSkillAvailabilityQuery query,
        StringName skillEntryId,
        int fallback = 0
    )
    {
        return TryResolveSkillEntry(query, skillEntryId, out BattleAvailableSkillEntry entry)
            ? entry.SkillLevel
            : fallback;
    }

    internal BattleSkillAccessResult ValidateSkillEntryAccess(
        BattleSkillAvailabilityQuery query,
        StringName skillEntryId,
        StringName expectedSkillId
    )
    {
        StringName normalizedEntryId = NormalizeStringName(skillEntryId);
        StringName normalizedSkillId = NormalizeStringName(expectedSkillId);
        if (IsEmpty(normalizedEntryId))
        {
            return BattleSkillAccessResult.Deny(
                "missing_skill_entry_id",
                "技能入口无效。"
            );
        }
        if (IsEmpty(normalizedSkillId))
        {
            return BattleSkillAccessResult.Deny("missing_skill_id", "技能无效。");
        }
        if (!TryResolveSkillEntry(query, normalizedEntryId, out BattleAvailableSkillEntry entry))
        {
            return BattleSkillAccessResult.Deny(
                "stale_skill_entry_id",
                "技能入口已失效。"
            );
        }
        if (entry.EntryRef.SkillId != normalizedSkillId)
        {
            return BattleSkillAccessResult.Deny(
                "skill_entry_mismatch",
                "技能入口与技能不匹配。"
            );
        }
        if (!entry.IsSelectable)
        {
            return BattleSkillAccessResult.Deny(
                IsEmpty(entry.DisabledReason) ? "skill_entry_disabled" : entry.DisabledReason,
                "技能入口当前不可用。"
            );
        }
        return BattleSkillAccessResult.Allow(entry);
    }

    private void AddKnownSkillEntries(
        BattleUnitState user,
        List<BattleAvailableSkillEntry> entries
    )
    {
        if (user.known_active_skill_ids == null)
        {
            return;
        }

        var seenSkillIds = new HashSet<StringName>();
        foreach (StringName rawSkillId in user.known_active_skill_ids)
        {
            StringName skillId = NormalizeStringName(rawSkillId);
            if (IsEmpty(skillId) || !seenSkillIds.Add(skillId))
            {
                continue;
            }
            TryGetSkillDefinition(skillId, out SkillDefinition skillDefinition);
            entries.Add(
                new BattleAvailableSkillEntry
                {
                    EntryRef = new BattleSkillEntryRef(
                        BattleSkillEntryIds.KnownSkill(skillId),
                        skillId,
                        BattleSkillEntrySourceKind.KnownSkill,
                        ""
                    ),
                    SkillDefinition = skillDefinition,
                    SkillLevel = ResolveKnownSkillLevel(user, skillId),
                    IsSelectable = true,
                    DisabledReason = "",
                    SuppressedSourceKeys = System.Array.Empty<StringName>(),
                }
            );
        }
    }

    private bool TryGetSkillDefinition(StringName skillId, out SkillDefinition skillDefinition)
    {
        skillDefinition = null;
        if (IsEmpty(skillId))
        {
            return false;
        }
        if (_skillCatalog != null && _skillCatalog.TryGetSkillDefinition(skillId, out skillDefinition))
        {
            return true;
        }
        return _skillDefinitions != null
            && _skillDefinitions.TryGetValue(skillId, out skillDefinition);
    }

    private static int ResolveKnownSkillLevel(BattleUnitState user, StringName skillId)
    {
        if (user == null || IsEmpty(skillId))
        {
            return 0;
        }
        int explicitLevel = user.GetKnownSkillLevelTyped(skillId, int.MinValue);
        if (explicitLevel != int.MinValue)
        {
            return explicitLevel;
        }
        return user.KnowsActiveSkill(skillId) ? 1 : 0;
    }

    private static StringName NormalizeStringName(StringName value) =>
        ProgressionDataUtils.to_string_name(value);

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
