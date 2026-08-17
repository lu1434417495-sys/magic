using System;
using System.Collections.Generic;
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

    private readonly TestHarness _test = new();
    private readonly ContentCanonicalJsonWriter _writer = new();

    public override void _Initialize()
    {
        try
        {
            TestImportModelRoundTripAndCanonicalOrder();
            TestNonDefaultOptionalValuesAreWritten();
            TestParityIgnoresObjectKeyOrderOnly();
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

    private enum SampleTargetShape
    {
        NarrowCone,
        SingleEnemy,
    }

    private readonly record struct SampleRuneId(string Value);

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
