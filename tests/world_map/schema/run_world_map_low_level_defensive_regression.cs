using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_low_level_defensive_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestWorldJsonRegistryListsAndFindsTypedPresets();
        TestGridFootprintStateUsesPublicBehavior();
        TestVisibilityRebuildIgnoresForeignFactionSources();
        TestFogPersistentRevisionOnlyTracksPersistentChanges();
        TestFogRevealExportLoadKeepsRevealedCells();

        return _test.Finish("World map low-level defensive regression");
    }

    private void TestWorldJsonRegistryListsAndFindsTypedPresets()
    {
        var registry = new WorldContentRegistry();
        registry.Rebuild();
        IReadOnlyDictionary<StringName, WorldPresetDefinition> presets = registry.GetPresets();
        _test.Eq(registry.GetValidationErrors().Count, 0, "world JSON registry 不应包含导入错误。");
        _test.True(presets.Count > 0, "world JSON registry 应暴露 typed 预设列表。");
        _test.True(
            presets.TryGetValue("test", out WorldPresetDefinition testPreset),
            "world JSON registry typed 查询应找到 test 预设。"
        );
        _test.Eq(
            testPreset?.DisplayName,
            "测试",
            "world JSON registry typed 查询应保留 test 预设名称。"
        );
    }

    private void TestGridFootprintStateUsesPublicBehavior()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.Setup(new Vector2I(2, 2), new Vector2I(4, 4));

        _test.False(
            gridSystem.RegisterFootprint("", new Vector2I(1, 1), Vector2I.One),
            "空 entity_id 不应注册 footprint。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "", "空 entity_id 注册失败后不应占格。");

        _test.True(
            gridSystem.RegisterFootprint("camp", new Vector2I(1, 1), new Vector2I(2, 2)),
            "合法 footprint 应可注册。"
        );
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(1, 1)), "camp", "注册后 origin 应暴露占位根。");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(2, 2)), "camp", "注册后 footprint 覆盖格应暴露占位根。");

        _test.False(
            gridSystem.CanPlaceFootprint(new Vector2I(2, 2), Vector2I.One),
            "已有 footprint 的格子不应允许再次占用。"
        );
        _test.True(
            gridSystem.RegisterFootprint("camp", new Vector2I(4, 4), new Vector2I(2, 2)),
            "同一 entity 成功重注册时应移动 footprint。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(1, 1)),
            "",
            "成功重注册后旧 origin 应清空。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(2, 2)),
            "",
            "成功重注册后旧 footprint 覆盖格应清空。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(4, 4)),
            "camp",
            "成功重注册后新 origin 应暴露占位根。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(5, 5)),
            "camp",
            "成功重注册后新 footprint 覆盖格应暴露占位根。"
        );
        _test.False(
            gridSystem.RegisterFootprint("camp", new Vector2I(7, 7), new Vector2I(2, 2)),
            "同一 entity 移动到越界 footprint 应失败。"
        );
        _test.Eq(
            gridSystem.GetOccupantRoot(new Vector2I(4, 4)),
            "camp",
            "同一 entity 移动失败后应恢复原 footprint。"
        );

        gridSystem.ClearFootprint("camp");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(4, 4)), "", "清理 footprint 后 origin 不应继续占格。");
        _test.Eq(gridSystem.GetOccupantRoot(new Vector2I(5, 5)), "", "清理 footprint 后覆盖格不应继续占格。");
    }

    private void TestVisibilityRebuildIgnoresForeignFactionSources()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.Setup(new Vector2I(8, 8));

        var playerSource = new VisionSourceData("scout", new Vector2I(2, 2), 1, "player");
        var hostileSource = new VisionSourceData("raider", new Vector2I(5, 5), 1, "hostile");

        fogSystem.RebuildVisibilityForFaction("player", new[] { playerSource, hostileSource });

        _test.True(
            fogSystem.IsVisible(new Vector2I(2, 2), "player"),
            "玩家阵营的自有视野源应继续正常生效。"
        );
        _test.False(
            fogSystem.IsVisible(new Vector2I(5, 5), "player"),
            "foreign faction 的视野源不应污染当前阵营可见区。"
        );
    }

    private void TestFogRevealExportLoadKeepsRevealedCells()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.Setup(new Vector2I(8, 8));

        List<Vector2I> revealedCoords = fogSystem.RevealDiamond(
            new Vector2I(3, 3),
            1,
            "player"
        );
        _test.True(revealedCoords.Contains(new Vector2I(3, 3)), "迷雾揭示应返回中心格。");

        using GodotProjectionLease<GDictionary> persistedStateLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                fogSystem.BuildPersistentStatePlain(),
                "WorldMapLowLevelDefensiveRegression.fog_state",
                LifetimeDomain.Request,
                "WorldMapLowLevelDefensiveRegression.fog_state"
            );
        using GDictionary factions = persistedStateLease.Value["factions"].AsGodotDictionary();
        using GDictionary playerFogState = factions["player"].AsGodotDictionary();
        using GArray exploredCoords = playerFogState["explored"].AsGodotArray();
        using GArray revealedStateCoords = playerFogState["revealed"].AsGodotArray();
        _test.True(
            exploredCoords.Count > 0
                && AllCoordsAreNativeVector2I(exploredCoords)
                && AllCoordsAreNativeVector2I(revealedStateCoords),
            "迷雾持久态的 explored/revealed 数组应只包含原生 Vector2I。"
        );
        var restoredFogSystem = new WorldMapFogSystem();
        restoredFogSystem.Setup(new Vector2I(8, 8), persistedStateLease.Value);

        _test.True(
            restoredFogSystem.IsExplored(new Vector2I(3, 3), "player"),
            "持久化恢复后 paid reveal 中心格应保持已探索。"
        );
        _test.False(
            restoredFogSystem.IsVisible(new Vector2I(3, 3), "player"),
            "持久化恢复不应把 paid reveal 误当作当前可见。"
        );

        var distantSource = new VisionSourceData("scout", new Vector2I(7, 7), 0, "player");
        restoredFogSystem.RebuildVisibilityForFaction("player", new[] { distantSource });
        _test.True(
            restoredFogSystem.IsExplored(new Vector2I(3, 3), "player"),
            "后续可见性刷新不应清除已持久化的 paid reveal。"
        );
    }

    private void TestFogPersistentRevisionOnlyTracksPersistentChanges()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.Setup(new Vector2I(8, 8));
        long setupRevision = fogSystem.PersistentRevision;
        var source = new VisionSourceData("scout", new Vector2I(2, 2), 1, "player");

        fogSystem.RebuildVisibilityForFaction("player", new[] { source });
        long exploredRevision = fogSystem.PersistentRevision;
        _test.True(
            exploredRevision > setupRevision,
            "首次视野刷新写入 explored 坐标时应推进 fog persistent revision。"
        );

        fogSystem.RebuildVisibilityForFaction("player", new[] { source });
        _test.Eq(
            fogSystem.PersistentRevision,
            exploredRevision,
            "仅重建相同 visible 集合且没有新增 explored 时不应推进 persistent revision。"
        );

        fogSystem.RevealDiamond(new Vector2I(5, 5), 1, "player");
        long revealRevision = fogSystem.PersistentRevision;
        _test.True(
            revealRevision > exploredRevision,
            "新增 paid reveal 坐标时应推进 fog persistent revision。"
        );
        fogSystem.RevealDiamond(new Vector2I(5, 5), 1, "player");
        _test.Eq(
            fogSystem.PersistentRevision,
            revealRevision,
            "重复揭示同一区域不应制造虚假的 persistent revision。"
        );
    }

    private static bool AllCoordsAreNativeVector2I(GArray coords)
    {
        foreach (Variant coord in coords)
        {
            if (coord.VariantType != Variant.Type.Vector2I)
                return false;
        }
        return true;
    }

}
