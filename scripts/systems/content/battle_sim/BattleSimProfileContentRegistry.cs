#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleSimProfileContentRegistry
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, BattleSimProfileDefinition> _definitions = new();
    private readonly List<string> _validationErrors = new();

    internal BattleSimProfileContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal BattleSimProfileContentRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    internal void Rebuild() => LoadFromDirectory(BattleSimJsonContentDomains.ProfileDirectory);

    internal void LoadFromDirectory(string directory)
    {
        _definitions.Clear();
        _validationErrors.Clear();
        ContentImportBatch<BattleSimProfileImportModel> batch = BattleSimJsonContentDomains
            .CreateProfileDescriptor(directory, _sourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validationErrors.Add($"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}");
        foreach (ContentImportEntry<BattleSimProfileImportModel> entry in batch.Entries)
        {
            BattleSimProfileDefinition definition = BattleSimProfileDefinitionProjector.Project(entry.Import);
            if (!_definitions.TryAdd(definition.ProfileId, definition))
                _validationErrors.Add($"Duplicate BattleSim profile_id '{definition.ProfileId}'.");
        }
    }

    internal bool TryGetDefinition(StringName profileId, out BattleSimProfileDefinition? definition) =>
        _definitions.TryGetValue(profileId, out definition);

    internal IReadOnlyDictionary<StringName, BattleSimProfileDefinition> GetDefinitions() =>
        new ReadOnlyDictionary<StringName, BattleSimProfileDefinition>(
            new Dictionary<StringName, BattleSimProfileDefinition>(_definitions)
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors.AsReadOnly();
}
