using System.Collections.Generic;
using System.IO;
using Godot;

public class StageAdvancementContentRegistry : IdentityContentRegistryBase
{
    private const string StageAdvancementConfigDirectoryPath =
        "res://data/configs/stage_advancements";

    private readonly Dictionary<StringName, StageAdvancementDefinition> _stage_advancement_defs = new();

    internal StageAdvancementContentRegistry(IContentResourceLoader resourceLoader)
        : this(resourceLoader, loadDefaultContent: true) { }

    internal StageAdvancementContentRegistry(
        IContentResourceLoader resourceLoader,
        bool loadDefaultContent
    )
        : base(resourceLoader)
    {
        _registry_label = "StageAdvancementContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(StageAdvancementConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _stage_advancement_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, StageAdvancementDefinition> GetStageAdvancementDefsTyped() =>
        _snapshot_definitions(_stage_advancement_defs);

    protected override void ClearRegistryData()
    {
        _stage_advancement_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load stage advancement config {resourcePath}.");
            return;
        }
        if (resource is not StageAdvancementModifier modifier)
        {
            _validation_errors.Add(
                $"Stage advancement config {resourcePath} is not a StageAdvancementModifier."
            );
            return;
        }
        if (modifier.modifier_id == "")
        {
            _validation_errors.Add(
                $"Stage advancement config {resourcePath} is missing modifier_id."
            );
            return;
        }
        if (_stage_advancement_defs.ContainsKey(modifier.modifier_id))
        {
            _validation_errors.Add(
                $"Duplicate stage advancement modifier_id registered: {modifier.modifier_id}"
            );
            return;
        }

        try
        {
            StageAdvancementDefinition definition = StageAdvancementDefinition.FromResource(
                modifier,
                $"stage_advancement.{modifier.modifier_id}"
            );
            _stage_advancement_defs.Add(definition.ModifierId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Stage advancement config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();
        foreach (var modifierKey in _sorted_registry_keys(_stage_advancement_defs.Keys))
        {
            var modifierId = new StringName(modifierKey);
            _append_stage_advancement_validation_errors(errors, modifierId, _stage_advancement_defs[modifierId]);
        }
        return errors;
    }

    private void _append_stage_advancement_validation_errors(
        ICollection<string> errors,
        StringName modifierId,
        StageAdvancementDefinition modifier
    )
    {
        var ownerLabel = $"StageAdvancement {modifierId}";
        _append_string_name_field_error(errors, ownerLabel, "modifier_id", modifier.ModifierId);
        _append_string_field_error(errors, ownerLabel, "display_name", modifier.DisplayName);
        _append_string_name_field_error(errors, ownerLabel, "target_axis", modifier.TargetAxis);
        StageAdvancementTargetAxis axisKind = modifier.TargetAxisKind;
        if (axisKind == StageAdvancementTargetAxis.Unknown)
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.TargetAxis}.");
        _append_int_field_error(errors, ownerLabel, "stage_offset", modifier.StageOffset);
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "max_stage_id",
            modifier.MaxStageId,
            true
        );

        if (
            axisKind == StageAdvancementTargetAxis.Bloodline
            || axisKind == StageAdvancementTargetAxis.Divine
        )
        {
            if (modifier.MaxStageId == "")
                errors.Add(
                    $"{ownerLabel}.max_stage_id must be non-empty for target_axis {modifier.TargetAxis}."
                );
        }
        else if (modifier.StageOffset <= 0)
        {
            errors.Add(
                $"{ownerLabel}.stage_offset must be > 0 for age-stage axis (current value yields no advancement)."
            );
        }

        _append_string_name_array_errors(
            errors,
            ownerLabel,
            modifier.AppliesToRaceIds,
            "applies_to_race_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            modifier.AppliesToSubraceIds,
            "applies_to_subrace_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            modifier.AppliesToBloodlineIds,
            "applies_to_bloodline_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            modifier.AppliesToAscensionIds,
            "applies_to_ascension_ids"
        );
        _append_bool_field_error(
            errors,
            ownerLabel,
            "grants_attributes",
            modifier.GrantsAttributes
        );
        _append_bool_field_error(errors, ownerLabel, "grants_traits", modifier.GrantsTraits);
        _append_bool_field_error(
            errors,
            ownerLabel,
            "grants_body_size_change",
            modifier.GrantsBodySizeChange
        );
    }

}
