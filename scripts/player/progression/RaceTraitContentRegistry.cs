using Godot;

[GlobalClass]
public partial class RaceTraitContentRegistry : IdentityContentRegistryBase
{
    public const string RACE_TRAIT_CONFIG_DIRECTORY = "res://data/configs/race_traits";

    private static readonly Godot.Collections.Array<StringName> ValidEffectTypes = new()
    {
        "darkvision",
        "superior_darkvision",
        "fey_ancestry",
        "brave",
        "halfling_luck",
        "savage_attacks",
        "relentless_endurance",
        "gnome_cunning",
        "dwarven_resilience",
        "duergar_resilience",
        "human_versatility",
        "small_body",
        "fleet_of_foot",
        "dragon_breath",
        "racial_spell_grant",
        "damage_resistance",
        "save_advantage",
        "civil_militia",
        "keen_senses",
        "trance",
        "elven_weapon_training",
        "drow_weapon_training",
        "dwarven_combat_training",
        "shield_dwarf_armor_training",
        "dwarven_toughness",
        "menacing",
        "halfling_nimbleness",
        "naturally_stealthy",
        "mask_of_the_wild",
        "stonecunning",
        "forest_gnome_magic",
        "deep_gnome_camouflage",
        "artificers_lore",
        "duergar_magic",
        "githyanki_martial_prodigy",
        "astral_knowledge",
        "githyanki_psionics",
        "infernal_legacy",
        "asmodeus_legacy",
        "mephistopheles_legacy",
        "zariel_legacy",
        "drow_magic",
        "draconic_ancestry",
    };

    private Godot.Collections.Dictionary _race_trait_defs = new();

    public RaceTraitContentRegistry()
    {
        _registry_label = "RaceTraitContentRegistry";
        rebuild();
    }

    public static string race_trait_config_directory() => RACE_TRAIT_CONFIG_DIRECTORY;

    public void rebuild() => load_from_directory(RACE_TRAIT_CONFIG_DIRECTORY);

    public void load_from_directory(string directoryPath)
    {
        _race_trait_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_race_trait_defs() => _race_trait_defs.Duplicate();

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
        foreach (var traitKey in _sorted_registry_keys(_race_trait_defs))
        {
            var traitId = new StringName(traitKey);
            if (!_race_trait_defs.ContainsKey(traitId))
                continue;
            var traitDef = _race_trait_defs[traitId].AsGodotObject() as RaceTraitDef;
            if (traitDef == null)
                continue;
            _append_trait_validation_errors(errors, traitId, traitDef);
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
        if (!TraitTriggerContentRules.is_valid_trigger_type(triggerType))
            errors.Add($"{ownerLabel} uses unsupported trigger_type {triggerType}.");

        _append_string_name_field_error(errors, ownerLabel, "effect_type", traitDef.effect_type);
        var effectType = traitDef.effect_type;
        if (!ValidEffectTypes.Contains(effectType))
            errors.Add($"{ownerLabel} uses unsupported effect_type {effectType}.");

        foreach (var keyVariant in traitDef.@params.Keys)
        {
            if (!_is_string_or_string_name(keyVariant))
            {
                errors.Add($"{ownerLabel}.params key {keyVariant} must be a String or StringName.");
                continue;
            }
            if (string.IsNullOrEmpty(keyVariant.AsString().StripEdges()))
                errors.Add($"{ownerLabel}.params has an empty key.");
        }
    }
}
