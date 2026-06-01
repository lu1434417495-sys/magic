using System.Collections.Generic;
using Godot;

public partial class run_world_time_system_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestStepAndDayAccessors();
        TestAdvanceReportsDayCrossing();
        TestAdvanceRejectsInvalidWorldStep();

        if (_failures.Count == 0)
        {
            GD.Print("World time system regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"World time system regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestStepAndDayAccessors()
    {
        AssertEq(WorldTimeSystem.step_to_day(30), 2, "step_to_day 应按 STEPS_PER_DAY 派生日数。");
        AssertEq(WorldTimeSystem.step_to_day(-1), -1, "负 world_step 不应兼容为第 0 天。");
        AssertEq(
            WorldTimeSystem.AdvanceWorldStep(29, 1).new_day,
            2,
            "typed step 推进应按 STEPS_PER_DAY 派生新 day。"
        );
    }

    private void TestAdvanceReportsDayCrossing()
    {
        WorldTimeAdvanceResult advanceResult = WorldTimeSystem.AdvanceWorldStep(14, 2);

        AssertEq(advanceResult.old_step, 14, "typed advance 应报告旧 step。");
        AssertEq(advanceResult.new_step, 16, "typed advance 应报告新 step。");
        AssertTrue(advanceResult.changed, "正数推进应报告 changed。");
        AssertTrue(advanceResult.day_changed, "跨 day 推进应报告 day_changed。");
        AssertEq(advanceResult.days_elapsed, 1, "跨过一天应报告 days_elapsed=1。");

        WorldTimeAdvanceResult unchangedResult = WorldTimeSystem.AdvanceWorldStep(16, -5);
        AssertEq(unchangedResult.new_step, 16, "负 delta_steps 应按 0 处理。");
        AssertFalse(unchangedResult.changed, "负 delta_steps 不应报告 changed。");
    }

    private void TestAdvanceRejectsInvalidWorldStep()
    {
        WorldTimeAdvanceResult rejectedAdvance = WorldTimeSystem.AdvanceWorldStep(-1, 1);

        AssertEq(
            rejectedAdvance.error_code,
            "invalid_world_step",
            "WorldTimeSystem 不应把负数 world_step 兼容成 0。"
        );
        AssertEq(
            rejectedAdvance.new_step,
            -1,
            "WorldTimeSystem 拒绝坏 world_step 时不应推进时间。"
        );
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
