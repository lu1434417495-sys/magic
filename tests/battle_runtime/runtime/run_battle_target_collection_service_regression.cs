using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_target_collection_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGroundAreaCollectionUsesTypedInputs();
            TestSelfAndUnitTargetCollectionUseTypedUnits();
            Quit(_test.Finish("Battle target collection service regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(1);
        }
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

        _test.True(result.Handled, "ground area 目标收集应由 service 处理。");
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
        _test.True(selfResult.Handled, "self 目标收集应由 service 处理。");
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
        _test.True(unitResult.Handled, "unit 目标收集应由 service 处理。");
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
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
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
        unit.SetAnchorCoord(coord);
        return unit;
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
            _test.Fail($"{message} Expected count {expected.Count}, got {actual.Count}.");
            return;
        }
        for (int index = 0; index < expected.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _test.Fail($"{message} index={index} expected={expected[index]} actual={actual[index]}.");
                return;
            }
        }
    }

}
