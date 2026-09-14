#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleSimContentCatalog
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, BattleSimScenarioDefinition> _scenarios = new();
    private readonly List<string> _validationErrors = new();

    internal BattleSimContentCatalog()
        : this(new GodotContentJsonSourceReader()) { }

    internal BattleSimContentCatalog(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    internal void Rebuild()
    {
        _validationErrors.Clear();
        LoadScenarios(BattleSimJsonContentDomains.ScenarioDirectory);
    }

    internal bool TryGetScenario(
        StringName scenarioId,
        out BattleSimScenarioDefinition? definition
    ) => _scenarios.TryGetValue(scenarioId, out definition);

    internal IReadOnlyDictionary<StringName, BattleSimScenarioDefinition> GetScenarios() =>
        new ReadOnlyDictionary<StringName, BattleSimScenarioDefinition>(
            new Dictionary<StringName, BattleSimScenarioDefinition>(_scenarios)
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors.AsReadOnly();

    internal void LoadScenarios(string directory)
    {
        _scenarios.Clear();
        ContentImportBatch<BattleSimScenarioImportModel> batch = BattleSimJsonContentDomains
            .CreateScenarioDescriptor(directory, _sourceReader)
            .Import();
        AppendDiagnostics(batch.Diagnostics);
        foreach (ContentImportEntry<BattleSimScenarioImportModel> entry in batch.Entries)
        {
            BattleSimScenarioDefinition definition = BattleSimContentDefinitionProjector.ProjectScenario(
                entry.Import,
                entry.Context.SourceLabel
            );
            if (!_scenarios.TryAdd(definition.ScenarioId, definition))
                _validationErrors.Add($"Duplicate BattleSim scenario_id '{definition.ScenarioId}'.");
        }
    }

    private void AppendDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics)
    {
        foreach (ContentJsonDiagnostic diagnostic in diagnostics)
        {
            _validationErrors.Add(
                $"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
    }
}
