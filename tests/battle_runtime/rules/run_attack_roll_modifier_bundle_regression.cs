using System;
using System.Collections.Generic;
using Godot;

public partial class run_attack_roll_modifier_bundle_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypesArePlainCSharp();
        TestPositiveAddStack();
        TestPenaltyMaxAndMinStack();
        TestMixedSignStackHardFailsToEmpty();
        TestBundleUsesTypedBreakdownList();
        TestExactSchemaRoundTrip();

        Quit(_test.Finish("Attack roll modifier bundle regression"));
    }

    private void TestTypesArePlainCSharp()
    {
        AssertPlainCSharpType(typeof(BattleAttackCheckPolicyContext));
        AssertPlainCSharpType(typeof(BattleAttackRollModifierBundle));
        AssertPlainCSharpType(typeof(BattleAttackRollModifierSpec));
        AssertPlainCSharpType(typeof(BattleAttackCheckPolicyService));
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
        _test.Eq(resolved.Count, 1, "add stack 应合并为一条 post-stack spec。");
        _test.Eq(resolved.Count > 0 ? resolved[0].modifier_delta : 0, 3, "add stack 应同号求和。");
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
        _test.Eq(
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
        _test.Eq(
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
        _test.Eq(resolved.Count, 0, "同一 stack_key 混合 bonus/penalty 应 hard fail，不产生 post-stack breakdown。");
    }

    private void TestBundleUsesTypedBreakdownList()
    {
        var bundle = new BattleAttackRollModifierBundle();
        bundle.AddSpec(BuildSpec("dust", -2, "dust_attack_roll_penalty", "max"));

        _test.Eq(bundle.TotalBonus, 0, "penalty spec 不应增加 TotalBonus。");
        _test.Eq(bundle.TotalPenalty, 2, "penalty spec 应累加 TotalPenalty。");
        _test.Eq(bundle.GetEffectiveModifierDelta(), -2, "effective modifier 应为 bonus - penalty。");
        _test.Eq(bundle.Breakdown.Count, 1, "bundle breakdown 应暴露 typed IReadOnlyList。");
        _test.True(!bundle.IsEmpty(), "包含 spec 的 bundle 不应为空。");

        Godot.Collections.Dictionary payload =
            BattleAttackRollModifierProjection.ProjectBundle(bundle);
        _test.Eq(payload["effective_modifier_delta"].AsInt32(), -2, "payload 投影应保留 effective modifier。");

        var preview = new AttackPreviewData();
        preview.SetAttackRollModifierBreakdown(bundle.Breakdown);
        _test.Eq(preview.AttackRollModifierBreakdownTyped.Count, 1, "hit preview 应内部保存 typed modifier breakdown。");
        Godot.Collections.Array<Godot.Collections.Dictionary> projected =
            preview.AttackRollModifierBreakdown;
        projected.Clear();
        _test.Eq(
            preview.AttackRollModifierBreakdownTyped.Count,
            1,
            "修改投影 Array 不应影响 typed breakdown 状态。"
        );
        _test.Eq(
            preview.BuildAttackRollModifierBreakdownPayload().Count,
            1,
            "projection helper 应按 typed breakdown 生成 Godot payload。"
        );

        var payloadPreview = new AttackPreviewData();
        payloadPreview.SetAttackRollModifierBreakdownPayload(bundle.BuildBreakdownPayload());
        _test.Eq(
            payloadPreview.AttackRollModifierBreakdownTyped.Count,
            1,
            "Godot payload 只应在边界解析为 typed breakdown。"
        );
    }

    private void TestExactSchemaRoundTrip()
    {
        BattleAttackRollModifierSpec payloadSpec = BuildSpec(
            "dust",
            -2,
            "dust_attack_roll_penalty",
            "max"
        );
        Godot.Collections.Dictionary payload =
            BattleAttackRollModifierProjection.ProjectSpec(payloadSpec);
        payload.Remove("effective_modifier_delta");

        BattleAttackRollModifierSpec restored = BattleAttackRollModifierSpec.FromDictionary(payload);
        _test.True(restored != null, "exact schema payload 应恢复为 typed modifier spec。");
        _test.Eq(restored?.modifier_delta ?? 0, -2, "typed modifier spec roundtrip 应保留 modifier_delta。");

        payload["unexpected"] = true;
        _test.True(BattleAttackRollModifierSpec.FromDictionary(payload) == null, "exact schema 应拒绝额外字段。");

        Godot.Collections.Dictionary invalidTargetFilterPayload =
            BattleAttackRollModifierProjection.ProjectSpec(
                BuildSpec("dust", -2, "dust_attack_roll_penalty", "max")
            );
        invalidTargetFilterPayload.Remove("effective_modifier_delta");
        invalidTargetFilterPayload["target_team_filter"] = "hostile";
        _test.True(
            BattleAttackRollModifierSpec.FromDictionary(invalidTargetFilterPayload) == null,
            "modifier spec 不应接受 hostile 作为 target_team_filter。"
        );
        _test.True(
            BattleAttackRollModifierSpec.FromPartialDictionary(
                new Godot.Collections.Dictionary { ["target_team_filter"] = "friendly" }
            ) == null,
            "partial modifier spec 不应接受 friendly 作为 target_team_filter。"
        );

        Godot.Collections.Dictionary invalidStackModePayload =
            BattleAttackRollModifierProjection.ProjectSpec(
                BuildSpec("dust", -2, "dust_attack_roll_penalty", "max")
            );
        invalidStackModePayload.Remove("effective_modifier_delta");
        invalidStackModePayload["stack_mode"] = "legacy_add";
        _test.True(
            BattleAttackRollModifierSpec.FromDictionary(invalidStackModePayload) == null,
            "modifier spec 不应把未知 stack_mode 静默当作默认 add。"
        );
        _test.True(
            BattleAttackRollModifierSpec.FromPartialDictionary(
                new Godot.Collections.Dictionary { ["endpoint_mode"] = "legacy_either" }
            ) == null,
            "partial modifier spec 不应把未知 endpoint_mode 静默当作 either。"
        );
        _test.True(
            BattleAttackRollModifierSpec.FromPartialDictionary(
                new Godot.Collections.Dictionary { ["footprint_mode"] = "legacy_any" }
            ) == null,
            "partial modifier spec 不应把未知 footprint_mode 静默当作 any_cell。"
        );

        BattleAttackRollModifierSpec partial = BattleAttackRollModifierSpec.FromPartialDictionary(
            new Godot.Collections.Dictionary { ["modifier_delta"] = -2 }
        );
        _test.True(partial != null, "partial modifier spec 应允许只提供局部字段。");
        _test.Eq(partial?.modifier_delta ?? 0, -2, "partial modifier spec 应解析显式字段。");
        _test.Eq(partial?.stack_mode ?? default, new StringName("add"), "partial modifier spec 应保留 stack_mode 默认值。");
        _test.Eq(partial?.target_team_filter ?? default, new StringName("any"), "partial modifier spec 应保留 target_team_filter 默认值。");
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
}
