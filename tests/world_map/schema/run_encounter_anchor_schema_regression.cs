using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_encounter_anchor_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestValidRoundtripPreservesCurrentSchema();
        TestEmptyProfileFieldIsRejected();
        TestExtraFieldsAreRejected();
        TestMissingRequiredFieldIsRejected();
        TestWrongFieldTypeIsRejected();
        TestEmptyRequiredIdentityFieldsAreRejected();
        TestInvalidEncounterKindIsRejected();

        RequestTestExit(_test.Finish("Encounter anchor schema regression"));
    }

    private void TestValidRoundtripPreservesCurrentSchema()
    {
        EncounterAnchorData encounterAnchor = new()
        {
            entity_id = "wild_den_roundtrip",
            display_name = "Wild Den",
            world_coord = new Vector2I(12, 7),
            faction_id = "hostile",
            region_tag = "north_wilds",
            vision_range = 3,
            is_cleared = true,
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement),
            encounter_profile_id = "wolf_den",
            growth_stage = 2,
            suppressed_until_step = 11,
        };

        using GodotProjectionLease<GDictionary> sourceLease =
            WorldMapDataProjection.ProjectLease(encounterAnchor);
        EncounterAnchorData restoredAnchor = EncounterAnchorData.FromDictionary(sourceLease.Value);
        _test.True(restoredAnchor != null, "valid projection payload should deserialize.");
        if (restoredAnchor == null)
        {
            return;
        }
        using GodotProjectionLease<GDictionary> restoredLease =
            WorldMapDataProjection.ProjectLease(restoredAnchor);
        AssertDictionaryEq(
            restoredLease.Value,
            sourceLease.Value,
            "valid roundtrip should preserve all serialized fields."
        );
    }

    private void TestEmptyProfileFieldIsRejected()
    {
        StringName[] encounterKinds =
        {
            EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
            EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement),
        };
        foreach (StringName encounterKind in encounterKinds)
        {
            GDictionary payload = BuildValidPayload();
            payload["encounter_kind"] = encounterKind;
            payload["encounter_profile_id"] = "";

            EncounterAnchorData restoredAnchor = EncounterAnchorData.FromDictionary(payload);
            _test.True(
                restoredAnchor == null,
                $"{encounterKind} payload should reject an empty encounter profile field."
            );
        }
    }

    private void TestMissingRequiredFieldIsRejected()
    {
        string[] requiredSerializedFields =
        {
            "entity_id",
            "display_name",
            "world_coord",
            "faction_id",
            "region_tag",
            "vision_range",
            "is_cleared",
            "encounter_kind",
            "encounter_profile_id",
            "growth_stage",
            "suppressed_until_step",
        };
        foreach (string fieldName in requiredSerializedFields)
        {
            GDictionary payload = BuildValidPayload();
            payload.Remove(fieldName);
            _test.True(
                EncounterAnchorData.FromDictionary(payload) == null,
                $"missing required field {fieldName} should be rejected."
            );
        }
    }

    private void TestExtraFieldsAreRejected()
    {
        GDictionary payload = BuildValidPayload();
        payload["enemy_roster_template_id"] = "wolf_pack";
        _test.True(
            EncounterAnchorData.FromDictionary(payload) == null,
            "payload with the removed legacy enemy_roster_template_id field should be rejected."
        );
    }

    private void TestWrongFieldTypeIsRejected()
    {
        (string Field, Variant Value)[] cases =
        {
            ("entity_id", Variant.From(12)),
            ("display_name", Variant.From(new StringName("Wild Den"))),
            ("world_coord", Variant.From(new Vector2(1.0f, 2.0f))),
            ("faction_id", Variant.From(1)),
            ("region_tag", Variant.From(1)),
            ("vision_range", Variant.From("2")),
            ("is_cleared", Variant.From(0)),
            ("encounter_kind", Variant.From(1)),
            ("encounter_profile_id", Variant.From(1)),
            ("growth_stage", Variant.From("0")),
            ("suppressed_until_step", Variant.From("0")),
        };

        foreach ((string field, Variant value) in cases)
        {
            GDictionary payload = BuildValidPayload();
            payload[field] = value;
            _test.True(
                EncounterAnchorData.FromDictionary(payload) == null,
                $"wrong type for {field} should be rejected."
            );
        }
    }

    private void TestEmptyRequiredIdentityFieldsAreRejected()
    {
        string[] fieldNames =
        {
            "entity_id",
            "display_name",
            "faction_id",
            "encounter_kind",
        };
        foreach (string fieldName in fieldNames)
        {
            GDictionary payload = BuildValidPayload();
            payload[fieldName] = "";
            _test.True(
                EncounterAnchorData.FromDictionary(payload) == null,
                $"empty required identity field {fieldName} should be rejected."
            );
        }
    }

    private void TestInvalidEncounterKindIsRejected()
    {
        GDictionary payload = BuildValidPayload();
        payload["encounter_kind"] = "legacy_default_hostile";
        _test.True(
            EncounterAnchorData.FromDictionary(payload) == null,
            "invalid encounter_kind should be rejected."
        );
    }

    private static GDictionary BuildValidPayload()
    {
        return new GDictionary
        {
            ["entity_id"] = "wild_anchor",
            ["display_name"] = "Wild Anchor",
            ["world_coord"] = new Vector2I(4, 5),
            ["faction_id"] = "hostile",
            ["region_tag"] = "north_wilds",
            ["vision_range"] = 2,
            ["is_cleared"] = false,
            ["encounter_kind"] = "single",
            ["encounter_profile_id"] = "wolf_den",
            ["growth_stage"] = 0,
            ["suppressed_until_step"] = 0,
        };
    }

    private void AssertDictionaryEq(GDictionary actual, GDictionary expected, string message)
    {
        if (actual.Count != expected.Count)
        {
            _test.Fail($"{message} | actual_count={actual.Count} expected_count={expected.Count}");
            return;
        }
        foreach (Variant key in expected.Keys)
        {
            if (!actual.ContainsKey(key))
            {
                _test.Fail($"{message} | missing_key={key}");
                return;
            }
            if (!VariantValueEquals(actual[key], expected[key]))
            {
                _test.Fail($"{message} | key={key} actual={actual[key]} expected={expected[key]}");
                return;
            }
        }
    }

    private static bool VariantValueEquals(Variant actual, Variant expected)
    {
        if (actual.VariantType != expected.VariantType)
        {
            return false;
        }
        return actual.VariantType switch
        {
            Variant.Type.String => actual.AsString() == expected.AsString(),
            Variant.Type.StringName => actual.AsStringName() == expected.AsStringName(),
            Variant.Type.Vector2I => actual.AsVector2I() == expected.AsVector2I(),
            Variant.Type.Int => actual.AsInt64() == expected.AsInt64(),
            Variant.Type.Bool => actual.AsBool() == expected.AsBool(),
            _ => Equals(actual, expected),
        };
    }
}
