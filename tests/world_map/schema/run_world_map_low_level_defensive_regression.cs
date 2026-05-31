using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_world_map_low_level_defensive_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestGridFootprintStateUsesPublicBehavior();
        TestGridCellSurfaceKeepsMinimalRuntimeContract();
        TestVisibilityRebuildIgnoresForeignFactionSources();
        TestFogRevealExportLoadKeepsRevealedCells();

        if (_failures.Count == 0)
        {
            GD.Print("World map low-level defensive regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World map low-level defensive regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestGridFootprintStateUsesPublicBehavior()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.setup(new Vector2I(2, 2), new Vector2I(4, 4));

        AssertFalse(
            gridSystem.register_footprint("", new Vector2I(1, 1), Vector2I.One),
            "空 entity_id 不应注册 footprint。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "", "空 entity_id 注册失败后不应占格。");

        AssertTrue(
            gridSystem.register_footprint("camp", new Vector2I(1, 1), new Vector2I(2, 2)),
            "合法 footprint 应可注册。"
        );
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "camp", "注册后 origin 应暴露占位根。");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(2, 2)), "camp", "注册后 footprint 覆盖格应暴露占位根。");

        AssertFalse(
            gridSystem.can_place_footprint(new Vector2I(2, 2), Vector2I.One),
            "已有 footprint 的格子不应允许再次占用。"
        );
        AssertFalse(
            gridSystem.register_footprint("camp", new Vector2I(7, 7), new Vector2I(2, 2)),
            "同一 entity 移动到越界 footprint 应失败。"
        );
        AssertEq(
            gridSystem.get_occupant_root(new Vector2I(1, 1)),
            "camp",
            "同一 entity 移动失败后应恢复原 footprint。"
        );

        gridSystem.clear_footprint("camp");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(1, 1)), "", "清理 footprint 后 origin 不应继续占格。");
        AssertEq(gridSystem.get_occupant_root(new Vector2I(2, 2)), "", "清理 footprint 后覆盖格不应继续占格。");
    }

    private void TestGridCellSurfaceKeepsMinimalRuntimeContract()
    {
        var gridSystem = new WorldMapGridSystem();
        gridSystem.setup(new Vector2I(2, 2), new Vector2I(4, 4));
        gridSystem.register_footprint("camp", new Vector2I(5, 6), Vector2I.One);

        WorldMapCellData cell = gridSystem.get_cell(new Vector2I(5, 6));
        AssertTrue(cell != null, "世界地图格子读取面应继续返回有效格子对象。");
        AssertEq(cell.coord, new Vector2I(5, 6), "格子读取面应继续暴露正式坐标。");
        AssertEq(cell.chunk_coord, new Vector2I(1, 1), "格子读取面应继续暴露区块坐标。");
        AssertEq(cell.occupant_id, "camp", "格子读取面应继续暴露占用者 id。");
        AssertEq(cell.footprint_root_id, "camp", "格子读取面应继续暴露占位根 id。");
        AssertFalse(
            PropertyListHasName(cell, "terrain_visual_type"),
            "WorldMapCellData 不应继续暴露未消费的 terrain_visual_type 字段。"
        );
        AssertFalse(
            gridSystem.HasMethod("get_cells_in_rect"),
            "WorldMapGridSystem 不应继续保留无调用方的 get_cells_in_rect()。"
        );
    }

    private void TestVisibilityRebuildIgnoresForeignFactionSources()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.setup(new Vector2I(8, 8));

        var playerSource = new VisionSourceData("scout", new Vector2I(2, 2), 1, "player");
        var hostileSource = new VisionSourceData("raider", new Vector2I(5, 5), 1, "hostile");

        fogSystem.rebuild_visibility_for_faction("player", new GArray { playerSource, hostileSource });

        AssertTrue(
            fogSystem.is_visible(new Vector2I(2, 2), "player"),
            "玩家阵营的自有视野源应继续正常生效。"
        );
        AssertFalse(
            fogSystem.is_visible(new Vector2I(5, 5), "player"),
            "foreign faction 的视野源不应污染当前阵营可见区。"
        );
    }

    private void TestFogRevealExportLoadKeepsRevealedCells()
    {
        var fogSystem = new WorldMapFogSystem();
        fogSystem.setup(new Vector2I(8, 8));

        Godot.Collections.Array<Vector2I> revealedCoords = fogSystem.reveal_diamond(
            new Vector2I(3, 3),
            1,
            "player"
        );
        AssertTrue(revealedCoords.Contains(new Vector2I(3, 3)), "迷雾揭示应返回中心格。");

        GDictionary persistedState = fogSystem.export_persistent_state();
        var restoredFogSystem = new WorldMapFogSystem();
        restoredFogSystem.setup(new Vector2I(8, 8), persistedState);

        AssertTrue(
            restoredFogSystem.is_explored(new Vector2I(3, 3), "player"),
            "持久化恢复后 paid reveal 中心格应保持已探索。"
        );
        AssertFalse(
            restoredFogSystem.is_visible(new Vector2I(3, 3), "player"),
            "持久化恢复不应把 paid reveal 误当作当前可见。"
        );

        var distantSource = new VisionSourceData("scout", new Vector2I(7, 7), 0, "player");
        restoredFogSystem.rebuild_visibility_for_faction("player", new GArray { distantSource });
        AssertTrue(
            restoredFogSystem.is_explored(new Vector2I(3, 3), "player"),
            "后续可见性刷新不应清除已持久化的 paid reveal。"
        );
    }

    private static bool PropertyListHasName(GodotObject instance, string propertyName)
    {
        if (instance == null)
        {
            return false;
        }

        foreach (GDictionary propertyInfo in instance.GetPropertyList())
        {
            if (string.Equals(
                propertyInfo.GetValueOrDefault("name", "").AsString(),
                propertyName,
                StringComparison.Ordinal
            ))
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
