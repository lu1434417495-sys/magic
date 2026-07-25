using Godot;

public partial class run_item_price_rules_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestSchemaMaximumPriceUsesWideArithmetic();
        TestFormerOverflowThresholdRemainsExact();
        TestRoundingAndNegativeNormalizationRemainStable();
        TestUnrepresentableResultSaturates();

        RequestTestExit(_test.Finish("Item price rules regression"));
    }

    private void TestSchemaMaximumPriceUsesWideArithmetic()
    {
        using ItemDef source = BuildItem(
            "schema_max_price",
            buyPrice: 999999,
            sellPrice: 500000
        );
        ItemDefinition definition = source.ToDefinition();

        _test.Eq(
            definition.GetBuyPrice(),
            999999,
            "schema 最大价格按原价购买时不应发生 int 乘法溢出。"
        );
        _test.Eq(
            definition.GetBuyPrice(11000),
            1099999,
            "schema 最大价格应用 110% 商店倍率后应保持正确且为正数。"
        );
        _test.Eq(
            source.GetBuyPrice(11000),
            1099999,
            "authored ItemDef 的便利入口也必须委托同一宽整数规则。"
        );
        _test.Eq(
            definition.GetSellPrice(11000),
            550000,
            "schema 最大价格的合法出售倍率也应使用宽整数计算。"
        );
        _test.Eq(
            source.GetSellPrice(11000),
            550000,
            "authored ItemDef 出售价入口也必须委托同一宽整数规则。"
        );
    }

    private void TestFormerOverflowThresholdRemainsExact()
    {
        using ItemDef source = BuildItem(
            "former_overflow_threshold",
            buyPrice: 214748,
            sellPrice: 0
        );
        ItemDefinition definition = source.ToDefinition();

        _test.Eq(
            definition.GetBuyPrice(),
            214748,
            "214748 金的默认倍率计算不得因舍入加数触发 int 溢出。"
        );
        _test.Eq(
            definition.GetBuyPrice(11000),
            236223,
            "原溢出区价格应用 110% 倍率应得到正确四舍五入结果。"
        );
    }

    private void TestRoundingAndNegativeNormalizationRemainStable()
    {
        using ItemDef source = BuildItem(
            "rounding_contract",
            buyPrice: 275,
            sellPrice: 0
        );
        ItemDefinition definition = source.ToDefinition();

        _test.Eq(
            definition.GetBuyPrice(5000),
            138,
            "basis-point 价格应继续按 half-up 规则取整。"
        );
        _test.Eq(
            definition.GetBuyPrice(-1),
            0,
            "负倍率应继续归一化为零。"
        );

        using ItemDef negativeSource = BuildItem(
            "negative_price_contract",
            buyPrice: -1,
            sellPrice: 0
        );
        _test.Eq(
            negativeSource.ToDefinition().GetBuyPrice(),
            0,
            "负价格应继续归一化为零。"
        );
    }

    private void TestUnrepresentableResultSaturates()
    {
        using ItemDef source = BuildItem(
            "saturating_price_contract",
            buyPrice: 999999,
            sellPrice: 0
        );
        ItemDefinition definition = source.ToDefinition();

        _test.Eq(
            definition.GetBuyPrice(int.MaxValue),
            int.MaxValue,
            "超出 int 返回契约的价格应饱和而不是回绕为负数。"
        );
        _test.Eq(
            source.GetBuyPrice(int.MaxValue),
            int.MaxValue,
            "authored ItemDef 的超大倍率结果也应饱和而不是回绕。"
        );
    }

    private static ItemDef BuildItem(
        string itemId,
        int buyPrice,
        int sellPrice
    ) =>
        new()
        {
            item_id = new StringName(itemId),
            display_name = itemId,
            description = "Price rule fixture.",
            buy_price = buyPrice,
            sell_price = sellPrice,
            sellable = true,
        };
}
