using Godot;

// FLOOR 与引擎断点的绑定回归。
// 设计来源：docs/design/battle/action_cadence.md §四
//
// AdvanceAndConsumeThresholds 在单 step 内跨过两次阈值时只产出一次 ready，多余那次行动被吞。
// 断点通式 rate >= (100 * threshold + 1) / tu_per_tick，因此属性派生可达的最快阈值
// 决定了内容侧允许的最大加速速率。这条用例把两者钉在一起：
// 放宽 MinDerivedActionThresholdTu 而不同步收紧速率上限，或反过来放宽速率上限，都会失败。
public partial class run_action_threshold_floor_breakpoint_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private const int Floor = ActionCadenceContentRules.MinDerivedActionThresholdTu;
    private const int TuPerTick = ActionCadenceContentRules.ActionThresholdGranularityTu;
    private const int MaxRate = ActionCadenceContentRules.MaxTemporalProgressRatePercent;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestMaxRateIsDerivedFromFloor();
        TestMaxRateDoesNotSwallowActionsAtFloor();
        TestOnePercentAboveMaxRateSwallowsActionsAtFloor();
        TestFiveTuWouldBeUnusable();
        RequestTestExit(_test.Finish("Action threshold floor breakpoint regression"));
    }

    // 在给定阈值与速率下推进 stepCount 个 step，返回“损失的行动次数”。
    // 期望行动数按累计进度算；实际行动数按 ready 次数算；差值即被吞掉的行动。
    private static int CountSwallowedActions(int threshold, int ratePercent, int stepCount)
    {
        var clock = new BattleUnitActionClockState();
        clock.SetThresholdRaw(threshold);
        int readyCount = 0;
        long totalGain = 0;
        for (int i = 0; i < stepCount; i++)
        {
            int gain = clock.ConsumeRateScaledGain(TuPerTick, ratePercent);
            totalGain += gain;
            if (clock.AdvanceAndConsumeThresholds(gain, threshold))
                readyCount++;
        }
        int expectedReady = (int)(totalGain / threshold);
        return expectedReady - readyCount;
    }

    private void TestMaxRateIsDerivedFromFloor()
    {
        _test.Eq(
            MaxRate,
            Floor * 100 / TuPerTick,
            "速率上限必须由 FLOOR 与 tu_per_tick 推导，不得写死。"
        );
        _test.Eq(MaxRate, 200, "当前 FLOOR=10、tu_per_tick=5，速率上限应为 200%。");
        _test.Eq(
            BattleTemporalStatusService.MaxTemporalProgressRatePercent,
            MaxRate,
            "运行时侧的速率上限应与内容规则同源。"
        );
    }

    private void TestMaxRateDoesNotSwallowActionsAtFloor()
    {
        _test.Eq(
            CountSwallowedActions(Floor, MaxRate, 2000),
            0,
            $"最快阈值 {Floor} TU 在上限速率 {MaxRate}% 下不得吞掉任何行动。"
        );
        // 现存最强加速内容 sands_time_pack 的 success_rate_percent 恰好等于上限。
        _test.Eq(
            CountSwallowedActions(Floor, 200, 2000),
            0,
            "现存 200% 加速内容必须落在安全侧。"
        );
    }

    private void TestOnePercentAboveMaxRateSwallowsActionsAtFloor()
    {
        _test.True(
            CountSwallowedActions(Floor, MaxRate + 1, 2000) > 0,
            $"超出上限 1 个百分点（{MaxRate + 1}%）就应在 {Floor} TU 处吞掉行动——"
                + "这正是速率上限存在的理由。"
        );
    }

    // 5 TU 档为什么不能由属性派生：断点降到 101%，任何加速状态都会吞行动。
    private void TestFiveTuWouldBeUnusable()
    {
        _test.True(
            CountSwallowedActions(TuPerTick, 101, 2000) > 0,
            "5 TU 阈值在 101% 速率下就会吞行动，因此不能作为属性派生的可达档位。"
        );
        _test.True(
            Floor > TuPerTick,
            "FLOOR 必须严格大于 tu_per_tick，否则最快档等于每 step 行动且无加速余量。"
        );
    }
}
