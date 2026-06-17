using Godot;

internal sealed class SettlementResearchMemberAvailability
{
    public readonly StringName MemberId;
    public readonly bool HasAvailableResearch;
    public readonly bool IsEnabled;
    public readonly string DisabledReason;

    public SettlementResearchMemberAvailability(
        StringName memberId,
        bool hasAvailableResearch,
        bool isEnabled,
        string disabledReason
    )
    {
        MemberId = ProgressionDataUtils.to_string_name(memberId);
        HasAvailableResearch = hasAvailableResearch;
        IsEnabled = isEnabled;
        DisabledReason = disabledReason ?? "";
    }

    public SettlementResearchMemberAvailability DuplicateState() =>
        new(MemberId, HasAvailableResearch, IsEnabled, DisabledReason);
}
