using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_warehouse_preview_no_side_effect_regression : SceneTree
{
    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        PartyState party = BuildPartyState(1);

        ItemDef item = new()
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
        };

        var traitDefs = new Dictionary<StringName, TraitDef>
        {
            ["sharp_edge"] = new TraitDef
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
        };
        var rollService = new EquipmentTraitRollService(traitDefs.Values);
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

        PartyWarehouseService service = new();
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
                    EquipmentInstance = EquipmentInstanceState.CreateTransientInstance("sword"),
                },
            }
        );
        if (!result.Allowed)
            throw new Exception("preview should allow adding sword");
        if (GetLocalSerial(service) != 7)
            throw new Exception("preview consumed equipment instance serial");
        if (party.warehouse_state.GetEquipmentInstancesTyped().Count != 0)
            throw new Exception("preview mutated warehouse equipment instances");
        if (rangeCalls != 0 || unitCalls != 0)
            throw new Exception("preview minted equipment trait rolls");

        GD.Print("PASS warehouse preview has no allocator or inventory side effects");
        Quit(0);
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
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, capacity);
        partyState.SetMemberState(memberState);
        return partyState;
    }
}
