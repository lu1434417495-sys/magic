using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_equipment_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEquipmentServiceKeepsTypedItemDefIndex();
        TestPreviewResultKeepsTypedCollections();
        TestEquipmentRequirementCheckResultKeepsTypedBlockers();
        TestEquipmentServiceIsPlainCSharpService();
        TestTwoHandPreviewUsesTypedBatchEntriesWithoutMutatingState();

        if (_failures.Count == 0)
        {
            GD.Print("Party equipment service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Party equipment service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestEquipmentServiceKeepsTypedItemDefIndex()
    {
        AssertEq(
            typeof(PartyEquipmentService)
                .GetField("_item_defs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, ItemDef>),
            "PartyEquipmentService internal item-def cache should be a typed dictionary."
        );
    }

    private void TestPreviewResultKeepsTypedCollections()
    {
        System.Type resultType = typeof(PartyEquipmentService).GetNestedType(
            "EquipmentEquipPreviewResult",
            BindingFlags.NonPublic
        );
        AssertEq(
            resultType
                ?.GetField("Blockers")
                ?.FieldType,
            typeof(List<string>),
            "EquipmentEquipPreviewResult blockers should stay in a C# List<string>."
        );
        AssertEq(
            resultType
                ?.GetField("OccupiedSlotIds")
                ?.FieldType,
            typeof(List<StringName>),
            "EquipmentEquipPreviewResult occupied slots should stay in a C# List<StringName>."
        );
        AssertTrue(
            typeof(PartyWarehouseService).GetNestedType(
                "WarehouseBatchItemEntry",
                BindingFlags.NonPublic
            ) != null,
            "Warehouse batch item entries should be available as an internal typed DTO."
        );
    }

    private void TestEquipmentRequirementCheckResultKeepsTypedBlockers()
    {
        AssertEq(
            typeof(EquipmentRequirementCheckResult)
                .GetField(nameof(EquipmentRequirementCheckResult.Blockers))
                ?.FieldType,
            typeof(IReadOnlyList<string>),
            "EquipmentRequirementCheckResult blockers should be exposed as a typed IReadOnlyList<string>."
        );

        EquipmentRequirement requirement = new()
        {
            required_profession_ids = new Godot.Collections.Array<string> { "fighter" },
            min_body_size = 2,
        };
        PartyMemberState memberState = new()
        {
            member_id = "hero",
            display_name = "Hero",
            body_size = 1,
        };

        EquipmentRequirementCheckResult result = requirement.CheckResult(memberState);
        AssertTrue(!result.Allowed, "Requirement should fail when profession and body size are missing.");
        AssertStringListEq(
            new List<string>(result.Blockers),
            new List<string> { "missing_profession", "body_size_too_small" },
            "Requirement typed blockers should preserve stable failure order."
        );

        GDictionary publicResult = requirement.Check(memberState);
        AssertTrue(!DictBool(publicResult, "allowed", true), "Public Check() should still project allowed=false.");
        AssertStringListEq(
            StringList(publicResult, "blockers"),
            new List<string> { "missing_profession", "body_size_too_small" },
            "Public Check() should still project blockers as a Godot Array boundary value."
        );
    }

    private void TestEquipmentServiceIsPlainCSharpService()
    {
        var type = typeof(PartyEquipmentService);
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(type),
            "PartyEquipmentService should be a plain C# service, not a Godot RefCounted."
        );
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            "PartyEquipmentService should not be registered as a Godot GlobalClass."
        );
    }

    private void TestTwoHandPreviewUsesTypedBatchEntriesWithoutMutatingState()
    {
        PartyState partyState = BuildPartyState();
        GDictionary itemDefs = BuildItemDefs();
        PartyWarehouseService warehouseService = new();
        warehouseService.setup(partyState, itemDefs);
        PartyEquipmentService equipmentService = new();
        equipmentService.setup(partyState, itemDefs, warehouseService);

        warehouseService.add_item("bronze_sword", 1);
        GDictionary equipResult = equipmentService.equip_item("hero", "bronze_sword");
        AssertTrue(DictBool(equipResult, "success", false), "Precondition: one-handed sword should equip.");

        warehouseService.add_item("iron_greatsword", 1);
        GDictionary preview = equipmentService.preview_equip("hero", "iron_greatsword");

        AssertTrue(DictBool(preview, "success", false), "Two-handed weapon preview should succeed.");
        AssertStringListEq(
            StringList(preview, "occupied_slot_ids"),
            new List<string> { "main_hand", "off_hand" },
            "Preview should project the occupied slots for the two-handed weapon."
        );

        var displacedEntries = preview["displaced_entries"].AsGodotArray();
        AssertEq(displacedEntries.Count, 1, "Preview should report the displaced main-hand item.");
        if (displacedEntries.Count > 0)
        {
            var displaced = displacedEntries[0].AsGodotDictionary();
            AssertEq(
                displaced["item_id"].AsString(),
                "bronze_sword",
                "Preview displaced entry should identify the one-handed sword."
            );
        }

        EquipmentState equipmentState = partyState.get_member_state("hero").equipment_state;
        AssertEq(
            equipmentState.get_equipped_item_id("main_hand"),
            new StringName("bronze_sword"),
            "Preview should not mutate equipped main-hand state."
        );
        AssertEq(
            warehouseService.count_item("iron_greatsword"),
            1,
            "Preview should not consume the two-handed weapon."
        );
        AssertEq(
            warehouseService.count_item("bronze_sword"),
            0,
            "Preview should not deposit the displaced sword."
        );
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState memberState = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        memberState.progression.unit_id = "hero";
        memberState.progression.display_name = "Hero";
        memberState
            .progression
            .unit_base_attributes
            .set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(), 4);
        partyState.set_member_state(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs() =>
        new()
        {
            ["bronze_sword"] = new ItemDef
            {
                item_id = "bronze_sword",
                display_name = "Bronze Sword",
                item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
                is_stackable = false,
                max_stack = 1,
                equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            },
            ["iron_greatsword"] = new ItemDef
            {
                item_id = "iron_greatsword",
                display_name = "Iron Greatsword",
                item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
                is_stackable = false,
                max_stack = 1,
                equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
                occupied_slot_ids = new Godot.Collections.Array<string>
                {
                    "main_hand",
                    "off_hand",
                },
            },
        };

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={FormatValue(expected)} actual={FormatValue(actual)}");
    }

    private void AssertStringListEq(List<string> actual, List<string> expected, string message)
    {
        bool equal = actual.Count == expected.Count;
        for (int index = 0; equal && index < actual.Count; index++)
            equal = actual[index] == expected[index];
        if (!equal)
            _failures.Add($"{message} expected={FormatValue(expected)} actual={FormatValue(actual)}");
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }

    private static List<string> StringList(GDictionary dictionary, string key)
    {
        var result = new List<string>();
        if (dictionary == null || !dictionary.ContainsKey(key))
            return result;
        foreach (Variant value in dictionary[key].AsGodotArray())
            result.Add(value.AsString());
        return result;
    }

    private static string FormatValue<T>(T value)
    {
        if (value is IEnumerable<string> strings)
            return $"[{string.Join(", ", strings)}]";
        return value?.ToString() ?? "<null>";
    }
}
