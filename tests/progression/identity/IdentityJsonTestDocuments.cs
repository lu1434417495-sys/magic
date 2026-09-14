#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal sealed class IdentityJsonTestSourceReader : IContentJsonSourceReader
{
    private readonly IReadOnlyList<ContentJsonSourceText> _sources;

    internal IdentityJsonTestSourceReader(string domain, params string[] entries)
    {
        string entryJson = string.Join(",", entries ?? Array.Empty<string>());
        string document =
            $"{{\"schema\":1,\"domain\":{JsonSerializer.Serialize(domain)},\"family\":\"test\",\"templates\":{{}},\"entries\":[{entryJson}]}}";
        _sources = new[] { new ContentJsonSourceText($"{domain}.json", document) };
    }

    public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) =>
        _sources;
}

internal static class IdentityJsonTestDocuments
{
    internal static string Entry<T>(T value, JsonTypeInfo<T> typeInfo) where T : notnull =>
        JsonSerializer.Serialize(value, typeInfo);

    internal static string Bloodline(string entryId, string kind, BloodlineJsonDto value) =>
        ClosedEntry(entryId, kind, Entry(value, ProfessionIdentityJsonSerializerContext.Default.BloodlineJsonDto));

    internal static string BloodlineStage(string entryId, BloodlineStageJsonDto value) =>
        ClosedEntry(entryId, "stage", Entry(value, ProfessionIdentityJsonSerializerContext.Default.BloodlineStageJsonDto));

    internal static string Ascension(string entryId, string kind, AscensionJsonDto value) =>
        ClosedEntry(entryId, kind, Entry(value, ProfessionIdentityJsonSerializerContext.Default.AscensionJsonDto));

    internal static string AscensionStage(string entryId, AscensionStageJsonDto value) =>
        ClosedEntry(entryId, "stage", Entry(value, ProfessionIdentityJsonSerializerContext.Default.AscensionStageJsonDto));

    private static string ClosedEntry(string entryId, string kind, string payload) =>
        $"{{\"entry_id\":{JsonSerializer.Serialize(entryId)},\"kind\":{JsonSerializer.Serialize(kind)},\"payload\":{payload}}}";
}
