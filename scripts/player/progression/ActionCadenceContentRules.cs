using Godot;

// 行动节奏的内容规则：agility_modifier -> action_threshold 派生表，以及由它推导出的内容侧约束。
// 设计来源：docs/design/battle/action_cadence.md
//
// 本类属于 content_definition 层：运行时（AttributeService / BattleTemporalStatusService）与
// 内容校验（EquipmentAbilityBindingValidator）都从这里取值，避免两侧各自写一份常量而漂移。
public static class ActionCadenceContentRules
{
    // 行动阈值基数。由内容 TU 分布反推：技能冷却的四个主峰 40 / 80 / 120 / 160
    // 在此基数上正好落在 1 / 2 / 3 / 4 次行动，冷却整除率 44.5%（基数 30 时只有 24.2%）。
    public const int BaseActionThresholdTu = 40;

    // 阈值粒度。时间线只能以此为步长推进，因此行动间隔天然量化到它的倍数。
    public const int ActionThresholdGranularityTu = 5;

    // 属性派生可达的最快阈值。封在 10 而非 5 是引擎约束而非手感取舍：
    // AdvanceAndConsumeThresholds 在单 step 内跨过两次阈值时只产出一次 ready，多余那次行动被吞；
    // 断点通式 rate >= (100 * threshold + 1) / tu_per_tick，5 TU 处降到 101%，
    // 意味着任何加速状态都会吞行动。10 TU 处是 201%，仍留有余量。
    public const int MinDerivedActionThresholdTu = 10;

    // 内容侧允许的最大行动速率百分比。由最快阈值与粒度推导，不写死。
    // 放宽 MinDerivedActionThresholdTu 会自动收紧本上限，两者不会脱钩。
    public const int MaxTemporalProgressRatePercent =
        MinDerivedActionThresholdTu * 100 / ActionThresholdGranularityTu;

    private const int AgilityModifierTableOffset = 3;

    // agility_modifier -> action_threshold。索引 = modifier + AgilityModifierTableOffset，
    // 域外 clamp 到两端（低于 -3 取 85，高于 +26 取 10）。
    //
    // 生成器（运行时查表，此式仅供回归断言与将来调参）：
    //     m < 0: BASE + GRANULARITY * m^2                          二次加速惩罚
    //     m = 0: BASE
    //     m > 0: max(BASE - GRANULARITY * ceil(sqrt(m)), FLOOR)    平方根递减收益
    //
    // 慢侧与快侧取互逆指数：周期轴向慢发散、向快收敛，同一个指数做不出
    // “惩罚陡峭 + 收益微薄”的形状。快侧档位入场点 = (k-1)^2 + 1（1, 2, 5, 10, 17, 26），
    // 成本 = 2k-1（1, 3, 5, 7, 9, 11）——每往下一档所需调整值按奇数列递增。
    private static readonly int[] ActionThresholdByAgilityModifier =
    {
        85, 60, 45, 40, 35, 30, 30, 30, 25, 25, 25, 25,
        25, 20, 20, 20, 20, 20, 20, 20, 15, 15, 15, 15,
        15, 15, 15, 15, 15, 10,
    };

    public static int ResolveActionThreshold(int agilityModifier)
    {
        int index = Mathf.Clamp(
            agilityModifier + AgilityModifierTableOffset,
            0,
            ActionThresholdByAgilityModifier.Length - 1
        );
        return ActionThresholdByAgilityModifier[index];
    }

    // 回归用：表的定义域两端，供测试遍历而不必硬编码长度。
    internal static int MinTableAgilityModifier => -AgilityModifierTableOffset;

    internal static int MaxTableAgilityModifier =>
        ActionThresholdByAgilityModifier.Length - 1 - AgilityModifierTableOffset;
}
