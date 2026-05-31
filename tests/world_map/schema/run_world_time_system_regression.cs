using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

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
        using var timeSystem = new WorldTimeSystem();
        var worldData = new GDictionary { ["world_step"] = 30 };

        AssertEq(timeSystem.get_world_step(worldData), 30, "get_world_step 应读取正式 int world_step。");
        AssertEq(timeSystem.get_world_day(worldData), 2, "get_world_day 应按 STEPS_PER_DAY 派生日数。");
        AssertEq(WorldTimeSystem.step_to_day(-1), -1, "负 world_step 不应兼容为第 0 天。");
        AssertEq(
            WorldTimeSystem.AdvanceWorldStep(29, 1).new_day,
            2,
            "typed step 推进应按 STEPS_PER_DAY 派生新 day。"
        );
    }

    private void TestAdvanceReportsDayCrossing()
    {
        using var timeSystem = new WorldTimeSystem();
        var worldData = new GDictionary { ["world_step"] = 14 };

        WorldTimeAdvanceResult advanceResult = timeSystem.AdvanceWorldData(worldData, 2);

        AssertEq(advanceResult.old_step, 14, "typed advance 应报告旧 step。");
        AssertEq(advanceResult.new_step, 16, "typed advance 应报告新 step。");
        AssertEq(DictInt(worldData, "world_step", -1), 16, "advance 应写回 world_data。");
        AssertTrue(advanceResult.changed, "正数推进应报告 changed。");
        AssertTrue(advanceResult.day_changed, "跨 day 推进应报告 day_changed。");
        AssertEq(advanceResult.days_elapsed, 1, "跨过一天应报告 days_elapsed=1。");

        WorldTimeAdvanceResult unchangedResult = timeSystem.AdvanceWorldData(worldData, -5);
        AssertEq(unchangedResult.new_step, 16, "负 delta_steps 应按 0 处理。");
        AssertFalse(unchangedResult.changed, "负 delta_steps 不应报告 changed。");

        GDictionary boundaryPayload = timeSystem.advance(new GDictionary { ["world_step"] = 14 }, 2);
        AssertEq(DictInt(boundaryPayload, "new_step", -1), 16, "Godot dictionary 边界仍应输出 new_step。");
    }

    private void TestAdvanceRejectsInvalidWorldStep()
    {
        using var timeSystem = new WorldTimeSystem();
        GDictionary[] invalidWorldData =
        {
            new(),
            new GDictionary { ["world_step"] = "0" },
            new GDictionary { ["world_step"] = -1 },
        };

        foreach (GDictionary worldData in invalidWorldData)
        {
            WorldTimeAdvanceResult rejectedAdvance = timeSystem.AdvanceWorldData(worldData, 1);
            AssertEq(
                rejectedAdvance.error_code,
                "invalid_world_step",
                "WorldTimeSystem 不应把缺失、字符串或负数 world_step 兼容成 0。"
            );
            AssertEq(
                rejectedAdvance.new_step,
                -1,
                "WorldTimeSystem 拒绝坏 world_step 时不应推进时间。"
            );
        }
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
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
