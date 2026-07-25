using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_equipment_requirement_rules_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestShieldRequirementReadsTypedItemIndex();
        TestShieldRequirementRejectsMissingOrNonShieldItems();
        TestItemTagRuleRejectsInvalidSlot();

        RequestTestExit(_test.Finish("Battle equipment requirement rules regression"));
    }

    private void TestShieldRequirementReadsTypedItemIndex()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_shield");
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            ["training_shield"] = BuildItemDef("training_shield", "shield", "off_hand"),
        };

        _test.True(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(unit, itemDefs),
            "副手装备带 shield tag 的物品时应满足盾牌需求。"
        );
    }

    private void TestShieldRequirementRejectsMissingOrNonShieldItems()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_focus");
        var nonShieldItemDefs = new Dictionary<StringName, ItemDefinition>
        {
            ["training_focus"] = BuildItemDef("training_focus", "focus", "off_hand"),
        };

        _test.False(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(unit, nonShieldItemDefs),
            "副手物品缺少 shield tag 时不应满足盾牌需求。"
        );
        _test.False(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(
                unit,
                new Dictionary<StringName, ItemDefinition>()
            ),
            "缺少 typed item index 时不应通过盾牌需求。"
        );
    }

    private void TestItemTagRuleRejectsInvalidSlot()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_shield");
        var itemDefs = new Dictionary<StringName, ItemDefinition>
        {
            ["training_shield"] = BuildItemDef("training_shield", "shield", "off_hand"),
        };

        _test.False(
            BattleEquipmentRequirementRules.UnitHasEquippedItemTag(
                unit,
                "invalid_slot",
                "shield",
                itemDefs
            ),
            "无效装备槽不应回读装备物品 tag。"
        );
    }

    private static BattleUnitState BuildUnitWithEquippedItem(StringName itemId)
    {
        var equipmentState = new EquipmentState();
        equipmentState.SetEquippedEntry(
            "off_hand",
            itemId,
            new[] { new StringName("off_hand") },
            EquipmentInstanceState.CreateInstance(itemId, $"eq_{itemId}")
        );

        var unit = new BattleUnitState
        {
            unit_id = "shield_requirement_user",
            display_name = "Shield Requirement User",
        }.WithCombatResourcesForTest(
            isAlive: true
        );
        unit.SetEquipmentView(equipmentState);
        return unit;
    }

    private static ItemDefinition BuildItemDef(StringName itemId, params StringName[] tags)
    {
        return new ItemDefinition(
            itemId,
            "",
            itemId.ToString(),
            "",
            "",
            false,
            0,
            0,
            0,
            true,
            1,
            ItemDefinition.ToStringName(ItemCategoryKind.Misc),
            tags,
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitRollGroupDefinition>(),
            Array.Empty<string>(),
            Array.Empty<AttributeModifierDefinition>(),
            "",
            Array.Empty<string>(),
            null,
            "",
            null,
            -1
        );
    }
}
