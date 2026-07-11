using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

internal sealed class ProgressionDefinitionSources
{
    public IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, ProfessionDefinition> ProfessionDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, AchievementDefinition> AchievementDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, QuestDefinition> QuestDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, ContingencySetupTemplateDefinition> ContingencyDefinitions
    {
        get;
        init;
    }
    public IReadOnlyDictionary<StringName, RaceDefinition> RaceDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, SubraceDefinition> SubraceDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, TraitDefinition> TraitDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, AgeProfileDefinition> AgeProfileDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, BloodlineDefinition> BloodlineDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, BloodlineStageDefinition> BloodlineStageDefinitions
    {
        get;
        init;
    }
    public IReadOnlyDictionary<StringName, AscensionDefinition> AscensionDefinitions { get; init; }
    public IReadOnlyDictionary<StringName, AscensionStageDefinition> AscensionStageDefinitions
    {
        get;
        init;
    }
    public IReadOnlyDictionary<StringName, StageAdvancementDefinition> StageAdvancementDefinitions
    {
        get;
        init;
    }
}

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
    private readonly Dictionary<StringName, SkillDefinition> _skillDefinitionIndex = new();
    private readonly Dictionary<StringName, ProfessionDefinition> _professionDefIndex = new();
    private readonly Dictionary<StringName, AchievementDefinition> _achievementDefIndex = new();
    private readonly Dictionary<StringName, QuestDefinition> _questDefIndex = new();
    private readonly Dictionary<StringName, ContingencySetupTemplateDefinition> _contingencyDefIndex =
        new();
    private readonly Dictionary<StringName, RaceDefinition> _raceDefIndex = new();
    private readonly Dictionary<StringName, SubraceDefinition> _subraceDefIndex = new();
    private readonly Dictionary<StringName, TraitDefinition> _traitDefIndex = new();
    private readonly Dictionary<StringName, AgeProfileDefinition> _ageProfileDefIndex = new();
    private readonly Dictionary<StringName, BloodlineDefinition> _bloodlineDefIndex = new();
    private readonly Dictionary<StringName, BloodlineStageDefinition> _bloodlineStageDefIndex = new();
    private readonly Dictionary<StringName, AscensionDefinition> _ascensionDefIndex = new();
    private readonly Dictionary<StringName, AscensionStageDefinition> _ascensionStageDefIndex = new();
    private readonly Dictionary<StringName, StageAdvancementDefinition> _stageAdvancementDefIndex =
        new();

    private readonly IContentResourceLoader _resourceLoader;
    private readonly SkillContentRegistry _skillContentRegistry;
    private readonly ProfessionContentRegistry _professionContentRegistry;
    private readonly RaceContentRegistry _raceContentRegistry;
    private readonly SubraceContentRegistry _subraceContentRegistry;
    private readonly TraitContentRegistry _traitContentRegistry;
    private readonly AgeContentRegistry _ageContentRegistry;
    private readonly BloodlineContentRegistry _bloodlineContentRegistry;
    private readonly AscensionContentRegistry _ascensionContentRegistry;
    private readonly StageAdvancementContentRegistry _stageAdvancementContentRegistry;
    private readonly QuestContentRegistry _questContentRegistry;
    private readonly ContingencyTemplateContentRegistry _contingencyTemplateContentRegistry;
    private readonly EquipmentAbilityContentRegistry _equipmentAbilityContentRegistry;

    private GStringArray _validationErrors = new();
    private readonly List<string> _questRegistrationErrors = new();
    private bool _usesReplacementDefinitionsForValidation;
    private bool _disposed;
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

    internal ProgressionContentRegistry(IContentResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader
            ?? throw new System.ArgumentNullException(nameof(resourceLoader));
        _skillContentRegistry = new SkillContentRegistry(_resourceLoader);
        _professionContentRegistry = new ProfessionContentRegistry(_resourceLoader);
        _raceContentRegistry = new RaceContentRegistry(_resourceLoader);
        _subraceContentRegistry = new SubraceContentRegistry(_resourceLoader);
        _traitContentRegistry = new TraitContentRegistry(_resourceLoader);
        _ageContentRegistry = new AgeContentRegistry(_resourceLoader);
        _bloodlineContentRegistry = new BloodlineContentRegistry(_resourceLoader);
        _ascensionContentRegistry = new AscensionContentRegistry(_resourceLoader);
        _stageAdvancementContentRegistry = new StageAdvancementContentRegistry(_resourceLoader);
        _questContentRegistry = new QuestContentRegistry(_resourceLoader);
        _contingencyTemplateContentRegistry = new ContingencyTemplateContentRegistry(
            _resourceLoader
        );
        _equipmentAbilityContentRegistry = new EquipmentAbilityContentRegistry(_resourceLoader);
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
        _skillDefs = _skillContentRegistry.DuplicateSkillResourceBucketForProgressionRegistry();
        ReplaceDefinitionIndex(
            _skillDefinitionIndex,
            _skillContentRegistry.GetSkillDefinitionsTyped()
        );
        AppendArray(_validationErrors, _skillContentRegistry.Validate());

        _professionContentRegistry.Setup(_skillContentRegistry.GetSkillDefinitionsTyped());
        ReplaceDefinitionIndex(
            _professionDefIndex,
            _professionContentRegistry.GetProfessionDefsTyped()
        );

        _raceContentRegistry.Rebuild();
        ReplaceDefinitionIndex(_raceDefIndex, _raceContentRegistry.GetRaceDefsTyped());
        _subraceContentRegistry.Rebuild();
        ReplaceDefinitionIndex(_subraceDefIndex, _subraceContentRegistry.GetSubraceDefsTyped());
        _traitContentRegistry.Rebuild();
        ReplaceDefinitionIndex(_traitDefIndex, _traitContentRegistry.GetTraitDefsTyped());
        _ageContentRegistry.Rebuild();
        ReplaceDefinitionIndex(_ageProfileDefIndex, _ageContentRegistry.GetAgeProfileDefsTyped());
        _bloodlineContentRegistry.Rebuild();
        ReplaceDefinitionIndex(
            _bloodlineDefIndex,
            _bloodlineContentRegistry.GetBloodlineDefsTyped()
        );
        ReplaceDefinitionIndex(
            _bloodlineStageDefIndex,
            _bloodlineContentRegistry.GetBloodlineStageDefsTyped()
        );
        _ascensionContentRegistry.Rebuild();
        ReplaceDefinitionIndex(
            _ascensionDefIndex,
            _ascensionContentRegistry.GetAscensionDefsTyped()
        );
        ReplaceDefinitionIndex(
            _ascensionStageDefIndex,
            _ascensionContentRegistry.GetAscensionStageDefsTyped()
        );
        _stageAdvancementContentRegistry.Rebuild();
        ReplaceDefinitionIndex(
            _stageAdvancementDefIndex,
            _stageAdvancementContentRegistry.GetStageAdvancementDefsTyped()
        );

        _questContentRegistry.Rebuild();
        foreach (string error in _questContentRegistry.GetValidationErrors())
            _validationErrors.Add(error);
        foreach (QuestDefinition questDef in _questContentRegistry.GetQuestDefsTyped().Values)
            _register_quest(questDef);
        _contingencyTemplateContentRegistry.Rebuild();
        foreach (string error in _contingencyTemplateContentRegistry.GetValidationErrors())
            _validationErrors.Add(error);
        ReplaceDefinitionIndex(
            _contingencyDefIndex,
            _contingencyTemplateContentRegistry.GetTemplateDefsTyped()
        );
        _register_seed_achievements();

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
        return CloneTypedDictionary(_skillDefinitionIndex);
    }

    internal GDictionary DuplicateSkillResourceBucketForValidation()
    {
        return DuplicateDictionary(_skillDefs);
    }

    internal IReadOnlyList<Resource> GetLoadedSkillResourcesForFinalizerDrain()
    {
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

    public IReadOnlyDictionary<StringName, ProfessionDefinition> GetProfessionDefsTyped()
    {
        return CloneTypedDictionary(_professionDefIndex);
    }

    public IReadOnlyDictionary<StringName, AchievementDefinition> GetAchievementDefsTyped()
    {
        return CloneTypedDictionary(_achievementDefIndex);
    }

    public IReadOnlyDictionary<StringName, QuestDefinition> GetQuestDefsTyped()
    {
        return CloneTypedDictionary(_questDefIndex);
    }

    public IReadOnlyList<string> GetQuestRegistrationErrorsTyped()
    {
        return new List<string>(_questRegistrationErrors);
    }

    public IReadOnlyDictionary<
        StringName,
        ContingencySetupTemplateDefinition
    > GetContingencySetupTemplatesTyped()
    {
        return CloneTypedDictionary(_contingencyDefIndex);
    }

    public IReadOnlyDictionary<StringName, RaceDefinition> GetRaceDefsTyped()
    {
        return CloneTypedDictionary(_raceDefIndex);
    }

    public IReadOnlyDictionary<StringName, SubraceDefinition> GetSubraceDefsTyped()
    {
        return CloneTypedDictionary(_subraceDefIndex);
    }

    public IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefsTyped()
    {
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

    public IReadOnlyDictionary<StringName, AgeProfileDefinition> GetAgeProfileDefsTyped()
    {
        return CloneTypedDictionary(_ageProfileDefIndex);
    }

    public IReadOnlyDictionary<StringName, BloodlineDefinition> GetBloodlineDefsTyped()
    {
        return CloneTypedDictionary(_bloodlineDefIndex);
    }

    public IReadOnlyDictionary<StringName, BloodlineStageDefinition> GetBloodlineStageDefsTyped()
    {
        return CloneTypedDictionary(_bloodlineStageDefIndex);
    }

    public IReadOnlyDictionary<StringName, AscensionDefinition> GetAscensionDefsTyped()
    {
        return CloneTypedDictionary(_ascensionDefIndex);
    }

    public IReadOnlyDictionary<StringName, AscensionStageDefinition> GetAscensionStageDefsTyped()
    {
        return CloneTypedDictionary(_ascensionStageDefIndex);
    }

    public IReadOnlyDictionary<
        StringName,
        StageAdvancementDefinition
    > GetStageAdvancementDefsTyped()
    {
        return CloneTypedDictionary(_stageAdvancementDefIndex);
    }

    public ProgressionIdentityCatalogData GetIdentityCatalogTyped()
    {
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
        var errors = new List<string>();
        AppendUniqueErrors(errors, _validationErrors);
        AppendUniqueErrors(errors, CollectValidationErrorsTyped());
        return errors;
    }

    internal void ReplaceDefinitionsForValidation(ProgressionDefinitionSources sources)
    {
        System.ArgumentNullException.ThrowIfNull(sources);
        _validationErrors.Clear();
        _questRegistrationErrors.Clear();
        _skillDefs.Clear();
        _usesReplacementDefinitionsForValidation = true;
        ReplaceDefinitionIndex(_skillDefinitionIndex, sources.SkillDefinitions);
        ReplaceDefinitionIndex(_professionDefIndex, sources.ProfessionDefinitions);
        ReplaceDefinitionIndex(_achievementDefIndex, sources.AchievementDefinitions);
        ReplaceDefinitionIndex(_questDefIndex, sources.QuestDefinitions);
        ReplaceDefinitionIndex(_contingencyDefIndex, sources.ContingencyDefinitions);
        ReplaceDefinitionIndex(_raceDefIndex, sources.RaceDefinitions);
        ReplaceDefinitionIndex(_subraceDefIndex, sources.SubraceDefinitions);
        ReplaceDefinitionIndex(_traitDefIndex, sources.TraitDefinitions);
        ReplaceDefinitionIndex(_ageProfileDefIndex, sources.AgeProfileDefinitions);
        ReplaceDefinitionIndex(_bloodlineDefIndex, sources.BloodlineDefinitions);
        ReplaceDefinitionIndex(
            _bloodlineStageDefIndex,
            sources.BloodlineStageDefinitions
        );
        ReplaceDefinitionIndex(_ascensionDefIndex, sources.AscensionDefinitions);
        ReplaceDefinitionIndex(
            _ascensionStageDefIndex,
            sources.AscensionStageDefinitions
        );
        ReplaceDefinitionIndex(
            _stageAdvancementDefIndex,
            sources.StageAdvancementDefinitions
        );
    }

    internal void ReplaceSkillAuthoringResourcesForValidation(GDictionary skillResources)
    {
        _skillDefs = DuplicateDictionary(skillResources);
        ReplaceDefinitionIndex(
            _skillDefinitionIndex,
            SkillDefinition.ProjectIndex(BuildSkillDefIndex())
        );
    }

    public GStringArray CollectValidationErrors()
    {
        return ToGodotStringArray(CollectValidationErrorsTyped());
    }

    public void AppendIdentityPhase2ValidationErrors(GStringArray errors)
    {
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

        if (_usesReplacementDefinitionsForValidation)
        {
            AppendUniqueErrors(
                errors,
                ProfessionContentRegistry.ValidateDefinitions(
                    _professionDefIndex,
                    _skillDefinitionIndex
                )
            );
            AppendUniqueErrors(
                errors,
                TraitContentRegistry.ValidateDefinitions(_traitDefIndex)
            );
        }

        foreach (StringName achievementId in SortedKeys(_achievementDefIndex))
        {
            _achievementDefIndex.TryGetValue(
                achievementId,
                out AchievementDefinition achievementDef
            );
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
            _raceDefIndex.TryGetValue(raceId, out RaceDefinition raceDef);
            _append_race_phase2_errors(errors, raceId, raceDef);
        }
        foreach (StringName subraceId in SortedKeys(_subraceDefIndex))
        {
            _subraceDefIndex.TryGetValue(subraceId, out SubraceDefinition subraceDef);
            _append_subrace_phase2_errors(
                errors,
                subraceId,
                subraceDef
            );
        }
        foreach (StringName profileId in SortedKeys(_ageProfileDefIndex))
        {
            _ageProfileDefIndex.TryGetValue(profileId, out AgeProfileDefinition profileDef);
            _append_age_profile_phase2_errors(
                errors,
                profileId,
                profileDef
            );
        }
        foreach (StringName bloodlineId in SortedKeys(_bloodlineDefIndex))
        {
            _bloodlineDefIndex.TryGetValue(
                bloodlineId,
                out BloodlineDefinition bloodlineDef
            );
            _append_bloodline_phase2_errors(
                errors,
                bloodlineId,
                bloodlineDef
            );
        }
        foreach (StringName stageId in SortedKeys(_bloodlineStageDefIndex))
        {
            _bloodlineStageDefIndex.TryGetValue(
                stageId,
                out BloodlineStageDefinition stageDef
            );
            _append_bloodline_stage_phase2_errors(
                errors,
                stageId,
                stageDef
            );
        }
        foreach (StringName ascensionId in SortedKeys(_ascensionDefIndex))
        {
            _ascensionDefIndex.TryGetValue(
                ascensionId,
                out AscensionDefinition ascensionDef
            );
            _append_ascension_phase2_errors(
                errors,
                ascensionId,
                ascensionDef
            );
        }
        foreach (StringName stageId in SortedKeys(_ascensionStageDefIndex))
        {
            _ascensionStageDefIndex.TryGetValue(
                stageId,
                out AscensionStageDefinition stageDef
            );
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
                out StageAdvancementDefinition modifier
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
        _questRegistrationErrors.Clear();
        _skillDefinitionIndex.Clear();
        _professionDefIndex.Clear();
        _achievementDefIndex.Clear();
        _questDefIndex.Clear();
        _contingencyDefIndex.Clear();
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
        _usesReplacementDefinitionsForValidation = false;
    }

    private IReadOnlyList<EquipmentAbilityContentPackDef> LoadEquipmentAbilityContentPacks()
    {
        var packs = new List<EquipmentAbilityContentPackDef>();
        string globalPath = ProjectSettings.GlobalizePath(EquipmentAbilityConfigDirectory);
        if (!DirAccess.DirExistsAbsolute(globalPath))
            return packs;

        ScanEquipmentAbilityContentDirectory(EquipmentAbilityConfigDirectory, packs);
        return packs;
    }

    private void ScanEquipmentAbilityContentDirectory(
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

                Resource resource = _resourceLoader.LoadCanonical<Resource>(entryPath);
                if (resource is not EquipmentAbilityContentPackDef pack)
                    continue;
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

    private AchievementDefinition _build_achievement(
        StringName achievementId,
        string displayName,
        string description,
        StringName eventType,
        StringName subjectId,
        int threshold,
        params AchievementRewardDefinition[] rewards
    )
    {
        return new AchievementDefinition(
            achievementId,
            displayName,
            description,
            eventType,
            subjectId,
            threshold,
            rewards ?? System.Array.Empty<AchievementRewardDefinition>()
        );
    }

    private AchievementRewardDefinition _build_achievement_reward(
        StringName rewardType,
        StringName targetId,
        string targetLabel,
        int amount,
        string reasonText = ""
    )
    {
        return new AchievementRewardDefinition(
            rewardType,
            targetId,
            targetLabel,
            amount,
            reasonText
        );
    }

    private void _register_achievement(AchievementDefinition achievementDef)
    {
        if (achievementDef == null || achievementDef.AchievementId == "")
        {
            _validationErrors.Add(
                "Encountered an achievement definition without an achievement_id."
            );
            return;
        }
        if (_achievementDefIndex.ContainsKey(achievementDef.AchievementId))
        {
            _validationErrors.Add(
                $"Duplicate achievement_id registered: {achievementDef.AchievementId}"
            );
            return;
        }
        _achievementDefIndex[achievementDef.AchievementId] = achievementDef;
    }

    private void _register_quest(QuestDefinition questDef)
    {
        if (questDef == null || questDef.QuestId == "")
        {
            _questRegistrationErrors.Add("Encountered a quest definition without a quest_id.");
            return;
        }
        if (_questDefIndex.ContainsKey(questDef.QuestId))
        {
            _questRegistrationErrors.Add($"Duplicate quest_id registered: {questDef.QuestId}");
            return;
        }

        _questDefIndex[questDef.QuestId] = questDef;
    }

    private void _append_race_phase2_errors(
        List<string> errors,
        StringName raceId,
        RaceDefinition raceDef
    )
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
            raceDef.BodySizeCategory,
            false
        );
        _append_damage_resistance_errors(errors, ownerLabel, raceDef.DamageResistances);
        _append_trait_reference_errors(errors, ownerLabel, raceDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            raceDef.RacialGrantedSkills,
            "race"
        );

        if (
            raceDef.AgeProfileId != ""
            && !_ageProfileDefIndex.ContainsKey(raceDef.AgeProfileId)
        )
        {
            errors.Add($"{ownerLabel} references missing age_profile {raceDef.AgeProfileId}.");
        }

        if (raceDef.DefaultSubraceId != "")
        {
            if (!_subraceDefIndex.ContainsKey(raceDef.DefaultSubraceId))
            {
                errors.Add(
                    $"{ownerLabel} references missing default_subrace {raceDef.DefaultSubraceId}."
                );
            }
            else if (!raceDef.SubraceIds.Contains(raceDef.DefaultSubraceId))
            {
                errors.Add(
                    $"{ownerLabel} default_subrace {raceDef.DefaultSubraceId} must be listed in subrace_ids."
                );
            }
        }

        foreach (StringName subraceId in raceDef.SubraceIds)
        {
            if (subraceId == "")
            {
                continue;
            }
            if (!_subraceDefIndex.TryGetValue(subraceId, out SubraceDefinition subraceDef))
            {
                errors.Add($"{ownerLabel} references missing subrace {subraceId}.");
                continue;
            }
            if (subraceDef.ParentRaceId != raceId)
            {
                errors.Add(
                    $"{ownerLabel} subrace {subraceId} parent_race_id must be {raceId}, got {subraceDef.ParentRaceId}."
                );
            }
        }
    }

    private void _append_subrace_phase2_errors(
        List<string> errors,
        StringName subraceId,
        SubraceDefinition subraceDef
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
            subraceDef.BodySizeCategoryOverride,
            true
        );
        _append_damage_resistance_errors(errors, ownerLabel, subraceDef.DamageResistances);
        _append_trait_reference_errors(errors, ownerLabel, subraceDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            subraceDef.RacialGrantedSkills,
            "subrace"
        );

        if (subraceDef.ParentRaceId == "")
        {
            return;
        }
        if (!_raceDefIndex.TryGetValue(subraceDef.ParentRaceId, out RaceDefinition parentRace))
        {
            errors.Add($"{ownerLabel} references missing parent_race {subraceDef.ParentRaceId}.");
            return;
        }
        if (!parentRace.SubraceIds.Contains(subraceId))
        {
            errors.Add(
                $"{ownerLabel} parent_race {subraceDef.ParentRaceId} must list this subrace in subrace_ids."
            );
        }
    }

    private void _append_age_profile_phase2_errors(
        List<string> errors,
        StringName profileId,
        AgeProfileDefinition profileDef
    )
    {
        if (profileDef == null)
        {
            return;
        }
        string ownerLabel = $"AgeProfile {profileId}";
        if (profileDef.RaceId != "")
        {
            if (!_raceDefIndex.TryGetValue(profileDef.RaceId, out RaceDefinition raceDef))
            {
                errors.Add($"{ownerLabel} references missing race {profileDef.RaceId}.");
            }
            else if (raceDef.AgeProfileId != profileId)
            {
                errors.Add(
                    $"{ownerLabel} race {profileDef.RaceId} must reference this profile as age_profile_id."
                );
            }
        }
        if (profileDef.StageRules.Count == 0)
        {
            errors.Add($"{ownerLabel} must declare at least one stage_rules entry.");
        }

        HashSet<StringName> stageIds = _collect_age_profile_stage_ids(profileDef);
        foreach (StringName stageId in profileDef.CreationStageIds)
        {
            if (stageId != "" && !stageIds.Contains(stageId))
            {
                errors.Add($"{ownerLabel} creation_stage_ids references missing stage {stageId}.");
            }
        }
        foreach (StringName stageId in profileDef.DefaultAgeByStage.Keys)
        {
            if (stageId != "" && !stageIds.Contains(stageId))
            {
                errors.Add(
                    $"{ownerLabel} default_age_by_stage references missing stage {stageId}."
                );
            }
        }
        foreach (AgeStageRuleDefinition stageRule in profileDef.StageRules)
        {
            if (stageRule == null)
            {
                continue;
            }
            _append_trait_reference_errors(
                errors,
                $"{ownerLabel} stage {stageRule.StageId}",
                stageRule.TraitIds,
                "trait_ids"
            );
        }
    }

    private void _append_bloodline_phase2_errors(
        List<string> errors,
        StringName bloodlineId,
        BloodlineDefinition bloodlineDef
    )
    {
        if (bloodlineDef == null)
        {
            return;
        }
        string ownerLabel = $"Bloodline {bloodlineId}";
        _append_trait_reference_errors(errors, ownerLabel, bloodlineDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            bloodlineDef.RacialGrantedSkills,
            "bloodline"
        );
        foreach (StringName stageId in bloodlineDef.StageIds)
        {
            if (stageId == "")
            {
                continue;
            }
            if (
                !_bloodlineStageDefIndex.TryGetValue(
                    stageId,
                    out BloodlineStageDefinition stageDef
                )
            )
            {
                errors.Add($"{ownerLabel} references missing bloodline_stage {stageId}.");
                continue;
            }
            if (stageDef.BloodlineId != bloodlineId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} bloodline_id must be {bloodlineId}, got {stageDef.BloodlineId}."
                );
            }
        }
    }

    private void _append_bloodline_stage_phase2_errors(
        List<string> errors,
        StringName stageId,
        BloodlineStageDefinition stageDef
    )
    {
        if (stageDef == null)
        {
            return;
        }
        string ownerLabel = $"BloodlineStage {stageId}";
        _append_trait_reference_errors(errors, ownerLabel, stageDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.RacialGrantedSkills,
            "bloodline"
        );
        if (stageDef.BloodlineId == "")
        {
            return;
        }
        if (
            !_bloodlineDefIndex.TryGetValue(
                stageDef.BloodlineId,
                out BloodlineDefinition bloodlineDef
            )
        )
        {
            errors.Add($"{ownerLabel} references missing bloodline {stageDef.BloodlineId}.");
            return;
        }
        if (!bloodlineDef.StageIds.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} bloodline {stageDef.BloodlineId} must list this stage in stage_ids."
            );
        }
    }

    private void _append_ascension_phase2_errors(
        List<string> errors,
        StringName ascensionId,
        AscensionDefinition ascensionDef
    )
    {
        if (ascensionDef == null)
        {
            return;
        }
        string ownerLabel = $"Ascension {ascensionId}";
        _append_trait_reference_errors(errors, ownerLabel, ascensionDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.RacialGrantedSkills,
            "ascension"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedRaceIds,
            "allowed_race_ids",
            _raceDefIndex,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedSubraceIds,
            "allowed_subrace_ids",
            _subraceDefIndex,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedBloodlineIds,
            "allowed_bloodline_ids",
            _bloodlineDefIndex,
            "bloodline"
        );

        foreach (StringName stageId in ascensionDef.StageIds)
        {
            if (stageId == "")
            {
                continue;
            }
            if (
                !_ascensionStageDefIndex.TryGetValue(
                    stageId,
                    out AscensionStageDefinition stageDef
                )
            )
            {
                errors.Add($"{ownerLabel} references missing ascension_stage {stageId}.");
                continue;
            }
            if (stageDef.AscensionId != ascensionId)
            {
                errors.Add(
                    $"{ownerLabel} stage {stageId} ascension_id must be {ascensionId}, got {stageDef.AscensionId}."
                );
            }
        }
    }

    private void _append_ascension_stage_phase2_errors(
        List<string> errors,
        StringName stageId,
        AscensionStageDefinition stageDef
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
            stageDef.BodySizeCategoryOverride,
            true
        );
        _append_trait_reference_errors(errors, ownerLabel, stageDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_reference_errors(
            errors,
            ownerLabel,
            stageDef.RacialGrantedSkills,
            "ascension"
        );
        if (stageDef.AscensionId == "")
        {
            return;
        }
        if (
            !_ascensionDefIndex.TryGetValue(
                stageDef.AscensionId,
                out AscensionDefinition ascensionDef
            )
        )
        {
            errors.Add($"{ownerLabel} references missing ascension {stageDef.AscensionId}.");
            return;
        }
        if (!ascensionDef.StageIds.Contains(stageId))
        {
            errors.Add(
                $"{ownerLabel} ascension {stageDef.AscensionId} must list this stage in stage_ids."
            );
        }
    }

    private void _append_stage_advancement_phase2_errors(
        List<string> errors,
        StringName modifierId,
        StageAdvancementDefinition modifier
    )
    {
        if (modifier == null)
        {
            return;
        }
        string ownerLabel = $"StageAdvancement {modifierId}";
        if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Unknown)
        {
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.TargetAxis}.");
        }
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.AppliesToRaceIds,
            "applies_to_race_ids",
            _raceDefIndex,
            "race"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.AppliesToSubraceIds,
            "applies_to_subrace_ids",
            _subraceDefIndex,
            "subrace"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.AppliesToBloodlineIds,
            "applies_to_bloodline_ids",
            _bloodlineDefIndex,
            "bloodline"
        );
        _append_id_reference_errors(
            errors,
            ownerLabel,
            modifier.AppliesToAscensionIds,
            "applies_to_ascension_ids",
            _ascensionDefIndex,
            "ascension"
        );
        _append_stage_advancement_max_stage_error(errors, ownerLabel, modifier);
    }

    private void _append_stage_advancement_max_stage_error(
        List<string> errors,
        string ownerLabel,
        StageAdvancementDefinition modifier
    )
    {
        if (modifier.MaxStageId == "")
        {
            return;
        }
        if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Bloodline)
        {
            if (!_bloodlineStageDefIndex.ContainsKey(modifier.MaxStageId))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing bloodline_stage {modifier.MaxStageId}."
                );
            }
        }
        else if (modifier.TargetAxisKind == StageAdvancementTargetAxis.Divine)
        {
            if (!_ascensionStageDefIndex.ContainsKey(modifier.MaxStageId))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing ascension_stage {modifier.MaxStageId}."
                );
            }
        }
        else
        {
            HashSet<StringName> knownStageIds = _collect_known_identity_stage_ids();
            if (!knownStageIds.Contains(modifier.MaxStageId))
            {
                errors.Add(
                    $"{ownerLabel} max_stage_id references missing stage {modifier.MaxStageId}."
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
        IReadOnlyList<StringName> traitIds,
        string fieldLabel
    )
    {
        foreach (StringName traitId in traitIds)
        {
            if (traitId == "")
            {
                continue;
            }
            if (!_traitDefIndex.TryGetValue(traitId, out TraitDefinition traitDef))
            {
                errors.Add($"{ownerLabel} {fieldLabel} references missing trait {traitId}.");
                continue;
            }
            if (!traitDef.IsSourceKindAllowed(TraitSourceKind.Identity))
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
        IReadOnlyList<RacialGrantedSkillDefinition> grantedSkills,
        StringName expectedLearnSource
    )
    {
        int index = 0;
        foreach (RacialGrantedSkillDefinition grantedSkill in grantedSkills)
        {
            if (grantedSkill == null || grantedSkill.SkillId == "")
            {
                index++;
                continue;
            }
            if (
                !_skillDefinitionIndex.TryGetValue(
                    grantedSkill.SkillId,
                    out SkillDefinition skillDefinition
                )
                || skillDefinition == null
            )
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] references missing skill {grantedSkill.SkillId}."
                );
                index++;
                continue;
            }
            if (skillDefinition.LearnSourceKind != SkillDefinition.ToLearnSource(expectedLearnSource))
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.SkillId} learn_source must be {expectedLearnSource}, got {skillDefinition.LearnSource}."
                );
            }
            if (grantedSkill.MinimumSkillLevel > skillDefinition.MaxLevel)
            {
                errors.Add(
                    $"{ownerLabel} racial_granted_skills[{index}] skill {grantedSkill.SkillId} minimum_skill_level must be <= max_level {skillDefinition.MaxLevel}."
                );
            }
            index++;
        }
    }

    private static void _append_id_reference_errors<T>(
        List<string> errors,
        string ownerLabel,
        IReadOnlyList<StringName> values,
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
        IReadOnlyDictionary<StringName, StringName> damageResistances
    )
    {
        foreach ((StringName damageTag, StringName mitigationTier) in damageResistances)
        {
            if (damageTag == "")
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances key {damageTag} must be a non-empty StringName."
                );
                continue;
            }
            if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
            {
                errors.Add(
                    $"{ownerLabel} damage_resistances references unsupported damage tag {damageTag}."
                );
            }
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

    private static HashSet<StringName> _collect_age_profile_stage_ids(
        AgeProfileDefinition profileDef
    )
    {
        var stageIds = new HashSet<StringName>();
        if (profileDef == null)
        {
            return stageIds;
        }
        foreach (AgeStageRuleDefinition stageRule in profileDef.StageRules)
        {
            if (stageRule != null && stageRule.StageId != "")
            {
                stageIds.Add(stageRule.StageId);
            }
        }
        return stageIds;
    }

    private HashSet<StringName> _collect_known_identity_stage_ids()
    {
        var stageIds = new HashSet<StringName>();
        foreach (AgeProfileDefinition profileDef in _ageProfileDefIndex.Values)
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
        AchievementDefinition achievementDef
    )
    {
        if (achievementDef == null)
        {
            return;
        }
        if (achievementDef.EventType == "")
        {
            errors.Add($"Achievement {achievementId} is missing event_type.");
        }
        if (achievementDef.Threshold <= 0)
        {
            errors.Add($"Achievement {achievementId} must have a positive threshold.");
        }
        foreach (AchievementRewardDefinition reward in achievementDef.Rewards)
        {
            if (reward == null)
                continue;
            if (reward.RewardType == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without reward_type.");
            }
            if (reward.TargetId == "")
            {
                errors.Add($"Achievement {achievementId} has a reward without target_id.");
            }
            if (reward.Amount == 0)
            {
                errors.Add(
                    $"Achievement {achievementId} has a zero-amount reward for {reward.TargetId}."
                );
            }
            if (
                reward.RewardType != ""
                && !PendingCharacterRewardContentRules.IsSupportedEntryType(reward.RewardType)
            )
            {
                errors.Add(
                    $"Achievement {achievementId} uses unsupported reward_type {reward.RewardType}."
                );
                continue;
            }
            if (
                reward.RewardKind == PendingCharacterRewardEntryKind.SkillUnlock
                || reward.RewardKind == PendingCharacterRewardEntryKind.SkillMastery
            )
            {
                if (!_skillDefinitionIndex.ContainsKey(reward.TargetId))
                {
                    errors.Add(
                        $"Achievement {achievementId} references missing skill {reward.TargetId}."
                    );
                }
            }
            else if (reward.RewardType == "attribute_progress")
            {
                if (
                    !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                        reward.TargetId
                    )
                )
                {
                    errors.Add(
                        $"Achievement {achievementId} attribute_progress reward references unsupported attribute {reward.TargetId}."
                    );
                }
            }
        }
    }

    private static GDictionary DuplicateDictionary(GDictionary source)
    {
        return source != null ? source.Duplicate() : new GDictionary();
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

    private static IReadOnlyDictionary<StringName, T> CloneTypedDictionary<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        return new ReadOnlyDictionary<StringName, T>(
            source != null
                ? new Dictionary<StringName, T>(source)
                : new Dictionary<StringName, T>()
        );
    }

    private static IReadOnlySet<StringName> ReadOnlyKeySet<T>(
        IReadOnlyDictionary<StringName, T> source
    )
    {
        if (source == null || source.Count == 0)
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        return EquipmentAbilityReadOnlySet<StringName>.From(source.Keys);
    }

    private static void ReplaceDefinitionIndex<T>(
        Dictionary<StringName, T> target,
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        target.Clear();
        if (source == null)
            return;
        foreach ((StringName key, T definition) in source)
        {
            if (key == "")
            {
                throw new InvalidDataException(
                    $"Progression definition index {typeof(T).Name} contains an empty key."
                );
            }
            if (definition == null)
            {
                throw new InvalidDataException(
                    $"Progression definition index {typeof(T).Name}[{key}] contains a null definition."
                );
            }
            target.Add(key, definition);
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

}
