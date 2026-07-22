using System.Collections.Generic;
using Godot;

public partial class run_world_map_spawn_typed_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTypedWorldBuildProjectsSettlementsAndAnchors();
        TestPlayerVillageUsesAuthoredNpcServices();
        TestTypedWorldBuildGeneratesResourceNodes();

        RequestTestExit(_test.Finish("World map spawn typed regression"));
    }

    private void TestPlayerVillageUsesAuthoredNpcServices()
    {
        WorldGenerationDefinition definition = TestWorldGenerationDefinitionFactory.Load(
            TestWorldConfig
        );
        _test.True(definition != null, "玩家村庄 NPC 回归需要可投影的测试世界配置。");
        if (definition == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        WorldMapSpawnSystem.WorldBuildData typedWorld = new WorldMapSpawnSystem().BuildWorldTyped(
            definition,
            gridSystem
        );
        WorldMapSpawnSystem.SettlementInstanceData playerVillage = null;
        foreach (WorldMapSpawnSystem.SettlementInstanceData settlement in typedWorld.Settlements)
        {
            if (settlement != null && settlement.IsPlayerStart)
            {
                playerVillage = settlement;
                break;
            }
        }

        _test.True(playerVillage != null, "世界生成应包含玩家出生村庄。");
        if (playerVillage == null)
            return;

        _test.Eq(playerVillage.Tier, 0, "玩家出生据点应保持村庄层级。");
        _test.Eq(playerVillage.Facilities.Count, 1, "出生村庄不应为新手任务新增设施。");
        if (playerVillage.Facilities.Count == 1)
        {
            _test.Eq(
                playerVillage.Facilities[0].TemplateId,
                "village_hearth",
                "出生村庄唯一设施应继续使用 village_hearth。"
            );
        }

        var interactionIds = new HashSet<string>();
        var actionIdByInteractionId = new Dictionary<string, string>();
        foreach (WorldMapSpawnSystem.ServiceEntryData service in playerVillage.AvailableServices)
        {
            if (service == null)
                continue;
            interactionIds.Add(service.InteractionScriptId);
            actionIdByInteractionId[service.InteractionScriptId] = service.ActionId;
        }

        _test.True(interactionIds.Contains("npc_village_chief"), "出生村庄应提供村长任务入口。");
        _test.True(interactionIds.Contains("npc_village_healer"), "出生村庄应提供村医任务入口。");
        _test.True(interactionIds.Contains("npc_old_hunter"), "出生村庄应提供老猎人任务入口。");
        _test.True(interactionIds.Contains("service_rest_basic"), "村长改为任务发布者后仍应保留基础休息服务。");
        _test.False(interactionIds.Contains("party_warehouse"), "村庄不应提供正式仓库服务。");

        actionIdByInteractionId.TryGetValue("npc_village_chief", out string chiefActionId);
        actionIdByInteractionId.TryGetValue("npc_village_healer", out string healerActionId);
        actionIdByInteractionId.TryGetValue("npc_old_hunter", out string hunterActionId);
        _test.True(!string.IsNullOrEmpty(chiefActionId), "村长服务应有非空 action_id。");
        _test.True(!string.IsNullOrEmpty(healerActionId), "村医服务应有非空 action_id。");
        _test.True(!string.IsNullOrEmpty(hunterActionId), "老猎人服务应有非空 action_id。");
        _test.True(
            chiefActionId != healerActionId,
            "村长与村医的 action_id 不应相同，否则据点窗口会把村医请求路由到村长的委托面板。"
        );
        _test.True(
            chiefActionId != hunterActionId,
            "村长与老猎人的 action_id 不应相同，否则据点窗口会把老猎人请求路由到村长的委托面板。"
        );
        _test.True(
            healerActionId != hunterActionId,
            "村医与老猎人的 action_id 不应相同，否则据点窗口会把两者请求互相混淆。"
        );

        int villageCount = 0;
        foreach (WorldMapSpawnSystem.SettlementInstanceData settlement in typedWorld.Settlements)
        {
            if (settlement == null || settlement.Tier != 0)
                continue;
            villageCount++;
            bool hasQuartermaster = false;
            bool hasSyntheticServiceDesk = false;
            bool hasWarehouse = false;
            foreach (WorldMapSpawnSystem.ServiceEntryData service in settlement.AvailableServices)
            {
                if (service == null)
                    continue;
                hasQuartermaster |= service.NpcName == "军需官";
                hasSyntheticServiceDesk |= service.FacilityName == "据点服务台";
                hasWarehouse |= service.InteractionScriptId == "party_warehouse";
            }
            _test.False(hasQuartermaster, $"村庄 {settlement.DisplayName} 不应出现军需官。");
            _test.False(hasSyntheticServiceDesk, $"村庄 {settlement.DisplayName} 不应出现虚构据点服务台。");
            _test.False(hasWarehouse, $"村庄 {settlement.DisplayName} 不应提供正式仓库服务。");
        }
        _test.True(villageCount > 0, "村庄服务回归需要至少一个 tier 0 据点。");
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
        Dictionary<string, object> projectedWorld =
            WorldMapSpawnProjection.BuildSnapshotPlain(typedWorld);

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
                StringValue(projectedWorld, "player_start_settlement_id"),
                playerSettlement.SettlementId,
                "public world_data.player_start_settlement_id 应来自 typed settlement。"
            );
            _test.Eq(
                StringValue(projectedWorld, "player_start_settlement_name"),
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
        IReadOnlyDictionary<string, object> firstProjectedSettlement =
            ArrayValue(projectedWorld, "settlements")[0]
                as IReadOnlyDictionary<string, object>
            ?? new Dictionary<string, object>();
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
            StringValue(firstProjectedSettlement, "settlement_id"),
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

    private static IReadOnlyList<object> ArrayValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is IReadOnlyList<object> array
            ? array
            : System.Array.Empty<object>();
    }

    private static IReadOnlyDictionary<string, object> DictValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    )
    {
        return dictionary != null
            && dictionary.TryGetValue(key, out object value)
            && value is IReadOnlyDictionary<string, object> nested
            ? nested
            : new Dictionary<string, object>();
    }

    private static string StringValue(
        IReadOnlyDictionary<string, object> dictionary,
        string key
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is string text
            ? text
            : "";
}
