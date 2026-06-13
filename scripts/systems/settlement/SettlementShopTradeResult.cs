public sealed class SettlementShopTradeResult
{
    public bool Success { get; }
    public string Message { get; }
    public int GoldDelta { get; }
    public string ItemId { get; }
    public string InstanceId { get; }
    public int Quantity { get; }

    public SettlementShopTradeResult(
        bool success,
        string message,
        int goldDelta = 0,
        string itemId = "",
        int quantity = 0,
        string instanceId = null
    )
    {
        Success = success;
        Message = message ?? "";
        GoldDelta = goldDelta;
        ItemId = itemId ?? "";
        Quantity = quantity;
        InstanceId = instanceId;
    }

}
