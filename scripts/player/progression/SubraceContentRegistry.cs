using System.Collections.Generic;
using System.IO;
using Godot;

public class SubraceContentRegistry : IdentityContentRegistryBase
{
    private const string SUBRACE_CONFIG_DIRECTORY = "res://data/configs/subraces";

    private readonly Dictionary<StringName, SubraceDefinition> _subrace_defs = new();

    internal SubraceContentRegistry(IContentResourceLoader resourceLoader)
        : base(resourceLoader)
    {
        _registry_label = "SubraceContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(SUBRACE_CONFIG_DIRECTORY);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _subrace_defs.Clear();
        _validation_errors.Clear();

        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);

        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, SubraceDefinition> GetSubraceDefsTyped() =>
        _snapshot_definitions(_subrace_defs);

    protected override void ClearRegistryData()
    {
        _subrace_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load subrace config {resourcePath}.");
            return;
        }
        if (resource is not SubraceDef subraceDef)
        {
            _validation_errors.Add($"Subrace config {resourcePath} is not a SubraceDef.");
            return;
        }

        if (subraceDef.subrace_id == "")
        {
            _validation_errors.Add($"Subrace config {resourcePath} is missing subrace_id.");
            return;
        }

        if (_subrace_defs.ContainsKey(subraceDef.subrace_id))
        {
            _validation_errors.Add($"Duplicate subrace_id registered: {subraceDef.subrace_id}");
            return;
        }

        try
        {
            SubraceDefinition definition = SubraceDefinition.FromResource(
                subraceDef,
                $"subrace.{subraceDef.subrace_id}"
            );
            _subrace_defs.Add(definition.SubraceId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Subrace config {resourcePath} projection failed: {exception.Message}"
            );
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

            _append_string_field_error(errors, label, "display_name", subraceDef.DisplayName);

            _append_string_field_error(errors, label, "description", subraceDef.Description);

            _append_string_name_field_error(
                errors,
                label,
                "body_size_category_override",
                subraceDef.BodySizeCategoryOverride,
                true
            );

            _append_int_field_error(errors, label, "speed_bonus", subraceDef.SpeedBonus);

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

            _append_string_name_array_errors(
                errors,
                label,
                subraceDef.SaveAdvantageTags,
                "save_advantage_tags"
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
