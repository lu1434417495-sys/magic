using System.Collections.Generic;
using System.IO;
using Godot;

public class AgeContentRegistry : IdentityContentRegistryBase
{
    private const string AgeProfileConfigDirectoryPath = "res://data/configs/age_profiles";

    private readonly Dictionary<StringName, AgeProfileDefinition> _age_profile_defs = new();

    internal AgeContentRegistry(IContentResourceLoader resourceLoader)
        : this(resourceLoader, loadDefaultContent: true) { }

    internal AgeContentRegistry(
        IContentResourceLoader resourceLoader,
        bool loadDefaultContent
    )
        : base(resourceLoader)
    {
        _registry_label = "AgeContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(AgeProfileConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _age_profile_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, AgeProfileDefinition> GetAgeProfileDefsTyped() =>
        _snapshot_definitions(_age_profile_defs);

    protected override void ClearRegistryData()
    {
        _age_profile_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load age profile config {resourcePath}.");
            return;
        }
        if (resource is not AgeProfileDef profileDef)
        {
            _validation_errors.Add($"Age profile config {resourcePath} is not an AgeProfileDef.");
            return;
        }
        if (profileDef.profile_id == "")
        {
            _validation_errors.Add($"Age profile config {resourcePath} is missing profile_id.");
            return;
        }
        if (_age_profile_defs.ContainsKey(profileDef.profile_id))
        {
            _validation_errors.Add($"Duplicate age profile_id registered: {profileDef.profile_id}");
            return;
        }

        try
        {
            AgeProfileDefinition definition = AgeProfileDefinition.FromResource(
                profileDef,
                $"age_profile.{profileDef.profile_id}"
            );
            _age_profile_defs.Add(definition.ProfileId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Age profile config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();
        foreach (var profileKey in _sorted_registry_keys(_age_profile_defs.Keys))
        {
            var profileId = new StringName(profileKey);
            AgeProfileDefinition profileDef = _age_profile_defs[profileId];
            _append_age_profile_validation_errors(errors, profileId, profileDef);
        }
        return errors;
    }

    private void _append_age_profile_validation_errors(
        ICollection<string> errors,
        StringName profileId,
        AgeProfileDefinition profileDef
    )
    {
        var ownerLabel = $"AgeProfile {profileId}";
        _append_string_name_field_error(errors, ownerLabel, "profile_id", profileDef.ProfileId);
        _append_string_name_field_error(errors, ownerLabel, "race_id", profileDef.RaceId);

        var ageFields = new (string Label, int Value)[]
        {
            ("child_age", profileDef.ChildAge),
            ("teen_age", profileDef.TeenAge),
            ("young_adult_age", profileDef.YoungAdultAge),
            ("adult_age", profileDef.AdultAge),
            ("middle_age", profileDef.MiddleAge),
            ("old_age", profileDef.OldAge),
            ("venerable_age", profileDef.VenerableAge),
            ("max_natural_age", profileDef.MaxNaturalAge),
        };
        var previousValue = -1;
        var previousField = "";
        foreach (var field in ageFields)
        {
            string fieldLabel = field.Label;
            int intValue = field.Value;
            if (intValue < 0)
                errors.Add($"{ownerLabel}.{fieldLabel} must be >= 0.");
            else if (previousValue >= 0 && intValue < previousValue)
                errors.Add(
                    $"{ownerLabel}.{fieldLabel} ({intValue}) must be >= {previousField} ({previousValue})."
                );
            previousValue = intValue;
            previousField = fieldLabel;
        }

        int maxNaturalAgeInt = profileDef.MaxNaturalAge;
        _append_age_stage_rule_errors(errors, ownerLabel, profileDef.StageRules, "stage_rules");

        var selectableStageIds = new HashSet<StringName>();
        foreach (AgeStageRuleDefinition stageRule in profileDef.StageRules)
        {
            if (stageRule.StageId != "" && stageRule.SelectableInCreation)
                selectableStageIds.Add(stageRule.StageId);
        }

        _append_string_name_array_errors(
            errors,
            ownerLabel,
            profileDef.CreationStageIds,
            "creation_stage_ids"
        );
        foreach (StringName stageIdName in profileDef.CreationStageIds)
        {
            if (stageIdName != "" && !selectableStageIds.Contains(stageIdName))
                errors.Add(
                    $"{ownerLabel}.creation_stage_ids references stage {stageIdName} that is not selectable_in_creation."
                );
        }

        _append_string_name_to_int_dictionary_errors(
            errors,
            ownerLabel,
            profileDef.DefaultAgeByStage,
            "default_age_by_stage"
        );
        foreach ((StringName stageKey, int defaultAgeInt) in profileDef.DefaultAgeByStage)
        {
            if (defaultAgeInt < 0)
                errors.Add(
                    $"{ownerLabel}.default_age_by_stage[{stageKey}] must be >= 0."
                );
            else if (maxNaturalAgeInt > 0 && defaultAgeInt > maxNaturalAgeInt)
                errors.Add(
                    $"{ownerLabel}.default_age_by_stage[{stageKey}] ({defaultAgeInt}) exceeds max_natural_age ({maxNaturalAgeInt})."
                );
        }
    }

    private void _append_age_stage_rule_errors(
        ICollection<string> errors,
        string ownerLabel,
        IReadOnlyList<AgeStageRuleDefinition> stageRules,
        string fieldLabel
    )
    {
        var seenStageIds = new HashSet<StringName>();
        for (var index = 0; index < stageRules.Count; index++)
        {
            AgeStageRuleDefinition stageRule = stageRules[index];
            var stageLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (stageRule == null)
            {
                errors.Add($"{stageLabel} must be an AgeStageRule.");
                continue;
            }

            _append_string_name_field_error(errors, stageLabel, "stage_id", stageRule.StageId);
            if (stageRule.StageId != "")
            {
                if (!seenStageIds.Add(stageRule.StageId))
                    errors.Add($"{ownerLabel} declares duplicate stage_id {stageRule.StageId}.");
            }
            _append_required_string_field_error(
                errors,
                stageLabel,
                "display_name",
                stageRule.DisplayName
            );
            _append_required_string_field_error(
                errors,
                stageLabel,
                "description",
                stageRule.Description
            );
            _append_attribute_modifier_array_errors(
                errors,
                stageLabel,
                stageRule.AttributeModifiers,
                "attribute_modifiers"
            );
            _append_string_name_array_errors(
                errors,
                stageLabel,
                stageRule.TraitIds,
                "trait_ids"
            );
            if (stageRule.TraitIds.Count > 0)
                errors.Add(
                    $"{stageLabel}.trait_ids is not yet supported by runtime passive projection; remove or implement age stage trait projection first."
                );
            _append_string_array_errors(
                errors,
                stageLabel,
                stageRule.TraitSummary,
                "trait_summary"
            );
        }
    }

}
