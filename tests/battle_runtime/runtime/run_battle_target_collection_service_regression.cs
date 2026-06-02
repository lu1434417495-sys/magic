using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_target_collection_service_regression : SceneTree
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
            TestGroundAreaCollectionUsesTypedInputs();
            TestSelfAndUnitTargetCollectionUseTypedUnits();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle target collection service regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle target collection service regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestServiceIsPlainTypedCSharp()
    {
        Type serviceType = typeof(BattleTargetCollectionService);
        AssertTrue(serviceType.IsSealed, "BattleTargetCollectionService 应为 sealed plain C# service。");
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleTargetCollectionService 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(serviceType, "GlobalClassAttribute"),
            "BattleTargetCollectionService 不应注册 GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("collect_combat_profile_target_coords") == null,
            "BattleTargetCollectionService 不应保留 GDScript-style collect_combat_profile_target_coords wrapper。"
        );
        AssertTrue(
            typeof(BattleTargetCollectionResult).IsSealed,
            "BattleTargetCollectionResult 应为 sealed plain C# DTO。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(typeof(BattleTargetCollectionResult)),
            "BattleTargetCollectionResult 不应继承 GodotObject/RefCounted。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(typeof(BattleTargetCollectionResult));

        MethodInfo collectMethod = serviceType.GetMethod(
            nameof(BattleTargetCollectionService.CollectCombatProfileTargetCoords)
        );
        AssertTrue(collectMethod != null, "BattleTargetCollectionService 应保留 typed C# 入口。");
        if (collectMethod == null)
        {
            return;
        }
        AssertEq(
            collectMethod.ReturnType,
            typeof(BattleTargetCollectionResult),
            "目标收集服务应返回 typed result DTO。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(serviceType);
        AssertEq(
            collectMethod.GetParameters()[4].ParameterType,
            typeof(IEnumerable<Vector2I>),
            "目标坐标入参应使用 IEnumerable<Vector2I>，不要恢复 Godot Array 边界。"
        );
        AssertEq(
            collectMethod.GetParameters()[6].ParameterType,
            typeof(IEnumerable<BattleUnitState>),
            "目标单位入参应使用 IEnumerable<BattleUnitState>，不要恢复 Godot Array 边界。"
        );
    }

    private void TestGroundAreaCollectionUsesTypedInputs()
    {
        BattleState state = BuildFlatState(new Vector2I(5, 5));
        var service = new BattleTargetCollectionService();
        CombatSkillDef combatProfile = new()
        {
            target_mode = "ground",
            area_pattern = "diamond",
            area_value = 1,
        };

        BattleTargetCollectionResult result = service.CollectCombatProfileTargetCoords(
            state,
            new BattleGridService(),
            new Vector2I(2, 2),
            combatProfile,
            new[] { new Vector2I(2, 2) }
        );

        AssertTrue(result.Handled, "ground area 目标收集应由 service 处理。");
        AssertCoords(
            result.TargetCoords,
            new[]
            {
                new Vector2I(2, 1),
                new Vector2I(1, 2),
                new Vector2I(2, 2),
                new Vector2I(3, 2),
                new Vector2I(2, 3),
            },
            "diamond area 目标收集应按 BattleTargetCollectionResult 的 y/x 顺序投影。"
        );
    }

    private void TestSelfAndUnitTargetCollectionUseTypedUnits()
    {
        BattleState state = BuildFlatState(new Vector2I(5, 5));
        var gridService = new BattleGridService();
        var service = new BattleTargetCollectionService();
        BattleUnitState sourceUnit = BuildUnit("source", new Vector2I(1, 1));
        BattleUnitState targetUnit = BuildUnit("target", new Vector2I(3, 2));

        BattleTargetCollectionResult selfResult = service.CollectCombatProfileTargetCoords(
            state,
            gridService,
            sourceUnit.coord,
            new CombatSkillDef
            {
                target_mode = "unit",
                target_selection_mode = "self",
            },
            Array.Empty<Vector2I>(),
            sourceUnit,
            Array.Empty<BattleUnitState>()
        );
        AssertTrue(selfResult.Handled, "self 目标收集应由 service 处理。");
        AssertCoords(
            selfResult.TargetCoords,
            new[] { new Vector2I(1, 1) },
            "self 目标收集应返回 source unit footprint。"
        );

        BattleTargetCollectionResult unitResult = service.CollectCombatProfileTargetCoords(
            state,
            gridService,
            sourceUnit.coord,
            new CombatSkillDef { target_mode = "unit" },
            Array.Empty<Vector2I>(),
            sourceUnit,
            new[] { targetUnit }
        );
        AssertTrue(unitResult.Handled, "unit 目标收集应由 service 处理。");
        AssertCoords(
            unitResult.TargetCoords,
            new[] { new Vector2I(3, 2) },
            "unit 目标收集应消费 typed target unit list。"
        );
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "battle_target_collection_service_regression",
            map_size = mapSize,
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            current_hp = 20,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        return unit;
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

    private void AssertCoords(
        IReadOnlyList<Vector2I> actual,
        IReadOnlyList<Vector2I> expected,
        string message
    )
    {
        if (actual.Count != expected.Count)
        {
            _failures.Add($"{message} Expected count {expected.Count}, got {actual.Count}.");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _failures.Add($"{message} index={index} expected={expected[index]} actual={actual[index]}.");
                return;
            }
        }
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
