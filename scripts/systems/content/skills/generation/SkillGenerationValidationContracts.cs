#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal enum SkillGenerationValidationStageKind
{
    Schema = 0,
    Domain = 1,
    CrossDomain = 2,
    BattleSimulation = 3,
}

internal static class SkillGenerationValidationStageCodec
{
    internal static string ToWireValue(SkillGenerationValidationStageKind stage) =>
        stage switch
        {
            SkillGenerationValidationStageKind.Schema => "schema",
            SkillGenerationValidationStageKind.Domain => "domain",
            SkillGenerationValidationStageKind.CrossDomain => "cross_domain",
            SkillGenerationValidationStageKind.BattleSimulation => "battle_simulation",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
        };
}

internal static class SkillGenerationValidationExitCodes
{
    internal const int Success = 0;
    internal const int SchemaRejected = 10;
    internal const int DomainRejected = 11;
    internal const int CrossDomainRejected = 12;
    internal const int BattleSimulationRejected = 13;
    internal const int HostFailure = 20;

    internal static int ForRejectedStage(SkillGenerationValidationStageKind stage) =>
        stage switch
        {
            SkillGenerationValidationStageKind.Schema => SchemaRejected,
            SkillGenerationValidationStageKind.Domain => DomainRejected,
            SkillGenerationValidationStageKind.CrossDomain => CrossDomainRejected,
            SkillGenerationValidationStageKind.BattleSimulation =>
                BattleSimulationRejected,
            _ => HostFailure,
        };
}

internal sealed class SkillGenerationValidationStageReport
{
    internal SkillGenerationValidationStageReport(
        SkillGenerationValidationStageKind stage,
        int validatedEntryCount,
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        IReadOnlyDictionary<string, object>? metrics = null
    )
    {
        if (validatedEntryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(validatedEntryCount));
        ArgumentNullException.ThrowIfNull(diagnostics);

        Stage = stage;
        ValidatedEntryCount = validatedEntryCount;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            diagnostics
                .Select(value =>
                    value
                    ?? throw new ArgumentException(
                        "Validation diagnostics cannot contain null.",
                        nameof(diagnostics)
                    )
                )
                .OrderBy(value => value.SourceLabel, StringComparer.Ordinal)
                .ThenBy(value => value.JsonPointer, StringComparer.Ordinal)
                .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                .ThenBy(value => value.Message, StringComparer.Ordinal)
                .ToList()
        );
        Metrics = new ReadOnlyDictionary<string, object>(
            CopyMetrics(metrics)
        );
    }

    internal SkillGenerationValidationStageKind Stage { get; }
    internal int ValidatedEntryCount { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal IReadOnlyDictionary<string, object> Metrics { get; }
    internal bool Success => Diagnostics.Count == 0;

    private static Dictionary<string, object> CopyMetrics(
        IReadOnlyDictionary<string, object>? source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach ((string key, object value) in source)
            result.Add(key, value);
        return result;
    }
}

internal sealed class SkillGenerationValidationReport
{
    internal SkillGenerationValidationReport(
        IEnumerable<SkillGenerationValidationStageReport> stages
    )
    {
        ArgumentNullException.ThrowIfNull(stages);
        var copy = new List<SkillGenerationValidationStageReport>();
        int expectedOrdinal = 0;
        bool rejected = false;
        foreach (SkillGenerationValidationStageReport stage in stages)
        {
            ArgumentNullException.ThrowIfNull(stage);
            if (rejected)
            {
                throw new ArgumentException(
                    "Validation stages cannot continue after a rejection.",
                    nameof(stages)
                );
            }
            if ((int)stage.Stage != expectedOrdinal)
            {
                throw new ArgumentException(
                    "Validation stages must be contiguous and ordered.",
                    nameof(stages)
                );
            }
            copy.Add(stage);
            rejected = !stage.Success;
            expectedOrdinal += 1;
        }

        Stages = new ReadOnlyCollection<SkillGenerationValidationStageReport>(copy);
        RejectedStage = copy.LastOrDefault(value => !value.Success)?.Stage;
        Success = copy.Count == 4 && RejectedStage == null;
        ExitCode = RejectedStage.HasValue
            ? SkillGenerationValidationExitCodes.ForRejectedStage(RejectedStage.Value)
            : Success
                ? SkillGenerationValidationExitCodes.Success
                : SkillGenerationValidationExitCodes.HostFailure;
    }

    internal IReadOnlyList<SkillGenerationValidationStageReport> Stages { get; }
    internal SkillGenerationValidationStageKind? RejectedStage { get; }
    internal bool Success { get; }
    internal int ExitCode { get; }
}

internal sealed class SkillGenerationBattleSimulationGateResult
{
    internal SkillGenerationBattleSimulationGateResult(
        int sampledSkillCount,
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        IReadOnlyDictionary<string, object>? metrics = null
    )
    {
        if (sampledSkillCount < 0)
            throw new ArgumentOutOfRangeException(nameof(sampledSkillCount));
        ArgumentNullException.ThrowIfNull(diagnostics);
        SampledSkillCount = sampledSkillCount;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            new List<ContentJsonDiagnostic>(diagnostics)
        );
        Metrics = new ReadOnlyDictionary<string, object>(
            CopyMetrics(metrics)
        );
    }

    internal int SampledSkillCount { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal IReadOnlyDictionary<string, object> Metrics { get; }

    private static Dictionary<string, object> CopyMetrics(
        IReadOnlyDictionary<string, object>? source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach ((string key, object value) in source)
            result.Add(key, value);
        return result;
    }
}

internal interface ISkillGenerationBattleSimulationGate
{
    SkillGenerationBattleSimulationGateResult Evaluate(
        IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills,
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
        ContentSnapshot processSnapshot
    );
}
