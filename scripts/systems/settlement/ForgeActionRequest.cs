using Godot;

internal readonly record struct ForgeActionRequest(
    StringName SettlementId,
    StringName ServiceId,
    StringName ActionId,
    StringName MemberId,
    StringName RecipeId
)
{
    public bool IsValid =>
        !SettlementActionRequest.IsEmpty(SettlementId)
        && !SettlementActionRequest.IsEmpty(ServiceId)
        && !SettlementActionRequest.IsEmpty(ActionId)
        && !SettlementActionRequest.IsEmpty(RecipeId);

    public SettlementActionRequest ToSettlementActionRequest() =>
        new(
            SettlementId,
            ServiceId,
            ActionId,
            MemberId,
            0,
            SettlementSubmissionSource.Forge
        );
}
