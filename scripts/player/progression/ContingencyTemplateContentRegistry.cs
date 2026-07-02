using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class ContingencyTemplateContentRegistry
{
    private const string TemplateConfigDirectory = "res://data/configs/contingency_templates";

    private readonly Dictionary<StringName, ContingencySetupTemplateDef> _templateDefs = new();
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

        string smokeError = GetTemplateSmokeValidationError(templateDef);
        if (smokeError.Length > 0)
        {
            _validationErrors.Add(
                $"ContingencyTemplateContentRegistry: {resourcePath} failed validation: {smokeError}"
            );
            return;
        }

        _templateDefs[templateId] = templateDef;
    }

    // Stamp the template with level-1 dynamic fields and run it through the schema
    // authority, so authoring mistakes surface at content load instead of at the
    // first player click.
    private static string GetTemplateSmokeValidationError(ContingencySetupTemplateDef templateDef)
    {
        IReadOnlyList<ContingencyTemplateStoredSpellInfo> storedSpells =
            ContingencyContentRules.GetTemplateStoredSpellsTyped(templateDef);
        if (storedSpells.Count == 0)
            return "stored_spells must contain at least one entry with a stored_skill_id.";

        var smokeCastLevels = new Dictionary<StringName, int>();
        foreach (ContingencyTemplateStoredSpellInfo spell in storedSpells)
            smokeCastLevels[spell.StoredSkillId] = 1;

        GDictionary payload = ContingencyContentRules.BuildSetupPayloadFromTemplate(
            templateDef,
            1,
            smokeCastLevels
        );
        if (payload == null)
            return "template payload could not be built.";
        if (ContingencyMatrixSetupState.FromDictionary(payload) == null)
            return "stamped payload was rejected by ContingencyMatrixSetupState schema.";
        return "";
    }

    internal IReadOnlyDictionary<StringName, ContingencySetupTemplateDef> GetTemplateDefsTyped() =>
        _templateDefs;

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;
}
