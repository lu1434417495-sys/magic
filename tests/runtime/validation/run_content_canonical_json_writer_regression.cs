using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

public partial class run_content_canonical_json_writer_regression : LifecycleTestSceneTree
{
    private static readonly ContentCanonicalJsonObjectSchema<SampleProfile> ProfileSchema = new(
        ContentCanonicalJsonProperty<SampleProfile>.Required(
            "resistance",
            profile => profile.Resistance,
            ContentCanonicalJsonValue.Double
        ),
        ContentCanonicalJsonProperty<SampleProfile>.Required(
            "label",
            profile => profile.Label,
            ContentCanonicalJsonValue.Text
        ),
        ContentCanonicalJsonProperty<SampleProfile>.Optional(
            "enabled",
            profile => profile.Enabled,
            defaultValue: true,
            ContentCanonicalJsonValue.Boolean
        )
    );

    private static readonly ContentCanonicalJsonObjectSchema<SampleImportModel> ImportSchema = new(
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "skill_id",
            model => model.SkillId,
            ContentCanonicalJsonValue.Text
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "display_name",
            model => model.DisplayName,
            ContentCanonicalJsonValue.Text
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "shape",
            model => model.Shape,
            ContentCanonicalJsonValue.StableBusinessString<SampleTargetShape>(ShapeToString)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "rune",
            model => model.Rune,
            ContentCanonicalJsonValue.StableBusinessString<SampleRuneId>(rune => rune.Value)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "profile",
            model => model.Profile,
            ContentCanonicalJsonValue.Object(ProfileSchema)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "hit_offsets",
            model => model.HitOffsets,
            ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Int32)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "double_boundaries",
            model => model.DoubleBoundaries,
            ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Double)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Required(
            "single_boundaries",
            model => model.SingleBoundaries,
            ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Single)
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Optional(
            "rank",
            model => model.Rank,
            defaultValue: 0,
            ContentCanonicalJsonValue.Int32
        ),
        ContentCanonicalJsonProperty<SampleImportModel>.Optional(
            "enabled",
            model => model.Enabled,
            defaultValue: true,
            ContentCanonicalJsonValue.Boolean
        )
    );

    private static readonly ContentCanonicalJsonObjectSchema<SampleMapEnvelope> MapEnvelopeSchema =
        new(
            ContentCanonicalJsonProperty<SampleMapEnvelope>.Required(
                "levels",
                model => model.Levels,
                ContentCanonicalJsonValue.OrderedObjectMap<
                    IReadOnlyDictionary<int, string>,
                    int,
                    string
                >(
                    entries => entries,
                    ContentCanonicalJsonKey.InvariantInt32,
                    ContentCanonicalJsonKey.InvariantInt32Order,
                    ContentCanonicalJsonValue.Text
                )
            )
        );

    private static readonly ContentCanonicalJsonObjectSchema<SampleShieldPayload>
        ShieldPayloadSchema = new(
            ContentCanonicalJsonProperty<SampleShieldPayload>.Required(
                "kind",
                payload => PayloadKindToString(payload.Kind),
                ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<SampleShieldPayload>.Required(
                "shield_points",
                payload => payload.ShieldPoints,
                ContentCanonicalJsonValue.Int32
            )
        );

    private static readonly ContentCanonicalJsonObjectSchema<SampleHealPayload>
        HealPayloadSchema = new(
            ContentCanonicalJsonProperty<SampleHealPayload>.Required(
                "kind",
                payload => PayloadKindToString(payload.Kind),
                ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<SampleHealPayload>.Required(
                "heal_points",
                payload => payload.HealPoints,
                ContentCanonicalJsonValue.Int32
            )
        );

    private static readonly ContentCanonicalJsonValueSchema<ISampleClosedUnionPayload>
        ClosedPayloadSchema = ContentCanonicalJsonValue.ClosedUnion<
            ISampleClosedUnionPayload,
            SamplePayloadKind
        >(
            payload => payload.Kind,
            ContentCanonicalJsonUnionCase<
                ISampleClosedUnionPayload,
                SamplePayloadKind
            >.Create<SampleShieldPayload>(
                SamplePayloadKind.Shield,
                ContentCanonicalJsonValue.Object(ShieldPayloadSchema)
            ),
            ContentCanonicalJsonUnionCase<
                ISampleClosedUnionPayload,
                SamplePayloadKind
            >.Create<SampleHealPayload>(
                SamplePayloadKind.Heal,
                ContentCanonicalJsonValue.Object(HealPayloadSchema)
            )
        );

    private static readonly ContentCanonicalJsonObjectSchema<SampleUnionEnvelope>
        UnionEnvelopeSchema = new(
            ContentCanonicalJsonProperty<SampleUnionEnvelope>.Required(
                "payload",
                model => model.Payload,
                ClosedPayloadSchema
            )
        );

    private readonly TestHarness _test = new();
    private readonly ContentCanonicalJsonWriter _writer = new();

    public override void _Initialize()
    {
        try
        {
            TestImportModelRoundTripAndCanonicalOrder();
            TestNonDefaultOptionalValuesAreWritten();
            TestParityIgnoresObjectKeyOrderOnly();
            TestOrderedObjectMapUsesCanonicalKeysAndStableOrder();
            TestOrderedObjectMapFailuresAreFailClosed();
            TestClosedUnionUsesExplicitPerKindSchemas();
            TestClosedUnionFailuresAreFailClosed();
            TestSchemaAndNumberFailuresAreFailClosed();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected canonical JSON writer regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Content canonical JSON writer regression"));
    }

    private void TestImportModelRoundTripAndCanonicalOrder()
    {
        SampleImportModel source = CreateSample();
        string json = _writer.Write(source, ImportSchema);
        SampleImportModel imported = ImportSample(json);

        AssertModelEqual(imported, source, "import model -> JSON -> import model");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        string[] actualOrder = root.EnumerateObject().Select(property => property.Name).ToArray();
        string[] expectedOrder =
        {
            "skill_id",
            "display_name",
            "shape",
            "rune",
            "profile",
            "hit_offsets",
            "double_boundaries",
            "single_boundaries",
        };
        AssertSequenceEqual(
            actualOrder,
            expectedOrder,
            "root keys should follow caller-provided schema declaration order"
        );

        string[] profileOrder = root
            .GetProperty("profile")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        AssertSequenceEqual(
            profileOrder,
            new[] { "resistance", "label" },
            "nested keys should follow their typed schema declaration order"
        );
        AssertSequenceEqual(
            root.GetProperty("hit_offsets").EnumerateArray().Select(item => item.GetInt32()),
            source.HitOffsets,
            "arrays should preserve source order"
        );

        _test.False(root.TryGetProperty("rank", out _), "DTO default rank should be omitted");
        _test.False(root.TryGetProperty("enabled", out _), "DTO default enabled should be omitted");
        _test.False(
            root.GetProperty("profile").TryGetProperty("enabled", out _),
            "nested DTO defaults should be omitted"
        );
        _test.Eq(
            root.GetProperty("shape").GetString(),
            "narrow_cone",
            "enum should use its stable business string"
        );
        _test.Eq(
            root.GetProperty("rune").GetString(),
            "frost-rune",
            "value object should use its stable business string"
        );
        _test.False(
            json.Contains(nameof(SampleTargetShape.NarrowCone), StringComparison.Ordinal),
            "writer should not expose CLR enum member names"
        );
        foreach (
            string forbiddenName in new[]
            {
                "source_label",
                "resource_path",
                "uid",
                "diagnostic",
            }
        )
        {
            _test.False(
                root.TryGetProperty(forbiddenName, out _),
                $"undeclared provenance/diagnostic field should not leak: {forbiddenName}"
            );
        }
    }

    private void TestNonDefaultOptionalValuesAreWritten()
    {
        SampleImportModel source = CreateSample(rank: 4, enabled: false, profileEnabled: false);
        string json = _writer.Write(source, ImportSchema, indented: false);
        using JsonDocument document = JsonDocument.Parse(json);

        _test.Eq(
            document.RootElement.GetProperty("rank").GetInt32(),
            4,
            "non-default integer should remain explicit"
        );
        _test.False(
            document.RootElement.GetProperty("enabled").GetBoolean(),
            "non-default boolean should remain explicit"
        );
        _test.False(
            document.RootElement.GetProperty("profile").GetProperty("enabled").GetBoolean(),
            "nested non-default boolean should remain explicit"
        );
    }

    private void TestParityIgnoresObjectKeyOrderOnly()
    {
        const string declaredOrder =
            "{\"skill_id\":\"fixture\",\"profile\":{\"resistance\":0.25,\"label\":\"ward\"},\"hit_offsets\":[3,1,2]}";
        const string reordered =
            "{\"hit_offsets\":[3,1,2],\"profile\":{\"label\":\"ward\",\"resistance\":0.25},\"skill_id\":\"fixture\"}";
        const string reorderedArray =
            "{\"skill_id\":\"fixture\",\"profile\":{\"resistance\":0.25,\"label\":\"ward\"},\"hit_offsets\":[1,3,2]}";
        const string changedValue =
            "{\"skill_id\":\"fixture\",\"profile\":{\"resistance\":0.5,\"label\":\"ward\"},\"hit_offsets\":[3,1,2]}";

        _test.True(
            _writer.JsonEqualsIgnoringObjectPropertyOrder(declaredOrder, reordered),
            "same writer should offer key-order-independent parity comparison"
        );
        _test.False(
            _writer.JsonEqualsIgnoringObjectPropertyOrder(declaredOrder, reorderedArray),
            "parity comparison should keep array order significant"
        );
        _test.False(
            _writer.JsonEqualsIgnoringObjectPropertyOrder(declaredOrder, changedValue),
            "parity comparison should keep values significant"
        );
    }

    private void TestOrderedObjectMapUsesCanonicalKeysAndStableOrder()
    {
        var levels = new Dictionary<int, string>
        {
            [10] = "ten",
            [2] = "two",
            [1] = "one",
        };
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        string json;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            json = _writer.Write(new SampleMapEnvelope(levels), MapEnvelopeSchema);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonProperty[] properties = document
            .RootElement
            .GetProperty("levels")
            .EnumerateObject()
            .ToArray();
        AssertSequenceEqual(
            properties.Select(property => property.Name),
            new[] { "1", "2", "10" },
            "object-map keys should use explicit canonical numeric order"
        );
        AssertSequenceEqual(
            properties.Select(property => property.Value.GetString()),
            new[] { "one", "two", "ten" },
            "object-map values should remain paired with canonical keys"
        );
        _test.False(
            json.Contains('\u0661'),
            "Int32 object-map keys should remain invariant under non-English culture"
        );
    }

    private void TestOrderedObjectMapFailuresAreFailClosed()
    {
        ContentCanonicalJsonValueSchema<
            IReadOnlyList<KeyValuePair<string, string>>
        > collidingMapSchema = ContentCanonicalJsonValue.OrderedObjectMap<
            IReadOnlyList<KeyValuePair<string, string>>,
            string,
            string
        >(
            entries => entries,
            key => key.ToUpperInvariant(),
            StringComparer.Ordinal,
            ContentCanonicalJsonValue.Text
        );
        var collisionEnvelopeSchema =
            new ContentCanonicalJsonObjectSchema<SampleStringMapEnvelope>(
                ContentCanonicalJsonProperty<SampleStringMapEnvelope>.Required(
                    "values",
                    model => model.Values,
                    collidingMapSchema
                )
            );
        bool collisionRejected = false;
        try
        {
            _writer.Write(
                new SampleStringMapEnvelope(
                    new[]
                    {
                        new KeyValuePair<string, string>("a", "first"),
                        new KeyValuePair<string, string>("A", "second"),
                    }
                ),
                collisionEnvelopeSchema
            );
        }
        catch (JsonException)
        {
            collisionRejected = true;
        }
        _test.True(
            collisionRejected,
            "object-map keys that collide after canonicalization should fail closed"
        );

        bool nullMapRejected = false;
        try
        {
            _writer.Write(new SampleMapEnvelope(null), MapEnvelopeSchema);
        }
        catch (JsonException)
        {
            nullMapRejected = true;
        }
        _test.True(nullMapRejected, "null object-map values should fail closed");

        ContentCanonicalJsonValueSchema<IReadOnlyDictionary<int, string>> invalidKeySchema =
            ContentCanonicalJsonValue.OrderedObjectMap<
                IReadOnlyDictionary<int, string>,
                int,
                string
            >(
                entries => entries,
                key => $"not-an-int-{key}",
                ContentCanonicalJsonKey.InvariantInt32Order,
                ContentCanonicalJsonValue.Text
            );
        var invalidKeyEnvelopeSchema = new ContentCanonicalJsonObjectSchema<SampleMapEnvelope>(
            ContentCanonicalJsonProperty<SampleMapEnvelope>.Required(
                "levels",
                model => model.Levels,
                invalidKeySchema
            )
        );
        bool invalidCanonicalKeyRejected = false;
        try
        {
            _writer.Write(
                new SampleMapEnvelope(new Dictionary<int, string> { [1] = "one", [2] = "two" }),
                invalidKeyEnvelopeSchema
            );
        }
        catch (JsonException)
        {
            invalidCanonicalKeyRejected = true;
        }
        _test.True(
            invalidCanonicalKeyRejected,
            "a canonical-key comparer should reject keys outside its declared format"
        );

        AssertNonCanonicalInt32MapKeyRejected("+1");
        AssertNonCanonicalInt32MapKeyRejected("01");
        AssertNonCanonicalInt32MapKeyRejected("-0");
    }

    private void TestClosedUnionUsesExplicitPerKindSchemas()
    {
        string shieldJson = _writer.Write(
            new SampleUnionEnvelope(new SampleShieldPayload(17)),
            UnionEnvelopeSchema
        );
        string healJson = _writer.Write(
            new SampleUnionEnvelope(new SampleHealPayload(9)),
            UnionEnvelopeSchema
        );
        using JsonDocument shieldDocument = JsonDocument.Parse(shieldJson);
        using JsonDocument healDocument = JsonDocument.Parse(healJson);
        JsonElement shield = shieldDocument.RootElement.GetProperty("payload");
        JsonElement heal = healDocument.RootElement.GetProperty("payload");

        _test.Eq(shield.GetProperty("kind").GetString(), "shield", "shield kind string");
        _test.Eq(shield.GetProperty("shield_points").GetInt32(), 17, "shield case schema");
        _test.False(
            shield.TryGetProperty("heal_points", out _),
            "shield case should not use heal schema"
        );
        _test.Eq(heal.GetProperty("kind").GetString(), "heal", "heal kind string");
        _test.Eq(heal.GetProperty("heal_points").GetInt32(), 9, "heal case schema");
        _test.False(
            heal.TryGetProperty("shield_points", out _),
            "heal case should not use shield schema"
        );
    }

    private void TestClosedUnionFailuresAreFailClosed()
    {
        bool duplicateKindRejected = false;
        try
        {
            _ = ContentCanonicalJsonValue.ClosedUnion<
                ISampleClosedUnionPayload,
                SamplePayloadKind
            >(
                payload => payload.Kind,
                ContentCanonicalJsonUnionCase<
                    ISampleClosedUnionPayload,
                    SamplePayloadKind
                >.Create<SampleShieldPayload>(
                    SamplePayloadKind.Shield,
                    ContentCanonicalJsonValue.Object(ShieldPayloadSchema)
                ),
                ContentCanonicalJsonUnionCase<
                    ISampleClosedUnionPayload,
                    SamplePayloadKind
                >.Create<SampleShieldPayload>(
                    SamplePayloadKind.Shield,
                    ContentCanonicalJsonValue.Object(ShieldPayloadSchema)
                )
            );
        }
        catch (ArgumentException)
        {
            duplicateKindRejected = true;
        }
        _test.True(
            duplicateKindRejected,
            "closed union should reject duplicate kind registrations"
        );

        bool unknownKindRejected = false;
        try
        {
            _writer.Write(
                new SampleUnionEnvelope(
                    new SampleForgedPayload((SamplePayloadKind)999)
                ),
                UnionEnvelopeSchema
            );
        }
        catch (JsonException)
        {
            unknownKindRejected = true;
        }
        _test.True(unknownKindRejected, "closed union should reject unregistered kinds");

        bool mismatchedPayloadRejected = false;
        try
        {
            _writer.Write(
                new SampleUnionEnvelope(new SampleForgedPayload(SamplePayloadKind.Shield)),
                UnionEnvelopeSchema
            );
        }
        catch (JsonException)
        {
            mismatchedPayloadRejected = true;
        }
        _test.True(
            mismatchedPayloadRejected,
            "closed-union dispatch should reject a payload whose concrete type does not match its kind"
        );
    }

    private void AssertNonCanonicalInt32MapKeyRejected(string nonCanonicalKey)
    {
        ContentCanonicalJsonValueSchema<
            IReadOnlyList<KeyValuePair<string, string>>
        > mapSchema = ContentCanonicalJsonValue.OrderedObjectMap<
            IReadOnlyList<KeyValuePair<string, string>>,
            string,
            string
        >(
            entries => entries,
            key => key,
            ContentCanonicalJsonKey.InvariantInt32Order,
            ContentCanonicalJsonValue.Text
        );
        var envelopeSchema = new ContentCanonicalJsonObjectSchema<SampleStringMapEnvelope>(
            ContentCanonicalJsonProperty<SampleStringMapEnvelope>.Required(
                "values",
                model => model.Values,
                mapSchema
            )
        );

        bool rejected = false;
        try
        {
            _writer.Write(
                new SampleStringMapEnvelope(
                    new[]
                    {
                        new KeyValuePair<string, string>(nonCanonicalKey, "invalid"),
                        new KeyValuePair<string, string>("2", "valid"),
                    }
                ),
                envelopeSchema
            );
        }
        catch (JsonException)
        {
            rejected = true;
        }
        _test.True(
            rejected,
            $"InvariantInt32 object-map order should reject non-canonical key '{nonCanonicalKey}'"
        );
    }

    private void TestSchemaAndNumberFailuresAreFailClosed()
    {
        bool duplicateSchemaRejected = false;
        try
        {
            _ = new ContentCanonicalJsonObjectSchema<SampleProfile>(
                ContentCanonicalJsonProperty<SampleProfile>.Required(
                    "label",
                    profile => profile.Label,
                    ContentCanonicalJsonValue.Text
                ),
                ContentCanonicalJsonProperty<SampleProfile>.Required(
                    "label",
                    profile => profile.Label,
                    ContentCanonicalJsonValue.Text
                )
            );
        }
        catch (ArgumentException)
        {
            duplicateSchemaRejected = true;
        }
        _test.True(duplicateSchemaRejected, "duplicate schema properties should fail closed");

        SampleImportModel nonFinite = CreateSample(
            doubleBoundaries: new[] { double.NaN },
            singleBoundaries: new[] { float.PositiveInfinity }
        );
        bool nonFiniteRejected = false;
        try
        {
            _writer.Write(nonFinite, ImportSchema);
        }
        catch (JsonException)
        {
            nonFiniteRejected = true;
        }
        _test.True(nonFiniteRejected, "non-finite floating-point values should fail closed");

        bool duplicateParityKeyRejected = false;
        try
        {
            _writer.JsonEqualsIgnoringObjectPropertyOrder(
                "{\"skill_id\":\"first\",\"skill_id\":\"second\"}",
                "{\"skill_id\":\"first\"}"
            );
        }
        catch (JsonException)
        {
            duplicateParityKeyRejected = true;
        }
        _test.True(
            duplicateParityKeyRejected,
            "parity comparison should reject ambiguous duplicate object keys"
        );
    }

    private void AssertModelEqual(
        SampleImportModel actual,
        SampleImportModel expected,
        string context
    )
    {
        _test.Eq(actual.SkillId, expected.SkillId, $"{context}: skill_id");
        _test.Eq(actual.DisplayName, expected.DisplayName, $"{context}: display_name");
        _test.Eq(actual.Shape, expected.Shape, $"{context}: shape");
        _test.Eq(actual.Rune, expected.Rune, $"{context}: rune");
        _test.Eq(actual.Profile.Label, expected.Profile.Label, $"{context}: profile.label");
        AssertDoubleBitsEqual(
            actual.Profile.Resistance,
            expected.Profile.Resistance,
            $"{context}: profile.resistance"
        );
        _test.Eq(actual.Profile.Enabled, expected.Profile.Enabled, $"{context}: profile.enabled");
        AssertSequenceEqual(actual.HitOffsets, expected.HitOffsets, $"{context}: hit_offsets");
        AssertFloatingPointSequencesEqual(
            actual.DoubleBoundaries,
            expected.DoubleBoundaries,
            $"{context}: double boundaries"
        );
        AssertFloatingPointSequencesEqual(
            actual.SingleBoundaries,
            expected.SingleBoundaries,
            $"{context}: float boundaries"
        );
        _test.Eq(actual.Rank, expected.Rank, $"{context}: rank default");
        _test.Eq(actual.Enabled, expected.Enabled, $"{context}: enabled default");
    }

    private void AssertFloatingPointSequencesEqual(
        IReadOnlyList<double> actual,
        IReadOnlyList<double> expected,
        string context
    )
    {
        _test.Eq(actual.Count, expected.Count, $"{context}: count");
        for (int index = 0; index < Math.Min(actual.Count, expected.Count); index++)
            AssertDoubleBitsEqual(actual[index], expected[index], $"{context}[{index}]");
    }

    private void AssertFloatingPointSequencesEqual(
        IReadOnlyList<float> actual,
        IReadOnlyList<float> expected,
        string context
    )
    {
        _test.Eq(actual.Count, expected.Count, $"{context}: count");
        for (int index = 0; index < Math.Min(actual.Count, expected.Count); index++)
        {
            _test.Eq(
                BitConverter.SingleToInt32Bits(actual[index]),
                BitConverter.SingleToInt32Bits(expected[index]),
                $"{context}[{index}] should preserve exact float bits"
            );
        }
    }

    private void AssertDoubleBitsEqual(double actual, double expected, string context)
    {
        _test.Eq(
            BitConverter.DoubleToInt64Bits(actual),
            BitConverter.DoubleToInt64Bits(expected),
            $"{context} should preserve exact double bits"
        );
    }

    private void AssertSequenceEqual<T>(
        IEnumerable<T> actual,
        IEnumerable<T> expected,
        string context
    )
    {
        T[] actualValues = actual.ToArray();
        T[] expectedValues = expected.ToArray();
        _test.Eq(actualValues.Length, expectedValues.Length, $"{context}: count");
        for (int index = 0; index < Math.Min(actualValues.Length, expectedValues.Length); index++)
        {
            _test.Eq(actualValues[index], expectedValues[index], $"{context}[{index}]");
        }
    }

    private static SampleImportModel CreateSample(
        int rank = 0,
        bool enabled = true,
        bool profileEnabled = true,
        IReadOnlyList<double> doubleBoundaries = null,
        IReadOnlyList<float> singleBoundaries = null
    ) =>
        new(
            skillId: "mage_fixture_ward",
            displayName: "边界·虹光屏障",
            shape: SampleTargetShape.NarrowCone,
            rune: new SampleRuneId("frost-rune"),
            profile: new SampleProfile(0.125, "ward-profile", profileEnabled),
            hitOffsets: new[] { 3, 1, 2 },
            doubleBoundaries:
                doubleBoundaries
                ?? new[]
                {
                    double.Epsilon,
                    -double.Epsilon,
                    double.MaxValue,
                    double.MinValue,
                    Math.PI,
                },
            singleBoundaries:
                singleBoundaries
                ?? new[]
                {
                    float.Epsilon,
                    -float.Epsilon,
                    float.MaxValue,
                    float.MinValue,
                    0.1f,
                },
            rank: rank,
            enabled: enabled,
            sourceLabel: "fixture.json#mage_fixture_ward",
            resourcePath: "res://forbidden/source.tres",
            uid: "uid://forbidden",
            diagnostic: "must never serialize"
        );

    private static SampleImportModel ImportSample(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement profile = root.GetProperty("profile");
        return new SampleImportModel(
            skillId: root.GetProperty("skill_id").GetString(),
            displayName: root.GetProperty("display_name").GetString(),
            shape: ShapeFromString(root.GetProperty("shape").GetString()),
            rune: new SampleRuneId(root.GetProperty("rune").GetString()),
            profile: new SampleProfile(
                profile.GetProperty("resistance").GetDouble(),
                profile.GetProperty("label").GetString(),
                profile.TryGetProperty("enabled", out JsonElement profileEnabled)
                    ? profileEnabled.GetBoolean()
                    : true
            ),
            hitOffsets: root
                .GetProperty("hit_offsets")
                .EnumerateArray()
                .Select(item => item.GetInt32())
                .ToArray(),
            doubleBoundaries: root
                .GetProperty("double_boundaries")
                .EnumerateArray()
                .Select(item => item.GetDouble())
                .ToArray(),
            singleBoundaries: root
                .GetProperty("single_boundaries")
                .EnumerateArray()
                .Select(item => item.GetSingle())
                .ToArray(),
            rank: root.TryGetProperty("rank", out JsonElement rank) ? rank.GetInt32() : 0,
            enabled: root.TryGetProperty("enabled", out JsonElement enabled)
                ? enabled.GetBoolean()
                : true,
            sourceLabel: "",
            resourcePath: "",
            uid: "",
            diagnostic: ""
        );
    }

    private static string ShapeToString(SampleTargetShape shape) =>
        shape switch
        {
            SampleTargetShape.NarrowCone => "narrow_cone",
            SampleTargetShape.SingleEnemy => "single_enemy",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };

    private static SampleTargetShape ShapeFromString(string shape) =>
        shape switch
        {
            "narrow_cone" => SampleTargetShape.NarrowCone,
            "single_enemy" => SampleTargetShape.SingleEnemy,
            _ => throw new JsonException($"Unknown sample target shape '{shape}'."),
        };

    private static string PayloadKindToString(SamplePayloadKind kind) =>
        kind switch
        {
            SamplePayloadKind.Shield => "shield",
            SamplePayloadKind.Heal => "heal",
            _ => throw new JsonException($"Unknown sample payload kind '{kind}'."),
        };

    private enum SampleTargetShape
    {
        NarrowCone,
        SingleEnemy,
    }

    private readonly record struct SampleRuneId(string Value);

    private sealed record SampleMapEnvelope(IReadOnlyDictionary<int, string> Levels);

    private sealed record SampleStringMapEnvelope(
        IReadOnlyList<KeyValuePair<string, string>> Values
    );

    private enum SamplePayloadKind
    {
        Shield,
        Heal,
    }

    private interface ISampleClosedUnionPayload
    {
        SamplePayloadKind Kind { get; }
    }

    private sealed record SampleShieldPayload(int ShieldPoints) : ISampleClosedUnionPayload
    {
        public SamplePayloadKind Kind => SamplePayloadKind.Shield;
    }

    private sealed record SampleHealPayload(int HealPoints) : ISampleClosedUnionPayload
    {
        public SamplePayloadKind Kind => SamplePayloadKind.Heal;
    }

    private sealed record SampleForgedPayload(SamplePayloadKind Kind)
        : ISampleClosedUnionPayload;

    private sealed record SampleUnionEnvelope(ISampleClosedUnionPayload Payload);

    private sealed record SampleProfile(double Resistance, string Label, bool Enabled);

    private sealed class SampleImportModel
    {
        internal SampleImportModel(
            string skillId,
            string displayName,
            SampleTargetShape shape,
            SampleRuneId rune,
            SampleProfile profile,
            IReadOnlyList<int> hitOffsets,
            IReadOnlyList<double> doubleBoundaries,
            IReadOnlyList<float> singleBoundaries,
            int rank,
            bool enabled,
            string sourceLabel,
            string resourcePath,
            string uid,
            string diagnostic
        )
        {
            SkillId = skillId;
            DisplayName = displayName;
            Shape = shape;
            Rune = rune;
            Profile = profile;
            HitOffsets = hitOffsets;
            DoubleBoundaries = doubleBoundaries;
            SingleBoundaries = singleBoundaries;
            Rank = rank;
            Enabled = enabled;
            SourceLabel = sourceLabel;
            ResourcePath = resourcePath;
            Uid = uid;
            Diagnostic = diagnostic;
        }

        internal string SkillId { get; }
        internal string DisplayName { get; }
        internal SampleTargetShape Shape { get; }
        internal SampleRuneId Rune { get; }
        internal SampleProfile Profile { get; }
        internal IReadOnlyList<int> HitOffsets { get; }
        internal IReadOnlyList<double> DoubleBoundaries { get; }
        internal IReadOnlyList<float> SingleBoundaries { get; }
        internal int Rank { get; }
        internal bool Enabled { get; }
        internal string SourceLabel { get; }
        internal string ResourcePath { get; }
        internal string Uid { get; }
        internal string Diagnostic { get; }
    }
}
