using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_terrain_topology_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

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
            TestWaterHeightNormalizationStaysComponentLocal();
            TestLowerBankReclassifiesWaterAsFlowing();
            TestEnclosedNearBankWaterReclassifiesAsShallow();
            TestGroundEffectAppliesTypedTopologyChanges();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        return _test.Finish("Battle terrain topology service regression");
    }

    private void TestServiceIsPlainTypedCSharp()
    {
        Type serviceType = typeof(BattleTerrainTopologyService);
        _test.True(serviceType.IsSealed, "BattleTerrainTopologyService should be a sealed plain C# service.");
        _test.True(
            serviceType.GetMethod("reclassify_water_terrain_near_coords") == null
                && serviceType.GetMethod("reclassify_all_water_terrain") == null,
            "BattleTerrainTopologyService should not retain GDScript-style reclassify_* APIs."
        );
        AssertPublicApiDoesNotExposeGodotCollections(serviceType);

        Type changeType = typeof(BattleTerrainTopologyChange);
        _test.True(changeType.IsValueType, "BattleTerrainTopologyChange should be a C# value type.");
        _test.Eq(
            changeType.GetFields(BindingFlags.Public | BindingFlags.Instance).Length,
            0,
            "BattleTerrainTopologyChange should expose results through typed properties instead of public mutable fields."
        );
    }

    private void TestLowerBankReclassifiesWaterAsFlowing()
    {
        BattleState state = BuildFlatState(new Vector2I(3, 3), 4);
        SetCell(state, new Vector2I(1, 1), BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater), 3);
        SetCell(state, new Vector2I(0, 1), BattleTerrainRules.ToStringName(BattleTerrainKind.Land), 2);

        var service = new BattleTerrainTopologyService();
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            service.ReclassifyWaterTerrainNearCoords(state, new[] { new Vector2I(1, 1) });
        BattleTerrainTopologyChange change = FindChange(changes, new Vector2I(1, 1));

        _test.Eq(
            change.BeforeTerrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater),
            "Flowing water reclassification should preserve before terrain."
        );
        _test.Eq(
            change.AfterTerrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater),
            "Water with a lower-bank outlet should reclassify to flowing water."
        );
        _test.Eq(
            change.AfterFlowDirection,
            Vector2I.Left,
            "Water with a lower-bank outlet should record flow direction toward the lower bank."
        );

        BattleCellState centerCell = GetCell(state, new Vector2I(1, 1));
        _test.Eq(
            centerCell.base_terrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater),
            "Topology service should return typed changes without mutating cell terrain directly."
        );
        _test.Eq(
            centerCell.flow_direction,
            Vector2I.Zero,
            "Topology service should return typed changes without mutating flow direction directly."
        );
    }

    private void TestWaterHeightNormalizationStaysComponentLocal()
    {
        var generator = new BattleTerrainGenerator();
        var heights = new Godot.Collections.Dictionary
        {
            [new Vector2I(0, 0)] = 5,
            [new Vector2I(0, 1)] = 4,
            [new Vector2I(2, 0)] = 6,
            [new Vector2I(2, 1)] = 7,
        };
        var waterCells = new Godot.Collections.Dictionary
        {
            [new Vector2I(0, 0)] = true,
            [new Vector2I(0, 1)] = true,
            [new Vector2I(2, 0)] = true,
            [new Vector2I(2, 1)] = true,
        };
        MethodInfo normalizeMethod = typeof(BattleTerrainGenerator).GetMethod(
            "NormalizeWaterHeights",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        _test.True(
            normalizeMethod != null,
            "BattleTerrainGenerator should keep a nonpublic water-height normalization helper."
        );
        normalizeMethod?.Invoke(generator, new object[] { heights, waterCells });

        _test.Eq(
            (int)heights[new Vector2I(0, 0)],
            4,
            "Left-side water component should normalize to its own minimum water height."
        );
        _test.Eq(
            (int)heights[new Vector2I(0, 1)],
            4,
            "All cells in the left-side water component should share the same normalized height."
        );
        _test.Eq(
            (int)heights[new Vector2I(2, 0)],
            6,
            "Independent right-side water component should not be lowered by another lake."
        );
        _test.Eq(
            (int)heights[new Vector2I(2, 1)],
            6,
            "Each independent water component should normalize locally."
        );
    }

    private void TestEnclosedNearBankWaterReclassifiesAsShallow()
    {
        BattleState state = BuildFlatState(new Vector2I(3, 3), 4);
        SetCell(state, new Vector2I(1, 1), BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater), 3);

        var service = new BattleTerrainTopologyService();
        IReadOnlyList<BattleTerrainTopologyChange> changes =
            service.ReclassifyAllWaterTerrain(state);
        BattleTerrainTopologyChange change = FindChange(changes, new Vector2I(1, 1));

        _test.Eq(
            change.AfterTerrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.ShallowWater),
            "Enclosed near-bank water should reclassify to shallow water."
        );
        _test.Eq(
            change.AfterFlowDirection,
            Vector2I.Zero,
            "Shallow water should not record flow direction."
        );
    }

    private void TestGroundEffectAppliesTypedTopologyChanges()
    {
        var runtime = new BattleRuntimeModule();
        runtime._state = BuildFlatState(new Vector2I(3, 3), 4);
        runtime._ground_effect_service.Setup(runtime);
        SetCell(runtime._state, new Vector2I(1, 1), BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater), 3);
        SetCell(runtime._state, new Vector2I(0, 1), BattleTerrainRules.ToStringName(BattleTerrainKind.Land), 2);

        var batch = new BattleEventBatch();
        bool applied = runtime._ground_effect_service.ReconcileWaterTopology(
            new[] { new Vector2I(1, 1) },
            batch
        );

        BattleCellState centerCell = GetCell(runtime._state, new Vector2I(1, 1));
        _test.True(applied, "Ground effect water topology reconcile should apply typed topology changes.");
        _test.Eq(
            centerCell.base_terrain,
            BattleTerrainRules.ToStringName(BattleTerrainKind.FlowingWater),
            "Ground effect should apply typed after-terrain to the cell."
        );
        _test.Eq(
            centerCell.flow_direction,
            Vector2I.Left,
            "Ground effect should apply typed after-flow-direction to the cell."
        );
        _test.True(
            batch.changed_coords.Contains(new Vector2I(1, 1)),
            "Ground effect should record applied topology coords into changed coords."
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
                SetCell(state, new Vector2I(x, y), BattleTerrainRules.ToStringName(BattleTerrainKind.Land), height);
            }
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
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
        cell.RecalculateRuntimeValues();
        state.cells[coord] = cell;
        state.cell_columns[coord] = BattleCellState.BuildStackedCellsFromSurfaceCell(cell);
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
        _test.Fail($"Missing terrain topology change for coord: {coord}.");
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
            _test.False(
                IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{type.Name}.{method.Name} should not return Godot Dictionary/Array/Variant."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.False(
                    IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) should not accept Godot Dictionary/Array/Variant."
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
}
