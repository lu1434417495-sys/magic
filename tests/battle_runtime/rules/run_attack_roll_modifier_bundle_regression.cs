using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_attack_roll_modifier_bundle_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        Run();
        if (_failures.Count == 0)
        {
            GD.Print("Attack roll modifier bundle regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Attack roll modifier bundle regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void Run()
    {
        TestTypesArePlainCSharp();
        TestPositiveAddStack();
        TestPenaltyMaxAndMinStack();
        TestMixedSignStackHardFailsToEmpty();
        TestBundleUsesTypedBreakdownList();
        TestExactSchemaRoundTrip();
    }

    private void TestTypesArePlainCSharp()
    {
        AssertPlainCSharpType(typeof(BattleAttackCheckPolicyContext));
        AssertPlainCSharpType(typeof(BattleAttackRollModifierBundle));
        AssertPlainCSharpType(typeof(BattleAttackRollModifierSpec));
        AssertPlainCSharpType(typeof(BattleAttackCheckPolicyService));
        AssertPublicApiDoesNotExposeGodotPayload(
            typeof(BattleAttackRollModifierBundle),
            "BattleAttackRollModifierBundle"
        );
        AssertPublicApiDoesNotExposeGodotPayload(
            typeof(BattleAttackRollModifierSpec),
            "BattleAttackRollModifierSpec"
        );
    }

    private void TestPositiveAddStack()
    {
        var service = new BattleAttackCheckPolicyService();
        var specs = new List<BattleAttackRollModifierSpec>
        {
            BuildSpec("height", 1, "height_bonus", "add"),
            BuildSpec("height", 2, "height_bonus", "add"),
        };

        List<BattleAttackRollModifierSpec> resolved = service.ResolveStackedSpecs(specs);
        AssertEq(resolved.Count, 1, "add stack 应合并为一条 post-stack spec。");
        AssertEq(resolved.Count > 0 ? resolved[0].modifier_delta : 0, 3, "add stack 应同号求和。");
    }

    private void TestPenaltyMaxAndMinStack()
    {
        var service = new BattleAttackCheckPolicyService();
        var maxSpecs = new List<BattleAttackRollModifierSpec>
        {
            BuildSpec("dust_a", -1, "dust_penalty", "max"),
            BuildSpec("dust_b", -2, "dust_penalty", "max"),
        };
        List<BattleAttackRollModifierSpec> maxResolved = service.ResolveStackedSpecs(maxSpecs);
        AssertEq(
            maxResolved.Count > 0 ? maxResolved[0].modifier_delta : 0,
            -2,
            "penalty max 应取绝对值最大的惩罚。"
        );

        var minSpecs = new List<BattleAttackRollModifierSpec>
        {
            BuildSpec("dust_a", -1, "dust_penalty", "min"),
            BuildSpec("dust_b", -2, "dust_penalty", "min"),
        };
        List<BattleAttackRollModifierSpec> minResolved = service.ResolveStackedSpecs(minSpecs);
        AssertEq(
            minResolved.Count > 0 ? minResolved[0].modifier_delta : 0,
            -1,
            "penalty min 应取最接近 0 的惩罚。"
        );
    }

    private void TestMixedSignStackHardFailsToEmpty()
    {
        var service = new BattleAttackCheckPolicyService();
        var specs = new List<BattleAttackRollModifierSpec>
        {
            BuildSpec("bonus", 1, "mixed", "max"),
            BuildSpec("penalty", -1, "mixed", "max"),
        };

        List<BattleAttackRollModifierSpec> resolved = service.ResolveStackedSpecs(specs);
        AssertEq(resolved.Count, 0, "同一 stack_key 混合 bonus/penalty 应 hard fail，不产生 post-stack breakdown。");
    }

    private void TestBundleUsesTypedBreakdownList()
    {
        var bundle = new BattleAttackRollModifierBundle();
        bundle.AddSpec(BuildSpec("dust", -2, "dust_attack_roll_penalty", "max"));

        AssertEq(bundle.TotalBonus, 0, "penalty spec 不应增加 TotalBonus。");
        AssertEq(bundle.TotalPenalty, 2, "penalty spec 应累加 TotalPenalty。");
        AssertEq(bundle.GetEffectiveModifierDelta(), -2, "effective modifier 应为 bonus - penalty。");
        AssertEq(bundle.Breakdown.Count, 1, "bundle breakdown 应暴露 typed IReadOnlyList。");
        AssertTrue(!bundle.IsEmpty(), "包含 spec 的 bundle 不应为空。");

        Godot.Collections.Dictionary payload = bundle.ToDictionary();
        AssertEq(payload["effective_modifier_delta"].AsInt32(), -2, "payload 投影应保留 effective modifier。");

        var preview = new AttackPreviewData();
        preview.SetAttackRollModifierBreakdown(bundle.Breakdown);
        AssertEq(preview.AttackRollModifierBreakdownTyped.Count, 1, "hit preview 应内部保存 typed modifier breakdown。");
        Godot.Collections.Array<Godot.Collections.Dictionary> projected =
            preview.AttackRollModifierBreakdown;
        projected.Clear();
        AssertEq(
            preview.AttackRollModifierBreakdownTyped.Count,
            1,
            "修改投影 Array 不应影响 typed breakdown 状态。"
        );
        AssertEq(
            preview.BuildAttackRollModifierBreakdownPayload().Count,
            1,
            "projection helper 应按 typed breakdown 生成 Godot payload。"
        );

        var payloadPreview = new AttackPreviewData();
        payloadPreview.SetAttackRollModifierBreakdownPayload(bundle.BuildBreakdownPayload());
        AssertEq(
            payloadPreview.AttackRollModifierBreakdownTyped.Count,
            1,
            "Godot payload 只应在边界解析为 typed breakdown。"
        );
    }

    private void TestExactSchemaRoundTrip()
    {
        Godot.Collections.Dictionary payload = BuildSpec(
            "dust",
            -2,
            "dust_attack_roll_penalty",
            "max"
        ).ToDictionary();
        payload.Remove("effective_modifier_delta");

        BattleAttackRollModifierSpec restored = BattleAttackRollModifierSpec.FromDictionary(payload);
        AssertTrue(restored != null, "exact schema payload 应恢复为 typed modifier spec。");
        AssertEq(restored?.modifier_delta ?? 0, -2, "typed modifier spec roundtrip 应保留 modifier_delta。");

        payload["unexpected"] = true;
        AssertTrue(BattleAttackRollModifierSpec.FromDictionary(payload) == null, "exact schema 应拒绝额外字段。");

        Godot.Collections.Dictionary invalidTargetFilterPayload = BuildSpec(
            "dust",
            -2,
            "dust_attack_roll_penalty",
            "max"
        ).ToDictionary();
        invalidTargetFilterPayload.Remove("effective_modifier_delta");
        invalidTargetFilterPayload["target_team_filter"] = "hostile";
        AssertTrue(
            BattleAttackRollModifierSpec.FromDictionary(invalidTargetFilterPayload) == null,
            "modifier spec 不应接受 hostile 作为 target_team_filter。"
        );
        AssertTrue(
            BattleAttackRollModifierSpec.FromPartialDictionary(
                new Godot.Collections.Dictionary { ["target_team_filter"] = "friendly" }
            ) == null,
            "partial modifier spec 不应接受 friendly 作为 target_team_filter。"
        );
    }

    private BattleAttackRollModifierSpec BuildSpec(
        StringName sourceId,
        int delta,
        StringName stackKey,
        StringName stackMode
    )
    {
        return new BattleAttackRollModifierSpec
        {
            source_domain = "terrain",
            source_id = sourceId,
            source_instance_id = sourceId.ToString(),
            label = sourceId.ToString(),
            modifier_delta = delta,
            stack_key = stackKey,
            stack_mode = stackMode,
            roll_kind_filter = "spell_attack",
            endpoint_mode = "either",
            target_team_filter = "any",
            footprint_mode = "any_cell",
            applies_to = "attack_roll",
        };
    }

    private void AssertPlainCSharpType(Type type)
    {
        AssertTrue(!typeof(GodotObject).IsAssignableFrom(type), $"{type.Name} 不应继承 GodotObject/RefCounted。");
        AssertTrue(
            type.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length == 0,
            $"{type.Name} 不应保留 GlobalClassAttribute。"
        );
    }

    private void AssertPublicApiDoesNotExposeGodotPayload(Type type, string label)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertTrue(
                !IsGodotPayloadType(method.ReturnType),
                $"{label}.{method.Name}() 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsGodotPayloadType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }

        foreach (PropertyInfo property in type.GetProperties(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertTrue(
                !IsGodotPayloadType(property.PropertyType),
                $"{label}.{property.Name} 不应公开 Godot Dictionary/Array/Variant 属性。"
            );
        }

        foreach (FieldInfo field in type.GetFields(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertTrue(
                !IsGodotPayloadType(field.FieldType),
                $"{label}.{field.Name} 不应公开 Godot Dictionary/Array/Variant 字段。"
            );
        }
    }

    private static bool IsGodotPayloadType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            type = type.GetElementType() ?? type;
        }
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        if (
            typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal)
        )
        {
            return true;
        }
        if (type.IsGenericType)
        {
            foreach (Type genericArgument in type.GetGenericArguments())
            {
                if (IsGodotPayloadType(genericArgument))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
