using Godot;

public partial class run_battle_ai_action_intent_safety_gate_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestIntentValidationAndSlotRoleDefaults();
            TestSafetyGateRejectionReasons();
        }
        finally
        {
            _test.DisposeTrackedGodotObjects();
            GodotSharpCleanup.CollectPendingFinalizers();
        }
        Quit(_test.Finish("Battle AI action intent safety gate regression"));
    }

    private void TestIntentValidationAndSlotRoleDefaults()
    {
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Offense), "offense intent 应合法。");
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Control), "control intent 应合法。");
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Survival), "survival intent 应合法。");
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Positioning), "positioning intent 应合法。");
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Escape), "escape intent 应合法。");
        _test.True(BattleAiActionIntent.IsValid(BattleAiActionIntent.Wait), "wait intent 应合法。");
        _test.True(!BattleAiActionIntent.IsValid("legacy_intent"), "未知 intent 不应被接受。");
        _test.True(!BattleAiActionIntent.IsValid(""), "空 intent 不应被 typed validation 接受。");

        _test.Eq(BattleAiActionIntent.DefaultFromSlotRole("offense"), BattleAiActionIntent.Offense, "offense slot role 默认 intent。");
        _test.Eq(BattleAiActionIntent.DefaultFromSlotRole("control"), BattleAiActionIntent.Control, "control slot role 默认 intent。");
        _test.Eq(BattleAiActionIntent.DefaultFromSlotRole("survival"), BattleAiActionIntent.Survival, "survival slot role 默认 intent。");
        _test.Eq(BattleAiActionIntent.DefaultFromSlotRole("positioning"), BattleAiActionIntent.Positioning, "positioning slot role 默认 intent。");
        _test.Eq(BattleAiActionIntent.DefaultFromSlotRole("support"), new StringName(""), "未映射 slot role 应返回空 intent。");
    }

    private void TestSafetyGateRejectionReasons()
    {
        _test.Eq(BattleAiSafetyGate.GetRejectionReason(null), "missing_score_input", "null score input 应拒绝。");
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput("")),
            "missing_action_intent",
            "空 action_intent 不应再走 legacy compatibility 放行。"
        );
        _test.True(!BattleAiSafetyGate.IsEligible(ScoreInput("")), "空 action_intent 应被 safety gate 拒绝。");

        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Offense, hasProjection: true, preLethal: false, postLethal: true)),
            "offense_post_lethal_from_safe",
            "offense 不应从安全状态进入致命威胁。"
        );
        _test.True(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Offense)), "普通 offense 应允许。");

        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape)),
            "escape_missing_projection",
            "escape 必须有 post-action threat projection。"
        );
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, postLethal: true)),
            "escape_post_lethal",
            "escape 后仍致命应拒绝。"
        );
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, preDamage: 10, postDamage: 10)),
            "escape_not_safer",
            "escape 后威胁伤害未降低应拒绝。"
        );
        _test.True(
            BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, preDamage: 10, postDamage: 3)),
            "escape 更安全时应允许。"
        );

        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Survival)),
            "survival_missing_projection",
            "survival 必须有 threat projection。"
        );
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Survival, hasProjection: true, postLethal: true)),
            "survival_post_lethal",
            "survival 后仍致命应拒绝。"
        );
        _test.True(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Survival, hasProjection: true)), "survival 安全时应允许。");

        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning)),
            "positioning_missing_projection",
            "positioning 必须有 threat projection。"
        );
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, postLethal: true)),
            "positioning_post_lethal_from_safe",
            "positioning 不应从安全状态进入致命威胁。"
        );
        _test.Eq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, preDamage: 0, postDamage: 1)),
            "positioning_introduces_threat",
            "positioning 不应引入新威胁。"
        );
        _test.True(
            BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, preDamage: 4, postDamage: 2)),
            "positioning 未引入更差威胁时应允许。"
        );

        _test.True(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Control)), "control intent 应允许。");
        _test.True(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Wait)), "wait intent 应允许。");
        _test.Eq(BattleAiSafetyGate.GetRejectionReason(ScoreInput("legacy_intent")), "unknown_action_intent", "未知 intent 应拒绝。");
    }

    private BattleAiScoreInput ScoreInput(
        StringName intent,
        bool hasProjection = false,
        bool preLethal = false,
        bool postLethal = false,
        int preDamage = 0,
        int postDamage = 0
    ) =>
        new BattleAiScoreInput
        {
            action_intent = intent,
            has_post_action_threat_projection = hasProjection,
            pre_action_is_lethal_survival_risk = preLethal,
            post_action_is_lethal_survival_risk = postLethal,
            pre_action_threat_expected_damage = preDamage,
            post_action_remaining_threat_expected_damage = postDamage,
        };

}
