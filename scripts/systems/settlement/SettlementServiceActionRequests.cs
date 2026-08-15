using Godot;

// Typed submissions raised by ShopWindow. They only carry stable ids; the runtime re-reads
// stock, price, quest state, recipe and destination before applying anything.

internal readonly record struct SettlementShopActionRequest(
    SettlementActionRequest Action,
    SettlementShopActionKind ActionKind,
    StringName ItemId,
    StringName InstanceId,
    int Quantity
)
{
    public bool IsValid =>
        !SettlementActionRequest.IsEmpty(Action.SettlementId)
        && !SettlementActionRequest.IsEmpty(ItemId)
        && Quantity > 0;
}

internal readonly record struct SettlementContractBoardActionRequest(
    SettlementActionRequest Action,
    StringName QuestId,
    bool ConfirmAccept
)
{
    public bool IsValid =>
        !SettlementActionRequest.IsEmpty(Action.SettlementId)
        && !SettlementActionRequest.IsEmpty(Action.ActionId)
        && !SettlementActionRequest.IsEmpty(QuestId);
}

internal readonly record struct SettlementStagecoachActionRequest(
    SettlementActionRequest Action,
    StringName TargetSettlementId
)
{
    public bool IsValid => !SettlementActionRequest.IsEmpty(TargetSettlementId);
}
