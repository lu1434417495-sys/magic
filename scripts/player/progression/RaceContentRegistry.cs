using Godot;

[GlobalClass]
public partial class RaceContentRegistry : IdentityContentRegistryBase
{
    private const string RACE_CONFIG_DIRECTORY = "res://data/configs/races";

    private System.Collections.Generic.Dictionary<StringName, RaceDef> _race_defs = new();

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
        ClearRegistryData();
        _validation_errors.Clear();

        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);

        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public System.Collections.Generic.IReadOnlyDictionary<StringName, RaceDef> GetRaceDefsTyped()
    {
        return new System.Collections.Generic.Dictionary<StringName, RaceDef>(_race_defs);
    }

    protected override void ClearRegistryData()
    {
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_race_defs.Values);
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

        if (resource is not RaceDef raceDef)
        {
            GodotRefCountedDisposer.KeepBorrowedResourceGraphAlive(resource);
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

        _race_defs[raceDef.race_id] = raceDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();

        foreach (var raceKey in _sorted_registry_keys(_race_defs.Keys))
        {
            var raceId = new StringName(raceKey);
            var raceDef = _race_defs[raceId];

            var label = $"Race {raceId}";

            _append_string_name_field_error(errors, label, "race_id", raceDef.race_id);

            _append_string_field_error(errors, label, "display_name", raceDef.display_name);

            _append_string_field_error(errors, label, "description", raceDef.description);

            _append_string_name_field_error(
                errors,
                label,
                "age_profile_id",
                raceDef.age_profile_id
            );

            _append_string_name_field_error(
                errors,
                label,
                "default_subrace_id",
                raceDef.default_subrace_id
            );

            _append_string_name_array_errors(errors, label, V(raceDef.subrace_ids), "subrace_ids");

            _append_string_name_field_error(
                errors,
                label,
                "body_size_category",
                raceDef.body_size_category
            );

            _append_int_field_error(errors, label, "base_speed", raceDef.base_speed);

            if (raceDef.base_speed <= 0)
                errors.Add($"{label}.base_speed must be > 0.");

            _append_attribute_modifier_array_errors(
                errors,
                label,
                V(raceDef.attribute_modifiers),
                "attribute_modifiers"
            );

            _append_string_name_array_errors(errors, label, V(raceDef.trait_ids), "trait_ids");

            _append_racial_granted_skill_array_errors(
                errors,
                label,
                V(raceDef.racial_granted_skills),
                "racial_granted_skills"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(raceDef.proficiency_tags),
                "proficiency_tags"
            );

            _append_string_name_array_errors(errors, label, V(raceDef.vision_tags), "vision_tags");

            _append_string_name_array_errors(
                errors,
                label,
                V(raceDef.save_advantage_tags),
                "save_advantage_tags"
            );

            _append_string_name_to_string_name_dictionary_errors(
                errors,
                label,
                raceDef.damage_resistances,
                "damage_resistances"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(raceDef.dialogue_tags),
                "dialogue_tags"
            );

            _append_string_array_errors(
                errors,
                label,
                V(raceDef.racial_trait_summary),
                "racial_trait_summary"
            );
        }

        return errors;
    }

    private static Godot.Collections.Array V<[MustBeVariant] T>(Godot.Collections.Array<T> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (T value in values)
            result.Add(Variant.From(value));
        return result;
    }
}
