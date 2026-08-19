#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

internal static class SkillGenerationDiagnosticEnricher
{
    private const string MissingValue = "<missing>";
    private const string UnreadableJson = "<unreadable-json>";
    private const string UnresolvedValue = "<unresolved>";

    internal static IReadOnlyList<ContentJsonDiagnostic> Enrich(
        SkillGenerationValidationStageKind stage,
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        IReadOnlyList<ContentJsonSourceText> sources
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(sources);
        IReadOnlyDictionary<string, ContentJsonSourceText> sourcesByFile = IndexSources(
            sources
        );
        return new ReadOnlyCollection<ContentJsonDiagnostic>(
            diagnostics.Select(diagnostic => Enrich(stage, diagnostic, sourcesByFile)).ToList()
        );
    }

    private static ContentJsonDiagnostic Enrich(
        SkillGenerationValidationStageKind stage,
        ContentJsonDiagnostic diagnostic,
        IReadOnlyDictionary<string, ContentJsonSourceText> sourcesByFile
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        string expected = string.IsNullOrWhiteSpace(diagnostic.Expected)
            ? ExpectedFor(stage, diagnostic.RuleId)
            : diagnostic.Expected;
        string actual = string.IsNullOrWhiteSpace(diagnostic.Actual)
            ? ResolveActual(diagnostic, sourcesByFile)
            : diagnostic.Actual;
        return diagnostic with { Expected = expected, Actual = actual };
    }

    private static IReadOnlyDictionary<string, ContentJsonSourceText> IndexSources(
        IReadOnlyList<ContentJsonSourceText> sources
    )
    {
        var result = new Dictionary<string, ContentJsonSourceText>(StringComparer.Ordinal);
        foreach (ContentJsonSourceText source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            result[FileLabel(source.FilePath)] = source;
        }
        return new ReadOnlyDictionary<string, ContentJsonSourceText>(result);
    }

    private static string ExpectedFor(
        SkillGenerationValidationStageKind stage,
        string ruleId
    ) =>
        ruleId switch
        {
            ContentJsonDocumentLoader.InvalidJsonRule => "well-formed JSON document",
            ContentJsonDocumentLoader.InvalidRootRule => "JSON object document root",
            ContentJsonDocumentLoader.UnknownRootMemberRule =>
                "root member in schema, domain, family, templates, entries",
            ContentJsonDocumentLoader.InvalidSchemaRule => "integer schema version",
            ContentJsonDocumentLoader.SchemaMismatchRule =>
                SkillContentJsonAuthoringDomain.SchemaVersion.ToString(),
            ContentJsonDocumentLoader.InvalidDomainRule => "non-empty domain string",
            ContentJsonDocumentLoader.DomainMismatchRule =>
                SkillContentJsonAuthoringDomain.DomainId,
            ContentJsonDocumentLoader.InvalidFamilyRule => "non-empty family string",
            ContentJsonDocumentLoader.InvalidTemplatesRule => "JSON object templates map",
            ContentJsonDocumentLoader.InvalidEntriesRule => "JSON array entries collection",
            ContentJsonDocumentLoader.InvalidEntryRule => "JSON object entry",
            ContentJsonDocumentLoader.InvalidEntryIdRule =>
                "non-empty canonical skill_id string",
            ContentJsonDocumentLoader.DuplicateEntryIdRule =>
                "skill_id unique across the candidate batch",
            SkillJsonImportRules.InvalidDto => "value accepted by the strict skill DTO",
            SkillJsonImportRules.RequiredMember => "required non-null member",
            SkillJsonImportRules.InvalidId => "canonical lower_snake_case ASCII identifier",
            SkillJsonImportRules.NumberOutOfRange =>
                "numeric value within the field contract",
            SkillJsonImportRules.UnknownSkillType => "registered skill_type value",
            SkillJsonImportRules.UnknownLearnSource => "registered learn_source value",
            SkillJsonImportRules.UnknownTargetMode => "registered target_mode value",
            SkillJsonImportRules.UnknownTargetTeamFilter =>
                "registered target_team_filter value",
            SkillJsonImportRules.UnknownRangePattern => "registered range_pattern value",
            SkillJsonImportRules.UnknownAreaPattern => "registered area_pattern value",
            SkillJsonImportRules.UnknownEffectKind => "registered effect_type value",
            SkillJsonImportRules.MissingEffectPayload => "required effect payload object",
            SkillJsonImportRules.InvalidEffectPayload =>
                "payload accepted by the selected effect_type contract",
            SkillJsonImportRules.SkillIdMismatch =>
                "combat_profile.skill_id equal to entry skill_id",
            SkillJsonImportRules.DuplicateLevelKey => "unique canonical level key",
            SkillJsonImportRules.DuplicateDescriptionVariableKey =>
                "unique level description variable key",
            SkillJsonImportRules.UnknownPendingCastBindingMode =>
                "registered pending_cast_binding_mode value",
            SkillJsonImportRules.UnknownAttackResolutionMode =>
                "registered attack_resolution_mode value",
            SkillJsonImportRules.UnknownAttackDefenseMode =>
                "registered attack_defense_mode value",
            SkillJsonImportRules.UnknownLevelOverrideAreaPattern =>
                "registered level override area_pattern value",
            "skill.validation.domain_rule" => "skill accepted by domain validator",
            SkillGenerationCrossDomainRules.ExistingSkillId =>
                "skill_id absent from the process snapshot",
            SkillGenerationCrossDomainRules.MissingSkillReference =>
                "skill_id present in the combined skill catalog",
            SkillGenerationCrossDomainRules.MissingAchievementReference =>
                "achievement_id present in the process snapshot",
            SkillGenerationCrossDomainRules.MissingBarrierProfile =>
                "barrier profile_id present in the process snapshot",
            SkillGenerationCrossDomainRules.MissingSpecialProfile =>
                "battle special profile_id present in the process snapshot",
            SkillGenerationCrossDomainRules.InvalidReactionSkill =>
                "active unit reaction skill with weapon-dice damage",
            _ => $"value satisfying {ruleId}",
        };

