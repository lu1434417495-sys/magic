#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal static class EnemyContentJsonAuthoringDomains
{
    private static readonly ContentJsonNullabilityPolicy BrainNullabilityPolicy = new(
        new[] { "/score_profile" }
    );
    private static readonly ContentJsonNullabilityPolicy StrictNullabilityPolicy = new(
        Array.Empty<string>()
    );

    internal static JsonContentDomainDescriptor<EnemyAiBrainJsonDto, EnemyAiBrainImportModel>
        CreateBrainDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            EnemyContentJsonDomains.BrainDomainId,
            EnemyContentJsonDomains.SchemaVersion,
            "brain_id",
            sourceDirectory,
            sourceReader,
            BrainNullabilityPolicy,
            EnemyContentJsonImportParser.ParseBrain,
            EnemyContentJsonImportParser.NormalizeBrain,
            EnemyContentImportValidator.ValidateBrain
        );

    internal static JsonContentDomainDescriptor<EnemyTemplateJsonDto, EnemyTemplateJsonDto>
        CreateTemplateDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            EnemyContentJsonDomains.TemplateDomainId,
            EnemyContentJsonDomains.SchemaVersion,
            "template_id",
            sourceDirectory,
            sourceReader,
            StrictNullabilityPolicy,
            EnemyContentJsonImportParser.ParseTemplate,
            static (_, value) => ContentImportStageResult<EnemyTemplateJsonDto>.Success(value),
            EnemyContentImportValidator.ValidateTemplate
        );

    internal static JsonContentDomainDescriptor<EncounterRosterJsonDto, EncounterRosterJsonDto>
        CreateRosterDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            EnemyContentJsonDomains.RosterDomainId,
            EnemyContentJsonDomains.SchemaVersion,
            "profile_id",
            sourceDirectory,
            sourceReader,
            StrictNullabilityPolicy,
            EnemyContentJsonImportParser.ParseRoster,
            static (_, value) => ContentImportStageResult<EncounterRosterJsonDto>.Success(value),
            EnemyContentImportValidator.ValidateRoster
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineBrainDomain();
        yield return new OfflineTemplateDomain();
        yield return new OfflineRosterDomain();
    }

    private sealed class OfflineBrainDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => EnemyContentJsonDomains.BrainDomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<EnemyAiBrainImportModel> batch = CreateBrainDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }

    private sealed class OfflineTemplateDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => EnemyContentJsonDomains.TemplateDomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<EnemyTemplateJsonDto> batch = CreateTemplateDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }

    private sealed class OfflineRosterDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => EnemyContentJsonDomains.RosterDomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<EncounterRosterJsonDto> batch = CreateRosterDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }
}

internal static class EnemyContentImportRules
{
    internal const string IdRequired = "enemy.content.id.required";
    internal const string IdMismatch = "enemy.content.id.mismatch";
    internal const string CollectionRequired = "enemy.content.collection.required";
    internal const string DuplicateId = "enemy.content.id.duplicate";
    internal const string ValueOutOfRange = "enemy.content.value.out_of_range";
    internal const string ValueUnsupported = "enemy.content.value.unsupported";
    internal const string ReferenceMissing = "enemy.content.reference.missing";
}

