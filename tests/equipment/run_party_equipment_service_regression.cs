using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_equipment_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

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
        TestBattleResolutionResultKeepsFormalRandomEquipmentLootShape();
        TestBattleResolutionResultKeepsFormalEquipmentInstanceLootShape();
        TestBattleLootCommitServiceRejectsMismatchedEquipmentInstanceShape();

        Quit(_test.Finish("Party equipment service regression"));
    }

    private void TestEquipmentServiceKeepsTypedItemDefIndex()
    {
        _test.Eq(
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
        _test.Eq(
            resultType
                ?.GetField("Blockers")
                ?.FieldType,
            typeof(List<string>),
            "EquipmentEquipPreviewResult blockers should stay in a C# List<string>."
        );
        _test.Eq(
            resultType
                ?.GetField("OccupiedSlotIds")
                ?.FieldType,
            typeof(List<StringName>),
            "EquipmentEquipPreviewResult occupied slots should stay in a C# List<StringName>."
        );
        _test.True(
            typeof(PartyWarehouseService).GetNestedType(
                "WarehouseBatchItemEntry",
                BindingFlags.NonPublic
            ) != null,
            "Warehouse batch item entries should be available as an internal typed DTO."
        );
    }

    private void TestEquipmentRequirementCheckResultKeepsTypedBlockers()
    {
        _test.Eq(
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
        _test.True(!result.Allowed, "Requirement should fail when profession and body size are missing.");
        AssertStringListEq(
            new List<string>(result.Blockers),
            new List<string> { "missing_profession", "body_size_too_small" },
            "Requirement typed blockers should preserve stable failure order."
        );

        GDictionary publicResult = requirement.CheckResult(memberState).ToDictionary();
        _test.True(!DictBool(publicResult, "allowed", true), "Public Check() should still project allowed=false.");
        AssertStringListEq(
            StringList(publicResult, "blockers"),
            new List<string> { "missing_profession", "body_size_too_small" },
            "Public Check() should still project blockers as a Godot Array boundary value."
        );
    }

    private void TestEquipmentServiceIsPlainCSharpService()
    {
        var type = typeof(PartyEquipmentService);
    }

    private void TestTwoHandPreviewUsesTypedBatchEntriesWithoutMutatingState()
    {
        PartyState partyState = BuildPartyState();
        GDictionary itemDefs = BuildItemDefs();
        Dictionary<StringName, ItemDef> typedItemDefs = BuildItemDefIndex(itemDefs);
        PartyWarehouseService warehouseService = new();
        warehouseService.Setup(partyState, typedItemDefs);
        PartyEquipmentService equipmentService = new();
        equipmentService.Setup(partyState, typedItemDefs, warehouseService);

        warehouseService.AddItemTyped("bronze_sword", 1);
        var equipResult = equipmentService.EquipItemTyped("hero", "bronze_sword");
        _test.True(equipResult.Success, "Precondition: one-handed sword should equip.");

        warehouseService.AddItemTyped("iron_greatsword", 1);
        var preview = equipmentService.PreviewEquipTyped(
            "hero",
            "iron_greatsword"
        );

        _test.True(preview.Success, "Two-handed weapon preview should succeed.");
        AssertStringListEq(
            preview.OccupiedSlotIds.ConvertAll(slot => slot.ToString()),
            new List<string> { "main_hand", "off_hand" },
            "Preview should project the occupied slots for the two-handed weapon."
        );

        _test.Eq(preview.DisplacedEntries.Count, 1, "Preview should report the displaced main-hand item.");
        if (preview.DisplacedEntries.Count > 0)
        {
            _test.Eq(
                preview.DisplacedEntries[0].ItemId.ToString(),
                "bronze_sword",
                "Preview displaced entry should identify the one-handed sword."
            );
        }

        EquipmentState equipmentState = partyState.GetMemberState("hero").equipment_state;
        _test.Eq(
            equipmentState.GetEquippedItemId("main_hand"),
            new StringName("bronze_sword"),
            "Preview should not mutate equipped main-hand state."
        );
        _test.Eq(
            warehouseService.CountItem("iron_greatsword"),
            1,
            "Preview should not consume the two-handed weapon."
        );
        _test.Eq(
            warehouseService.CountItem("bronze_sword"),
            0,
            "Preview should not deposit the displaced sword."
        );
    }

    private void TestBattleResolutionResultKeepsFormalRandomEquipmentLootShape()
    {
        var result = new BattleResolutionResult();
        result.SetLootEntries(
            new GArray
            {
                new GDictionary
                {
                    ["drop_type"] = BattleLootIds.ToStringName(BattleLootDropKind.RandomEquipment),
                    ["drop_source_kind"] = "enemy_unit",
                    ["drop_source_id"] = "wolf_alpha",
                    ["drop_source_label"] = "Wolf Alpha",
                    ["drop_entry_id"] = "enemy_unit_wolf_alpha_weapon_roll",
                    ["item_id"] = "iron_greatsword",
                    ["quantity"] = 2,
                    ["drop_luck"] = 8,
                },
            }
        );

        _test.Eq(result.loot_entries.Count, 1, "formal random_equipment loot entry 应继续被 BattleResolutionResult 接受。");
        if (result.loot_entries.Count > 0)
        {
            GDictionary entry = result.loot_entries[0].AsGodotDictionary();
            _test.Eq(entry["drop_type"].AsString(), "random_equipment", "random_equipment formal shape 应保留 drop_type。");
            _test.Eq(entry["item_id"].AsString(), "iron_greatsword", "random_equipment formal shape 应保留 item_id。");
            _test.Eq(entry["quantity"].AsInt32(), 2, "random_equipment formal shape 应保留 quantity。");
            _test.Eq(entry["drop_luck"].AsInt32(), 5, "random_equipment formal shape 应把 drop_luck 规范到正式范围。");
        }
    }

    private void TestBattleResolutionResultKeepsFormalEquipmentInstanceLootShape()
    {
        var result = new BattleResolutionResult();
        result.SetLootEntries(
            new GArray
            {
                new GDictionary
                {
                    ["drop_type"] = BattleLootIds.ToStringName(BattleLootDropKind.EquipmentInstance),
                    ["drop_source_kind"] = "enemy_unit",
                    ["drop_source_id"] = "wolf_alpha",
                    ["drop_source_label"] = "Wolf Alpha",
                    ["drop_entry_id"] = "enemy_unit_wolf_alpha_equipment_instance",
                    ["item_id"] = "iron_sword",
                    ["quantity"] = 1,
                    ["equipment_instance"] = EquipmentInstanceState.CreateInstance("iron_sword", "eq_000321").ToDictionary(),
                },
            }
        );

        _test.Eq(result.loot_entries.Count, 1, "formal equipment_instance loot entry 应继续被 BattleResolutionResult 接受。");
        if (result.loot_entries.Count > 0)
        {
            GDictionary entry = result.loot_entries[0].AsGodotDictionary();
            _test.Eq(entry["drop_type"].AsString(), "equipment_instance", "equipment_instance formal shape 应保留 drop_type。");
            GDictionary equipmentInstance = entry["equipment_instance"].AsGodotDictionary();
            _test.Eq(equipmentInstance["instance_id"].AsString(), "eq_000321", "equipment_instance formal shape 应保留 instance_id。");
            _test.Eq(equipmentInstance["item_id"].AsString(), "iron_sword", "equipment_instance formal shape 应保留 item_id。");
        }
    }

    private void TestBattleLootCommitServiceRejectsMismatchedEquipmentInstanceShape()
    {
        LootCommitFixture fixture = BuildLootCommitFixture();
        try
        {
            GDictionary mismatchedEntry = new()
            {
                ["drop_type"] = BattleLootIds.ToStringName(BattleLootDropKind.EquipmentInstance),
                ["drop_source_kind"] = "enemy_unit",
                ["drop_source_id"] = "wolf_alpha",
                ["drop_source_label"] = "Wolf Alpha",
                ["drop_entry_id"] = "enemy_unit_wolf_alpha_equipment_instance",
                ["item_id"] = "bronze_sword",
                ["quantity"] = 1,
                ["equipment_instance"] = EquipmentInstanceState
                    .CreateInstance("iron_sword", "eq_000654")
                    .ToDictionary(),
            };

            var commitResult = fixture.Service.CommitEquipmentInstanceLootEntry(mismatchedEntry);

            _test.True(!commitResult.Ok, "equipment_instance formal shape 与顶层 item_id 不一致时应被拒绝。");
            _test.Eq(
                commitResult.ErrorCode,
                "battle_loot_equipment_instance_invalid_payload",
                "formal equipment_instance mismatch 应返回稳定错误码。"
            );
            _test.Eq(
                fixture.PartyState.warehouse_state.equipment_instances.Count,
                0,
                "formal equipment_instance mismatch 不应修改共享仓库。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
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
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 4);
        partyState.SetMemberState(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs() =>
        new()
        {
            [new StringName("bronze_sword")] = new ItemDef
            {
                item_id = "bronze_sword",
                display_name = "Bronze Sword",
                CategoryKind = ItemCategoryKind.Equipment,
                is_stackable = false,
                max_stack = 1,
                EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            },
            [new StringName("iron_greatsword")] = new ItemDef
            {
                item_id = "iron_greatsword",
                display_name = "Iron Greatsword",
                CategoryKind = ItemCategoryKind.Equipment,
                is_stackable = false,
                max_stack = 1,
                EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
                occupied_slot_ids = new Godot.Collections.Array<string>
                {
                    "main_hand",
                    "off_hand",
                },
            },
            [new StringName("iron_sword")] = new ItemDef
            {
                item_id = "iron_sword",
                display_name = "Iron Sword",
                CategoryKind = ItemCategoryKind.Equipment,
                is_stackable = false,
                max_stack = 1,
                EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            },
        };

    private static LootCommitFixture BuildLootCommitFixture()
    {
        GDictionary itemDefs = BuildItemDefs();
        PartyState partyState = BuildPartyState();
        PartyWarehouseService warehouseService = new();
        warehouseService.Setup(partyState, BuildItemDefIndex(itemDefs));

        GameSession gameSession = new()
        {
            _item_defs = itemDefs,
        };
        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = partyState,
            _party_warehouse_service = warehouseService,
            _equipment_drop_service = new EquipmentDropService(),
        };
        runtime._battle_loot_commit_service.Setup(runtime);
        return new LootCommitFixture(runtime, gameSession, runtime._battle_loot_commit_service, partyState);
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }



    private void AssertStringListEq(List<string> actual, List<string> expected, string message)
    {
        bool equal = actual.Count == expected.Count;
        for (int index = 0; equal && index < actual.Count; index++)
            equal = actual[index] == expected[index];
        if (!equal)
            _test.Fail($"{message} expected={FormatValue(expected)} actual={FormatValue(actual)}");
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

    private sealed class LootCommitFixture
    {
        public LootCommitFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            GameRuntimeBattleLootCommitService service,
            PartyState partyState
        )
        {
            Runtime = runtime;
            GameSession = gameSession;
            Service = service;
            PartyState = partyState;
        }

        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeBattleLootCommitService Service { get; }
        public PartyState PartyState { get; }

        public void Dispose()
        {
            Service?.Dispose();
            Runtime?.Dispose();
            GameSession?.QueueFree();
        }
    }
}
