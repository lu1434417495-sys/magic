using Godot;

[GlobalClass]
public partial class RaceTraitContentRegistry : IdentityContentRegistryBase
{
    private const string RaceTraitConfigDirectoryPath = "res://data/configs/race_traits";

    private System.Collections.Generic.Dictionary<StringName, RaceTraitDef> _race_trait_defs = new();

    public RaceTraitContentRegistry()
    {
        _registry_label = "RaceTraitContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(RaceTraitConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _race_trait_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public System.Collections.Generic.IReadOnlyDictionary<StringName, RaceTraitDef> GetRaceTraitDefsTyped()
    {
        return new System.Collections.Generic.Dictionary<StringName, RaceTraitDef>(_race_trait_defs);
    }

    protected override void ClearRegistryData()
    {
        _race_trait_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load race trait config {resourcePath}.");
            return;
        }
        if (resource is not RaceTraitDef traitDef)
        {
            _validation_errors.Add($"Race trait config {resourcePath} is not a RaceTraitDef.");
            return;
        }

        var traitId = traitDef.trait_id;
        if (traitId == "")
        {
            _validation_errors.Add($"Race trait config {resourcePath} is missing trait_id.");
            return;
        }
        if (_race_trait_defs.ContainsKey(traitId))
        {
            _validation_errors.Add($"Duplicate race trait_id registered: {traitId}");
            return;
        }

        _race_trait_defs[traitId] = traitDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var traitKey in _sorted_registry_keys(_race_trait_defs.Keys))
        {
            var traitId = new StringName(traitKey);
            _append_trait_validation_errors(errors, traitId, _race_trait_defs[traitId]);
        }
        return errors;
    }

    private void _append_trait_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName traitId,
        RaceTraitDef traitDef
    )
    {
        var ownerLabel = $"RaceTrait {traitId}";
        _append_string_name_field_error(errors, ownerLabel, "trait_id", traitDef.trait_id);
        _append_string_field_error(errors, ownerLabel, "display_name", traitDef.display_name);
        _append_string_field_error(errors, ownerLabel, "description", traitDef.description);
        _append_string_name_field_error(errors, ownerLabel, "trigger_type", traitDef.trigger_type);
        var triggerType = traitDef.trigger_type;
        if (TraitTriggerContentRules.ToTriggerKind(triggerType) == TraitTriggerKind.Unknown)
            errors.Add($"{ownerLabel} uses unsupported trigger_type {triggerType}.");

        _append_string_name_field_error(errors, ownerLabel, "effect_type", traitDef.effect_type);
        var effectType = traitDef.effect_type;
        if (RaceTraitDef.ToEffectKind(effectType) == RaceTraitEffectKind.Unknown)
            errors.Add($"{ownerLabel} uses unsupported effect_type {effectType}.");

        foreach (var keyValue in traitDef.@params.Keys)
        {
            if (
                keyValue.VariantType != Variant.Type.String
                && keyValue.VariantType != Variant.Type.StringName
            )
            {
                errors.Add($"{ownerLabel}.params key {keyValue} must be a String or StringName.");
                continue;
            }
            if (string.IsNullOrEmpty(keyValue.AsString().StripEdges()))
                errors.Add($"{ownerLabel}.params has an empty key.");
        }
    }
}
