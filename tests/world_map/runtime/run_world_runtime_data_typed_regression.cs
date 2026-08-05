using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_runtime_data_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMalformedSettlementStateIsRejected();
        TestMalformedEncounterAnchorIsRejected();
        TestTypedSettlementFactoriesPreserveDecodeContract();
        TestTypedSettlementUpdateProjectsToPayload();
        TestResourceNodesRoundTripThroughTypedWorldData();
        TestContextAndRuntimeTransactionUseTypedWorldData();
        TestSaveWorldStateAcceptsTypedWorldData();
        RequestTestExit(_test.Finish("World runtime data typed regression"));
    }

    private void TestMalformedEncounterAnchorIsRejected()
    {
        GDictionary worldData = BuildWorldData();
        worldData["encounter_anchors"] = new GArray
        {
            new GDictionary
            {
                ["entity_id"] = "wild_anchor",
                ["display_name"] = "Wild Anchor",
                ["world_coord"] = new Vector2I(4, 5),
                ["faction_id"] = "hostile",
                ["region_tag"] = "north_wilds",
                ["vision_range"] = 2,
                ["is_cleared"] = false,
                ["encounter_kind"] = "single",
                ["encounter_profile_id"] = "",
                ["growth_stage"] = 0,
                ["suppressed_until_step"] = 0,
            },
        };

        _test.True(
            WorldRuntimeData.FromDictionary(worldData) == null,
            "WorldRuntimeData.FromDictionary 应拒绝包含空 encounter_profile_id 的遭遇锚点，而不是静默丢弃。"
        );
    }

    private void TestTypedSettlementFactoriesPreserveDecodeContract()
    {
        SettlementShopStockEntryData stock = SettlementShopStockEntryData.Create(
            "healing_herb",
            2,
            12
        );
        SettlementShopStateData shopState = SettlementShopStateData.Create(
            "village_basic_supply",
            new[] { stock },
            99L,
            5
        );
        WorldMapSettlementStateData settlementState = WorldMapSettlementStateData.Create(
            true,
            7,
            new[] { "safe" },
            new Dictionary<string, int>(StringComparer.Ordinal) { ["rest_basic"] = 3 },
            new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal)
            {
                ["village_basic_supply"] = shopState,
            }
        );

        _test.True(
            settlementState != null
                && WorldMapSettlementStateData.TryFromPlain(
                    settlementState.BuildSnapshotPlain(),
                    out WorldMapSettlementStateData decoded,
                    out _
                )
                && decoded.GetShopState("village_basic_supply") != null,
            "typed settlement factory 产物必须能按当前 v15 schema 解码。"
        );
        _test.True(
            SettlementShopStateData.Create(
                "village_basic_supply",
                new[] { stock, stock },
                99L,
                5
            ) == null,
            "typed shop factory 不得产生包含重复 item_id 的库存。"
        );
        _test.True(
            SettlementShopStateData.Create(
                "village_basic_supply",
                new SettlementShopStockEntryData[] { stock, null },
                99L,
                5
            ) == null,
            "typed shop factory 不得静默丢弃 null 库存项。"
        );
        _test.True(
            WorldMapSettlementStateData.Create(
                true,
                0,
                Array.Empty<string>(),
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rest_basic"] = 1,
                    [" rest_basic "] = 2,
                },
                new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal)
            ) == null,
            "typed settlement factory 必须拒绝规范化后冲突的 cooldown key。"
        );

        Dictionary<string, object> malformedSnapshot = settlementState.BuildSnapshotPlain();
        malformedSnapshot["cooldowns"] = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["rest_basic"] = 1,
            [" rest_basic "] = 2,
        };
        _test.False(
            WorldMapSettlementStateData.TryFromPlain(malformedSnapshot, out _, out _),
            "v15 parser 必须与 typed factory 一致拒绝规范化后冲突的 cooldown key。"
        );
    }

    private void TestMalformedSettlementStateIsRejected()
    {
        GDictionary worldData = BuildWorldData();
        worldData["settlements"] = new GArray
        {
            BuildSettlementRecord("spring", "Spring", "not_a_dictionary"),
        };

        _test.True(
            WorldRuntimeData.FromDictionary(worldData) == null,
            "WorldRuntimeData.FromDictionary 应拒绝 malformed settlement_state。"
        );

        GDictionary missingFieldWorldData = BuildWorldData();
        GDictionary missingFieldRecord = missingFieldWorldData["settlements"]
            .AsGodotArray()[0]
            .AsGodotDictionary();
        GDictionary missingFieldState = missingFieldRecord["settlement_state"]
            .AsGodotDictionary();
        missingFieldState.Remove("shop_states");
        missingFieldRecord["settlement_state"] = missingFieldState;
        missingFieldWorldData["settlements"] = new GArray { missingFieldRecord };
        _test.True(
            WorldRuntimeData.FromDictionary(missingFieldWorldData) == null,
            "WorldRuntimeData.FromDictionary 应拒绝缺少当前必填字段的 settlement_state。"
        );

        GDictionary extraFieldWorldData = BuildWorldData();
        GDictionary extraFieldRecord = extraFieldWorldData["settlements"]
            .AsGodotArray()[0]
            .AsGodotDictionary();
        GDictionary extraFieldState = extraFieldRecord["settlement_state"].AsGodotDictionary();
        extraFieldState["world_step"] = 0;
        extraFieldRecord["settlement_state"] = extraFieldState;
        extraFieldWorldData["settlements"] = new GArray { extraFieldRecord };
        _test.True(
            WorldRuntimeData.FromDictionary(extraFieldWorldData) == null,
            "WorldRuntimeData.FromDictionary 应拒绝 settlement_state 中的 transient/额外字段。"
        );

        GDictionary obsoleteMirrorWorldData = BuildWorldData();
        GDictionary obsoleteMirrorRecord = obsoleteMirrorWorldData["settlements"]
            .AsGodotArray()[0]
            .AsGodotDictionary();
        GDictionary obsoleteMirrorState = obsoleteMirrorRecord["settlement_state"]
            .AsGodotDictionary();
        obsoleteMirrorState["shop_inventory_seed"] = 1L;
        obsoleteMirrorState["shop_last_refresh_step"] = 0;
        obsoleteMirrorRecord["settlement_state"] = obsoleteMirrorState;
        obsoleteMirrorWorldData["settlements"] = new GArray { obsoleteMirrorRecord };
        _test.True(
            WorldRuntimeData.FromDictionary(obsoleteMirrorWorldData) == null,
            "v15 settlement_state 应拒绝已删除的顶层 shop seed/刷新步镜像。"
        );
    }

    private void TestTypedSettlementUpdateProjectsToPayload()
    {
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(BuildWorldData());
        _test.True(runtimeData != null, "valid world_data 应能构建 typed WorldRuntimeData。");
        _test.Eq(
            runtimeData.Settlements[0].CountryId,
            "spring_republic",
            "typed settlement record 应保留 country_id。"
        );

        SettlementShopStockEntryData stock = SettlementShopStockEntryData.Create(
            "healing_herb",
            2,
            12
        );
        SettlementShopStateData shopState = SettlementShopStateData.Create(
            "village_basic_supply",
            new[] { stock },
            99L,
            5
        );
        bool updated = runtimeData.TrySetSettlementState(
            "spring",
            WorldMapSettlementStateData.Create(
                true,
                7,
                new[] { "safe" },
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["rest_basic"] = 3,
                },
                new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal)
                {
                    ["village_basic_supply"] = shopState,
                }
            )
        );
        _test.True(updated, "typed settlement state update 应成功。");

        using GodotProjectionLease<GDictionary> projectedLease =
            WorldMapDataProjection.ProjectLease(runtimeData);
        GDictionary projected = projectedLease.Value;
        GDictionary settlement = projected["settlements"].AsGodotArray()[0].AsGodotDictionary();
        GDictionary state = settlement["settlement_state"].AsGodotDictionary();
        _test.Eq(
            runtimeData.Settlements[0].CountryId,
            "spring_republic",
            "WithSettlementState 不应丢失 typed country_id。"
        );
        _test.Eq(
            settlement["country_id"].AsString(),
            "spring_republic",
            "settlement state 更新后的 projection 应保留 country_id。"
        );
        _test.True(state["visited"].AsBool(), "typed visited 更新应体现在 projection。");
        _test.Eq(state["reputation"].AsInt32(), 7, "typed reputation 更新应体现在 projection。");
        _test.Eq(
            state["active_conditions"].AsGodotArray()[0].AsString(),
            "safe",
            "typed active_conditions 更新应体现在 projection。"
        );
        _test.Eq(
            state["cooldowns"].AsGodotDictionary()["rest_basic"].AsInt32(),
            3,
            "typed cooldowns 更新应体现在 projection。"
        );
        _test.False(state.ContainsKey("shop_inventory_seed"), "projection 不应恢复失效的顶层 shop seed。");
        _test.False(state.ContainsKey("shop_last_refresh_step"), "projection 不应恢复失效的顶层刷新步。");
        GDictionary projectedShop = state["shop_states"].AsGodotDictionary()
            ["village_basic_supply"].AsGodotDictionary();
        _test.Eq(projectedShop["seed"].AsInt64(), 99L, "每个商店应保留自身实际随机 seed。");
        _test.Eq(projectedShop["last_refresh_step"].AsInt32(), 5, "每个商店应保留自身刷新步。");
        _test.Eq(
            projectedShop["current_inventory"].AsGodotArray()[0]
                .AsGodotDictionary()["quantity"].AsInt32(),
            2,
            "typed shop_states/current_inventory 更新应体现在 projection。"
        );
    }

    private void TestResourceNodesRoundTripThroughTypedWorldData()
    {
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(BuildWorldData());
        _test.True(runtimeData != null, "valid world_data 应能构建 typed WorldRuntimeData。");
        if (runtimeData == null)
            return;

        _test.Eq(runtimeData.ResourceNodes.Count, 1, "typed world_data 应保留 resource_nodes。");
        WorldMapResourceNodeData node = runtimeData.ResourceNodes[0];
        _test.Eq(node.NodeId, "resource_farm_1", "typed resource node 应保留 node_id。");
        _test.Eq(node.NodeKind, WorldMapResourceNodeData.KindFarm, "typed resource node 应保留类型。");
        _test.Eq(node.WorldCoord, new Vector2I(2, 2), "typed resource node 应保留坐标。");
        _test.Eq(
            node.YieldItemId,
            WorldMapResourceNodeData.YieldTravelRation,
            "typed resource node 应保留产出物品。"
        );
        _test.Eq(node.SourceSettlementId, "spring", "typed resource node 应保留所属据点。");
        _test.Eq(node.RemainingCharges, 3, "typed resource node 应保留剩余次数。");

        using GodotProjectionLease<GDictionary> projectedLease =
            WorldMapDataProjection.ProjectLease(runtimeData);
        GDictionary projected = projectedLease.Value;
        GDictionary projectedNode = projected["resource_nodes"].AsGodotArray()[0]
            .AsGodotDictionary();
        _test.Eq(
            projectedNode["node_kind"].AsString(),
            WorldMapResourceNodeData.KindFarm,
            "resource_nodes projection 应保留 node_kind。"
        );
        _test.Eq(
            projectedNode["yield_item_id"].AsString(),
            WorldMapResourceNodeData.YieldTravelRation,
            "resource_nodes projection 应保留 yield_item_id。"
        );
    }

    private void TestContextAndRuntimeTransactionUseTypedWorldData()
    {
        WorldMapDataContext context = new();
        context.BindRootWorldData(BuildWorldData(12));

        _test.True(context.RootRuntimeData != null, "WorldMapDataContext 应拥有 typed root world data。");
        _test.Eq(context.RootRuntimeData.WorldStep, 12, "typed root world data 应反映 world_step。");

        FieldInfo worldDataField = typeof(RuntimeTransactionRollbackState).GetField(
            "_worldData",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.True(worldDataField != null, "rollback state 应保留 world data snapshot 字段。");
        _test.Eq(
            worldDataField.FieldType,
            typeof(WorldRuntimeData),
            "RuntimeTransactionRollbackState 应直接保存 WorldRuntimeData typed snapshot。"
        );
    }

    private void TestSaveWorldStateAcceptsTypedWorldData()
    {
        SaveSerializer serializer = new();
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(BuildWorldData(3));
        using GodotProjectionLease<GDictionary> worldStateLease =
            serializer.BuildWorldStatePayloadLease(
                runtimeData.BuildSaveSnapshotPlain(),
                new Vector2I(2, 1),
                "player"
            );
        GDictionary worldState = worldStateLease.Value;

        WorldRuntimeData roundTrip = WorldRuntimeData.FromDictionary(
            worldState["world_data"].AsGodotDictionary()
        );
        _test.True(roundTrip != null, "typed world state save payload 应能按当前 schema 回读。");
        _test.Eq(roundTrip.WorldStep, 3, "typed world state save roundtrip 应保留 world_step。");
        _test.Eq(
            roundTrip.Settlements[0].CountryId,
            "spring_republic",
            "typed world state save roundtrip 应保留 settlement country_id。"
        );
    }

    private static GDictionary BuildWorldData(int worldStep = 0)
    {
        return new GDictionary
        {
            ["map_seed"] = 1,
            ["world_step"] = worldStep,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = new GArray
            {
                BuildSettlementRecord("spring", "Spring", BuildSettlementState()),
            },
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["resource_nodes"] = new GArray
            {
                BuildResourceNode(
                    "resource_farm_1",
                    WorldMapResourceNodeData.KindFarm,
                    "农田",
                    new Vector2I(2, 2),
                    WorldMapResourceNodeData.YieldTravelRation,
                    "spring",
                    3,
                    3
                ),
            },
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = "spring",
            ["player_start_settlement_name"] = "Spring",
            ["fog_states"] = new GDictionary
            {
                ["version"] = WorldMapFogSystem.PersistentStateVersion,
                ["factions"] = new GDictionary(),
            },
        };
    }

    private static GDictionary BuildResourceNode(
        string nodeId,
        string nodeKind,
        string displayName,
        Vector2I worldCoord,
        string yieldItemId,
        string sourceSettlementId,
        int maxCharges,
        int remainingCharges
    )
    {
        return new GDictionary
        {
            ["node_id"] = nodeId,
            ["node_kind"] = nodeKind,
            ["display_name"] = displayName,
            ["world_coord"] = worldCoord,
            ["yield_item_id"] = yieldItemId,
            ["source_settlement_id"] = sourceSettlementId,
            ["max_charges"] = maxCharges,
            ["remaining_charges"] = remainingCharges,
        };
    }

    private static GDictionary BuildSettlementState() =>
        new()
        {
            ["visited"] = true,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["shop_states"] = new GDictionary(),
        };

    private static GDictionary BuildSettlementRecord(
        string settlementId,
        string displayName,
        Variant settlementState
    )
    {
        return new GDictionary
        {
            ["entity_id"] = $"settlement_{settlementId}",
            ["template_id"] = $"template_{settlementId}",
            ["settlement_id"] = settlementId,
            ["display_name"] = displayName,
            ["tier"] = 1,
            ["tier_name"] = "村镇",
            ["faction_id"] = "neutral",
            ["country_id"] = "spring_republic",
            ["origin"] = Vector2I.Zero,
            ["footprint_size"] = Vector2I.One,
            ["facilities"] = new GArray(),
            ["service_npcs"] = new GArray(),
            ["available_services"] = new GArray(),
            ["is_player_start"] = true,
            ["settlement_state"] = settlementState,
        };
    }
}
