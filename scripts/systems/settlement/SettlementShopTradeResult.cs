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
    private readonly Dictionary<string, object> _windowData;

    public IReadOnlyDictionary<string, object> WindowDataPlain =>
        RuntimePlainPayload.CloneDictionary(_windowData);
    public WorldMapSettlementStateData UpdatedSettlementState { get; }
    public bool StateChanged { get; }

    public SettlementShopWindowBuildResult(
        GDictionary windowData,
        WorldMapSettlementStateData updatedSettlementState,
        bool stateChanged
    )
    {
        _windowData = RuntimePlainPayload.NormalizeDictionary(
            windowData ?? new GDictionary(),
            "SettlementShopWindowBuildResult.window_data"
        );
        UpdatedSettlementState = updatedSettlementState;
        StateChanged = stateChanged;
    }

    internal GodotProjectionLease<GDictionary> ProjectWindowDataLease(string reason) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            _windowData,
            "settlement-shop-window",
            LifetimeDomain.Request,
            string.IsNullOrEmpty(reason)
                ? "SettlementShopWindowBuildResult.ProjectWindowDataLease"
                : reason
        );
}
