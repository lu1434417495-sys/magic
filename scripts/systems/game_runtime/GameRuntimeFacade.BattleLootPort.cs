using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeBattleLootCommitPort
{
    private sealed class BattleLootCommitCheckpoint : IBattleLootCommitCheckpoint
    {
        internal WarehouseState WarehouseState { get; }
        internal IReadOnlyDictionary<StringName, bool> FateRunFlags { get; }

        internal BattleLootCommitCheckpoint(
            WarehouseState warehouseState,
            IReadOnlyDictionary<StringName, bool> fateRunFlags
        )
        {
            WarehouseState = warehouseState?.DuplicateState();
            FateRunFlags =
                fateRunFlags
                ?? new Dictionary<StringName, bool>();
        }
    }

    bool IGameRuntimeBattleLootCommitPort.TryPrepareBattleLootCommit()
    {
        if (
            _party_state == null
            || _party_warehouse_service == null
            || _game_session == null
        )
            return false;

        SetupPartyWarehouseService(
            _party_warehouse_service,
            _party_state,
            _game_session.GetItemDefsTyped()
        );
        return true;
    }

    IBattleLootCommitCheckpoint
        IGameRuntimeBattleLootCommitPort.CaptureBattleLootCommitCheckpoint()
    {
        return new BattleLootCommitCheckpoint(
            _party_state?.warehouse_state,
            _party_state?.CaptureFateRunFlagsTyped()
        );
    }

    void IGameRuntimeBattleLootCommitPort.RestoreBattleLootCommitCheckpoint(
        IBattleLootCommitCheckpoint checkpoint
    )
    {
        if (
            checkpoint is not BattleLootCommitCheckpoint typedCheckpoint
            || _party_state == null
        )
            return;

        _party_state.warehouse_state = typedCheckpoint.WarehouseState?.DuplicateState();
        _party_state.ApplyFateRunFlagsTyped(typedCheckpoint.FateRunFlags);
        if (_party_warehouse_service != null && _game_session != null)
        {
            SetupPartyWarehouseService(
                _party_warehouse_service,
                _party_state,
                _game_session.GetItemDefsTyped()
            );
        }
    }

    BattleLootItemDefinitionKind
        IGameRuntimeBattleLootCommitPort.ResolveBattleLootItemDefinitionKind(
            StringName itemId
        )
    {
        if (
            _game_session == null
            || !_game_session.GetItemDefsTyped().TryGetValue(itemId, out ItemDefinition itemDef)
            || itemDef == null
        )
            return BattleLootItemDefinitionKind.Missing;
        return itemDef.IsEquipment()
            ? BattleLootItemDefinitionKind.Equipment
            : BattleLootItemDefinitionKind.NonEquipment;
    }

    BattleLootWarehouseAddResult IGameRuntimeBattleLootCommitPort.AddBattleLootItem(
        StringName itemId,
        int quantity
    )
    {
        if (_party_warehouse_service == null)
            return BattleLootWarehouseAddResult.Unavailable(quantity);
        var result = _party_warehouse_service.AddItemTyped(itemId, quantity);
        return new BattleLootWarehouseAddResult(
            result.ItemFound,
            result.IsEquipment,
            result.AddedQuantity,
            result.RemainingQuantity
        );
    }

    BattleLootWarehouseAddResult
        IGameRuntimeBattleLootCommitPort.AddBattleLootEquipmentInstance(
            EquipmentInstanceState equipmentInstance
        )
    {
        if (_party_warehouse_service == null)
            return BattleLootWarehouseAddResult.Unavailable(1);
        var result = _party_warehouse_service.AddEquipmentInstanceTyped(equipmentInstance);
        return new BattleLootWarehouseAddResult(
            result.ItemFound,
            result.IsEquipment,
            result.AddedQuantity,
            result.RemainingQuantity
        );
    }

    bool IGameRuntimeBattleLootCommitPort.TryRollBattleLootEquipment(
        StringName itemId,
        int quantity,
        int dropLuck,
        out IReadOnlyList<EquipmentInstanceState> rolledInstances
    )
    {
        rolledInstances = System.Array.Empty<EquipmentInstanceState>();
        if (_equipment_drop_service == null)
            return false;
        rolledInstances = _equipment_drop_service.RollItemInstances(
            itemId,
            quantity,
            dropLuck
        );
        return true;
    }

    bool IGameRuntimeBattleLootCommitPort.GetBattleLootFateRunFlag(StringName flagId) =>
        _party_state?.GetFateRunFlag(flagId, false) ?? false;

    void IGameRuntimeBattleLootCommitPort.SetBattleLootFateRunFlag(StringName flagId)
    {
        _party_state?.SetFateRunFlag(flagId, true);
    }

    void IGameRuntimeBattleLootCommitPort.ClearBattleLootFateRunFlag(StringName flagId)
    {
        _party_state?.ClearFateRunFlag(flagId);
    }

    string IGameRuntimeBattleLootCommitPort.FormatBattleLootFactionLabel(
        string factionId
    ) => FormatFactionLabel(factionId);

    string IGameRuntimeBattleLootCommitPort.GetBattleLootItemDisplayName(
        StringName itemId
    ) => GetItemDisplayName(itemId);
}
