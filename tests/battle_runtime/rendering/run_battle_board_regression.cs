using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_battle_board_regression : LifecycleTestSceneTree
{
    private static readonly PackedScene BattleBoardScene = GD.Load<PackedScene>(
        "res://scenes/ui/battle_board_2d.tscn"
    );

    private static readonly Vector2 ViewportSize = new(1280.0f, 720.0f);
    private static readonly Vector2I TestMapSize = new(19, 11);
    private static readonly Vector2I ExplicitMapSize = new(21, 13);
    private static readonly Vector2I TestWorldCoord = new(7, 11);
    private const int TestSeed = 424242;
    private const int MaxReadyFrames = 24;

    private readonly TestHarness _test = new();
    private readonly BattleGridService _gridService = new();

    public override async void _Initialize()
    {
        Root.Size = new Vector2I((int)ViewportSize.X, (int)ViewportSize.Y);
        TestCanyonGenerationUsesTypedColumnsAndSupportedProps();
        TestCanyonMapSizeInputContract();
        TestRenderProfileFormalSourceSpecs();
        await TestBoardSceneRendersGeneratedCanyon();
        RequestTestExit(_test.Finish("Battle board regression"));
    }

    private void TestCanyonGenerationUsesTypedColumnsAndSupportedProps()
    {
        using GodotProjectionLease<GDictionary> firstLease = BuildLayout(
            "canyon",
            TestSeed
        );
        using GodotProjectionLease<GDictionary> secondLease = BuildLayout(
            "canyon",
            TestSeed
        );
        GDictionary first = firstLease.Value;
        GDictionary second = secondLease.Value;
        _test.Eq(
            string.Join("\n", CaptureLayoutSignature(first)),
            string.Join("\n", CaptureLayoutSignature(second)),
            "canyon terrain generation should remain deterministic for a fixed seed."
        );
        _test.Eq(DictStringName(first, "terrain_profile_id"), new StringName("canyon"), "生成结果应回写正式 terrain_profile_id。");
        _test.True(DictVector2I(first, "map_size") == TestMapSize, "测试上下文应能固定 canyon battle_map_size。");
        _test.True(DictDict(first, "cells").Count > 0, "canyon 生成结果应包含 cells。");
        _test.True(DictDict(first, "cell_columns").Count > 0, "canyon 生成结果应包含 typed cell_columns。");
        _test.True(CountProp(first, "objective_marker") >= 1, "canyon 应生成 objective marker prop。");
        _test.True(CountProp(first, "tent") >= 2, "canyon 应保留双方 tent prop。");
        _test.True(CountProp(first, "torch") >= 2, "canyon 应保留 torch prop。");
        AssertColumnsMatchSurfaceCells(first);
        AssertSpawnCoordsAvoidWater(first, "canyon");
        AssertLayoutUsesSupportedProps(first);
    }

    private void TestCanyonMapSizeInputContract()
    {
        using var generator = new BattleTerrainGenerator();
        EncounterAnchorData encounterAnchor = BuildEncounterAnchor(
            "battle_board_map_size_contract",
            "battle board map size contract",
            "canyon"
        );
        using GDictionary missingSizeContext = new()
        {
            ["world_coord"] = TestWorldCoord,
            ["world_seed"] = TestSeed,
            ["battle_terrain_profile"] = "canyon",
        };
        using GodotProjectionLease<GDictionary> missingSizeLease = generator.GenerateLease(
            encounterAnchor,
            TestSeed,
            missingSizeContext,
            LifetimeDomain.Request
        );
        _test.Eq(
            DictVector2I(missingSizeLease.Value, "map_size"),
            TestMapSize,
            "未提供 battle_map_size 时应选择 canyon 正式缺省尺寸。"
        );

        using GDictionary explicitSizeContext = new()
        {
            ["world_coord"] = TestWorldCoord,
            ["world_seed"] = TestSeed,
            ["battle_terrain_profile"] = "canyon",
            ["battle_map_size"] = ExplicitMapSize,
        };
        using GodotProjectionLease<GDictionary> explicitSizeLease = generator.GenerateLease(
            encounterAnchor,
            TestSeed,
            explicitSizeContext,
            LifetimeDomain.Request
        );
        _test.Eq(
            DictVector2I(explicitSizeLease.Value, "map_size"),
            ExplicitMapSize,
            "显式正 battle_map_size 应原样进入 canyon layout。"
        );

        bool rejectedZeroSize = false;
        try
        {
            using GDictionary zeroSizeContext = new()
            {
                ["world_coord"] = TestWorldCoord,
                ["world_seed"] = TestSeed,
                ["battle_terrain_profile"] = "canyon",
                ["battle_map_size"] = Vector2I.Zero,
            };
            using GodotProjectionLease<GDictionary> _ = generator.GenerateLease(
                encounterAnchor,
                TestSeed,
                zeroSizeContext,
                LifetimeDomain.Request
            );
        }
        catch (ArgumentOutOfRangeException exception)
        {
            rejectedZeroSize = exception.ParamName == "battle_map_size";
        }
        _test.True(
            rejectedZeroSize,
            "显式零 battle_map_size 必须被拒绝，不能冒充缺省尺寸。"
        );
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
                new StringName("unknown_fixture_profile"),
            }
        )
        {
            BattleBoardRenderProfile profile = BattleBoardRenderProfile.ForTerrainProfileId(terrainId);
            _test.True(profile != null, $"terrain profile 应解析出 render profile：{terrainId}");
            if (profile == null)
                continue;
            _test.Eq(profile.render_profile_id, BattleBoardRenderProfile.RENDER_PROFILE_CANYON_ISO64(), "battle board 当前应统一使用 canyon iso64 render profile。");
            _test.Eq(profile.asset_dir, BattleBoardRenderProfile.DEFAULT_ASSET_DIR(), "render profile 应指向正式 canyon 资产目录。");
            _test.Eq(profile.visual_height_step, BattleBoardRenderProfile.DEFAULT_VISUAL_HEIGHT_STEP(), "render profile 应持有正式视觉高度步长。");
            _test.Eq(profile.board_tile_size, BattleBoardRenderProfile.DEFAULT_BOARD_TILE_SIZE(), "render profile 应显式持有棋盘 tile size。");
            _test.True(profile.GetSourceSpecs().Count > 0, "render profile 应以 formal source spec 表驱动 TileSet 注册。");
        }

        BattleBoardRenderProfile canyon = BattleBoardRenderProfile.ForTerrainProfileId("canyon");
        foreach (BattleBoardTileSourceSpec sourceSpec in canyon.GetSourceSpecs())
        {
            _test.True(sourceSpec.Key != "", "source spec 应包含 key。");
            _test.True(sourceSpec.Files != null, "source spec 应包含 files。");
            _test.True(sourceSpec.LayerRole != "", "source spec 应包含 layer_role。");
            _test.True(sourceSpec.AtlasRegionSize != Vector2I.Zero, "source spec 应包含 atlas_region_size。");
            foreach (string fileName in sourceSpec.Files)
            {
                string path = $"{canyon.asset_dir}/{fileName}";
                _test.True(FileAccess.FileExists(path), $"source spec 贴图必须存在：{path}");
            }
        }
    }

    private async Task TestBoardSceneRendersGeneratedCanyon()
    {
        using GodotProjectionLease<GDictionary> layoutLease = BuildLayout(
            "canyon",
            TestSeed
        );
        GDictionary layout = layoutLease.Value;
        BattleState state = BuildState(layout);
        BattleBoard2D board = BattleBoardScene.Instantiate<BattleBoard2D>();
        Root.AddChild(board);
        await ProcessFrames(1);

        Vector2I selectedCoord = DictVector2I(layout, "player_coord");
        board.SetViewportSize(ViewportSize);
        board.Configure(
            state,
            selectedCoord,
            new GVector2IArray(),
            CollectAllCoords(state),
            "single_unit",
            1,
            1,
            new Dictionary<Vector2I, string>()
        );

        bool ready = await WaitForBoardRenderReady(board);
        _test.True(ready, "BattleBoard2D Configure 后应在有限帧内完成渲染内容。");
        _test.True(HasAnyUsedLayer(board, "TopH", 0, 8), "BattleBoard2D 应渲染至少一个 top tile。");
        _test.True(board.prop_layer != null && board.prop_layer.GetChildCount() > 0, "BattleBoard2D 应渲染地形 prop。");
        _test.Eq(board.unit_layer?.GetChildCount() ?? 0, 2, "BattleBoard2D 应渲染测试双方单位 token。");
        _test.Eq(board._render_profile?.terrain_profile_id ?? new StringName(""), new StringName("canyon"), "BattleBoard2D 应按 battle state 绑定 terrain render profile。");

        board.QueueFree();
        await ProcessFrames(1);
    }

    private GodotProjectionLease<GDictionary> BuildLayout(string profileId, int seed)
    {
        using var generator = new BattleTerrainGenerator();
        using GDictionary context = new()
        {
            ["world_coord"] = TestWorldCoord,
            ["world_seed"] = seed,
            ["battle_terrain_profile"] = profileId,
            ["battle_map_size"] = TestMapSize,
        };
        return generator.GenerateLease(
            BuildEncounterAnchor("battle_board_regression", "battle board regression", profileId),
            seed,
            context,
            LifetimeDomain.Request
        );
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
            terrain_profile_id = DictStringName(layout, "terrain_profile_id", "default"),
            ally_unit_ids = new GStringNameArray(),
            enemy_unit_ids = new GStringNameArray(),
        };
        using (GDictionary cells = DictDict(layout, "cells"))
            state.SetCellsFromDictionary(cells, duplicateCells: true);
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
            if (layer != null && layer.GetUsedCells().Count > 0)
                return true;
        }
        return false;
    }

    private void AssertColumnsMatchSurfaceCells(GDictionary layout)
    {
        GDictionary cells = DictDict(layout, "cells");
        GDictionary columns = DictDict(layout, "cell_columns");
        bool foundStackedColumn = false;
        foreach (Variant coordValue in cells.Keys)
        {
            if (!BattleCellState.TryReadCellPayload(cells[coordValue], out BattleCellState surfaceCell)
                || surfaceCell == null)
                continue;
            _test.True(columns.ContainsKey(coordValue), $"cell_columns 应包含 surface cell 坐标：{coordValue}");
            if (!columns.ContainsKey(coordValue) || columns[coordValue].VariantType != Variant.Type.Array)
                continue;
            GArray column = columns[coordValue].AsGodotArray();
            _test.True(column.Count >= surfaceCell.current_height + 1, $"cell_columns 应按地表高度生成真实堆叠列：{coordValue}");
            if (column.Count > 1)
                foundStackedColumn = true;
        }
        _test.True(foundStackedColumn, "canyon 地图应至少包含一个多层堆叠列。");
    }

    private void AssertSpawnCoordsAvoidWater(GDictionary layout, string label)
    {
        foreach (Vector2I coord in CollectSpawnCoords(layout))
        {
            BattleCellState.TryReadCellPayload(DictDict(layout, "cells")[coord], out BattleCellState cell);
            _test.True(cell != null, $"{label} spawn 应指向有效 battle cell：{coord}");
            if (cell != null)
                _test.False(BattleTerrainRules.IsWaterTerrain(cell.base_terrain), $"{label} spawn 不应落在水域：{coord}");
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
            foreach (Variant coordValue in DictArray(layout, fieldName))
            {
                if (coordValue.VariantType == Variant.Type.Vector2I)
                    result.Add(coordValue.AsVector2I());
            }
        }
        return result;
    }

    private void AssertLayoutUsesSupportedProps(GDictionary layout)
    {
        foreach (Variant cellValue in DictDict(layout, "cells").Values)
        {
            if (!BattleCellState.TryReadCellPayload(cellValue, out BattleCellState cell) || cell == null)
                continue;
            foreach (StringName propId in cell.prop_ids)
                _test.True(BattleBoardPropCatalog.IsSupported(propId), $"显式 prop_id 必须来自正式 prop catalog：{propId}");
        }
    }

    private static int CountProp(GDictionary layout, StringName propId)
    {
        int count = 0;
        foreach (Variant cellValue in DictDict(layout, "cells").Values)
        {
            if (!BattleCellState.TryReadCellPayload(cellValue, out BattleCellState cell) || cell == null)
                continue;
            foreach (StringName cellPropId in cell.prop_ids)
            {
                if (cellPropId == propId)
                    count++;
            }
        }
        return count;
    }

    private static List<string> CaptureLayoutSignature(GDictionary layout)
    {
        var lines = new List<string>();
        GDictionary cells = DictDict(layout, "cells");
        var coords = new List<Vector2I>();
        foreach (Variant coordValue in cells.Keys)
        {
            if (coordValue.VariantType == Variant.Type.Vector2I)
                coords.Add(coordValue.AsVector2I());
        }
        coords.Sort((left, right) => left.Y == right.Y ? left.X.CompareTo(right.X) : left.Y.CompareTo(right.Y));
        foreach (Vector2I coord in coords)
        {
            if (!BattleCellState.TryReadCellPayload(cells[coord], out BattleCellState cell) || cell == null)
                continue;
            lines.Add(
                $"{coord.X},{coord.Y}|{cell.base_terrain}|{cell.current_height}|{string.Join(",", StringifyProps(cell.prop_ids))}"
            );
        }
        return lines;
    }

    private static List<string> StringifyProps(IEnumerable<StringName> propIds)
    {
        var values = new List<string>();
        foreach (StringName propId in propIds)
            values.Add(propId.ToString());
        values.Sort();
        return values;
    }

    private static GDictionary DictDict(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Dictionary
            ? dict[key].AsGodotDictionary()
            : new GDictionary();

    private static GArray DictArray(GDictionary dict, Variant key) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Array
            ? dict[key].AsGodotArray()
            : new GArray();

    private static Vector2I DictVector2I(GDictionary dict, Variant key, Vector2I fallback = default) =>
        dict != null && dict.ContainsKey(key) && dict[key].VariantType == Variant.Type.Vector2I
            ? dict[key].AsVector2I()
            : fallback;

    private static StringName DictStringName(GDictionary dict, Variant key, StringName fallback = default)
    {
        if (dict == null || !dict.ContainsKey(key))
            return fallback;
        Variant value = dict[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback,
        };
    }
}
