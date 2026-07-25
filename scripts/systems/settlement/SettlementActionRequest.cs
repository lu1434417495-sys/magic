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
    public bool IsValid =>
        !IsEmpty(SettlementId)
        && !IsEmpty(ServiceId)
        && !IsEmpty(ActionId);

    internal static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";

    internal static string ToText(StringName value) =>
        IsEmpty(value) ? "" : value.ToString();
}
