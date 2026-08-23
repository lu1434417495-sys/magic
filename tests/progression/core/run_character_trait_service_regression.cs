using System.Collections.Generic;
using Godot;

public partial class run_character_trait_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAggregatesIdentityCharacterAndEquipmentSources();
        TestStackPoliciesCollapseOrPreserveKeys();
        TestTraitAttributeModifiersUseEffectiveSourceKeys();
        RequestTestExit(_test.Finish("Character trait service regression"));
    }

    private void TestAggregatesIdentityCharacterAndEquipmentSources()
    {
        var service = BuildService();
        EffectiveTraitSet set = service.BuildEffectiveTraits("hero");

        _test.True(
            set.TryGetByKey("identity_watch", out _),
            "unique identity trait should collapse to trait_id key."
        );
        _test.True(
            set.TryGetByKey("character_grit", out _),
            "character unique trait should collapse to trait_id key."
        );
        _test.True(
            set.TryGetByKey("fixed_guard", out _),
            "equipment fixed unique trait should collapse to trait_id key."
        );
        _test.True(
            set.TryGetByKey("eq_000001_t01", out _),
            "stack_by_instance equipment roll should keep trait instance id key."
        );
    }

    private void TestStackPoliciesCollapseOrPreserveKeys()
    {
        var service = BuildService();
        EffectiveTraitSet set = service.BuildEffectiveTraits("hero");

        _test.Eq(
            set.GetByTraitId("lucky_roll")[0].TraitInstance.GetIntRoll("amount", -1),
            6,
            "highest_roll should keep the highest roll value."
        );
        _test.Eq(
            set.GetByTraitId("additive_power")[0].Stacks,
            2,
            "additive should sum stacks into one effective instance."
        );
        _test.Eq(
            set.GetByTraitId("sharp_edge").Count,
            1,
            "stack_by_instance should preserve the equipment roll entry."
        );
    }

    private void TestTraitAttributeModifiersUseEffectiveSourceKeys()
    {
        var service = BuildService();
        EffectiveTraitSet set = service.BuildEffectiveTraits("hero");
        IReadOnlyList<AttributeModifierDefinition> modifiers =
            service.ResolveTraitAttributeModifiers(set);

        AttributeModifierDefinition attack = FindModifier(modifiers, "armor_class");
        _test.True(attack != null, "Trait attribute modifiers should include armor_class.");
        _test.Eq(
            attack.Value,
            4,
            "additive trait modifier should multiply base value by effective stacks."
        );
        _test.Eq(
            attack.SourceType,
            new StringName("trait_character"),
            "character trait modifier should use trait source type."
        );
        _test.Eq(
            attack.SourceId,
            new StringName("additive_power"),
            "collapsed modifier source_id should be final effective key."
        );
    }

    private static CharacterTraitService BuildService()
    {
        var gateway = new FakeGateway();
        return new CharacterTraitService(
            BuildTraitDefs().Values,
            gateway
        );
    }

    private static Dictionary<StringName, TraitDefinition> BuildTraitDefs() =>
        new()
        {
            ["identity_watch"] = BuildTrait("identity_watch", "identity", "unique_by_trait"),
            ["character_grit"] = BuildTrait("character_grit", "character", "unique_by_trait"),
            ["fixed_guard"] = BuildTrait("fixed_guard", "equipment_fixed", "unique_by_trait"),
            ["sharp_edge"] = BuildRollTrait("sharp_edge", "stack_by_instance", "amount"),
            ["lucky_roll"] = BuildRollTrait("lucky_roll", "highest_roll", "amount"),
            [
                "additive_power"
            ] = BuildTrait("additive_power", "character", "additive", "armor_class", 2),
        };

    private static TraitDefinition BuildTrait(
        string traitId,
        string sourceKind,
        string stackPolicy,
        string attributeId = "",
        int value = 0
    )
    {
        IReadOnlyList<TraitAttributeModifierImportModel> modifiers =
            string.IsNullOrEmpty(attributeId)
                ? System.Array.Empty<TraitAttributeModifierImportModel>()
                : new[]
                {
                    new TraitAttributeModifierImportModel(
                        attributeId,
                        "flat",
                        value,
                        0,
                        "",
                        ""
                    ),
                };
        return TraitTestData.Definition(
            traitId,
            new[] { sourceKind },
            stackPolicy: stackPolicy,
            attributeModifiers: modifiers
        );
    }

    private static TraitDefinition BuildRollTrait(
        string traitId,
        string stackPolicy,
        string compareKey
    ) => TraitTestData.Definition(
        traitId,
        new[] { "equipment_roll" },
        stackPolicy: stackPolicy,
        chargeScope: "per_turn",
        chargeResetTiming: "turn_start",
        highestRollCompareKey: compareKey,
        rollValueSchema: new[]
        {
            new TraitRollValueSchemaEntryImportModel(
                compareKey,
                "int",
                0,
                20,
                System.Array.Empty<string>()
            ),
        }
    );

    private static AttributeModifierDefinition FindModifier(
        IEnumerable<AttributeModifierDefinition> modifiers,
        StringName attributeId
    )
    {
        if (modifiers == null)
            return null;
        foreach (AttributeModifierDefinition modifier in modifiers)
        {
            if (modifier != null && modifier.AttributeId == attributeId)
                return modifier;
        }
        return null;
    }

    private sealed class FakeGateway : CharacterTraitService.ICharacterTraitGateway
    {
        private readonly PartyMemberState _member;
        private readonly EquipmentState _equipment;
        private readonly Dictionary<StringName, ItemDefinition> _items = new();
        private readonly RaceDefinition _raceDefinition;

        public FakeGateway()
        {
            _member = new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
            };
            _raceDefinition = new RaceDefinition(
                "human",
                "Human",
                "Test-only human race.",
                "",
                "",
                System.Array.Empty<StringName>(),
                "medium",
                6,
                System.Array.Empty<AttributeModifierDefinition>(),
                new[] { new StringName("identity_watch") },
                System.Array.Empty<RacialGrantedSkillDefinition>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<StringName>(),
                new Dictionary<StringName, StringName>(),
                System.Array.Empty<StringName>(),
                System.Array.Empty<string>()
            );
            _member.trait_instances.Add(
                TraitInstanceState.Create(
                    "hero_trait_001",
                    "character_grit",
                    TraitSourceKind.Character,
                    "hero"
                )
            );
            _member.trait_instances.Add(
                TraitInstanceState.Create(
                    "hero_trait_002",
                    "additive_power",
                    TraitSourceKind.Character,
                    "hero"
                )
            );
            _member.trait_instances.Add(
                TraitInstanceState.Create(
                    "hero_trait_003",
                    "additive_power",
                    TraitSourceKind.Character,
                    "hero"
                )
            );

            TestItemDefinitionBuilder itemResource = new()
            {
                item_id = "iron_sword",
                trait_ids = new Godot.Collections.Array<StringName> { "fixed_guard" },
            };
            _items["iron_sword"] = itemResource.ToDefinition();

            EquipmentInstanceState equipmentInstance =
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_000001");
            equipmentInstance.trait_instances.Add(
                TraitInstanceState.Create(
                    "eq_000001_t01",
                    "sharp_edge",
                    TraitSourceKind.EquipmentRoll,
                    "eq_000001",
                    rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 4))
                )
            );
            equipmentInstance.trait_instances.Add(
                TraitInstanceState.Create(
                    "eq_000001_t02",
                    "lucky_roll",
                    TraitSourceKind.EquipmentRoll,
                    "eq_000001",
                    rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 3))
                )
            );
            equipmentInstance.trait_instances.Add(
                TraitInstanceState.Create(
                    "eq_000001_t03",
                    "lucky_roll",
                    TraitSourceKind.EquipmentRoll,
                    "eq_000001",
                    rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 6))
                )
            );

            _equipment = new EquipmentState();
            _equipment.SetEquippedEntry(
                "main_hand",
                "iron_sword",
                new[] { new StringName("main_hand") },
                equipmentInstance
            );
        }

        public RaceDefinition GetRaceDefForTraitAggregation(StringName memberId) =>
            _raceDefinition;

        public SubraceDefinition GetSubraceDefForTraitAggregation(StringName memberId) => null;

        public BloodlineDefinition GetBloodlineDefForTraitAggregation(StringName memberId) => null;

        public BloodlineStageDefinition GetBloodlineStageDefForTraitAggregation(StringName memberId) => null;

        public AscensionDefinition GetAscensionDefForTraitAggregation(StringName memberId) => null;

        public AscensionStageDefinition GetAscensionStageDefForTraitAggregation(StringName memberId) => null;

        public PartyMemberState GetMemberStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _member : null;

        public EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _equipment : null;

        public ItemDefinition GetItemDefForTraitAggregation(StringName itemId) =>
            _items.TryGetValue(itemId, out ItemDefinition itemDefinition)
                ? itemDefinition
                : null;

        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsForTraitAggregation() =>
            _items;
    }
}
