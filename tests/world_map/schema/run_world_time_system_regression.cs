using System.Collections.Generic;
using Godot;

public partial class run_world_time_system_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestStepAndDayAccessors();
        TestStepToMonthBoundaries();
        TestAdvanceReportsDayCrossing();
        TestAdvanceRejectsInvalidWorldStep();

        return _test.Finish("World time system regression");
    }

    private void TestStepAndDayAccessors()
    {
        _test.Eq(WorldTimeSystem.StepToDay(30), 2, "step_to_day 应按 STEPS_PER_DAY 派生日数。");
        _test.Eq(WorldTimeSystem.StepToDay(-1), -1, "负 world_step 不应兼容为第 0 天。");
        _test.Eq(
            WorldTimeSystem.AdvanceWorldStep(29, 1).new_day,
            2,
            "typed step 推进应按 STEPS_PER_DAY 派生新 day。"
        );
    }

    private void TestStepToMonthBoundaries()
    {
        _test.Eq(WorldTimeSystem.StepToMonth(-1), -1, "负 world_step 不应兼容为第 0 月。");
        _test.Eq(WorldTimeSystem.StepToMonth(0), 0, "第 0 step 应属于第 0 个 world month。");
        _test.Eq(WorldTimeSystem.StepToMonth(449), 0, "第 449 step 仍应属于第 0 个 world month。");
        _test.Eq(WorldTimeSystem.StepToMonth(450), 1, "第 450 step 应进入第 1 个 world month。");
        _test.Eq(WorldTimeSystem.StepToMonth(900), 2, "第 900 step 应进入第 2 个 world month。");
    }

    private void TestAdvanceReportsDayCrossing()
    {
        WorldTimeAdvanceResult advanceResult = WorldTimeSystem.AdvanceWorldStep(14, 2);

        _test.Eq(advanceResult.old_step, 14, "typed advance 应报告旧 step。");
        _test.Eq(advanceResult.new_step, 16, "typed advance 应报告新 step。");
        _test.True(advanceResult.changed, "正数推进应报告 changed。");
        _test.True(advanceResult.day_changed, "跨 day 推进应报告 day_changed。");
        _test.Eq(advanceResult.days_elapsed, 1, "跨过一天应报告 days_elapsed=1。");

        WorldTimeAdvanceResult unchangedResult = WorldTimeSystem.AdvanceWorldStep(16, -5);
        _test.Eq(unchangedResult.new_step, 16, "负 delta_steps 应按 0 处理。");
        _test.False(unchangedResult.changed, "负 delta_steps 不应报告 changed。");
    }

    private void TestAdvanceRejectsInvalidWorldStep()
    {
        WorldTimeAdvanceResult rejectedAdvance = WorldTimeSystem.AdvanceWorldStep(-1, 1);

        _test.Eq(
            rejectedAdvance.error_code,
            "invalid_world_step",
            "WorldTimeSystem 不应把负数 world_step 兼容成 0。"
        );
        _test.Eq(
            rejectedAdvance.new_step,
            -1,
            "WorldTimeSystem 拒绝坏 world_step 时不应推进时间。"
        );
    }
}
