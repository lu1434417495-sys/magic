using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_spawn_typed_regression : SceneTree
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

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("World map spawn typed regression"));
    }

    private void TestTypedWorldBuildProjectsSettlementsAndAnchors()
    {
        WorldMapGenerationConfig config = ResourceLoader.Load<WorldMapGenerationConfig>(
            TestWorldConfig
        );
        _test.True(config != null, "typed world build regression 需要可加载的测试世界配置。");
        if (config == null)
            return;

        WorldMapGridSystem gridSystem = new();
        gridSystem.Setup(config.world_size_in_chunks, config.chunk_size);
        WorldMapSpawnSystem spawnSystem = new();
        WorldMapSpawnSystem.WorldBuildData typedWorld = spawnSystem.BuildWorldTyped(
            config,
            gridSystem
        );
        GDictionary projectedWorld = typedWorld.ToDictionary();

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
