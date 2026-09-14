#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal sealed class QuestContentRegistry
{
    private readonly Dictionary<StringName, QuestDefinition> _questDefs = new();
    private readonly List<string> _validationErrors = new();
    private readonly IContentJsonSourceReader _sourceReader;

    internal QuestContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal QuestContentRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    public void Rebuild() => LoadFromDirectory(QuestJsonContentDomain.DirectoryPath);

    internal void LoadFromDirectory(string directoryPath)
    {
        _questDefs.Clear();
        _validationErrors.Clear();

        ContentImportBatch<QuestImportModel> batch = QuestJsonContentDomain
            .CreateDescriptor(directoryPath, _sourceReader)
            .Import();
        AppendDiagnostics(batch.Diagnostics);
        foreach (ContentImportEntry<QuestImportModel> entry in batch.Entries)
        {
            try
            {
                QuestDefinition definition = QuestDefinition.FromImport(
                    entry.Import,
                    entry.Context.SourceLabel
                );
                if (!_questDefs.TryAdd(definition.QuestId, definition))
                {
                    _validationErrors.Add(
                        $"QuestContentRegistry: duplicate quest_id '{definition.QuestId}'."
                    );
                }
            }
            catch (InvalidDataException exception)
            {
                _validationErrors.Add(
                    $"QuestContentRegistry: {entry.Context.SourceLabel} projection failed: {exception.Message}"
                );
            }
        }
    }

    internal bool TryGetDefinition(StringName questId, out QuestDefinition? definition) =>
        _questDefs.TryGetValue(questId, out definition);

    internal IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped() =>
        new ReadOnlyDictionary<StringName, QuestDefinition>(
            new Dictionary<StringName, QuestDefinition>(_questDefs)
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;

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
