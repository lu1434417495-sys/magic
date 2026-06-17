using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class SettlementServiceMetadataProjection
{
    internal static void ApplyToServiceData(
        GDictionary serviceData,
        SettlementServiceMetadata metadata
    )
    {
        if (serviceData == null || metadata == null)
            return;

        serviceData["cost_label"] = metadata.CostLabel.Trim();
        serviceData["is_enabled"] = metadata.IsEnabled;
        serviceData["disabled_reason"] = metadata.DisabledReason.Trim();

        GDictionary memberAvailability = ProjectMemberAvailability(
            metadata.CloneResearchMemberAvailability()
        );
        if (memberAvailability.Count > 0)
            serviceData["member_availability"] = memberAvailability;
    }

    private static GDictionary ProjectMemberAvailability(
        IEnumerable<SettlementResearchMemberAvailability> values
    )
    {
        var result = new GDictionary();
        if (values == null)
            return result;

        foreach (SettlementResearchMemberAvailability value in values)
        {
            if (value == null || value.MemberId == "")
                continue;
            result[value.MemberId.ToString()] = ProjectMemberAvailabilityEntry(value);
        }
        return result;
    }

    private static GDictionary ProjectMemberAvailabilityEntry(
        SettlementResearchMemberAvailability value
    )
    {
        return new GDictionary
        {
            ["member_id"] = value.MemberId.ToString(),
            ["has_available_research"] = value.HasAvailableResearch,
            ["is_enabled"] = value.IsEnabled,
            ["disabled_reason"] = value.DisabledReason,
        };
    }
}
