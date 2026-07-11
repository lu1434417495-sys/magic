using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

internal sealed class ContingencyTemplateContentRegistry
{
    private const string TemplateConfigDirectory = "res://data/configs/contingency_templates";

    private readonly Dictionary<StringName, ContingencySetupTemplateDefinition> _templateDefs =
        new();
    private readonly List<string> _validationErrors = new();

    public void Rebuild()
    {
        LoadFromDirectory(TemplateConfigDirectory);
    }

    internal void LoadFromDirectory(string directoryPath)
    {
        _templateDefs.Clear();
        _validationErrors.Clear();

        string globalPath = ProjectSettings.GlobalizePath(directoryPath);
        if (!DirAccess.DirExistsAbsolute(globalPath))
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry could not find {directoryPath}."
            );
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry could not open {directoryPath}."
            );
            return;
        }

        try
        {
            string[] files = directory.GetFiles();
            foreach (string fileName in files)
            {
                if (!fileName.EndsWith(".tres"))
                    continue;

                string resourcePath = $"{directoryPath}/{fileName}";
                RegisterTemplateResource(resourcePath);
            }
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterTemplateResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry failed to load {resourcePath}."
            );
            return;
        }

        if (resource is not ContingencySetupTemplateDef templateDef)
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry: {resourcePath} is not a ContingencySetupTemplateDef."
            );
            return;
        }

        GodotContentOwnership.RegisterBorrowedContent(templateDef, resourcePath);

        StringName templateId = templateDef.template_id;
        if (templateId == "")
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry: {resourcePath} is missing template_id."
            );
            return;
        }

        if (_templateDefs.ContainsKey(templateId))
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry: duplicate template_id '{templateId}' ({resourcePath})."
            );
            return;
        }

        try
        {
            ContingencySetupTemplateDefinition definition =
                ContingencySetupTemplateDefinition.FromResource(templateDef, resourcePath);
            string smokeError = GetTemplateSmokeValidationError(definition);
            if (smokeError.Length > 0)
            {
                _validationErrors.Add(
                    $"ContingencyTemplateContentRegistry: {resourcePath} failed validation: {smokeError}"
                );
                return;
            }
            _templateDefs.Add(definition.TemplateId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry: {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    // Stamp the template with level-1 dynamic fields and run it through the schema
    // authority, so authoring mistakes surface at content load instead of at the
    // first player click.
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
        if (setup == null)
            return "stamped payload was rejected by ContingencyMatrixSetupState schema.";
        return "";
    }

    internal IReadOnlyDictionary<
        StringName,
        ContingencySetupTemplateDefinition
    > GetTemplateDefsTyped() =>
        new ReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition>(
            new Dictionary<StringName, ContingencySetupTemplateDefinition>(_templateDefs)
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;
}
