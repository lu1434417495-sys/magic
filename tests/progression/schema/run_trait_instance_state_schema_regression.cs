using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_trait_instance_state_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStrictPayloadRoundTripAndTypedReaders();
        TestRejectsMissingExtraAndWrongTypedPayloadFields();
        TestRejectsPersistedInstanceSourcesWithoutSourceId();
        TestRejectsWrongSourceForCollection();
        TestValidateAgainstDefRequiresExactRollSchema();
        TestDuplicateDeepCopiesRollValues();
        RequestTestExit(_test.Finish("Trait instance state schema regression"));
    }

    private void TestStrictPayloadRoundTripAndTypedReaders()
    {
        TraitInstanceState state = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rank: 2,
            stacks: 3,
            rollValues: TraitTestData.RollValues(
                TraitTestData.IntRoll("amount", 4),
                TraitTestData.StringNameRoll("damage_tag", "physical_slash"),
                TraitTestData.BoolRoll("enabled", true)
            )
        );

        GDictionary payload = state.ToDictionary();
        TraitInstanceState restored = TraitInstanceState.FromDictionary(payload);

        _test.True(restored != null, "Canonical trait instance payload should parse.");
        _test.Eq(
            restored.trait_instance_id,
            new StringName("eq_000001_t01"),
            "trait_instance_id should round-trip."
        );
        _test.Eq(
            restored.GetIntRoll("amount", -1),
            4,
            "int roll reader should read normalized key."
        );
        _test.Eq(
            restored.GetStringNameRoll("damage_tag", "missing"),
            new StringName("physical_slash"),
            "string_name roll reader should read string values."
        );
        _test.True(restored.GetBoolRoll("enabled", false), "bool roll reader should read bool values.");
        _test.Eq(restored.GetIntRoll("missing", 9), 9, "missing int roll should use fallback.");
    }

    private void TestRejectsMissingExtraAndWrongTypedPayloadFields()
    {
        GDictionary payload = TraitInstanceState.Create(
            "char_trait_001",
            "battle_hardened",
            TraitSourceKind.Character,
            "reward_intro"
        ).ToDictionary();

        GDictionary missing = (GDictionary)payload.Duplicate(true);
        missing.Remove("roll_values");
        _test.True(
            TraitInstanceState.FromDictionary(missing) == null,
            "Missing required field should reject trait instance payload."
        );

        GDictionary extra = (GDictionary)payload.Duplicate(true);
        extra["extra"] = true;
        _test.True(
            TraitInstanceState.FromDictionary(extra) == null,
            "Extra field should reject trait instance payload."
        );

        GDictionary wrongType = (GDictionary)payload.Duplicate(true);
        wrongType["rank"] = "1";
        _test.True(TraitInstanceState.FromDictionary(wrongType) == null, "rank must be an int.");

        GDictionary missingInstanceId = (GDictionary)payload.Duplicate(true);
        missingInstanceId["trait_instance_id"] = "";
        _test.True(
            TraitInstanceState.FromDictionary(missingInstanceId) == null,
            "character source requires a stable trait_instance_id."
        );
    }

    private void TestRejectsPersistedInstanceSourcesWithoutSourceId()
    {
        GDictionary characterPayload = TraitInstanceState.Create(
            "char_trait_001",
            "battle_hardened",
            TraitSourceKind.Character,
            "reward_intro"
        ).ToDictionary();
        characterPayload["source_id"] = "";
        _test.True(
            TraitInstanceState.FromDictionary(characterPayload) == null,
            "character source requires a stable source_id."
        );

        GDictionary equipmentPayload = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001"
        ).ToDictionary();
        equipmentPayload["source_id"] = "   ";
        _test.True(
            TraitInstanceState.FromDictionary(equipmentPayload) == null,
            "equipment_roll source requires a stable source_id."
        );
    }

    private void TestRejectsWrongSourceForCollection()
    {
        var array = new Godot.Collections.Array
        {
            TraitInstanceState.Create(
                "char_trait_001",
                "battle_hardened",
                TraitSourceKind.Character,
                "reward_intro"
            ).ToDictionary(),
        };

        _test.True(
            TraitInstanceCollection.FromPayloadArray(Variant.From(array), TraitSourceKind.Character) != null,
            "Expected source kind should parse."
        );
        _test.True(
            TraitInstanceCollection.FromPayloadArray(Variant.From(array), TraitSourceKind.EquipmentRoll) == null,
            "Wrong source kind should reject the full collection."
        );
    }

    private void TestValidateAgainstDefRequiresExactRollSchema()
    {
        TraitDef authoredDef = new()
        {
            trait_id = "sharp_edge",
            display_name = "Sharp Edge",
            description = "Roll schema fixture.",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "equipment_roll" },
            effect_type = "attribute_modifier",
            roll_value_schema = new Godot.Collections.Array<TraitRollValueSchemaEntry>
            {
                new() { key = "amount", value_type = "int", min_value = 1, max_value = 6 },
                new()
                {
                    key = "damage_tag",
                    value_type = "string_name",
                    allowed_values = new Godot.Collections.Array<StringName>
                    {
                        "physical_slash",
                        "physical_pierce",
                    },
                },
                new() { key = "enabled", value_type = "bool" },
            },
        };
        TraitDefinition def = TestProgressionDefinitionProjection.Trait(authoredDef);

        TraitInstanceState valid = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rollValues: TraitTestData.RollValues(
                TraitTestData.IntRoll("amount", 4),
                TraitTestData.StringNameRoll("damage_tag", "physical_slash"),
                TraitTestData.BoolRoll("enabled", true)
            )
        );
        _test.Eq(valid.ValidateAgainstDef(def), "", "Exact roll schema should validate.");

        TraitInstanceState missing = valid.DuplicateState();
        missing.RemoveRoll("enabled");
        _test.True(
            missing.ValidateAgainstDef(def).Contains("missing roll key"),
            "Missing schema key should fail."
        );

        TraitInstanceState outOfRange = valid.DuplicateState();
        outOfRange.SetIntRoll("amount", 99);
        _test.True(
            outOfRange.ValidateAgainstDef(def).Contains("out of range"),
            "Out-of-range int roll should fail."
        );

        TraitInstanceState unexpected = valid.DuplicateState();
        unexpected.SetIntRoll("extra", 1);
        _test.True(
            unexpected.ValidateAgainstDef(def).Contains("unexpected roll key"),
            "Unexpected roll key should fail."
        );
    }

    private void TestDuplicateDeepCopiesRollValues()
    {
        TraitInstanceState source = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rollValues: TraitTestData.RollValues(TraitTestData.IntRoll("amount", 4))
        );
        TraitInstanceState copy = source.DuplicateState();
        copy.SetIntRoll("amount", 6);

        _test.Eq(source.GetIntRoll("amount", -1), 4, "DuplicateState should deep-copy roll_values.");
        _test.Eq(copy.GetIntRoll("amount", -1), 6, "copy should keep its own roll_values.");
    }
}
