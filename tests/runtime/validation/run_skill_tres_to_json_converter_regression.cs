#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;

public partial class run_skill_tres_to_json_converter_regression : LifecycleTestSceneTree
{
    private const string FixtureRootVirtual = "user://skill_tres_to_json_converter_regression";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly ContentJsonDocumentLoadOptions LoadOptions = new(
        SkillContentJsonAuthoringDomain.SchemaVersion,
        SkillContentJsonAuthoringDomain.DomainId,
        SkillContentJsonAuthoringDomain.EntryIdPropertyName
    );
    private static readonly HashSet<string> ForbiddenPropertyNames = new(
        new[]
    {
        "resource_path",
        "resource_local_to_scene",
        "resource_type",
        "class_name",
        "$type",
        "uid",
    },
        StringComparer.Ordinal
    );

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        RemoveFixtureRoot();
        try
        {
            TestManifestIsFixedAndOrdered();
            TestCommandLineRejectsReparsePointAncestorsByMode();
            TestRealResourcesRoundTripThroughPublishedDocuments();
            TestConversionFailureDoesNotTouchTarget();
            TestExistingTargetRollbackIsRecoverable();
            TestConcurrentPublicationIsRejectedBeforeTargetMutation();
            TestConverterRejectsUnownedExactFileSets();
            TestPublisherRejectsUnownedAndStaleTargets();
            TestPublishedBackupCleanupFailureIsExplicit();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected skill tres-to-JSON converter exception: {exception}");
        }
        finally
        {
            RemoveFixtureRoot();
        }

