using System.Collections.Generic;
using Godot;

public class AgeContentRegistry : IdentityContentRegistryBase
{
    private const string AgeProfileConfigDirectoryPath = ProfessionIdentityJsonDomains.AgeProfileDirectory;

    private readonly Dictionary<StringName, AgeProfileDefinition> _age_profile_defs = new();
    private readonly IContentJsonSourceReader _jsonSourceReader;

    internal AgeContentRegistry(bool loadDefaultContent = true)
        : this(new GodotContentJsonSourceReader(), loadDefaultContent) { }

    internal AgeContentRegistry(IContentJsonSourceReader jsonSourceReader, bool loadDefaultContent = true)
        : base()
    {
        _jsonSourceReader = jsonSourceReader
            ?? throw new System.ArgumentNullException(nameof(jsonSourceReader));
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
            ImportDirectory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, AgeProfileDefinition> GetAgeProfileDefsTyped() =>
        _snapshot_definitions(_age_profile_defs);

    protected override void ClearRegistryData()
    {
        _age_profile_defs.Clear();
    }

    private void ImportDirectory(string directoryPath)
    {
        ContentImportBatch<AgeProfileImportModel> batch = ProfessionIdentityJsonImport
            .CreateAgeProfileDescriptor(directoryPath, _jsonSourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validation_errors.Add(ProfessionIdentityJsonImport.FormatDiagnostic(diagnostic));
        foreach (ContentImportEntry<AgeProfileImportModel> entry in batch.Entries)
        {
            try
            {
                AgeProfileDefinition definition = ProfessionIdentityDefinitionProjector.Project(entry.Import);
                if (!_age_profile_defs.TryAdd(definition.ProfileId, definition))
                    _validation_errors.Add($"Duplicate age profile_id registered: {definition.ProfileId}");
            }
            catch (System.Exception exception)
                when (exception is System.IO.InvalidDataException
                    or System.InvalidOperationException)
            {
                _validation_errors.Add(
                    $"Age profile JSON {entry.Context.SourceLabel} projection failed: {exception.GetType().Name}: {exception.Message}"
                );
            }
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
                errors.Add($"{stageLabel} must be an AgeStageRuleDefinition.");
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
