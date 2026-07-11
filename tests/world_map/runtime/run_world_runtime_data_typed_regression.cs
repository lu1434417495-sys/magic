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
        TestTypedSettlementUpdateProjectsToPayload();
        TestResourceNodesRoundTripThroughTypedWorldData();
        TestContextAndRuntimeTransactionUseTypedWorldData();
        TestSaveWorldStateAcceptsTypedWorldData();
        RequestTestExit(_test.Finish("World runtime data typed regression"));
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
    }

    private void TestTypedSettlementUpdateProjectsToPayload()
    {
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(BuildWorldData());
        _test.True(runtimeData != null, "valid world_data 应能构建 typed WorldRuntimeData。");

        bool updated = runtimeData.TrySetSettlementState(
            "spring",
            WorldMapSettlementStateData.Create(true, 7, new[] { "safe" })
        );
        _test.True(updated, "typed settlement state update 应成功。");

        GDictionary projected = runtimeData.ToDictionary();
        GDictionary settlement = projected["settlements"].AsGodotArray()[0].AsGodotDictionary();
        GDictionary state = settlement["settlement_state"].AsGodotDictionary();
        _test.True(state["visited"].AsBool(), "typed visited 更新应体现在 projection。");
        _test.Eq(state["reputation"].AsInt32(), 7, "typed reputation 更新应体现在 projection。");
        _test.Eq(
            state["active_conditions"].AsGodotArray()[0].AsString(),
            "safe",
            "typed active_conditions 更新应体现在 projection。"
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

        GDictionary projected = runtimeData.ToDictionary();
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
                BuildSettlementRecord("spring", "Spring", new GDictionary()),
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
            ["fog_states"] = new GDictionary(),
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
