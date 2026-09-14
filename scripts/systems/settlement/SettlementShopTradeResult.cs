using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementShopTradeResult
{
    public bool Success { get; }
    public string Message { get; }
    public int GoldDelta { get; }
    public string ItemId { get; }
    public string InstanceId { get; }
    public int Quantity { get; }
    public WorldMapSettlementStateData UpdatedSettlementState { get; }

    public SettlementShopTradeResult(
        bool success,
        string message,
        int goldDelta = 0,
        string itemId = "",
        int quantity = 0,
        string instanceId = null,
        WorldMapSettlementStateData updatedSettlementState = null
    )
    {
        Success = success;
        Message = message ?? "";
        GoldDelta = goldDelta;
        ItemId = itemId ?? "";
        Quantity = quantity;
        InstanceId = instanceId;
        UpdatedSettlementState = updatedSettlementState;
    }

}

public sealed class SettlementShopWindowBuildResult
{
    internal SettlementServiceWindowData WindowData { get; }
    public WorldMapSettlementStateData UpdatedSettlementState { get; }
    public bool StateChanged { get; }

    internal SettlementShopWindowBuildResult(
        SettlementServiceWindowData windowData,
        WorldMapSettlementStateData updatedSettlementState,
        bool stateChanged
    )
    {
        WindowData = windowData ?? SettlementServiceWindowData.Empty;
        UpdatedSettlementState = updatedSettlementState;
        StateChanged = stateChanged;
    }

    internal bool HasWindowData => WindowData.IsValid;
}
