using System;
using System.Collections.Generic;
using Godot;

public partial class run_gear_set_evaluation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestThresholdActivationAnchorFallbackAndStableTraits();
        TestBrokenAndDuplicateMembersDoNotIncreasePieceCount();
        TestWrongMemberSlotAndOccupiedFootprintDoNotCount();
        TestContentValidationRejectsDuplicateAttributeOwnership();
        TestFullSetUsesConfiguredAnchorAndEmitsEveryActiveThreshold();
        TestCharacterTraitProjectionKeepsThresholdSourceAndStableKey();
        TestCharacterManagementAppliesActiveThresholdAttributes();

        RequestTestExit(_test.Finish("Gear set evaluation regression"));
    }

    private void TestThresholdActivationAnchorFallbackAndStableTraits()
    {
        TestFixture fixture = BuildFixture();
        EquipmentState equipment = BuildPartialEquipment();

        GearSetEvaluationSnapshot snapshot = GearSetEvaluationService.Evaluate(
            equipment,
            fixture.Items,
            fixture.GearSets
        );

        _test.Eq(snapshot.ActiveSets.Count, 1, "A contributing set should be queryable.");
        GearSetActivationSummary summary = snapshot.ActiveSets[0];
        _test.Eq(summary.GearSetId, new StringName("test_set"), "Set id should be preserved.");
        _test.Eq(summary.EquippedPieceCount, 2, "Distinct equipped member ids should count once.");
        _test.Eq(summary.TotalPieceCount, 4, "Presentation summary should expose total pieces.");
        _test.Eq(summary.Thresholds.Count, 2, "Presentation summary should expose every threshold.");
        _test.True(summary.Thresholds[0].IsActive, "The two-piece threshold should activate.");
        _test.True(!summary.Thresholds[1].IsActive, "The four-piece threshold should remain inactive.");
        _test.Eq(snapshot.AttributeModifiers.Count, 1, "Only active threshold attributes should emit.");
        _test.Eq(snapshot.AttributeModifiers[0].Value, 3, "The two-piece attribute value should survive projection.");
        _test.Eq(snapshot.DerivedTraitInstances.Count, 1, "Only the active threshold trait should emit.");

        GearSetDerivedTraitInstance derived = snapshot.DerivedTraitInstances[0];
        _test.Eq(
            derived.SourceEquipmentInstanceId,
            new StringName("eq_armor_primary"),
            "Without the configured anchor, the first contributing member in authoring order should own usage."
        );
        _test.Eq(
            derived.EffectiveInstanceKey,
            new StringName("gear_set::test_set::two_piece::test_set_two_trait"),
            "Derived trait instance keys must remain stable and instance-independent."
        );
        _test.Eq(
            fixture.Set.GetThresholdById("two_piece")?.ThresholdId ?? new StringName(""),
            new StringName("two_piece"),
            "Threshold lookup by threshold id should support presentation consumers."
        );
        _test.Eq(
            fixture.Set.GetThresholdByTraitId("test_set_two_trait")?.ThresholdId
                ?? new StringName(""),
            new StringName("two_piece"),
            "Threshold lookup by granted trait id should support presentation consumers."
        );
    }

    private void TestBrokenAndDuplicateMembersDoNotIncreasePieceCount()
    {
        TestFixture fixture = BuildFixture();
        EquipmentState equipment = BuildPartialEquipment();
        equipment.GetEquippedInstance("hands").current_durability = 0;

        GearSetEvaluationSnapshot snapshot = GearSetEvaluationService.Evaluate(
            equipment,
            fixture.Items,
            fixture.GearSets
        );

        _test.Eq(
            snapshot.ActiveSets[0].EquippedPieceCount,
            1,
            "A broken member should not count, and a duplicate copy of another member should not replace it."
        );
        _test.True(
            !snapshot.ActiveSets[0].Thresholds[0].IsActive,
            "The threshold should deactivate when only one distinct intact member remains."
        );
        _test.Eq(snapshot.AttributeModifiers.Count, 0, "Inactive thresholds should emit no attributes.");
        _test.Eq(snapshot.DerivedTraitInstances.Count, 0, "Inactive thresholds should emit no traits.");
    }

    private void TestWrongMemberSlotAndOccupiedFootprintDoNotCount()
    {
        TestFixture fixture = BuildFixture();
        var wrongEntrySlot = new EquipmentState();
        Equip(wrongEntrySlot, "ring_1", "test_helm", "eq_helm_wrong_slot");
        Equip(wrongEntrySlot, "hands", "test_gloves", "eq_gloves_valid");

        GearSetEvaluationSnapshot wrongSlotSnapshot = GearSetEvaluationService.Evaluate(
            wrongEntrySlot,
            fixture.Items,
            fixture.GearSets
        );
        _test.Eq(wrongSlotSnapshot.ActiveSets.Count, 1, "The valid member should keep the set visible.");
        _test.Eq(
            wrongSlotSnapshot.ActiveSets[0].EquippedPieceCount,
            1,
            "A member in a globally valid but item-disallowed entry slot must not count."
        );
        _test.False(
            wrongSlotSnapshot.ActiveSets[0].Thresholds[0].IsActive,
            "An item-disallowed slot must not activate a threshold."
        );

        var wrongFootprint = new EquipmentState();
        bool stored = wrongFootprint.SetEquippedEntry(
            "head",
            "test_helm",
            new StringName[] { "head", "body" },
            EquipmentInstanceState.CreateInstance("test_helm", "eq_helm_wrong_footprint")
        );
        if (!stored)
            throw new InvalidOperationException("Could not build the malformed occupied-slot fixture.");
        Equip(wrongFootprint, "hands", "test_gloves", "eq_gloves_valid_footprint");

        GearSetEvaluationSnapshot wrongFootprintSnapshot = GearSetEvaluationService.Evaluate(
            wrongFootprint,
            fixture.Items,
            fixture.GearSets
        );
        _test.Eq(
            wrongFootprintSnapshot.ActiveSets[0].EquippedPieceCount,
            1,
            "A member whose occupied slots exceed its canonical item footprint must not count."
        );
        _test.False(
            wrongFootprintSnapshot.ActiveSets[0].Thresholds[0].IsActive,
            "A malformed occupied-slot footprint must not activate a threshold."
        );
    }

    private void TestContentValidationRejectsDuplicateAttributeOwnership()
    {
        TestFixture fixture = BuildFixture();
        var directAndTraitModifier = new AttributeModifierDefinition(
            "strength",
            "flat",
            1,
            0,
            "fixture",
            "fixture"
        );
        var traitDefinitions = new Dictionary<StringName, TraitDefinition>
        {
            ["test_overlap_trait"] = BuildSetTraitDefinition(
                "test_overlap_trait",
                new[] { directAndTraitModifier }
            ),
        };
        GearSetImportModel overlapImport = new(
            "invalid_attribute_overlap_set",
            "Invalid Attribute Overlap",
            "Fixture only.",
            new[] { "test_helm" },
            "test_helm",
            new[]
            {
                new GearSetThresholdImportModel(
                    "one_piece",
                    1,
                    "One Piece",
                    "Invalid overlap fixture.",
                    Array.Empty<string>(),
                    new[]
                    {
                        new GearSetAttributeModifierImportModel("strength", "flat", 1, 0),
                    },
                    new[] { "test_overlap_trait" }
                ),
            }
        );
        string overlapJson = GearSetImportCanonicalJson.WriteDocument(
            new ContentCanonicalJsonWriter(),
            "attribute_overlap_fixture",
            new[] { overlapImport }
        );
        using var registry = new GearSetContentRegistry(
            "res://virtual/gear_set_attribute_overlap",
            new SingleJsonSourceReader(
                new ContentJsonSourceText("invalid_overlap_set.json", overlapJson)
            )
        );
        registry.Rebuild();
        IReadOnlyList<string> errors = registry.ValidateTyped(
            fixture.Items,
            traitDefinitions,
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>()
        );
        bool foundOverlap = false;
        foreach (string error in errors)
        {
            if (!error.Contains("duplicates direct threshold attribute strength", StringComparison.Ordinal))
                continue;
            foundOverlap = true;
            break;
        }
        _test.True(
            foundOverlap,
            "Gear-set content validation must reject an attribute authored both directly and through a granted trait."
        );
    }

    private void TestFullSetUsesConfiguredAnchorAndEmitsEveryActiveThreshold()
    {
        TestFixture fixture = BuildFixture();
        EquipmentState equipment = BuildPartialEquipment();
        Equip(equipment, "head", "test_helm", "eq_configured_anchor");
        Equip(equipment, "feet", "test_boots", "eq_boots");

        GearSetEvaluationSnapshot snapshot = GearSetEvaluationService.Evaluate(
            equipment,
            fixture.Items,
            fixture.GearSets
        );

        _test.Eq(snapshot.ActiveSets[0].EquippedPieceCount, 4, "All four distinct members should count.");
        _test.True(snapshot.ActiveSets[0].Thresholds[0].IsActive, "Lower thresholds stay active at full set.");
        _test.True(snapshot.ActiveSets[0].Thresholds[1].IsActive, "The full-set threshold should activate.");
        _test.Eq(snapshot.AttributeModifiers.Count, 2, "Every active threshold should emit its attributes.");
        _test.Eq(snapshot.DerivedTraitInstances.Count, 2, "Every active threshold should emit its granted trait.");
        foreach (GearSetDerivedTraitInstance derived in snapshot.DerivedTraitInstances)
        {
            _test.Eq(
                derived.SourceEquipmentInstanceId,
                new StringName("eq_configured_anchor"),
                "When equipped, the configured anchor should own all set-threshold ability usage."
            );
        }
        _test.Eq(
            snapshot.DerivedTraitInstances[1].EffectiveInstanceKey,
            new StringName("gear_set::test_set::four_piece::test_set_four_trait"),
            "The full-set trait should use its threshold-scoped stable key."
        );
    }

    private void TestCharacterTraitProjectionKeepsThresholdSourceAndStableKey()
    {
        TestFixture fixture = BuildFixture();
        EquipmentState equipment = BuildPartialEquipment();
        var gateway = new GearSetTraitGateway(equipment, fixture.Items);
        var service = new CharacterTraitService(
            new[]
            {
                BuildSetTraitDefinition("test_set_two_trait"),
                BuildSetTraitDefinition("test_set_four_trait"),
            },
            gateway,
            fixture.GearSets
        );

        EffectiveTraitSet effectiveTraits = service.BuildEffectiveTraits("hero", equipment);
        _test.True(
            effectiveTraits.TryGetByKey(
                "gear_set::test_set::two_piece::test_set_two_trait",
                out EffectiveTraitInstance projected
            ),
            "Character trait aggregation should keep the threshold-scoped effective key."
        );
        _test.True(
            projected?.SourceKind == TraitSourceKind.GearSetThreshold,
            "Character trait aggregation should preserve gear_set_threshold provenance."
        );
        _test.Eq(
            projected?.SourceId ?? new StringName(""),
            new StringName("eq_armor_primary"),
            "The effective trait source id should bind the fallback physical equipment instance."
        );
        List<BattleEffectiveTraitInstanceState> battleTraits =
            effectiveTraits.ToBattleEffectiveInstances();
        _test.Eq(battleTraits.Count, 1, "The active set threshold should reach battle projection.");
        if (battleTraits.Count > 0)
        {
            _test.Eq(
                battleTraits[0].source_type,
                new StringName("gear_set_threshold"),
                "Battle projection should receive the typed gear-set threshold source."
            );
        }

        var unit = new BattleUnitState();
        unit.SetEquipmentView(equipment);
        unit.ReplaceEffectiveTraitsTyped(battleTraits);
        TraitDefinition twoPieceTrait = BuildSetTraitDefinition("test_set_two_trait");
        var binding = new EquipmentAbilityBindingDefinition
        {
            BindingId = "binding.test_set_two_trait",
            TraitId = twoPieceTrait.TraitId,
            AllowedSourceKinds = EquipmentAbilityReadOnlySet<StringName>.From(
                new StringName[] { "gear_set_threshold" }
            ),
        };
        BattleEquipmentAbilityProjectionResult abilityProjection =
            BattleEquipmentAbilityProjectionService.ProjectPlayerPersistent(
                unit,
                new Dictionary<StringName, EquipmentAbilityBindingDefinition>
                {
                    [binding.BindingId] = binding,
                },
                new Dictionary<StringName, TraitDefinition>
                {
                    [twoPieceTrait.TraitId] = twoPieceTrait,
                },
                fixture.Items
            );
        _test.Eq(
            abilityProjection.Sources.Count,
            1,
            "An allowed threshold trait should project one equipment ability source."
        );
        if (abilityProjection.Sources.Count > 0)
        {
            _test.True(
                abilityProjection.Sources[0].SourceKind
                    == EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold,
                "Threshold abilities should retain their distinct persistent source kind."
            );
            _test.Eq(
                abilityProjection.Sources[0].SourceEquipmentInstanceId,
                new StringName("eq_armor_primary"),
                "Threshold ability writeback should bind to the selected physical equipment instance."
            );
        }
    }

    private void TestCharacterManagementAppliesActiveThresholdAttributes()
    {
        TestFixture fixture = BuildFixture();
        UnitProgress progress = new()
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        progress.unit_base_attributes.SetAttributeValue("strength", 10);
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = progress,
            equipment_state = BuildPartialEquipment(),
        };
        var party = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
        };
        party.SetMemberState(member);

        var traitDefinitions = new Dictionary<StringName, TraitDefinition>
        {
            ["test_set_two_trait"] = BuildSetTraitDefinition("test_set_two_trait"),
            ["test_set_four_trait"] = BuildSetTraitDefinition("test_set_four_trait"),
        };
        CharacterManagementModule manager = new();
        try
        {
            manager.setup(
                party,
                new Dictionary<StringName, SkillDefinition>(),
                new Dictionary<StringName, ProfessionDefinition>(),
                new Dictionary<StringName, AchievementDefinition>(),
                fixture.Items,
                new Dictionary<StringName, QuestDefinition>(),
                traitDefinitions,
                null,
                new ProgressionIdentityCatalogData(),
                fixture.GearSets
            );

            AttributeSourceContext context = manager.build_attribute_source_context("hero");
            _test.Eq(
                context.equipment_state.Count,
                1,
                "Character attribute context should include active direct threshold modifiers."
            );
            _test.Eq(
                context.equipment_state[0].SourceId,
                new StringName("gear_set::test_set::two_piece"),
                "Direct threshold attributes should retain threshold provenance."
            );
            _test.Eq(
                manager.GetMemberAttributeSnapshot("hero").GetValue("strength"),
                13,
                "Final character attributes should apply the active set threshold modifier."
            );
        }
        finally
        {
            manager.Dispose();
        }
    }

    private static TestFixture BuildFixture()
    {
        var twoPieceModifier = new AttributeModifierDefinition(
            "strength",
            "flat",
            3,
            0,
            "gear_set",
            "gear_set::test_set::two_piece"
        );
        var fourPieceModifier = new AttributeModifierDefinition(
            "willpower",
            "flat",
            2,
            0,
            "gear_set",
            "gear_set::test_set::four_piece"
        );
        var twoPiece = new GearSetThresholdDefinition(
            "two_piece",
            2,
            "Two Pieces",
            "Test two-piece threshold.",
            Array.Empty<StringName>(),
            new[] { twoPieceModifier },
            new StringName[] { "test_set_two_trait" }
        );
        var fourPiece = new GearSetThresholdDefinition(
            "four_piece",
            4,
            "Four Pieces",
            "Test full-set threshold.",
            new StringName[] { "test_helm" },
            new[] { fourPieceModifier },
            new StringName[] { "test_set_four_trait" }
        );
        var set = new GearSetDefinition(
            "test_set",
            "Test Set",
            "Behavior fixture.",
            new StringName[] { "test_helm", "test_armor", "test_gloves", "test_boots" },
            "test_helm",
            new[] { twoPiece, fourPiece }
        );
        var items = new Dictionary<StringName, ItemDefinition>
        {
            ["test_helm"] = BuildEquipmentItem("test_helm", "head"),
            ["test_armor"] = BuildEquipmentItem("test_armor", "body"),
            ["test_gloves"] = BuildEquipmentItem("test_gloves", "hands"),
            ["test_boots"] = BuildEquipmentItem("test_boots", "feet"),
        };
        return new TestFixture(
            set,
            items,
            new Dictionary<StringName, GearSetDefinition> { [set.GearSetId] = set }
        );
    }

    private static EquipmentState BuildPartialEquipment()
    {
        var equipment = new EquipmentState();
        Equip(equipment, "body", "test_armor", "eq_armor_primary");
        Equip(equipment, "ring_1", "test_armor", "eq_armor_duplicate");
        Equip(equipment, "hands", "test_gloves", "eq_gloves");
        return equipment;
    }

    private static void Equip(
        EquipmentState equipment,
        StringName slotId,
        StringName itemId,
        StringName instanceId
    )
    {
        bool equipped = equipment.SetEquippedEntry(
            slotId,
            itemId,
            new[] { slotId },
            EquipmentInstanceState.CreateInstance(itemId, instanceId)
        );
        if (!equipped)
            throw new InvalidOperationException($"Could not equip {itemId} in {slotId}.");
    }

    private static ItemDefinition BuildEquipmentItem(StringName itemId, string slotId) =>
        new TestItemDefinitionBuilder
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Armor,
            is_stackable = false,
            max_stack = 1,
            equipment_slot_ids = new Godot.Collections.Array<string> { slotId },
        }.ToDefinition();

    private static TraitDefinition BuildSetTraitDefinition(
        StringName traitId,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers = null
    ) =>
        new(
            traitId,
            traitId.ToString(),
            traitId.ToString(),
            Array.Empty<StringName>(),
            new StringName[] { "gear_set_threshold" },
            "attribute_modifier",
            "passive",
            "unique_by_trait",
            "none",
            "none",
            "",
            0,
            0,
            attributeModifiers ?? Array.Empty<AttributeModifierDefinition>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<StringName>(),
            Array.Empty<TraitDamageResistanceEntryDefinition>(),
            Array.Empty<TraitSaveBonusEntryDefinition>(),
            Array.Empty<TraitPassiveStatusEffectDefinition>(),
            Array.Empty<TraitRollValueSchemaEntryDefinition>()
        );

    private sealed class GearSetTraitGateway : CharacterTraitService.ICharacterTraitGateway
    {
        private readonly PartyMemberState _member = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        private readonly EquipmentState _equipment;
        private readonly IReadOnlyDictionary<StringName, ItemDefinition> _items;

        internal GearSetTraitGateway(
            EquipmentState equipment,
            IReadOnlyDictionary<StringName, ItemDefinition> items
        )
        {
            _equipment = equipment;
            _items = items;
        }

        public RaceDefinition GetRaceDefForTraitAggregation(StringName memberId) => null;

        public SubraceDefinition GetSubraceDefForTraitAggregation(StringName memberId) => null;

        public BloodlineDefinition GetBloodlineDefForTraitAggregation(StringName memberId) => null;

        public BloodlineStageDefinition GetBloodlineStageDefForTraitAggregation(
            StringName memberId
        ) => null;

        public AscensionDefinition GetAscensionDefForTraitAggregation(StringName memberId) => null;

        public AscensionStageDefinition GetAscensionStageDefForTraitAggregation(
            StringName memberId
        ) => null;

        public PartyMemberState GetMemberStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _member : null;

        public EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId) =>
            memberId == _member.member_id ? _equipment : null;

        public ItemDefinition GetItemDefForTraitAggregation(StringName itemId) =>
            _items.TryGetValue(itemId, out ItemDefinition definition) ? definition : null;

        public IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsForTraitAggregation() =>
            _items;
    }

    private sealed class TestFixture
    {
        internal TestFixture(
            GearSetDefinition set,
            IReadOnlyDictionary<StringName, ItemDefinition> items,
            IReadOnlyDictionary<StringName, GearSetDefinition> gearSets
        )
        {
            Set = set;
            Items = items;
            GearSets = gearSets;
        }

        internal GearSetDefinition Set { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> Items { get; }
        internal IReadOnlyDictionary<StringName, GearSetDefinition> GearSets { get; }
    }

    private sealed class SingleJsonSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal SingleJsonSourceReader(params ContentJsonSourceText[] sources) =>
            _sources = sources;

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) =>
            _sources;
    }
}
