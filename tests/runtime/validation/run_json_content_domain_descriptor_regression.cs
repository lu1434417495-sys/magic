using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class run_json_content_domain_descriptor_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEmptyDomainPipeline();
            TestSuccessfulPipelineStopsAfterDomainLocalValidation();
            TestStageErrorsAggregateAndPublishNoPartialImports();
            TestLoaderAndDomainDiagnosticsAggregate();
            TestCallerOwnsCrossDomainOrder();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected JSON content domain descriptor regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("JSON content domain descriptor regression"));
    }

    private void TestEmptyDomainPipeline()
    {
        var trace = new List<string>();
        var reader = new FakeSourceReader(trace, "read:empty");
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> descriptor =
            BuildDescriptor("empty", "res://fixtures/empty", reader, trace);

        ContentImportBatch<FixtureImport> batch = descriptor.Import();

        _test.False(batch.HasErrors, "an empty registered domain should complete successfully");
        _test.Eq(batch.Entries.Count, 0, "an empty domain should publish no imports");
        _test.Eq(batch.Diagnostics.Count, 0, "an empty domain should publish no diagnostics");
        _test.Eq(reader.CallCount, 1, "empty domain should still execute source discovery once");
        _test.Eq(trace.Single(), "read:empty", "empty pipeline should stop after discovery");
    }

    private void TestSuccessfulPipelineStopsAfterDomainLocalValidation()
    {
        var trace = new List<string>();
        var reader = new FakeSourceReader(
            trace,
            "read:fixture",
            new ContentJsonSourceText(
                "res://fixtures/fixture/success.json",
                Document(
                    "fixture",
                    templates: "{\"base\":{\"value\":7}}",
                    entries:
                        "[{\"template\":\"base\",\"fixture_id\":\"first\"},"
                        + "{\"fixture_id\":\"second\",\"value\":2}]"
                )
            )
        );
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> descriptor =
            BuildDescriptor("fixture", "res://fixtures/fixture", reader, trace);

        ContentImportBatch<FixtureImport> batch = descriptor.Import();

        _test.False(batch.HasErrors, "valid single-domain pipeline should succeed");
        _test.Eq(batch.Entries.Count, 2, "valid entries should publish two import models");
        if (batch.Entries.Count == 2)
        {
            _test.Eq(batch.Entries[0].Import.Id, "first", "source order should be retained");
            _test.Eq(
                batch.Entries[0].Import.Value,
                7,
                "DTO parse should receive the file-local template-expanded JSON"
            );
            _test.Eq(
                batch.Entries[0].Context.SourceLabel,
                "success.json#first",
                "validated import should retain diagnostic context for explicit builder stages"
            );
            _test.Eq(
                batch.Entries[1].Import.Id,
                "second",
                "second source entry should remain second"
            );
        }

        string[] expected =
        {
            "read:fixture",
            "parse:first",
            "normalize:first",
            "validate:first",
            "parse:second",
            "normalize:second",
            "validate:second",
        };
        _test.True(
            trace.SequenceEqual(expected),
            $"single-domain stages should execute in contract order | actual={string.Join(",", trace)}"
        );
    }

    private void TestStageErrorsAggregateAndPublishNoPartialImports()
    {
        var trace = new List<string>();
        var reader = new FakeSourceReader(
            trace,
            "read:stage_errors",
            new ContentJsonSourceText(
                "res://fixtures/stage_errors/errors.json",
                Document(
                    "stage_errors",
                    entries:
                        "[{\"fixture_id\":\"parse_bad\",\"value\":\"bad\"},"
                        + "{\"fixture_id\":\"normalize_bad\",\"value\":2,\"fail_normalize\":true},"
                        + "{\"fixture_id\":\"validate_bad\",\"value\":-1},"
                        + "{\"fixture_id\":\"good\",\"value\":5}]"
                )
            )
        );
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> descriptor =
            BuildDescriptor("stage_errors", "res://fixtures/stage_errors", reader, trace);

        ContentImportBatch<FixtureImport> batch = descriptor.Import();

        _test.True(batch.HasErrors, "errors from any domain-local stage should fail the batch");
        _test.Eq(
            batch.Entries.Count,
            0,
            "a failed batch must not publish the successfully validated sibling import"
        );
        _test.Eq(batch.Diagnostics.Count, 3, "parse/normalize/validate diagnostics should aggregate");
        AssertRule(batch, "fixture.dto.invalid_value", "errors.json#parse_bad");
        AssertRule(batch, "fixture.normalize.rejected", "errors.json#normalize_bad");
        AssertRule(batch, "fixture.validate.negative", "errors.json#validate_bad");
        _test.False(
            trace.Contains("normalize:parse_bad"),
            "an entry with a parse error must not reach normalization"
        );
        _test.False(
            trace.Contains("validate:normalize_bad"),
            "an entry with a normalization error must not reach validation"
        );
        _test.True(
            trace.Contains("validate:good"),
            "a good sibling should form a validated import before fail-closed batch publication"
        );
    }

    private void TestLoaderAndDomainDiagnosticsAggregate()
    {
        var trace = new List<string>();
        var reader = new FakeSourceReader(
            trace,
            "read:aggregate",
            new ContentJsonSourceText(
                "res://fixtures/aggregate/first.json",
                Document("aggregate", entries: "[{\"fixture_id\":\"duplicate\",\"value\":-1}]")
            ),
            new ContentJsonSourceText(
                "res://fixtures/aggregate/second.json",
                Document("aggregate", entries: "[{\"fixture_id\":\"duplicate\",\"value\":1}]")
            )
        );
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> descriptor =
            BuildDescriptor("aggregate", "res://fixtures/aggregate", reader, trace);

        ContentImportBatch<FixtureImport> batch = descriptor.Import();

        _test.True(batch.HasErrors, "loader duplicate and domain validation should fail one batch");
        _test.Eq(batch.Entries.Count, 0, "diagnostic aggregation must remain fail-closed");
        _test.Eq(
            batch.Diagnostics.Count(diagnostic =>
                diagnostic.RuleId == ContentJsonDocumentLoader.DuplicateEntryIdRule
            ),
            2,
            "both duplicate ID occurrences should remain in the aggregate"
        );
        _test.Eq(
            batch.Diagnostics.Count(diagnostic =>
                diagnostic.RuleId == "fixture.validate.negative"
            ),
            1,
            "domain diagnostics should aggregate with document diagnostics"
        );
    }

    private void TestCallerOwnsCrossDomainOrder()
    {
        var callOrder = new List<string>();
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> first =
            BuildDescriptor(
                "first_domain",
                "res://fixtures/first",
                new FakeSourceReader(callOrder, "first_domain"),
                new List<string>()
            );
        JsonContentDomainDescriptor<FixtureDto, FixtureImport> second =
            BuildDescriptor(
                "second_domain",
                "res://fixtures/second",
                new FakeSourceReader(callOrder, "second_domain"),
                new List<string>()
            );

        second.Import();
        first.Import();

        _test.True(
            callOrder.SequenceEqual(new[] { "second_domain", "first_domain" }),
            "descriptor should preserve caller-selected cross-domain order, not derive a graph"
        );
    }

    private JsonContentDomainDescriptor<FixtureDto, FixtureImport> BuildDescriptor(
        string domainId,
        string sourceDirectory,
        IContentJsonSourceReader reader,
        List<string> trace
    )
    {
        return new JsonContentDomainDescriptor<FixtureDto, FixtureImport>(
            domainId,
            schemaVersion: 1,
            entryIdPropertyName: "fixture_id",
            sourceDirectory,
            reader,
            ContentJsonNullabilityPolicy.None,
            (context, json) =>
            {
                trace.Add($"parse:{context.EntryId}");
                return ParseDto(context, json);
            },
            (context, dto) =>
            {
                trace.Add($"normalize:{context.EntryId}");
                return dto.FailNormalize
                    ? ContentImportStageResult<FixtureImport>.Failure(
                        Diagnostic(
                            "fixture.normalize.rejected",
                            context,
                            "/fail_normalize"
                        )
                    )
                    : ContentImportStageResult<FixtureImport>.Success(
                        new FixtureImport(dto.Id, dto.Value)
                    );
            },
            (context, import) =>
            {
                trace.Add($"validate:{context.EntryId}");
                return import.Value < 0
                    ? new[]
                    {
                        Diagnostic("fixture.validate.negative", context, "/value"),
                    }
                    : Array.Empty<ContentJsonDiagnostic>();
            }
        );
    }

    private static ContentImportStageResult<FixtureDto> ParseDto(
        JsonContentEntryContext context,
        string json
    )
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (
            !root.TryGetProperty("value", out JsonElement valueElement)
            || valueElement.ValueKind != JsonValueKind.Number
            || !valueElement.TryGetInt32(out int value)
        )
        {
            return ContentImportStageResult<FixtureDto>.Failure(
                Diagnostic("fixture.dto.invalid_value", context, "/value")
            );
        }

        bool failNormalize =
            root.TryGetProperty("fail_normalize", out JsonElement failNormalizeElement)
            && failNormalizeElement.ValueKind == JsonValueKind.True;
        return ContentImportStageResult<FixtureDto>.Success(
            new FixtureDto(context.EntryId, value, failNormalize)
        );
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        JsonContentEntryContext context,
        string relativePointer
    ) =>
        new(
            ruleId,
            ruleId,
            context.SourceLabel,
            $"{context.JsonPointer}{relativePointer}"
        );

    private void AssertRule(
        ContentImportBatch<FixtureImport> batch,
        string ruleId,
        string sourceLabel
    )
    {
        _test.True(
            batch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == ruleId && diagnostic.SourceLabel == sourceLabel
            ),
            $"aggregate should contain rule={ruleId} source={sourceLabel}"
        );
    }

    private static string Document(
        string domain,
        string templates = "{}",
        string entries = "[]"
    ) =>
        $"{{\"schema\":1,\"domain\":\"{domain}\",\"family\":\"fixture\","
        + $"\"templates\":{templates},\"entries\":{entries}}}";

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly List<string> _trace;
        private readonly string _traceLabel;
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeSourceReader(
            List<string> trace,
            string traceLabel,
            params ContentJsonSourceText[] sources
        )
        {
            _trace = trace;
            _traceLabel = traceLabel;
            _sources = sources;
        }

        internal int CallCount { get; private set; }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
        {
            CallCount += 1;
            _trace.Add(_traceLabel);
            return _sources;
        }
    }

    private sealed record FixtureDto(
        string Id,
        int Value,
        bool FailNormalize
    );

    private sealed record FixtureImport(string Id, int Value);
}
