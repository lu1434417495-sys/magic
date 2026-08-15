using System;
using Godot;

// 行动阈值 agility 派生表的规范回归。
// 设计来源：docs/proposals/battle/action_cadence_agility_derivation.md
//
// 表是规范产物、公式是生成器；本用例逐点比对二者，任何手改表值都会在这里失败。
public partial class run_action_threshold_agility_derivation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    private const int Base = ActionCadenceContentRules.BaseActionThresholdTu;
    private const int Granularity = ActionCadenceContentRules.ActionThresholdGranularityTu;
    private const int Floor = ActionCadenceContentRules.MinDerivedActionThresholdTu;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTableMatchesGenerator();
        TestTableShape();
        TestOutOfDomainClampsToEnds();
        TestAttributeServiceDerivesFromAgility();
        TestExplicitCustomStatOverridesDerivation();
        RequestTestExit(_test.Finish("Action threshold agility derivation regression"));
    }

    // 生成器：m<0 -> BASE + GRAN*m^2；m=0 -> BASE；m>0 -> max(BASE - GRAN*ceil(sqrt(m)), FLOOR)
    private static int Generate(int m)
    {
        if (m == 0)
            return Base;
        if (m < 0)
            return Base + Granularity * m * m;
        int rungs = (int)Math.Ceiling(Math.Sqrt(m));
        return Math.Max(Base - Granularity * rungs, Floor);
    }

    private void TestTableMatchesGenerator()
    {
        int min = ActionCadenceContentRules.MinTableAgilityModifier;
        int max = ActionCadenceContentRules.MaxTableAgilityModifier;
        _test.Eq(min, -3, "派生表下界应为调整值 -3。");
        _test.Eq(max, 26, "派生表上界应为调整值 +26（FLOOR 首次可达处）。");

        for (int m = min; m <= max; m++)
        {
            int actual = ActionCadenceContentRules.ResolveActionThreshold(m);
            _test.Eq(actual, Generate(m), $"调整值 {m} 的表值应与生成器一致。");
            _test.True(
                actual > 0 && actual % Granularity == 0,
                $"调整值 {m} 的阈值必须是 {Granularity} 的正整数倍，实际 {actual}。"
            );
            _test.True(
                actual >= Floor,
                $"调整值 {m} 的阈值不得低于 FLOOR={Floor}，实际 {actual}。"
            );
        }
    }

    private void TestTableShape()
    {
        // 基准点与快侧档位入场点 (k-1)^2+1：1, 2, 5, 10, 17, 26。
        _test.Eq(ActionCadenceContentRules.ResolveActionThreshold(0), 40, "调整值 0 应为基数 40 TU。");
        _test.Eq(ActionCadenceContentRules.ResolveActionThreshold(-1), 45, "调整值 -1 应为 45 TU。");
        _test.Eq(ActionCadenceContentRules.ResolveActionThreshold(-2), 60, "调整值 -2 应为 60 TU。");
        _test.Eq(ActionCadenceContentRules.ResolveActionThreshold(-3), 85, "调整值 -3 应为 85 TU。");

        (int Entry, int Threshold)[] fastBands =
        {
            (1, 35),
            (2, 30),
            (5, 25),
            (10, 20),
            (17, 15),
            (26, 10),
        };
        foreach ((int entry, int threshold) in fastBands)
        {
            _test.Eq(
                ActionCadenceContentRules.ResolveActionThreshold(entry),
                threshold,
                $"调整值 {entry} 应是 {threshold} TU 档的入场点。"
            );
            _test.True(
                ActionCadenceContentRules.ResolveActionThreshold(entry - 1) > threshold,
                $"调整值 {entry - 1} 不应已经进入 {threshold} TU 档（入场点必须精确）。"
            );
        }

        // 单调性：调整值越高，阈值不增（越快）。
        for (int m = ActionCadenceContentRules.MinTableAgilityModifier;
            m < ActionCadenceContentRules.MaxTableAgilityModifier;
            m++)
        {
            _test.True(
                ActionCadenceContentRules.ResolveActionThreshold(m + 1)
                    <= ActionCadenceContentRules.ResolveActionThreshold(m),
                $"调整值 {m} -> {m + 1} 的阈值不得回升。"
            );
        }
    }

    private void TestOutOfDomainClampsToEnds()
    {
        foreach (int m in new[] { -4, -10, -99 })
        {
            _test.Eq(
                ActionCadenceContentRules.ResolveActionThreshold(m),
                85,
                $"低于定义域的调整值 {m} 应 clamp 到表首项 85 TU。"
            );
        }
        foreach (int m in new[] { 27, 40, 999 })
        {
            _test.Eq(
                ActionCadenceContentRules.ResolveActionThreshold(m),
                Floor,
                $"高于定义域的调整值 {m} 应 clamp 到表末项 {Floor} TU。"
            );
        }
    }

    private void TestAttributeServiceDerivesFromAgility()
    {
        // agility -> 调整值 -> 阈值：走完整的 AttributeService 快照路径。
        (int Agility, int Threshold)[] cases =
        {
            (6, 60),   // mod -2
            (8, 45),   // mod -1
            (10, 40),  // mod  0
            (12, 35),  // mod +1
            (14, 30),  // mod +2
            (16, 30),  // mod +3
            (20, 25),  // mod +5
        };
        foreach ((int agility, int expected) in cases)
        {
            AttributeSnapshot snapshot = BuildSnapshot(agility);
            _test.Eq(
                snapshot.GetValue(AttributeService.ACTION_THRESHOLD),
                expected,
                $"agility {agility} 应派生出 {expected} TU 行动阈值。"
            );
        }
    }

    private void TestExplicitCustomStatOverridesDerivation()
    {
        // 显式配置的 action_threshold 优先于派生（sim 场景与特殊内容依赖这条）。
        var progress = new UnitProgress();
        progress.unit_base_attributes.SetAttributeValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
            20
        );
        progress.unit_base_attributes.SetAttributeValue(
            AttributeService.ACTION_THRESHOLD,
            55
        );
        var service = new AttributeService();
        service.SetupContext(new AttributeSourceContext { unit_progress = progress });

        _test.Eq(
            service.GetSnapshot().GetValue(AttributeService.ACTION_THRESHOLD),
            55,
            "custom_stats 里的显式 action_threshold 应压过 agility 派生。"
        );
    }

    private static AttributeSnapshot BuildSnapshot(int agility)
    {
        var progress = new UnitProgress();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            progress.unit_base_attributes.SetAttributeValue(attributeId, 10);
        progress.unit_base_attributes.SetAttributeValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
            agility
        );
        var service = new AttributeService();
        service.SetupContext(new AttributeSourceContext { unit_progress = progress });
        return service.GetSnapshot();
    }
}
