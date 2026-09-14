using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class GameSession
{
    private static readonly StringName PhoenixRebirthSetId = "phoenix_rebirth_set";

    private int InitializeNewWorldUniqueEquipmentPool()
    {
        if (_worldData.ContainsKey(WorldRuntimeSaveSchema.UniqueEquipmentPool))
            return (int)Error.AlreadyExists;

        IReadOnlyDictionary<StringName, GearSetDefinition> gearSets =
            GetContentCatalogTyped().GetGearSetDefinitionsTyped();
        if (
            !gearSets.TryGetValue(PhoenixRebirthSetId, out GearSetDefinition phoenixSet)
            || phoenixSet == null
            || phoenixSet.MemberItemIds.Count != 10
        )
        {
            return (int)Error.InvalidData;
        }

        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = GetItemDefsTyped();
        var instances = new List<EquipmentInstanceState>(phoenixSet.MemberItemIds.Count);
        using var traitRollService = new EquipmentTraitRollService(GetTraitDefsTyped().Values);
        foreach (StringName memberItemId in phoenixSet.MemberItemIds)
        {
            if (
                memberItemId == ""
                || !itemDefinitions.TryGetValue(memberItemId, out ItemDefinition itemDefinition)
                || itemDefinition == null
                || !itemDefinition.IsEquipment()
            )
            {
                return (int)Error.InvalidData;
            }

            StringName instanceId = AllocateNewWorldEquipmentInstanceIdUnchecked();
            if (instanceId == "")
                return (int)Error.InvalidData;
            EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
                memberItemId,
                instanceId
            );
            instance.rarity = (int)EquipmentInstanceState.RarityTier.LEGENDARY;
            instance.current_durability = EquipmentDurabilityRules.GetDefaultCurrentDurability(
                instance.rarity
            );
            traitRollService.MintWithRolls(instance, itemDefinition);
            instances.Add(instance);
        }

        WorldUniqueEquipmentPoolState pool = WorldUniqueEquipmentPoolState.CreateNew(
            PhoenixRebirthSetId.ToString(),
            instances
        );
        if (pool == null || pool.RemainingInstanceCount != phoenixSet.MemberItemIds.Count)
            return (int)Error.InvalidData;

        WorldRuntimeData worldData;
        using (GodotProjectionLease<GDictionary> worldDataLease = WorldDataPayloadLease())
            worldData = WorldRuntimeData.FromDictionary(worldDataLease.Value);
        if (worldData == null || worldData.HasUniqueEquipmentPool)
            return (int)Error.InvalidData;
        worldData.SetUniqueEquipmentPool(pool);
        ReplaceWorldDataOwnedPlain(worldData.BuildSaveSnapshotPlain());
        MarkRuntimeStateDirty(SaveDirtyScopeWorldData);
        return (int)Error.Ok;
    }

    private StringName AllocateNewWorldEquipmentInstanceIdUnchecked()
    {
        if (!_worldData.TryGetValue(WorldEquipmentInstanceSerialKey, out object rawSerial))
            return "";
        int serial = rawSerial switch
        {
            int intValue => intValue,
            long longValue when longValue <= int.MaxValue => (int)longValue,
            _ => 0,
        };
        if (serial < 1 || serial == int.MaxValue)
            return "";
        _worldData[WorldEquipmentInstanceSerialKey] = serial + 1;
        MarkRuntimeStateDirty(SaveDirtyScopeWorldData);
        return EquipmentInstanceState.FormatInstanceId(serial);
    }
}
