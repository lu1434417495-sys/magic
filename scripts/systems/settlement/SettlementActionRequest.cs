using Godot;

internal readonly record struct SettlementActionRequest(
    StringName SettlementId,
    StringName ServiceId,
    StringName ActionId,
    StringName MemberId,
    int Quantity,
    SettlementSubmissionSource Source
)
{
    public bool IsValid => SettlementId != "" && ServiceId != "" && ActionId != "";
}
