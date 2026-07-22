using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_trait_roll_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestMintRequiresStableInstanceId();
        TestMintRollsWeightedEntriesAndRollValues();
        TestDuplicateAndValidateRehydratedConsumeNoRng();
        TestWarehouseAddItemMintsAfterStableInstanceId();
        TestWarehouseDepositingExistingInstanceDoesNotReroll();

        RequestTestExit(_test.Finish("Equipment trait roll regression"));
    }

    private void TestMintRequiresStableInstanceId()
    {
        using EquipmentTraitRollService service = BuildService();
        EquipmentInstanceState instance = EquipmentInstanceState.CreateTransientInstance("iron_sword");
        ItemDefinition item = BuildItem();

        service.MintWithRolls(instance, item);

        _test.Eq(instance.trait_instances.Count, 0, "MintWithRolls should reject empty instance_id.");
    }

    private void TestMintRollsWeightedEntriesAndRollValues()
    {
        using EquipmentTraitRollService service = BuildService();
        FixedRolls rolls = new(rangeRolls: new[] { 5 }, unitRolls: new[] { 0.0f });
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        service.MintWithRolls(instance, BuildItem());

        _test.Eq(instance.trait_instances.Count, 1, "roll_count=1 should mint one equipment_roll trait.");
        TraitInstanceState trait = instance.trait_instances[0];
        _test.Eq(
            trait.trait_instance_id,
            new StringName("eq_000001_t01"),
            "trait instance id should derive from stable equipment id."
        );
        _test.Eq(
            trait.trait_id,
            new StringName("sharp_edge"),
            "weighted pick should select the first entry for unit roll 0."
        );
        _test.Eq(
            trait.source_type,
            new StringName("equipment_roll"),
            "minted trait source should be equipment_roll."
        );
        _test.Eq(
            trait.source_id,
            new StringName("eq_000001"),
            "minted trait source_id should be equipment instance id."
        );
        _test.Eq(
            trait.GetIntRoll("amount", -1),
            5,
            "int roll value should use injected range roll."
        );
    }

    private void TestDuplicateAndValidateRehydratedConsumeNoRng()
    {
        using EquipmentTraitRollService service = BuildService();
        FixedRolls rolls = new(rangeRolls: new[] { 4 }, unitRolls: new[] { 0.0f });
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        service.MintWithRolls(instance, BuildItem());
        EquipmentInstanceState copy = instance.DuplicateState();

        _test.True(
            service.ValidateRehydrated(copy),
            "rehydrated copy should validate existing equipment_roll trait instances."
        );
        _test.Eq(rolls.RangeCalls, 1, "ValidateRehydrated should not consume range RNG.");
        _test.Eq(rolls.UnitCalls, 1, "ValidateRehydrated should not consume unit RNG.");
        _test.Eq(
            copy.trait_instances[0].GetIntRoll("amount", -1),
            4,
            "DuplicateState should preserve roll_values."
        );
    }

    private void TestWarehouseAddItemMintsAfterStableInstanceId()
    {
        using EquipmentTraitRollService rollService = BuildService();
        FixedRolls rolls = new(new[] { 3 }, new[] { 0.0f });
        rollService.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        PartyState party = BuildPartyWithCapacity(2);
        using PartyWarehouseService warehouse = new();
        warehouse.Setup(
            party,
            new Dictionary<StringName, ItemDefinition> { ["iron_sword"] = BuildItem() },
            () => "eq_000777",
            rollService
        );

        PartyWarehouseService.WarehouseAddItemResult result = warehouse.AddItemTyped(
            "iron_sword",
            1
        );
        EquipmentInstanceState stored = party.warehouse_state.GetNonEmptyEquipmentInstancesTyped()[0];

        _test.Eq(result.AddedQuantity, 1, "warehouse AddItemTyped should add equipment.");
        _test.Eq(
            stored.instance_id,
            new StringName("eq_000777"),
            "warehouse allocator should assign stable id."
        );
        _test.Eq(
            stored.trait_instances.Count,
            1,
            "warehouse AddItemTyped should mint equipment traits after stable id."
        );
        _test.Eq(
            stored.trait_instances[0].trait_instance_id,
            new StringName("eq_000777_t01"),
            "minted trait id should derive from stable warehouse id."
        );
    }

    private void TestWarehouseDepositingExistingInstanceDoesNotReroll()
    {
        using EquipmentTraitRollService rollService = BuildService();
        FixedRolls rolls = new(new[] { 3 }, new[] { 0.0f });
        rollService.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState existing = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_existing"
        );
        existing.trait_instances.Add(
            TraitInstanceState.Create(
                "eq_existing_t01",
                "sharp_edge",
                TraitSourceKind.EquipmentRoll,
                "eq_existing",
                rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 6))
            )
        );

        PartyState party = BuildPartyWithCapacity(2);
        using PartyWarehouseService warehouse = new();
        warehouse.Setup(
            party,
            new Dictionary<StringName, ItemDefinition> { ["iron_sword"] = BuildItem() },
            () => "eq_unused",
            rollService
        );

        PartyWarehouseService.WarehouseAddItemResult result = warehouse.AddEquipmentInstanceTyped(
            existing
        );
        EquipmentInstanceState stored = party.warehouse_state.GetNonEmptyEquipmentInstancesTyped()[0];

        _test.Eq(result.AddedQuantity, 1, "existing equipment instance should deposit.");
        _test.Eq(
            stored.instance_id,
            new StringName("eq_existing"),
            "existing id should be preserved."
        );
        _test.Eq(
            stored.trait_instances[0].GetIntRoll("amount", -1),
            6,
            "existing trait roll_values should be preserved."
        );
        _test.Eq(
            rolls.RangeCalls,
            0,
            "depositing existing instance should not consume range RNG."
        );
        _test.Eq(rolls.UnitCalls, 0, "depositing existing instance should not consume unit RNG.");
    }

    private static EquipmentTraitRollService BuildService()
    {
        Dictionary<StringName, TraitDefinition> traitDefs = new()
        {
            ["sharp_edge"] = BuildTraitDefinition(
                "sharp_edge",
                new TraitRollValueSchemaEntryDefinition(
                    "amount",
                    "int",
                    1,
                    6,
                    Array.Empty<StringName>()
                )
            ),
            ["heavy_head"] = BuildTraitDefinition("heavy_head"),
        };
        return new EquipmentTraitRollService(traitDefs.Values);
    }

    private static TraitDefinition BuildTraitDefinition(
        StringName traitId,
        params TraitRollValueSchemaEntryDefinition[] rollValueSchema
    ) =>
        new(
            traitId,
            "",
            "",
            Array.Empty<StringName>(),
            new StringName[] { "equipment_roll" },
            "",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitDamageResistanceEntryDefinition>(),
            Array.Empty<TraitSaveBonusEntryDefinition>(),
            Array.Empty<TraitPassiveStatusEffectDefinition>(),
            rollValueSchema
        );

    private static ItemDefinition BuildItem()
    {
        TraitRollGroupDefinition group = new(
            "prefix",
            1,
            new TraitRollGroupEntryDefinition[]
            {
                new("sharp_edge", 1, ""),
                new("heavy_head", 1, ""),
            }
        );

        return new ItemDefinition(
            itemId: "iron_sword",
            baseItemId: "",
            displayName: "Iron Sword",
            description: "",
            icon: "",
            isStackable: false,
            basePrice: 0,
            buyPrice: 0,
            sellPrice: 0,
            sellable: true,
            maxStack: 1,
            itemCategory: "equipment",
            tags: Array.Empty<StringName>(),
            craftingGroups: Array.Empty<StringName>(),
            questGroups: Array.Empty<StringName>(),
            traitIds: Array.Empty<StringName>(),
            traitRollGroups: new TraitRollGroupDefinition[] { group },
            equipmentSlotIds: new string[] { "main_hand" },
            attributeModifiers: Array.Empty<AttributeModifierDefinition>(),
            grantedSkillId: "",
            occupiedSlotIds: Array.Empty<string>(),
            equipRequirement: null,
            equipmentTypeId: "weapon",
            weaponProfile: null,
            maxDexBonus: -1
        );
    }

    private static PartyState BuildPartyWithCapacity(int capacity)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new StringNameList { "hero" },
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

    private sealed class FixedRolls
    {
        private readonly Queue<int> _rangeRolls;
        private readonly Queue<float> _unitRolls;

        public int RangeCalls { get; private set; }
        public int UnitCalls { get; private set; }

        public FixedRolls(IEnumerable<int> rangeRolls, IEnumerable<float> unitRolls)
        {
            _rangeRolls = new Queue<int>(rangeRolls ?? Array.Empty<int>());
            _unitRolls = new Queue<float>(unitRolls ?? Array.Empty<float>());
        }

        public int RollRange(int minInclusive, int maxInclusive)
        {
            RangeCalls++;
            if (_rangeRolls.Count == 0)
                return minInclusive;
            return Mathf.Clamp(_rangeRolls.Dequeue(), minInclusive, maxInclusive);
        }

        public float RollUnit()
        {
            UnitCalls++;
            if (_unitRolls.Count == 0)
                return 0.0f;
            return Mathf.Clamp(_unitRolls.Dequeue(), 0.0f, 0.999999f);
        }
    }
}
