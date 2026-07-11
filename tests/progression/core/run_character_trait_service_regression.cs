using System.Collections.Generic;
using Godot;

public partial class run_character_trait_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
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

        AttributeModifierDefinition attack = FindModifier(modifiers, "attack_bonus");
        _test.True(attack != null, "Trait attribute modifiers should include attack_bonus.");
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
        return new CharacterTraitService(BuildTraitDefs().Values, gateway);
    }

    private static Dictionary<StringName, TraitDef> BuildTraitDefs() =>
        new()
        {
            ["identity_watch"] = BuildTrait("identity_watch", "identity", "unique_by_trait"),
            ["character_grit"] = BuildTrait("character_grit", "character", "unique_by_trait"),
            ["fixed_guard"] = BuildTrait("fixed_guard", "equipment_fixed", "unique_by_trait"),
            ["sharp_edge"] = BuildRollTrait("sharp_edge", "stack_by_instance", "amount"),
            ["lucky_roll"] = BuildRollTrait("lucky_roll", "highest_roll", "amount"),
            [
                "additive_power"
            ] = BuildTrait("additive_power", "character", "additive", "attack_bonus", 2),
        };

    private static TraitDef BuildTrait(
        string traitId,
        string sourceKind,
        string stackPolicy,
        string attributeId = "",
        int value = 0
    )
    {
        TraitDef trait = new()
        {
            trait_id = traitId,
            display_name = traitId,
            description = traitId,
            allowed_source_kinds = new Godot.Collections.Array<StringName> { sourceKind },
            effect_type = "attribute_modifier",
            trigger_type = "passive",
            stack_policy = stackPolicy,
            charge_scope = "none",
            charge_reset_timing = "none",
        };
        if (!string.IsNullOrEmpty(attributeId))
        {
            trait.attribute_modifiers.Add(
                new AttributeModifier
                {
                    attribute_id = attributeId,
                    mode = "flat",
                    value = value,
                    value_per_rank = 0,
                }
            );
        }
        return trait;
    }

    private static TraitDef BuildRollTrait(string traitId, string stackPolicy, string compareKey)
    {
        TraitDef trait = BuildTrait(traitId, "equipment_roll", stackPolicy);
        trait.charge_scope = "per_turn";
        trait.charge_reset_timing = "turn_start";
        trait.highest_roll_compare_key = compareKey;
        trait.roll_value_schema.Add(
            new TraitRollValueSchemaEntry
            {
                key = compareKey,
                value_type = "int",
                min_value = 0,
                max_value = 20,
            }
        );
        return trait;
    }

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
        private readonly Dictionary<StringName, ItemDef> _items = new();

        public FakeGateway()
        {
            _member = new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
            };
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

            _items["iron_sword"] = new ItemDef
            {
                item_id = "iron_sword",
                trait_ids = new Godot.Collections.Array<StringName> { "fixed_guard" },
            };

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

        public RaceDef GetRaceDefForTraitAggregation(StringName memberId) =>
            new()
            {
                race_id = "human",
                trait_ids = new Godot.Collections.Array<StringName> { "identity_watch" },
            };

        public SubraceDef GetSubraceDefForTraitAggregation(StringName memberId) => null;

        public BloodlineDef GetBloodlineDefForTraitAggregation(StringName memberId) => null;

        public BloodlineStageDef GetBloodlineStageDefForTraitAggregation(StringName memberId) => null;

        public AscensionDef GetAscensionDefForTraitAggregation(StringName memberId) => null;

        public AscensionStageDef GetAscensionStageDefForTraitAggregation(StringName memberId) => null;

        public PartyMemberState GetMemberStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _member : null;

        public EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _equipment : null;

        public ItemDef GetItemDefForTraitAggregation(StringName itemId) =>
            _items.TryGetValue(itemId, out ItemDef itemDef) ? itemDef : null;
    }
}
