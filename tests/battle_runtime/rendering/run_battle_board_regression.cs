using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_battle_board_regression : SceneTree
{
    private const string BattleBoardScenePath = "res://scenes/ui/battle_board_2d.tscn";

    private static readonly Vector2 ViewportSize = new(1280.0f, 720.0f);
    private static readonly Vector2I TestMapSize = new(19, 11);
    private static readonly Vector2I TestWorldCoord = new(7, 11);
    private const int TestSeed = 424242;
    private const int MaxReadyFrames = 24;

    private readonly TestHarness _test = new();
    private readonly BattleGridService _gridService = new();

    public override async void _Initialize()
    {
        Root.Size = new Vector2I((int)ViewportSize.X, (int)ViewportSize.Y);
        TestCanyonGenerationUsesTypedColumnsAndSupportedProps();
        TestRenderProfileFormalSourceSpecs();
        await TestBoardSceneRendersGeneratedCanyon();
        int exitCode = _test.Finish("Battle board regression");
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private void TestCanyonGenerationUsesTypedColumnsAndSupportedProps()
    {
        GDictionary first = BuildLayout("canyon", TestSeed);
        GDictionary second = BuildLayout("canyon", TestSeed);
        try
        {
            _test.Eq(
                string.Join("\n", CaptureLayoutSignature(first)),
                string.Join("\n", CaptureLayoutSignature(second)),
                "canyon terrain generation should remain deterministic for a fixed seed."
            );
            _test.Eq(DictString(first, "terrain_profile_id"), "canyon", "生成结果应回写正式 terrain_profile_id。");
            _test.True(DictVector2I(first, "map_size") == TestMapSize, "测试上下文应能固定 canyon battle_map_size。");
            _test.True(DictCount(first, "cells") > 0, "canyon 生成结果应包含 cells。");
            _test.True(DictCount(first, "cell_columns") > 0, "canyon 生成结果应包含 typed cell_columns。");
            _test.True(CountProp(first, "objective_marker") >= 1, "canyon 应生成 objective marker prop。");
            _test.True(CountProp(first, "tent") >= 2, "canyon 应保留双方 tent prop。");
            _test.True(CountProp(first, "torch") >= 2, "canyon 应保留 torch prop。");
            AssertColumnsMatchSurfaceCells(first);
            AssertSpawnCoordsAvoidWater(first, "canyon");
            AssertLayoutUsesSupportedProps(first);
        }
        finally
        {
            BattleTestFixture.DisposeBattleLayout(first);
            BattleTestFixture.DisposeBattleLayout(second);
        }
    }

    private void TestRenderProfileFormalSourceSpecs()
    {
        foreach (
            StringName terrainId in new[]
            {
                BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT(),
                BattleBoardRenderProfile.TERRAIN_PROFILE_CANYON(),
                BattleBoardRenderProfile.TERRAIN_PROFILE_NARROW_ASSAULT(),
                BattleBoardRenderProfile.TERRAIN_PROFILE_HOLDOUT_PUSH(),
                ProgressionDataUtils.to_string_name("unknown_fixture_profile"),
            }
        )
        {
            BattleBoardRenderProfile profile = BattleBoardRenderProfile.ForTerrainProfileId(terrainId);
            try
            {
                _test.True(profile != null, $"terrain profile 应解析出 render profile：{terrainId}");
                if (profile == null)
                    continue;
                _test.Eq(profile.render_profile_id, BattleBoardRenderProfile.RENDER_PROFILE_CANYON_ISO64(), "battle board 当前应统一使用 canyon iso64 render profile。");
                _test.Eq(profile.asset_dir, BattleBoardRenderProfile.DEFAULT_ASSET_DIR(), "render profile 应指向正式 canyon 资产目录。");
                _test.Eq(profile.visual_height_step, BattleBoardRenderProfile.DEFAULT_VISUAL_HEIGHT_STEP(), "render profile 应持有正式视觉高度步长。");
                _test.Eq(profile.board_tile_size, BattleBoardRenderProfile.DEFAULT_BOARD_TILE_SIZE(), "render profile 应显式持有棋盘 tile size。");
                IReadOnlyList<BattleBoardSourceSpec> sourceSpecs = profile.GetSourceSpecsTyped();
                _test.True(sourceSpecs.Count > 0, "render profile 应以 formal source spec 表驱动 TileSet 注册。");
            }
            finally
            {
                GodotRefCountedDisposer.DisposeIfValid(profile);
            }
        }

        BattleBoardRenderProfile canyon = BattleBoardRenderProfile.ForTerrainProfileId("canyon");
        try
        {
            foreach (BattleBoardSourceSpec sourceSpec in canyon.GetSourceSpecsTyped())
            {
                _test.True(sourceSpec.Key != "", "source spec 应包含 key。");
                _test.True(sourceSpec.Files.Count > 0, "source spec 应包含 files。");
                _test.True(sourceSpec.LayerRole != "", "source spec 应包含 layer_role。");
                _test.True(
                    sourceSpec.AtlasRegionSize != Vector2I.Zero,
                    "source spec 应包含 atlas_region_size。"
                );
                foreach (string fileName in sourceSpec.Files)
                {
                    string path = $"{canyon.asset_dir}/{fileName}";
                    _test.True(FileAccess.FileExists(path), $"source spec 贴图必须存在：{path}");
                }
            }
        }
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(canyon);
        }
    }

    private async Task TestBoardSceneRendersGeneratedCanyon()
    {
        GDictionary layout = BuildLayout("canyon", TestSeed);
        BattleState state = BuildState(layout);
        PackedScene battleBoardScene = GD.Load<PackedScene>(BattleBoardScenePath);
        GodotRefCountedDisposer.KeepBorrowedResourceGraphAlive(battleBoardScene);
        BattleBoard2D board = battleBoardScene.Instantiate<BattleBoard2D>();
        GVector2IArray allCoords = null;
        GVector2IArray emptyPreviewCoords = null;
        GDictionary targetHitBadges = null;
        try
        {
            Root.AddChild(board);
            await ProcessFrames(1);

            Vector2I selectedCoord = DictVector2I(layout, "player_coord");
            allCoords = CollectAllCoords(state);
            emptyPreviewCoords = new GVector2IArray();
            targetHitBadges = new GDictionary();
            board.SetViewportSize(ViewportSize);
            board.Configure(
                state,
                selectedCoord,
                emptyPreviewCoords,
                allCoords,
                "single_unit",
                1,
                1,
                targetHitBadges
            );

            bool ready = await WaitForBoardRenderReady(board);
            _test.True(ready, "BattleBoard2D Configure 后应在有限帧内完成渲染内容。");
            _test.True(HasAnyUsedLayer(board, "TopH", 0, 8), "BattleBoard2D 应渲染至少一个 top tile。");
            _test.True(board.prop_layer != null && board.prop_layer.GetChildCount() > 0, "BattleBoard2D 应渲染地形 prop。");
            _test.Eq(board.unit_layer?.GetChildCount() ?? 0, 2, "BattleBoard2D 应渲染测试双方单位 token。");
            _test.Eq(board._render_profile?.terrain_profile_id.ToString() ?? "", "canyon", "BattleBoard2D 应按 battle state 绑定 terrain render profile。");

            board.QueueFree();
            await ProcessFrames(1);
            System.GC.SuppressFinalize(board);
        }
        finally
        {
            GodotCollectionDisposer.DisposeOwnedPayloadTree(emptyPreviewCoords);
            GodotCollectionDisposer.DisposeOwnedPayloadTree(allCoords);
            GodotCollectionDisposer.DisposeOwnedPayloadTree(targetHitBadges);
            BattleTestFixture.DisposeBattleState(state);
            BattleTestFixture.DisposeBattleLayout(layout);
        }
    }

    private GDictionary BuildLayout(string profileId, int seed)
    {
        var generator = new BattleTerrainGenerator();
        EncounterAnchorData anchor = BuildEncounterAnchor(
            "battle_board_regression",
            "battle board regression",
            profileId
        );
        try
        {
            return generator.GenerateTyped(
                anchor,
                seed,
                new GDictionary
                {
                    ["world_coord"] = TestWorldCoord,
                    ["world_seed"] = seed,
                    ["battle_terrain_profile"] = profileId,
                    ["battle_map_size"] = TestMapSize,
                }
            );
        }
        finally
        {
            generator.Dispose();
            GodotRefCountedDisposer.DisposeIfValid(anchor);
        }
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        string entityId,
        string displayName,
        string regionTag
    ) =>
        new()
        {
            entity_id = entityId,
            display_name = displayName,
            faction_id = "hostile",
            region_tag = regionTag,
            world_coord = TestWorldCoord,
            encounter_kind = "single",
        };

    private BattleState BuildState(GDictionary layout)
    {
        var state = new BattleState
        {
            battle_id = "battle_board_regression",
            seed = TestSeed,
            map_size = DictVector2I(layout, "map_size"),
            world_coord = TestWorldCoord,
            terrain_profile_id = ProgressionDataUtils.to_string_name(
                DictString(layout, "terrain_profile_id", "default")
            ),
            ally_unit_ids = new GStringNameArray(),
            enemy_unit_ids = new GStringNameArray(),
        };
        GDictionary sourceCells = DictDict(layout, "cells");
        try
        {
            state.SetCellsFromDictionary(CloneCells(sourceCells));
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(sourceCells);
        }
        BattleUnitState ally = BuildUnit("ally_board", "队员", "player", 160);
        BattleUnitState enemy = BuildUnit("enemy_board", "敌人", "hostile", 120);
        RegisterAndPlace(state, ally, DictVector2I(layout, "player_coord"), false);
        RegisterAndPlace(state, enemy, DictVector2I(layout, "enemy_coord"), true);
        state.active_unit_id = ally.unit_id;
        state.phase = "unit_acting";
        state.timeline.current_tu = 120;
        state.log_entries.Add("Battle board regression fixture.");
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = factionId == "player" ? "manual" : "ai",
            current_hp = hp,
            current_mp = 30,
            current_stamina = 40,
            current_aura = 10,
            current_ap = 2,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 14);
        unit.known_active_skill_ids.Add("basic_attack");
        unit.known_skill_level_map["basic_attack"] = 1;
        unit.RefreshFootprint();
        return unit;
    }

    private void RegisterAndPlace(BattleState state, BattleUnitState unit, Vector2I coord, bool enemy)
    {
        state.SetUnit(unit);
        if (enemy)
            state.enemy_unit_ids.Add(unit.unit_id);
        else
            state.ally_unit_ids.Add(unit.unit_id);
        _test.True(_gridService.PlaceUnit(state, unit, coord, true), $"测试单位应可放置到 {coord}：{unit.unit_id}");
    }

    private static GDictionary CloneCells(GDictionary cells)
    {
        var cloned = new GDictionary();
        foreach (Variant coordValue in cells.Keys)
        {
            try
            {
                if (coordValue.VariantType != Variant.Type.Vector2I)
                    continue;
                Variant cellValue = cells[coordValue];
                try
                {
                    BattleCellState cell = cellValue.AsGodotObject() as BattleCellState;
                    if (cell != null)
                        cloned[coordValue.AsVector2I()] = cell.DuplicateCell();
                }
                finally
                {
                    cellValue.Dispose();
                }
            }
            finally
            {
                coordValue.Dispose();
            }
        }
        return cloned;
    }

    private static GVector2IArray CollectAllCoords(BattleState state)
    {
        var coords = new GVector2IArray();
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
                coords.Add(new Vector2I(x, y));
        }
        return coords;
    }

    private async Task<bool> WaitForBoardRenderReady(BattleBoard2D board)
    {
        if (board == null)
            return false;
        if (board.IsRenderContentReady())
            return true;
        for (int frame = 0; frame < MaxReadyFrames; frame++)
        {
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
            if (board.IsRenderContentReady())
                return true;
        }
        return false;
    }

    private async Task ProcessFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static bool HasAnyUsedLayer(BattleBoard2D board, string prefix, int start, int end)
    {
        if (board == null)
            return false;
        for (int height = start; height <= end; height++)
        {
            TileMapLayer layer = board.GetNodeOrNull<TileMapLayer>($"{prefix}{height}");
            if (CountUsedCells(layer) > 0)
                return true;
        }
        return false;
    }

    private static int CountUsedCells(TileMapLayer layer)
    {
        if (layer == null)
            return 0;
        Godot.Collections.Array<Vector2I> cells = layer.GetUsedCells();
        try
        {
            return cells.Count;
        }
        finally
        {
            GodotCollectionDisposer.DisposeOwnedPayloadTree(cells);
        }
    }

    private void AssertColumnsMatchSurfaceCells(GDictionary layout)
    {
        GDictionary cells = DictDict(layout, "cells");
        GDictionary columns = DictDict(layout, "cell_columns");
        try
        {
            bool foundStackedColumn = false;
            foreach (Variant coordValue in cells.Keys)
            {
                try
                {
                    Variant surfaceCellValue = cells[coordValue];
                    try
                    {
                        BattleCellState surfaceCell =
                            surfaceCellValue.AsGodotObject() as BattleCellState;
                        if (surfaceCell == null)
                            continue;
                        bool hasColumn = columns.TryReadValue(coordValue, out Variant columnValue);
                        try
                        {
                            _test.True(
                                hasColumn,
                                $"cell_columns 应包含 surface cell 坐标：{coordValue}"
                            );
                            if (!hasColumn || columnValue.VariantType != Variant.Type.Array)
                                continue;
                            GArray column = columnValue.AsGodotArray();
                            try
                            {
                                _test.True(
                                    column.Count >= surfaceCell.current_height + 1,
                                    $"cell_columns 应按地表高度生成真实堆叠列：{coordValue}"
                                );
                                if (column.Count > 1)
                                    foundStackedColumn = true;
                            }
                            finally
                            {
                                GodotCollectionDisposer.DisposeWrapperOnly(column);
                            }
                        }
                        finally
                        {
                            columnValue.Dispose();
                        }
                    }
                    finally
                    {
                        surfaceCellValue.Dispose();
                    }
                }
                finally
                {
                    coordValue.Dispose();
                }
            }
            _test.True(foundStackedColumn, "canyon 地图应至少包含一个多层堆叠列。");
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(cells);
            GodotCollectionDisposer.DisposeWrapperOnly(columns);
        }
    }

    private void AssertSpawnCoordsAvoidWater(GDictionary layout, string label)
    {
        GDictionary cells = DictDict(layout, "cells");
        try
        {
            foreach (Vector2I coord in CollectSpawnCoords(layout))
            {
                bool hasCell = cells.TryReadValue(coord, out Variant cellValue);
                try
                {
                    BattleCellState cell =
                        hasCell ? cellValue.AsGodotObject() as BattleCellState : null;
                    _test.True(cell != null, $"{label} spawn 应指向有效 battle cell：{coord}");
                    if (cell != null)
                    {
                        _test.False(
                            BattleTerrainRules.IsWaterTerrain(cell.base_terrain),
                            $"{label} spawn 不应落在水域：{coord}"
                        );
                    }
                }
                finally
                {
                    cellValue.Dispose();
                }
            }
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(cells);
        }
    }

    private static List<Vector2I> CollectSpawnCoords(GDictionary layout)
    {
        var result = new List<Vector2I>
        {
            DictVector2I(layout, "player_coord"),
            DictVector2I(layout, "enemy_coord"),
        };
        foreach (string fieldName in new[] { "ally_spawns", "enemy_spawns" })
        {
            GArray coords = DictArray(layout, fieldName);
            try
            {
                foreach (Variant coordValue in coords)
                {
                    try
                    {
                        if (coordValue.VariantType == Variant.Type.Vector2I)
                            result.Add(coordValue.AsVector2I());
                    }
                    finally
                    {
                        coordValue.Dispose();
                    }
                }
            }
            finally
            {
                GodotCollectionDisposer.DisposeWrapperOnly(coords);
            }
        }
        return result;
    }

    private void AssertLayoutUsesSupportedProps(GDictionary layout)
    {
        GDictionary cells = DictDict(layout, "cells");
        try
        {
            foreach (Variant cellValue in cells.Values)
            {
                try
                {
                    BattleCellState cell = cellValue.AsGodotObject() as BattleCellState;
                    if (cell == null)
                        continue;
                    foreach (StringName propId in cell.prop_ids)
                        _test.True(BattleBoardPropCatalog.IsSupported(propId), $"显式 prop_id 必须来自正式 prop catalog：{propId}");
                }
                finally
                {
                    cellValue.Dispose();
                }
            }
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(cells);
        }
    }

    private static int CountProp(GDictionary layout, string propId)
    {
        int count = 0;
        GDictionary cells = DictDict(layout, "cells");
        try
        {
            foreach (Variant cellValue in cells.Values)
            {
                try
                {
                    BattleCellState cell = cellValue.AsGodotObject() as BattleCellState;
                    if (cell == null)
                        continue;
                    foreach (StringName cellPropId in cell.prop_ids)
                    {
                        if (cellPropId.ToString() == propId)
                            count++;
                    }
                }
                finally
                {
                    cellValue.Dispose();
                }
            }
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(cells);
        }
        return count;
    }

    private static List<string> CaptureLayoutSignature(GDictionary layout)
    {
        var lines = new List<string>();
        GDictionary cells = DictDict(layout, "cells");
        try
        {
            var coords = new List<Vector2I>();
            foreach (Variant coordValue in cells.Keys)
            {
                try
                {
                    if (coordValue.VariantType == Variant.Type.Vector2I)
                        coords.Add(coordValue.AsVector2I());
                }
                finally
                {
                    coordValue.Dispose();
                }
            }
            coords.Sort((left, right) => left.Y == right.Y ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y));
            foreach (Vector2I coord in coords)
            {
                bool hasCell = cells.TryReadValue(coord, out Variant cellValue);
                try
                {
                    BattleCellState cell =
                        hasCell ? cellValue.AsGodotObject() as BattleCellState : null;
                    if (cell == null)
                        continue;
                    lines.Add(
                        $"{coord.X},{coord.Y}|{cell.base_terrain}|{cell.current_height}|{string.Join(",", StringifyProps(cell.prop_ids))}"
                    );
                }
                finally
                {
                    cellValue.Dispose();
                }
            }
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(cells);
        }
        return lines;
    }

    private static List<string> StringifyProps(GStringNameArray propIds)
    {
        var values = new List<string>();
        foreach (StringName propId in propIds)
            values.Add(propId.ToString());
        values.Sort();
        return values;
    }

    private static GDictionary DictDict(GDictionary dict, object key)
    {
        return dict.ReadDictionaryOrEmpty(key);
    }

    private static int DictCount(GDictionary dict, object key)
    {
        GDictionary value = DictDict(dict, key);
        try
        {
            return value.Count;
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(value);
        }
    }

    private static GArray DictArray(GDictionary dict, object key)
    {
        return dict.ReadArrayOrEmpty(key);
    }

    private static Vector2I DictVector2I(GDictionary dict, object key, Vector2I fallback = default)
    {
        return dict.ReadVector2I(key, fallback);
    }

    private static string DictString(GDictionary dict, object key, string fallback = "")
    {
        return dict.ReadString(key, fallback);
    }
}
