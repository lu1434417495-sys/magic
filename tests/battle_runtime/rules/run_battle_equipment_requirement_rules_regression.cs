using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_equipment_requirement_rules_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRuleTypeIsPlainStaticCSharp();
        TestShieldRequirementReadsTypedItemIndex();
        TestShieldRequirementRejectsMissingOrNonShieldItems();
        TestItemTagRuleRejectsInvalidSlot();

        if (_failures.Count == 0)
        {
            GD.Print("Battle equipment requirement rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle equipment requirement rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRuleTypeIsPlainStaticCSharp()
    {
        Type ruleType = typeof(BattleEquipmentRequirementRules);
        AssertTrue(ruleType.IsAbstract && ruleType.IsSealed, "装备需求规则应是 plain static C# class。");
        AssertFalse(
            typeof(RefCounted).IsAssignableFrom(ruleType),
            "装备需求规则不应继承 RefCounted。"
        );
        AssertFalse(HasAttributeNamed(ruleType, "GlobalClassAttribute"), "装备需求规则不应注册 GlobalClass。");

        foreach (
            MethodInfo method in ruleType.GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly
            )
        )
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                string typeName = parameter.ParameterType.FullName ?? "";
                AssertFalse(
                    typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal),
                    $"{method.Name} 不应公开 Godot Dictionary 参数。"
                );
            }
        }
    }

    private void TestShieldRequirementReadsTypedItemIndex()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_shield");
        var itemDefs = new Dictionary<StringName, ItemDef>
        {
            ["training_shield"] = BuildItemDef("training_shield", "shield", "off_hand"),
        };

        AssertTrue(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(unit, itemDefs),
            "副手装备带 shield tag 的物品时应满足盾牌需求。"
        );
    }

    private void TestShieldRequirementRejectsMissingOrNonShieldItems()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_focus");
        var nonShieldItemDefs = new Dictionary<StringName, ItemDef>
        {
            ["training_focus"] = BuildItemDef("training_focus", "focus", "off_hand"),
        };

        AssertFalse(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(unit, nonShieldItemDefs),
            "副手物品缺少 shield tag 时不应满足盾牌需求。"
        );
        AssertFalse(
            BattleEquipmentRequirementRules.UnitHasEquippedShield(
                unit,
                new Dictionary<StringName, ItemDef>()
            ),
            "缺少 typed item index 时不应通过盾牌需求。"
        );
    }

    private void TestItemTagRuleRejectsInvalidSlot()
    {
        BattleUnitState unit = BuildUnitWithEquippedItem("training_shield");
        var itemDefs = new Dictionary<StringName, ItemDef>
        {
            ["training_shield"] = BuildItemDef("training_shield", "shield", "off_hand"),
        };

        AssertFalse(
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
        equipmentState.SetEquippedEntryTyped(
            "off_hand",
            itemId,
            new[] { new StringName("off_hand") },
            EquipmentInstanceState.create_instance(itemId, $"eq_{itemId}")
        );

        var unit = new BattleUnitState
        {
            unit_id = "shield_requirement_user",
            display_name = "Shield Requirement User",
            is_alive = true,
        };
        unit.set_equipment_view(equipmentState);
        return unit;
    }

    private static ItemDef BuildItemDef(StringName itemId, params StringName[] tags)
    {
        return new ItemDef
        {
            item_id = itemId,
            tags = new GStringNameArray(tags),
        };
    }

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
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
        AssertTrue(!condition, message);
    }
}
