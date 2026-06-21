using Godot;

[GlobalClass]
public partial class SubraceContentRegistry : IdentityContentRegistryBase
{
    private const string SUBRACE_CONFIG_DIRECTORY = "res://data/configs/subraces";

    private System.Collections.Generic.Dictionary<StringName, SubraceDef> _subrace_defs = new();

    public SubraceContentRegistry()
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
        ClearRegistryData();
        _validation_errors.Clear();

        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);

        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public System.Collections.Generic.IReadOnlyDictionary<StringName, SubraceDef> GetSubraceDefsTyped()
    {
        return new System.Collections.Generic.Dictionary<StringName, SubraceDef>(_subrace_defs);
    }

    protected override void ClearRegistryData()
    {
        GodotRefCountedDisposer.KeepBorrowedResourceGraphsAlive(_subrace_defs.Values);
        _subrace_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load subrace config {resourcePath}.");
            return;
        }

        if (resource is not SubraceDef subraceDef)
        {
            GodotRefCountedDisposer.KeepBorrowedResourceGraphAlive(resource);
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

        _subrace_defs[subraceDef.subrace_id] = subraceDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();

        foreach (var subraceKey in _sorted_registry_keys(_subrace_defs.Keys))
        {
            var subraceId = new StringName(subraceKey);
            var subraceDef = _subrace_defs[subraceId];

            var label = $"Subrace {subraceId}";

            _append_string_name_field_error(errors, label, "subrace_id", subraceDef.subrace_id);

            _append_string_name_field_error(
                errors,
                label,
                "parent_race_id",
                subraceDef.parent_race_id
            );

            _append_string_field_error(errors, label, "display_name", subraceDef.display_name);

            _append_string_field_error(errors, label, "description", subraceDef.description);

            _append_string_name_field_error(
                errors,
                label,
                "body_size_category_override",
                subraceDef.body_size_category_override,
                true
            );

            _append_int_field_error(errors, label, "speed_bonus", subraceDef.speed_bonus);

            _append_attribute_modifier_array_errors(
                errors,
                label,
                V(subraceDef.attribute_modifiers),
                "attribute_modifiers"
            );

            _append_string_name_array_errors(errors, label, V(subraceDef.trait_ids), "trait_ids");

            _append_racial_granted_skill_array_errors(
                errors,
                label,
                V(subraceDef.racial_granted_skills),
                "racial_granted_skills"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(subraceDef.proficiency_tags),
                "proficiency_tags"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(subraceDef.vision_tags),
                "vision_tags"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(subraceDef.save_advantage_tags),
                "save_advantage_tags"
            );

            _append_string_name_to_string_name_dictionary_errors(
                errors,
                label,
                subraceDef.damage_resistances,
                "damage_resistances"
            );

            _append_string_name_array_errors(
                errors,
                label,
                V(subraceDef.dialogue_tags),
                "dialogue_tags"
            );

            _append_string_array_errors(
                errors,
                label,
                V(subraceDef.racial_trait_summary),
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
