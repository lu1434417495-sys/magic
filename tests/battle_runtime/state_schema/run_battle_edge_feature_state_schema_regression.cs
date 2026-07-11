using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_edge_feature_state_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

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

        RequestTestExit(_test.Finish("Battle edge feature state schema regression"));
    }

    private void TestMakeWallRoundtrip()
    {
        BattleEdgeFeatureState wall = BattleEdgeFeatureState.MakeWall();
        BattleEdgeFeatureState restored = BattleEdgeFeatureState.FromDictionary(wall.ToDictionary());
        _test.True(restored != null, "make_wall 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.feature_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.Wall),
            "make_wall roundtrip 应保留 feature_kind。"
        );
        _test.Eq(
            restored.render_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeRenderKind.Wall),
            "make_wall roundtrip 应保留 render_kind。"
        );
        _test.Eq(restored.render_layers, 1, "make_wall roundtrip 应保留 render_layers。");
        _test.True(restored.blocks_move, "make_wall roundtrip 应保留 blocks_move。");
        _test.True(restored.blocks_occupancy, "make_wall roundtrip 应保留 blocks_occupancy。");
        _test.True(restored.blocks_los, "make_wall roundtrip 应保留 blocks_los。");
        _test.Eq(
            restored.interaction_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeInteractionKind.None),
            "make_wall roundtrip 应保留 interaction_kind。"
        );
        _test.Eq(restored.state_tag, new StringName(""), "make_wall roundtrip 应允许空 state_tag。");
    }

    private void TestMakeNoneRoundtrip()
    {
        BattleEdgeFeatureState noneFeature = BattleEdgeFeatureState.MakeNone();
        BattleEdgeFeatureState restored = BattleEdgeFeatureState.FromDictionary(noneFeature.ToDictionary());
        _test.True(restored != null, "make_none 的当前 to_dict 形状应能恢复。");
        if (restored == null)
        {
            return;
        }

        _test.Eq(
            restored.feature_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.None),
            "make_none roundtrip 应保留 feature_kind。"
        );
        _test.Eq(
            restored.render_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeRenderKind.None),
            "make_none roundtrip 应保留 render_kind。"
        );
        _test.Eq(restored.render_layers, 0, "make_none roundtrip 应保留 render_layers。");
        _test.True(restored.IsEmpty(), "make_none roundtrip 后仍应为空 edge feature。");
    }

    private void TestMissingFieldIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload.Remove("state_tag");
        _test.True(BattleEdgeFeatureState.FromDictionary(payload) == null, "缺少当前 schema 字段应返回 null。");
    }

    private void TestExtraFieldIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload["legacy_blocks_projectile"] = true;
        _test.True(BattleEdgeFeatureState.FromDictionary(payload) == null, "包含额外旧字段应返回 null。");
    }

    private void TestWrongTypesAreRejected()
    {
        GDictionary featureKindNumber = ValidPayload();
        featureKindNumber["feature_kind"] = 1;
        _test.True(
            BattleEdgeFeatureState.FromDictionary(featureKindNumber) == null,
            "feature_kind 非 String/StringName 应返回 null。"
        );

        GDictionary stateTagNumber = ValidPayload();
        stateTagNumber["state_tag"] = 1;
        _test.True(
            BattleEdgeFeatureState.FromDictionary(stateTagNumber) == null,
            "state_tag 非 String/StringName 应返回 null。"
        );

        GDictionary renderLayersFloat = ValidPayload();
        renderLayersFloat["render_layers"] = 1.0;
        _test.True(
            BattleEdgeFeatureState.FromDictionary(renderLayersFloat) == null,
            "render_layers 非 int 应返回 null。"
        );

        GDictionary blocksMoveNumber = ValidPayload();
        blocksMoveNumber["blocks_move"] = 1;
        _test.True(
            BattleEdgeFeatureState.FromDictionary(blocksMoveNumber) == null,
            "blocks_move 非 bool 应返回 null。"
        );

        GDictionary stringNamePayload = ValidPayload();
        stringNamePayload["feature_kind"] = new StringName("wall");
        stringNamePayload["render_kind"] = new StringName("wall");
        stringNamePayload["interaction_kind"] = new StringName("none");
        stringNamePayload["state_tag"] = new StringName("closed");
        _test.True(
            BattleEdgeFeatureState.FromDictionary(stringNamePayload) != null,
            "StringName enum 字段应继续可用。"
        );
    }

    private void TestStringBoolAndIntAreRejected()
    {
        GDictionary stringInt = ValidPayload();
        stringInt["render_layers"] = "1";
        _test.True(
            BattleEdgeFeatureState.FromDictionary(stringInt) == null,
            "字符串 render_layers 不应被 int() 恢复。"
        );

        GDictionary stringBool = ValidPayload();
        stringBool["blocks_los"] = "true";
        _test.True(
            BattleEdgeFeatureState.FromDictionary(stringBool) == null,
            "字符串 bool 不应被 bool() 恢复。"
        );
    }

    private void TestEmptyRequiredEnumIsRejected()
    {
        foreach (string field in new[] { "feature_kind", "render_kind", "interaction_kind" })
        {
            GDictionary payload = ValidPayload();
            payload[field] = "";
            _test.True(
                BattleEdgeFeatureState.FromDictionary(payload) == null,
                $"空必填 enum 字段 {field} 应返回 null。"
            );
        }
    }

    private void TestNegativeRenderLayersIsRejected()
    {
        GDictionary payload = ValidPayload();
        payload["render_layers"] = -1;
        _test.True(BattleEdgeFeatureState.FromDictionary(payload) == null, "负 render_layers 应返回 null。");
    }

    private void TestDuplicateFeatureStillUsesCurrentSchema()
    {
        BattleEdgeFeatureState duplicate = BattleEdgeFeatureState.MakeLowWall().DuplicateFeature();
        _test.True(duplicate != null, "duplicate_feature 应继续返回有效对象。");
        if (duplicate == null)
        {
            return;
        }

        _test.Eq(
            duplicate.feature_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeFeatureKind.LowWall),
            "duplicate_feature 应保留 feature_kind。"
        );
        _test.Eq(
            duplicate.render_kind,
            BattleEdgeFeatureState.ToStringName(BattleEdgeRenderKind.Wall),
            "duplicate_feature 应保留 render_kind。"
        );
        _test.Eq(duplicate.render_layers, 1, "duplicate_feature 应保留 render_layers。");
    }

    private static GDictionary ValidPayload()
    {
        return BattleEdgeFeatureState.MakeWall().ToDictionary();
    }
}
