using System.Collections.Generic;
using System.Reflection;
using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_rules_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSlotOrderProjectionIsStableCopy();
        TestNormalizeSlotIdsDeduplicatesWithStringSet();
        TestTypedSlotHelpersAvoidGodotCollections();
        TestEquipmentRulesIsPlainStaticHelper();

        if (_failures.Count == 0)
        {
            GD.Print("Equipment rules regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Equipment rules regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSlotOrderProjectionIsStableCopy()
    {
        GStringNameArray slots = EquipmentRules.get_all_slot_ids();
        AssertEq(slots.Count, 12, "装备槽位表应保持 12 个正式槽位。");
        AssertEq(slots[0], EquipmentRules.MAIN_HAND(), "第一个装备槽位应是 main_hand。");
        AssertEq(slots[1], EquipmentRules.OFF_HAND(), "第二个装备槽位应是 off_hand。");
        AssertEq(slots[slots.Count - 1], EquipmentRules.BADGE(), "最后一个装备槽位应是 badge。");

        slots[0] = "invalid_test_slot";
        AssertEq(
            EquipmentRules.get_all_slot_ids()[0],
            EquipmentRules.MAIN_HAND(),
            "get_all_slot_ids 应返回投影副本，调用方不能污染内部槽位表。"
        );
    }

    private void TestNormalizeSlotIdsDeduplicatesWithStringSet()
    {
        GStringNameArray normalizedNames = EquipmentRules.normalize_slot_ids(
            new GStringNameArray
            {
                EquipmentRules.MAIN_HAND(),
                "invalid_slot",
                EquipmentRules.MAIN_HAND(),
                EquipmentRules.OFF_HAND(),
            }
        );
        AssertEq(normalizedNames.Count, 2, "StringName 入口应过滤非法槽位并去重。");
        AssertEq(normalizedNames[0], EquipmentRules.MAIN_HAND(), "去重后应保留第一次出现的 main_hand。");
        AssertEq(normalizedNames[1], EquipmentRules.OFF_HAND(), "去重后应保留 off_hand。");

        GStringNameArray normalizedStrings = EquipmentRules.normalize_slot_ids(
            new GStringArray { "body", "body", "head", "missing" }
        );
        AssertEq(normalizedStrings.Count, 2, "string 入口应同样过滤非法槽位并去重。");
        AssertEq(normalizedStrings[0], EquipmentRules.BODY(), "string 入口应规范化 body。");
        AssertEq(normalizedStrings[1], EquipmentRules.HEAD(), "string 入口应规范化 head。");
    }

    private void TestEquipmentRulesIsPlainStaticHelper()
    {
        var type = typeof(EquipmentRules);
        AssertTrue(type.IsAbstract && type.IsSealed, "EquipmentRules 应是 C# static helper。");
        AssertTrue(!typeof(RefCounted).IsAssignableFrom(type), "EquipmentRules 不应再继承 RefCounted。");
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            "EquipmentRules 不应再注册为 Godot GlobalClass。"
        );

        FieldInfo slotOrderField = type.GetField(
            "SlotOrder",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        FieldInfo validSlotIdsField = type.GetField(
            "ValidSlotIds",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        AssertEq(
            slotOrderField?.FieldType,
            typeof(string[]),
            "slot 顺序内部状态应是普通 string[]，避免静态 StringName 初始化。"
        );
        AssertEq(
            validSlotIdsField?.FieldType,
            typeof(HashSet<string>),
            "slot 存在性检查内部状态应是普通 HashSet<string>。"
        );

        AssertEq(
            type.GetMethod(nameof(EquipmentRules.MAIN_HAND))?.ReturnType,
            typeof(StringName),
            "公共槽位常量投影仍应返回 StringName。"
        );

        foreach (FieldInfo field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        {
            string fullName = field.FieldType.FullName ?? "";
            AssertTrue(
                !fullName.StartsWith("Godot.Collections.Dictionary"),
                $"EquipmentRules 内部状态不应使用 Godot Dictionary：{field.Name}"
            );
            AssertTrue(
                !fullName.StartsWith("Godot.Collections.Array"),
                $"EquipmentRules 内部状态不应使用 Godot Array：{field.Name}"
            );
            AssertTrue(
                field.FieldType != typeof(StringName),
                $"EquipmentRules 内部状态不应保存静态 StringName：{field.Name}"
            );
        }
    }

    private void TestTypedSlotHelpersAvoidGodotCollections()
    {
        IReadOnlyList<StringName> typedSlots = EquipmentRules.GetAllSlotIdsTyped();
        AssertEq(typedSlots.Count, 12, "typed slot table should expose the same slot count.");
        AssertTrue(
            typedSlots.GetType() == typeof(List<StringName>),
            "typed slot table should be backed by a C# List<StringName>, not Godot Array."
        );

        IReadOnlyList<StringName> normalizedNames = EquipmentRules.NormalizeSlotIdsTyped(
            new StringName[]
            {
                EquipmentRules.MAIN_HAND(),
                "invalid_slot",
                EquipmentRules.MAIN_HAND(),
                EquipmentRules.OFF_HAND(),
            }
        );
        AssertEq(normalizedNames.Count, 2, "typed StringName normalize should filter and dedupe.");
        AssertEq(normalizedNames[0], EquipmentRules.MAIN_HAND(), "typed normalize should keep main_hand first.");
        AssertEq(normalizedNames[1], EquipmentRules.OFF_HAND(), "typed normalize should keep off_hand second.");

        IReadOnlyList<StringName> normalizedStrings = EquipmentRules.NormalizeSlotIdsTyped(
            new[] { "body", "body", "head", "missing" }
        );
        AssertEq(normalizedStrings.Count, 2, "typed string normalize should filter and dedupe.");
        AssertEq(normalizedStrings[0], EquipmentRules.BODY(), "typed string normalize should keep body first.");
        AssertEq(normalizedStrings[1], EquipmentRules.HEAD(), "typed string normalize should keep head second.");

        AssertEq(
            typeof(EquipmentRules)
                .GetMethod(nameof(EquipmentRules.GetAllSlotIdsTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<StringName>),
            "typed slot order API should expose IReadOnlyList<StringName>."
        );
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
