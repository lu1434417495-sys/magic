using System.Collections.Generic;
using Godot;

// Verifies the shared item-trait detail composer surfaces equipment-trait mechanics
// (display_name + description) for item-inspection surfaces, and folds them below the
// item's flavor description without dropping the flavor text.
public partial class run_item_trait_detail_text_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestComposeFoldsTraitMechanicsBelowFlavor();
            TestComposeReturnsFlavorWhenNoTraits();
            TestBuildLinesSkipsUnknownAndNamelessTraits();
            RequestTestExit(_test.Finish("Item trait detail text regression"));
        }
        catch (System.Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Item trait detail text regression"));
        }
    }

    private void TestComposeFoldsTraitMechanicsBelowFlavor()
    {
        ItemDefinition item = BuildItem("一柄会撕开心绪的细剑。", "weapon.sword.demo.sting", "weapon.sword.demo.rend");
        var traitDefs = new Dictionary<StringName, TraitDefinition>
        {
            ["weapon.sword.demo.sting"] = BuildTrait("噬心之刺", "暴击时额外造成2D6 psychic伤害。"),
            ["weapon.sword.demo.rend"] = BuildTrait("情感撕裂", "命中叠加60TU情感撕裂，最多3层。"),
        };

        string text = ItemTraitDetailText.Compose(item.Description, item, traitDefs);
        _test.True(text.Contains("一柄会撕开心绪的细剑。"), "应保留 item flavor 描述。");
        _test.True(text.Contains("【噬心之刺】"), "应含第一个 trait 名。");
        _test.True(text.Contains("暴击时额外造成2D6 psychic伤害。"), "应含第一个 trait 机制描述。");
        _test.True(text.Contains("【情感撕裂】"), "应含第二个 trait 名。");
        _test.True(text.Contains("命中叠加60TU情感撕裂，最多3层。"), "应含第二个 trait 机制描述。");
        _test.True(
            text.IndexOf("一柄会撕开心绪的细剑。", System.StringComparison.Ordinal)
                < text.IndexOf("【噬心之刺】", System.StringComparison.Ordinal),
            "flavor 应排在 trait 机制之前。"
        );
    }

    private void TestComposeReturnsFlavorWhenNoTraits()
    {
        ItemDefinition item = BuildItem("普通铁剑。");
        string text = ItemTraitDetailText.Compose(
            item.Description,
            item,
            new Dictionary<StringName, TraitDefinition>()
        );
        _test.Eq(text, "普通铁剑。", "无 trait 时应原样返回 flavor 描述。");
    }

    private void TestBuildLinesSkipsUnknownAndNamelessTraits()
    {
        ItemDefinition item = BuildItem("测试。", "weapon.known", "weapon.unknown", "weapon.nameless");
        var traitDefs = new Dictionary<StringName, TraitDefinition>
        {
            ["weapon.known"] = BuildTrait("已知", "有效机制。"),
            ["weapon.nameless"] = BuildTrait("", "无名 trait 不应显示。"),
        };
        List<string> lines = ItemTraitDetailText.BuildTraitLines(item, traitDefs);
        _test.True(lines.Contains("【已知】"), "已知 trait 应出现。");
        _test.True(lines.Contains("有效机制。"), "已知 trait 描述应出现。");
        _test.False(lines.Contains("无名 trait 不应显示。"), "无 display_name 的 trait 应被跳过。");
        foreach (string line in lines)
            _test.False(line.Contains("unknown"), "未注册 trait id 不应出现。");
    }

    private static ItemDefinition BuildItem(string description, params string[] traitIds)
    {
        var item = new ItemDef { item_id = "demo_item", description = description };
        foreach (string traitId in traitIds)
            item.trait_ids.Add(traitId);
        return item.ToDefinition();
    }

    private static TraitDefinition BuildTrait(string displayName, string description) =>
        new(
            "",
            displayName,
            description,
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            "",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            System.Array.Empty<AttributeModifierDefinition>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<TraitDamageResistanceEntryDefinition>(),
            System.Array.Empty<TraitSaveBonusEntryDefinition>(),
            System.Array.Empty<TraitPassiveStatusEffectDefinition>(),
            System.Array.Empty<TraitRollValueSchemaEntryDefinition>()
        );
}
