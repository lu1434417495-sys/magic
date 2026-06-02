using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_move_path_result_projection_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestMovePathResultProjectsTypedPath();
        TestMovePathTreeProjectsTypedMaps();
        TestValidatedMoveExecutionResultProjectsTypedPath();
        TestMovePathDtosDoNotExposeGodotCollections();

        if (_failures.Count == 0)
        {
            GD.Print("Battle move path result projection regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle move path result projection regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMovePathResultProjectsTypedPath()
    {
        var result = new BattleMovePathResult
        {
            Allowed = true,
            Cost = 3,
            Path = new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(1, 1) },
            Message = "ok",
        };

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Array<Vector2I> pathPayload = payload["path"].AsGodotArray<Vector2I>();

        AssertTrue(
            result.Path.GetType() != typeof(Godot.Collections.Array<Vector2I>),
            "Path 真相源不应是 Godot Array。"
        );
        AssertTrue(payload["allowed"].AsBool(), "move path result 应投影 allowed。");
        AssertEq(payload["cost"].AsInt32(), 3, "move path result 应投影 cost。");
        AssertEq(payload["message"].AsString(), "ok", "move path result 应投影 message。");
        AssertEq(pathPayload[2], new Vector2I(1, 1), "move path result 应投影 path 坐标。");
    }

    private void TestMovePathTreeProjectsTypedMaps()
    {
        var result = new BattleMovePathTreeResult();
        result.Costs[new Vector2I(1, 2)] = 5;
        result.Previous[new Vector2I(1, 2)] = new Vector2I(1, 1);
        result.Steps[new Vector2I(1, 2)] = 2;

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Dictionary costs = payload["costs"].AsGodotDictionary();
        Godot.Collections.Dictionary previous = payload["previous"].AsGodotDictionary();
        Godot.Collections.Dictionary steps = payload["steps"].AsGodotDictionary();

        AssertEq(costs[new Vector2I(1, 2)].AsInt32(), 5, "path tree 应投影 cost map。");
        AssertEq(
            previous[new Vector2I(1, 2)].AsVector2I(),
            new Vector2I(1, 1),
            "path tree 应投影 previous map。"
        );
        AssertEq(steps[new Vector2I(1, 2)].AsInt32(), 2, "path tree 应投影 step map。");
    }

    private void TestValidatedMoveExecutionResultProjectsTypedPath()
    {
        var result = new BattleValidatedMoveExecutionResult
        {
            Executed = true,
            ReachedTarget = false,
            StoppedByBarrier = true,
        };
        result.ExecutedPath.Add(new Vector2I(0, 0));
        result.ExecutedPath.Add(new Vector2I(0, 1));

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Array<Vector2I> executedPath = payload["executed_path"]
            .AsGodotArray<Vector2I>();

        AssertTrue(
            result.ExecutedPath.GetType() != typeof(Godot.Collections.Array<Vector2I>),
            "ExecutedPath 真相源不应是 Godot Array。"
        );
        AssertTrue(payload["executed"].AsBool(), "validated move result 应投影 executed。");
        AssertTrue(
            !payload["reached_target"].AsBool(),
            "validated move result 应投影 reached target。"
        );
        AssertTrue(
            payload["stopped_by_barrier"].AsBool(),
            "validated move result 应投影 stopped by barrier。"
        );
        AssertEq(
            executedPath[1],
            new Vector2I(0, 1),
            "validated move result 应投影 executed path。"
        );
    }

    private void TestMovePathDtosDoNotExposeGodotCollections()
    {
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleMovePathResult),
            "BattleMovePathResult"
        );
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleMovePathTreeResult),
            "BattleMovePathTreeResult"
        );
        AssertPublicApiDoesNotExposeGodotCollections(
            typeof(BattleValidatedMoveExecutionResult),
            "BattleValidatedMoveExecutionResult"
        );
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string label)
    {
        foreach (
            MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
            )
        )
        {
            AssertTrue(
                !IsForbiddenGodotBoundaryType(method.ReturnType),
                $"{label}.{method.Name} 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsForbiddenGodotBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }
}