    private static string ResolveActual(
        ContentJsonDiagnostic diagnostic,
        IReadOnlyDictionary<string, ContentJsonSourceText> sourcesByFile
    )
    {
        string fileLabel = SourceFileLabel(diagnostic.SourceLabel);
        if (!sourcesByFile.TryGetValue(fileLabel, out ContentJsonSourceText? source))
            return UnresolvedValue;
        try
        {
            using JsonDocument document = JsonDocument.Parse(source.Utf8Json ?? "");
            if (!TryResolvePointer(
                    document.RootElement,
                    diagnostic.JsonPointer,
                    out JsonElement value
                ))
            {
                return MissingValue;
            }
            return value.GetRawText();
        }
        catch (JsonException)
        {
            return UnreadableJson;
        }
    }

    private static bool TryResolvePointer(
        JsonElement root,
        string? pointer,
        out JsonElement value
    )
    {
        value = root;
        if (string.IsNullOrEmpty(pointer))
            return true;
        if (pointer[0] != '/')
            return false;

        foreach (string encodedToken in pointer[1..].Split('/'))
        {
            string token = encodedToken
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(token, out JsonElement property))
                    return false;
                value = property;
                continue;
            }
            if (
                value.ValueKind == JsonValueKind.Array
                && int.TryParse(token, out int index)
                && index >= 0
                && index < value.GetArrayLength()
            )
            {
                value = value[index];
                continue;
            }
            return false;
        }
        return true;
    }

    private static string SourceFileLabel(string sourceLabel)
    {
        string value = sourceLabel ?? "";
        int separator = value.IndexOf('#');
        return separator >= 0 ? value[..separator] : value;
    }

    private static string FileLabel(string path)
    {
        string normalized = (path ?? "").Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator >= 0 ? normalized[(separator + 1)..] : normalized;
    }
}

internal sealed class SkillGenerationBufferedSourceReader : IContentJsonSourceReader
{
    private readonly IReadOnlyList<ContentJsonSourceText> _sources;

    internal SkillGenerationBufferedSourceReader(
        IReadOnlyList<ContentJsonSourceText> sources
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = new ReadOnlyCollection<ContentJsonSourceText>(
            sources.Select(source =>
                source ?? throw new ArgumentException(
                    "Generated skill source collection cannot contain null.",
                    nameof(sources)
                )
            ).ToList()
        );
    }

    public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) =>
        _sources;
}
