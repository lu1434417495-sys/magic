using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_loot_commit_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEquipmentInstanceCommitKeepsPublicDictionaryShape();
        TestEquipmentInstanceAliasPayloadIsRejected();

        if (_failures.Count == 0)
        {
            GD.Print("Battle loot commit service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Battle loot commit service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestEquipmentInstanceCommitKeepsPublicDictionaryShape()
    {
        RuntimeFixture fixture = BuildFixture(capacity: 3);
        try
        {
            GDictionary result = fixture.Service._commit_equipment_instance_loot_entry(
                new GDictionary
                {
                    ["drop_type"] = BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE(),
                    ["item_id"] = "iron_sword",
                    ["equipment_instance"] = EquipmentInstanceState
                        .create("iron_sword", "eq_000001")
                        .to_dict(),
                }
            );

            AssertTrue(DictBool(result, "ok", false), "装备实例掉落应提交成功。");
            AssertEq(DictInt(result, "committed_item_count", -1), 1, "提交成功应保留 committed_item_count。");
            AssertEq(DictArray(result, "overflow_entries").Count, 0, "容量充足时不应产生 overflow。");
            AssertEq(
                fixture.PartyState.warehouse_state.equipment_instances.Count,
                1,
                "装备实例应写入共享仓库。"
            );
            AssertEq(
                fixture.PartyState.warehouse_state.equipment_instances[0].item_id,
                new StringName("iron_sword"),
                "写入仓库的装备实例应保留 item_id。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private void TestEquipmentInstanceAliasPayloadIsRejected()
    {
        RuntimeFixture fixture = BuildFixture(capacity: 3);
        try
        {
            GDictionary result = fixture.Service._commit_equipment_instance_loot_entry(
                new GDictionary
                {
                    ["drop_type"] = BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE(),
                    ["item_id"] = "iron_sword",
                    ["equipment_instance_data"] = EquipmentInstanceState
                        .create("iron_sword", "eq_000002")
                        .to_dict(),
                }
            );

            AssertFalse(DictBool(result, "ok", true), "旧 equipment_instance_data alias 不应被接受。");
            AssertEq(
                DictString(result, "error_code", ""),
                "battle_loot_equipment_instance_missing_payload",
                "缺失正式 equipment_instance payload 时应返回稳定错误码。"
            );
            AssertEq(
                fixture.PartyState.warehouse_state.equipment_instances.Count,
                0,
                "失败提交不应修改共享仓库。"
            );
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static RuntimeFixture BuildFixture(int capacity)
    {
        GDictionary itemDefs = BuildItemDefs();
        PartyState partyState = BuildPartyState(capacity);
        PartyWarehouseService warehouseService = new();
        warehouseService.setup(partyState, itemDefs);

        GameSession gameSession = new()
        {
            _item_defs = itemDefs,
        };
        GameRuntimeFacade runtime = new()
        {
            _game_session = gameSession,
            _party_state = partyState,
            _party_warehouse_service = warehouseService,
        };
        runtime._battle_loot_commit_service.setup(runtime);
        return new RuntimeFixture(runtime, gameSession, runtime._battle_loot_commit_service, partyState);
    }

    private static PartyState BuildPartyState(int capacity)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
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
            .set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(), capacity);
        partyState.set_member_state(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs()
    {
        ItemDef sword = new()
        {
            item_id = "iron_sword",
            display_name = "Iron Sword",
            item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
            is_stackable = false,
            max_stack = 1,
            equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
            equipment_slot_ids = new GStringArray { EquipmentRules.MAIN_HAND().ToString() },
        };
        return new GDictionary { ["iron_sword"] = sword };
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }

    private static Godot.Collections.Array DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Godot.Collections.Array();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture(
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
            Service?.dispose();
            Runtime?.dispose();
            GameSession?.QueueFree();
        }
    }
}
