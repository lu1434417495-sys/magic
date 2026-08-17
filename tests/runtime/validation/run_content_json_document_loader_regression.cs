using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

public partial class run_content_json_document_loader_regression : LifecycleTestSceneTree
{
    private const string IoFixtureDirectory =
        "user://content_json_document_loader_regression";
    private const string ValidUtf8FilePath = $"{IoFixtureDirectory}/valid_utf8.json";
    private const string InvalidUtf8FilePath = $"{IoFixtureDirectory}/invalid_utf8.json";

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
            TestInjectedIoAndUtf8RoundTrip();
            TestGodotIoReadsUtf8AndRejectsInvalidBytes();
            TestSchemaAndDomainValidation();
            TestUnknownRootMemberRejection();
            TestDuplicateEntryIdAggregation();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected content JSON document loader regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Content JSON document loader regression"));
    }

    private void TestInjectedIoAndUtf8RoundTrip()
    {
        const string directoryPath = "res://tests/fixtures/content_json";
        var reader = new FakeContentJsonSourceReader(
            new ContentJsonSourceText(
                $"{directoryPath}/mage_虹光.json",
                ValidDocument(
                    family: "法师·虹光屏障",
                    entriesJson:
                        "[{\"skill_id\":\"mage_prismatic_red_ward\",\"display_name\":\"虹光·赤红屏障\"}]"
                )
            )
        );
        var loader = new ContentJsonDocumentLoader(reader);

        ContentJsonDocumentLoadResult result = loader.LoadDirectory(directoryPath, SkillOptions);

        _test.False(result.HasErrors, "UTF-8 document should parse without diagnostics");
        _test.Eq(reader.CallCount, 1, "loader should call the injected IO boundary once");
        _test.Eq(reader.LastDirectoryPath, directoryPath, "loader should pass the source directory");
        _test.Eq(result.Documents.Count, 1, "UTF-8 batch should expose one document");
        if (result.Documents.Count != 1)
            return;

        ContentJsonDocumentEnvelope document = result.Documents[0];
        _test.Eq(document.SchemaVersion, 1, "document schema should round-trip");
        _test.Eq(document.Domain, "skills", "document domain should round-trip");
        _test.Eq(document.Family, "法师·虹光屏障", "multi-byte family should round-trip");
        _test.Eq(document.Entries.Count, 1, "UTF-8 document should expose one entry");
        if (document.Entries.Count == 1)
        {
            _test.Eq(
                document.Entries[0].EntryId,
                "mage_prismatic_red_ward",
                "configured skill_id should own entry identity"
            );
            _test.True(
                document.Entries[0].Json.Contains("虹光·赤红屏障", StringComparison.Ordinal),
                "multi-byte entry content should round-trip"
            );
        }
    }

    private void TestGodotIoReadsUtf8AndRejectsInvalidBytes()
    {
        RemoveIoFixtureDirectory();
        try
        {
            Error createError = DirAccess.MakeDirRecursiveAbsolute(
                ProjectSettings.GlobalizePath(IoFixtureDirectory)
            );
            _test.Eq(createError, Error.Ok, "Godot IO fixture directory should be created");
            if (createError != Error.Ok)
                return;

            string validJson = ValidDocument(
                family: "真实字节·虹光",
                entriesJson:
                    "[{\"skill_id\":\"mage_real_utf8\",\"display_name\":\"多字节内容\"}]"
            );
            WriteGodotBytes(ValidUtf8FilePath, new UTF8Encoding(false, true).GetBytes(validJson));

            var reader = new GodotContentJsonSourceReader();
            IReadOnlyList<ContentJsonSourceText> sources = reader.ReadUtf8Documents(
                IoFixtureDirectory
            );
            _test.Eq(sources.Count, 1, "Godot IO reader should discover the valid JSON file");
            if (sources.Count == 1)
            {
                _test.Eq(
                    sources[0].FilePath,
                    ValidUtf8FilePath,
                    "Godot IO reader should retain the virtual file path"
                );
                _test.True(
                    sources[0].Utf8Json.Contains("真实字节·虹光", StringComparison.Ordinal),
                    "FileAccess bytes should decode multi-byte UTF-8 without loss"
                );

                ContentJsonDocumentLoadResult parseResult =
                    ContentJsonDocumentLoader.ParseDocuments(sources, SkillOptions);
                _test.False(
                    parseResult.HasErrors,
                    "Godot-read UTF-8 document should pass the pure parser"
                );
                if (parseResult.Documents.Count == 1)
                {
                    _test.Eq(
                        parseResult.Documents[0].Family,
                        "真实字节·虹光",
                        "Godot byte read and pure parse should preserve UTF-8 content"
                    );
                }
            }

            WriteGodotBytes(
                InvalidUtf8FilePath,
                new byte[] { 0x7B, 0x22, 0xC3, 0x28, 0x22, 0x3A, 0x31, 0x7D }
            );
            bool rejectedInvalidUtf8 = false;
            try
            {
                reader.ReadUtf8Documents(IoFixtureDirectory);
            }
            catch (FormatException exception)
            {
                rejectedInvalidUtf8 = true;
                _test.Eq(
                    exception.Message,
                    $"JSON content file '{InvalidUtf8FilePath}' is not valid UTF-8.",
                    "invalid UTF-8 should expose a stable IO diagnostic"
                );
            }
            _test.True(rejectedInvalidUtf8, "invalid UTF-8 bytes should fail closed");
        }
        finally
        {
            RemoveIoFixtureDirectory();
        }
    }

    private void TestSchemaAndDomainValidation()
    {
        ContentJsonDocumentLoadResult result = ContentJsonDocumentLoader.ParseDocuments(
            new[]
            {
                new ContentJsonSourceText(
                    "res://data/configs/skills/bad_schema.json",
                    ValidDocument(schema: 2, entriesJson: "[]")
                ),
                new ContentJsonSourceText(
                    "res://data/configs/skills/bad_domain.json",
                    ValidDocument(domain: "items", entriesJson: "[]")
                ),
            },
            SkillOptions
        );

        _test.True(result.HasErrors, "schema/domain mismatches should reject their documents");
        _test.Eq(result.Documents.Count, 0, "mismatched documents should not be published");
        AssertDiagnostic(
            result,
            ContentJsonDocumentLoader.SchemaMismatchRule,
            "bad_schema.json",
            "/schema"
        );
        AssertDiagnostic(
            result,
            ContentJsonDocumentLoader.DomainMismatchRule,
            "bad_domain.json",
            "/domain"
        );
    }

    private void TestUnknownRootMemberRejection()
    {
        string json =
            "{\"schema\":1,\"domain\":\"skills\",\"family\":\"fixture\","
            + "\"templates\":{},\"entries\":[],\"resource/path\":\"forbidden\"}";
        ContentJsonDocumentLoadResult result = ContentJsonDocumentLoader.ParseDocuments(
            new[] { new ContentJsonSourceText("res://data/configs/skills/unknown.json", json) },
            SkillOptions
        );

        _test.True(result.HasErrors, "unknown root member should reject the document");
        _test.Eq(result.Documents.Count, 0, "unknown-root document should not be published");
        AssertDiagnostic(
            result,
            ContentJsonDocumentLoader.UnknownRootMemberRule,
            "unknown.json",
            "/resource~1path"
        );
    }

    private void TestDuplicateEntryIdAggregation()
    {
        ContentJsonDocumentLoadResult result = ContentJsonDocumentLoader.ParseDocuments(
            new[]
            {
                new ContentJsonSourceText(
                    "res://data/configs/skills/first.json",
                    ValidDocument(
                        family: "first",
                        entriesJson:
                            "[{\"skill_id\":\"duplicate_a\"},{\"skill_id\":\"duplicate_a\"},{\"skill_id\":\"duplicate_b\"}]"
                    )
                ),
                new ContentJsonSourceText(
                    "res://data/configs/skills/second.json",
                    ValidDocument(
                        family: "second",
                        entriesJson:
                            "[{\"skill_id\":\"duplicate_b\"},{\"skill_id\":\"unique\"}]"
                    )
                ),
            },
            SkillOptions
        );

        ContentJsonDiagnostic[] duplicates = result.Diagnostics
            .Where(diagnostic =>
                diagnostic.RuleId == ContentJsonDocumentLoader.DuplicateEntryIdRule
            )
            .ToArray();
        _test.Eq(
            duplicates.Length,
            4,
            "loader should report every occurrence of every duplicate ID in one batch"
        );
        _test.True(
            duplicates.Any(diagnostic =>
                diagnostic.SourceLabel == "first.json#duplicate_a"
                && diagnostic.JsonPointer == "/entries/0/skill_id"
            ),
            "duplicate diagnostic should carry file.json#entry_id and entry pointer"
        );
        _test.True(
            duplicates.Any(diagnostic =>
                diagnostic.SourceLabel == "second.json#duplicate_b"
                && diagnostic.JsonPointer == "/entries/0/skill_id"
            ),
            "cross-file duplicate should retain its own source label and pointer"
        );
        _test.False(
            duplicates.Any(diagnostic => diagnostic.SourceLabel.Contains("unique")),
            "unique entry IDs should not receive duplicate diagnostics"
        );
    }

    private void AssertDiagnostic(
        ContentJsonDocumentLoadResult result,
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

    private static string ValidDocument(
        int schema = 1,
        string domain = "skills",
        string family = "fixture",
        string entriesJson = "[]"
    ) =>
        $"{{\"schema\":{schema},\"domain\":\"{domain}\",\"family\":\"{family}\","
        + $"\"templates\":{{}},\"entries\":{entriesJson}}}";

    private void WriteGodotBytes(string filePath, byte[] bytes)
    {
        using FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        _test.True(
            file != null,
            $"Godot FileAccess should open fixture for writing: {filePath} error={FileAccess.GetOpenError()}"
        );
        if (file == null)
            return;
        file.StoreBuffer(bytes);
    }

    private static void RemoveIoFixtureDirectory()
    {
        string absoluteDirectoryPath = ProjectSettings.GlobalizePath(IoFixtureDirectory);
        if (!DirAccess.DirExistsAbsolute(absoluteDirectoryPath))
            return;

        string[] fileNames;
        using (DirAccess directory = DirAccess.Open(IoFixtureDirectory))
        {
            if (directory == null)
                return;
            fileNames = directory.GetFiles();
        }
        foreach (string fileName in fileNames)
        {
            string virtualFilePath = $"{IoFixtureDirectory}/{fileName}";
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(virtualFilePath));
        }
        DirAccess.RemoveAbsolute(absoluteDirectoryPath);
    }

    private sealed class FakeContentJsonSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _documents;

        internal FakeContentJsonSourceReader(params ContentJsonSourceText[] documents)
        {
            _documents = documents;
        }

        internal int CallCount { get; private set; }
        internal string LastDirectoryPath { get; private set; } = "";

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath)
        {
            CallCount += 1;
            LastDirectoryPath = directoryPath;
            return _documents;
        }
    }
}
