using Godot;

public partial class run_effective_trait_set_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTraitSetLookupsAndSortedBattleProjection();
        Quit(_test.Finish("Effective trait set regression"));
    }

    private void TestTraitSetLookupsAndSortedBattleProjection()
    {
        TraitDef sharp = BuildTrait("sharp_edge", "equipment_roll");
        TraitDef guarded = BuildTrait("guarded_grip", "equipment_fixed");
        EffectiveTraitSet set = new(
            new System.Collections.Generic.List<EffectiveTraitInstance>
            {
                new()
                {
                    TraitId = "sharp_edge",
                    TraitDef = sharp,
                    SourceKind = TraitSourceKind.EquipmentRoll,
                    SourceId = "eq_000002",
                    EffectiveInstanceKey = "eq_000002_t01",
                    StackPolicy = TraitStackPolicyKind.StackByInstance,
                    ChargeScope = TraitChargeScopeKind.PerTurn,
                    ChargeResetTiming = TraitChargeResetTimingKind.TurnStart,
                    Rank = 2,
                    Stacks = 1,
                    RollValues = TraitTestData.RollValues(TraitTestData.IntRoll("amount", 4)),
                },
                new()
                {
                    TraitId = "guarded_grip",
                    TraitDef = guarded,
                    SourceKind = TraitSourceKind.EquipmentFixed,
                    SourceId = "eq_000001",
                    EffectiveInstanceKey = "guarded_grip",
                    StackPolicy = TraitStackPolicyKind.UniqueByTrait,
                    ChargeScope = TraitChargeScopeKind.None,
                    ChargeResetTiming = TraitChargeResetTimingKind.None,
                    Rank = 1,
                    Stacks = 1,
                    RollValues = TraitTestData.RollValues(),
                },
            }
        );

        _test.True(
            set.TryGetByKey("eq_000002_t01", out EffectiveTraitInstance byKey),
            "Trait set should index by effective key."
        );
        _test.Eq(byKey.TraitId, new StringName("sharp_edge"), "Key lookup should return matching trait.");
        _test.Eq(set.GetByTraitId("sharp_edge").Count, 1, "Trait id lookup should return matching instances.");

        var ids = set.DeriveTraitIds();
        _test.Eq(ids.Count, 2, "Derived ids should be unique.");
        _test.Eq(ids[0], new StringName("guarded_grip"), "Derived ids should sort by ordinal text.");
        _test.Eq(ids[1], new StringName("sharp_edge"), "Derived ids should sort by ordinal text.");

        Godot.Collections.Array<BattleEffectiveTraitInstanceState> projection =
            set.ToBattleEffectiveInstances();
        _test.Eq(projection.Count, 2, "Battle projection should include both instances.");
        BattleEffectiveTraitInstanceState first = projection[0];
        BattleEffectiveTraitInstanceState second = projection[1];
        _test.Eq(
            first.effective_instance_key,
            new StringName("eq_000002_t01"),
            "Battle projection should sort by effective key."
        );
        _test.Eq(
            second.effective_instance_key,
            new StringName("guarded_grip"),
            "Battle projection should sort by effective key."
        );
        _test.Eq(
            first.effect_type,
            new StringName("attribute_modifier"),
            "Projection should denormalize effect type."
        );
        _test.Eq(
            first.source_type,
            new StringName("equipment_roll"),
            "Projection should project source kind."
        );
        _test.Eq(
            first.roll_values[0].int_value,
            4,
            "Projection should duplicate roll values."
        );
    }

    private static TraitDef BuildTrait(string traitId, string sourceKind) =>
        new()
        {
            trait_id = traitId,
            display_name = traitId,
            description = traitId,
            allowed_source_kinds = new Godot.Collections.Array<StringName> { sourceKind },
            effect_type = "attribute_modifier",
            trigger_type = "passive",
            stack_policy = sourceKind == "equipment_roll" ? "stack_by_instance" : "unique_by_trait",
            charge_scope = sourceKind == "equipment_roll" ? "per_turn" : "none",
            charge_reset_timing = sourceKind == "equipment_roll" ? "turn_start" : "none",
        };

}