        RequestTestExit(_test.Finish("Skill tres-to-JSON converter regression"));
    }

    private void TestManifestIsFixedAndOrdered()
    {
        string[] expectedColors =
        {
            "red",
            "orange",
            "yellow",
            "green",
            "blue",
            "indigo",
            "violet",
        };
        _test.Eq(SkillTresToJsonConverter.Sources.Count, expectedColors.Length,
            "converter manifest should contain exactly seven wards");
        for (int index = 0; index < expectedColors.Length; index += 1)
        {
            if (index >= SkillTresToJsonConverter.Sources.Count)
                break;
            string color = expectedColors[index];
            string skillId = $"mage_prismatic_{color}_ward";
            SkillTresToJsonSource source = SkillTresToJsonConverter.Sources[index];
            _test.Eq(source.Color, color, $"manifest color order {index}");
            _test.Eq(source.SkillId, skillId, $"manifest skill id {index}");
            _test.Eq(
                source.ResourcePath,
                $"res://data/configs/skills/{skillId}.tres",
                $"manifest Resource path {index}"
            );
            _test.Eq(source.OutputFileName, $"{skillId}.json", $"manifest output {index}");
        }
    }

    private void TestCommandLineRejectsReparsePointAncestorsByMode()
    {
        string externalRoot = Path.Combine(
            Path.GetPathRoot(ProjectSettings.GlobalizePath("res://"))!,
            "t1a6_converter_path_oracle"
        );
        string reparseAncestor = Path.Combine(externalRoot, "junction");
        string output = Path.Combine(reparseAncestor, "generated");
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        FileAttributes? Attributes(string path) =>
            string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(reparseAncestor)),
                comparison
            )
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : null;

        bool explicitRejected = false;
        try
        {
            run_skill_tres_to_json_converter.ParseArguments(
                new[] { $"--output-dir={output}" },
                tryGetAttributesForTests: Attributes
            );
        }
        catch (ArgumentException exception)
        {
            explicitRejected = exception.Message.Contains(
                "reparse-point ancestor",
                StringComparison.Ordinal
            );
        }
        _test.True(explicitRejected,
            "explicit output-dir should reject a reparse-point ancestor as usage error");

        bool temporaryRejected = false;
        try
        {
            run_skill_tres_to_json_converter.ParseArguments(
                Array.Empty<string>(),
                createTemporaryOutputDirectoryForTests: () => output,
                tryGetAttributesForTests: Attributes
            );
        }
        catch (InvalidOperationException exception)
        {
            temporaryRejected = exception.Message.Contains(
                "reparse-point ancestor",
                StringComparison.Ordinal
            );
        }
        _test.True(temporaryRejected,
            "default temporary output should reject a reparse-point ancestor as runtime failure");

        string projectRoot = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(ProjectSettings.GlobalizePath("res://"))
        );
        bool projectRootRejected = false;
        try
        {
            run_skill_tres_to_json_converter.ParseArguments(
                new[] { $"--output-dir={Path.Combine(externalRoot, "safe")}" },
                tryGetAttributesForTests: path =>
                    string.Equals(
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                        projectRoot,
                        comparison
                    )
                        ? FileAttributes.Directory | FileAttributes.ReparsePoint
                        : null
            );
        }
        catch (ArgumentException exception)
        {
            projectRootRejected = exception.Message.Contains(
                "project root traverses reparse-point ancestor",
                StringComparison.Ordinal
            );
        }
        _test.True(projectRootRejected,
            "explicit output-dir should reject a reparse-point project-root ancestor");

        run_skill_tres_to_json_converter.ConverterArguments inPlace =
            run_skill_tres_to_json_converter.ParseArguments(
                new[] { "--in-place" },
                tryGetAttributesForTests: static _ =>
                    FileAttributes.Directory | FileAttributes.ReparsePoint
            );
        _test.Eq(inPlace.Mode, "in-place",
            "fixed in-place mode should not reject a repository reparse-point ancestor");
    }

    private void TestRealResourcesRoundTripThroughPublishedDocuments()
    {
        const string outputVirtual = $"{FixtureRootVirtual}/success";
        const string repeatedVirtual = $"{FixtureRootVirtual}/repeat";
        string outputHost = ProjectSettings.GlobalizePath(outputVirtual);
        string repeatedHost = ProjectSettings.GlobalizePath(repeatedVirtual);

        var converter = new SkillTresToJsonConverter();
        SkillTresToJsonExportResult result = converter.ConvertAndPublish(outputHost);
        SkillTresToJsonExportResult repeated = converter.ConvertAndPublish(repeatedHost);

        _test.Eq(result.EntryCount, 7, "successful conversion should publish seven entries");
        _test.Eq(result.OutputDirectory, Path.GetFullPath(outputHost),
            "result should expose the canonical host target");
        AssertOrderedFileNames(result.FileNames, "primary publication");
        AssertOrderedFileNames(repeated.FileNames, "repeated publication");

        var sourceReader = new GodotContentJsonSourceReader();
        IReadOnlyList<ContentJsonSourceText> sourceTexts =
            sourceReader.ReadUtf8Documents(outputVirtual);
        _test.Eq(sourceTexts.Count, 7, "Godot source reader should observe seven JSON files");

        var documentLoader = new ContentJsonDocumentLoader(sourceReader);
        ContentJsonDocumentLoadResult loaded = documentLoader.LoadDirectory(
            outputVirtual,
            LoadOptions
        );
        _test.False(loaded.HasErrors, Diagnostics("document load", loaded.Diagnostics));
        _test.Eq(loaded.Documents.Count, 7, "one document should be published per ward");

        ContentImportBatch<SkillImportModel> importedBatch =
            SkillContentJsonAuthoringDomain.CreateImportDescriptor(
                outputVirtual,
                sourceReader
            ).Import();
        _test.False(importedBatch.HasErrors, Diagnostics("domain import", importedBatch.Diagnostics));
        _test.Eq(importedBatch.Entries.Count, 7,
            "the shared skill domain descriptor should import all seven entries");

        using var resourceLoader = new TestContentResourceLoader();
        var writer = new ContentCanonicalJsonWriter();
        Dictionary<string, SkillImportModel> importedById = importedBatch.Entries.ToDictionary(
            static entry => entry.Context.EntryId,
            static entry => entry.Import,
            StringComparer.Ordinal
        );
        foreach (ContentJsonDocumentEnvelope document in loaded.Documents)
        {
            _test.Eq(document.SchemaVersion, 1, "document schema should be canonical");
            _test.Eq(document.Domain, "skills", "document domain should be canonical");
            _test.Eq(document.Family, SkillImportCanonicalJson.FamilyId,
                "document family should be canonical");
            _test.Eq(document.Templates.Count, 0, "converter documents should have no templates");
            _test.Eq(document.Entries.Count, 1, "each converter document should have one entry");
        }

        _test.Eq(importedById.Count, 7, "all seven JSON entries should re-import");
        foreach (SkillTresToJsonSource source in SkillTresToJsonConverter.Sources)
        {
            SkillDef resource = resourceLoader.LoadCanonical<SkillDef>(source.ResourcePath);
            var context = new JsonContentEntryContext(
                SkillContentJsonAuthoringDomain.DomainId,
                source.SkillId,
                source.ResourcePath,
                "/entries/0"
            );
            ContentImportStageResult<SkillImportModel> adapted =
                SkillTresImportAdapter.TryAdapt(context, resource);
            _test.True(adapted.HasValue, Diagnostics("Resource adaptation", adapted.Diagnostics));
            if (!adapted.HasValue || !importedById.TryGetValue(source.SkillId, out SkillImportModel? imported))
                continue;

            string canonicalResource = SkillImportCanonicalJson.WriteEntry(writer, adapted.Value);
            string canonicalImported = SkillImportCanonicalJson.WriteEntry(writer, imported);
            _test.Eq(
                canonicalImported,
                canonicalResource,
                $"{source.SkillId} JSON re-import should preserve every canonical field"
            );

            string expectedDocument = SkillImportCanonicalJson.WriteSingleEntry(writer, adapted.Value);
            string primaryPath = Path.Combine(outputHost, source.OutputFileName);
            byte[] actualBytes = File.ReadAllBytes(primaryPath);
            byte[] repeatedBytes = File.ReadAllBytes(
                Path.Combine(repeatedHost, source.OutputFileName)
            );
            byte[] expectedBytes = StrictUtf8.GetBytes(NormalizeLf(expectedDocument));
            _test.True(actualBytes.AsSpan().SequenceEqual(expectedBytes),
                $"{source.OutputFileName} bytes should equal the canonical envelope");
            _test.True(actualBytes.AsSpan().SequenceEqual(repeatedBytes),
                $"{source.OutputFileName} bytes should be stable across publications");
            _test.True(actualBytes.Length > 0 && actualBytes[0] != 0xEF,
                $"{source.OutputFileName} should not start with a UTF-8 BOM");

            string text = StrictUtf8.GetString(actualBytes);
            _test.True(text.EndsWith('\n'), $"{source.OutputFileName} should end with LF");
            _test.False(text.Contains('\r'), $"{source.OutputFileName} should contain LF only");
            using JsonDocument json = JsonDocument.Parse(text);
            AssertNoEngineSerialization(json.RootElement, source.OutputFileName, "");
        }
    }

    private void TestConversionFailureDoesNotTouchTarget()
    {
        const string targetVirtual = $"{FixtureRootVirtual}/failed";
        string targetHost = ProjectSettings.GlobalizePath(targetVirtual);
        int loadCount = 0;
        using var resourceLoader = new TestContentResourceLoader();
        var converter = new SkillTresToJsonConverter(
            path =>
            {
                loadCount += 1;
                if (loadCount == 4)
                    throw new InvalidOperationException("synthetic fourth Resource load failure");
                return resourceLoader.LoadCanonical<SkillDef>(path);
            }
        );

        bool rejected = false;
        try
        {
            converter.ConvertAndPublish(targetHost);
        }
        catch (InvalidOperationException exception)
        {
            rejected = exception.Message.Contains("fourth Resource load", StringComparison.Ordinal);
        }

        _test.True(rejected, "a domain conversion failure should escape as a failed export");
        _test.Eq(loadCount, 4, "conversion should stop at the failing Resource");
        _test.False(Directory.Exists(targetHost),
            "conversion failure should not create the publication target");
        AssertNoTransactionArtifacts(Path.GetDirectoryName(targetHost)!, "failed");
    }

    private void TestExistingTargetRollbackIsRecoverable()
    {
        const string targetVirtual = $"{FixtureRootVirtual}/rollback";
        string targetHost = ProjectSettings.GlobalizePath(targetVirtual);
        Directory.CreateDirectory(targetHost);
        var previousBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (SkillTresToJsonSource source in SkillTresToJsonConverter.Sources)
        {
            byte[] bytes = StrictUtf8.GetBytes(OwnedEnvelope(source, "rollback old"));
            previousBytes.Add(source.OutputFileName, bytes);
            File.WriteAllBytes(Path.Combine(targetHost, source.OutputFileName), bytes);
        }

        var publisher = new ContentJsonTransactionalDirectoryPublisher(
            (phase, _, _) =>
            {
                if (phase == ContentJsonDirectoryMovePhase.StagingToTarget)
                    throw new IOException("synthetic promote failure");
            },
            validateExistingTargetOwnership: SkillTresToJsonConverter.ValidateExistingTargetOwnership
        );
        using var resourceLoader = new TestContentResourceLoader();
        var converter = new SkillTresToJsonConverter(
            resourceLoader.LoadCanonical<SkillDef>,
            publisher: publisher
        );

        bool rejected = false;
        try
        {
            converter.ConvertAndPublish(targetHost);
        }
        catch (IOException exception)
        {
            rejected = exception.Message.Contains("synthetic promote failure", StringComparison.Ordinal);
        }

        _test.True(rejected, "synthetic promotion failure should fail the publication");
        _test.True(Directory.Exists(targetHost), "rollback should restore the old target directory");
        _test.Eq(Directory.GetFiles(targetHost, "*.json").Length, 7,
            "rollback should restore the complete old generation");
        foreach ((string fileName, byte[] bytes) in previousBytes)
        {
            _test.True(
                File.ReadAllBytes(Path.Combine(targetHost, fileName)).AsSpan().SequenceEqual(bytes),
                $"rollback should restore old bytes for {fileName}"
            );
        }
        AssertNoTransactionArtifacts(Path.GetDirectoryName(targetHost)!, "rollback");
    }

    private void TestPublisherRejectsUnownedAndStaleTargets()
    {
        string parentHost = ProjectSettings.GlobalizePath(FixtureRootVirtual);
        Directory.CreateDirectory(parentHost);
        IReadOnlyList<ContentJsonOutputFile> files = SkillTresToJsonConverter.Sources
            .Select(source => new ContentJsonOutputFile(source.OutputFileName, "{}\n"))
            .ToArray();
        var publisher = new ContentJsonTransactionalDirectoryPublisher();

        string unownedTarget = Path.Combine(parentHost, "unowned");
        Directory.CreateDirectory(unownedTarget);
        string unrelated = Path.Combine(unownedTarget, "personal-notes.txt");
        File.WriteAllText(unrelated, "keep", StrictUtf8);
        bool rejectedUnowned = false;
        try
        {
            publisher.Publish(unownedTarget, files);
        }
        catch (IOException exception)
        {
            rejectedUnowned = exception.Message.Contains(
                "not the exact expected generated file set",
                StringComparison.Ordinal
            );
        }
        _test.True(rejectedUnowned, "publisher should reject an arbitrary existing directory");
        _test.Eq(File.ReadAllText(unrelated, StrictUtf8), "keep",
            "publisher should not replace files it does not own");

        string staleTarget = Path.Combine(parentHost, "stale");
        string staleArtifact = Path.Combine(parentHost, ".stale.backup-reviewed");
        Directory.CreateDirectory(staleArtifact);
        File.WriteAllText(Path.Combine(staleArtifact, "review.txt"), "keep", StrictUtf8);
        bool rejectedStale = false;
        try
        {
            publisher.Publish(staleTarget, files);
        }
        catch (IOException exception)
        {
            rejectedStale = exception.Message.Contains(
                "stale transaction artifact",
                StringComparison.Ordinal
            );
        }
        _test.True(rejectedStale, "publisher should require review of a stale backup");
        _test.True(File.Exists(Path.Combine(staleArtifact, "review.txt")),
            "publisher should not silently delete a stale backup");
        _test.False(Directory.Exists(staleTarget),
            "stale backup rejection should not create the target");

        string lockedTarget = Path.Combine(parentHost, "locked");
        string staleLock = Path.Combine(parentHost, ".locked.publish.lock");
        File.WriteAllText(staleLock, "stale", StrictUtf8);
        bool rejectedLock = false;
        try
        {
            publisher.Publish(lockedTarget, files);
        }
        catch (IOException exception)
        {
            rejectedLock =
                exception.Message.Contains("active or stale publication lock", StringComparison.Ordinal)
                && exception.Message.Contains("review it before retrying", StringComparison.Ordinal);
        }
        _test.True(rejectedLock, "publisher should fail closed on a stale publication lock");
        _test.Eq(File.ReadAllText(staleLock, StrictUtf8), "stale",
            "publisher should preserve a stale publication lock for manual review");
        _test.False(Directory.Exists(lockedTarget),
            "stale publication lock rejection should not create the target");
    }

    private void TestConcurrentPublicationIsRejectedBeforeTargetMutation()
    {
        string parentHost = ProjectSettings.GlobalizePath(FixtureRootVirtual);
        string target = Path.Combine(parentHost, "concurrent");
        Directory.CreateDirectory(target);
        IReadOnlyList<ContentJsonOutputFile> firstGeneration =
            SkillTresToJsonConverter.Sources.Select(
                source => new ContentJsonOutputFile(source.OutputFileName, "{\"generation\":1}\n")
            ).ToArray();
        IReadOnlyList<ContentJsonOutputFile> secondGeneration =
            SkillTresToJsonConverter.Sources.Select(
                source => new ContentJsonOutputFile(source.OutputFileName, "{\"generation\":2}\n")
            ).ToArray();
        foreach (SkillTresToJsonSource source in SkillTresToJsonConverter.Sources)
            File.WriteAllText(
                Path.Combine(target, source.OutputFileName),
                OwnedEnvelope(source, "concurrent old"),
                StrictUtf8
            );

        using var reachedPromotion = new ManualResetEventSlim(false);
        using var releasePromotion = new ManualResetEventSlim(false);
        var firstPublisher = new ContentJsonTransactionalDirectoryPublisher(
            (phase, _, _) =>
            {
                if (phase != ContentJsonDirectoryMovePhase.StagingToTarget)
                    return;
                reachedPromotion.Set();
                if (!releasePromotion.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("synthetic concurrent publish release timed out");
            },
            validateExistingTargetOwnership: SkillTresToJsonConverter.ValidateExistingTargetOwnership
        );
        Task first = Task.Run(() => firstPublisher.Publish(target, firstGeneration));
        bool reached = reachedPromotion.Wait(TimeSpan.FromSeconds(10));
        _test.True(reached, "first publisher should reach the blocked promote seam");

        bool secondRejected = false;
        try
        {
            new ContentJsonTransactionalDirectoryPublisher(
                validateExistingTargetOwnership: SkillTresToJsonConverter.ValidateExistingTargetOwnership
            ).Publish(target, secondGeneration);
        }
        catch (IOException exception)
        {
            secondRejected = exception.Message.Contains(
                "active or stale publication lock",
                StringComparison.Ordinal
            );
        }
        finally
        {
            releasePromotion.Set();
        }
        _test.True(secondRejected,
            "a concurrent publisher should fail before inspecting or mutating the target");

        try
        {
            first.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _test.Fail($"first publisher should finish after release: {exception.Message}");
        }
        foreach (ContentJsonOutputFile file in firstGeneration)
        {
            _test.Eq(
                File.ReadAllText(Path.Combine(target, file.FileName), StrictUtf8),
                "{\"generation\":1}\n",
                $"first publisher should own final generation for {file.FileName}"
            );
        }
        AssertNoTransactionArtifacts(parentHost, "concurrent");
    }

    private void TestConverterRejectsUnownedExactFileSets()
    {
        string parentHost = ProjectSettings.GlobalizePath(FixtureRootVirtual);
        SkillTresToJsonSource firstSource = SkillTresToJsonConverter.Sources[0];
        (string Label, string InvalidJson)[] cases =
        {
            ("bad_json", "{\n"),
            ("wrong_id", OwnedEnvelope(firstSource, "wrong id", entryId: "mage_prismatic_orange_ward")),
            ("extra_member", OwnedEnvelope(firstSource, "extra member", extraRootMember: true)),
            ("multiple_entries", OwnedEnvelope(firstSource, "multiple entries", multipleEntries: true)),
        };

        using var resourceLoader = new TestContentResourceLoader();
        var converter = new SkillTresToJsonConverter(resourceLoader.LoadCanonical<SkillDef>);
        foreach ((string label, string invalidJson) in cases)
        {
            string target = Path.Combine(parentHost, $"unowned_{label}");
            Directory.CreateDirectory(target);
            foreach (SkillTresToJsonSource source in SkillTresToJsonConverter.Sources)
            {
                File.WriteAllText(
                    Path.Combine(target, source.OutputFileName),
                    OwnedEnvelope(source, $"{label} old"),
                    StrictUtf8
                );
            }
            File.WriteAllText(
                Path.Combine(target, firstSource.OutputFileName),
                invalidJson,
                StrictUtf8
            );
            Dictionary<string, byte[]> before = Directory.GetFiles(target)
                .ToDictionary(
                    static path => Path.GetFileName(path),
                    static path => File.ReadAllBytes(path),
                    StringComparer.Ordinal
                );

            bool rejected = false;
            try
            {
                converter.ConvertAndPublish(target);
            }
            catch (IOException exception)
            {
                rejected = exception.Message.Contains(
                    "is not an owned mage_prismatic_ward generation",
                    StringComparison.Ordinal
                );
            }
            _test.True(rejected, $"{label} exact-name target should be rejected as unowned");
            foreach ((string fileName, byte[] bytes) in before)
            {
                _test.True(
                    File.ReadAllBytes(Path.Combine(target, fileName)).AsSpan().SequenceEqual(bytes),
                    $"{label} rejection should preserve original bytes for {fileName}"
                );
            }
            AssertNoTransactionArtifacts(parentHost, $"unowned_{label}");
        }
    }

    private void TestPublishedBackupCleanupFailureIsExplicit()
    {
        string parentHost = ProjectSettings.GlobalizePath(FixtureRootVirtual);
        string target = Path.Combine(parentHost, "cleanup");
        Directory.CreateDirectory(target);
        IReadOnlyList<ContentJsonOutputFile> files = SkillTresToJsonConverter.Sources
            .Select(source => new ContentJsonOutputFile(source.OutputFileName, "{}\n"))
            .ToArray();
        foreach (ContentJsonOutputFile file in files)
            File.WriteAllText(Path.Combine(target, file.FileName), "old\n", StrictUtf8);

        var publisher = new ContentJsonTransactionalDirectoryPublisher(
            beforeBackupDeleteForTests: _ =>
                throw new IOException("synthetic backup cleanup failure")
        );
        bool reported = false;
        try
        {
            publisher.Publish(target, files);
        }
        catch (IOException exception)
        {
            reported =
                exception.Message.Contains("contains the new generation", StringComparison.Ordinal)
                && exception.Message.Contains("requires manual review", StringComparison.Ordinal);
        }

        _test.True(reported, "backup cleanup failure should be reported, not swallowed");
        foreach (ContentJsonOutputFile file in files)
        {
            _test.Eq(File.ReadAllText(Path.Combine(target, file.FileName), StrictUtf8), "{}\n",
                $"cleanup failure should leave the new target generation for {file.FileName}");
        }
        string[] backups = Directory.GetDirectories(parentHost, ".cleanup.backup-*");
        _test.Eq(backups.Length, 1,
            "cleanup failure should preserve exactly one backup for manual review");
    }

    private void AssertOrderedFileNames(IReadOnlyList<string> actual, string label)
    {
        _test.Eq(actual.Count, SkillTresToJsonConverter.Sources.Count,
            $"{label} file count");
        for (int index = 0; index < SkillTresToJsonConverter.Sources.Count; index += 1)
        {
            if (index >= actual.Count)
                break;
            _test.Eq(actual[index], SkillTresToJsonConverter.Sources[index].OutputFileName,
                $"{label} file order {index}");
        }
    }

    private void AssertNoTransactionArtifacts(string parentDirectory, string targetName)
    {
        if (!Directory.Exists(parentDirectory))
            return;
        string[] artifacts = Directory.GetFileSystemEntries(parentDirectory)
            .Where(
                path =>
                    Path.GetFileName(path).StartsWith($".{targetName}.", StringComparison.Ordinal)
            )
            .ToArray();
        _test.Eq(artifacts.Length, 0, $"{targetName} should leave no staging/backup directory");
    }

    private static string Diagnostics(
        string stage,
        IReadOnlyList<ContentJsonDiagnostic> diagnostics
    ) =>
        diagnostics.Count == 0
            ? $"{stage} should succeed"
            : $"{stage} diagnostics: "
                + string.Join(
                    " | ",
                    diagnostics.Select(
                        static diagnostic =>
                            $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: "
                                + diagnostic.Message
                    )
                );

    private static string OwnedEnvelope(
        SkillTresToJsonSource source,
        string description,
        string? entryId = null,
        bool extraRootMember = false,
        bool multipleEntries = false
    )
    {
        string id = JsonSerializer.Serialize(entryId ?? source.SkillId);
        string display = JsonSerializer.Serialize($"Old {source.Color} ward");
        string serializedDescription = JsonSerializer.Serialize(description);
        string entry =
            $"{{\"skill_id\":{id},\"display_name\":{display},\"description\":{serializedDescription}}}";
        string entries = multipleEntries ? $"[{entry},{entry}]" : $"[{entry}]";
        string extra = extraRootMember ? ",\"unexpected\":true" : "";
        return
            $"{{\"schema\":1,\"domain\":\"skills\",\"family\":\"mage_prismatic_ward\","
                + $"\"templates\":{{}},\"entries\":{entries}{extra}}}\n";
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private void AssertNoEngineSerialization(JsonElement element, string fileName, string pointer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string propertyPointer = pointer + "/" + property.Name;
                    _test.False(
                        ForbiddenPropertyNames.Contains(property.Name),
                        $"{fileName}{propertyPointer} should not expose engine metadata"
                    );
                    AssertNoEngineSerialization(property.Value, fileName, propertyPointer);
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertNoEngineSerialization(item, fileName, $"{pointer}/{index}");
                    index += 1;
                }
                break;
            case JsonValueKind.String:
                string value = element.GetString() ?? "";
                bool hasEngineValue =
                    value.Contains("res://", StringComparison.Ordinal)
                    || value.Contains("uid://", StringComparison.Ordinal)
                    || value.Contains(".tres", StringComparison.Ordinal)
                    || value.Contains("[gd_resource", StringComparison.Ordinal)
                    || value.Contains("ExtResource(", StringComparison.Ordinal)
                    || value.Contains("SubResource(", StringComparison.Ordinal)
                    || value.Contains("ResourceLoader", StringComparison.Ordinal)
                    || value.Contains("Godot.", StringComparison.Ordinal)
                    || value is "SkillDef" or "CombatSkillDef" or "CombatEffectDef";
                _test.False(hasEngineValue,
                    $"{fileName}{pointer} should not expose an engine path/class/loader value");
                break;
        }
    }

    private static void RemoveFixtureRoot()
    {
        string hostPath = ProjectSettings.GlobalizePath(FixtureRootVirtual);
        if (Directory.Exists(hostPath))
            Directory.Delete(hostPath, recursive: true);
    }
}
