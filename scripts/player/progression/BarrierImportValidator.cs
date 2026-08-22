#nullable enable

using System;
using System.Collections.Generic;

internal static class BarrierImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateProfile(
        JsonContentEntryContext context,
        BarrierProfileImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.ProfileId, "/profile_id");
        RequireText(diagnostics, context, import.DisplayName, "/display_name", "display_name");
        if (import.AnchorMode == BarrierAnchorImportKind.Unknown)
            Unsupported(diagnostics, context, "anchor_mode", "/anchor_mode");
        if (import.AreaPattern == BarrierAreaPatternImportKind.Unknown)
            Unsupported(diagnostics, context, "area_pattern", "/area_pattern");
        if (import.RadiusCells < 0)
            OutOfRange(diagnostics, context, "radius_cells must be >= 0.", "/radius_cells");
        if (import.DurationTu < 0)
            OutOfRange(diagnostics, context, "duration_tu must be >= 0.", "/duration_tu");
        if (import.LayerIds.Count == 0)
            Required(diagnostics, context, "layer_ids must be non-empty.", "/layer_ids");
        AddDuplicateStrings(diagnostics, context, import.LayerIds, "/layer_ids", "layer id");
        return diagnostics;
    }

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateLayer(
        JsonContentEntryContext context,
        BarrierLayerImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.LayerId, "/layer_id");
        RequireText(diagnostics, context, import.DisplayName, "/display_name", "display_name");
        if (import.Order <= 0)
            OutOfRange(diagnostics, context, "order must be positive.", "/order");
        AddDuplicateStrings(diagnostics, context, import.BlockedCategories, "/blocked_categories", "blocked category");
        AddDuplicateStrings(diagnostics, context, import.BreakerSkillIds, "/breaker_skill_ids", "breaker skill id");
        for (int index = 0; index < import.PassageOutcomes.Count; index += 1)
        {
            BarrierOutcomeImportModel outcome = import.PassageOutcomes[index];
            string pointer = $"/passage_outcomes/{index}";
            if (outcome.OutcomeKind is BarrierOutcomeImportKind.None or BarrierOutcomeImportKind.Unknown)
                Unsupported(diagnostics, context, "outcome_type", $"{pointer}/outcome_type");
            if (outcome.Amount < 0 || outcome.SuccessAmount < 0 || outcome.SaveDc < 0)
                OutOfRange(diagnostics, context, "outcome numeric values must be >= 0.", pointer);
            if (outcome.FatalDamage <= 0)
                OutOfRange(diagnostics, context, "fatal_damage must be positive.", $"{pointer}/fatal_damage");
        }
        return diagnostics;
    }

    private static void RequireIdentity(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string value,
        string pointer
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(diagnostics, context, BarrierJsonRules.IdRequired, "Content entry id is required.", pointer);
        else if (!string.Equals(value, context.EntryId, StringComparison.Ordinal))
            Add(diagnostics, context, BarrierJsonRules.IdMismatch, "Content entry id must match the envelope entry id.", pointer);
    }

    private static void RequireText(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string value,
        string pointer,
        string label
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            Required(diagnostics, context, $"{label} is required.", pointer);
    }

    private static void AddDuplicateStrings(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        IReadOnlyList<string> values,
        string pointer,
        string label
    )
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                Required(diagnostics, context, $"{label} must be non-empty.", pointer);
            else if (!seen.Add(value))
                Add(diagnostics, context, BarrierJsonRules.DuplicateId, $"Duplicate {label} '{value}'.", pointer);
        }
    }

    private static void Required(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) =>
        Add(diagnostics, context, BarrierJsonRules.ValueRequired, message, pointer);

    private static void Unsupported(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string label, string pointer) =>
        Add(diagnostics, context, BarrierJsonRules.ValueUnsupported, $"{label} is unsupported.", pointer);

    private static void OutOfRange(List<ContentJsonDiagnostic> diagnostics, JsonContentEntryContext context, string message, string pointer) =>
        Add(diagnostics, context, BarrierJsonRules.ValueOutOfRange, message, pointer);

    private static void Add(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string pointer
    ) =>
        diagnostics.Add(
            new ContentJsonDiagnostic(
                ruleId,
                message,
                context.SourceLabel,
                $"{context.JsonPointer}{pointer}"
            )
        );
}
