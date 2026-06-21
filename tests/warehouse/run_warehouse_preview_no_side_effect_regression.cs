using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_warehouse_preview_no_side_effect_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        PartyState party = null;
        PartyMemberState memberState = null;
        ItemDef item = null;
        TraitRollGroupDef rollGroup = null;
        TraitRollGroupEntryDef rollEntry = null;
        TraitDef traitDef = null;
        TraitRollValueSchemaEntry rollValueSchema = null;
        EquipmentInstanceState firstPreviewInstance = null;
        EquipmentInstanceState secondPreviewInstance = null;
        EquipmentTraitRollService rollService = null;
        PartyWarehouseService service = null;

        try
        {
            party = BuildPartyState(1, out memberState);
            rollEntry = new TraitRollGroupEntryDef
            {
                trait_id = "sharp_edge",
                weight = 1,
            };
            rollGroup = new TraitRollGroupDef
            {
                group_id = "prefix",
                roll_count = 1,
                entries = new Godot.Collections.Array<TraitRollGroupEntryDef> { rollEntry },
            };
            item = new ItemDef
            {
                item_id = "sword",
                display_name = "Sword",
                CategoryKind = ItemCategoryKind.Equipment,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
                is_stackable = false,
                max_stack = 1,
                trait_roll_groups = new Godot.Collections.Array<TraitRollGroupDef> { rollGroup },
            };

            rollValueSchema = new TraitRollValueSchemaEntry
            {
                key = "amount",
                value_type = "int",
                min_value = 1,
                max_value = 6,
            };
            traitDef = new TraitDef
            {
                trait_id = "sharp_edge",
                allowed_source_kinds = new Godot.Collections.Array<StringName>
                {
                    "equipment_roll",
                },
                roll_value_schema = new Godot.Collections.Array<TraitRollValueSchemaEntry>
                {
                    rollValueSchema,
                },
            };
            var traitDefs = new Dictionary<StringName, TraitDef> { ["sharp_edge"] = traitDef };

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

            firstPreviewInstance = EquipmentInstanceState.CreateTransientInstance("sword");
            secondPreviewInstance = EquipmentInstanceState.CreateTransientInstance("sword");
            var result = service.PreviewBatchSwapEntriesTyped(
                new List<PartyWarehouseService.WarehouseBatchItemEntry>(),
                new List<PartyWarehouseService.WarehouseBatchItemEntry>
                {
                    new()
                    {
                        ItemId = "sword",
                        EquipmentInstance = firstPreviewInstance,
                    },
                    new()
                    {
                        ItemId = "sword",
                        EquipmentInstance = secondPreviewInstance,
                    },
                }
            );
            _test.False(result.Allowed, "preview should block over-capacity equipment batch");
            _test.Eq(
                result.ErrorCode,
                "warehouse_blocked_swap",
                "preview should return stable block error"
            );
            _test.Eq(
                GetLocalSerial(service),
                7,
                "preview should not consume equipment instance serial"
            );
            _test.Eq(
                party.warehouse_state.GetEquipmentInstancesTyped().Count,
                0,
                "preview should not mutate warehouse equipment instances"
            );
            _test.Eq(rangeCalls, 0, "preview should not mint equipment trait range rolls");
            _test.Eq(unitCalls, 0, "preview should not mint equipment trait unit rolls");
        }
        catch (Exception error)
        {
            _test.Fail(error.ToString());
        }
        finally
        {
            service?.Dispose();
            rollService?.Dispose();
            GodotSharpCleanup.DisposeGodotObject(firstPreviewInstance);
            GodotSharpCleanup.DisposeGodotObject(secondPreviewInstance);
            if (traitDef != null)
                BattleTestFixture.DisposeFixtureObject(traitDef);
            else
                GodotSharpCleanup.DisposeGodotObject(rollValueSchema);
            if (item != null)
                BattleTestFixture.DisposeFixtureObject(item);
            else
            {
                BattleTestFixture.DisposeFixtureObject(rollGroup);
                GodotSharpCleanup.DisposeGodotObject(rollEntry);
            }
            if (party != null)
                GodotRefCountedDisposer.DisposeIfValid(party);
            else
                GodotSharpCleanup.DisposeGodotObject(memberState);
            GodotSharpCleanup.CollectPendingFinalizers();
            Quit(_test.Finish("Warehouse preview no side effect regression"));
        }
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

    private static PartyState BuildPartyState(int capacity, out PartyMemberState memberState)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
            warehouse_state = new WarehouseState(),
        };
        memberState = new PartyMemberState
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
