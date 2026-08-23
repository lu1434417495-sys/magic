#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal enum EquipmentClosureGenerationValidationStageKind
{
    Schema = 0,
    Domain = 1,
    CrossDomain = 2,
    BattleSimulation = 3,
}

internal static class EquipmentClosureGenerationValidationStageCodec
{
    internal static string ToWireValue(
        EquipmentClosureGenerationValidationStageKind stage
    ) => stage switch
    {
        EquipmentClosureGenerationValidationStageKind.Schema => "schema",
        EquipmentClosureGenerationValidationStageKind.Domain => "domain",
        EquipmentClosureGenerationValidationStageKind.CrossDomain => "cross_domain",
        EquipmentClosureGenerationValidationStageKind.BattleSimulation =>
            "battle_simulation",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };
}

internal static class EquipmentClosureGenerationValidationExitCodes
{
    internal const int Success = 0;
    internal const int SchemaRejected = 30;
    internal const int DomainRejected = 31;
    internal const int CrossDomainRejected = 32;
    internal const int BattleSimulationRejected = 33;
    internal const int HostFailure = 40;

    internal static int ForRejectedStage(
        EquipmentClosureGenerationValidationStageKind stage
    ) => stage switch
    {
        EquipmentClosureGenerationValidationStageKind.Schema => SchemaRejected,
        EquipmentClosureGenerationValidationStageKind.Domain => DomainRejected,
        EquipmentClosureGenerationValidationStageKind.CrossDomain =>
            CrossDomainRejected,
        EquipmentClosureGenerationValidationStageKind.BattleSimulation =>
            BattleSimulationRejected,
        _ => HostFailure,
    };
}

internal sealed record EquipmentClosureGenerationSourceSet(
    string ItemsDirectory,
    string TraitsDirectory,
    string EquipmentAbilitiesDirectory,
    string GearSetsDirectory,
    string RecipesDirectory
)
{
    internal static EquipmentClosureGenerationSourceSet Production { get; } = new(
        ItemContentJsonAuthoringDomain.ProductionDirectory,
        TraitContentJsonAuthoringDomain.ProductionDirectory,
        EquipmentAbilityContentJsonAuthoringDomain.ProductionDirectory,
        GearSetContentJsonAuthoringDomain.ProductionDirectory,
        RecipeContentJsonAuthoringDomain.ProductionDirectory
    );

    internal void Validate()
    {
        Require(ItemsDirectory, nameof(ItemsDirectory));
        Require(TraitsDirectory, nameof(TraitsDirectory));
        Require(EquipmentAbilitiesDirectory, nameof(EquipmentAbilitiesDirectory));
        Require(GearSetsDirectory, nameof(GearSetsDirectory));
        Require(RecipesDirectory, nameof(RecipesDirectory));
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Equipment closure source directories are required.", name);
    }
}

internal sealed class EquipmentClosureGenerationValidationStageReport
{
    internal EquipmentClosureGenerationValidationStageReport(
        EquipmentClosureGenerationValidationStageKind stage,
        int validatedEntryCount,
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        IReadOnlyDictionary<string, object>? metrics = null
    )
    {
        if (validatedEntryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(validatedEntryCount));
        ArgumentNullException.ThrowIfNull(diagnostics);
        Stage = stage;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            diagnostics
                .Select(value => value ?? throw new ArgumentException(
                    "Validation diagnostics cannot contain null.",
                    nameof(diagnostics)
                ))
                .OrderBy(value => value.SourceLabel, StringComparer.Ordinal)
                .ThenBy(value => value.JsonPointer, StringComparer.Ordinal)
                .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                .ThenBy(value => value.Message, StringComparer.Ordinal)
                .ToList()
        );
        ValidatedEntryCount = Diagnostics.Count == 0 ? validatedEntryCount : 0;
        Metrics = new ReadOnlyDictionary<string, object>(
            metrics == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(metrics, StringComparer.Ordinal)
        );
    }

    internal EquipmentClosureGenerationValidationStageKind Stage { get; }
    internal int ValidatedEntryCount { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal IReadOnlyDictionary<string, object> Metrics { get; }
    internal bool Success => Diagnostics.Count == 0;
}

internal sealed class EquipmentClosureGenerationValidationReport
{
    internal const string ProtocolId = "magic.equipment_closure.validation/v1";

