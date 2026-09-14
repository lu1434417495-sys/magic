using System;
using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimeBattleLootCommitPort
{
    private const int UniqueEquipmentRandomDropChancePercent = 5;
    private Func<int, int, int> _uniqueEquipmentDropRollRangeForTesting;

    internal void SetUniqueEquipmentDropRollRangeForTesting(
        Func<int, int, int> rollRange
    ) => _uniqueEquipmentDropRollRangeForTesting = rollRange;

    private sealed class BattleLootCommitCheckpoint : IBattleLootCommitCheckpoint
    {
        internal WarehouseState WarehouseState { get; }
        internal IReadOnlyDictionary<StringName, bool> FateRunFlags { get; }
        internal bool HadUniqueEquipmentPool { get; }
        internal WorldUniqueEquipmentPoolState UniqueEquipmentPool { get; }

        internal BattleLootCommitCheckpoint(
            WarehouseState warehouseState,
            IReadOnlyDictionary<StringName, bool> fateRunFlags,
            WorldUniqueEquipmentPoolState uniqueEquipmentPool
        )
        {
            WarehouseState = warehouseState?.DuplicateState();
            FateRunFlags =
                fateRunFlags
                ?? new Dictionary<StringName, bool>();
            HadUniqueEquipmentPool = uniqueEquipmentPool != null;
            UniqueEquipmentPool = uniqueEquipmentPool?.DuplicateState();
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
            _party_state?.CaptureFateRunFlagsTyped(),
            _world_map_data_context?.RootRuntimeData?.UniqueEquipmentPool
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
        _world_map_data_context?.RootRuntimeData?.RestoreUniqueEquipmentPool(
            typedCheckpoint.HadUniqueEquipmentPool,
            typedCheckpoint.UniqueEquipmentPool
        );
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
        var resolved = new List<EquipmentInstanceState>();
        int ordinaryQuantity = 0;
        for (int index = 0; index < Mathf.Max(quantity, 0); index++)
        {
            WorldUniqueEquipmentDropTakeKind takeKind = TryTakeUniqueEquipmentForRandomDrop(
                itemId,
                out EquipmentInstanceState uniqueInstance
            );
            if (takeKind == WorldUniqueEquipmentDropTakeKind.Taken)
            {
                resolved.Add(uniqueInstance);
                continue;
            }
            if (takeKind == WorldUniqueEquipmentDropTakeKind.NotSelected)
                ordinaryQuantity++;
        }
        if (ordinaryQuantity > 0)
        {
            if (_equipment_drop_service == null)
            {
                rolledInstances = System.Array.Empty<EquipmentInstanceState>();
                return false;
            }
            resolved.AddRange(
                _equipment_drop_service.RollItemInstances(
                    itemId,
                    ordinaryQuantity,
                    dropLuck
                )
            );
        }
        rolledInstances = resolved.AsReadOnly();
        return true;
    }

    bool IGameRuntimeBattleLootCommitPort.TryReturnUniqueWorldEquipmentLoot(
        EquipmentInstanceState equipmentInstance
    )
    {
        if (!IsPhoenixRebirthMember(equipmentInstance?.item_id ?? new StringName("")))
            return false;
        WorldUniqueEquipmentPoolState pool =
            _world_map_data_context?.RootRuntimeData?.UniqueEquipmentPool;
        return pool?.TryReturnToReserve(equipmentInstance) ?? false;
    }

    bool IGameRuntimeBattleLootCommitPort.IsUniqueWorldEquipmentItem(StringName itemId) =>
        IsPhoenixRebirthMember(itemId);

    private WorldUniqueEquipmentDropTakeKind TryTakeUniqueEquipmentForRandomDrop(
        StringName requestedItemId,
        out EquipmentInstanceState instance
    )
    {
        instance = null;
        WorldUniqueEquipmentPoolState pool =
            _world_map_data_context?.RootRuntimeData?.UniqueEquipmentPool;
        if (IsPhoenixRebirthMember(requestedItemId))
        {
            return pool != null && pool.TryTakeReserveByItem(requestedItemId, out instance)
                ? WorldUniqueEquipmentDropTakeKind.Taken
                : WorldUniqueEquipmentDropTakeKind.UniqueUnavailable;
        }
        if (
            pool == null
            || RollUniqueEquipmentDropRange(1, 100)
                > UniqueEquipmentRandomDropChancePercent
        )
        {
            return WorldUniqueEquipmentDropTakeKind.NotSelected;
        }
        return pool.TryTakeRandomReserve(RollUniqueEquipmentDropRange, out instance)
            ? WorldUniqueEquipmentDropTakeKind.Taken
            : WorldUniqueEquipmentDropTakeKind.NotSelected;
    }

    private bool IsPhoenixRebirthMember(StringName itemId)
    {
        IReadOnlyDictionary<StringName, GearSetDefinition> gearSets =
            _content_catalog?.GetGearSetDefinitionsTyped();
        if (
            gearSets == null
            || !gearSets.TryGetValue(
                WorldUniqueEquipmentPoolState.PhoenixRebirthPoolId,
                out GearSetDefinition phoenixSet
            )
            || phoenixSet == null
        )
        {
            return false;
        }
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        foreach (StringName memberItemId in phoenixSet.MemberItemIds)
        {
            if (memberItemId == normalizedItemId)
                return true;
        }
        return false;
    }

    private int RollUniqueEquipmentDropRange(int minInclusive, int maxInclusive) =>
        _uniqueEquipmentDropRollRangeForTesting != null
            ? _uniqueEquipmentDropRollRangeForTesting(minInclusive, maxInclusive)
            : TrueRandomSeedService.RandiRange(minInclusive, maxInclusive);

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
