using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class ContentJsonNullabilityPolicy
{
    internal static ContentJsonNullabilityPolicy None { get; } = new(Array.Empty<string>());

    private readonly HashSet<string> _nullableSchemaPointers;

    internal ContentJsonNullabilityPolicy(IEnumerable<string> nullableSchemaPointers)
    {
        ArgumentNullException.ThrowIfNull(nullableSchemaPointers);

        _nullableSchemaPointers = new HashSet<string>(StringComparer.Ordinal);
        foreach (string pointer in nullableSchemaPointers)
        {
            if (
                string.IsNullOrWhiteSpace(pointer)
                || !pointer.StartsWith("/", StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    "Nullable schema pointers must be non-empty absolute JSON pointers.",
                    nameof(nullableSchemaPointers)
                );
            }

            _nullableSchemaPointers.Add(pointer);
        }
    }

    internal bool AllowsNull(string schemaPointer) =>
        _nullableSchemaPointers.Contains(schemaPointer);
}

internal sealed class ContentJsonTemplateMergeResult
{
    internal ContentJsonTemplateMergeResult(
        IList<ContentJsonEntryDocument> entries,
        IList<ContentJsonDiagnostic> diagnostics
    )
    {
        Entries = new ReadOnlyCollection<ContentJsonEntryDocument>(
            new List<ContentJsonEntryDocument>(entries)
        );
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            new List<ContentJsonDiagnostic>(diagnostics)
        );
    }

    internal IReadOnlyList<ContentJsonEntryDocument> Entries { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal bool HasErrors => Diagnostics.Count > 0;
}

internal static class ContentJsonTemplateMerger
{
    internal const string InvalidTemplateObjectRule =
        "content.json.template.invalid_template_object";
    internal const string InvalidTemplateReferenceRule =
        "content.json.template.invalid_template_reference";
    internal const string UnknownTemplateRule = "content.json.template.unknown_template";
    internal const string TemplateCycleRule = "content.json.template.cycle";
    internal const string NullNotAllowedRule = "content.json.template.null_not_allowed";

    private const string TemplatePropertyName = "template";

    internal static ContentJsonTemplateMergeResult Merge(
        ContentJsonDocumentEnvelope document,
        ContentJsonNullabilityPolicy nullabilityPolicy
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(nullabilityPolicy);

        string fileLabel = FileLabel(document.FilePath);
        var diagnostics = new List<ContentJsonDiagnostic>();
        Dictionary<string, TemplateSource> templates = ParseTemplates(
            document,
            fileLabel,
            nullabilityPolicy,
            diagnostics
        );
        var resolver = new TemplateResolver(fileLabel, templates, diagnostics);

        foreach (string templateId in templates.Keys.OrderBy(id => id, StringComparer.Ordinal))
            resolver.Resolve(templateId);

        var mergedEntries = new List<ContentJsonEntryDocument>();
        for (int index = 0; index < document.Entries.Count; index += 1)
        {
            ContentJsonEntryDocument entry = document.Entries[index];
            string sourceLabel = $"{fileLabel}#{entry.EntryId}";
            string sourcePointer = $"/entries/{index}";
            JsonObject entryObject = ParseObject(
                entry.Json,
                sourceLabel,
                sourcePointer,
                InvalidTemplateObjectRule,
                "JSON content entry must be an object before template merge.",
                diagnostics
            );
            if (entryObject == null)
                continue;

            ValidateNulls(
                entryObject,
                schemaPointer: "",
                sourcePointer,
                sourceLabel,
                nullabilityPolicy,
                diagnostics,
                skipTemplateControlMember: true
            );

            if (
                !TryReadTemplateReference(
                    entryObject,
                    sourceLabel,
                    sourcePointer,
                    diagnostics,
                    out string templateId
                )
            )
            {
                continue;
            }

            JsonObject baseObject = new();
            if (templateId.Length > 0)
            {
                if (!templates.ContainsKey(templateId))
                {
                    diagnostics.Add(
                        new ContentJsonDiagnostic(
                            UnknownTemplateRule,
                            $"JSON content entry references unknown file-local template '{templateId}'.",
                            sourceLabel,
                            $"{sourcePointer}/{TemplatePropertyName}"
                        )
                    );
                    continue;
                }

                JsonObject resolvedTemplate = resolver.Resolve(templateId);
                if (resolvedTemplate == null)
                    continue;
                baseObject = resolvedTemplate;
            }

            JsonObject merged = MergeObjects(baseObject, entryObject);
            ValidateNulls(
                merged,
                schemaPointer: "",
                sourcePointer,
                sourceLabel,
                nullabilityPolicy,
                diagnostics,
                skipTemplateControlMember: false
            );
            mergedEntries.Add(new ContentJsonEntryDocument(entry.EntryId, merged.ToJsonString()));
        }

        if (diagnostics.Count > 0)
            mergedEntries.Clear();

        return new ContentJsonTemplateMergeResult(mergedEntries, diagnostics);
    }

