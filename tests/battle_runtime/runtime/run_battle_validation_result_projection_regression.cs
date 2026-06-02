using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_validation_result_projection_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestUnitSkillValidationProjectsTypedLists();
        TestGroundSkillValidationParsesAndProjectsStringKeyPayload();
        TestTargetCollectionSortsAndProjectsCoords();
        TestValidationResultPublicApiStaysTyped();

        if (_failures.Count == 0)
        {
            GD.Print("Battle validation result projection regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle validation result projection regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestUnitSkillValidationProjectsTypedLists()
    {
        var targetUnit = new BattleUnitState { unit_id = "target_1" };
        BattleUnitSkillValidationResult result = BattleUnitSkillValidationResult.AllowedResult(
            new[] { new StringName("target_1") },
            new[] { targetUnit },
            new[] { new StringName("chain_1") },
            new[] { new Vector2I(2, 3) },
            "ok"
        );

        Godot.Collections.Dictionary payload = result.ToDictionary();

        AssertTrue(payload["allowed"].AsBool(), "单位技能 validation 应投影 allowed。");
        AssertEq(payload["message"].AsString(), "ok", "单位技能 validation 应投影 message。");
        AssertEq(
            payload["target_unit_ids"].AsGodotArray<StringName>()[0],
            new StringName("target_1"),
            "单位技能 validation 应投影目标 id。"
        );
        AssertEq(
            payload["target_units"].AsGodotArray()[0].As<BattleUnitState>(),
            targetUnit,
            "单位技能 validation 应投影目标 unit。"
        );
        AssertEq(
            payload["random_chain_candidate_unit_ids"].AsGodotArray<StringName>()[0],
            new StringName("chain_1"),
            "单位技能 validation 应投影随机连锁候选。"
        );
        AssertEq(
            payload["preview_coords"].AsGodotArray<Vector2I>()[0],
            new Vector2I(2, 3),
            "单位技能 validation 应投影 preview coord。"
        );
    }

    private void TestGroundSkillValidationParsesAndProjectsStringKeyPayload()
    {
        var source = new Godot.Collections.Dictionary
        {
            ["allowed"] = true,
            ["message"] = "cast",
            ["target_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
            },
            ["preview_coords"] = new Godot.Collections.Array<Vector2I>
            {
                new(4, 5),
                new(5, 5),
            },
            ["direction"] = Vector2I.Right,
            ["distance"] = 2,
            ["resolved_anchor_coord"] = new Vector2I(6, 5),
        };

        BattleGroundSkillValidationResult result = BattleGroundSkillValidationResult.FromDictionary(
            source
        );
        Godot.Collections.Dictionary payload = result.ToDictionary();

        AssertTrue(result.Allowed, "地面技能 validation 应从 string-key payload 解析 allowed。");
        AssertEq(result.Message, "cast", "地面技能 validation 应解析 message。");
        AssertEq(result.TargetCoords[0], new Vector2I(4, 5), "地面技能 validation 应解析目标格。");
        AssertEq(result.PreviewCoords[1], new Vector2I(5, 5), "地面技能 validation 应解析 preview 格。");
        AssertEq(result.Direction, Vector2I.Right, "地面技能 validation 应解析方向。");
        AssertEq(result.Distance, 2, "地面技能 validation 应解析距离。");
        AssertEq(
            payload["resolved_anchor_coord"].AsVector2I(),
            new Vector2I(6, 5),
            "地面技能 validation 应投影 resolved anchor。"
        );
    }

    private void TestTargetCollectionSortsAndProjectsCoords()
    {
        BattleTargetCollectionResult result = BattleTargetCollectionResult.HandledResult(
            new[] { new Vector2I(3, 2), new Vector2I(1, 1), new Vector2I(2, 1) }
        );

        Godot.Collections.Array<Vector2I> coords = result.ToTargetCoordsArray();
        Godot.Collections.Dictionary payload = result.ToDictionary();

        AssertTrue(result.Handled, "目标收集结果应保留 handled。");
        AssertEq(coords[0], new Vector2I(1, 1), "目标收集结果应按 y/x 排序。");
        AssertEq(coords[1], new Vector2I(2, 1), "目标收集结果排序应稳定按 x。");
        AssertEq(
            payload["target_coords"].AsGodotArray<Vector2I>()[2],
            new Vector2I(3, 2),
            "目标收集结果应投影排序后的 coords。"
        );
    }

    private void TestValidationResultPublicApiStaysTyped()
    {
        AssertPublicApiDoesNotExposeGodotCollections(typeof(BattleUnitSkillValidationResult));
        AssertPublicApiDoesNotExposeGodotCollections(typeof(BattleGroundSkillValidationResult));
        AssertPublicApiDoesNotExposeGodotCollections(typeof(BattleTargetCollectionResult));
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsForbiddenPublicApiType(method.ReturnType),
                $"{type.Name}.{method.Name} 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsForbiddenPublicApiType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsForbiddenPublicApiType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }

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

    private void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }
}
