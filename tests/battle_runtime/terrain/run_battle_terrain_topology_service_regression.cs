using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_terrain_topology_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestServiceIsPlainTypedCSharp();
            TestLowerBankReclassifiesWaterAsFlowing();
            TestEnclosedNearBankWaterReclassifiesAsShallow();
            TestGroundEffectAppliesTypedTopologyChanges();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle terrain topology service regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle terrain topology service regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceIsPlainTypedCSharp()
    {
        Type serviceType = typeof(BattleTerrainTopologyService);
        AssertTrue(serviceType.IsSealed, "BattleTerrainTopologyService 应为 sealed plain C# service。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleTerrainTopologyService 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(serviceType, "GlobalClassAttribute"),
            "BattleTerrainTopologyService 不应注册 GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("reclassify_water_terrain_near_coords") == null
                && serviceType.GetMethod("reclassify_all_water_terrain") == null,
            "BattleTerrainTopologyService 不应保留 GDScript-style reclassify_* API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(serviceType);

        Type changeType = typeof(BattleTerrainTopologyChange);
        AssertTrue(changeType.IsValueType, "BattleTerrainTopologyChange 应为 C# value type。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(changeType),
            "BattleTerrainTopologyChange 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(changeType, "GlobalClassAttribute"),
            "BattleTerrainTopologyChange 不应注册 GlobalClass。"
        );
        AssertEq(
            changeType.GetFields(BindingFlags.Public | BindingFlags.Instance).Length,
            0,
            "BattleTerrainTopologyChange 应通过 typed properties 暴露结果，不要用 public mutable fields。"
        );
    }

    private void TestLowerBankReclassifiesWaterAsFlowing()
    {
        BattleState state = BuildFlatState(new Vector2I(3, 3), 4);
        SetCell(state, new Vector2I(1, 1), BattleCellState.TERRAIN_DEEP_WATER(), 3);
        SetCell(state, new Vector2I(0, 1), BattleCellState.TERRAIN_LAND(), 2);

        var service = new BattleTerrainTopologyService();
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            service.ReclassifyWaterTerrainNearCoords(state, new[] { new Vector2I(1, 1) });
        BattleTerrainTopologyChange change = FindChange(changes, new Vector2I(1, 1));

        AssertEq(
            change.BeforeTerrain,
            BattleCellState.TERRAIN_DEEP_WATER(),
            "流水重分类应保留 before terrain。"
        );
        AssertEq(
            change.AfterTerrain,
            BattleCellState.TERRAIN_FLOWING_WATER(),
            "有低岸出口的水域应重分类为 flowing water。"
        );
        AssertEq(
            change.AfterFlowDirection,
            Vector2I.Left,
            "有低岸出口的水域应记录指向低岸的 flow direction。"
        );

        BattleCellState centerCell = GetCell(state, new Vector2I(1, 1));
        AssertEq(
            centerCell.base_terrain,
            BattleCellState.TERRAIN_DEEP_WATER(),
            "拓扑服务只返回 typed change，不应直接改写 cell terrain。"
        );
        AssertEq(
            centerCell.flow_direction,
            Vector2I.Zero,
            "拓扑服务只返回 typed change，不应直接改写 flow direction。"
        );
    }

    private void TestEnclosedNearBankWaterReclassifiesAsShallow()
    {
        BattleState state = BuildFlatState(new Vector2I(3, 3), 4);
        SetCell(state, new Vector2I(1, 1), BattleCellState.TERRAIN_DEEP_WATER(), 3);

        var service = new BattleTerrainTopologyService();
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            service.ReclassifyAllWaterTerrain(state);
        BattleTerrainTopologyChange change = FindChange(changes, new Vector2I(1, 1));

        AssertEq(
            change.AfterTerrain,
            BattleCellState.TERRAIN_SHALLOW_WATER(),
            "封闭且贴近岸坡的水域应重分类为 shallow water。"
        );
        AssertEq(
            change.AfterFlowDirection,
            Vector2I.Zero,
            "shallow water 不应记录 flow direction。"
        );
    }

    private void TestGroundEffectAppliesTypedTopologyChanges()
    {
        var runtime = new BattleRuntimeModule();
        runtime._state = BuildFlatState(new Vector2I(3, 3), 4);
        runtime._ground_effect_service.setup(runtime);
        SetCell(runtime._state, new Vector2I(1, 1), BattleCellState.TERRAIN_DEEP_WATER(), 3);
        SetCell(runtime._state, new Vector2I(0, 1), BattleCellState.TERRAIN_LAND(), 2);

        var batch = new BattleEventBatch();
        bool applied = runtime._ground_effect_service._reconcile_water_topology(
            new Godot.Collections.Array { new Vector2I(1, 1) },
            batch
        );

        BattleCellState centerCell = GetCell(runtime._state, new Vector2I(1, 1));
        AssertTrue(applied, "ground effect 水域拓扑调和应应用 typed topology change。");
        AssertEq(
            centerCell.base_terrain,
            BattleCellState.TERRAIN_FLOWING_WATER(),
            "ground effect 应把 typed after terrain 应用到 cell。"
        );
        AssertEq(
            centerCell.flow_direction,
            Vector2I.Left,
            "ground effect 应把 typed after flow direction 应用到 cell。"
        );
        AssertTrue(
            batch.changed_coords.Contains(new Vector2I(1, 1)),
            "ground effect 应把应用过的 topology coord 写入 changed coords。"
        );
    }

    private static BattleState BuildFlatState(Vector2I mapSize, int height)
    {
        var state = new BattleState
        {
            battle_id = "battle_terrain_topology_service_regression",
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                SetCell(state, new Vector2I(x, y), BattleCellState.TERRAIN_LAND(), height);
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static void SetCell(
        BattleState state,
        Vector2I coord,
        StringName terrain,
        int height
    )
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_terrain = terrain,
            base_height = height,
            height_offset = 0,
        };
        cell.recalculate_runtime_values();
        state.cells[coord] = cell;
        state.cell_columns[coord] = BattleCellState.build_stacked_cells_from_surface_cell(cell);
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord)
    {
        return state.TryGetCellTyped(coord, out BattleCellState cell) ? cell : null;
    }

    private BattleTerrainTopologyChange FindChange(
        IReadOnlyList<BattleTerrainTopologyChange> changes,
        Vector2I coord
    )
    {
        foreach (BattleTerrainTopologyChange change in changes)
        {
            if (change.Coord == coord)
            {
                return change;
            }
        }
        _failures.Add($"未找到 terrain topology change: {coord}.");
        return default;
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type)
    {
        foreach (
            MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
            )
        )
        {
            AssertFalse(
                IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{type.Name}.{method.Name} 不应返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant)
        || IsGodotCollectionType(type);

    private static bool IsGodotCollectionType(Type type)
    {
        if (type == null || type.IsGenericParameter)
        {
            return false;
        }
        if (type.Namespace == "Godot.Collections")
        {
            return type.Name.StartsWith("Dictionary", StringComparison.Ordinal)
                || type.Name.StartsWith("Array", StringComparison.Ordinal);
        }
        if (!type.IsGenericType)
        {
            return false;
        }
        foreach (Type genericArgument in type.GetGenericArguments())
        {
            if (IsGodotCollectionType(genericArgument))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasAttributeNamed(Type type, string attributeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeName)
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
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} Expected {expected}, got {actual}.");
        }
    }
}
