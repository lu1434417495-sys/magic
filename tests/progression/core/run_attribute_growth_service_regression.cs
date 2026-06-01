using System.Collections.Generic;
using Godot;

public partial class run_attribute_growth_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestProgressConvertsBelowTwentyAndAccumulatesAfterCap();
        TestInvalidAttributeDoesNotApply();

        if (_failures.Count == 0)
        {
            GD.Print("Attribute growth service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Attribute growth service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestProgressConvertsBelowTwentyAndAccumulatesAfterCap()
    {
        UnitProgress progress = MakeProgress();
        StringName agility = UnitBaseAttributes.AGILITY();
        progress.unit_base_attributes.set_attribute_value(agility, 2);

        AttributeGrowthService service = new();
        service.setup(progress);

        AttributeGrowthResult first = service.apply_attribute_progress_typed(
            agility,
            60,
            "first"
        );
        AssertTrue(first.Applied, "正数合法属性进度应被应用。");
        AssertEq(first.ProgressBefore, 0, "首次进度前值应为 0。");
        AssertEq(first.ProgressAfter, 60, "60 点进度应被保存。");
        AssertEq(first.AttributeBefore, 2, "首次属性前值应来自 UnitBaseAttributes。");
        AssertEq(first.AttributeAfter, 2, "不足 100 时属性不应提高。");
        AssertEq(ReadProgress(progress, agility), 60, "UnitProgress 应保存 60 点敏捷进度。");

        AttributeGrowthResult second = service.apply_attribute_progress_typed(
            agility,
            50,
            "second"
        );
        AssertTrue(second.Applied, "第二次合法属性进度应被应用。");
        AssertEq(second.ProgressBefore, 60, "第二次进度前值应读取已有进度。");
        AssertEq(second.ProgressAfter, 10, "累计 110 后应扣除 100 并保留 10。");
        AssertEq(second.AttributeBefore, 2, "第二次属性前值应仍为 2。");
        AssertEq(second.AttributeAfter, 3, "累计达到 100 后属性应提高 1。");
        AssertEq(progress.unit_base_attributes.get_attribute_value(agility), 3, "属性提高应写回基础属性。");

        progress.unit_base_attributes.set_attribute_value(agility, 19);
        progress.attribute_growth_progress[agility] = 90;
        AttributeGrowthResult capped = service.apply_attribute_progress_typed(
            agility,
            240,
            "cap"
        );
        AssertEq(capped.AttributeBefore, 19, "封顶转换前属性应为 19。");
        AssertEq(capped.AttributeAfter, 20, "属性低于 20 时最多转换到 20。");
        AssertEq(capped.ProgressAfter, 230, "达到 20 后剩余进度应继续保存。");

        AttributeGrowthResult overCap = service.apply_attribute_progress_typed(
            agility,
            120,
            "over_cap"
        );
        AssertEq(overCap.AttributeBefore, 20, "超过转换上限前属性应为 20。");
        AssertEq(overCap.AttributeAfter, 20, "属性达到 20 后不应继续自动提高。");
        AssertEq(overCap.ProgressAfter, 350, "属性达到 20 后进度应无上限累计。");
    }

    private void TestInvalidAttributeDoesNotApply()
    {
        UnitProgress progress = MakeProgress();
        AttributeGrowthService service = new();
        service.setup(progress);

        StringName invalid = "not_an_attribute";
        AttributeGrowthResult result = service.apply_attribute_progress_typed(invalid, 100);

        AssertTrue(!result.Applied, "无效属性 id 不应应用成长进度。");
        AssertTrue(
            !progress.attribute_growth_progress.ContainsKey(invalid),
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
        return progress.attribute_growth_progress.ContainsKey(attributeId)
            ? progress.attribute_growth_progress[attributeId].AsInt32()
            : 0;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
