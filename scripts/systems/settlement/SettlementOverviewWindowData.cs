using System;
using System.Collections.Generic;
using Godot;

// Detached window input for SettlementWindow. It never holds PartyState, a runtime owner or
// a Godot collection: the builder resolves every displayed fact up front, and the UI only reads.
internal sealed record SettlementMemberOptionData(
    StringName MemberId,
    string DisplayName,
    string RosterRole,
    bool IsLeader,
    int CurrentHp,
    int CurrentMp
);

internal sealed record SettlementFacilityEntryData(
    StringName FacilityId,
    string DisplayName,
    string SlotTag,
    string InteractionType
);

internal sealed record SettlementResidentEntryData(
    StringName NpcId,
    string DisplayName,
    string ServiceType,
    string FacilityName
);

internal readonly record struct SettlementMemberAvailabilityData(
    bool IsEnabled,
    string DisabledReason
);

internal sealed class SettlementServiceEntryData
{
    internal SettlementServiceEntryData(
        StringName actionId,
        StringName facilityId,
        string facilityName,
        StringName npcId,
        string npcName,
        string serviceType,
        StringName interactionScriptId,
        string costLabel,
        string stateLabel,
        string summaryText,
        bool isEnabled,
        string disabledReason,
        SettlementPanelKind panelKind,
        IEnumerable<KeyValuePair<StringName, SettlementMemberAvailabilityData>> memberAvailability
    )
    {
        ActionId = actionId ?? "";
        FacilityId = facilityId ?? "";
        FacilityName = facilityName ?? "";
        NpcId = npcId ?? "";
        NpcName = npcName ?? "";
        ServiceType = serviceType ?? "";
        InteractionScriptId = interactionScriptId ?? "";
        CostLabel = costLabel ?? "";
        StateLabel = stateLabel ?? "";
        SummaryText = summaryText ?? "";
        IsEnabled = isEnabled;
        DisabledReason = disabledReason ?? "";
        PanelKind = panelKind;

        var copy = new Dictionary<StringName, SettlementMemberAvailabilityData>();
        if (memberAvailability != null)
        {
            foreach (
                KeyValuePair<StringName, SettlementMemberAvailabilityData> pair in memberAvailability
            )
            {
                if (pair.Key != null && pair.Key != (StringName)"")
                    copy[pair.Key] = pair.Value;
            }
        }
        MemberAvailability = copy;
    }

    internal StringName ActionId { get; }

    internal StringName FacilityId { get; }

    internal string FacilityName { get; }

    internal StringName NpcId { get; }

    internal string NpcName { get; }

    internal string ServiceType { get; }

    internal StringName InteractionScriptId { get; }

    internal string CostLabel { get; }

    internal string StateLabel { get; }

    internal string SummaryText { get; }

    internal bool IsEnabled { get; }

    internal string DisabledReason { get; }

    internal SettlementPanelKind PanelKind { get; }

    internal IReadOnlyDictionary<StringName, SettlementMemberAvailabilityData> MemberAvailability
    {
        get;
    }
}

internal sealed class SettlementOverviewWindowData
{
    internal static SettlementOverviewWindowData Empty { get; } =
        new(
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            Vector2I.One,
            "",
            null,
            null,
            null,
            null
        );

    internal SettlementOverviewWindowData(
        StringName settlementId,
        string displayName,
        string tierName,
        StringName factionId,
        StringName countryId,
        string feedbackText,
        string stateSummaryText,
        Vector2I footprintSize,
        StringName defaultMemberId,
        IEnumerable<SettlementMemberOptionData> memberOptions,
        IEnumerable<SettlementFacilityEntryData> facilities,
        IEnumerable<SettlementResidentEntryData> residents,
        IEnumerable<SettlementServiceEntryData> services
    )
    {
        SettlementId = settlementId ?? "";
        DisplayName = displayName ?? "";
        TierName = tierName ?? "";
        FactionId = factionId ?? "";
        CountryId = countryId ?? "";
        FeedbackText = feedbackText ?? "";
        StateSummaryText = stateSummaryText ?? "";
        FootprintSize = footprintSize;
        DefaultMemberId = defaultMemberId ?? "";
        MemberOptions = CopyList(memberOptions);
        Facilities = CopyList(facilities);
        Residents = CopyList(residents);
        Services = CopyList(services);

        var memberOptionMap = new Dictionary<StringName, SettlementMemberOptionData>();
        foreach (SettlementMemberOptionData option in MemberOptions)
        {
            if (option.MemberId != (StringName)"" && !string.IsNullOrEmpty(option.DisplayName))
                memberOptionMap[option.MemberId] = option;
        }
        MemberOptionMap = memberOptionMap;
    }

    internal StringName SettlementId { get; }

    internal string DisplayName { get; }

    internal string TierName { get; }

    internal StringName FactionId { get; }

    internal StringName CountryId { get; }

    internal string FeedbackText { get; }

    internal string StateSummaryText { get; }

    internal Vector2I FootprintSize { get; }

    // Already resolved against leader / active / reserve order by the builder: the window
    // never re-derives a default from PartyState.
    internal StringName DefaultMemberId { get; }

    internal IReadOnlyList<SettlementMemberOptionData> MemberOptions { get; }

    internal IReadOnlyDictionary<StringName, SettlementMemberOptionData> MemberOptionMap { get; }

    internal IReadOnlyList<SettlementFacilityEntryData> Facilities { get; }

    internal IReadOnlyList<SettlementResidentEntryData> Residents { get; }

    internal IReadOnlyList<SettlementServiceEntryData> Services { get; }

    internal bool IsValid =>
        SettlementId != (StringName)"" && !string.IsNullOrEmpty(DisplayName);

    private static IReadOnlyList<T> CopyList<T>(IEnumerable<T> values)
        where T : class
    {
        var copy = new List<T>();
        if (values != null)
        {
            foreach (T value in values)
            {
                if (value != null)
                    copy.Add(value);
            }
        }
        return copy.AsReadOnly();
    }
}
