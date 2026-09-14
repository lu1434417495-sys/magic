using System.Collections.Generic;
using Godot;

public partial class run_equipment_rules_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestSlotOrderProjectionIsStableCopy();
        TestNormalizeSlotIdsFiltersAndDeduplicates();

        RequestTestExit(_test.Finish("Equipment rules regression"));
    }

    private void TestSlotOrderProjectionIsStableCopy()
    {
        IReadOnlyList<StringName> slots = EquipmentRules.GetAllSlotIdsTyped();
        _test.Eq(slots.Count, 12, "装备槽位表应保持 12 个正式槽位。");
        _test.Eq(
            slots[0],
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            "第一个装备槽位应是 main_hand。"
        );
        _test.Eq(
            slots[1],
            EquipmentRules.ToStringName(EquipmentSlotKind.OffHand),
            "第二个装备槽位应是 off_hand。"
        );
        _test.Eq(
            slots[slots.Count - 1],
            EquipmentRules.ToStringName(EquipmentSlotKind.Badge),
            "最后一个装备槽位应是 badge。"
        );

        ((List<StringName>)slots)[0] = "invalid_test_slot";
        _test.Eq(
            EquipmentRules.GetAllSlotIdsTyped()[0],
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            "GetAllSlotIdsTyped 应返回副本，调用方不能污染内部槽位表。"
        );
    }

    private void TestNormalizeSlotIdsFiltersAndDeduplicates()
    {
        IReadOnlyList<StringName> normalizedNames = EquipmentRules.NormalizeSlotIdsTyped(
            new StringName[]
            {
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
                "invalid_slot",
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
                EquipmentRules.ToStringName(EquipmentSlotKind.OffHand),
            }
        );
        _test.Eq(normalizedNames.Count, 2, "StringName normalize should filter and dedupe.");
        _test.Eq(
            normalizedNames[0],
            EquipmentRules.ToStringName(EquipmentSlotKind.MainHand),
            "StringName normalize should keep main_hand first."
        );
        _test.Eq(
            normalizedNames[1],
            EquipmentRules.ToStringName(EquipmentSlotKind.OffHand),
            "StringName normalize should keep off_hand second."
        );

        IReadOnlyList<StringName> normalizedStrings = EquipmentRules.NormalizeSlotIdsTyped(
            new[] { "body", "body", "head", "missing" }
        );
        _test.Eq(normalizedStrings.Count, 2, "string normalize should filter and dedupe.");
        _test.Eq(
            normalizedStrings[0],
            EquipmentRules.ToStringName(EquipmentSlotKind.Body),
            "string normalize should keep body first."
        );
        _test.Eq(
            normalizedStrings[1],
            EquipmentRules.ToStringName(EquipmentSlotKind.Head),
            "string normalize should keep head second."
        );
    }
}