    private static Dictionary<string, TemplateSource> ParseTemplates(
        ContentJsonDocumentEnvelope document,
        string fileLabel,
        ContentJsonNullabilityPolicy nullabilityPolicy,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var templates = new Dictionary<string, TemplateSource>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<string, string> template in document.Templates.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal
            )
        )
        {
            string escapedTemplateId = EscapePointerToken(template.Key);
            string sourcePointer = $"/templates/{escapedTemplateId}";
            string sourceLabel = $"{fileLabel}#{template.Key}";
            JsonObject templateObject = ParseObject(
                template.Value,
                sourceLabel,
                sourcePointer,
                InvalidTemplateObjectRule,
                "JSON content template must be an object.",
                diagnostics
            );
            if (templateObject == null)
                continue;

            ValidateNulls(
                templateObject,
                schemaPointer: "",
                sourcePointer,
                sourceLabel,
                nullabilityPolicy,
                diagnostics,
                skipTemplateControlMember: true
            );

            if (
                !TryReadTemplateReference(
                    templateObject,
                    sourceLabel,
                    sourcePointer,
                    diagnostics,
                    out string parentTemplateId
                )
            )
            {
                continue;
            }

            templates.Add(
                template.Key,
                new TemplateSource(template.Key, templateObject, parentTemplateId, sourcePointer)
            );
        }
        return templates;
    }

    private static JsonObject ParseObject(
        string rawJson,
        string sourceLabel,
        string sourcePointer,
        string ruleId,
        string message,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        JsonNode node;
        try
        {
            node = JsonNode.Parse(
                rawJson ?? "",
                nodeOptions: null,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                }
            );
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    ruleId,
                    $"{message} Malformed JSON: {exception.Message}",
                    sourceLabel,
                    sourcePointer
                )
            );
            return null;
        }

        if (node is JsonObject jsonObject)
            return jsonObject;

        diagnostics.Add(new ContentJsonDiagnostic(ruleId, message, sourceLabel, sourcePointer));
        return null;
    }

    private static bool TryReadTemplateReference(
        JsonObject source,
        string sourceLabel,
        string sourcePointer,
        List<ContentJsonDiagnostic> diagnostics,
        out string templateId
    )
    {
        templateId = "";
        if (!source.TryGetPropertyValue(TemplatePropertyName, out JsonNode value))
            return true;

        if (
            value is JsonValue jsonValue
            && jsonValue.TryGetValue(out string parsedTemplateId)
            && !string.IsNullOrWhiteSpace(parsedTemplateId)
        )
        {
            templateId = parsedTemplateId;
            return true;
        }

        diagnostics.Add(
            new ContentJsonDiagnostic(
                InvalidTemplateReferenceRule,
                "JSON content 'template' must be a non-empty string when present.",
                sourceLabel,
                $"{sourcePointer}/{TemplatePropertyName}"
            )
        );
        return false;
    }

    private static JsonObject MergeObjects(
        JsonObject parent,
        JsonObject child,
        bool stripRootTemplateControlMember = true
    )
    {
        var merged = (JsonObject)parent.DeepClone();
        foreach (KeyValuePair<string, JsonNode> property in child)
        {
            if (
                stripRootTemplateControlMember
                && string.Equals(property.Key, TemplatePropertyName, StringComparison.Ordinal)
            )
                continue;

            if (
                property.Value is JsonObject childObject
                && merged[property.Key] is JsonObject parentObject
            )
            {
                merged[property.Key] = MergeObjects(
                    parentObject,
                    childObject,
                    stripRootTemplateControlMember: false
                );
                continue;
            }

            merged[property.Key] = property.Value?.DeepClone();
        }
        return merged;
    }

    private static void ValidateNulls(
        JsonNode node,
        string schemaPointer,
        string sourcePointer,
        string sourceLabel,
        ContentJsonNullabilityPolicy nullabilityPolicy,
        List<ContentJsonDiagnostic> diagnostics,
        bool skipTemplateControlMember
    )
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode> property in jsonObject)
            {
                if (
                    skipTemplateControlMember
                    && string.Equals(property.Key, TemplatePropertyName, StringComparison.Ordinal)
                )
                {
                    continue;
                }

                string token = EscapePointerToken(property.Key);
                string childSchemaPointer = $"{schemaPointer}/{token}";
                string childSourcePointer = $"{sourcePointer}/{token}";
                if (property.Value == null)
                {
                    if (!nullabilityPolicy.AllowsNull(childSchemaPointer))
                    {
                        diagnostics.Add(
                            new ContentJsonDiagnostic(
                                NullNotAllowedRule,
                                $"JSON null is not allowed by the schema at '{childSchemaPointer}'.",
                                sourceLabel,
                                childSourcePointer
                            )
                        );
                    }
                    continue;
                }

                ValidateNulls(
                    property.Value,
                    childSchemaPointer,
                    childSourcePointer,
                    sourceLabel,
                    nullabilityPolicy,
                    diagnostics,
                    skipTemplateControlMember: false
                );
            }
            return;
        }

        if (node is not JsonArray jsonArray)
            return;

        for (int index = 0; index < jsonArray.Count; index += 1)
        {
            string childSchemaPointer = $"{schemaPointer}/*";
            string childSourcePointer = $"{sourcePointer}/{index}";
            JsonNode child = jsonArray[index];
            if (child == null)
            {
                if (!nullabilityPolicy.AllowsNull(childSchemaPointer))
                {
                    diagnostics.Add(
                        new ContentJsonDiagnostic(
                            NullNotAllowedRule,
                            $"JSON null is not allowed by the schema at '{childSchemaPointer}'.",
                            sourceLabel,
                            childSourcePointer
                        )
                    );
                }
                continue;
            }

            ValidateNulls(
                child,
                childSchemaPointer,
                childSourcePointer,
                sourceLabel,
                nullabilityPolicy,
                diagnostics,
                skipTemplateControlMember: false
            );
        }
    }

    private static string EscapePointerToken(string value) =>
        (value ?? "").Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string FileLabel(string filePath)
    {
        string normalized = (filePath ?? "").Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }

    private sealed record TemplateSource(
        string TemplateId,
        JsonObject Json,
        string ParentTemplateId,
        string SourcePointer
    );

    private enum VisitState
    {
        Visiting,
        Complete,
    }

    private sealed class TemplateResolver
    {
        private readonly string _fileLabel;
        private readonly IReadOnlyDictionary<string, TemplateSource> _templates;
        private readonly List<ContentJsonDiagnostic> _diagnostics;
        private readonly Dictionary<string, VisitState> _states = new(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonObject> _resolved = new(StringComparer.Ordinal);
        private readonly List<string> _stack = new();
        private readonly HashSet<string> _reportedCycles = new(StringComparer.Ordinal);

        internal TemplateResolver(
            string fileLabel,
            IReadOnlyDictionary<string, TemplateSource> templates,
            List<ContentJsonDiagnostic> diagnostics
        )
        {
            _fileLabel = fileLabel;
            _templates = templates;
            _diagnostics = diagnostics;
        }

        internal JsonObject Resolve(string templateId)
        {
            if (_resolved.TryGetValue(templateId, out JsonObject resolved))
                return (JsonObject)resolved.DeepClone();

            if (_states.TryGetValue(templateId, out VisitState state))
            {
                if (state == VisitState.Complete)
                    return null;

                AppendCycleDiagnostic(templateId);
                return null;
            }

            if (!_templates.TryGetValue(templateId, out TemplateSource source))
                return null;

            _states[templateId] = VisitState.Visiting;
            _stack.Add(templateId);

            JsonObject parent = new();
            bool canResolve = true;
            if (source.ParentTemplateId.Length > 0)
            {
                if (!_templates.ContainsKey(source.ParentTemplateId))
                {
                    _diagnostics.Add(
                        new ContentJsonDiagnostic(
                            UnknownTemplateRule,
                            $"JSON content template '{templateId}' references unknown file-local template '{source.ParentTemplateId}'.",
                            $"{_fileLabel}#{templateId}",
                            $"{source.SourcePointer}/{TemplatePropertyName}"
                        )
                    );
                    canResolve = false;
                }
                else
                {
                    JsonObject resolvedParent = Resolve(source.ParentTemplateId);
                    if (resolvedParent == null)
                        canResolve = false;
                    else
                        parent = resolvedParent;
                }
            }

            JsonObject result = canResolve ? MergeObjects(parent, source.Json) : null;
            _stack.RemoveAt(_stack.Count - 1);
            _states[templateId] = VisitState.Complete;
            if (result == null)
                return null;

            _resolved[templateId] = result;
            return (JsonObject)result.DeepClone();
        }

        private void AppendCycleDiagnostic(string repeatedTemplateId)
        {
            int cycleStart = _stack.IndexOf(repeatedTemplateId);
            IEnumerable<string> cycleIds = cycleStart >= 0
                ? _stack.Skip(cycleStart).Concat(new[] { repeatedTemplateId })
                : _stack.Concat(new[] { repeatedTemplateId });
            string cycle = string.Join(" -> ", cycleIds);
            if (!_reportedCycles.Add(cycle))
                return;

            string referringTemplateId = _stack.Count > 0 ? _stack[^1] : repeatedTemplateId;
            TemplateSource referringSource = _templates[referringTemplateId];
            _diagnostics.Add(
                new ContentJsonDiagnostic(
                    TemplateCycleRule,
                    $"File-local JSON template inheritance cycle detected: {cycle}.",
                    $"{_fileLabel}#{referringTemplateId}",
                    $"{referringSource.SourcePointer}/{TemplatePropertyName}"
                )
            );
        }
    }
}
