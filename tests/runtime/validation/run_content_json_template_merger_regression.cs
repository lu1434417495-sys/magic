using System;
using System.Linq;
using System.Text.Json;

public partial class run_content_json_template_merger_regression : LifecycleTestSceneTree
{
    private static readonly ContentJsonDocumentLoadOptions SkillOptions = new(
        expectedSchemaVersion: 1,
        expectedDomain: "skills",
        entryIdPropertyName: "skill_id"
    );

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRecursiveObjectMergeArrayReplacementAndExplicitScalars();
            TestSchemaDeclaredNullabilityIncludingArrayWildcards();
            TestForbiddenRawNullCannotBeHiddenByOverride();
            TestTemplateCycleIsRejected();
            TestUnknownTemplateReferencesAreRejected();
            TestEntryWithoutTemplateIsPreserved();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected content JSON template merger regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Content JSON template merger regression"));
    }

    private void TestRecursiveObjectMergeArrayReplacementAndExplicitScalars()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "merge.json",
            templatesJson:
                "{\"root\":{\"combat\":{\"ap\":2,\"mp\":60,\"nested\":{\"keep\":7,\"override\":\"base\"}},"
                + "\"tags\":[\"base\"],\"power\":9,\"enabled\":true,\"label\":\"base\",\"keep\":\"base\","
                + "\"inherited_business\":{\"template\":\"nested_parent\",\"keep\":1},"
                + "\"overridden_business\":{\"template\":\"nested_parent\",\"keep\":2}},"
                + "\"mid\":{\"template\":\"root\",\"combat\":{\"mp\":30,\"nested\":{\"override\":\"mid\",\"mid_only\":1}},"
                + "\"tags\":[\"mid\"],\"mid_only\":\"yes\",\"overridden_business\":{\"template\":\"nested_child\"}}}",
            entriesJson:
                "[{\"template\":\"mid\",\"skill_id\":\"merged\",\"combat\":{\"ap\":0,\"nested\":{\"override\":\"\"}},"
                + "\"tags\":[],\"power\":0,\"enabled\":false,\"label\":\"\"},"
                + "{\"template\":\"mid\",\"skill_id\":\"sibling\",\"combat\":{\"nested\":{\"override\":\"sibling\"}}}]"
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(
            document,
            ContentJsonNullabilityPolicy.None
        );

        _test.False(result.HasErrors, "valid multi-level template merge should succeed");
        _test.Eq(result.Entries.Count, 2, "both entries should be expanded independently");
        using JsonDocument mergedDocument = ParseEntry(result, "merged");
        JsonElement merged = mergedDocument.RootElement;
        JsonElement combat = merged.GetProperty("combat");
        JsonElement nested = combat.GetProperty("nested");
        _test.Eq(combat.GetProperty("ap").GetInt32(), 0, "explicit zero should override template");
        _test.Eq(combat.GetProperty("mp").GetInt32(), 30, "missing entry key should keep intermediate template value");
        _test.Eq(nested.GetProperty("keep").GetInt32(), 7, "nested missing key should keep root template value");
        _test.Eq(nested.GetProperty("mid_only").GetInt32(), 1, "nested object merge should keep intermediate keys");
        _test.Eq(nested.GetProperty("override").GetString(), "", "empty string should explicitly override template");
        _test.Eq(merged.GetProperty("tags").GetArrayLength(), 0, "child array should replace the entire template array");
        _test.Eq(merged.GetProperty("power").GetInt32(), 0, "top-level zero should explicitly override template");
        _test.False(merged.GetProperty("enabled").GetBoolean(), "false should explicitly override template");
        _test.Eq(merged.GetProperty("label").GetString(), "", "top-level empty string should explicitly override template");
        _test.Eq(merged.GetProperty("keep").GetString(), "base", "missing entry key should retain root template scalar");
        _test.Eq(merged.GetProperty("mid_only").GetString(), "yes", "multi-level template should retain intermediate scalar");
        _test.Eq(
            merged.GetProperty("inherited_business").GetProperty("template").GetString(),
            "nested_parent",
            "nested business template field should be inherited and retained"
        );
        _test.Eq(
            merged.GetProperty("overridden_business").GetProperty("template").GetString(),
            "nested_child",
            "nested business template field should be recursively overridden and retained"
        );
        _test.Eq(
            merged.GetProperty("overridden_business").GetProperty("keep").GetInt32(),
            2,
            "nested business object merge should retain sibling fields while overriding template"
        );
        _test.False(merged.TryGetProperty("template", out _), "template control field must not escape into expanded JSON");

        using JsonDocument siblingDocument = ParseEntry(result, "sibling");
        JsonElement siblingNested = siblingDocument
            .RootElement.GetProperty("combat")
            .GetProperty("nested");
        _test.Eq(siblingNested.GetProperty("override").GetString(), "sibling", "sibling should receive its own nested override");
        _test.Eq(nested.GetProperty("override").GetString(), "", "one expanded entry must not share mutable object state with a sibling");
    }

    private void TestSchemaDeclaredNullabilityIncludingArrayWildcards()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "nullable.json",
            templatesJson:
                "{\"base\":{\"optional_note\":\"base\",\"effect_defs\":[{\"payload\":{\"note\":\"base\"}}]}}",
            entriesJson:
                "[{\"template\":\"base\",\"skill_id\":\"nullable\",\"optional_note\":null,"
                + "\"effect_defs\":[{\"payload\":{\"note\":null}}]}]"
        );
        var policy = new ContentJsonNullabilityPolicy(
            new[] { "/optional_note", "/effect_defs/*/payload/note" }
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(document, policy);

        _test.False(result.HasErrors, "schema-declared nullable fields should accept explicit null");
        using JsonDocument outputDocument = ParseEntry(result, "nullable");
        JsonElement output = outputDocument.RootElement;
        _test.Eq(output.GetProperty("optional_note").ValueKind, JsonValueKind.Null, "nullable scalar field should remain explicit null");
        _test.Eq(
            output.GetProperty("effect_defs")[0].GetProperty("payload").GetProperty("note").ValueKind,
            JsonValueKind.Null,
            "schema wildcard should authorize the same nullable field at any array index"
        );
    }

    private void TestForbiddenRawNullCannotBeHiddenByOverride()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "forbidden_null.json",
            templatesJson: "{\"base\":{\"optional_note\":null}}",
            entriesJson:
                "[{\"template\":\"base\",\"skill_id\":\"override\",\"optional_note\":\"replacement\"},"
                + "{\"skill_id\":\"direct_null\",\"optional_note\":null}]"
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(
            document,
            ContentJsonNullabilityPolicy.None
        );

        _test.True(result.HasErrors, "null outside schema nullability should fail closed");
        _test.Eq(result.Entries.Count, 0, "a document with forbidden null must not publish partial expanded entries");
        AssertDiagnostic(
            result,
            ContentJsonTemplateMerger.NullNotAllowedRule,
            "forbidden_null.json#base",
            "/templates/base/optional_note"
        );
        AssertDiagnostic(
            result,
            ContentJsonTemplateMerger.NullNotAllowedRule,
            "forbidden_null.json#direct_null",
            "/entries/1/optional_note"
        );
    }

    private void TestTemplateCycleIsRejected()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "cycle.json",
            templatesJson:
                "{\"a\":{\"template\":\"b\",\"a_value\":1},\"b\":{\"template\":\"a\",\"b_value\":2}}",
            entriesJson: "[{\"template\":\"a\",\"skill_id\":\"cycle_entry\"}]"
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(
            document,
            ContentJsonNullabilityPolicy.None
        );

        _test.True(result.HasErrors, "template inheritance cycle should fail closed");
        _test.Eq(result.Entries.Count, 0, "cyclic template document should publish no entries");
        _test.True(
            result.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == ContentJsonTemplateMerger.TemplateCycleRule
                && diagnostic.Message.Contains("a -> b -> a", StringComparison.Ordinal)
            ),
            "cycle diagnostic should identify the file-local inheritance route"
        );
    }

    private void TestUnknownTemplateReferencesAreRejected()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "unknown.json",
            templatesJson: "{\"orphan\":{\"template\":\"missing_parent\",\"value\":1}}",
            entriesJson:
                "[{\"template\":\"orphan\",\"skill_id\":\"inherits_orphan\"},"
                + "{\"template\":\"missing_entry_template\",\"skill_id\":\"direct_unknown\"}]"
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(
            document,
            ContentJsonNullabilityPolicy.None
        );

        _test.True(result.HasErrors, "unknown template references should fail closed");
        _test.Eq(result.Entries.Count, 0, "unknown template document should publish no entries");
        AssertDiagnostic(
            result,
            ContentJsonTemplateMerger.UnknownTemplateRule,
            "unknown.json#orphan",
            "/templates/orphan/template"
        );
        AssertDiagnostic(
            result,
            ContentJsonTemplateMerger.UnknownTemplateRule,
            "unknown.json#direct_unknown",
            "/entries/1/template"
        );
    }

    private void TestEntryWithoutTemplateIsPreserved()
    {
        ContentJsonDocumentEnvelope document = LoadEnvelope(
            "plain.json",
            templatesJson: "{\"unused\":{\"value\":99}}",
            entriesJson:
                "[{\"skill_id\":\"plain\",\"value\":0,\"enabled\":false,\"label\":\"\","
                + "\"nested\":{\"keep\":3},\"values\":[1,2]}]"
        );

        ContentJsonTemplateMergeResult result = ContentJsonTemplateMerger.Merge(
            document,
            ContentJsonNullabilityPolicy.None
        );

        _test.False(result.HasErrors, "entry without template should pass through merger");
        _test.Eq(result.Entries.Count, 1, "plain entry should still be published");
        using JsonDocument outputDocument = ParseEntry(result, "plain");
        JsonElement output = outputDocument.RootElement;
        _test.Eq(output.GetProperty("skill_id").GetString(), "plain", "plain entry ID should be preserved");
        _test.Eq(output.GetProperty("value").GetInt32(), 0, "plain entry zero should be preserved");
        _test.False(output.GetProperty("enabled").GetBoolean(), "plain entry false should be preserved");
        _test.Eq(output.GetProperty("label").GetString(), "", "plain entry empty string should be preserved");
        _test.Eq(output.GetProperty("nested").GetProperty("keep").GetInt32(), 3, "plain nested object should be preserved");
        _test.Eq(output.GetProperty("values").GetArrayLength(), 2, "plain array should be preserved");
    }

    private ContentJsonDocumentEnvelope LoadEnvelope(
        string fileName,
        string templatesJson,
        string entriesJson
    )
    {
        string json =
            $"{{\"schema\":1,\"domain\":\"skills\",\"family\":\"fixture\",\"templates\":{templatesJson},\"entries\":{entriesJson}}}";
        ContentJsonDocumentLoadResult loadResult = ContentJsonDocumentLoader.ParseDocuments(
            new[] { new ContentJsonSourceText($"res://tests/fixtures/content_json/{fileName}", json) },
            SkillOptions
        );
        _test.False(loadResult.HasErrors, $"T0.1 loader should accept merger fixture {fileName}");
        _test.Eq(loadResult.Documents.Count, 1, $"fixture {fileName} should produce one envelope");
        if (loadResult.Documents.Count != 1)
            throw new InvalidOperationException($"Unable to construct merger fixture '{fileName}'.");
        return loadResult.Documents[0];
    }

    private JsonDocument ParseEntry(ContentJsonTemplateMergeResult result, string entryId)
    {
        ContentJsonEntryDocument entry = result.Entries.Single(candidate =>
            string.Equals(candidate.EntryId, entryId, StringComparison.Ordinal)
        );
        return JsonDocument.Parse(entry.Json);
    }

    private void AssertDiagnostic(
        ContentJsonTemplateMergeResult result,
        string ruleId,
        string sourceLabel,
        string jsonPointer
    )
    {
        _test.True(
            result.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == ruleId
                && diagnostic.SourceLabel == sourceLabel
                && diagnostic.JsonPointer == jsonPointer
            ),
            $"diagnostic should match rule={ruleId} source={sourceLabel} pointer={jsonPointer}"
        );
    }
}
