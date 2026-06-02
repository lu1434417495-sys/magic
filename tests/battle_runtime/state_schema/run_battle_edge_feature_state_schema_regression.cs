using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_edge_feature_state_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestMakeWallRoundtrip();
        TestMakeNoneRoundtrip();
        TestMissingFieldIsRejected();
        TestExtraFieldIsRejected();
        TestWrongTypesAreRejected();
        TestStringBoolAndIntAreRejected();
        TestEmptyRequiredEnumIsRejected();
        TestNegativeRenderLayersIsRejected();
        TestDuplicateFeatureStillUsesCurrentSchema();

        if (_failures.Count == 0)
        {
            GD.Print("Battle edge feature state schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle edge feature state schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestMakeWallRoundtrip()
    {
        BattleEdgeFeatureState wall = BattleEdgeFeatureState.make_wall();
        BattleEdgeFeatureState restored = BattleEdgeFeatureState.from_dict(wall.to_dict());
        AssertTrue(restored != null, "make_wall 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(
            restored.feature_kind,
            BattleEdgeFeatureState.FEATURE_WALL(),
            "make_wall roundtrip 应保留 feature_kind。"
        );
        AssertEq(
            restored.render_kind,
            BattleEdgeFeatureState.RENDER_WALL(),
            "make_wall roundtrip 应保留 render_kind。"
        );
        AssertEq(restored.render_layers, 1, "make_wall roundtrip 应保留 render_layers。");
        AssertTrue(restored.blocks_move, "make_wall roundtrip 应保留 blocks_move。");
        AssertTrue(restored.blocks_occupancy, "make_wall roundtrip 应保留 blocks_occupancy。");
        AssertTrue(restored.blocks_los, "make_wall roundtrip 应保留 blocks_los。");
        AssertEq(
            restored.interaction_kind,
            BattleEdgeFeatureState.INTERACT_NONE(),
            "make_wall roundtrip 应保留 interaction_kind。"
        );
        AssertEq(restored.state_tag, new StringName(""), "make_wall roundtrip 应允许空 state_tag。");
    }

    private void TestMakeNoneRoundtrip()
    {
        BattleEdgeFeatureState noneFeature = BattleEdgeFeatureState.make_none();
        BattleEdgeFeatureState restored = BattleEdgeFeatureState.from_dict(noneFeature.to_dict());
        AssertTrue(restored != null, "make_none 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        AssertEq(
            restored.feature_kind,
            BattleEdgeFeatureState.FEATURE_NONE(),
            "make_none roundtrip 应保留 feature_kind。"
        );
        AssertEq(
            restored.render_kind,
            BattleEdgeFeatureState.RENDER_NONE(),
            "make_none roundtrip 应保留 render_kind。"
        );
        AssertEq(restored.render_layers, 0, "make_none roundtrip 应保留 render_layers。");
        AssertTrue(restored.is_empty(), "make_none roundtrip 后仍应为空 edge feature。");
    }

    private void TestMissingFieldIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload.Remove("state_tag");
        AssertTrue(BattleEdgeFeatureState.from_dict(payload) == null, "缺少当前 schema 字段应返回 null。");
    }

    private void TestExtraFieldIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload["legacy_blocks_projectile"] = true;
        AssertTrue(BattleEdgeFeatureState.from_dict(payload) == null, "包含额外旧字段应返回 null。");
    }

    private void TestWrongTypesAreRejected()
    {
        GDictionary featureKindNumber = ValidPayload();
        featureKindNumber["feature_kind"] = 1;
        AssertTrue(
            BattleEdgeFeatureState.from_dict(featureKindNumber) == null,
            "feature_kind 非 String/StringName 应返回 null。"
        );

        GDictionary stateTagNumber = ValidPayload();
        stateTagNumber["state_tag"] = 1;
        AssertTrue(
            BattleEdgeFeatureState.from_dict(stateTagNumber) == null,
            "state_tag 非 String/StringName 应返回 null。"
        );

        GDictionary renderLayersFloat = ValidPayload();
        renderLayersFloat["render_layers"] = 1.0;
        AssertTrue(
            BattleEdgeFeatureState.from_dict(renderLayersFloat) == null,
            "render_layers 非 int 应返回 null。"
        );

        GDictionary blocksMoveNumber = ValidPayload();
        blocksMoveNumber["blocks_move"] = 1;
        AssertTrue(
            BattleEdgeFeatureState.from_dict(blocksMoveNumber) == null,
            "blocks_move 非 bool 应返回 null。"
        );

        GDictionary stringNamePayload = ValidPayload();
        stringNamePayload["feature_kind"] = new StringName("wall");
        stringNamePayload["render_kind"] = new StringName("wall");
        stringNamePayload["interaction_kind"] = new StringName("none");
        stringNamePayload["state_tag"] = new StringName("closed");
        AssertTrue(
            BattleEdgeFeatureState.from_dict(stringNamePayload) != null,
            "StringName enum 字段应继续可用。"
        );
    }

    private void TestStringBoolAndIntAreRejected()
    {
        GDictionary stringInt = ValidPayload();
        stringInt["render_layers"] = "1";
        AssertTrue(
            BattleEdgeFeatureState.from_dict(stringInt) == null,
            "字符串 render_layers 不应被 int() 恢复。"
        );

        GDictionary stringBool = ValidPayload();
        stringBool["blocks_los"] = "true";
        AssertTrue(
            BattleEdgeFeatureState.from_dict(stringBool) == null,
            "字符串 bool 不应被 bool() 恢复。"
        );
    }

    private void TestEmptyRequiredEnumIsRejected()
    {
        foreach (string field in new[] { "feature_kind", "render_kind", "interaction_kind" })
        {
            GDictionary payload = ValidPayload();
            payload[field] = "";
            AssertTrue(
                BattleEdgeFeatureState.from_dict(payload) == null,
                $"空必填 enum 字段 {field} 应返回 null。"
            );
        }
    }

    private void TestNegativeRenderLayersIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload["render_layers"] = -1;
        AssertTrue(BattleEdgeFeatureState.from_dict(payload) == null, "负 render_layers 应返回 null。");
    }

    private void TestDuplicateFeatureStillUsesCurrentSchema()
    {
        BattleEdgeFeatureState duplicate = BattleEdgeFeatureState.make_low_wall().duplicate_feature();
        AssertTrue(duplicate != null, "duplicate_feature 应继续返回有效对象。");
        if (duplicate == null)
        {
            return;
        }

        AssertEq(
            duplicate.feature_kind,
            BattleEdgeFeatureState.FEATURE_LOW_WALL(),
            "duplicate_feature 应保留 feature_kind。"
        );
        AssertEq(
            duplicate.render_kind,
            BattleEdgeFeatureState.RENDER_WALL(),
            "duplicate_feature 应保留 render_kind。"
        );
        AssertEq(duplicate.render_layers, 1, "duplicate_feature 应保留 render_layers。");
    }

    private static GDictionary ValidPayload()
    {
        return BattleEdgeFeatureState.make_wall().to_dict();
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
