using System.Collections.Generic;
using Godot;

public partial class run_attribute_growth_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestProgressConvertsBelowTwentyAndAccumulatesAfterCap();
        TestInvalidAttributeDoesNotApply();

        Quit(_test.Finish("Attribute growth service regression"));
    }

    private void TestProgressConvertsBelowTwentyAndAccumulatesAfterCap()
    {
        UnitProgress progress = MakeProgress();
        StringName agility = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility);
        progress.unit_base_attributes.SetAttributeValue(agility, 2);

        AttributeGrowthService service = new();
        service.Setup(progress);

        AttributeGrowthResult first = service.ApplyAttributeProgressTyped(
            agility,
            60,
            "first"
        );
        _test.True(first.Applied, "正数合法属性进度应被应用。");
        _test.Eq(first.ProgressBefore, 0, "首次进度前值应为 0。");
        _test.Eq(first.ProgressAfter, 60, "60 点进度应被保存。");
        _test.Eq(first.AttributeBefore, 2, "首次属性前值应来自 UnitBaseAttributes。");
        _test.Eq(first.AttributeAfter, 2, "不足 100 时属性不应提高。");
        _test.Eq(ReadProgress(progress, agility), 60, "UnitProgress 应保存 60 点敏捷进度。");

        AttributeGrowthResult second = service.ApplyAttributeProgressTyped(
            agility,
            50,
            "second"
        );
        _test.True(second.Applied, "第二次合法属性进度应被应用。");
        _test.Eq(second.ProgressBefore, 60, "第二次进度前值应读取已有进度。");
        _test.Eq(second.ProgressAfter, 10, "累计 110 后应扣除 100 并保留 10。");
        _test.Eq(second.AttributeBefore, 2, "第二次属性前值应仍为 2。");
        _test.Eq(second.AttributeAfter, 3, "累计达到 100 后属性应提高 1。");
        _test.Eq(progress.unit_base_attributes.GetAttributeValue(agility), 3, "属性提高应写回基础属性。");

        progress.unit_base_attributes.SetAttributeValue(agility, 19);
        progress.SetAttributeGrowthProgressAmount(agility, 90);
        AttributeGrowthResult capped = service.ApplyAttributeProgressTyped(
            agility,
            240,
            "cap"
        );
        _test.Eq(capped.AttributeBefore, 19, "封顶转换前属性应为 19。");
        _test.Eq(capped.AttributeAfter, 20, "属性低于 20 时最多转换到 20。");
        _test.Eq(capped.ProgressAfter, 230, "达到 20 后剩余进度应继续保存。");

        AttributeGrowthResult overCap = service.ApplyAttributeProgressTyped(
            agility,
            120,
            "over_cap"
        );
        _test.Eq(overCap.AttributeBefore, 20, "超过转换上限前属性应为 20。");
        _test.Eq(overCap.AttributeAfter, 20, "属性达到 20 后不应继续自动提高。");
        _test.Eq(overCap.ProgressAfter, 350, "属性达到 20 后进度应无上限累计。");
    }

    private void TestInvalidAttributeDoesNotApply()
    {
        UnitProgress progress = MakeProgress();
        AttributeGrowthService service = new();
        service.Setup(progress);

        StringName invalid = "not_an_attribute";
        AttributeGrowthResult result = service.ApplyAttributeProgressTyped(invalid, 100);

        _test.True(!result.Applied, "无效属性 id 不应应用成长进度。");
        _test.True(
            !progress.TryGetAttributeGrowthProgressAmount(invalid, out _),
            "无效属性 id 不应写入成长进度表。"
        );
    }

    private static UnitProgress MakeProgress() =>
        new()
        {
            unit_id = "hero",
            display_name = "hero",
            unit_base_attributes = new UnitBaseAttributes(),
        };

    private static int ReadProgress(UnitProgress progress, StringName attributeId)
    {
        return progress.TryGetAttributeGrowthProgressAmount(attributeId, out int amount)
            ? amount
            : 0;
    }


}
