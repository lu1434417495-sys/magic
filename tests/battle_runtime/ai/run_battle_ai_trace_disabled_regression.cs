using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_trace_disabled_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestDisabledTraceSkipsConstructionWithoutChangingDecision();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI disabled trace regression"));
    }

    private void TestDisabledTraceSkipsConstructionWithoutChangingDecision()
    {
        var actor = new BattleUnitState
        {
            unit_id = "trace_gate_actor",
            display_name = "Trace Gate Actor",
            faction_id = "hostile",
            control_mode = "ai",
        }.WithCombatResourcesForTest(
            hp: 10,
            stamina: 0,
            isAlive: true
        );
        actor.SetAnchorCoord(Vector2I.Zero);

        var state = new BattleState
        {
            battle_id = "trace_gate_battle",
            phase = "unit_acting",
            map_size = Vector2I.One,
            timeline = new BattleTimelineState(),
        };
        state.SetUnit(actor);

        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            trace_enabled = false,
        };
        context.action_score_input_callback = (
            _,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            _
        ) =>
            new BattleAiScoreInput
            {
                action_kind = actionKind,
                action_label = actionLabel,
                score_bucket_id = scoreBucketId,
                command = command,
                preview = preview,
                total_score = 17,
            };

        var action = new WaitActionDefinition(
            "trace_gate_wait",
            "utility",
            BattleAiActionIntent.Wait,
            0,
            0
        );
        var evaluator = new BattleAiWaitActionEvaluator();

        BattleAiDecision disabledDecision = evaluator.Evaluate(action, context);
        _test.True(disabledDecision?.command?.IsWait() == true, "关闭 trace 不应改变 wait 决策。");
        _test.Eq(disabledDecision?.score_input?.total_score ?? -1, 17, "关闭 trace 应保留评分结果。");
        _test.Eq(
            disabledDecision?.action_trace_id ?? new StringName(""),
            new StringName(""),
            "关闭 trace 时决策不应引用一个未记录的 action trace。"
        );
        _test.Eq(context.GetActionTracesTyped().Count, 0, "关闭 trace 时不应记录 action trace。");

        context.trace_enabled = true;
        BattleAiDecision enabledDecision = evaluator.Evaluate(action, context);
        _test.True(enabledDecision?.command?.IsWait() == true, "开启 trace 后仍应选择相同 wait 决策。");
        _test.Eq(
            enabledDecision?.score_input?.total_score ?? -1,
            disabledDecision?.score_input?.total_score ?? -2,
            "trace 开关不应改变决策评分。"
        );
        _test.Eq(
            enabledDecision?.reason_text ?? "",
            disabledDecision?.reason_text ?? "",
            "trace 开关不应改变决策原因。"
        );

        IReadOnlyList<AiActionTrace> traces = context.GetActionTracesTyped();
        _test.Eq(traces.Count, 1, "开启 trace 时应记录一条 action trace。");
        if (traces.Count == 0)
            return;

        AiActionTrace trace = traces[0];
        _test.Eq(
            trace.TraceId,
            new StringName("trace_gate_wait_1"),
            "关闭 trace 的评估不应创建 trace 或消耗 trace nonce。"
        );
        _test.Eq(trace.ActionId, "trace_gate_wait", "开启后的 trace 应保留 action id。");
        _test.Eq(trace.CandidateCount, 1, "开启后的 wait trace 应保留候选计数。");
        _test.Eq(trace.TopCandidates.Count, 1, "开启后的 wait trace 应保留候选摘要。");
        if (trace.TopCandidates.Count > 0)
            _test.Eq(trace.TopCandidates[0].Label, "wait", "候选摘要结构不应改变。");
        _test.Eq(trace.BestCommand.CommandType, "wait", "开启后的 trace 应保留最佳命令。");
        _test.Eq(
            enabledDecision?.action_trace_id ?? new StringName(""),
            trace.TraceId,
            "开启 trace 时决策应引用对应 action trace。"
        );
    }
}