    internal EquipmentClosureGenerationValidationReport(
        IEnumerable<EquipmentClosureGenerationValidationStageReport> stages
    )
    {
        ArgumentNullException.ThrowIfNull(stages);
        var copy = new List<EquipmentClosureGenerationValidationStageReport>();
        int expectedOrdinal = 0;
        bool rejected = false;
        foreach (EquipmentClosureGenerationValidationStageReport stage in stages)
        {
            ArgumentNullException.ThrowIfNull(stage);
            if (rejected)
                throw new ArgumentException(
                    "Validation stages cannot continue after a rejection.",
                    nameof(stages)
                );
            if ((int)stage.Stage != expectedOrdinal)
                throw new ArgumentException(
                    "Validation stages must be contiguous and ordered.",
                    nameof(stages)
                );
            copy.Add(stage);
            rejected = !stage.Success;
            expectedOrdinal += 1;
        }

        Stages = new ReadOnlyCollection<EquipmentClosureGenerationValidationStageReport>(copy);
        RejectedStage = copy.LastOrDefault(value => !value.Success)?.Stage;
        Success = copy.Count == 4 && RejectedStage == null;
        ExitCode = RejectedStage.HasValue
            ? EquipmentClosureGenerationValidationExitCodes.ForRejectedStage(
                RejectedStage.Value
            )
            : Success
                ? EquipmentClosureGenerationValidationExitCodes.Success
                : EquipmentClosureGenerationValidationExitCodes.HostFailure;
    }

    internal IReadOnlyList<EquipmentClosureGenerationValidationStageReport> Stages { get; }
    internal EquipmentClosureGenerationValidationStageKind? RejectedStage { get; }
    internal bool Success { get; }
    internal int ExitCode { get; }
}

internal sealed class EquipmentClosureGenerationProjectedContent
{
    internal required IReadOnlyDictionary<StringName, ItemDefinition> CandidateItems { get; init; }
    internal required IReadOnlyDictionary<StringName, TraitDefinition> CandidateTraits { get; init; }
    internal required IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition>
        CandidateEquipmentAbilityPacks { get; init; }
    internal required IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        CandidateEquipmentAbilityBindings { get; init; }
    internal required IReadOnlyDictionary<StringName, GearSetDefinition> CandidateGearSets { get; init; }
    internal required IReadOnlyDictionary<StringName, RecipeDefinition> CandidateRecipes { get; init; }

    internal required IReadOnlyDictionary<StringName, ItemDefinition> CombinedItems { get; init; }
    internal required IReadOnlyDictionary<StringName, TraitDefinition> CombinedTraits { get; init; }
    internal required IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition>
        CombinedEquipmentAbilityPacks { get; init; }
    internal required IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition>
        CombinedEquipmentAbilityBindings { get; init; }
    internal required IReadOnlyDictionary<StringName, GearSetDefinition> CombinedGearSets { get; init; }
    internal required IReadOnlyDictionary<StringName, RecipeDefinition> CombinedRecipes { get; init; }

    internal required IReadOnlyDictionary<StringName, JsonContentEntryContext> ItemContexts { get; init; }
    internal required IReadOnlyDictionary<StringName, JsonContentEntryContext> TraitContexts { get; init; }
    internal required IReadOnlyDictionary<StringName, JsonContentEntryContext>
        EquipmentAbilityPackContexts { get; init; }
    internal required IReadOnlyDictionary<StringName, JsonContentEntryContext> GearSetContexts { get; init; }
    internal required IReadOnlyDictionary<StringName, JsonContentEntryContext> RecipeContexts { get; init; }
}

internal sealed class EquipmentClosureGenerationBattleSimulationGateResult
{
    internal EquipmentClosureGenerationBattleSimulationGateResult(
        int sampledItemCount,
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        IReadOnlyDictionary<string, object>? metrics = null
    )
    {
        if (sampledItemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampledItemCount));
        ArgumentNullException.ThrowIfNull(diagnostics);
        SampledItemCount = sampledItemCount;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            new List<ContentJsonDiagnostic>(diagnostics)
        );
        Metrics = new ReadOnlyDictionary<string, object>(
            metrics == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(metrics, StringComparer.Ordinal)
        );
    }

    internal int SampledItemCount { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal IReadOnlyDictionary<string, object> Metrics { get; }
}

internal interface IEquipmentClosureGenerationBattleSimulationGate
{
    EquipmentClosureGenerationBattleSimulationGateResult Evaluate(
        EquipmentClosureGenerationProjectedContent content,
        ContentSnapshot processSnapshot
    );
}
