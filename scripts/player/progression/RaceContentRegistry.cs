using System.Collections.Generic;
using System.IO;
using Godot;

public class RaceContentRegistry : IdentityContentRegistryBase
{
    private const string RACE_CONFIG_DIRECTORY = "res://data/configs/races";

    private readonly Dictionary<StringName, RaceDefinition> _race_defs = new();

    public RaceContentRegistry()
    {
        _registry_label = "RaceContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(RACE_CONFIG_DIRECTORY);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _race_defs.Clear();
        _validation_errors.Clear();

        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);

        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, RaceDefinition> GetRaceDefsTyped() =>
        _snapshot_definitions(_race_defs);

    protected override void ClearRegistryData()
    {
        _race_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load race config {resourcePath}.");
            return;
        }
        GodotContentOwnership.RegisterBorrowedContent(resource, resourcePath);
        if (resource is not RaceDef raceDef)
        {
            _validation_errors.Add($"Race config {resourcePath} is not a RaceDef.");
            return;
        }

        if (raceDef.race_id == "")
        {
            _validation_errors.Add($"Race config {resourcePath} is missing race_id.");
            return;
        }

        if (_race_defs.ContainsKey(raceDef.race_id))
        {
            _validation_errors.Add($"Duplicate race_id registered: {raceDef.race_id}");
            return;
        }

        try
        {
            RaceDefinition definition = RaceDefinition.FromResource(
                raceDef,
                $"race.{raceDef.race_id}"
            );
            _race_defs.Add(definition.RaceId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Race config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();

        foreach (var raceKey in _sorted_registry_keys(_race_defs.Keys))
        {
            var raceId = new StringName(raceKey);
            RaceDefinition raceDef = _race_defs[raceId];

            var label = $"Race {raceId}";

            _append_string_name_field_error(errors, label, "race_id", raceDef.RaceId);

            _append_string_field_error(errors, label, "display_name", raceDef.DisplayName);

            _append_string_field_error(errors, label, "description", raceDef.Description);

            _append_string_name_field_error(
                errors,
                label,
                "age_profile_id",
                raceDef.AgeProfileId
            );

            _append_string_name_field_error(
                errors,
                label,
                "default_subrace_id",
                raceDef.DefaultSubraceId
            );

            _append_string_name_array_errors(errors, label, raceDef.SubraceIds, "subrace_ids");

            _append_string_name_field_error(
                errors,
                label,
                "body_size_category",
                raceDef.BodySizeCategory
            );

            _append_int_field_error(errors, label, "base_speed", raceDef.BaseSpeed);

            if (raceDef.BaseSpeed <= 0)
                errors.Add($"{label}.base_speed must be > 0.");

            _append_attribute_modifier_array_errors(
                errors,
                label,
                raceDef.AttributeModifiers,
                "attribute_modifiers"
            );

            _append_string_name_array_errors(errors, label, raceDef.TraitIds, "trait_ids");

            _append_racial_granted_skill_array_errors(
                errors,
                label,
                raceDef.RacialGrantedSkills,
                "racial_granted_skills"
            );

            _append_string_name_array_errors(
                errors,
                label,
                raceDef.ProficiencyTags,
                "proficiency_tags"
            );

            _append_string_name_array_errors(errors, label, raceDef.VisionTags, "vision_tags");

            _append_string_name_array_errors(
                errors,
                label,
                raceDef.SaveAdvantageTags,
                "save_advantage_tags"
            );

            _append_string_name_to_string_name_dictionary_errors(
                errors,
                label,
                raceDef.DamageResistances,
                "damage_resistances"
            );

            _append_string_name_array_errors(
                errors,
                label,
                raceDef.DialogueTags,
                "dialogue_tags"
            );

            _append_string_array_errors(
                errors,
                label,
                raceDef.RacialTraitSummary,
                "racial_trait_summary"
            );
        }

        return errors;
    }

}
