using System;
using System.Collections.Generic;
using System.Linq;

public partial class run_content_json_offline_validation_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestRegisteredPipelineMergesAndValidatesWithoutSnapshot();
            TestEntryDiagnosticsAggregateWithStableProvenanceAndFailClosedPublication();
            TestSkillPilotDomainUsesSealedParserWithStableProvenance();
            TestJsonAndNdjsonProtocolsAreByteStable();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected content JSON offline validation regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Content JSON offline validation regression"));
    }

    private void TestRegisteredPipelineMergesAndValidatesWithoutSnapshot()
    {
        IContentJsonOfflineValidationDomain domain =
            ContentJsonOfflineValidationCatalog.Require("schema_fixture");
        var reader = new FakeSourceReader(
            new ContentJsonSourceText(
                "res://fixtures/schema_fixture/valid.json",
                Document(
                    templates:
                        "{\"base\":{\"mode\":\"Manual\",\"quality\":\"common\","
                        + "\"tags\":[\"fixture\"],\"optional_note\":null,"
                        + "\"action\":{\"kind\":\"counter\"}}}",
                    entries:
                        "[{\"template\":\"base\",\"fixture_id\":\"valid\","
                        + "\"display_name\":\"Visible fixture\","
                        + "\"action\":{\"payload\":{\"amount\":2}}}]"
                )
            )
        );

        ContentJsonOfflineValidationReport report = domain.Validate(
            "res://fixtures/schema_fixture",
            reader
        );

        _test.True(
            report.Success,
            "deep-merged direct closed-kind fragments should pass the offline pipeline"
        );
        _test.Eq(report.ValidatedEntryCount, 1, "one expanded import should be validated");
        _test.Eq(report.Diagnostics.Count, 0, "valid fixture should emit no diagnostics");
        _test.Eq(reader.CallCount, 1, "offline registration should reuse one descriptor pipeline");
    }

    private void TestEntryDiagnosticsAggregateWithStableProvenanceAndFailClosedPublication()
    {
        IContentJsonOfflineValidationDomain domain =
            ContentJsonOfflineValidationCatalog.Require("schema_fixture");
        var reader = new FakeSourceReader(
            new ContentJsonSourceText(
                "res://fixtures/schema_fixture/a_strict.json",
                Document(
                    entries:
                        "[{\"fixture_id\":\"strict_bad\",\"display_name\":\"Strict\","
                        + "\"mode\":\"Manual\",\"quality\":\"common\",\"tags\":[],"
                        + "\"action\":{\"kind\":\"counter\",\"payload\":{\"amount\":1}},"
                        + "\"unexpected\":true}]"
                )
            ),
            new ContentJsonSourceText(
                "res://fixtures/schema_fixture/b_domain.json",
                Document(
                    entries:
                        "[{\"fixture_id\":\"domain_bad\",\"display_name\":\"\","
                        + "\"mode\":\"Automatic\",\"quality\":\"rare\",\"tags\":[],"
                        + "\"action\":{\"kind\":\"message\",\"payload\":{\"text\":\"ok\"}}}]"
                )
            ),
            new ContentJsonSourceText(
                "res://fixtures/schema_fixture/c_good.json",
                Document(
                    entries:
                        "[{\"fixture_id\":\"good\",\"display_name\":\"Good\","
                        + "\"mode\":\"Manual\",\"quality\":\"legendary\",\"tags\":[],"
                        + "\"action\":{\"kind\":\"counter\",\"payload\":{\"amount\":3}}}]"
                )
            )
        );

        ContentJsonOfflineValidationReport report = domain.Validate(
            "res://fixtures/schema_fixture",
            reader
        );

        _test.False(report.Success, "strict DTO and domain-local failures should aggregate");
        _test.Eq(report.Diagnostics.Count, 2, "both entry-local failures should be reported");
        _test.Eq(
            report.ValidatedEntryCount,
            0,
            "one invalid sibling must suppress otherwise valid partial imports"
        );
        AssertDiagnostic(
            report,
            ContentJsonOfflineValidationSampleRules.InvalidEntryDto,
            "a_strict.json#strict_bad",
            "/entries/0/unexpected"
        );
        AssertDiagnostic(
            report,
            ContentJsonOfflineValidationSampleRules.DisplayNameRequired,
            "b_domain.json#domain_bad",
            "/entries/0/display_name"
        );
    }

    private void TestJsonAndNdjsonProtocolsAreByteStable()
    {
        var valid = new ContentJsonOfflineValidationReport(
            "schema_fixture",
            validatedEntryCount: 2,
            Array.Empty<ContentJsonDiagnostic>()
        );
        const string expectedValidJson =
            "{\n"
            + "  \"protocol\": \"magic.content_json.validation/v1\",\n"
            + "  \"domain\": \"schema_fixture\",\n"
            + "  \"success\": true,\n"
            + "  \"validated_entry_count\": 2,\n"
            + "  \"diagnostic_count\": 0,\n"
            + "  \"diagnostics\": []\n"
            + "}\n";
        _test.Eq(
            ContentJsonOfflineValidationProtocol.FormatJson(valid),
            expectedValidJson,
            "valid JSON machine envelope should be byte-exact"
        );

        var failed = new ContentJsonOfflineValidationReport(
            "schema_fixture",
            validatedEntryCount: 9,
            new[]
            {
                new ContentJsonDiagnostic(
                    "z.rule",
                    "later",
                    "z.json#z",
                    "/entries/0/z"
                ),
                new ContentJsonDiagnostic(
                    "a.rule",
                    "first",
                    "a.json#a",
                    "/entries/0/a"
                ),
            }
        );
        const string expectedFailedJson =
            "{\n"
            + "  \"protocol\": \"magic.content_json.validation/v1\",\n"
            + "  \"domain\": \"schema_fixture\",\n"
            + "  \"success\": false,\n"
            + "  \"validated_entry_count\": 0,\n"
            + "  \"diagnostic_count\": 2,\n"
            + "  \"diagnostics\": [\n"
            + "    {\n"
            + "      \"rule_id\": \"a.rule\",\n"
            + "      \"message\": \"first\",\n"
            + "      \"source_label\": \"a.json#a\",\n"
            + "      \"json_pointer\": \"/entries/0/a\"\n"
            + "    },\n"
            + "    {\n"
            + "      \"rule_id\": \"z.rule\",\n"
            + "      \"message\": \"later\",\n"
            + "      \"source_label\": \"z.json#z\",\n"
            + "      \"json_pointer\": \"/entries/0/z\"\n"
            + "    }\n"
            + "  ]\n"
            + "}\n";
        _test.Eq(
            ContentJsonOfflineValidationProtocol.FormatJson(failed),
            expectedFailedJson,
            "diagnostic JSON envelope should be byte-exact and canonically ordered"
        );

        const string expectedNdjson =
            "{\"type\":\"diagnostic\",\"protocol\":\"magic.content_json.validation/v1\","
            + "\"domain\":\"schema_fixture\",\"rule_id\":\"a.rule\",\"message\":\"first\","
            + "\"source_label\":\"a.json#a\",\"json_pointer\":\"/entries/0/a\"}\n"
            + "{\"type\":\"diagnostic\",\"protocol\":\"magic.content_json.validation/v1\","
            + "\"domain\":\"schema_fixture\",\"rule_id\":\"z.rule\",\"message\":\"later\","
            + "\"source_label\":\"z.json#z\",\"json_pointer\":\"/entries/0/z\"}\n"
            + "{\"type\":\"summary\",\"protocol\":\"magic.content_json.validation/v1\","
            + "\"domain\":\"schema_fixture\",\"success\":false,\"validated_entry_count\":0,"
            + "\"diagnostic_count\":2}\n";
        _test.Eq(
            ContentJsonOfflineValidationProtocol.FormatNdjson(failed),
            expectedNdjson,
            "NDJSON diagnostics and summary should be byte-exact"
        );
        _test.Eq(
            ContentJsonOfflineValidationProtocol.FormatNdjson(failed),
            ContentJsonOfflineValidationProtocol.FormatNdjson(failed),
            "repeated NDJSON formatting should be deterministic"
        );
    }

    private void TestSkillPilotDomainUsesSealedParserWithStableProvenance()
    {
        IContentJsonOfflineValidationDomain domain =
            ContentJsonOfflineValidationCatalog.Require("skills");
        var reader = new FakeSourceReader(
            new ContentJsonSourceText(
                "res://fixtures/skills/pilot_bad.json",
                "{\"schema\":1,\"domain\":\"skills\",\"family\":\"pilot\","
                    + "\"templates\":{\"base\":{\"skill_type\":\"active\"}},"
                    + "\"entries\":[{\"template\":\"base\","
                    + "\"skill_id\":\"mage_focus\",\"display_name\":\"Focus\","
                    + "\"combat_profile\":{\"skill_id\":\"mage_focus\","
                    + "\"mp_cost\":-1}}]}"
            )
        );

        ContentJsonOfflineValidationReport report = domain.Validate(
            "res://fixtures/skills",
            reader
        );

        _test.False(report.Success, "invalid skill JSON should fail the offline pilot domain");
        _test.Eq(report.Diagnostics.Count, 1, "invalid skill should emit one stable diagnostic");
        _test.Eq(
            report.ValidatedEntryCount,
            0,
            "invalid skill sibling publication should remain fail closed"
        );
        AssertDiagnostic(
            report,
            SkillJsonImportRules.NumberOutOfRange,
            "pilot_bad.json#mage_focus",
            "/entries/0/combat_profile/mp_cost"
        );
    }

    private void AssertDiagnostic(
        ContentJsonOfflineValidationReport report,
        string ruleId,
        string sourceLabel,
        string jsonPointer
    )
    {
        _test.True(
            report.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == ruleId
                && diagnostic.SourceLabel == sourceLabel
                && diagnostic.JsonPointer == jsonPointer
            ),
            $"diagnostic should match rule={ruleId} source={sourceLabel} pointer={jsonPointer}"
        );
    }

    private static string Document(string templates = "{}", string entries = "[]") =>
        "{\"schema\":1,\"domain\":\"schema_fixture\",\"family\":\"fixture\","
        + $"\"templates\":{templates},\"entries\":{entries}}}";

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeSourceReader(params ContentJsonSourceText[] sources)
        {
            _sources = sources;
        }

        internal int CallCount { get; private set; }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
        {
            CallCount += 1;
            return _sources;
        }
    }
}
