using System.Collections.Generic;
using Godot;

public class SubraceContentRegistry : IdentityContentRegistryBase
{
    private const string SubraceJsonDirectory = ProfessionIdentityJsonDomains.SubraceDirectory;

    private readonly Dictionary<StringName, SubraceDefinition> _subrace_defs = new();
    private readonly IContentJsonSourceReader _jsonSourceReader;

    internal SubraceContentRegistry(bool loadDefaultContent = true)
        : this(new GodotContentJsonSourceReader(), loadDefaultContent) { }

    internal SubraceContentRegistry(IContentJsonSourceReader jsonSourceReader, bool loadDefaultContent = true)
        : base()
    {
        _jsonSourceReader = jsonSourceReader
            ?? throw new System.ArgumentNullException(nameof(jsonSourceReader));
        _registry_label = "SubraceContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(SubraceJsonDirectory);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _subrace_defs.Clear();
        _validation_errors.Clear();

        foreach (var directoryPath in directoryPaths)
            ImportDirectory(directoryPath);

        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, SubraceDefinition> GetSubraceDefsTyped() =>
        _snapshot_definitions(_subrace_defs);

    protected override void ClearRegistryData()
    {
        _subrace_defs.Clear();
    }

    private void ImportDirectory(string directoryPath)
    {
        ContentImportBatch<SubraceImportModel> batch = ProfessionIdentityJsonImport
            .CreateSubraceDescriptor(directoryPath, _jsonSourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validation_errors.Add(ProfessionIdentityJsonImport.FormatDiagnostic(diagnostic));
        foreach (ContentImportEntry<SubraceImportModel> entry in batch.Entries)
        {
            try
            {
                SubraceDefinition definition = ProfessionIdentityDefinitionProjector.Project(entry.Import);
                if (!_subrace_defs.TryAdd(definition.SubraceId, definition))
                    _validation_errors.Add($"Duplicate subrace_id registered: {definition.SubraceId}");
            }
            catch (System.Exception exception)
                when (exception is System.IO.InvalidDataException
                    or System.InvalidOperationException)
            {
                _validation_errors.Add(
                    $"Subrace JSON {entry.Context.SourceLabel} projection failed: {exception.GetType().Name}: {exception.Message}"
                );
            }
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();

        foreach (var subraceKey in _sorted_registry_keys(_subrace_defs.Keys))
        {
            var subraceId = new StringName(subraceKey);
            SubraceDefinition subraceDef = _subrace_defs[subraceId];

            var label = $"Subrace {subraceId}";

            _append_string_name_field_error(errors, label, "subrace_id", subraceDef.SubraceId);

            _append_string_name_field_error(
                errors,
                label,
                "parent_race_id",
                subraceDef.ParentRaceId
            );

            _append_required_string_field_error(
                errors,
                label,
                "display_name",
                subraceDef.DisplayName
            );

            _append_required_string_field_error(
                errors,
                label,
                "description",
                subraceDef.Description
            );

            _append_string_name_field_error(
                errors,
                label,
                "body_size_category_override",
                subraceDef.BodySizeCategoryOverride,
                true
            );

            _append_attribute_modifier_array_errors(
                errors,
                label,
                subraceDef.AttributeModifiers,
                "attribute_modifiers"
            );

            _append_string_name_array_errors(errors, label, subraceDef.TraitIds, "trait_ids");

            _append_racial_granted_skill_array_errors(
                errors,
                label,
                subraceDef.RacialGrantedSkills,
                "racial_granted_skills"
            );

            _append_string_name_array_errors(
                errors,
                label,
                subraceDef.ProficiencyTags,
                "proficiency_tags"
            );

            _append_string_name_array_errors(
                errors,
                label,
                subraceDef.VisionTags,
                "vision_tags"
            );

            SaveTagListContentRules.AppendValidationErrors(
                errors,
                $"{label}.save_advantage_tags",
                subraceDef.SaveAdvantageTags
            );

            SaveTagListContentRules.AppendValidationErrors(
                errors,
                $"{label}.save_disadvantage_tags",
                subraceDef.SaveDisadvantageTags
            );

            SaveTagListContentRules.AppendValidationErrors(
                errors,
                $"{label}.save_immunity_tags",
                subraceDef.SaveImmunityTags
            );

            _append_string_name_to_string_name_dictionary_errors(
                errors,
                label,
                subraceDef.DamageResistances,
                "damage_resistances"
            );

            _append_string_name_array_errors(
                errors,
                label,
                subraceDef.DialogueTags,
                "dialogue_tags"
            );

            _append_string_array_errors(
                errors,
                label,
                subraceDef.RacialTraitSummary,
                "racial_trait_summary"
            );
        }

        return errors;
    }

}
