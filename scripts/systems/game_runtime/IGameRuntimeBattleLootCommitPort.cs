using System.Collections.Generic;
using Godot;

internal interface IGameRuntimeBattleLootCommitPort
{
    bool TryPrepareBattleLootCommit();

    IBattleLootCommitCheckpoint CaptureBattleLootCommitCheckpoint();

    void RestoreBattleLootCommitCheckpoint(IBattleLootCommitCheckpoint checkpoint);

    BattleLootItemDefinitionKind ResolveBattleLootItemDefinitionKind(StringName itemId);

    BattleLootWarehouseAddResult AddBattleLootItem(StringName itemId, int quantity);

    BattleLootWarehouseAddResult AddBattleLootEquipmentInstance(
        EquipmentInstanceState equipmentInstance
    );

    bool TryRollBattleLootEquipment(
        StringName itemId,
        int quantity,
        int dropLuck,
        out IReadOnlyList<EquipmentInstanceState> rolledInstances
    );

    bool GetBattleLootFateRunFlag(StringName flagId);

    void SetBattleLootFateRunFlag(StringName flagId);

    void ClearBattleLootFateRunFlag(StringName flagId);

    string FormatBattleLootFactionLabel(string factionId);

    string GetBattleLootItemDisplayName(StringName itemId);
}

internal interface IBattleLootCommitCheckpoint { }

internal enum BattleLootItemDefinitionKind
{
    Missing = 0,
    NonEquipment = 1,
    Equipment = 2,
}

internal sealed class BattleLootWarehouseAddResult
{
    internal bool ItemFound { get; }
    internal bool IsEquipment { get; }
    internal int AddedQuantity { get; }
    internal int RemainingQuantity { get; }

    internal BattleLootWarehouseAddResult(
        bool itemFound,
        bool isEquipment,
        int addedQuantity,
        int remainingQuantity
    )
    {
        ItemFound = itemFound;
        IsEquipment = isEquipment;
        AddedQuantity = Mathf.Max(addedQuantity, 0);
        RemainingQuantity = Mathf.Max(remainingQuantity, 0);
    }

    internal static BattleLootWarehouseAddResult Unavailable(int requestedQuantity) =>
        new(false, false, 0, requestedQuantity);
}
