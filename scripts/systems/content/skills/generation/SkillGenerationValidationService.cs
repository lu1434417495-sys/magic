#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class SkillGenerationValidationService
{
    private readonly SkillImportModelValidator _domainValidator = new();
    private readonly ContentSnapshot _processSnapshot;
    private readonly ISkillGenerationBattleSimulationGate _battleSimulationGate;

    internal SkillGenerationValidationService(
        ContentSnapshot processSnapshot,
        ISkillGenerationBattleSimulationGate battleSimulationGate
    )
    {
        _processSnapshot = processSnapshot
            ?? throw new ArgumentNullException(nameof(processSnapshot));
        _battleSimulationGate = battleSimulationGate
            ?? throw new ArgumentNullException(nameof(battleSimulationGate));
    }

    internal SkillGenerationValidationReport Validate(
        string sourceDirectory,
        IContentJsonSourceReader sourceReader
    )
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new ArgumentException("Source directory is required.", nameof(sourceDirectory));
        ArgumentNullException.ThrowIfNull(sourceReader);

        IReadOnlyList<ContentJsonSourceText> sources =
            sourceReader.ReadUtf8Documents(sourceDirectory)
            ?? throw new InvalidOperationException(
                "Generated skill source reader returned a null document collection."
            );
        var bufferedReader = new SkillGenerationBufferedSourceReader(sources);

        var reports = new List<SkillGenerationValidationStageReport>();
        ContentImportBatch<SkillImportModel> schemaBatch =
            SkillContentJsonAuthoringDomain
                .CreateSchemaImportDescriptor(sourceDirectory, bufferedReader)
                .Import();
        reports.Add(new SkillGenerationValidationStageReport(
            SkillGenerationValidationStageKind.Schema,
            schemaBatch.Entries.Count,
            SkillGenerationDiagnosticEnricher.Enrich(
                SkillGenerationValidationStageKind.Schema,
                schemaBatch.Diagnostics,
                sources
            )
        ));
        if (schemaBatch.HasErrors)
            return new SkillGenerationValidationReport(reports);

        var domainDiagnostics = new List<ContentJsonDiagnostic>();
        foreach (ContentImportEntry<SkillImportModel> entry in schemaBatch.Entries)
        {
            domainDiagnostics.AddRange(
                _domainValidator.ValidateDomainLocal(entry.Context, entry.Import)
            );
        }
        reports.Add(new SkillGenerationValidationStageReport(
            SkillGenerationValidationStageKind.Domain,
            domainDiagnostics.Count == 0 ? schemaBatch.Entries.Count : 0,
            SkillGenerationDiagnosticEnricher.Enrich(
                SkillGenerationValidationStageKind.Domain,
                domainDiagnostics,
                sources
            )
        ));
        if (domainDiagnostics.Count > 0)
            return new SkillGenerationValidationReport(reports);

        IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills =
            ProjectCandidateSkills(schemaBatch.Entries);
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills =
            BuildCombinedSkillCatalog(candidateSkills);
        IReadOnlyList<ContentJsonDiagnostic> crossDomainDiagnostics =
            SkillGenerationCrossDomainValidator.Validate(
                schemaBatch.Entries,
                candidateSkills,
                combinedSkills,
                _processSnapshot
            );
        reports.Add(new SkillGenerationValidationStageReport(
            SkillGenerationValidationStageKind.CrossDomain,
            crossDomainDiagnostics.Count == 0 ? candidateSkills.Count : 0,
            SkillGenerationDiagnosticEnricher.Enrich(
                SkillGenerationValidationStageKind.CrossDomain,
                crossDomainDiagnostics,
                sources
            )
        ));
        if (crossDomainDiagnostics.Count > 0)
            return new SkillGenerationValidationReport(reports);

        SkillGenerationBattleSimulationGateResult simulation =
            _battleSimulationGate.Evaluate(
                candidateSkills,
                BuildCandidateContexts(schemaBatch.Entries),
                combinedSkills,
                _processSnapshot
            ) ?? throw new InvalidOperationException(
                "Battle simulation gate returned a null result."
            );
        reports.Add(new SkillGenerationValidationStageReport(
            SkillGenerationValidationStageKind.BattleSimulation,
            simulation.Diagnostics.Count == 0 ? simulation.SampledSkillCount : 0,
            SkillGenerationDiagnosticEnricher.Enrich(
                SkillGenerationValidationStageKind.BattleSimulation,
                simulation.Diagnostics,
                sources
            ),
            simulation.Metrics
        ));
        return new SkillGenerationValidationReport(reports);
    }

    private static IReadOnlyDictionary<StringName, SkillDefinition> ProjectCandidateSkills(
        IReadOnlyList<ContentImportEntry<SkillImportModel>> entries
    )
    {
        var projected = new Dictionary<StringName, SkillDefinition>();
        foreach (ContentImportEntry<SkillImportModel> entry in entries)
        {
            var skillId = new StringName(entry.Import.SkillId.Value);
            projected.Add(skillId, SkillDefinitionProjector.Project(entry.Import));
        }
        return new ReadOnlyDictionary<StringName, SkillDefinition>(projected);
    }

    private static IReadOnlyDictionary<StringName, JsonContentEntryContext>
        BuildCandidateContexts(
            IReadOnlyList<ContentImportEntry<SkillImportModel>> entries
        )
    {
        var contexts = new Dictionary<StringName, JsonContentEntryContext>();
        foreach (ContentImportEntry<SkillImportModel> entry in entries)
            contexts.Add(new StringName(entry.Import.SkillId.Value), entry.Context);
        return new ReadOnlyDictionary<StringName, JsonContentEntryContext>(contexts);
    }

    private IReadOnlyDictionary<StringName, SkillDefinition> BuildCombinedSkillCatalog(
        IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills
    )
    {
        var combined = new Dictionary<StringName, SkillDefinition>();
        foreach ((StringName skillId, SkillDefinition definition) in _processSnapshot.Skills)
            combined.Add(skillId, definition);
        foreach ((StringName skillId, SkillDefinition definition) in candidateSkills)
        {
            if (!combined.ContainsKey(skillId))
                combined.Add(skillId, definition);
        }
        return new ReadOnlyDictionary<StringName, SkillDefinition>(combined);
    }
}
