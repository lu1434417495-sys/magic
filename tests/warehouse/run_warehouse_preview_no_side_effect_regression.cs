using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_warehouse_preview_no_side_effect_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        new("warehouse_preview_no_side_effect", quarantineOnDrain: true);

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        EquipmentTraitRollService rollService = null;
        PartyWarehouseService service = null;
        try
        {
            PartyState party = BuildPartyState(1);
            ItemDef item = BuildItemDef();
            var traitDefs = new Dictionary<StringName, TraitDef>
            {
                ["sharp_edge"] = BuildTraitDef(),
            };
            rollService = new EquipmentTraitRollService(traitDefs.Values);
            int rangeCalls = 0;
            int unitCalls = 0;
            rollService.SetRollHooksForTesting(
                (minInclusive, maxInclusive) =>
                {
                    rangeCalls += 1;
                    return minInclusive;
                },
                () =>
                {
                    unitCalls += 1;
                    return 0.0f;
                }
            );

            service = new PartyWarehouseService();
            service.Setup(
                party,
                new Dictionary<StringName, ItemDef> { [item.item_id] = item },
                default,
                rollService
            );
            SetLocalSerial(service, 7);

            var result = service.PreviewBatchSwapEntriesTyped(
                new List<PartyWarehouseService.WarehouseBatchItemEntry>(),
                new List<PartyWarehouseService.WarehouseBatchItemEntry>
                {
                    new()
                    {
                        ItemId = "sword",
                        EquipmentInstance = _runtimeScope.OwnWrapper(
                            EquipmentInstanceState.CreateTransientInstance("sword"),
                            "preview-equipment"
                        ),
                    },
                    new()
                    {
                        ItemId = "sword",
                        EquipmentInstance = _runtimeScope.OwnWrapper(
                            EquipmentInstanceState.CreateTransientInstance("sword"),
                            "preview-equipment"
                        ),
                    },
                }
            );
            _test.False(result.Allowed, "preview should block over-capacity equipment batch");
            _test.Eq(
                result.ErrorCode,
                "warehouse_blocked_swap",
                "preview should return stable block error"
            );
            _test.Eq(GetLocalSerial(service), 7, "preview consumed equipment instance serial");
            _test.Eq(
                party.warehouse_state.GetEquipmentInstancesTyped().Count,
                0,
                "preview mutated warehouse equipment instances"
            );
            _test.Eq(rangeCalls, 0, "preview minted equipment trait range rolls");
            _test.Eq(unitCalls, 0, "preview minted equipment trait unit rolls");
        }
        catch (Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            service?.Dispose();
            rollService?.Dispose();
            _runtimeScope.Close();
        }

        Quit(_test.Finish("warehouse preview has no allocator or inventory side effects"));
    }

    private ItemDef BuildItemDef()
    {
        return _runtimeScope.OwnWrapper(
            new ItemDef
            {
                item_id = "sword",
                display_name = "Sword",
                CategoryKind = ItemCategoryKind.Equipment,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
                is_stackable = false,
                max_stack = 1,
                trait_roll_groups = new Godot.Collections.Array<TraitRollGroupDef>
                {
                    new()
                    {
                        group_id = "prefix",
                        roll_count = 1,
                        entries = new Godot.Collections.Array<TraitRollGroupEntryDef>
                        {
                            new()
                            {
                                trait_id = "sharp_edge",
                                weight = 1,
                            },
                        },
                    },
                },
            },
            "item"
        );
    }

    private TraitDef BuildTraitDef()
    {
        return _runtimeScope.OwnWrapper(
            new TraitDef
            {
                trait_id = "sharp_edge",
                allowed_source_kinds = new Godot.Collections.Array<StringName>
                {
                    "equipment_roll",
                },
                roll_value_schema = new Godot.Collections.Array<TraitRollValueSchemaEntry>
                {
                    new()
                    {
                        key = "amount",
                        value_type = "int",
                        min_value = 1,
                        max_value = 6,
                    },
                },
            },
            "trait"
        );
    }

    private static void SetLocalSerial(PartyWarehouseService service, int value)
    {
        var field = typeof(PartyWarehouseService).GetField(
            "_local_equipment_instance_serial",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
            throw new Exception("missing warehouse serial field");
        field.SetValue(service, value);
    }

    private static int GetLocalSerial(PartyWarehouseService service)
    {
        var field = typeof(PartyWarehouseService).GetField(
            "_local_equipment_instance_serial",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        if (field == null)
            throw new Exception("missing warehouse serial field");
        return (int)field.GetValue(service);
    }

    private PartyState BuildPartyState(int capacity)
    {
        PartyState partyState = _runtimeScope.OwnWrapper(
            new PartyState
            {
                leader_member_id = "hero",
                main_character_member_id = "hero",
                active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
                warehouse_state = new WarehouseState(),
            },
            "party"
        );
        PartyMemberState memberState = _runtimeScope.OwnWrapper(
            new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
            },
            "member"
        );
        memberState.progression.unit_id = "hero";
        memberState.progression.display_name = "Hero";
        memberState
            .progression
            .unit_base_attributes
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, capacity);
        partyState.SetMemberState(memberState);
        _runtimeScope.OwnWrapper(partyState, "party-built");
        return partyState;
    }
}
