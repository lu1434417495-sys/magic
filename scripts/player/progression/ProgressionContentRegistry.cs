using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public class ProgressionContentRegistry : IValidatableRegistry, System.IDisposable
{
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName PracticeMeditation = "meditation";
    private static readonly StringName PracticeCultivation = "cultivation";
    private const string EquipmentAbilityConfigDirectory =
        "res://data/configs/equipment_abilities";

    private static readonly StringName[] PracticeTrackTags =
    {
        PracticeMeditation,
        PracticeCultivation,
    };
    private GDictionary _skillDefs = new();
    private GDictionary _professionDefs = new();
    private GDictionary _achievementDefs = new();
    private GDictionary _questDefs = new();
    private GDictionary _raceDefs = new();
    private GDictionary _subraceDefs = new();
    private GDictionary _traitDefs = new();
    private GDictionary _ageProfileDefs = new();
    private GDictionary _bloodlineDefs = new();
    private GDictionary _bloodlineStageDefs = new();
    private GDictionary _ascensionDefs = new();
    private GDictionary _ascensionStageDefs = new();
    private GDictionary _stageAdvancementDefs = new();
    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitionIndex = new();
    private readonly Dictionary<StringName, ProfessionDef> _professionDefIndex = new();
    private readonly Dictionary<StringName, AchievementDef> _achievementDefIndex = new();
    private readonly Dictionary<StringName, QuestDef> _questDefIndex = new();
    private readonly Dictionary<StringName, RaceDef> _raceDefIndex = new();
    private readonly Dictionary<StringName, SubraceDef> _subraceDefIndex = new();
    private readonly Dictionary<StringName, TraitDef> _traitDefIndex = new();
    private readonly Dictionary<StringName, AgeProfileDef> _ageProfileDefIndex = new();
    private readonly Dictionary<StringName, BloodlineDef> _bloodlineDefIndex = new();
    private readonly Dictionary<StringName, BloodlineStageDef> _bloodlineStageDefIndex = new();
    private readonly Dictionary<StringName, AscensionDef> _ascensionDefIndex = new();
    private readonly Dictionary<StringName, AscensionStageDef> _ascensionStageDefIndex = new();
    private readonly Dictionary<StringName, StageAdvancementModifier> _stageAdvancementDefIndex =
        new();

    private readonly SkillContentRegistry _skillContentRegistry = new();
    private readonly ProfessionContentRegistry _professionContentRegistry = new();
    private readonly RaceContentRegistry _raceContentRegistry = new();
    private readonly SubraceContentRegistry _subraceContentRegistry = new();
    private readonly TraitContentRegistry _traitContentRegistry = new();
    private readonly AgeContentRegistry _ageContentRegistry = new();
    private readonly BloodlineContentRegistry _bloodlineContentRegistry = new();
    private readonly AscensionContentRegistry _ascensionContentRegistry = new();
    private readonly StageAdvancementContentRegistry _stageAdvancementContentRegistry = new();
    private readonly QuestContentRegistry _questContentRegistry = new();
    private readonly EquipmentAbilityContentRegistry _equipmentAbilityContentRegistry = new();

    private GStringArray _validationErrors = new();
    private readonly List<string> _questRegistrationErrors = new();
    private bool _disposed;
    private bool _typedIndexesDirty = true;

    public GDictionary _skill_defs
    {
        get => _skillDefs;
        set
        {
            _skillDefs = RegisterDefinitionBucket(value, "_skill_defs.set");
            _typedIndexesDirty = true;
        }
    }
    public GDictionary _profession_defs
    {
        get => _professionDefs;
        set
        {
            _professionDefs = RegisterDefinitionBucket(value, "_profession_defs.set");
            _typedIndexesDirty = true;
        }
    }
    public GDictionary _achievement_defs
    {
        get => _achievementDefs;
        set
        {
            _achievementDefs = RegisterDefinitionBucket(value, "_achievement_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _quest_defs
    {
        get => _questDefs;
        set
        {
            _questDefs = RegisterDefinitionBucket(value, "_quest_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _race_defs
    {
        get => _raceDefs;
        set
        {
            _raceDefs = RegisterDefinitionBucket(value, "_race_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _subrace_defs
    {
        get => _subraceDefs;
        set
        {
            _subraceDefs = RegisterDefinitionBucket(value, "_subrace_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _trait_defs
    {
        get => _traitDefs;
        set
        {
            _traitDefs = RegisterDefinitionBucket(value, "_trait_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _age_profile_defs
    {
        get => _ageProfileDefs;
        set
        {
            _ageProfileDefs = RegisterDefinitionBucket(value, "_age_profile_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _bloodline_defs
    {
        get => _bloodlineDefs;
        set
        {
            _bloodlineDefs = RegisterDefinitionBucket(value, "_bloodline_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _bloodline_stage_defs
    {
        get => _bloodlineStageDefs;
        set
        {
            _bloodlineStageDefs = RegisterDefinitionBucket(value, "_bloodline_stage_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _ascension_defs
    {
        get => _ascensionDefs;
        set
        {
            _ascensionDefs = RegisterDefinitionBucket(value, "_ascension_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _ascension_stage_defs
    {
        get => _ascensionStageDefs;
        set
        {
            _ascensionStageDefs = RegisterDefinitionBucket(value, "_ascension_stage_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GDictionary _stage_advancement_defs
    {
        get => _stageAdvancementDefs;
        set
        {
            _stageAdvancementDefs = RegisterDefinitionBucket(value, "_stage_advancement_defs.set");
            _typedIndexesDirty = true;
            SyncTypedDefinitionIndexes();
        }
    }
    public GStringArray _validation_errors
    {
        get => _validationErrors;
        set => _validationErrors = value ?? new GStringArray();
    }
    public GStringArray _quest_registration_errors
    {
        get => ToGodotStringArray(_questRegistrationErrors);
        set
        {
            _questRegistrationErrors.Clear();
            if (value == null)
                return;
            foreach (string error in value)
                AppendUniqueErrors(_questRegistrationErrors, new[] { error });
        }
    }

    public ProgressionContentRegistry()
    {
        Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ClearRuntimeCaches();
        _skillContentRegistry.Dispose();
        _professionContentRegistry.Dispose();
        _raceContentRegistry.Dispose();
        _subraceContentRegistry.Dispose();
        _traitContentRegistry.Dispose();
        _ageContentRegistry.Dispose();
        _bloodlineContentRegistry.Dispose();
        _ascensionContentRegistry.Dispose();
        _stageAdvancementContentRegistry.Dispose();
        _equipmentAbilityContentRegistry.Dispose();
    }

    public void Rebuild()
    {
        ClearRuntimeCaches();

        _skillContentRegistry.Rebuild();
        _skillDefs = RegisterDefinitionBucket(
            _skillContentRegistry.DuplicateSkillResourceBucketForProgressionRegistry(),
            "SkillContentRegistry.skill_defs"
        );
        AppendArray(_validationErrors, _skillContentRegistry.Validate());

        _professionContentRegistry.Setup(_skillContentRegistry.GetSkillDefinitionsTyped());
        _professionDefs = ProjectTypedDictionary(_professionContentRegistry.GetProfessionDefsTyped());

        _raceContentRegistry.Rebuild();
        _raceDefs = ProjectTypedDictionary(_raceContentRegistry.GetRaceDefsTyped());
        _subraceContentRegistry.Rebuild();
        _subraceDefs = ProjectTypedDictionary(_subraceContentRegistry.GetSubraceDefsTyped());
        _traitContentRegistry.Rebuild();
        _traitDefs = ProjectTraitDefs(_traitContentRegistry.GetTraitDefsTyped());
        _ageContentRegistry.Rebuild();
        _ageProfileDefs = ProjectTypedDictionary(_ageContentRegistry.GetAgeProfileDefsTyped());
        _bloodlineContentRegistry.Rebuild();
        _bloodlineDefs = ProjectTypedDictionary(_bloodlineContentRegistry.GetBloodlineDefsTyped());
        _bloodlineStageDefs = ProjectTypedDictionary(
            _bloodlineContentRegistry.GetBloodlineStageDefsTyped()
        );
        _ascensionContentRegistry.Rebuild();
        _ascensionDefs = ProjectTypedDictionary(_ascensionContentRegistry.GetAscensionDefsTyped());
        _ascensionStageDefs = ProjectTypedDictionary(
            _ascensionContentRegistry.GetAscensionStageDefsTyped()
        );
        _stageAdvancementContentRegistry.Rebuild();
        _stageAdvancementDefs = ProjectTypedDictionary(
            _stageAdvancementContentRegistry.GetStageAdvancementDefsTyped()
        );

        _questContentRegistry.Rebuild();
        foreach (string error in _questContentRegistry.GetValidationErrors())
            _validationErrors.Add(error);
        foreach (QuestDef questDef in _questContentRegistry.GetQuestDefsTyped().Values)
            _register_quest(questDef);
        _register_seed_achievements();
        SyncTypedDefinitionIndexes();

        EquipmentAbilityRegistryBuildResult equipmentAbilityResult =
            _equipmentAbilityContentRegistry.Rebuild(
                LoadEquipmentAbilityContentPacks(),
                BuildEquipmentAbilityValidationContext()
            );
        foreach (string error in equipmentAbilityResult.Errors)
            _validationErrors.Add(error);

        AppendArray(_validationErrors, _professionContentRegistry.Validate());
        AppendArray(_validationErrors, _raceContentRegistry.Validate());
        AppendArray(_validationErrors, _subraceContentRegistry.Validate());
        AppendArray(_validationErrors, _traitContentRegistry.Validate());
        AppendArray(_validationErrors, _ageContentRegistry.Validate());
        AppendArray(_validationErrors, _bloodlineContentRegistry.Validate());
        AppendArray(_validationErrors, _ascensionContentRegistry.Validate());
        AppendArray(_validationErrors, _stageAdvancementContentRegistry.Validate());
        AppendArray(_validationErrors, CollectValidationErrors());
    }

    public IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_skillDefinitionIndex);
    }

    internal GDictionary DuplicateSkillResourceBucketForValidation()
    {
        SyncTypedDefinitionIndexes();
        return DuplicateDictionary(_skillDefs, "skill_defs.validation_snapshot");
    }

    internal IReadOnlyList<Resource> GetLoadedSkillResourcesForFinalizerDrain()
    {
        SyncTypedDefinitionIndexes();
        var result = new List<Resource>();
        foreach (Variant rawKey in _skillDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }

            StringName skillId = rawKey.AsStringName();
            if (skillId == "")
            {
                continue;
            }

            if (_skillDefs[rawKey].AsGodotObject() is Resource resource)
            {
                result.Add(resource);
            }
        }
        return result;
    }

    public IReadOnlyDictionary<StringName, ProfessionDef> GetProfessionDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_professionDefIndex);
    }

    public IReadOnlyDictionary<StringName, AchievementDef> GetAchievementDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_achievementDefIndex);
    }

    public IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_questDefIndex);
    }

    public IReadOnlyList<string> GetQuestRegistrationErrorsTyped()
    {
        return new List<string>(_questRegistrationErrors);
    }

    public IReadOnlyDictionary<StringName, RaceDef> GetRaceDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_raceDefIndex);
    }

    public IReadOnlyDictionary<StringName, SubraceDef> GetSubraceDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_subraceDefIndex);
    }

    public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_traitDefIndex);
    }

    public EquipmentAbilityRegistryBuildResult GetEquipmentAbilityLastBuildResultTyped() =>
        _equipmentAbilityContentRegistry.GetLastBuildResultTyped();

    public int GetEquipmentAbilityContentRevision() =>
        _equipmentAbilityContentRegistry.GetRevision();

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetEquipmentAbilityPackDefinitionsTyped() =>
        _equipmentAbilityContentRegistry.GetPackDefinitionsTyped();

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetEquipmentAbilityBindingDefinitionsTyped() =>
        _equipmentAbilityContentRegistry.GetBindingDefinitionsTyped();

    internal EquipmentAbilityContentRegistry GetEquipmentAbilityContentRegistryTyped() =>
        _equipmentAbilityContentRegistry;

    public IReadOnlyDictionary<StringName, AgeProfileDef> GetAgeProfileDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_ageProfileDefIndex);
    }

    public IReadOnlyDictionary<StringName, BloodlineDef> GetBloodlineDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_bloodlineDefIndex);
    }

    public IReadOnlyDictionary<StringName, BloodlineStageDef> GetBloodlineStageDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_bloodlineStageDefIndex);
    }

    public IReadOnlyDictionary<StringName, AscensionDef> GetAscensionDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_ascensionDefIndex);
    }

    public IReadOnlyDictionary<StringName, AscensionStageDef> GetAscensionStageDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_ascensionStageDefIndex);
    }

    public IReadOnlyDictionary<StringName, StageAdvancementModifier> GetStageAdvancementDefsTyped()
    {
        SyncTypedDefinitionIndexes();
        return CloneTypedDictionary(_stageAdvancementDefIndex);
    }

    public ProgressionIdentityCatalogData GetIdentityCatalogTyped()
    {
        SyncTypedDefinitionIndexes();
        return new ProgressionIdentityCatalogData(
            _raceDefIndex,
            _subraceDefIndex,
            _ageProfileDefIndex,
            _bloodlineDefIndex,
            _bloodlineStageDefIndex,
            _ascensionDefIndex,
            _ascensionStageDefIndex,
            _stageAdvancementDefIndex
        );
    }

    public GStringArray Validate()
    {
        return ToGodotStringArray(ValidateTyped());
    }

    public IReadOnlyList<string> ValidateTyped()
    {
        SyncTypedDefinitionIndexes();
        var errors = new List<string>();
        AppendUniqueErrors(errors, _validationErrors);
        AppendUniqueErrors(errors, _skillContentRegistry.Validate());
        AppendUniqueErrors(errors, _professionContentRegistry.Validate());
        AppendUniqueErrors(errors, _raceContentRegistry.Validate());
        AppendUniqueErrors(errors, _subraceContentRegistry.Validate());
        AppendUniqueErrors(errors, _traitContentRegistry.Validate());
        AppendUniqueErrors(errors, _ageContentRegistry.Validate());
        AppendUniqueErrors(errors, _bloodlineContentRegistry.Validate());
        AppendUniqueErrors(errors, _ascensionContentRegistry.Validate());
        AppendUniqueErrors(errors, _stageAdvancementContentRegistry.Validate());
        AppendUniqueErrors(errors, CollectValidationErrorsTyped());
        return errors;
    }

    public void ReplaceValidationSources(GDictionary sources)
    {
        _validationErrors.Clear();
        ReplaceDefinitionBuckets(sources);
    }

    public void ReplaceDefinitionBuckets(GDictionary sources)
    {
        _skillDefs = DuplicateDictionary(GetDictionary(sources, "skill_defs"), "skill_defs");
        _professionDefs = DuplicateDictionary(GetDictionary(sources, "profession_defs"), "profession_defs");
        _achievementDefs = DuplicateDictionary(GetDictionary(sources, "achievement_defs"), "achievement_defs");
        _questDefs = DuplicateDictionary(GetDictionary(sources, "quest_defs"), "quest_defs");
        _raceDefs = DuplicateDictionary(GetDictionary(sources, "race_defs"), "race_defs");
        _subraceDefs = DuplicateDictionary(GetDictionary(sources, "subrace_defs"), "subrace_defs");
        _traitDefs = DuplicateDictionary(GetDictionary(sources, "trait_defs"), "trait_defs");
        _ageProfileDefs = DuplicateDictionary(GetDictionary(sources, "age_profile_defs"), "age_profile_defs");
        _bloodlineDefs = DuplicateDictionary(GetDictionary(sources, "bloodline_defs"), "bloodline_defs");
        _bloodlineStageDefs = DuplicateDictionary(
            GetDictionary(sources, "bloodline_stage_defs"),
            "bloodline_stage_defs"
        );
        _ascensionDefs = DuplicateDictionary(GetDictionary(sources, "ascension_defs"), "ascension_defs");
        _ascensionStageDefs = DuplicateDictionary(
            GetDictionary(sources, "ascension_stage_defs"),
            "ascension_stage_defs"
        );
        _stageAdvancementDefs = DuplicateDictionary(
            GetDictionary(sources, "stage_advancement_defs"),
            "stage_advancement_defs"
        );
        _typedIndexesDirty = true;
        SyncTypedDefinitionIndexes();
    }

    public GStringArray CollectValidationErrors()
    {
        SyncTypedDefinitionIndexes();
        return ToGodotStringArray(CollectValidationErrorsTyped());
    }

    public void AppendIdentityPhase2ValidationErrors(GStringArray errors)
    {
        SyncTypedDefinitionIndexes();
        if (errors == null)
        {
            return;
        }
        foreach (string error in CollectIdentityPhase2ValidationErrorsTyped())
        {
            errors.Add(error);
        }
    }

    private List<string> CollectValidationErrorsTyped()
    {
        var errors = new List<string>();

        foreach (StringName skillId in SortedKeys(_skillDefinitionIndex))
        {
            var skillErrors = new GStringArray();
            _skillDefinitionIndex.TryGetValue(skillId, out SkillDefinition skillDefinition);
            _append_invalid_skill_errors(
                skillErrors,
                skillId,
                skillDefinition
            );
            if (TryGetSkillDef(skillId, out SkillDef skillDef))
            {
                _append_raw_int_requirement_entry_errors(
                    skillErrors,
                    skillId,
                    skillDef.SkillLevelRequirementEntriesTyped,
                    "skill_level_requirements",
                    "skill_id"
                );
                _append_raw_int_requirement_entry_errors(
                    skillErrors,
                    skillId,
                    skillDef.AttributeRequirementEntriesTyped,
                    "attribute_requirements",
                    "attribute_id"
                );
            }
            AppendUniqueErrors(errors, skillErrors);
        }

        foreach (string achievementKey in ProgressionDataUtils.sorted_string_keys(_achievementDefs))
        {
            var achievementId = new StringName(achievementKey);
            _achievementDefIndex.TryGetValue(achievementId, out AchievementDef achievementDef);
            _append_invalid_achievement_errors(
                errors,
                achievementId,
                achievementDef
            );
        }

        AppendUniqueErrors(errors, CollectIdentityPhase2ValidationErrorsTyped());
        return errors;
    }

    private List<string> CollectIdentityPhase2ValidationErrorsTyped()
    {
        var errors = new List<string>();
        _append_global_stage_id_errors(errors);

        foreach (StringName raceId in SortedKeys(_raceDefIndex))
        {
            _raceDefIndex.TryGetValue(raceId, out RaceDef raceDef);
            _append_race_phase2_errors(errors, raceId, raceDef);
        }
        foreach (StringName subraceId in SortedKeys(_subraceDefIndex))
        {
            _subraceDefIndex.TryGetValue(subraceId, out SubraceDef subraceDef);
            _append_subrace_phase2_errors(
                errors,
                subraceId,
                subraceDef
            );
        }
        foreach (StringName profileId in SortedKeys(_ageProfileDefIndex))
        {
            _ageProfileDefIndex.TryGetValue(profileId, out AgeProfileDef profileDef);
            _append_age_profile_phase2_errors(
                errors,
                profileId,
                profileDef
            );
        }
        foreach (StringName bloodlineId in SortedKeys(_bloodlineDefIndex))
        {
            _bloodlineDefIndex.TryGetValue(bloodlineId, out BloodlineDef bloodlineDef);
            _append_bloodline_phase2_errors(
                errors,
                bloodlineId,
                bloodlineDef
            );
        }
        foreach (StringName stageId in SortedKeys(_bloodlineStageDefIndex))
        {
            _bloodlineStageDefIndex.TryGetValue(stageId, out BloodlineStageDef stageDef);
            _append_bloodline_stage_phase2_errors(
                errors,
                stageId,
                stageDef
            );
        }
        foreach (StringName ascensionId in SortedKeys(_ascensionDefIndex))
        {
            _ascensionDefIndex.TryGetValue(ascensionId, out AscensionDef ascensionDef);
            _append_ascension_phase2_errors(
                errors,
                ascensionId,
                ascensionDef
            );
        }
        foreach (StringName stageId in SortedKeys(_ascensionStageDefIndex))
        {
            _ascensionStageDefIndex.TryGetValue(stageId, out AscensionStageDef stageDef);
            _append_ascension_stage_phase2_errors(
                errors,
                stageId,
                stageDef
            );
        }
        foreach (StringName modifierId in SortedKeys(_stageAdvancementDefIndex))
        {
            _stageAdvancementDefIndex.TryGetValue(
                modifierId,
                out StageAdvancementModifier modifier
            );
            _append_stage_advancement_phase2_errors(
                errors,
                modifierId,
                modifier
            );
        }
        return errors;
    }

    private void ClearRuntimeCaches()
    {
        _skillDefs.Clear();
        _professionDefs.Clear();
        _achievementDefs.Clear();
        _questDefs.Clear();
        _questRegistrationErrors.Clear();
        _raceDefs.Clear();
        _subraceDefs.Clear();
        _traitDefs.Clear();
        _ageProfileDefs.Clear();
        _bloodlineDefs.Clear();
        _bloodlineStageDefs.Clear();
        _ascensionDefs.Clear();
        _ascensionStageDefs.Clear();
        _stageAdvancementDefs.Clear();
        _skillDefinitionIndex.Clear();
        _professionDefIndex.Clear();
        _achievementDefIndex.Clear();
        _questDefIndex.Clear();
        _raceDefIndex.Clear();
        _subraceDefIndex.Clear();
        _traitDefIndex.Clear();
        _ageProfileDefIndex.Clear();
        _bloodlineDefIndex.Clear();
        _bloodlineStageDefIndex.Clear();
        _ascensionDefIndex.Clear();
        _ascensionStageDefIndex.Clear();
        _stageAdvancementDefIndex.Clear();
        _validationErrors.Clear();
        _typedIndexesDirty = true;
    }

    private static IReadOnlyList<EquipmentAbilityContentPackDef> LoadEquipmentAbilityContentPacks()
    {
        var packs = new List<EquipmentAbilityContentPackDef>();
        string globalPath = ProjectSettings.GlobalizePath(EquipmentAbilityConfigDirectory);
        if (!DirAccess.DirExistsAbsolute(globalPath))
            return packs;

        ScanEquipmentAbilityContentDirectory(EquipmentAbilityConfigDirectory, packs);
        return packs;
    }

    private static void ScanEquipmentAbilityContentDirectory(
        string directoryPath,
        List<EquipmentAbilityContentPackDef> packs
    )
    {
        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
            return;

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanEquipmentAbilityContentDirectory(entryPath, packs);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;

                Resource resource = ResourceLoader.Load<Resource>(entryPath);
                if (resource is not EquipmentAbilityContentPackDef pack)
                    continue;
                GodotContentOwnership.RegisterBorrowedContent(pack, entryPath);
                packs.Add(pack);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private EquipmentAbilityContentValidationContext BuildEquipmentAbilityValidationContext()
    {
        SyncTypedDefinitionIndexes();
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = ReadOnlyKeySet(_traitDefIndex),
            KnownSkillIds = ReadOnlyKeySet(_skillDefinitionIndex),
        };
    }

    private void _register_seed_achievements()
    {
        _register_achievement(
            _build_achievement(
                "battle_won_first",
                "首战归来",
                "亲自完成一次战斗胜利，证明自己已经能从正式交战中平安归来。",
                "battle_won",
                "",
                1,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    HpMax,
                    "生命上限",
                    8,
                    "首战后的胆气与耐力提升。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "settlement_wayfarer",
                "行路借火",
                "在据点完成一次事务，学会把旅途见闻整理成可反复回想的经验。",
                "settlement_action_completed",
                "",
                1,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.KnowledgeUnlock),
                    "wayfarer_notes",
                    "旅途见闻",
                    1,
                    "据点经历转化成了可保留的见闻。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "enemy_defeated_apprentice",
                "开刃",
                "累计击倒 3 名敌人，开始掌握主动突进的节奏。",
                "enemy_defeated",
                "",
                3,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillUnlock),
                    "charge",
                    "冲锋",
                    1,
                    "连战后的脚步更敢向前。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "near_death_unbroken",
                "濒死未倒",
                "在生命低于三分之一时承受重击仍存活，证明自身已经能在生死边缘守住形神。",
                "near_death_unbroken_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "warrior_heavy_strike_practice",
                "重击热身",
                "累计施放 5 次重击，挥砍节奏进一步稳定。",
                "skill_used",
                "warrior_heavy_strike",
                5,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.SkillMastery),
                    "warrior_heavy_strike",
                    "重击",
                    10,
                    "熟能生巧。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "profession_promoted_first",
                "迈向正职",
                "完成首次职业晋升，体魄和力量都得到巩固。",
                "profession_promoted",
                "",
                1,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength),
                    "力量",
                    1,
                    "正式晋升让动作更加扎实。"
                ),
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    HpMax,
                    "生命上限",
                    5,
                    "长期训练开始反映到体魄上。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "skill_learned_guard_break",
                "添一门手段",
                "学会裂甲斩，开始愿意把近战手段拓展到不同战术用途。",
                "skill_learned",
                "warrior_guard_break",
                1,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception),
                    "感知",
                    1,
                    "换用不同兵器后，对出手距离和节奏的判断更敏锐。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "knowledge_learned_field_manual",
                "把见闻记下来",
                "学会《野外手册》，开始把零散经历整理成能反复调用的知识。",
                "knowledge_learned",
                "field_manual",
                1,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower),
                    "意志",
                    1,
                    "把经验写成规则后，行动会更有把握。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "skill_mastery_charge_stride",
                "冲锋起步",
                "累计获得 20 点冲锋熟练度，开始掌握直线突进的起手节奏。",
                "skill_mastery_gained",
                "charge",
                20,
                _build_achievement_reward(
                    PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.AttributeDelta),
                    UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
                    "敏捷",
                    1,
                    "反复练习冲锋后，脚步转换更利落。"
                )
            )
        );

        _register_achievement(
            _build_achievement(
                "fortuna_guidance_true",
                "Fortuna Guidance I",
                "已被 Fortuna 标记后，再次对 elite 或 boss 触发一次劣势大成功。",
                "fortuna_guidance_true_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_devout",
                "Fortuna Guidance II",
                "已信 Fortuna 的角色在低血且承受强 debuff 的逆境中活下来并赢下战斗。",
                "fortuna_guidance_devout_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_exalted",
                "Fortuna Guidance III",
                "已信 Fortuna 的角色用高位威胁区间而非门骰，对 elite 或 boss 打出一次大成功。",
                "fortuna_guidance_exalted_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "fortuna_guidance_blessed",
                "Fortuna Guidance IV",
                "完成一个章节且无人永久死亡，并且该角色在本章内至少经历过一次 Fortuna 相关战斗事件。",
                "fortuna_guidance_blessed_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_true",
                "Misfortune Guidance I",
                "已被黑冕标记后，成功用 Misfortune 的封印链终结一次 elite 或 boss。",
                "misfortune_guidance_true_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_devout",
                "Misfortune Guidance II",
                "同一战斗内曾遭遇大失败或强 debuff，随后再用封印链赢下 elite 或 boss。",
                "misfortune_guidance_devout_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_exalted",
                "Misfortune Guidance III",
                "把同一战斗中未用完的 calamity 结算成 shard，并用固定黑冕材料打造第一件黑暗装备。",
                "misfortune_guidance_exalted_manual",
                "",
                1
            )
        );
        _register_achievement(
            _build_achievement(
                "misfortune_guidance_blessed",
                "Misfortune Guidance IV",
                "用 doom_sentence 的宣判击杀完成一次 boss 终结。",
                "misfortune_guidance_blessed_manual",
                "",
                1
            )
        );
    
    }

    private AchievementDef _build_achievement(
        StringName achievementId,
        string displayName,
        string description,
        StringName eventType,
        StringName subjectId,
        int threshold,
        params AchievementRewardDef[] rewards
    )
    {
        var achievement = new AchievementDef
        {
            achievement_id = achievementId,
            display_name = displayName,
            description = description,
            event_type = eventType,
            subject_id = subjectId,
            threshold = threshold,
        };
        if (rewards == null)
        {
            return achievement;
        }
        foreach (AchievementRewardDef reward in rewards)
        {
            if (reward != null)
                achievement.rewards.Add(reward);
        }
        return achievement;
    }

    private QuestDef _build_quest(
        StringName questId,
        string displayName,
        string description,
        StringName providerInteractionId,
        GArray objectiveDefs,
        GArray rewardEntries,
        GStringNameArray tags = null,
        StringName providerKind = null,
        GStringNameArray listingChannels = null,
        string acceptDialogueText = "",
        string acceptFeedbackSuccess = "",
        string acceptFeedbackFailure = "",
        string acceptConfirmationText = ""
    )
    {
        var questDef = new QuestDef
        {
            quest_id = questId,
            display_name = displayName,
            description = description,
            provider_interaction_id = providerInteractionId,
            tags = tags != null ? DuplicateStringNameArray(tags) : new GStringNameArray(),
            provider_kind = providerKind ?? new StringName(""),
            listing_channels = listingChannels != null
                ? DuplicateStringNameArray(listingChannels)
                : new GStringNameArray(),
            accept_dialogue_text = acceptDialogueText,
            accept_feedback_success = acceptFeedbackSuccess,
            accept_feedback_failure = acceptFeedbackFailure,
            accept_confirmation_text = acceptConfirmationText,
        };
        foreach (GDictionary objectiveValue in ReadDictionaryItems(objectiveDefs))
        {
            questDef.objective_defs.Add(objectiveValue.Duplicate(true));
        }
        foreach (GDictionary rewardValue in ReadDictionaryItems(rewardEntries))
        {
            questDef.reward_entries.Add(rewardValue.Duplicate(true));
        }
        return questDef;
    }

    private AchievementRewardDef _build_achievement_reward(
        StringName rewardType,
        StringName targetId,
        string targetLabel,
        int amount,
        string reasonText = ""
    )
    {
        return new AchievementRewardDef
        {
            reward_type = rewardType,
            target_id = targetId,
            target_label = targetLabel,
            amount = amount,
            reason_text = reasonText,
        };
    }

    private void _register_achievement(AchievementDef achievementDef)
    {
        if (achievementDef == null || achievementDef.achievement_id == "")
        {
            _validationErrors.Add(
                "Encountered an achievement definition without an achievement_id."
            );
            return;
        }
        if (_achievementDefs.ContainsKey(achievementDef.achievement_id))
        {
            _validationErrors.Add(
                $"Duplicate achievement_id registered: {achievementDef.achievement_id}"
            );
            return;
        }
        _achievementDefs[achievementDef.achievement_id] = achievementDef.ToDictionary();
        _achievementDefIndex[achievementDef.achievement_id] = achievementDef;
        _typedIndexesDirty = true;
    }

    private void _register_quest(QuestDef questDef)
    {
        if (questDef == null || questDef.quest_id == "")
        {
            _questRegistrationErrors.Add("Encountered a quest definition without a quest_id.");
            return;
        }
        if (_questDefs.ContainsKey(questDef.quest_id))
        {
            _questRegistrationErrors.Add($"Duplicate quest_id registered: {questDef.quest_id}");
            return;
        }

        _questDefs[questDef.quest_id] = questDef;
        _questDefIndex[questDef.quest_id] = questDef;
        _typedIndexesDirty = true;
    }

    private void _append_race_phase2_errors(List<string> errors, StringName raceId, RaceDef raceDef)
    {
        if (raceDef == null)
        {
            return;
        }
        string ownerLabel = $"Race {raceId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category",
            raceDef.body_size_category,
            false
        );
        _append_damage_resistance_errors(errors, ownerLabel, raceDef.damage_resistances);
        _append_trait_reference_errors(errors, ownerLabel, raceDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            raceDef.racial_granted_skills,
            "race"
        );

        if (
            raceDef.age_profile_id != ""
            && !_ageProfileDefIndex.ContainsKey(raceDef.age_profile_id)
        )
        {
            errors.Add($"{ownerLabel} references missing age_profile {raceDef.age_profile_id}.");
        }

        if (raceDef.default_subrace_id != "")
        {
            if (!_subraceDefIndex.ContainsKey(raceDef.default_subrace_id))
            {
                errors.Add(
                    $"{ownerLabel} references missing default_subrace {raceDef.default_subrace_id}."
                );
            }
            else if (!raceDef.subrace_ids.Contains(raceDef.default_subrace_id))
            {
                errors.Add(
                    $"{ownerLabel} default_subrace {raceDef.default_subrace_id} must be listed in subrace_ids."
                );
            }
        }

        foreach (StringName subraceId in raceDef.subrace_ids)
        {
            if (subraceId == "")
            {
                continue;
            }
            if (!_subraceDefIndex.TryGetValue(subraceId, out SubraceDef subraceDef))
            {
                errors.Add($"{ownerLabel} references missing subrace {subraceId}.");
                continue;
            }
            if (subraceDef.parent_race_id != raceId)
            {
                errors.Add(
                    $"{ownerLabel} subrace {subraceId} parent_race_id must be {raceId}, got {subraceDef.parent_race_id}."
                );
            }
        }
    }

    private void _append_subrace_phase2_errors(
        List<string> errors,
        StringName subraceId,
        SubraceDef subraceDef
    )
    {
        if (subraceDef == null)
        {
            return;
        }
        string ownerLabel = $"Subrace {subraceId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category_override",
            subraceDef.body_size_category_override,
            true
        );
        _append_damage_resistance_errors(errors, ownerLabel, subraceDef.damage_resistances);
        _append_trait_reference_errors(errors, ownerLabel, subraceDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            subraceDef.racial_granted_skills,
            "subrace"
        );

        if (subraceDef.parent_race_id == "")
        {
            return;
        }
        if (!_raceDefIndex.TryGetValue(subraceDef.parent_race_id, out RaceDef parentRace))
        {
            errors.Add($"{ownerLabel} references missing parent_race {subraceDef.parent_race_id}.");
            return;
        }
        if (!parentRace.subrace_ids.Contains(subraceId))
        {
            errors.Add(
                $"{ownerLabel} parent_race {subraceDef.parent_race_id} must list this subrace in subrace_ids."
            );
        }
    }

    private void _append_age_profile_phase2_errors(
        List<string> errors,
        StringName profileId,
        AgeProfileDef profileDef
    )
    {
        if (profileDef == null)
        {
            return;
        }
        string ownerLabel = $"AgeProfile {profileId}";
        if (profileDef.race_id != "")
        {
            if (!_raceDefIndex.TryGetValue(profileDef.race_id, out RaceDef raceDef))
            {
                errors.Add($"{ownerLabel} references missing race {profileDef.race_id}.");
            }
            else if (raceDef.age_profile_id != profileId)
            {
                errors.Add(
                    $"{ownerLabel} race {profileDef.race_id} must reference this profile as age_profile_id."
                );
            }
        }
        if (profileDef.stage_rules.Count == 0)
        {
            errors.Add($"{ownerLabel} must declare at least one stage_rules entry.");
        }

        HashSet<StringName> stageIds = _collect_age_profile_stage_ids(profileDef);
        foreach (StringName stageId in profileDef.creation_stage_ids)
        {
            if (stageId != "" && !stageIds.Contains(stageId))
            {
                errors.Add($"{ownerLabel} creation_stage_ids references missing stage {stageId}.");
            }
        }
        foreach (var stageKeyValue in profileDef.default_age_by_stage.Keys)
        {
            StringName stageId = _strict_to_string_name(stageKeyValue);
            if (stageId != "" && !stageIds.Contains(stageId))
            {
                errors.Add(
                    $"{ownerLabel} default_age_by_stage references missing stage {stageId}."
                );
            }
        }
        foreach (AgeStageRule stageRule in profileDef.stage_rules)
        {
            if (stageRule == null)
            {
                continue;
            }
            _append_trait_reference_errors(
                errors,
                $"{ownerLabel} stage {stageRule.stage_id}",
                stageRule.trait_ids,
                "trait_ids"
            );
        }
    }

    private void _append_bloodline_phase2_errors(
        List<string> errors,
        StringName bloodlineId,
        BloodlineDef bloodlineDef
    )
    {
        if (bloodlineDef == null)
        {
            return;
        }
        string ownerLabel = $"Bloodline {bloodlineId}";
        _append_trait_reference_errors(errors, ownerLabel, bloodlineDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            bloodlineDef.racial_granted_skills,
            "bloodline"
        );
        foreach (StringName stageId in bloodlineDef.stage_ids)
        {
            if (stageId == "")
            {
                continue;
            }
            if (!_bloodlineStageDefIndex.TryGetValue(stageId, out BloodlineStageDef stageDef))
            {
                errors.Add($"{ownerLabel} references missing bloodline_stage {stageId}.");
                continue;
            }
            if (stageDef.bloodline_id != bloodlineId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} bloodline_id must be {bloodlineId}, got {stageDef.bloodline_id}."
                );
            }
        }
    }

    private void _append_bloodline_stage_phase2_errors(
        List<string> errors,
        StringName stageId,
        BloodlineStageDef stageDef
    )
    {
        if (stageDef == null)
        {
            return;
        }
        string ownerLabel = $"BloodlineStage {stageId}";
        _append_trait_reference_errors(errors, ownerLabel, stageDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.racial_granted_skills,
            "bloodline"
        );
        if (stageDef.bloodline_id == "")
        {
            return;
        }
        if (!_bloodlineDefIndex.TryGetValue(stageDef.bloodline_id, out BloodlineDef bloodlineDef))
        {
            errors.Add($"{ownerLabel} references missing bloodline {stageDef.bloodline_id}.");
            return;
        }
        if (!bloodlineDef.stage_ids.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} bloodline {stageDef.bloodline_id} must list this stage in stage_ids."
            );
        }
    }

    private void _append_ascension_phase2_errors(
        List<string> errors,
        StringName ascensionId,
        AscensionDef ascensionDef
    )
    {
        if (ascensionDef == null)
        {
            return;
        }
        string ownerLabel = $"Ascension {ascensionId}";
        _append_trait_reference_errors(errors, ownerLabel, ascensionDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.racial_granted_skills,
            "ascension"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_race_ids,
            "allowed_race_ids",
            _raceDefIndex,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_subrace_ids,
            "allowed_subrace_ids",
            _subraceDefIndex,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.allowed_bloodline_ids,
            "allowed_bloodline_ids",
            _bloodlineDefIndex,
            "bloodline"
        );

        foreach (StringName stageId in ascensionDef.stage_ids)
        {
            if (stageId == "")
            {
                continue;
            }
            if (!_ascensionStageDefIndex.TryGetValue(stageId, out AscensionStageDef stageDef))
            {
                errors.Add($"{ownerLabel} references missing ascension_stage {stageId}.");
                continue;
            }
            if (stageDef.ascension_id != ascensionId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} ascension_id must be {ascensionId}, got {stageDef.ascension_id}."
                );
            }
        }
    }

    private void _append_ascension_stage_phase2_errors(
        List<string> errors,
        StringName stageId,
        AscensionStageDef stageDef
    )
    {
        if (stageDef == null)
        {
            return;
        }
        string ownerLabel = $"AscensionStage {stageId}";
        _append_body_size_category_error(
            errors,
            ownerLabel,
            "body_size_category_override",
            stageDef.body_size_category_override,
            true
        );
        _append_trait_reference_errors(errors, ownerLabel, stageDef.trait_ids, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.racial_granted_skills,
            "ascension"
        );
        if (stageDef.ascension_id == "")
        {
            return;
        }
        if (!_ascensionDefIndex.TryGetValue(stageDef.ascension_id, out AscensionDef ascensionDef))
        {
            errors.Add($"{ownerLabel} references missing ascension {stageDef.ascension_id}.");
            return;
        }
        if (!ascensionDef.stage_ids.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} ascension {stageDef.ascension_id} must list this stage in stage_ids."
            );
        }
    }

    private void _append_stage_advancement_phase2_errors(
        List<string> errors,
        StringName modifierId,
        StageAdvancementModifier modifier
    )
    {
        if (modifier == null)
        {
            return;
        }
        string ownerLabel = $"StageAdvancement {modifierId}";
        if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Unknown)
        {
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.target_axis}.");
        }
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_race_ids,
            "applies_to_race_ids",
            _raceDefIndex,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_subrace_ids,
            "applies_to_subrace_ids",
            _subraceDefIndex,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_bloodline_ids,
            "applies_to_bloodline_ids",
            _bloodlineDefIndex,
            "bloodline"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.applies_to_ascension_ids,
            "applies_to_ascension_ids",
            _ascensionDefIndex,
            "ascension"
        );
        _append_stage_advancement_max_stage_error(errors, ownerLabel, modifier);
    }

    private void _append_stage_advancement_max_stage_error(
        List<string> errors,
        string ownerLabel,
        StageAdvancementModifier modifier
    )
    {
        if (modifier.max_stage_id == "")
        {
            return;
        }
        if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Bloodline)
        {
            if (!_bloodlineStageDefIndex.ContainsKey(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing bloodline_stage {modifier.max_stage_id}."
                );
            }
        }
        else if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Divine)
        {
            if (!_ascensionStageDefIndex.ContainsKey(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing ascension_stage {modifier.max_stage_id}."
                );
            }
        }
        else
        {
            HashSet<StringName> knownStageIds = _collect_known_identity_stage_ids();
            if (!knownStageIds.Contains(modifier.max_stage_id))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing stage {modifier.max_stage_id}."
                );
            }
        }
    }

    private void _append_global_stage_id_errors(List<string> errors)
    {
        var stageSources = new Dictionary<StringName, string>();
        foreach (StringName stageId in SortedKeys(_bloodlineStageDefIndex))
        {
            _append_global_stage_id(errors, stageSources, stageId, "bloodline_stage");
        }
        foreach (StringName stageId in SortedKeys(_ascensionStageDefIndex))
        {
            _append_global_stage_id(errors, stageSources, stageId, "ascension_stage");
        }
    }

    private static void _append_global_stage_id(
        List<string> errors,
        Dictionary<StringName, string> stageSources,
        StringName stageId,
        string stageSource
    )
    {
        if (stageId == "")
        {
            return;
        }
        if (stageSources.TryGetValue(stageId, out string existingSource))
        {
            errors.Add(
                $"Stage id {stageId} must be globally unique across bloodline_stage and ascension_stage; declared by {existingSource} and {stageSource}."
            );
            return;
        }
        stageSources[stageId] = stageSource;
    }

    private void _append_trait_reference_errors(
        List<string> errors,
        string ownerLabel,
        GStringNameArray traitIds,
        string fieldLabel
    )
    {
        foreach (StringName traitId in traitIds)
        {
            if (traitId == "")
            {
                continue;
            }
            if (!_traitDefIndex.TryGetValue(traitId, out TraitDef traitDef))
            {
                errors.Add($"{ownerLabel} {fieldLabel} references missing trait {traitId}.");
                continue;
            }
            if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.Identity))
            {
                errors.Add(
                    $"{ownerLabel} {fieldLabel} references trait {traitId} that does not allow identity source."
                );
            }
        }
    }

    private void _append_racial_granted_skill_reference_errors(
        List<string> errors,
        string ownerLabel,
        System.Collections.IEnumerable grantedSkills,
        StringName expectedLearnSource
    )
    {
        int index = 0;
        foreach (Resource grantedSkillValue in grantedSkills)
        {
            var grantedSkill = grantedSkillValue as RacialGrantedSkill;
            if (grantedSkill == null || grantedSkill.skill_id == "")
            {
                index++;
                continue;
            }
            if (
                !_skillDefinitionIndex.TryGetValue(
                    grantedSkill.skill_id,
                    out SkillDefinition skillDefinition
                )
                || skillDefinition == null
            )
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] references missing skill {grantedSkill.skill_id}."
                );
                index++;
                continue;
            }
            if (skillDefinition.LearnSourceKind != SkillDefinition.ToLearnSource(expectedLearnSource))
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.skill_id} learn_source must be {expectedLearnSource}, got {skillDefinition.LearnSource}."
                );
            }
            if (grantedSkill.minimum_skill_level > skillDefinition.MaxLevel)
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.skill_id} minimum_skill_level must be <= max_level {skillDefinition.MaxLevel}."
                );
            }
            index++;
        }
    }

    private static void _append_id_reference_errors<T>(
        List<string> errors,
        string ownerLabel,
        GStringNameArray values,
        string fieldLabel,
        IReadOnlyDictionary<StringName, T> targetDefs,
        string targetLabel
    )
        where T : class
    {
        foreach (StringName valueId in values)
        {
            if (valueId == "")
            {
                continue;
            }
            if (!targetDefs.ContainsKey(valueId))
            {
                errors.Add(
                    $"{ownerLabel} {fieldLabel} references missing {targetLabel} {valueId}."
                );
            }
        }
    }

    private static void _append_damage_resistance_errors(
        List<string> errors,
        string ownerLabel,
        GDictionary damageResistances
    )
    {
        foreach (var keyValue in damageResistances.Keys)
        {
            if (keyValue.VariantType != Variant.Type.StringName)
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances key {keyValue} must be a non-empty StringName."
                );
                continue;
            }
            StringName damageTag = keyValue.AsStringName();
            if (damageTag == "")
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances key {keyValue} must be a non-empty StringName."
                );
                continue;
            }
            if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances references unsupported damage tag {damageTag}."
                );
            }
            Variant rawTier = damageResistances[keyValue];
            if (rawTier.VariantType != Variant.Type.StringName)
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances[{damageTag}] must be a non-empty StringName."
                );
                continue;
            }
            StringName mitigationTier = rawTier.AsStringName();
            if (mitigationTier == "")
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances[{damageTag}] must be a non-empty StringName."
                );
                continue;
            }
            if (
                DamageTagContentRules.ToMitigationTierKind(mitigationTier)
                == DamageMitigationTierKind.Unknown
            )
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances[{damageTag}] uses unsupported mitigation tier {mitigationTier}."
                );
            }
        }
    }

    private static void _append_body_size_category_error(
        List<string> errors,
        string ownerLabel,
        string fieldLabel,
        StringName category,
        bool allowEmpty
    )
    {
        if (category == "")
        {
            if (!allowEmpty)
            {
                errors.Add($"{ownerLabel} {fieldLabel} must be a non-empty body_size_category.");
            }
            return;
        }
        if (!BodySizeContentRules.IsValidBodySizeCategory(category))
        {
            errors.Add(
                $"{ownerLabel} {fieldLabel} uses unsupported body_size_category {category}."
            );
        }
    }

    private static HashSet<StringName> _collect_age_profile_stage_ids(AgeProfileDef profileDef)
    {
        var stageIds = new HashSet<StringName>();
        if (profileDef == null)
        {
            return stageIds;
        }
        foreach (AgeStageRule stageRule in profileDef.stage_rules)
        {
            if (stageRule != null && stageRule.stage_id != "")
            {
                stageIds.Add(stageRule.stage_id);
            }
        }
        return stageIds;
    }

    private HashSet<StringName> _collect_known_identity_stage_ids()
    {
        var stageIds = new HashSet<StringName>();
        foreach (AgeProfileDef profileDef in _ageProfileDefIndex.Values)
        {
            foreach (StringName stageId in _collect_age_profile_stage_ids(profileDef))
            {
                stageIds.Add(stageId);
            }
        }
        foreach (StringName stageId in _bloodlineStageDefIndex.Keys)
        {
            stageIds.Add(stageId);
        }
        foreach (StringName stageId in _ascensionStageDefIndex.Keys)
        {
            stageIds.Add(stageId);
        }
        return stageIds;
    }

    private static StringName _strict_to_string_name(object rawValue)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
        string normalizedText = normalized.ToString().StripEdges();
        return string.IsNullOrEmpty(normalizedText) ? new StringName("") : new StringName(normalizedText);
    }

    private static bool HasDictionary(GDictionary source, string key)
    {
        return TryGetValue(source, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary;
    }

    private void _append_invalid_skill_errors(
        GStringArray errors,
        StringName skillId,
        SkillDefinition skillDefinition
    )
    {
        if (skillDefinition == null)
        {
            return;
        }
        if (skillDefinition.SkillTypeKind == SkillTypeKind.Unknown)
        {
            errors.Add($"Skill {skillId} uses unsupported skill_type {skillDefinition.SkillType}.");
        }
        if (skillDefinition.LearnSourceKind == SkillLearnSourceKind.Unknown)
        {
            errors.Add($"Skill {skillId} uses unsupported learn_source {skillDefinition.LearnSource}.");
        }
        if (skillDefinition.UnlockModeKind == SkillUnlockMode.Unknown)
        {
            errors.Add($"Skill {skillId} uses unsupported unlock_mode {skillDefinition.UnlockMode}.");
        }
        if (skillDefinition.CoreSkillTransitionModeKind == CoreSkillTransitionMode.Unknown)
        {
            errors.Add(
                $"Skill {skillId} uses unsupported core_skill_transition_mode {skillDefinition.CoreSkillTransitionMode}."
            );
        }
        if (skillDefinition.MaxLevel < 0 && skillDefinition.DynamicMaxLevelStatId == "")
        {
            errors.Add($"Skill {skillId} must have max_level >= 0.");
        }
        if (skillDefinition.NonCoreMaxLevel < 0)
        {
            errors.Add($"Skill {skillId} non_core_max_level must be >= 0.");
        }
        if (
            skillDefinition.NonCoreMaxLevel > skillDefinition.MaxLevel
            && skillDefinition.MaxLevel >= 0
            && skillDefinition.DynamicMaxLevelStatId == ""
        )
        {
            errors.Add($"Skill {skillId} non_core_max_level must be <= max_level.");
        }
        if (
            skillDefinition.MasteryCurve.Count != skillDefinition.MaxLevel
            && skillDefinition.MaxLevel >= 0
            && skillDefinition.DynamicMaxLevelStatId == ""
        )
        {
            errors.Add($"Skill {skillId} mastery_curve size must match max_level.");
        }
        _append_dynamic_max_level_errors(errors, skillId, skillDefinition);
        _append_practice_skill_errors(errors, skillId, skillDefinition);
        _append_skill_attribute_growth_errors(errors, skillId, skillDefinition);
        _append_skill_requirement_errors(
            errors,
            skillId,
            skillDefinition.LearnRequirements,
            "learn_requirements"
        );
        _append_skill_level_requirement_errors(
            errors,
            skillId,
            skillDefinition.SkillLevelRequirements
        );
        _append_attribute_requirement_errors(
            errors,
            skillId,
            skillDefinition.AttributeRequirements
        );
        _append_skill_requirement_errors(
            errors,
            skillId,
            skillDefinition.UpgradeSourceSkillIds,
            "upgrade_source_skill_ids"
        );
        foreach (StringName achievementId in skillDefinition.AchievementRequirements)
        {
            if (achievementId == "")
            {
                errors.Add($"Skill {skillId} has an empty achievement requirement.");
            }
        }
        if (
            skillDefinition.UnlockModeKind == SkillUnlockMode.CompositeUpgrade
            && skillDefinition.UpgradeSourceSkillIds.Count == 0
        )
        {
            errors.Add(
                $"Skill {skillId} is composite_upgrade but missing upgrade_source_skill_ids."
            );
        }
    }

    private static void _append_practice_skill_errors(
        GStringArray errors,
        StringName skillId,
        SkillDefinition skillDefinition
    )
    {
        int trackCount = 0;
        foreach (StringName trackTag in PracticeTrackTags)
        {
            if (skillDefinition.HasTag(trackTag))
            {
                trackCount++;
            }
        }
        if (trackCount == 0)
        {
            if (skillDefinition.PracticeTierKind != SkillPracticeTierKind.None)
            {
                errors.Add(
                    $"Skill {skillId} practice_tier requires meditation or cultivation tag."
                );
            }
            return;
        }
        if (trackCount != 1)
        {
            errors.Add($"Skill {skillId} must use exactly one practice track tag.");
        }
        if (skillDefinition.Tags.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} practice tags must be exclusive; tags must contain only meditation or cultivation."
            );
        }
        if (
            skillDefinition.PracticeTierKind
            is SkillPracticeTierKind.None
                or SkillPracticeTierKind.Unknown
        )
        {
            errors.Add(
                $"Skill {skillId} practice_tier must be one of basic, intermediate, advanced, ultimate."
            );
        }
    }

    private static void _append_dynamic_max_level_errors(
        GStringArray errors,
        StringName skillId,
        SkillDefinition skillDefinition
    )
    {
        bool hasDynamicStat = skillDefinition.DynamicMaxLevelStatId != "";
        if (!hasDynamicStat)
        {
            if (skillDefinition.DynamicMaxLevelBase != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_base requires dynamic_max_level_stat_id."
                );
            }
            if (skillDefinition.DynamicMaxLevelPerStat != 0)
            {
                errors.Add(
                    $"Skill {skillId} dynamic_max_level_per_stat requires dynamic_max_level_stat_id."
                );
            }
            return;
        }
        if (skillDefinition.DynamicMaxLevelBase <= 0)
        {
            errors.Add($"Skill {skillId} dynamic_max_level_base must be >= 1.");
        }
        if (skillDefinition.DynamicMaxLevelPerStat == 0)
        {
            errors.Add(
                $"Skill {skillId} dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set."
            );
        }
    }

    private static void _append_skill_attribute_growth_errors(
        GStringArray errors,
        StringName skillId,
        SkillDefinition skillDefinition
    )
    {
        if (skillDefinition.AttributeGrowthProgress.Count == 0 && skillDefinition.GrowthTier == "")
        {
            return;
        }
        if (!AttributeGrowthContentRules.IsValidGrowthTier(skillDefinition.GrowthTier))
        {
            errors.Add($"Skill {skillId} uses unsupported growth_tier {skillDefinition.GrowthTier}.");
            return;
        }
        int progressTotal = 0;
        foreach (KeyValuePair<StringName, int> entry in skillDefinition.AttributeGrowthProgress)
        {
            StringName attributeId = entry.Key;
            int amount = entry.Value;
            if (!AttributeGrowthContentRules.IsValidAttributeId(attributeId))
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress references invalid attribute {attributeId}."
                );
            }
            if (amount <= 0)
            {
                errors.Add(
                    $"Skill {skillId} attribute_growth_progress for {attributeId} must be > 0."
                );
            }
            progressTotal += amount;
        }
        int expectedTotal = AttributeGrowthContentRules.GetTierBudget(skillDefinition.GrowthTier);
        if (progressTotal != expectedTotal)
        {
            errors.Add(
                $"Skill {skillId} attribute_growth_progress total must equal {expectedTotal} for growth_tier {skillDefinition.GrowthTier}."
            );
        }
    }

    private void _append_skill_requirement_errors(
        GStringArray errors,
        StringName skillId,
        IReadOnlyList<StringName> requirementIds,
        string contextLabel
    )
    {
        foreach (StringName requiredSkillId in requirementIds)
        {
            if (requiredSkillId == "")
            {
                errors.Add($"Skill {skillId} has an empty skill reference in {contextLabel}.");
                continue;
            }
            if (!_skillDefs.ContainsKey(requiredSkillId))
            {
                errors.Add(
                    $"Skill {skillId} references missing skill {requiredSkillId} in {contextLabel}."
                );
            }
        }
    }

    private void _append_skill_level_requirement_errors(
        GStringArray errors,
        StringName skillId,
        IReadOnlyDictionary<StringName, int> skillLevelRequirements
    )
    {
        foreach (KeyValuePair<StringName, int> entry in skillLevelRequirements)
        {
            StringName requiredSkillId = entry.Key;
            if (requiredSkillId == "")
            {
                errors.Add($"Skill {skillId} has an empty skill_id in skill_level_requirements.");
                continue;
            }
            if (!_skillDefs.ContainsKey(requiredSkillId))
            {
                errors.Add(
                    $"Skill {skillId} references missing skill {requiredSkillId} in skill_level_requirements."
                );
            }
            int requiredLevel = entry.Value;
            if (requiredLevel <= 0)
            {
                errors.Add(
                    $"Skill {skillId} requires non-positive level {requiredLevel} for {requiredSkillId} in skill_level_requirements."
                );
            }
        }
    }

    private static void _append_attribute_requirement_errors(
        GStringArray errors,
        StringName skillId,
        IReadOnlyDictionary<StringName, int> attributeRequirements
    )
    {
        foreach (KeyValuePair<StringName, int> entry in attributeRequirements)
        {
            StringName attributeId = entry.Key;
            if (attributeId == "")
            {
                errors.Add($"Skill {skillId} has an empty attribute_id in attribute_requirements.");
                continue;
            }
            if (!UnitBaseAttributes.IsBaseAttributeId(attributeId))
            {
                errors.Add(
                    $"Skill {skillId} references unsupported attribute {attributeId} in attribute_requirements."
                );
            }
            int requiredValue = entry.Value;
            if (requiredValue <= 0)
            {
                errors.Add(
                    $"Skill {skillId} requires non-positive value {requiredValue} for {attributeId} in attribute_requirements."
                );
            }
        }
    }

    private static void _append_raw_int_requirement_entry_errors(
        GStringArray errors,
        StringName skillId,
        IReadOnlyList<SkillDef.IntRequirementEntryData> entries,
        string contextLabel,
        string idLabel
    )
    {
        foreach (SkillDef.IntRequirementEntryData entry in entries ?? System.Array.Empty<SkillDef.IntRequirementEntryData>())
        {
            if (!entry.HasStringLikeKey)
            {
                errors.Add(
                    $"Skill {skillId} has a non-string {idLabel} key {entry.RawKeyLabel} in {contextLabel}."
                );
                continue;
            }
            if (!entry.HasNonEmptyKey)
            {
                errors.Add($"Skill {skillId} has an empty {idLabel} in {contextLabel}.");
                continue;
            }
            if (!entry.HasStrictIntAmount)
            {
                errors.Add(
                    $"Skill {skillId} requires integer value for {entry.RequirementId} in {contextLabel}."
                );
            }
        }
    }

    private void _append_invalid_achievement_errors(
        List<string> errors,
        StringName achievementId,
        AchievementDef achievementDef
    )
    {
        if (achievementDef == null)
        {
            return;
        }
        if (achievementDef.event_type == "")
        {
            errors.Add($"Achievement {achievementId} is missing event_type.");
        }
        if (achievementDef.threshold <= 0)
        {
            errors.Add($"Achievement {achievementId} must have a positive threshold.");
        }
        foreach (AchievementRewardDef reward in achievementDef.rewards)
        {
            if (reward == null)
                continue;
            if (reward.reward_type == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without reward_type.");
            }
            if (reward.target_id == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without target_id.");
            }
            if (reward.amount == 0)
            {
                errors.Add(
                    $"Achievement {achievementId} has a zero-amount reward for {reward.target_id}."
                );
            }
            if (
                reward.reward_type != ""
                && !PendingCharacterRewardContentRules.IsSupportedEntryType(reward.reward_type)
            )
            {
                errors.Add(
                    $"Achievement {achievementId} uses unsupported reward_type {reward.reward_type}."
                );
                continue;
            }
            if (
                reward.RewardKind == PendingCharacterRewardEntryKind.SkillUnlock
                || reward.RewardKind == PendingCharacterRewardEntryKind.SkillMastery
            )
            {
                if (!_skillDefs.ContainsKey(reward.target_id))
                {
                    errors.Add(
                        $"Achievement {achievementId} references missing skill {reward.target_id}."
                    );
                }
            }
            else if (reward.reward_type == "attribute_progress")
            {
                if (
                    !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                        reward.target_id
                    )
                )
                {
                    errors.Add(
                        $"Achievement {achievementId} attribute_progress reward references unsupported attribute {reward.target_id}."
                    );
                }
            }
        }
    }

    private static GDictionary DuplicateDictionary(GDictionary source, string reason)
    {
        return RegisterDefinitionBucket(
            source != null ? source.Duplicate() : new GDictionary(),
            $"DuplicateDictionary:{reason}"
        );
    }

    private void SyncTypedDefinitionIndexes()
    {
        if (!_typedIndexesDirty)
        {
            return;
        }
        _typedIndexesDirty = false;
        _skillDefinitionIndex.Clear();
        foreach (
            (StringName skillId, SkillDefinition skillDefinition) in SkillDefinition.ProjectIndex(
                BuildSkillDefIndex()
            )
        )
        {
            _skillDefinitionIndex[skillId] = skillDefinition;
        }
        ReplaceTypedIndex(_professionDefIndex, _professionDefs);
        ReplaceAchievementTypedIndex(_achievementDefIndex, _achievementDefs);
        ReplaceTypedIndex(_questDefIndex, _questDefs);
        ReplaceTypedIndex(_raceDefIndex, _raceDefs);
        ReplaceTypedIndex(_subraceDefIndex, _subraceDefs);
        ReplaceTypedIndex(_traitDefIndex, _traitDefs);
        ReplaceTypedIndex(_ageProfileDefIndex, _ageProfileDefs);
        ReplaceTypedIndex(_bloodlineDefIndex, _bloodlineDefs);
        ReplaceTypedIndex(_bloodlineStageDefIndex, _bloodlineStageDefs);
        ReplaceTypedIndex(_ascensionDefIndex, _ascensionDefs);
        ReplaceTypedIndex(_ascensionStageDefIndex, _ascensionStageDefs);
        ReplaceTypedIndex(_stageAdvancementDefIndex, _stageAdvancementDefs);
    }

    private Dictionary<StringName, SkillDef> BuildSkillDefIndex()
    {
        var result = new Dictionary<StringName, SkillDef>();
        foreach (Variant rawKey in _skillDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }

            StringName skillId = rawKey.AsStringName();
            if (skillId == "")
            {
                continue;
            }

            if (_skillDefs[rawKey].AsGodotObject() is SkillDef skillDef)
            {
                result[skillId] = skillDef;
            }
        }
        return result;
    }

    private bool TryGetSkillDef(StringName skillId, out SkillDef skillDef)
    {
        skillDef = null;
        if (skillId == "" || !_skillDefs.ContainsKey(skillId))
        {
            return false;
        }

        skillDef = _skillDefs[skillId].AsGodotObject() as SkillDef;
        return skillDef != null;
    }

    private static Dictionary<StringName, T> CloneTypedDictionary<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        return new Dictionary<StringName, T>(source);
    }

    private static IReadOnlySet<StringName> ReadOnlyKeySet<T>(
        IReadOnlyDictionary<StringName, T> source
    )
    {
        if (source == null || source.Count == 0)
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        return EquipmentAbilityReadOnlySet<StringName>.From(source.Keys);
    }

    private static GDictionary ProjectTypedDictionary<T>(IReadOnlyDictionary<StringName, T> source)
        where T : Resource
    {
        var result = new GDictionary();
        if (source == null)
        {
            return RegisterDefinitionBucket(
                result,
                $"ProjectTypedDictionary:{typeof(T).Name}:empty"
            );
        }
        foreach ((StringName key, T value) in source)
        {
            if (key == "" || value == null)
            {
                continue;
            }
            result[key] = value;
        }
        return RegisterDefinitionBucket(result, $"ProjectTypedDictionary:{typeof(T).Name}");
    }

    private static GDictionary ProjectTraitDefs(IReadOnlyList<TraitDef> source)
    {
        var result = new GDictionary();
        if (source == null)
            return RegisterDefinitionBucket(result, "ProjectTraitDefs:empty");
        foreach (TraitDef traitDef in source)
        {
            if (traitDef == null || traitDef.trait_id == "")
                continue;
            result[traitDef.trait_id] = traitDef;
        }
        return RegisterDefinitionBucket(result, "ProjectTraitDefs");
    }

    private static GDictionary RegisterDefinitionBucket(GDictionary bucket, string reason)
    {
        bucket ??= new GDictionary();
        GodotContentOwnership.RegisterDerivedWrapper(
            bucket,
            $"ProgressionContentRegistry.definition_bucket:{reason}:{RuntimeHelpers.GetHashCode(bucket)}",
            "ProgressionContentRegistry.RegisterDefinitionBucket"
        );
        return bucket;
    }

    private static void ReplaceTypedIndex<T>(Dictionary<StringName, T> target, GDictionary source)
        where T : class
    {
        target.Clear();
        if (source == null)
        {
            return;
        }
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName key = rawKey.AsStringName();
            if (key == "")
            {
                continue;
            }
            Variant value = source[rawKey];
            if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is T typedValue)
            {
                target[key] = typedValue;
            }
        }
    }

    private static void ReplaceAchievementTypedIndex(
        Dictionary<StringName, AchievementDef> target,
        GDictionary source
    )
    {
        target.Clear();
        if (source == null)
        {
            return;
        }
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName key = rawKey.AsStringName();
            if (key == "")
            {
                continue;
            }
            Variant value = source[rawKey];
            if (value.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            AchievementDef achievementDef = AchievementDef.FromDictionary(value.AsGodotDictionary());
            if (achievementDef == null || achievementDef.achievement_id == "")
            {
                continue;
            }
            target[key] = achievementDef;
        }
    }

    private static List<StringName> SortedKeys<T>(IReadOnlyDictionary<StringName, T> source)
    {
        var result = new List<StringName>(source.Count);
        foreach (StringName key in source.Keys)
        {
            result.Add(key);
        }
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static GStringArray DuplicateStringArray(GStringArray source)
    {
        var result = new GStringArray();
        if (source == null)
        {
            return result;
        }
        foreach (string value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(GStringNameArray source)
    {
        var result = new GStringNameArray();
        if (source == null)
        {
            return result;
        }
        foreach (StringName value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static void AppendArray(GStringArray target, GStringArray source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string value in source)
        {
            target.Add(value);
        }
    }

    private static void AppendUniqueErrors(GStringArray target, GStringArray source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string value in source)
        {
            if (!target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    private static void AppendUniqueErrors(List<string> target, IEnumerable<string> source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string value in source)
        {
            if (!target.Contains(value))
            {
                target.Add(value);
            }
        }
    }

    private static GStringArray ToGodotStringArray(IEnumerable<string> source)
    {
        var result = new GStringArray();
        if (source == null)
        {
            return result;
        }
        foreach (string value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static GDictionary GetDictionary(GDictionary source, string key)
    {
        if (
            !TryGetValue(source, key, out Variant value)
            || value.VariantType != Variant.Type.Dictionary
        )
        {
            return new GDictionary();
        }
        return value.AsGodotDictionary();
    }

    private static T GetObject<T>(GDictionary source, StringName key)
        where T : class
    {
        if (
            !TryGetValue(source, key, out Variant value)
            || value.VariantType != Variant.Type.Object
        )
        {
            return null;
        }
        return value.AsGodotObject() as T;
    }

    private static IEnumerable<T> ReadObjectItems<T>(GArray values)
        where T : class
    {
        if (values == null)
        {
            yield break;
        }
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is T typedValue)
            {
                yield return typedValue;
            }
        }
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                yield return value.AsGodotDictionary();
            }
        }
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = ToVariantKey(key);
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static Variant ToVariantKey(object key)
    {
        return key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            bool boolValue => Variant.From(boolValue),
            Vector2I coord => Variant.From(coord),
            _ => Variant.From(key?.ToString() ?? ""),
        };
    }
}
