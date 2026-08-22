#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal sealed class ContingencyTemplateContentRegistry
{
    private readonly Dictionary<StringName, ContingencySetupTemplateDefinition> _templateDefs = new();
    private readonly List<string> _validationErrors = new();
    private readonly IContentJsonSourceReader _sourceReader;

    internal ContingencyTemplateContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal ContingencyTemplateContentRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    public void Rebuild() => LoadFromDirectory(ContingencyJsonContentDomain.DirectoryPath);

    internal void LoadFromDirectory(string directoryPath)
    {
        _templateDefs.Clear();
        _validationErrors.Clear();
        ContentImportBatch<ContingencyTemplateImportModel> batch = ContingencyJsonContentDomain
            .CreateDescriptor(directoryPath, _sourceReader)
            .Import();
        AppendDiagnostics(batch.Diagnostics);
        foreach (ContentImportEntry<ContingencyTemplateImportModel> entry in batch.Entries)
        {
            try
            {
                ContingencySetupTemplateDefinition definition =
                    ContingencySetupTemplateDefinition.FromImport(
                        entry.Import,
                        entry.Context.SourceLabel
                    );
                string smokeError = GetTemplateSmokeValidationError(definition);
                if (smokeError.Length > 0)
                {
                    _validationErrors.Add(
                        $"ContingencyTemplateContentRegistry: {entry.Context.SourceLabel} failed validation: {smokeError}"
                    );
                    continue;
                }
                if (!_templateDefs.TryAdd(definition.TemplateId, definition))
                {
                    _validationErrors.Add(
                        $"ContingencyTemplateContentRegistry: duplicate template_id '{definition.TemplateId}'."
                    );
                }
            }
            catch (InvalidDataException exception)
            {
                _validationErrors.Add(
                    $"ContingencyTemplateContentRegistry: {entry.Context.SourceLabel} projection failed: {exception.Message}"
                );
            }
        }
    }

    private static string GetTemplateSmokeValidationError(
        ContingencySetupTemplateDefinition templateDefinition
    )
    {
        IReadOnlyList<ContingencyTemplateStoredSpellInfo> storedSpells =
            ContingencyContentRules.GetTemplateStoredSpellsTyped(templateDefinition);
        if (storedSpells.Count == 0)
            return "stored_spells must contain at least one entry with a stored_skill_id.";

        var smokeCastLevels = new Dictionary<StringName, int>();
        foreach (ContingencyTemplateStoredSpellInfo spell in storedSpells)
            smokeCastLevels[spell.StoredSkillId] = 1;
        ContingencyMatrixSetupState setup = ContingencyContentRules.BuildSetupStateFromTemplate(
            templateDefinition,
            1,
            smokeCastLevels
        );
        return setup == null
            ? "stamped payload was rejected by ContingencyMatrixSetupState schema."
            : "";
    }

    internal bool TryGetDefinition(
        StringName templateId,
        out ContingencySetupTemplateDefinition? definition
    ) => _templateDefs.TryGetValue(templateId, out definition);

    internal IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> GetTemplateDefsTyped() =>
        new ReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition>(
            new Dictionary<StringName, ContingencySetupTemplateDefinition>(_templateDefs)
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
