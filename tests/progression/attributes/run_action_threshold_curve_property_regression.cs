using System;
using Godot;

// 行动阈值派生曲线的设计性质回归。
// 设计来源：docs/design/battle/action_cadence.md §一
//
// 上一条回归保证“表 == 生成器”，这条保证“生成器给出的形状仍是设计要的那个”：
// 慢侧二次加速惩罚、快侧平方根递减收益、以及 末档回升幅度。
// 调 BASE / FLOOR / 换掉 m^2 或 sqrt(m) 都会在这里失败。
public partial class run_action_threshold_curve_property_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private const int Base = ActionCadenceContentRules.BaseActionThresholdTu;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestFastSideBandCostsAreOddSequence();
        TestSlowSidePenaltyAcceleratesThenTapers();
        TestFastSideValuePerCostShape();
        TestAsymmetryFavoursTheSlowSideOverTheDomain();
        RequestTestExit(_test.Finish("Action threshold curve property regression"));
    }

    private static double Frequency(int agilityModifier) =>
        (double)Base / ActionCadenceContentRules.ResolveActionThreshold(agilityModifier);

    // 快侧档位入场点 (k-1)^2+1，成本 2k-1：每往下一档所需调整值按奇数列递增。
    private void TestFastSideBandCostsAreOddSequence()
    {
        int[] entries = { 1, 2, 5, 10, 17, 26 };
        for (int k = 1; k <= entries.Length; k++)
        {
            _test.Eq(
                entries[k - 1],
                (k - 1) * (k - 1) + 1,
                $"第 {k} 档入场点应为 (k-1)^2+1。"
            );
            if (k >= 2)
            {
                _test.Eq(
                    entries[k - 1] - entries[k - 2],
                    2 * (k - 1) - 1,
                    $"第 {k - 1} 档的成本应为奇数列 2k-1。"
                );
            }
        }
    }

    // 慢侧 m^2：损失幅度先加速（第 2 点最重）后收敛，且每点都在扣。
    private void TestSlowSidePenaltyAcceleratesThenTapers()
    {
        double d1 = Frequency(0) - Frequency(-1);
        double d2 = Frequency(-1) - Frequency(-2);
        double d3 = Frequency(-2) - Frequency(-3);

        _test.True(d1 > 0 && d2 > 0 && d3 > 0, "慢侧每一点都必须造成频率损失。");
        _test.True(d2 > d1, $"慢侧第 2 点的损失应大于第 1 点（加速惩罚），实际 {d2:F4} vs {d1:F4}。");
        _test.True(d3 < d2, $"慢侧第 3 点的损失应开始收敛，实际 {d3:F4} vs {d2:F4}。");
        _test.True(
            Math.Abs(Frequency(-3) - 0.4706) < 0.001,
            $"调整值 -3 的频率应约为 0.471x，实际 {Frequency(-3):F4}。"
        );
    }

    // 快侧 sqrt(m)：首点一枝独秀，随后压进窄带；末档回升但仍低于首点。
    private void TestFastSideValuePerCostShape()
    {
        (int Entry, int Cost)[] bands =
        {
            (1, 1),
            (2, 3),
            (5, 5),
            (10, 7),
            (17, 9),
            (26, 11),
        };
        var valuePerCost = new double[bands.Length];
        double previousFrequency = Frequency(0);
        for (int i = 0; i < bands.Length; i++)
        {
            double frequency = Frequency(bands[i].Entry);
            valuePerCost[i] = (frequency - previousFrequency) / bands[i].Cost;
            previousFrequency = frequency;
        }

        _test.True(
            Math.Abs(valuePerCost[0] - 0.1429) < 0.001,
            $"首点的价值/成本应约为 0.143，实际 {valuePerCost[0]:F4}。"
        );
        _test.True(
            valuePerCost[1] < valuePerCost[0] * 0.5,
            "第二档的价值/成本应比首点掉一半以上（低正数调整值收益差）。"
        );
        _test.True(
            valuePerCost[1] > valuePerCost[2],
            "价值/成本在第 2->3 档之间应继续递减。"
        );

        // 已知性质：档位成本线性增长追不上 Δf 的几何增长，后段回升。
        // 允许回升，但末档不得超过首点——超过说明曲线形状已经变质。
        _test.True(
            valuePerCost[^1] < valuePerCost[0],
            $"末档价值/成本 {valuePerCost[^1]:F4} 不得超过首点 {valuePerCost[0]:F4}。"
        );
        _test.True(
            Math.Abs(valuePerCost[^1] - 0.1212) < 0.001,
            $"末档价值/成本应约为 0.121（已知性质），实际 {valuePerCost[^1]:F4}。"
        );
    }

    // 全域不对称：负侧每点效率显著高于正侧——敏捷是“别拖后腿”型属性。
    private void TestAsymmetryFavoursTheSlowSideOverTheDomain()
    {
        double slowEfficiency = (Frequency(0) - Frequency(-3)) / 3.0;
        double fastEfficiency = (Frequency(26) - Frequency(0)) / 26.0;
        _test.True(
            slowEfficiency > fastEfficiency,
            $"负侧每点效率 {slowEfficiency:F4} 应高于正侧 {fastEfficiency:F4}。"
        );
        _test.True(
            Frequency(26) / Frequency(-3) > 8.0,
            $"两端频率跨度应超过 8 倍（设计目标），实际 {Frequency(26) / Frequency(-3):F2}。"
        );
    }
}
