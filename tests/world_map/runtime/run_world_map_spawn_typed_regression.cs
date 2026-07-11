using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_spawn_typed_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTypedWorldBuildProjectsSettlementsAndAnchors();
        TestTypedWorldBuildGeneratesResourceNodes();

        RequestTestExit(_test.Finish("World map spawn typed regression"));
    }

    private void TestTypedWorldBuildProjectsSettlementsAndAnchors()
    {
        WorldGenerationDefinition definition = TestWorldGenerationDefinitionFactory.Load(
            TestWorldConfig
        );
        _test.True(definition != null, "typed world build regression 需要可投影的测试世界配置。");
        if (definition == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        WorldMapSpawnSystem spawnSystem = new();
        WorldMapSpawnSystem.WorldBuildData typedWorld = spawnSystem.BuildWorldTyped(
            definition,
            gridSystem
        );
        GDictionary projectedWorld = WorldMapSpawnProjection.Project(typedWorld);

        _test.True(typedWorld.MapSeed > 0, "typed world build 应记录运行时 map seed。");
        _test.True(typedWorld.Settlements.Count > 0, "typed world build 应生成 settlement 列表。");
        _test.Eq(
            ArrayValue(projectedWorld, "settlements").Count,
            typedWorld.Settlements.Count,
            "settlement Dictionary 投影数量应与 typed 列表一致。"
        );
        _test.Eq(
            ArrayValue(projectedWorld, "world_npcs").Count,
            typedWorld.WorldNpcs.Count,
            "world npc Dictionary 投影数量应与 typed 列表一致。"
        );
        _test.Eq(
            ArrayValue(projectedWorld, "encounter_anchors").Count,
            typedWorld.EncounterAnchors.Count,
            "encounter anchor 投影数量应与 typed 列表一致。"
        );
        _test.Eq(
            ArrayValue(projectedWorld, "world_events").Count,
            typedWorld.WorldEvents.Count,
            "world event 投影数量应与 typed 列表一致。"
        );
        _test.Eq(
            ArrayValue(projectedWorld, "resource_nodes").Count,
            typedWorld.ResourceNodes.Count,
            "resource node 投影数量应与 typed 列表一致。"
        );
        _test.Eq(
            DictValue(projectedWorld, "mounted_submaps").Count,
            typedWorld.MountedSubmaps.Count,
            "mounted submap 投影数量应与 typed 列表一致。"
        );

        WorldMapSpawnSystem.SettlementInstanceData playerSettlement = null;
        foreach (WorldMapSpawnSystem.SettlementInstanceData settlement in typedWorld.Settlements)
        {
            if (settlement != null && settlement.IsPlayerStart)
            {
                playerSettlement = settlement;
                break;
            }
        }
        _test.True(playerSettlement != null, "typed world build 应保留玩家起始 settlement。");
        if (playerSettlement != null)
        {
            _test.Eq(
                projectedWorld["player_start_settlement_id"].AsString(),
                playerSettlement.SettlementId,
                "public world_data.player_start_settlement_id 应来自 typed settlement。"
            );
            _test.Eq(
                projectedWorld["player_start_settlement_name"].AsString(),
                playerSettlement.DisplayName,
                "public world_data.player_start_settlement_name 应来自 typed settlement。"
            );
        }

        WorldMapSpawnSystem.SettlementInstanceData firstSettlement = typedWorld.Settlements[0];
        _test.True(firstSettlement != null, "typed world build 首个 settlement 不应为空。");
        if (firstSettlement == null)
            return;

        _test.True(
            firstSettlement.SettlementState != null,
            "typed settlement 应持有 typed settlement state。"
        );
        _test.True(
            firstSettlement.AvailableServices.Count > 0,
            "typed settlement 应持有 service entry 列表。"
        );
        GDictionary firstProjectedSettlement = ArrayValue(projectedWorld, "settlements")[0]
            .AsGodotDictionary();
        _test.Eq(
            ArrayValue(firstProjectedSettlement, "facilities").Count,
            firstSettlement.Facilities.Count,
            "typed facility 列表与公开投影数量应一致。"
        );
        _test.Eq(
            ArrayValue(firstProjectedSettlement, "available_services").Count,
            firstSettlement.AvailableServices.Count,
            "typed service entry 列表与公开投影数量应一致。"
        );
        _test.Eq(
            ArrayValue(firstProjectedSettlement, "service_npcs").Count,
            firstSettlement.ServiceNpcs.Count,
            "typed service npc 列表与公开投影数量应一致。"
        );
        _test.Eq(
            firstProjectedSettlement["settlement_id"].AsString(),
            firstSettlement.SettlementId,
            "typed settlement id 应直接映射到公开投影。"
        );
    }

    private void TestTypedWorldBuildGeneratesResourceNodes()
    {
        WorldGenerationDefinition definition = TestWorldGenerationDefinitionFactory.Load(
            TestWorldConfig
        );
        _test.True(definition != null, "resource node generation regression 需要可投影的测试世界配置。");
        if (definition == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        WorldMapSpawnSystem spawnSystem = new();
        WorldMapSpawnSystem.WorldBuildData typedWorld = spawnSystem.BuildWorldTyped(
            definition,
            gridSystem
        );

        _test.True(typedWorld.ResourceNodes.Count > 0, "typed world build 应生成资源点。");

        bool hasFarm = false;
        bool hasHerbGarden = false;
        bool hasMine = false;
        bool hasStartHerbGarden = false;
        var occupiedCoords = new HashSet<Vector2I>();
        foreach (WorldMapResourceNodeData node in typedWorld.ResourceNodes)
        {
            _test.True(!string.IsNullOrEmpty(node.NodeId), "resource node 应有稳定 node_id。");
            _test.True(!string.IsNullOrEmpty(node.DisplayName), "resource node 应有显示名。");
            _test.True(
                gridSystem.IsCellInsideWorld(node.WorldCoord),
                "resource node 坐标应落在世界范围内。"
            );
            _test.True(
                occupiedCoords.Add(node.WorldCoord),
                "resource node 之间不应共享同一世界格。"
            );
            _test.True(
                node.MaxCharges >= node.RemainingCharges && node.RemainingCharges > 0,
                "resource node 初始采集次数应为正数且不超过上限。"
            );

            if (node.NodeKind == WorldMapResourceNodeData.KindFarm)
            {
                hasFarm = true;
                _test.Eq(
                    node.YieldItemId,
                    WorldMapResourceNodeData.YieldTravelRation,
                    "farm 应产出 travel_ration。"
                );
            }
            else if (node.NodeKind == WorldMapResourceNodeData.KindHerbGarden)
            {
                hasHerbGarden = true;
                _test.Eq(
                    node.YieldItemId,
                    WorldMapResourceNodeData.YieldHealingHerb,
                    "herb_garden 应产出 healing_herb。"
                );
                Vector2I delta = node.WorldCoord - typedWorld.PlayerStartCoord;
                if (Mathf.Abs(delta.X) + Mathf.Abs(delta.Y) <= 6)
                    hasStartHerbGarden = true;
            }
            else if (node.NodeKind == WorldMapResourceNodeData.KindMine)
            {
                hasMine = true;
                _test.Eq(
                    node.YieldItemId,
                    WorldMapResourceNodeData.YieldIronOre,
                    "mine 应产出 iron_ore。"
                );
            }
        }

        _test.True(hasFarm, "世界生成至少应包含农田资源点。");
        _test.True(hasHerbGarden, "世界生成至少应包含药园资源点。");
        _test.True(hasMine, "世界生成至少应包含矿场资源点。");
        _test.True(hasStartHerbGarden, "玩家初始点附近应保证一个新手药园。");
    }

    private static GArray ArrayValue(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new GArray();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary DictValue(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new GDictionary();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }
}
