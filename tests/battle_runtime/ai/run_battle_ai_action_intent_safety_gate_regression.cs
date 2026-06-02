using System;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_action_intent_safety_gate_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestIntentRulesArePlainStaticTypedCSharp();
        TestSafetyGateIsPlainStaticTypedCSharp();
        TestIntentValidationAndSlotRoleDefaults();
        TestSafetyGateRejectionReasons();

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI action intent safety gate regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI action intent safety gate regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestIntentRulesArePlainStaticTypedCSharp()
    {
        Type intentType = typeof(BattleAiActionIntent);
        AssertTrue(intentType.IsAbstract && intentType.IsSealed, "BattleAiActionIntent 应是 plain static C# helper。");
        AssertTrue(!typeof(GodotObject).IsAssignableFrom(intentType), "BattleAiActionIntent 不应继承 GodotObject/RefCounted。");
        AssertTrue(
            intentType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiActionIntent 不应注册 GlobalClass。"
        );
        AssertTrue(
            intentType.GetMethod("INTENT_OFFENSE") == null
                && intentType.GetMethod("is_valid") == null
                && intentType.GetMethod("default_from_slot_role") == null,
            "BattleAiActionIntent 不应保留 GDScript-style snake_case API。"
        );
    }

    private void TestSafetyGateIsPlainStaticTypedCSharp()
    {
        Type gateType = typeof(BattleAiSafetyGate);
        AssertTrue(gateType.IsAbstract && gateType.IsSealed, "BattleAiSafetyGate 应是 plain static C# helper。");
        AssertTrue(!typeof(GodotObject).IsAssignableFrom(gateType), "BattleAiSafetyGate 不应继承 GodotObject/RefCounted。");
        AssertTrue(
            gateType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiSafetyGate 不应注册 GlobalClass。"
        );
        AssertTrue(
            gateType.GetMethod("is_eligible") == null
                && gateType.GetMethod("get_rejection_reason") == null,
            "BattleAiSafetyGate 不应保留 GDScript-style snake_case API。"
        );
    }

    private void TestIntentValidationAndSlotRoleDefaults()
    {
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Offense), "offense intent 应合法。");
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Control), "control intent 应合法。");
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Survival), "survival intent 应合法。");
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Positioning), "positioning intent 应合法。");
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Escape), "escape intent 应合法。");
        AssertTrue(BattleAiActionIntent.IsValid(BattleAiActionIntent.Wait), "wait intent 应合法。");
        AssertTrue(!BattleAiActionIntent.IsValid("legacy_intent"), "未知 intent 不应被接受。");
        AssertTrue(!BattleAiActionIntent.IsValid(""), "空 intent 不应被 typed validation 接受。");

        AssertEq(BattleAiActionIntent.DefaultFromSlotRole("offense"), BattleAiActionIntent.Offense, "offense slot role 默认 intent。");
        AssertEq(BattleAiActionIntent.DefaultFromSlotRole("control"), BattleAiActionIntent.Control, "control slot role 默认 intent。");
        AssertEq(BattleAiActionIntent.DefaultFromSlotRole("survival"), BattleAiActionIntent.Survival, "survival slot role 默认 intent。");
        AssertEq(BattleAiActionIntent.DefaultFromSlotRole("positioning"), BattleAiActionIntent.Positioning, "positioning slot role 默认 intent。");
        AssertEq(BattleAiActionIntent.DefaultFromSlotRole("support"), new StringName(""), "未映射 slot role 应返回空 intent。");
    }

    private void TestSafetyGateRejectionReasons()
    {
        AssertEq(BattleAiSafetyGate.GetRejectionReason(null), "missing_score_input", "null score input 应拒绝。");
        AssertTrue(
            BattleAiSafetyGate.IsEligible(ScoreInput("")),
            "空 action_intent 当前仍只在 gate 层允许通过；严格合法性由 candidate request typed validation 承担。"
        );

        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Offense, hasProjection: true, preLethal: false, postLethal: true)),
            "offense_post_lethal_from_safe",
            "offense 不应从安全状态进入致命威胁。"
        );
        AssertTrue(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Offense)), "普通 offense 应允许。");

        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape)),
            "escape_missing_projection",
            "escape 必须有 post-action threat projection。"
        );
        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, postLethal: true)),
            "escape_post_lethal",
            "escape 后仍致命应拒绝。"
        );
        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, preDamage: 10, postDamage: 10)),
            "escape_not_safer",
            "escape 后威胁伤害未降低应拒绝。"
        );
        AssertTrue(
            BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Escape, hasProjection: true, preDamage: 10, postDamage: 3)),
            "escape 更安全时应允许。"
        );

        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Survival)),
            "survival_missing_projection",
            "survival 必须有 threat projection。"
        );
        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Survival, hasProjection: true, postLethal: true)),
            "survival_post_lethal",
            "survival 后仍致命应拒绝。"
        );
        AssertTrue(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Survival, hasProjection: true)), "survival 安全时应允许。");

        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning)),
            "positioning_missing_projection",
            "positioning 必须有 threat projection。"
        );
        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, postLethal: true)),
            "positioning_post_lethal_from_safe",
            "positioning 不应从安全状态进入致命威胁。"
        );
        AssertEq(
            BattleAiSafetyGate.GetRejectionReason(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, preDamage: 0, postDamage: 1)),
            "positioning_introduces_threat",
            "positioning 不应引入新威胁。"
        );
        AssertTrue(
            BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Positioning, hasProjection: true, preDamage: 4, postDamage: 2)),
            "positioning 未引入更差威胁时应允许。"
        );

        AssertTrue(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Control)), "control intent 应允许。");
        AssertTrue(BattleAiSafetyGate.IsEligible(ScoreInput(BattleAiActionIntent.Wait)), "wait intent 应允许。");
        AssertEq(BattleAiSafetyGate.GetRejectionReason(ScoreInput("legacy_intent")), "unknown_action_intent", "未知 intent 应拒绝。");
    }

    private static BattleAiScoreInput ScoreInput(
        StringName intent,
        bool hasProjection = false,
        bool preLethal = false,
        bool postLethal = false,
        int preDamage = 0,
        int postDamage = 0
    ) =>
        new()
        {
            action_intent = intent,
            has_post_action_threat_projection = hasProjection,
            pre_action_is_lethal_survival_risk = preLethal,
            post_action_is_lethal_survival_risk = postLethal,
            pre_action_threat_expected_damage = preDamage,
            post_action_remaining_threat_expected_damage = postDamage,
        };

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
