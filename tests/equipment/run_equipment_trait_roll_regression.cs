using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_trait_roll_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceOwnsFallbackRngAndBorrowsInjectedRng();
        TestServiceCanTakeOwnershipOfInjectedRng();
        TestServiceCanTakeOwnershipOfPreviouslyBorrowedRng();
        TestMintRequiresStableInstanceId();
        TestMintRollsWeightedEntriesAndRollValues();
        TestDuplicateAndValidateRehydratedConsumeNoRng();
        TestWarehouseAddItemMintsAfterStableInstanceId();
        TestWarehouseDepositingExistingInstanceDoesNotReroll();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Equipment trait roll regression"));
    }

    private void TestServiceOwnsFallbackRngAndBorrowsInjectedRng()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService service = catalog.BuildService();
        var borrowedRng = new RandomNumberGenerator();

        try
        {
            _test.True(
                service.HasLiveOwnedRngForTests,
                "未注入 RNG 时 EquipmentTraitRollService 应持有 live owned RNG。"
            );

            service.ConfigureRng(borrowedRng);

            _test.False(
                service.OwnsRngForTests,
                "注入 RNG 应保持 caller-owned borrowed 语义。"
            );
            _test.True(
                ReferenceEquals(service.RngForTests, borrowedRng),
                "注入 borrowed RNG 后 service 应切换到 caller 提供的 RNG。"
            );
            _test.False(
                service.HasLiveOwnedRngForTests,
                "注入 borrowed RNG 后 service 不应报告 live owned RNG。"
            );
        }
        finally
        {
            service.Dispose();
            _test.True(
                service.RngForTests == null,
                "service dispose 应清空当前 RNG 引用。"
            );
            _ = borrowedRng.Randi();
            GodotSharpCleanup.DisposeGodotObject(borrowedRng);
        }
    }

    private void TestServiceCanTakeOwnershipOfInjectedRng()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        RandomNumberGenerator ownedRng = new();
        ownedRng.Randomize();
        EquipmentTraitRollService service = catalog.BuildService(ownedRng, ownsRng: true);

        _test.True(
            service.OwnsRngForTests,
            "ownsRng=true 应把传入 RNG 标记为 trait roll service-owned。"
        );
        _test.True(
            service.HasLiveOwnedRngForTests,
            "转交所有权的 RNG 在 trait roll service dispose 前应保持 live owned。"
        );

        service.Dispose();
        _test.True(
            service.IsDisposedForTests,
            "trait roll service dispose 后应标记 disposed。"
        );
        _test.True(
            service.RngForTests == null,
            "trait roll service dispose 应清空已 owned 的 RNG 引用。"
        );
        _test.False(
            service.HasLiveOwnedRngForTests,
            "trait roll service dispose 后不应继续报告 live owned RNG。"
        );
    }

    private void TestServiceCanTakeOwnershipOfPreviouslyBorrowedRng()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        RandomNumberGenerator rng = new();
        rng.Randomize();
        EquipmentTraitRollService service = catalog.BuildService(rng);

        _test.False(
            service.OwnsRngForTests,
            "普通构造注入 RNG 时 trait roll service 应先保持 borrowed。"
        );
        service.SetOwnedRng(rng);
        _test.True(
            ReferenceEquals(service.RngForTests, rng),
            "同一个 RNG 转交所有权时不应替换或释放当前 wrapper。"
        );
        _test.True(
            service.OwnsRngForTests,
            "SetOwnedRng 应允许 caller 显式转交之前 borrowed 的 RNG。"
        );
        _test.True(
            service.HasLiveOwnedRngForTests,
            "显式转交后的 RNG 在 service dispose 前应保持 live owned。"
        );

        service.Dispose();
        _test.True(
            service.RngForTests == null,
            "释放接管的 RNG 后 service 应清空引用。"
        );
        _test.False(
            service.HasLiveOwnedRngForTests,
            "释放接管的 RNG 后 service 不应报告 live owned RNG。"
        );
    }

    private void TestMintRequiresStableInstanceId()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService service = catalog.BuildService();
        using ItemFixture item = BuildItemFixture();
        EquipmentInstanceState instance = EquipmentInstanceState.CreateTransientInstance("iron_sword");
        try
        {
            service.MintWithRolls(instance, item.Item);
            _test.Eq(instance.trait_instances.Count, 0, "MintWithRolls should reject empty instance_id.");
        }
        finally
        {
            instance?.Dispose();
        }
    }

    private void TestMintRollsWeightedEntriesAndRollValues()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService service = catalog.BuildService();
        using ItemFixture item = BuildItemFixture();
        FixedRolls rolls = new(rangeRolls: new[] { 5 }, unitRolls: new[] { 0.0f });
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        try
        {
            service.MintWithRolls(instance, item.Item);

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
        finally
        {
            instance?.Dispose();
        }
    }

    private void TestDuplicateAndValidateRehydratedConsumeNoRng()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService service = catalog.BuildService();
        using ItemFixture item = BuildItemFixture();
        FixedRolls rolls = new(rangeRolls: new[] { 4 }, unitRolls: new[] { 0.0f });
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        EquipmentInstanceState copy = null;
        try
        {
            service.MintWithRolls(instance, item.Item);
            copy = instance.DuplicateState();

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
        finally
        {
            copy?.Dispose();
            instance?.Dispose();
        }
    }

    private void TestWarehouseAddItemMintsAfterStableInstanceId()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService rollService = catalog.BuildService();
        using ItemFixture item = BuildItemFixture();
        FixedRolls rolls = new(new[] { 3 }, new[] { 0.0f });
        rollService.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        PartyState party = BuildPartyWithCapacity(2);
        PartyWarehouseService warehouse = new();
        try
        {
            warehouse.Setup(
                party,
                new Dictionary<StringName, ItemDef> { ["iron_sword"] = item.Item },
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
        finally
        {
            warehouse.Dispose();
            party?.Dispose();
        }
    }

    private void TestWarehouseDepositingExistingInstanceDoesNotReroll()
    {
        using TraitCatalogFixture catalog = BuildTraitCatalog();
        using EquipmentTraitRollService rollService = catalog.BuildService();
        using ItemFixture item = BuildItemFixture();
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
        PartyWarehouseService warehouse = new();
        bool transferred = false;
        try
        {
            warehouse.Setup(
                party,
                new Dictionary<StringName, ItemDef> { ["iron_sword"] = item.Item },
                () => "eq_unused",
                rollService
            );

            PartyWarehouseService.WarehouseAddItemResult result = warehouse.AddEquipmentInstanceTyped(
                existing
            );
            transferred = result.AddedQuantity > 0;
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
        finally
        {
            warehouse.Dispose();
            party?.Dispose();
            if (!transferred)
                existing?.Dispose();
        }
    }

    private static TraitCatalogFixture BuildTraitCatalog()
    {
        return new TraitCatalogFixture(
            new List<TraitDef>
            {
                new()
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
                new()
                {
                    trait_id = "heavy_head",
                    allowed_source_kinds = new Godot.Collections.Array<StringName>
                    {
                        "equipment_roll",
                    },
                },
            }
        );
    }

    private static ItemFixture BuildItemFixture()
    {
        TraitRollGroupDef group = new()
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
                new()
                {
                    trait_id = "heavy_head",
                    weight = 1,
                },
            },
        };

        return new ItemFixture(
            new ItemDef
            {
                item_id = "iron_sword",
                display_name = "Iron Sword",
                item_category = "equipment",
                equipment_type_id = "weapon",
                is_stackable = false,
                max_stack = 1,
                equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
                trait_roll_groups = new Godot.Collections.Array<TraitRollGroupDef> { group },
            }
        );
    }

    private static PartyState BuildPartyWithCapacity(int capacity)
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

    private sealed class TraitCatalogFixture : IDisposable
    {
        private readonly List<TraitDef> _traitDefs;

        public TraitCatalogFixture(List<TraitDef> traitDefs)
        {
            _traitDefs = traitDefs ?? new List<TraitDef>();
        }

        public EquipmentTraitRollService BuildService(
            RandomNumberGenerator rng = null,
            bool ownsRng = false
        )
        {
            return new EquipmentTraitRollService(_traitDefs, rng, ownsRng);
        }

        public void Dispose()
        {
            foreach (TraitDef traitDef in _traitDefs)
                DisposeTraitDef(traitDef);
            _traitDefs.Clear();
        }
    }

    private sealed class ItemFixture : IDisposable
    {
        public readonly ItemDef Item;

        public ItemFixture(ItemDef item)
        {
            Item = item;
        }

        public void Dispose()
        {
            DisposeItemDef(Item);
        }
    }

    private static void DisposeTraitDef(TraitDef traitDef)
    {
        if (traitDef == null)
            return;
        foreach (TraitRollValueSchemaEntry entry in traitDef.roll_value_schema)
            GodotSharpCleanup.DisposeGodotObject(entry);
        traitDef.roll_value_schema.Clear();
        foreach (AttributeModifier modifier in traitDef.attribute_modifiers)
            GodotSharpCleanup.DisposeGodotObject(modifier);
        traitDef.attribute_modifiers.Clear();
        GodotSharpCleanup.DisposeGodotObject(traitDef);
    }

    private static void DisposeItemDef(ItemDef itemDef)
    {
        if (itemDef == null)
            return;
        foreach (TraitRollGroupDef group in itemDef.trait_roll_groups)
        {
            if (group == null)
                continue;
            foreach (TraitRollGroupEntryDef entry in group.entries)
                GodotSharpCleanup.DisposeGodotObject(entry);
            group.entries.Clear();
            GodotSharpCleanup.DisposeGodotObject(group);
        }
        itemDef.trait_roll_groups.Clear();
        foreach (AttributeModifier modifier in itemDef.attribute_modifiers)
            GodotSharpCleanup.DisposeGodotObject(modifier);
        itemDef.attribute_modifiers.Clear();
        GodotSharpCleanup.DisposeGodotObject(itemDef.equip_requirement);
        itemDef.equip_requirement = null;
        GodotSharpCleanup.DisposeGodotObject(itemDef.weapon_profile);
        itemDef.weapon_profile = null;
        GodotSharpCleanup.DisposeGodotObject(itemDef);
    }
}
