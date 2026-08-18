#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

internal sealed record SkillTresToJsonSource(
    string Color,
    string SkillId,
    string ResourcePath,
    string OutputFileName
);

internal sealed class SkillTresToJsonExportResult
{
    internal SkillTresToJsonExportResult(
        string outputDirectory,
        IReadOnlyList<string> fileNames
    )
    {
        OutputDirectory = outputDirectory;
        FileNames = new ReadOnlyCollection<string>(new List<string>(fileNames));
    }

    internal string OutputDirectory { get; }
    internal IReadOnlyList<string> FileNames { get; }
    internal int EntryCount => FileNames.Count;
}

/// <summary>
/// Converts the fixed stage-1a prismatic ward manifest through the sole Resource-to-import
/// adapter and canonical writer. The complete output is built in memory before publication.
/// </summary>
internal sealed class SkillTresToJsonConverter
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );
    private static readonly IReadOnlyList<SkillTresToJsonSource> Manifest =
        Array.AsReadOnly(
            new[]
            {
                Source("red"),
                Source("orange"),
                Source("yellow"),
                Source("green"),
                Source("blue"),
                Source("indigo"),
                Source("violet"),
            }
        );

    private readonly Func<string, SkillDef?> _loadResource;
    private readonly ContentCanonicalJsonWriter _writer;
    private readonly ContentJsonTransactionalDirectoryPublisher _publisher;

    internal SkillTresToJsonConverter(
        Func<string, SkillDef?>? loadResource = null,
        ContentCanonicalJsonWriter? writer = null,
        ContentJsonTransactionalDirectoryPublisher? publisher = null
    )
    {
        _loadResource = loadResource ?? LoadResource;
        _writer = writer ?? new ContentCanonicalJsonWriter();
        _publisher = publisher ?? new ContentJsonTransactionalDirectoryPublisher(
            validateExistingTargetOwnership: ValidateExistingTargetOwnership
        );
    }

    internal static IReadOnlyList<SkillTresToJsonSource> Sources => Manifest;

    internal SkillTresToJsonExportResult ConvertAndPublish(string targetDirectory)
    {
        IReadOnlyList<ContentJsonOutputFile> files = ConvertAllToMemory();
        string target = Path.GetFullPath(targetDirectory);
        _publisher.Publish(target, files);
        return new SkillTresToJsonExportResult(
            target,
            files.Select(static file => file.FileName).ToArray()
        );
    }

    internal IReadOnlyList<ContentJsonOutputFile> ConvertAllToMemory()
    {
        // ResourceLoader returns borrowed roots. Keep all seven references alive until every
        // adapter invocation has finished; callers must not Dispose these path-backed roots.
        var loadedResources = new List<(SkillTresToJsonSource Source, SkillDef Resource)>(
            Manifest.Count
        );
        foreach (SkillTresToJsonSource source in Manifest)
        {
            SkillDef? resource = _loadResource(source.ResourcePath);
            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"Could not load skill Resource '{source.ResourcePath}' as SkillDef."
                );
            }
            loadedResources.Add((source, resource));
        }

        var files = new List<ContentJsonOutputFile>(Manifest.Count);
        foreach ((SkillTresToJsonSource source, SkillDef resource) in loadedResources)
        {
            var context = new JsonContentEntryContext(
                SkillContentJsonAuthoringDomain.DomainId,
                source.SkillId,
                source.ResourcePath,
                "/entries/0"
            );
            ContentImportStageResult<SkillImportModel> adapted =
                SkillTresImportAdapter.TryAdapt(context, resource);
            if (!adapted.HasValue)
            {
                string diagnostics = string.Join(
                    " | ",
                    adapted.Diagnostics.Select(
                        static diagnostic =>
                            $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: "
                                + diagnostic.Message
                    )
                );
                throw new InvalidOperationException(
                    $"Skill Resource '{source.ResourcePath}' could not be adapted: {diagnostics}"
                );
            }

            SkillImportModel import = adapted.Value;
            if (!string.Equals(import.SkillId.Value, source.SkillId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Skill Resource '{source.ResourcePath}' adapted as '{import.SkillId.Value}', "
                        + $"not manifest id '{source.SkillId}'."
                );
            }

            files.Add(
                new ContentJsonOutputFile(
                    source.OutputFileName,
                    SkillImportCanonicalJson.WriteSingleEntry(_writer, import)
                )
            );
        }

        return new ReadOnlyCollection<ContentJsonOutputFile>(files);
    }

    internal static void ValidateExistingTargetOwnership(string targetDirectory)
    {
        foreach (SkillTresToJsonSource source in Manifest)
        {
            string path = Path.Combine(targetDirectory, source.OutputFileName);
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string json = StrictUtf8.GetString(bytes);
                using JsonDocument document = JsonDocument.Parse(json);
                ValidateOwnedDocument(document.RootElement, source);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or DecoderFallbackException
                    or JsonException
                    or InvalidDataException
            )
            {
                throw new IOException(
                    $"Existing skill JSON target file '{source.OutputFileName}' is not an owned "
                        + $"{SkillImportCanonicalJson.FamilyId} generation: {exception.Message}",
                    exception
                );
            }
        }
    }

    private static void ValidateOwnedDocument(
        JsonElement root,
        SkillTresToJsonSource source
    )
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Root must be an object.");

        string[] expectedMembers = { "schema", "domain", "family", "templates", "entries" };
        string[] actualMembers = root.EnumerateObject()
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actualMembers.SequenceEqual(expectedMembers.OrderBy(static x => x, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidDataException("Root must contain the exact canonical envelope members.");

        if (
            !root.TryGetProperty("schema", out JsonElement schema)
            || !schema.TryGetInt32(out int schemaVersion)
            || schemaVersion != SkillContentJsonAuthoringDomain.SchemaVersion
        )
        {
            throw new InvalidDataException("Schema version is not canonical.");
        }
        if (
            !root.TryGetProperty("domain", out JsonElement domain)
            || domain.ValueKind != JsonValueKind.String
            || !string.Equals(
                domain.GetString(),
                SkillContentJsonAuthoringDomain.DomainId,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidDataException("Domain is not canonical.");
        }
        if (
            !root.TryGetProperty("family", out JsonElement family)
            || family.ValueKind != JsonValueKind.String
            || !string.Equals(
                family.GetString(),
                SkillImportCanonicalJson.FamilyId,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidDataException("Family is not canonical.");
        }
        if (
            !root.TryGetProperty("templates", out JsonElement templates)
            || templates.ValueKind != JsonValueKind.Object
            || templates.EnumerateObject().Any()
        )
        {
            throw new InvalidDataException("Templates must be an empty object.");
        }
        if (
            !root.TryGetProperty("entries", out JsonElement entries)
            || entries.ValueKind != JsonValueKind.Array
            || entries.GetArrayLength() != 1
        )
        {
            throw new InvalidDataException("Entries must contain exactly one skill.");
        }

        JsonElement entry = entries[0];
        if (
            entry.ValueKind != JsonValueKind.Object
            || !entry.TryGetProperty("skill_id", out JsonElement skillId)
            || skillId.ValueKind != JsonValueKind.String
            || !string.Equals(skillId.GetString(), source.SkillId, StringComparison.Ordinal)
        )
        {
            throw new InvalidDataException(
                $"Entry skill_id must match manifest id '{source.SkillId}'."
            );
        }
    }

    private static SkillTresToJsonSource Source(string color)
    {
        string skillId = $"mage_prismatic_{color}_ward";
        return new SkillTresToJsonSource(
            color,
            skillId,
            $"res://data/configs/skills/{skillId}.tres",
            $"{skillId}.json"
        );
    }

    private static SkillDef? LoadResource(string resourcePath) =>
        ResourceLoader.Load<SkillDef>(
            resourcePath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
}
