using System.Collections.Generic;
using Godot;

internal sealed class SettlementServiceMetadata
{
    private readonly List<SettlementResearchMemberAvailability> _researchMemberAvailability;

    public string CostLabel { get; }
    public bool IsEnabled { get; }
    public string DisabledReason { get; }

    internal SettlementServiceMetadata(
        string costLabel,
        bool isEnabled,
        string disabledReason = "",
        IEnumerable<SettlementResearchMemberAvailability> researchMemberAvailability = null
    )
    {
        CostLabel = costLabel ?? "";
        IsEnabled = isEnabled;
        DisabledReason = disabledReason ?? "";
        _researchMemberAvailability = DuplicateResearchMemberAvailability(
            researchMemberAvailability
        );
    }

    internal List<SettlementResearchMemberAvailability> CloneResearchMemberAvailability() =>
        DuplicateResearchMemberAvailability(_researchMemberAvailability);

    private static List<SettlementResearchMemberAvailability> DuplicateResearchMemberAvailability(
        IEnumerable<SettlementResearchMemberAvailability> values
    )
    {
        var result = new List<SettlementResearchMemberAvailability>();
        if (values == null)
            return result;

        foreach (SettlementResearchMemberAvailability value in values)
        {
            SettlementResearchMemberAvailability copy = value?.DuplicateState();
            if (copy != null && copy.MemberId != "")
                result.Add(copy);
        }
        return result;
    }
}