internal static partial class EnemyContentImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateBrain(
        JsonContentEntryContext context,
        EnemyAiBrainImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.BrainId, "/brain_id");
        if (string.IsNullOrWhiteSpace(import.DefaultStateId))
            Add(diagnostics, context, EnemyContentImportRules.IdRequired, "Enemy AI brain must declare default_state_id.", "/default_state_id");
        if (import.States.Count == 0)
            Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, "Enemy AI brain must declare at least one state.", "/states");
        AddDuplicateIds(diagnostics, context, import.States.Select(value => value.StateId), "/states", "state_id");
        for (int stateIndex = 0; stateIndex < import.States.Count; stateIndex += 1)
        {
            EnemyAiStateImportModel state = import.States[stateIndex];
            if (state.Actions.Count == 0)
                Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, "Enemy AI state must declare at least one action.", $"/states/{stateIndex}/actions");
            AddDuplicateIds(diagnostics, context, state.Actions.Select(value => value.Payload.ActionId), $"/states/{stateIndex}/actions", "action_id");
            AddDuplicateIds(diagnostics, context, state.GenerationSlots.Select(value => value.SlotId), $"/states/{stateIndex}/generation_slots", "slot_id");
            AddDuplicateInts(diagnostics, context, state.GenerationSlots.Select(value => value.Order), $"/states/{stateIndex}/generation_slots", "order");
        }
        AddDuplicateIds(diagnostics, context, import.TransitionRules.Select(value => value.RuleId), "/transition_rules", "rule_id");
        AddDuplicateInts(diagnostics, context, import.TransitionRules.Select(value => value.Order), "/transition_rules", "order");
        AppendBrainRuleDiagnostics(diagnostics, context, import);
        return diagnostics;
    }

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateTemplate(
        JsonContentEntryContext context,
        EnemyTemplateJsonDto import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.TemplateId, "/template_id");
        if (string.IsNullOrWhiteSpace(import.DisplayName))
            Add(diagnostics, context, EnemyContentImportRules.IdRequired, "Enemy template must declare display_name.", "/display_name");
        if (import.EnemyCount <= 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "enemy_count must be >= 1.", "/enemy_count");
        if (import.CreatureLevel < 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "creature_level must be >= 0.", "/creature_level");
        if (import.HitDieSides <= 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "hit_die_sides must be >= 1.", "/hit_die_sides");
        if (import.GeneratedCoreSkillCount < 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "generated_core_skill_count must be >= 0.", "/generated_core_skill_count");
        AddDuplicateIds(diagnostics, context, import.SkillIds, "/skill_ids", "skill_id");
        AddDuplicateIds(diagnostics, context, import.DropEntries.Select(value => value.DropEntryId), "/drop_entries", "drop_entry_id");
        AppendTemplateRuleDiagnostics(diagnostics, context, import);
        return diagnostics;
    }

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateRoster(
        JsonContentEntryContext context,
        EncounterRosterJsonDto import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.ProfileId, "/profile_id");
        if (string.IsNullOrWhiteSpace(import.DisplayName))
            Add(diagnostics, context, EnemyContentImportRules.IdRequired, "Encounter roster must declare display_name.", "/display_name");
        if (import.InitialStage < 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "initial_stage must be >= 0.", "/initial_stage");
        if (import.GrowthStepInterval <= 0)
            Add(diagnostics, context, EnemyContentImportRules.ValueOutOfRange, "growth_step_interval must be >= 1.", "/growth_step_interval");
        if (import.Stages.Count == 0)
            Add(diagnostics, context, EnemyContentImportRules.CollectionRequired, "Encounter roster must declare at least one stage.", "/stages");
        AddDuplicateInts(diagnostics, context, import.Stages.Select(value => value.Stage), "/stages", "stage");
        AppendRosterRuleDiagnostics(diagnostics, context, import);
        return diagnostics;
    }

    private static void RequireIdentity(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string value, string pointer)
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(target, context, EnemyContentImportRules.IdRequired, "Content entry ID is required.", pointer);
        else if (!string.Equals(value, context.EntryId, StringComparison.Ordinal))
            Add(target, context, EnemyContentImportRules.IdMismatch, "Content entry ID must match the document envelope entry ID.", pointer);
    }

    private static void AddDuplicateIds(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, IEnumerable<string> values, string pointer, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value) && !seen.Add(value))
                Add(target, context, EnemyContentImportRules.DuplicateId, $"Duplicate {label} '{value}'.", pointer);
    }

    private static void AddDuplicateInts(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, IEnumerable<int> values, string pointer, string label)
    {
        var seen = new HashSet<int>();
        foreach (int value in values)
            if (!seen.Add(value))
                Add(target, context, EnemyContentImportRules.DuplicateId, $"Duplicate {label} '{value}'.", pointer);
    }

    private static void Add(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string ruleId, string message, string relativePointer) =>
        target.Add(new ContentJsonDiagnostic(ruleId, message, context.SourceLabel, $"{context.JsonPointer}{relativePointer}"));
}
