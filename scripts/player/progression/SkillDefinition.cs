using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal static class SkillDefinitionCollectionFreeze
{
    internal static IReadOnlyList<T> List<T>(IReadOnlyList<T> values)
    {
        if (values == null || values.Count == 0)
        {
            return System.Array.Empty<T>();
        }
        return new ReadOnlyCollection<T>(new List<T>(values));
    }

    internal static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return new ReadOnlyDictionary<TKey, TValue>(
                new Dictionary<TKey, TValue>()
            );
        }
        return new ReadOnlyDictionary<TKey, TValue>(
            new Dictionary<TKey, TValue>(values)
        );
    }
}

public sealed class SkillDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyList<AttributeModifierDefinition> EmptyAttributeModifiers =
        System.Array.Empty<AttributeModifierDefinition>();
    private static readonly IReadOnlyDictionary<StringName, int> EmptyStringNameIntMap =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> EmptyLevelDescriptionConfigs =
        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>(
            new Dictionary<int, IReadOnlyDictionary<string, object>>()
        );

    public SkillDefinition(
        StringName skillId,
        string displayName,
        StringName iconId,
        string description,
        StringName skillType,
        int maxLevel,
        int nonCoreMaxLevel,
        StringName dynamicMaxLevelStatId,
        int dynamicMaxLevelBase,
        int dynamicMaxLevelPerStat,
        IReadOnlyList<int> masteryCurve,
        IReadOnlyList<StringName> tags,
        StringName learnSource,
        IReadOnlyList<StringName> learnRequirements,
        StringName unlockMode,
        IReadOnlyList<StringName> knowledgeRequirements,
        IReadOnlyDictionary<StringName, int> skillLevelRequirements,
        IReadOnlyDictionary<StringName, int> attributeRequirements,
        IReadOnlyList<StringName> achievementRequirements,
        IReadOnlyList<StringName> upgradeSourceSkillIds,
        bool retainSourceSkillsOnUnlock,
        StringName coreSkillTransitionMode,
        IReadOnlyList<StringName> masterySources,
        StringName growthTier,
        IReadOnlyDictionary<StringName, int> attributeGrowthProgress,
        StringName practiceTier,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        string levelDescriptionTemplate,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelDescriptionConfigs,
        CombatSkillDefinition combatProfile,
        ContingencyAutomationDefinition contingencyAutomationProfile = null
    )
    {
        SkillId = skillId;
        DisplayName = displayName ?? "";
        IconId = iconId;
        Description = description ?? "";
        SkillType = skillType;
        MaxLevel = maxLevel;
        NonCoreMaxLevel = nonCoreMaxLevel;
        DynamicMaxLevelStatId = dynamicMaxLevelStatId;
        DynamicMaxLevelBase = dynamicMaxLevelBase;
        DynamicMaxLevelPerStat = dynamicMaxLevelPerStat;
        MasteryCurve = SkillDefinitionCollectionFreeze.List(masteryCurve);
        Tags = SkillDefinitionCollectionFreeze.List(tags);
        LearnSource = learnSource;
        LearnRequirements = SkillDefinitionCollectionFreeze.List(learnRequirements);
        UnlockMode = unlockMode;
        KnowledgeRequirements = SkillDefinitionCollectionFreeze.List(
            knowledgeRequirements
        );
        SkillLevelRequirements = SkillDefinitionCollectionFreeze.Dictionary(
            skillLevelRequirements
        );
        AttributeRequirements = SkillDefinitionCollectionFreeze.Dictionary(
            attributeRequirements
        );
        AchievementRequirements = SkillDefinitionCollectionFreeze.List(
            achievementRequirements
        );
        UpgradeSourceSkillIds = SkillDefinitionCollectionFreeze.List(
            upgradeSourceSkillIds
        );
        RetainSourceSkillsOnUnlock = retainSourceSkillsOnUnlock;
        CoreSkillTransitionMode = coreSkillTransitionMode;
        MasterySources = SkillDefinitionCollectionFreeze.List(masterySources);
        GrowthTier = growthTier;
        AttributeGrowthProgress = SkillDefinitionCollectionFreeze.Dictionary(
            attributeGrowthProgress
        );
        PracticeTier = practiceTier;
        AttributeModifiers = SkillDefinitionCollectionFreeze.List(attributeModifiers);
        LevelDescriptionTemplate = levelDescriptionTemplate ?? "";
        LevelDescriptionConfigs = FreezeLevelValueMap(
            levelDescriptionConfigs,
            "SkillDefinition.LevelDescriptionConfigs"
        );
        CombatProfile = combatProfile;
        ContingencyAutomationProfile = contingencyAutomationProfile;
    }

    public StringName SkillId { get; }
    public string DisplayName { get; }
    public StringName IconId { get; }
    public string Description { get; }
    public StringName SkillType { get; }
    public int MaxLevel { get; }
    public int NonCoreMaxLevel { get; }
    public StringName DynamicMaxLevelStatId { get; }
    public int DynamicMaxLevelBase { get; }
    public int DynamicMaxLevelPerStat { get; }
    public IReadOnlyList<int> MasteryCurve { get; }
    public IReadOnlyList<StringName> Tags { get; }
    public StringName LearnSource { get; }
    public IReadOnlyList<StringName> LearnRequirements { get; }
    public StringName UnlockMode { get; }
    public IReadOnlyList<StringName> KnowledgeRequirements { get; }
    public IReadOnlyDictionary<StringName, int> SkillLevelRequirements { get; }
    public IReadOnlyDictionary<StringName, int> AttributeRequirements { get; }
    public IReadOnlyList<StringName> AchievementRequirements { get; }
    public IReadOnlyList<StringName> UpgradeSourceSkillIds { get; }
    public bool RetainSourceSkillsOnUnlock { get; }
    public StringName CoreSkillTransitionMode { get; }
    public IReadOnlyList<StringName> MasterySources { get; }
    public StringName GrowthTier { get; }
    public IReadOnlyDictionary<StringName, int> AttributeGrowthProgress { get; }
    public StringName PracticeTier { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public string LevelDescriptionTemplate { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> LevelDescriptionConfigs { get; }
    public CombatSkillDefinition CombatProfile { get; }
    public ContingencyAutomationDefinition ContingencyAutomationProfile { get; }
    internal SkillTypeKind SkillTypeKind => SkillContentRules.ToSkillType(SkillType);
    internal SkillLearnSourceKind LearnSourceKind => SkillContentRules.ToLearnSource(LearnSource);
    internal SkillUnlockMode UnlockModeKind => SkillContentRules.ToUnlockMode(UnlockMode);
    internal CoreSkillTransitionMode CoreSkillTransitionModeKind =>
        SkillContentRules.ToCoreSkillTransitionMode(CoreSkillTransitionMode);
    internal SkillPracticeTierKind PracticeTierKind =>
        SkillContentRules.ToPracticeTier(PracticeTier);

    public bool CanUseInCombat() => CombatProfile != null;

    internal SkillDefinition WithCombatProfile(CombatSkillDefinition combatProfile) =>
        new(
            SkillId,
            DisplayName,
            IconId,
            Description,
            SkillType,
            MaxLevel,
            NonCoreMaxLevel,
            DynamicMaxLevelStatId,
            DynamicMaxLevelBase,
            DynamicMaxLevelPerStat,
            MasteryCurve,
            Tags,
            LearnSource,
            LearnRequirements,
            UnlockMode,
            KnowledgeRequirements,
            SkillLevelRequirements,
            AttributeRequirements,
            AchievementRequirements,
            UpgradeSourceSkillIds,
            RetainSourceSkillsOnUnlock,
            CoreSkillTransitionMode,
            MasterySources,
            GrowthTier,
            AttributeGrowthProgress,
            PracticeTier,
            AttributeModifiers,
            LevelDescriptionTemplate,
            LevelDescriptionConfigs,
            combatProfile,
            ContingencyAutomationProfile
        );

    internal static SkillPracticeTierKind ToPracticeTier(StringName value) =>
        SkillContentRules.ToPracticeTier(value);

    internal static StringName ToPracticeTierName(SkillPracticeTierKind value) =>
        SkillContentRules.ToStringName(value);

    internal static SkillLearnSourceKind ToLearnSource(StringName value) =>
        SkillContentRules.ToLearnSource(value);

    public int GetMasteryRequiredForLevel(int level)
    {
        if (level < 0)
            return 0;
        if (level < MasteryCurve.Count)
            return MasteryCurve[level];
        if (MasteryCurve.Count <= 0)
            return 0;
        if (MasteryCurve.Count == 1)
            return MasteryCurve[0];
        int lastIndex = MasteryCurve.Count - 1;
        int delta = Mathf.Max(MasteryCurve[lastIndex] - MasteryCurve[lastIndex - 1], 1);
        return MasteryCurve[lastIndex] + delta * (level - lastIndex);
    }

    public bool HasTag(StringName tag)
    {
        foreach (StringName value in Tags)
        {
            if (value == tag)
                return true;
        }
        return false;
    }

    internal static SkillDefinition FromResource(SkillDef source)
    {
        if (source == null)
            return null;
        string skillPath = $"skill.{source.skill_id}";
        return new SkillDefinition(
            source.skill_id,
            source.display_name,
            source.icon_id == "" && source.skill_id != "" ? source.skill_id : source.icon_id,
            source.description,
            source.skill_type,
            source.max_level,
            source.non_core_max_level,
            source.dynamic_max_level_stat_id,
            source.dynamic_max_level_base,
            source.dynamic_max_level_per_stat,
            CopyIntArray(source.mastery_curve),
            CopyStringNames(source.TagsTyped),
            source.learn_source,
            CopyStringNames(source.LearnRequirementsTyped),
            source.unlock_mode,
            CopyStringNames(source.KnowledgeRequirementsTyped),
            CopyStringNameIntMap(source.SkillLevelRequirementsTyped),
            CopyStringNameIntMap(source.AttributeRequirementsTyped),
            CopyStringNames(source.AchievementRequirementsTyped),
            CopyStringNames(source.UpgradeSourceSkillIdsTyped),
            source.retain_source_skills_on_unlock,
            source.core_skill_transition_mode,
            CopyStringNames(source.MasterySourcesTyped),
            source.growth_tier,
            CopyStringNameIntMap(source.AttributeGrowthProgressTyped),
            source.practice_tier,
            ProjectAttributeModifiers(source.AttributeModifiersTyped),
            source.level_description_template,
            ProjectLevelDescriptionConfigs(
                source.LevelDescriptionConfigsProjectionBorrowed,
                $"{skillPath}.level_description_configs"
            ),
            CombatSkillDefinition.FromResource(
                source.combat_profile,
                source.skill_id,
                $"{skillPath}.combat_profile"
            ),
            ContingencyAutomationDefinition.FromResource(
                source.contingency_automation_profile,
                $"{skillPath}.contingency_automation_profile"
            )
        );
    }

    internal static IReadOnlyDictionary<StringName, SkillDefinition> ProjectIndex(
        IReadOnlyDictionary<StringName, SkillDef> source
    )
    {
        if (source == null || source.Count == 0)
            return new ReadOnlyDictionary<StringName, SkillDefinition>(
                new Dictionary<StringName, SkillDefinition>()
            );
        var result = new Dictionary<StringName, SkillDefinition>(source.Count);
        foreach ((StringName skillId, SkillDef skillDef) in source)
        {
            SkillDefinition definition = FromResource(skillDef);
            if (definition != null && skillId != "")
                result[skillId] = definition;
        }
        return new ReadOnlyDictionary<StringName, SkillDefinition>(result);
    }

    private static IReadOnlyList<int> CopyIntArray(int[] values)
    {
        if (values == null || values.Length == 0)
            return System.Array.Empty<int>();
        int[] result = new int[values.Length];
        System.Array.Copy(values, result, values.Length);
        return result;
    }

    internal static IReadOnlyList<StringName> CopyStringNames(IReadOnlyList<StringName> values)
    {
        if (values == null || values.Count == 0)
            return EmptyStringNames;
        return new ReadOnlyCollection<StringName>(new List<StringName>(values));
    }

    internal static IReadOnlyDictionary<StringName, int> CopyStringNameIntMap(
        IReadOnlyDictionary<StringName, int> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyStringNameIntMap;
        return new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>(values));
    }

    private static IReadOnlyList<AttributeModifierDefinition> ProjectAttributeModifiers(
        IReadOnlyList<AttributeModifier> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyAttributeModifiers;
        var result = new List<AttributeModifierDefinition>(values.Count);
        foreach (AttributeModifier modifier in values)
        {
            AttributeModifierDefinition definition = modifier?.ToDefinition();
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<AttributeModifierDefinition>(result)
            : EmptyAttributeModifiers;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> ProjectLevelDescriptionConfigs(
        Godot.Collections.Dictionary source,
        string path
    )
    {
        if (source == null || source.Count == 0)
            return EmptyLevelDescriptionConfigs;
        var result = new Dictionary<int, IReadOnlyDictionary<string, object>>(source.Count);
        int keyIndex = 0;
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != Variant.Type.String)
            {
                throw new System.IO.InvalidDataException(
                    $"Content dictionary at '{path}' requires string level keys; key[{keyIndex}] has {rawKey.VariantType}."
                );
            }
            string key = rawKey.AsString();
            if (!int.TryParse(key, out int level))
            {
                throw new System.IO.InvalidDataException(
                    $"Content dictionary key at '{path}.{key}' must be an integer level."
                );
            }
            if (result.ContainsKey(level))
            {
                throw new System.IO.InvalidDataException(
                    $"Content dictionary at '{path}' contains duplicate normalized level key '{level}'."
                );
            }
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
            {
                throw new System.IO.InvalidDataException(
                    $"Content value at '{path}.{key}' must be a Dictionary, got {rawValue.VariantType}."
                );
            }
            using Godot.Collections.Dictionary config = rawValue.AsGodotDictionary();
            result[level] = ContentValueNormalizer.NormalizeDictionary(
                config,
                $"{path}.{key}"
            );
            keyIndex++;
        }
        return new ReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>(result);
    }

    internal static IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> FreezeLevelValueMap(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> values,
        string path
    )
    {
        if (values == null || values.Count == 0)
            return EmptyLevelDescriptionConfigs;
        var result = new Dictionary<int, IReadOnlyDictionary<string, object>>(values.Count);
        foreach ((int level, IReadOnlyDictionary<string, object> config) in values)
        {
            result[level] = ContentValueNormalizer.NormalizeDictionary(
                config,
                $"{path}[{level}]"
            );
        }
        return new ReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>(result);
    }
}

public sealed class ContingencyAutomationDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyDictionary<string, object> EmptyBindings =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

    public ContingencyAutomationDefinition(
        bool canBeStoredInContingency,
        int minContingencySkillLevel,
        StringName effectCategory,
        IReadOnlyList<StringName> tags,
        int contingencyLoadOverride,
        IReadOnlyList<StringName> allowedTargetResolvers,
        bool requiresManualTargeting,
        IReadOnlyDictionary<string, object> allowedParameterBindings
    )
    {
        CanBeStoredInContingency = canBeStoredInContingency;
        MinContingencySkillLevel = minContingencySkillLevel;
        EffectCategory = effectCategory;
        Tags = SkillDefinitionCollectionFreeze.List(tags);
        ContingencyLoadOverride = contingencyLoadOverride;
        AllowedTargetResolvers = SkillDefinitionCollectionFreeze.List(
            allowedTargetResolvers
        );
        RequiresManualTargeting = requiresManualTargeting;
        AllowedParameterBindings = ContentValueNormalizer.NormalizeDictionary(
            allowedParameterBindings,
            "ContingencyAutomationDefinition.AllowedParameterBindings"
        );
    }

    public bool CanBeStoredInContingency { get; }
    public int MinContingencySkillLevel { get; }
    public StringName EffectCategory { get; }
    public IReadOnlyList<StringName> Tags { get; }
    public int ContingencyLoadOverride { get; }
    public IReadOnlyList<StringName> AllowedTargetResolvers { get; }
    public bool RequiresManualTargeting { get; }
    public IReadOnlyDictionary<string, object> AllowedParameterBindings { get; }

    public bool AllowsTargetResolver(StringName resolver)
    {
        if (resolver == "")
            return false;
        foreach (StringName allowedResolver in AllowedTargetResolvers)
            if (allowedResolver == resolver)
                return true;
        return false;
    }

    public bool AllowsParameterBinding(StringName bindingKey)
    {
        if (bindingKey == "" || AllowedParameterBindings == null)
            return false;
        return AllowedParameterBindings.ContainsKey(bindingKey.ToString());
    }

    internal static ContingencyAutomationDefinition FromResource(
        ContingencyAutomationDef source,
        string path
    )
    {
        if (source == null)
            return null;
        return new ContingencyAutomationDefinition(
            source.can_be_stored_in_contingency,
            source.min_contingency_skill_level,
            source.effect_category,
            CopyStringNameArray(source.tags),
            source.contingency_load_override,
            CopyStringNameArray(source.allowed_target_resolvers),
            source.requires_manual_targeting,
            ContentValueNormalizer.NormalizeDictionary(
                source.allowed_parameter_bindings,
                $"{path}.allowed_parameter_bindings"
            )
        );
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyStringNames;
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
            result.Add(value);
        return new ReadOnlyCollection<StringName>(result);
    }

}

public sealed class CombatSkillDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyList<CombatEffectDefinition> EmptyEffectDefinitions =
        System.Array.Empty<CombatEffectDefinition>();
    private static readonly IReadOnlyList<CombatCastVariantDefinition> EmptyCastVariants =
        System.Array.Empty<CombatCastVariantDefinition>();
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> EmptyLevelOverrides =
        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>(
            new Dictionary<int, IReadOnlyDictionary<string, object>>()
        );

    public CombatSkillDefinition(
        StringName skillId,
        StringName targetMode,
        StringName targetTeamFilter,
        StringName rangePattern,
        int rangeValue,
        StringName areaPattern,
        int areaValue,
        bool requiresLos,
        int apCost,
        int mpCost,
        int staminaCost,
        int cooldownTu,
        int castingTimeTu,
        int castingMaintenanceDc,
        int castingSpellControlDc,
        StringName pendingCastBindingMode,
        int attackRollBonus,
        StringName attackResolutionMode,
        int auraCost,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelOverrides,
        StringName masteryTriggerMode,
        StringName masteryAmountMode,
        StringName spellFateMode,
        StringName spellCriticalMode,
        int spellCriticalMpRefundPercent,
        IReadOnlyList<int> fumbleProtectionCurve,
        int fumbleProtectionExtraMpPercent,
        StringName backlashMode,
        StringName backlashTargetFilter,
        int backlashOffsetRadius,
        StringName areaOriginMode,
        StringName areaDirectionMode,
        IReadOnlyList<StringName> aiTags,
        IReadOnlyList<StringName> deliveryCategories,
        StringName specialResolutionProfileId,
        StringName targetSelectionMode,
        int minTargetCount,
        int maxTargetCount,
        bool allowRepeatTarget,
        int maxHitsPerTarget,
        StringName selectionOrderMode,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<CombatEffectDefinition> passiveEffectDefinitions,
        IReadOnlyList<CombatCastVariantDefinition> castVariants,
        IReadOnlyList<StringName> requiredWeaponFamilies,
        IReadOnlyList<StringName> excludedWeaponFamilies,
        IReadOnlyList<StringName> excludedWeaponTypeIds,
        bool requiresEquippedShield,
        int masteryLowHpBonusMultiplier,
        int masteryLowHpThresholdPercent,
        StringName weaponRangePolicy = default,
        StringName projectileKind = default,
        StringName attackRollBonusStatusId = default,
        int attackRollBonusStatusStackDivisor = 0,
        int randomChainAttackCount = 0,
        bool randomChainContinueOnMiss = false,
        IReadOnlyList<StringName> requiredWeaponTypeIds = null
    )
    {
        SkillId = skillId;
        TargetMode = targetMode;
        TargetTeamFilter = targetTeamFilter;
        RangePattern = rangePattern;
        RangeValue = rangeValue;
        AreaPattern = areaPattern;
        AreaValue = areaValue;
        RequiresLos = requiresLos;
        ApCost = apCost;
        MpCost = mpCost;
        StaminaCost = staminaCost;
        CooldownTu = cooldownTu;
        CastingTimeTu = castingTimeTu;
        CastingMaintenanceDc = castingMaintenanceDc;
        CastingSpellControlDc = castingSpellControlDc;
        PendingCastBindingMode = pendingCastBindingMode;
        AttackRollBonus = attackRollBonus;
        AttackResolutionMode = attackResolutionMode;
        AuraCost = auraCost;
        LevelOverrides = FreezeLevelOverrides(levelOverrides);
        MasteryTriggerMode = masteryTriggerMode;
        MasteryAmountMode = masteryAmountMode;
        SpellFateMode = spellFateMode;
        SpellCriticalMode = spellCriticalMode;
        SpellCriticalMpRefundPercent = Mathf.Clamp(spellCriticalMpRefundPercent, 0, 100);
        FumbleProtectionCurve = SkillDefinitionCollectionFreeze.List(
            fumbleProtectionCurve
        );
        FumbleProtectionExtraMpPercent = Mathf.Max(fumbleProtectionExtraMpPercent, 0);
        BacklashMode = backlashMode;
        BacklashTargetFilter = backlashTargetFilter;
        BacklashOffsetRadius = backlashOffsetRadius;
        AreaOriginMode = areaOriginMode;
        AreaDirectionMode = areaDirectionMode;
        AiTags = SkillDefinitionCollectionFreeze.List(aiTags);
        DeliveryCategories = SkillDefinitionCollectionFreeze.List(
            deliveryCategories
        );
        SpecialResolutionProfileId = specialResolutionProfileId;
        TargetSelectionMode = targetSelectionMode;
        MinTargetCount = minTargetCount;
        MaxTargetCount = maxTargetCount;
        AllowRepeatTarget = allowRepeatTarget;
        MaxHitsPerTarget = maxHitsPerTarget;
        SelectionOrderMode = selectionOrderMode;
        EffectDefinitions = SkillDefinitionCollectionFreeze.List(effectDefinitions);
        PassiveEffectDefinitions = SkillDefinitionCollectionFreeze.List(
            passiveEffectDefinitions
        );
        CastVariants = SkillDefinitionCollectionFreeze.List(castVariants);
        RequiredWeaponFamilies = SkillDefinitionCollectionFreeze.List(
            requiredWeaponFamilies
        );
        RequiredWeaponTypeIds = SkillDefinitionCollectionFreeze.List(
            requiredWeaponTypeIds
        );
        ExcludedWeaponFamilies = SkillDefinitionCollectionFreeze.List(
            excludedWeaponFamilies
        );
        ExcludedWeaponTypeIds = SkillDefinitionCollectionFreeze.List(
            excludedWeaponTypeIds
        );
        RequiresEquippedShield = requiresEquippedShield;
        MasteryLowHpBonusMultiplier = masteryLowHpBonusMultiplier;
        MasteryLowHpThresholdPercent = masteryLowHpThresholdPercent;
        WeaponRangePolicy = weaponRangePolicy;
        ProjectileKind = projectileKind;
        AttackRollBonusStatusId = attackRollBonusStatusId;
        AttackRollBonusStatusStackDivisor = attackRollBonusStatusStackDivisor;
        RandomChainAttackCount = randomChainAttackCount;
        RandomChainContinueOnMiss = randomChainContinueOnMiss;
    }

    public StringName SkillId { get; }
    public StringName TargetMode { get; }
    public StringName TargetTeamFilter { get; }
    public StringName RangePattern { get; }
    public int RangeValue { get; }
    public StringName WeaponRangePolicy { get; }
    public StringName AreaPattern { get; }
    public int AreaValue { get; }
    public bool RequiresLos { get; }
    public int ApCost { get; }
    public int MpCost { get; }
    public int StaminaCost { get; }
    public int CooldownTu { get; }
    public int CastingTimeTu { get; }
    public int CastingMaintenanceDc { get; }
    public int CastingSpellControlDc { get; }
    public StringName PendingCastBindingMode { get; }
    public int AttackRollBonus { get; }
    public StringName AttackResolutionMode { get; }
    public int AuraCost { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> LevelOverrides { get; }
    public StringName MasteryTriggerMode { get; }
    public StringName MasteryAmountMode { get; }
    public StringName SpellFateMode { get; }
    public StringName SpellCriticalMode { get; }
    public int SpellCriticalMpRefundPercent { get; }
    public IReadOnlyList<int> FumbleProtectionCurve { get; }
    public int FumbleProtectionExtraMpPercent { get; }
    public StringName BacklashMode { get; }
    public StringName BacklashTargetFilter { get; }
    public int BacklashOffsetRadius { get; }
    public StringName AreaOriginMode { get; }
    public StringName AreaDirectionMode { get; }
    public IReadOnlyList<StringName> AiTags { get; }
    public IReadOnlyList<StringName> DeliveryCategories { get; }
    public StringName ProjectileKind { get; }
    public StringName AttackRollBonusStatusId { get; }
    public int AttackRollBonusStatusStackDivisor { get; }
    public StringName SpecialResolutionProfileId { get; }
    public StringName TargetSelectionMode { get; }
    public int MinTargetCount { get; }
    public int MaxTargetCount { get; }
    public bool AllowRepeatTarget { get; }
    public int MaxHitsPerTarget { get; }
    public int RandomChainAttackCount { get; }
    public bool RandomChainContinueOnMiss { get; }
    public StringName SelectionOrderMode { get; }
    public IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    public IReadOnlyList<CombatEffectDefinition> PassiveEffectDefinitions { get; }
    public IReadOnlyList<CombatCastVariantDefinition> CastVariants { get; }
    public IReadOnlyList<StringName> RequiredWeaponFamilies { get; }
    public IReadOnlyList<StringName> RequiredWeaponTypeIds { get; }
    public IReadOnlyList<StringName> ExcludedWeaponFamilies { get; }
    public IReadOnlyList<StringName> ExcludedWeaponTypeIds { get; }
    public bool RequiresEquippedShield { get; }
    public int MasteryLowHpBonusMultiplier { get; }
    public int MasteryLowHpThresholdPercent { get; }
    internal BattleTargetMode TargetModeKind => BattleTypedNames.ToTargetMode(TargetMode);
    internal BattleTargetFilter TargetFilterKind => BattleTypedNames.ToTargetFilter(TargetTeamFilter);
    internal BattleTargetSelectionMode TargetSelectionModeKind =>
        BattleTypedNames.ToTargetSelectionMode(TargetSelectionMode);
    internal BattleTargetSelectionOrderMode SelectionOrderModeKind =>
        BattleTypedNames.ToTargetSelectionOrderMode(SelectionOrderMode);
    internal PendingCastBindingModeKind PendingCastBindingModeKind =>
        BattleTypedNames.ToPendingCastBindingMode(PendingCastBindingMode);
    internal CombatSkillMasteryTriggerMode MasteryTriggerModeKind =>
        BattleTypedNames.ToCombatSkillMasteryTriggerMode(MasteryTriggerMode);
    internal CombatSkillMasteryAmountMode MasteryAmountModeKind =>
        BattleTypedNames.ToCombatSkillMasteryAmountMode(MasteryAmountMode);
    internal CombatSpellFateMode SpellFateModeKind =>
        CombatSkillContentRules.ToSpellFateMode(SpellFateMode);
    internal CombatSkillAttackResolutionMode AttackResolutionModeKind =>
        CombatSkillContentRules.ToAttackResolutionMode(AttackResolutionMode);
    internal CombatSkillAttackResolutionMode GetEffectiveAttackResolutionMode(int skillLevel)
    {
        IReadOnlyDictionary<string, object> overrides = BuildLevelOverride(skillLevel);
        return overrides != null
            && overrides.TryGetValue("attack_resolution_mode", out object rawValue)
            && TryReadStringName(rawValue, out StringName value)
            ? CombatSkillContentRules.ToAttackResolutionMode(value)
            : AttackResolutionModeKind;
    }
    internal CombatProjectileKind ProjectileKindTyped =>
        CombatProjectileContentRules.ToProjectileKind(ProjectileKind);
    internal CombatSpellCriticalMode SpellCriticalModeKind =>
        CombatSkillDef.ToSpellCriticalMode(SpellCriticalMode);
    internal CombatSkillBacklashMode BacklashModeKind =>
        CombatSkillContentRules.ToBacklashMode(BacklashMode);
    internal CombatAreaOriginMode AreaOriginModeKind =>
        CombatSkillContentRules.ToAreaOriginMode(AreaOriginMode);
    internal CombatAreaDirectionMode AreaDirectionModeKind =>
        CombatSkillContentRules.ToAreaDirectionMode(AreaDirectionMode);

    public CombatSkillResourceCosts GetEffectiveResourceCostValues(int skillLevel)
    {
        IReadOnlyDictionary<string, object> overrides = BuildLevelOverride(skillLevel);
        return new CombatSkillResourceCosts(
            TryReadIntOverride(overrides, "ap_cost", out int effectiveApCost)
                ? effectiveApCost
                : ApCost,
            TryReadIntOverride(overrides, "mp_cost", out int effectiveMpCost)
                ? effectiveMpCost
                : MpCost,
            TryReadIntOverride(overrides, "stamina_cost", out int effectiveStaminaCost)
                ? effectiveStaminaCost
                : StaminaCost,
            TryReadIntOverride(overrides, "aura_cost", out int effectiveAuraCost)
                ? effectiveAuraCost
                : AuraCost,
            TryReadIntOverride(overrides, "cooldown_tu", out int effectiveCooldownTu)
                ? effectiveCooldownTu
                : CooldownTu
        );
    }

    public int GetEffectiveAttackRollBonus(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "attack_roll_bonus", AttackRollBonus);

    public int GetEffectiveCastingTimeTu(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "casting_time_tu", CastingTimeTu);

    public int GetEffectiveCastingMaintenanceDc(int skillLevel) =>
        ReadIntOverride(
            BuildLevelOverride(skillLevel),
            "casting_maintenance_dc",
            CastingMaintenanceDc
        );

    public int GetEffectiveCastingSpellControlDc(int skillLevel) =>
        ReadIntOverride(
            BuildLevelOverride(skillLevel),
            "casting_spell_control_dc",
            CastingSpellControlDc
        );

    public PendingCastBindingModeKind GetEffectivePendingCastBindingMode(int skillLevel)
    {
        IReadOnlyDictionary<string, object> overrides = BuildLevelOverride(skillLevel);
        return overrides != null
            && overrides.TryGetValue("pending_cast_binding_mode", out object rawValue)
            && TryReadStringName(rawValue, out StringName value)
            ? BattleTypedNames.ToPendingCastBindingMode(value)
            : PendingCastBindingModeKind;
    }

    public StringName GetEffectiveAreaPattern(int skillLevel)
    {
        IReadOnlyDictionary<string, object> overrides = BuildLevelOverride(skillLevel);
        return overrides != null
            && overrides.TryGetValue("area_pattern", out object rawValue)
            && TryReadStringName(rawValue, out StringName value)
            ? value
            : AreaPattern;
    }

    public int GetEffectiveAreaValue(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "area_value", AreaValue);

    public int GetEffectiveRangeValue(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "range_value", RangeValue);

    public int GetEffectiveMaxTargetCount(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "max_target_count", MaxTargetCount);

    public int GetEffectiveRandomChainAttackCount(int skillLevel) =>
        ReadIntOverride(
            BuildLevelOverride(skillLevel),
            "random_chain_attack_count",
            RandomChainAttackCount
        );

    public bool HasCastingTime(int skillLevel) => GetEffectiveCastingTimeTu(skillLevel) > 0;

    public bool HasSpellFateControl() => SpellFateModeKind == CombatSpellFateMode.ControlRoll;

    internal CombatSkillDefinition WithStaminaCost(int staminaCost) =>
        new(
            SkillId,
            TargetMode,
            TargetTeamFilter,
            RangePattern,
            RangeValue,
            AreaPattern,
            AreaValue,
            RequiresLos,
            ApCost,
            MpCost,
            staminaCost,
            CooldownTu,
            CastingTimeTu,
            CastingMaintenanceDc,
            CastingSpellControlDc,
            PendingCastBindingMode,
            AttackRollBonus,
            AttackResolutionMode,
            AuraCost,
            LevelOverrides,
            MasteryTriggerMode,
            MasteryAmountMode,
            SpellFateMode,
            SpellCriticalMode,
            SpellCriticalMpRefundPercent,
            FumbleProtectionCurve,
            FumbleProtectionExtraMpPercent,
            BacklashMode,
            BacklashTargetFilter,
            BacklashOffsetRadius,
            AreaOriginMode,
            AreaDirectionMode,
            AiTags,
            DeliveryCategories,
            SpecialResolutionProfileId,
            TargetSelectionMode,
            MinTargetCount,
            MaxTargetCount,
            AllowRepeatTarget,
            MaxHitsPerTarget,
            SelectionOrderMode,
            EffectDefinitions,
            PassiveEffectDefinitions,
            CastVariants,
            RequiredWeaponFamilies,
            ExcludedWeaponFamilies,
            ExcludedWeaponTypeIds,
            RequiresEquippedShield,
            MasteryLowHpBonusMultiplier,
            MasteryLowHpThresholdPercent,
            WeaponRangePolicy,
            ProjectileKind,
            AttackRollBonusStatusId,
            AttackRollBonusStatusStackDivisor,
            RandomChainAttackCount,
            RandomChainContinueOnMiss,
            RequiredWeaponTypeIds
        );

    internal CombatSkillDefinition WithArea(StringName areaPattern, int areaValue) =>
        new(
            SkillId,
            TargetMode,
            TargetTeamFilter,
            RangePattern,
            RangeValue,
            areaPattern,
            areaValue,
            RequiresLos,
            ApCost,
            MpCost,
            StaminaCost,
            CooldownTu,
            CastingTimeTu,
            CastingMaintenanceDc,
            CastingSpellControlDc,
            PendingCastBindingMode,
            AttackRollBonus,
            AttackResolutionMode,
            AuraCost,
            LevelOverrides,
            MasteryTriggerMode,
            MasteryAmountMode,
            SpellFateMode,
            SpellCriticalMode,
            SpellCriticalMpRefundPercent,
            FumbleProtectionCurve,
            FumbleProtectionExtraMpPercent,
            BacklashMode,
            BacklashTargetFilter,
            BacklashOffsetRadius,
            AreaOriginMode,
            AreaDirectionMode,
            AiTags,
            DeliveryCategories,
            SpecialResolutionProfileId,
            TargetSelectionMode,
            MinTargetCount,
            MaxTargetCount,
            AllowRepeatTarget,
            MaxHitsPerTarget,
            SelectionOrderMode,
            EffectDefinitions,
            PassiveEffectDefinitions,
            CastVariants,
            RequiredWeaponFamilies,
            ExcludedWeaponFamilies,
            ExcludedWeaponTypeIds,
            RequiresEquippedShield,
            MasteryLowHpBonusMultiplier,
            MasteryLowHpThresholdPercent,
            WeaponRangePolicy,
            ProjectileKind,
            AttackRollBonusStatusId,
            AttackRollBonusStatusStackDivisor,
            RandomChainAttackCount,
            RandomChainContinueOnMiss,
            RequiredWeaponTypeIds
        );

    public int GetFumbleProtectionLimit(int skillLevel)
    {
        if (FumbleProtectionCurve.Count == 0)
            return 0;
        int index = Mathf.Clamp(skillLevel, 0, FumbleProtectionCurve.Count - 1);
        return Mathf.Max(FumbleProtectionCurve[index], 0);
    }

    public bool UsesGroundAnchorDriftBacklash() =>
        BacklashModeKind == CombatSkillBacklashMode.GroundAnchorDrift;

    public IReadOnlyList<CombatCastVariantDefinition> GetUnlockedCastVariants(int skillLevel)
    {
        if (CastVariants.Count == 0)
            return EmptyCastVariants;
        var result = new List<CombatCastVariantDefinition>();
        foreach (CombatCastVariantDefinition variant in CastVariants)
        {
            if (variant != null && skillLevel >= variant.MinSkillLevel)
                result.Add(variant);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatCastVariantDefinition>(result)
            : EmptyCastVariants;
    }

    internal static CombatSkillDefinition FromResource(
        CombatSkillDef source,
        StringName fallbackSkillId,
        string path
    )
    {
        if (source == null)
            return null;
        return new CombatSkillDefinition(
            source.skill_id == "" ? fallbackSkillId : source.skill_id,
            source.target_mode,
            source.target_team_filter,
            source.range_pattern,
            source.range_value,
            source.area_pattern,
            source.area_value,
            source.requires_los,
            source.ap_cost,
            source.mp_cost,
            source.stamina_cost,
            source.cooldown_tu,
            source.casting_time_tu,
            source.casting_maintenance_dc,
            source.casting_spell_control_dc,
            source.pending_cast_binding_mode,
            source.attack_roll_bonus,
            source.attack_resolution_mode,
            source.aura_cost,
            ProjectLevelOverrides(source.level_overrides, $"{path}.level_overrides"),
            source.mastery_trigger_mode,
            source.mastery_amount_mode,
            source.spell_fate_mode,
            source.spell_critical_mode,
            source.spell_critical_mp_refund_percent,
            CopyIntArray(source.fumble_protection_curve),
            source.fumble_protection_extra_mp_percent,
            source.backlash_mode,
            source.backlash_target_filter,
            source.backlash_offset_radius,
            source.area_origin_mode,
            source.area_direction_mode,
            CopyStringNameArray(source.ai_tags),
            CopyStringNameArray(source.delivery_categories),
            source.special_resolution_profile_id,
            source.target_selection_mode,
            source.min_target_count,
            source.max_target_count,
            source.allow_repeat_target,
            source.max_hits_per_target,
            source.selection_order_mode,
            ProjectEffectDefinitions(source.effect_defs, $"{path}.effect_defs"),
            ProjectEffectDefinitions(
                source.passive_effect_defs,
                $"{path}.passive_effect_defs"
            ),
            ProjectCastVariants(source.cast_variants, $"{path}.cast_variants"),
            CopyStringNameArray(source.required_weapon_families),
            CopyStringNameArray(source.excluded_weapon_families),
            CopyStringNameArray(source.excluded_weapon_type_ids),
            source.requires_equipped_shield,
            source.mastery_low_hp_bonus_multiplier,
            source.mastery_low_hp_threshold_percent,
            source.weapon_range_policy,
            source.projectile_kind,
            source.attack_roll_bonus_status_id,
            source.attack_roll_bonus_status_stack_divisor,
            source.random_chain_attack_count,
            source.random_chain_continue_on_miss,
            CopyStringNameArray(source.required_weapon_type_ids)
        );
    }

    private IReadOnlyDictionary<string, object> BuildLevelOverride(int skillLevel)
    {
        if (LevelOverrides.Count == 0)
            return ContentValueNormalizer.NormalizeDictionary(
                (IReadOnlyDictionary<string, object>)null,
                "CombatSkillDefinition.LevelOverrides"
            );
        var merged = new Dictionary<string, object>();
        var eligible = new List<int>();
        foreach (int level in LevelOverrides.Keys)
        {
            if (level >= 0 && level <= skillLevel)
                eligible.Add(level);
        }
        eligible.Sort();
        foreach (int level in eligible)
        {
            foreach ((string key, object value) in LevelOverrides[level])
                merged[key] = value;
        }
        return new ReadOnlyDictionary<string, object>(merged);
    }

    private static int ReadIntOverride(
        IReadOnlyDictionary<string, object> overrides,
        string key,
        int fallback
    )
    {
        return TryReadIntOverride(overrides, key, out int value) ? value : fallback;
    }

    private static bool TryReadIntOverride(
        IReadOnlyDictionary<string, object> overrides,
        string key,
        out int value
    )
    {
        if (overrides != null && overrides.TryGetValue(key, out object rawValue))
        {
            if (TryReadIntValue(rawValue, out value))
            {
                return true;
            }
        }
        value = 0;
        return false;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> ProjectLevelOverrides(
        Godot.Collections.Dictionary source,
        string path
    )
    {
        if (source == null || source.Count == 0)
            return EmptyLevelOverrides;
        var result = new Dictionary<int, IReadOnlyDictionary<string, object>>();
        foreach (Variant rawKey in source.Keys)
        {
            if (!TryReadLevelKey(rawKey, out int level))
                throw new System.IO.InvalidDataException(
                    $"Content dictionary key at '{path}' must be an integral level, got {rawKey.VariantType}."
                );
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
                throw new System.IO.InvalidDataException(
                    $"Content value at '{path}[{level}]' must be a Dictionary, got {rawValue.VariantType}."
                );
            if (result.ContainsKey(level))
                throw new System.IO.InvalidDataException(
                    $"Content dictionary at '{path}' contains duplicate normalized level key '{level}'."
                );
            using Godot.Collections.Dictionary dictionary = rawValue.AsGodotDictionary();
            result[level] = ContentValueNormalizer.NormalizeDictionary(
                dictionary,
                $"{path}[{level}]"
            );
        }
        return result.Count > 0
            ? new ReadOnlyDictionary<int, IReadOnlyDictionary<string, object>>(result)
            : EmptyLevelOverrides;
    }

    private static bool TryReadLevelKey(Variant rawKey, out int level)
    {
        if (rawKey.VariantType == Variant.Type.Int)
        {
            level = rawKey.AsInt32();
            return true;
        }
        if (rawKey.VariantType == Variant.Type.Float)
        {
            double rawLevel = rawKey.AsDouble();
            int normalized = (int)System.Math.Floor(rawLevel);
            if (Mathf.IsEqualApprox((float)rawLevel, normalized))
            {
                level = normalized;
                return true;
            }
        }
        level = 0;
        return false;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> FreezeLevelOverrides(
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> values
    ) => SkillDefinition.FreezeLevelValueMap(values, "CombatSkillDefinition.LevelOverrides");

    private static bool TryReadIntValue(object value, out int result)
    {
        if (value is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }
        if (
            value is double doubleValue
            && doubleValue >= int.MinValue
            && doubleValue <= int.MaxValue
        )
        {
            result = (int)doubleValue;
            return true;
        }
        result = 0;
        return false;
    }

    private static bool TryReadStringName(object value, out StringName result)
    {
        if (value is StringName stringName)
        {
            result = stringName;
            return true;
        }
        if (value is string text)
        {
            result = new StringName(text);
            return true;
        }
        result = default;
        return false;
    }

    private static IReadOnlyList<int> CopyIntArray(int[] values)
    {
        if (values == null || values.Length == 0)
            return System.Array.Empty<int>();
        int[] result = new int[values.Length];
        System.Array.Copy(values, result, values.Length);
        return result;
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyStringNames;
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
            result.Add(value);
        return new ReadOnlyCollection<StringName>(result);
    }

    private static IReadOnlyList<CombatCastVariantDefinition> ProjectCastVariants(
        Godot.Collections.Array<CombatCastVariantDef> values,
        string path
    )
    {
        if (values == null || values.Count == 0)
            return EmptyCastVariants;
        var result = new List<CombatCastVariantDefinition>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            CombatCastVariantDef variant = values[index];
            CombatCastVariantDefinition definition = CombatCastVariantDefinition.FromResource(
                variant,
                $"{path}[{index}]"
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatCastVariantDefinition>(result)
            : EmptyCastVariants;
    }

    private static IReadOnlyList<CombatEffectDefinition> ProjectEffectDefinitions(
        Godot.Collections.Array<CombatEffectDef> values,
        string path
    )
    {
        if (values == null || values.Count == 0)
            return EmptyEffectDefinitions;
        var result = new List<CombatEffectDefinition>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            CombatEffectDef effect = values[index];
            CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
                effect,
                $"{path}[{index}]"
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatEffectDefinition>(result)
            : EmptyEffectDefinitions;
    }
}

public sealed class CombatCastVariantDefinition
{
    public CombatCastVariantDefinition(
        StringName variantId,
        string displayName,
        string description,
        int minSkillLevel,
        StringName targetMode,
        StringName footprintPattern,
        int requiredCoordCount,
        IReadOnlyList<StringName> allowedBaseTerrains,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> parameters,
        StringName projectileKindOverride = default
    )
    {
        VariantId = variantId;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        MinSkillLevel = minSkillLevel;
        TargetMode = targetMode;
        FootprintPattern = footprintPattern;
        RequiredCoordCount = requiredCoordCount;
        AllowedBaseTerrains = SkillDefinitionCollectionFreeze.List(
            allowedBaseTerrains
        );
        EffectDefinitions = SkillDefinitionCollectionFreeze.List(effectDefinitions);
        ProjectileKindOverride = projectileKindOverride;
        Parameters = ContentValueNormalizer.NormalizeDictionary(
            parameters,
            "CombatCastVariantDefinition.Parameters"
        );
    }

    public StringName VariantId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int MinSkillLevel { get; }
    public StringName TargetMode { get; }
    public StringName FootprintPattern { get; }
    public int RequiredCoordCount { get; }
    public IReadOnlyList<StringName> AllowedBaseTerrains { get; }
    public IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    public StringName ProjectileKindOverride { get; }
    public IReadOnlyDictionary<string, object> Parameters { get; }
    internal BattleTargetMode TargetModeKind => BattleTypedNames.ToTargetMode(TargetMode);
    internal CombatCastFootprintPattern FootprintPatternKind =>
        CombatSkillTargetingContentRules.ToFootprintPattern(FootprintPattern);
    internal CombatProjectileKind ProjectileKindOverrideTyped =>
        CombatProjectileContentRules.ToProjectileKind(ProjectileKindOverride);

    internal static CombatCastVariantDefinition FromResource(
        CombatCastVariantDef source,
        string path
    )
    {
        if (source == null)
            return null;
        return new CombatCastVariantDefinition(
            source.variant_id,
            source.display_name,
            source.description,
            source.min_skill_level,
            source.target_mode,
            source.footprint_pattern,
            source.required_coord_count,
            CopyStringNameArray(source.allowed_base_terrains),
            CopyEffectDefinitions(source.effect_defs, $"{path}.effect_defs"),
            ContentValueNormalizer.NormalizeDictionary(
                source.@params,
                $"{path}.params"
            ),
            source.projectile_kind_override
        );
    }

    private static IReadOnlyList<CombatEffectDefinition> CopyEffectDefinitions(
        Godot.Collections.Array<CombatEffectDef> values,
        string path
    )
    {
        if (values == null || values.Count == 0)
            return System.Array.Empty<CombatEffectDefinition>();
        var result = new List<CombatEffectDefinition>(values.Count);
        for (int index = 0; index < values.Count; index++)
        {
            CombatEffectDef effect = values[index];
            CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
                effect,
                $"{path}[{index}]"
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatEffectDefinition>(result)
            : System.Array.Empty<CombatEffectDefinition>();
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
            return System.Array.Empty<StringName>();
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
            result.Add(value);
        return new ReadOnlyCollection<StringName>(result);
    }

}

public sealed class CombatDamageSegmentDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();

    public CombatDamageSegmentDefinition(
        StringName damageTag,
        int power,
        int diceCount,
        int diceSides,
        int diceBonus,
        double preResistanceDamageMultiplier,
        IReadOnlyList<StringName> damageTags = null,
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null
    )
    {
        DamageTag = damageTag;
        Power = System.Math.Max(power, 0);
        DiceCount = diceCount;
        DiceSides = diceSides;
        DiceBonus = diceBonus;
        PreResistanceDamageMultiplier = preResistanceDamageMultiplier;
        DamageTags = SkillDefinitionCollectionFreeze.List(damageTags);
        MitigationBypassDamageTags = SkillDefinitionCollectionFreeze.List(
            mitigationBypassDamageTags
        );
        MitigationBypassTiers = SkillDefinitionCollectionFreeze.List(
            mitigationBypassTiers
        );
    }

    public StringName DamageTag { get; }
    public int Power { get; }
    public int DiceCount { get; }
    public int DiceSides { get; }
    public int DiceBonus { get; }
    public double PreResistanceDamageMultiplier { get; }
    public IReadOnlyList<StringName> DamageTags { get; }
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; }
    public IReadOnlyList<StringName> MitigationBypassTiers { get; }

    internal static IReadOnlyList<CombatDamageSegmentDefinition> ProjectArray(
        Godot.Collections.Array<CombatDamageSegmentDef> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return System.Array.Empty<CombatDamageSegmentDefinition>();
        }
        var result = new List<CombatDamageSegmentDefinition>();
        foreach (CombatDamageSegmentDef value in values)
        {
            CombatDamageSegmentDefinition definition = FromResource(value);
            if (definition != null)
            {
                result.Add(definition);
            }
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatDamageSegmentDefinition>(result)
            : System.Array.Empty<CombatDamageSegmentDefinition>();
    }

    private static CombatDamageSegmentDefinition FromResource(CombatDamageSegmentDef source)
    {
        return source == null
            ? null
            : new CombatDamageSegmentDefinition(
                source.damage_tag,
                source.power,
                source.dice_count,
                source.dice_sides,
                source.dice_bonus,
                source.pre_resistance_damage_multiplier,
                CopyStringNameArray(source.damage_tags),
                CopyStringNameArray(source.mitigation_bypass_damage_tags),
                CopyStringNameArray(source.mitigation_bypass_tiers)
            );
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return EmptyStringNames;
        }
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
            {
                result.Add(normalized);
            }
        }
        return result.Count > 0
            ? new ReadOnlyCollection<StringName>(result)
            : EmptyStringNames;
    }
}

public sealed class CombatTargetDamageMultiplierRuleDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();

    public CombatTargetDamageMultiplierRuleDefinition(
        IReadOnlyList<StringName> anyCreatureTypeTags,
        IReadOnlyList<StringName> allCreatureTypeTags,
        IReadOnlyList<StringName> excludedCreatureTypeTags,
        int multiplierPercent
    )
    {
        AnyCreatureTypeTags = SkillDefinitionCollectionFreeze.List(
            anyCreatureTypeTags
        );
        AllCreatureTypeTags = SkillDefinitionCollectionFreeze.List(
            allCreatureTypeTags
        );
        ExcludedCreatureTypeTags = SkillDefinitionCollectionFreeze.List(
            excludedCreatureTypeTags
        );
        MultiplierPercent = multiplierPercent;
    }

    public IReadOnlyList<StringName> AnyCreatureTypeTags { get; }
    public IReadOnlyList<StringName> AllCreatureTypeTags { get; }
    public IReadOnlyList<StringName> ExcludedCreatureTypeTags { get; }
    public int MultiplierPercent { get; }

    internal bool Matches(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return false;
        }
        foreach (StringName excluded in ExcludedCreatureTypeTags)
        {
            if (excluded != "" && targetUnit.HasCreatureTypeTag(excluded))
            {
                return false;
            }
        }
        foreach (StringName required in AllCreatureTypeTags)
        {
            if (required == "" || !targetUnit.HasCreatureTypeTag(required))
            {
                return false;
            }
        }
        if (AnyCreatureTypeTags.Count == 0)
        {
            return AllCreatureTypeTags.Count > 0;
        }
        foreach (StringName candidate in AnyCreatureTypeTags)
        {
            if (candidate != "" && targetUnit.HasCreatureTypeTag(candidate))
            {
                return true;
            }
        }
        return false;
    }

    internal static IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition> ProjectArray(
        Godot.Collections.Array<CombatTargetDamageMultiplierRuleDef> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return System.Array.Empty<CombatTargetDamageMultiplierRuleDefinition>();
        }
        var result = new List<CombatTargetDamageMultiplierRuleDefinition>();
        foreach (CombatTargetDamageMultiplierRuleDef value in values)
        {
            CombatTargetDamageMultiplierRuleDefinition definition = FromResource(value);
            if (definition != null)
            {
                result.Add(definition);
            }
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatTargetDamageMultiplierRuleDefinition>(result)
            : System.Array.Empty<CombatTargetDamageMultiplierRuleDefinition>();
    }

    private static CombatTargetDamageMultiplierRuleDefinition FromResource(
        CombatTargetDamageMultiplierRuleDef source
    )
    {
        return source == null
            ? null
            : new CombatTargetDamageMultiplierRuleDefinition(
                CopyStringNameArray(source.any_creature_type_tags),
                CopyStringNameArray(source.all_creature_type_tags),
                CopyStringNameArray(source.excluded_creature_type_tags),
                source.multiplier_percent
            );
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return EmptyStringNames;
        }
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "" && !result.Contains(normalized))
            {
                result.Add(normalized);
            }
        }
        return result.Count > 0
            ? new ReadOnlyCollection<StringName>(result)
            : EmptyStringNames;
    }
}

public sealed class CombatEffectDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyList<CombatDamageSegmentDefinition> EmptyDamageSegments =
        System.Array.Empty<CombatDamageSegmentDefinition>();
    private static readonly IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition> EmptyTargetDamageMultiplierRules =
        System.Array.Empty<CombatTargetDamageMultiplierRuleDefinition>();

    public CombatEffectDefinition(
        StringName effectType,
        StringName effectTargetTeamFilter,
        StringName statusId,
        StringName saveFailureStatusId,
        StringName terrainEffectId,
        StringName terrainReplaceTo,
        int heightDelta,
        bool requiresWeapon,
        bool addWeaponDice,
        bool preventRepeatTarget,
        StringName forcedMoveMode,
        int minSkillLevel,
        int maxSkillLevel,
        StringName damageTag,
        int damageRatioPercent,
        double preResistanceDamageMultiplier,
        StringName bonusCondition,
        int hpRatioThresholdPercent,
        StringName damageCategory,
        StringName drBypassTag,
        int diceCount,
        int diceSides,
        int diceBonus,
        int bonusDamageDiceCount,
        int bonusDamageDiceSides,
        int bonusDamageDiceBonus,
        int saveDc,
        StringName saveDcMode,
        StringName saveDcSourceAbility,
        StringName saveAbility,
        bool savePartialOnSuccess,
        StringName saveTag,
        int thresholdBaseValue,
        int thresholdLevelAnchor,
        int thresholdLevelBonusPerDelta,
        int thresholdMaxHpRatioPercent,
        int thresholdCapMaxHpRatioPercent,
        int soulFractureDurationTu,
        int healMultiplierPercent,
        int shieldGainMultiplierPercent,
        int appliedStatusDurationTu,
        int durationTu,
        int tickIntervalTu,
        IReadOnlyList<StringName> effectTags,
        StringName triggerCondition = default,
        int power = 0,
        int rangeBonus = 0,
        int forcedMoveDistance = 0,
        int jumpBaseBudget = 0,
        double jumpStrScale = 0.0,
        double jumpArcRatio = 0.0,
        int jumpRangeMultiplier = 1,
        int diceSidesBase = 0,
        int diceSidesPerConstitutionMod = 0,
        int diceSidesPerWillpowerMod = 0,
        IReadOnlyDictionary<string, object> parameters = null,
        IReadOnlyList<StringName> effectCategories = null,
        bool allowRepeatHitsAcrossSteps = false,
        StringName tickEffectType = default,
        StringName lifetimePolicy = default,
        int moveCostDelta = 0,
        StringName renderOverlayId = default,
        int overlayPriority = 0,
        string displayName = "",
        BattleAttackRollModifierSpec accuracyModifierSpec = null,
        StringName doesNotStackWithStatusId = default,
        IReadOnlyList<StringName> doesNotStackWithStatusIds = null,
        IReadOnlyList<StringName> damageTags = null,
        bool useWeaponPhysicalDamageTag = false,
        bool resolveAsWeaponAttack = false,
        bool stopOnMiss = true,
        bool stopOnTargetDown = true,
        bool removeHarmful = false,
        bool removeHarmfulFromAllies = true,
        bool removeBeneficial = false,
        bool removeBeneficialFromEnemies = true,
        bool requireDamageApplied = false,
        int maxStatusRemoved = 0,
        int minHpAfterDamage = 1,
        int deathPreventionPriority = 0,
        int attackRollPenalty = -1,
        bool undispellable = false,
        bool dispellableMagic = false,
        bool dispellableHarmfulMagic = false,
        bool dispellableBeneficialMagic = false,
        StringName mitigationTier = default,
        int secondaryHitDcBase = 10,
        int debuffCountThreshold = 3,
        int baseHeal = 8,
        int healPerLevel = 4,
        int conModBase = 2,
        int conModPer2Levels = 1,
        StringName bodySizeCategory = default,
        StringName stackBehavior = default,
        int stackLimit = 0,
        StringName triggerEvent = default,
        StringName triggerStatusId = default,
        StringName consumedStatusId = default,
        StringName requiredTargetStatusId = default,
        int requiredTargetStatusMinStacks = 0,
        int dicePerConsumedStack = 0,
        int diceSidesPerStack = 0,
        int apGain = 0,
        int freeMovePointsGain = 0,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false,
        bool lockCounterattack = false,
        bool lockGuard = false,
        bool lockDodgeBonus = false,
        bool lockCrit = false,
        int saveBonus = 0,
        int controlSaveBonus = 0,
        int passiveReduction = 0,
        int contentDr = 0,
        int guardBlock = 0,
        int mainSkillLockOtherDebuffCount = 0,
        IReadOnlyList<StringName> saveAdvantageTags = null,
        IReadOnlyList<StringName> saveDisadvantageTags = null,
        IReadOnlyList<StringName> saveImmunityTags = null,
        IReadOnlyList<EquipmentSlotWeightDefinition> equipmentDurabilitySlotWeights = null,
        StringName requiredTargetStatusSourceSelector = default,
        StringName bonusConditionCreatureTypeTag = default,
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null,
        IReadOnlyList<CombatDamageSegmentDefinition> extraDamageSegments = null,
        IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition> targetDamageMultiplierRules = null,
        int attackRollBonus = 0,
        bool consumeOnNextAttackCheck = false,
        bool consumeOnNextSave = false,
        bool attackRollAdvantage = false,
        int sourceBoundWeaponBonusDamageDiceCount = 0,
        int sourceBoundWeaponBonusDamageDiceSides = 0,
        int sourceBoundWeaponBonusDamageDiceBonus = 0,
        int chargeTrapImmunityMinSkillLevel = -1,
        StringName pathStepAreaPattern = default,
        int pathStepRadius = 1,
        string pathStepLogLabel = "",
        StringName repeatHitStatusId = default,
        int repeatHitStatusThreshold = 0,
        int repeatHitStatusMinSkillLevel = 0,
        int repeatHitStatusPower = 1,
        int repeatHitStatusDurationTu = 0,
        string repeatHitStatusLogTemplate = "",
        int fixedAttackCount = 0,
        int weaponDiceMultiplier = 1,
        int bonusWeaponDiceMultiplier = 0,
        bool bonusDamageSeparateEvent = false,
        int meleeComboStackGainBonus = 0,
        StringName comboAttackBonusStatusId = default,
        int comboAttackBonusStackDivisor = 0,
        StringName upkeepResource = default,
        int upkeepIntervalTu = 0,
        int upkeepBaseCost = 0,
        int upkeepEscalationIntervalTu = 0,
        int upkeepCostMultiplier = 1,
        bool breakOnHardControl = false,
        StringName terminationStatusId = default,
        int terminationStatusDurationTu = 0,
        int terminationAttackRollPenalty = 0,
        int terminationCooldownTu = 0
    )
    {
        EffectType = effectType;
        EffectTargetTeamFilter = effectTargetTeamFilter;
        StatusId = statusId;
        SaveFailureStatusId = saveFailureStatusId;
        TerrainEffectId = terrainEffectId;
        TerrainReplaceTo = terrainReplaceTo;
        HeightDelta = heightDelta;
        RequiresWeapon = requiresWeapon;
        AddWeaponDice = addWeaponDice;
        PreventRepeatTarget = preventRepeatTarget;
        ForcedMoveMode = forcedMoveMode;
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
        DamageTag = damageTag;
        DamageRatioPercent = damageRatioPercent;
        PreResistanceDamageMultiplier = preResistanceDamageMultiplier;
        WeaponDiceMultiplier = System.Math.Max(weaponDiceMultiplier, 1);
        BonusWeaponDiceMultiplier = System.Math.Max(bonusWeaponDiceMultiplier, 0);
        BonusCondition = bonusCondition;
        BonusConditionCreatureTypeTag = bonusConditionCreatureTypeTag;
        HpRatioThresholdPercent = hpRatioThresholdPercent;
        DamageCategory = damageCategory;
        DrBypassTag = drBypassTag;
        DiceCount = diceCount;
        DiceSides = diceSides;
        DiceBonus = diceBonus;
        BonusDamageDiceCount = bonusDamageDiceCount;
        BonusDamageDiceSides = bonusDamageDiceSides;
        BonusDamageDiceBonus = bonusDamageDiceBonus;
        BonusDamageSeparateEvent = bonusDamageSeparateEvent;
        MeleeComboStackGainBonus = System.Math.Max(meleeComboStackGainBonus, 0);
        ComboAttackBonusStatusId = ProgressionDataUtils.to_string_name(
            comboAttackBonusStatusId
        );
        ComboAttackBonusStackDivisor = System.Math.Max(comboAttackBonusStackDivisor, 0);
        UpkeepResource = ProgressionDataUtils.to_string_name(upkeepResource);
        UpkeepIntervalTu = System.Math.Max(upkeepIntervalTu, 0);
        UpkeepBaseCost = System.Math.Max(upkeepBaseCost, 0);
        UpkeepEscalationIntervalTu = System.Math.Max(upkeepEscalationIntervalTu, 0);
        UpkeepCostMultiplier = System.Math.Max(upkeepCostMultiplier, 1);
        BreakOnHardControl = breakOnHardControl;
        TerminationStatusId = ProgressionDataUtils.to_string_name(terminationStatusId);
        TerminationStatusDurationTu = System.Math.Max(terminationStatusDurationTu, 0);
        TerminationAttackRollPenalty = System.Math.Max(terminationAttackRollPenalty, 0);
        TerminationCooldownTu = System.Math.Max(terminationCooldownTu, 0);
        SourceBoundWeaponBonusDamageDiceCount = sourceBoundWeaponBonusDamageDiceCount;
        SourceBoundWeaponBonusDamageDiceSides = sourceBoundWeaponBonusDamageDiceSides;
        SourceBoundWeaponBonusDamageDiceBonus = sourceBoundWeaponBonusDamageDiceBonus;
        ChargeTrapImmunityMinSkillLevel = chargeTrapImmunityMinSkillLevel;
        SaveDc = saveDc;
        SaveDcMode = saveDcMode;
        SaveDcSourceAbility = saveDcSourceAbility;
        SaveAbility = saveAbility;
        SavePartialOnSuccess = savePartialOnSuccess;
        SaveTag = saveTag;
        ThresholdBaseValue = thresholdBaseValue;
        ThresholdLevelAnchor = thresholdLevelAnchor;
        ThresholdLevelBonusPerDelta = thresholdLevelBonusPerDelta;
        ThresholdMaxHpRatioPercent = thresholdMaxHpRatioPercent;
        ThresholdCapMaxHpRatioPercent = thresholdCapMaxHpRatioPercent;
        SoulFractureDurationTu = soulFractureDurationTu;
        HealMultiplierPercent = healMultiplierPercent;
        ShieldGainMultiplierPercent = shieldGainMultiplierPercent;
        AppliedStatusDurationTu = appliedStatusDurationTu;
        DurationTu = durationTu;
        TickIntervalTu = tickIntervalTu;
        EffectTags = SkillDefinitionCollectionFreeze.List(effectTags);
        TriggerCondition = triggerCondition;
        Power = power;
        RangeBonus = rangeBonus;
        ForcedMoveDistance = forcedMoveDistance;
        JumpBaseBudget = jumpBaseBudget;
        JumpStrScale = jumpStrScale;
        JumpArcRatio = jumpArcRatio;
        JumpRangeMultiplier = jumpRangeMultiplier;
        DiceSidesBase = diceSidesBase;
        DiceSidesPerConstitutionMod = diceSidesPerConstitutionMod;
        DiceSidesPerWillpowerMod = diceSidesPerWillpowerMod;
        Parameters = ContentValueNormalizer.NormalizeDictionary(
            parameters,
            "CombatEffectDefinition.Parameters"
        );
        EffectCategories = SkillDefinitionCollectionFreeze.List(effectCategories);
        AllowRepeatHitsAcrossSteps = allowRepeatHitsAcrossSteps;
        TickEffectType = tickEffectType;
        LifetimePolicy = lifetimePolicy == "" ? (StringName)"timed" : lifetimePolicy;
        MoveCostDelta = moveCostDelta;
        RenderOverlayId = renderOverlayId;
        OverlayPriority = overlayPriority;
        DisplayName = displayName ?? "";
        _accuracyModifierSpec = accuracyModifierSpec?.Clone();
        DoesNotStackWithStatusId = doesNotStackWithStatusId;
        DoesNotStackWithStatusIds = SkillDefinitionCollectionFreeze.List(
            doesNotStackWithStatusIds
        );
        DamageTags = SkillDefinitionCollectionFreeze.List(damageTags);
        MitigationBypassDamageTags = SkillDefinitionCollectionFreeze.List(
            mitigationBypassDamageTags
        );
        MitigationBypassTiers = SkillDefinitionCollectionFreeze.List(
            mitigationBypassTiers
        );
        UseWeaponPhysicalDamageTag = useWeaponPhysicalDamageTag;
        ResolveAsWeaponAttack = resolveAsWeaponAttack;
        StopOnMiss = stopOnMiss;
        StopOnTargetDown = stopOnTargetDown;
        FixedAttackCount = fixedAttackCount;
        RemoveHarmful = removeHarmful;
        RemoveHarmfulFromAllies = removeHarmfulFromAllies;
        RemoveBeneficial = removeBeneficial;
        RemoveBeneficialFromEnemies = removeBeneficialFromEnemies;
        RequireDamageApplied = requireDamageApplied;
        MaxStatusRemoved = maxStatusRemoved;
        MinHpAfterDamage = minHpAfterDamage;
        DeathPreventionPriority = deathPreventionPriority;
        AttackRollPenalty = attackRollPenalty;
        AttackRollBonus = attackRollBonus;
        AttackRollAdvantage = attackRollAdvantage;
        ConsumeOnNextAttackCheck = consumeOnNextAttackCheck;
        ConsumeOnNextSave = consumeOnNextSave;
        Undispellable = undispellable;
        DispellableMagic = dispellableMagic;
        DispellableHarmfulMagic = dispellableHarmfulMagic;
        DispellableBeneficialMagic = dispellableBeneficialMagic;
        MitigationTier = mitigationTier;
        SecondaryHitDcBase = secondaryHitDcBase;
        DebuffCountThreshold = debuffCountThreshold;
        BaseHeal = baseHeal;
        HealPerLevel = healPerLevel;
        ConModBase = conModBase;
        ConModPer2Levels = conModPer2Levels;
        BodySizeCategory = bodySizeCategory;
        StackBehavior = stackBehavior == "" ? (StringName)"refresh" : stackBehavior;
        StackLimit = stackLimit;
        TriggerEvent = triggerEvent;
        TriggerStatusId = triggerStatusId;
        ConsumedStatusId = consumedStatusId;
        RequiredTargetStatusId = requiredTargetStatusId;
        RequiredTargetStatusMinStacks = System.Math.Max(requiredTargetStatusMinStacks, 0);
        DicePerConsumedStack = dicePerConsumedStack;
        DiceSidesPerStack = diceSidesPerStack;
        ApGain = apGain;
        FreeMovePointsGain = freeMovePointsGain;
        CountsAsDebuffOverride = countsAsDebuffOverride;
        CountsAsDebuff = countsAsDebuff;
        LockCounterattack = lockCounterattack;
        LockGuard = lockGuard;
        LockDodgeBonus = lockDodgeBonus;
        LockCrit = lockCrit;
        SaveBonus = saveBonus;
        ControlSaveBonus = controlSaveBonus;
        PassiveReduction = passiveReduction;
        ContentDr = contentDr;
        GuardBlock = guardBlock;
        MainSkillLockOtherDebuffCount = mainSkillLockOtherDebuffCount;
        SaveAdvantageTags = SkillDefinitionCollectionFreeze.List(
            saveAdvantageTags
        );
        SaveDisadvantageTags = SkillDefinitionCollectionFreeze.List(
            saveDisadvantageTags
        );
        SaveImmunityTags = SkillDefinitionCollectionFreeze.List(saveImmunityTags);
        EquipmentDurabilitySlotWeights = SkillDefinitionCollectionFreeze.List(
            equipmentDurabilitySlotWeights
        );
        RequiredTargetStatusSourceSelector = requiredTargetStatusSourceSelector;
        ExtraDamageSegments = SkillDefinitionCollectionFreeze.List(
            extraDamageSegments
        );
        TargetDamageMultiplierRules = SkillDefinitionCollectionFreeze.List(
            targetDamageMultiplierRules
        );
        PathStepAreaPattern =
            pathStepAreaPattern == "" ? (StringName)"diamond" : pathStepAreaPattern;
        PathStepRadius = pathStepRadius;
        PathStepLogLabel = pathStepLogLabel ?? "";
        RepeatHitStatusId = repeatHitStatusId;
        RepeatHitStatusThreshold = repeatHitStatusThreshold;
        RepeatHitStatusMinSkillLevel = repeatHitStatusMinSkillLevel;
        RepeatHitStatusPower = repeatHitStatusPower;
        RepeatHitStatusDurationTu = repeatHitStatusDurationTu;
        RepeatHitStatusLogTemplate = repeatHitStatusLogTemplate ?? "";
    }

    public StringName EffectType { get; }
    public StringName EffectTargetTeamFilter { get; }
    public StringName StatusId { get; }
    public StringName SaveFailureStatusId { get; }
    public StringName TerrainEffectId { get; }
    public StringName TerrainReplaceTo { get; }
    public int HeightDelta { get; }
    public bool RequiresWeapon { get; }
    public bool AddWeaponDice { get; }
    public bool PreventRepeatTarget { get; }
    public StringName ForcedMoveMode { get; }
    public int MinSkillLevel { get; }
    public int MaxSkillLevel { get; }
    public StringName DamageTag { get; }
    public int DamageRatioPercent { get; }
    public double PreResistanceDamageMultiplier { get; }
    public int WeaponDiceMultiplier { get; }
    public int BonusWeaponDiceMultiplier { get; }
    public StringName BonusCondition { get; }
    public StringName BonusConditionCreatureTypeTag { get; }
    public int HpRatioThresholdPercent { get; }
    public StringName DamageCategory { get; }
    public StringName DrBypassTag { get; }
    public int DiceCount { get; }
    public int DiceSides { get; }
    public int DiceBonus { get; }
    public int BonusDamageDiceCount { get; }
    public int BonusDamageDiceSides { get; }
    public int BonusDamageDiceBonus { get; }
    public bool BonusDamageSeparateEvent { get; }
    public int MeleeComboStackGainBonus { get; }
    public StringName ComboAttackBonusStatusId { get; }
    public int ComboAttackBonusStackDivisor { get; }
    public StringName UpkeepResource { get; }
    public int UpkeepIntervalTu { get; }
    public int UpkeepBaseCost { get; }
    public int UpkeepEscalationIntervalTu { get; }
    public int UpkeepCostMultiplier { get; }
    public bool BreakOnHardControl { get; }
    public StringName TerminationStatusId { get; }
    public int TerminationStatusDurationTu { get; }
    public int TerminationAttackRollPenalty { get; }
    public int TerminationCooldownTu { get; }
    public int SourceBoundWeaponBonusDamageDiceCount { get; }
    public int SourceBoundWeaponBonusDamageDiceSides { get; }
    public int SourceBoundWeaponBonusDamageDiceBonus { get; }
    public int ChargeTrapImmunityMinSkillLevel { get; }
    public int SaveDc { get; }
    public StringName SaveDcMode { get; }
    public StringName SaveDcSourceAbility { get; }
    public StringName SaveAbility { get; }
    public bool SavePartialOnSuccess { get; }
    public StringName SaveTag { get; }
    public int ThresholdBaseValue { get; }
    public int ThresholdLevelAnchor { get; }
    public int ThresholdLevelBonusPerDelta { get; }
    public int ThresholdMaxHpRatioPercent { get; }
    public int ThresholdCapMaxHpRatioPercent { get; }
    public int SoulFractureDurationTu { get; }
    public int HealMultiplierPercent { get; }
    public int ShieldGainMultiplierPercent { get; }
    public int AppliedStatusDurationTu { get; }
    public int DurationTu { get; }
    public int TickIntervalTu { get; }
    public IReadOnlyList<StringName> EffectTags { get; }
    public StringName TriggerCondition { get; }
    public int Power { get; }
    public int RangeBonus { get; }
    public int ForcedMoveDistance { get; }
    public int JumpBaseBudget { get; }
    public double JumpStrScale { get; }
    public double JumpArcRatio { get; }
    public int JumpRangeMultiplier { get; }
    public int DiceSidesBase { get; }
    public int DiceSidesPerConstitutionMod { get; }
    public int DiceSidesPerWillpowerMod { get; }
    public IReadOnlyDictionary<string, object> Parameters { get; }
    public IReadOnlyList<StringName> EffectCategories { get; }
    public bool AllowRepeatHitsAcrossSteps { get; }
    public StringName TickEffectType { get; }
    public StringName LifetimePolicy { get; }
    public int MoveCostDelta { get; }
    public StringName RenderOverlayId { get; }
    public int OverlayPriority { get; }
    public string DisplayName { get; }
    private readonly BattleAttackRollModifierSpec _accuracyModifierSpec;
    public BattleAttackRollModifierSpec AccuracyModifierSpec =>
        _accuracyModifierSpec?.Clone();
    public StringName DoesNotStackWithStatusId { get; }
    public IReadOnlyList<StringName> DoesNotStackWithStatusIds { get; }
    public IReadOnlyList<StringName> DamageTags { get; }
    public IReadOnlyList<StringName> MitigationBypassDamageTags { get; }
    public IReadOnlyList<StringName> MitigationBypassTiers { get; }
    public bool UseWeaponPhysicalDamageTag { get; }
    public bool ResolveAsWeaponAttack { get; }
    public bool StopOnMiss { get; }
    public bool StopOnTargetDown { get; }
    public int FixedAttackCount { get; }
    public bool RemoveHarmful { get; }
    public bool RemoveHarmfulFromAllies { get; }
    public bool RemoveBeneficial { get; }
    public bool RemoveBeneficialFromEnemies { get; }
    public bool RequireDamageApplied { get; }
    public int MaxStatusRemoved { get; }
    public int MinHpAfterDamage { get; }
    public int DeathPreventionPriority { get; }
    public int AttackRollPenalty { get; }
    public int AttackRollBonus { get; }
    public bool AttackRollAdvantage { get; }
    public bool ConsumeOnNextAttackCheck { get; }
    public bool ConsumeOnNextSave { get; }
    public bool Undispellable { get; }
    public bool DispellableMagic { get; }
    public bool DispellableHarmfulMagic { get; }
    public bool DispellableBeneficialMagic { get; }
    public StringName MitigationTier { get; }
    public int SecondaryHitDcBase { get; }
    public int DebuffCountThreshold { get; }
    public int BaseHeal { get; }
    public int HealPerLevel { get; }
    public int ConModBase { get; }
    public int ConModPer2Levels { get; }
    public StringName BodySizeCategory { get; }
    public StringName StackBehavior { get; }
    public int StackLimit { get; }
    public StringName TriggerEvent { get; }
    public StringName TriggerStatusId { get; }
    public StringName ConsumedStatusId { get; }
    public StringName RequiredTargetStatusId { get; }
    public int RequiredTargetStatusMinStacks { get; }
    public StringName RequiredTargetStatusSourceSelector { get; }
    public int DicePerConsumedStack { get; }
    public int DiceSidesPerStack { get; }
    public int ApGain { get; }
    public int FreeMovePointsGain { get; }
    public bool CountsAsDebuffOverride { get; }
    public bool CountsAsDebuff { get; }
    public bool LockCounterattack { get; }
    public bool LockGuard { get; }
    public bool LockDodgeBonus { get; }
    public bool LockCrit { get; }
    public int SaveBonus { get; }
    public int ControlSaveBonus { get; }
    public int PassiveReduction { get; }
    public int ContentDr { get; }
    public int GuardBlock { get; }
    public int MainSkillLockOtherDebuffCount { get; }
    public IReadOnlyList<StringName> SaveAdvantageTags { get; }
    public IReadOnlyList<StringName> SaveDisadvantageTags { get; }
    public IReadOnlyList<StringName> SaveImmunityTags { get; }
    public IReadOnlyList<EquipmentSlotWeightDefinition> EquipmentDurabilitySlotWeights { get; }
    public IReadOnlyList<CombatDamageSegmentDefinition> ExtraDamageSegments { get; }
    public IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition> TargetDamageMultiplierRules { get; }
    public StringName PathStepAreaPattern { get; }
    internal BattleAreaPattern PathStepAreaPatternKind =>
        BattleTypedNames.ToAreaPattern(PathStepAreaPattern);
    public int PathStepRadius { get; }
    public string PathStepLogLabel { get; }
    public StringName RepeatHitStatusId { get; }
    public int RepeatHitStatusThreshold { get; }
    public int RepeatHitStatusMinSkillLevel { get; }
    public int RepeatHitStatusPower { get; }
    public int RepeatHitStatusDurationTu { get; }
    public string RepeatHitStatusLogTemplate { get; }
    internal BattleEffectKind EffectKind => BattleTypedNames.ToEffectKind(EffectType);
    internal BattleDamageBonusConditionKind BonusConditionKind =>
        BattleTypedNames.ToDamageBonusConditionKind(BonusCondition);
    internal CombatEffectTriggerCondition TriggerConditionKind =>
        CombatEffectContentRules.ToTriggerCondition(TriggerCondition);
    internal CombatEffectTriggerEvent TriggerEventKind =>
        CombatEffectContentRules.ToTriggerEvent(TriggerEvent);
    internal BattleForcedMoveMode ForcedMoveModeKind =>
        BattleTypedNames.ToForcedMoveMode(ForcedMoveMode);
    internal BattleSaveDcMode SaveDcModeKind =>
        BattleSaveContentRules.ToSaveDcMode(SaveDcMode);

    internal int GetIntParamTyped(string key, int fallback = 0)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return fallback;
        }
        if (
            Parameters.TryGetValue(key, out object value)
            && value is long intValue
            && intValue >= int.MinValue
            && intValue <= int.MaxValue
        )
            return (int)intValue;
        return fallback;
    }

    internal StringName GetStringNameParamTyped(string key, StringName fallback = default)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return fallback;
        }
        if (Parameters.TryGetValue(key, out object value))
        {
            StringName normalized = value switch
            {
                StringName stringName => stringName,
                string text => new StringName(text),
                _ => default,
            };
            return normalized != "" ? normalized : fallback;
        }
        return fallback;
    }

    internal double GetFloatParamTyped(string key, double fallback = 0.0)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return fallback;
        }
        if (Parameters.TryGetValue(key, out object value))
        {
            return value switch
            {
                long intValue => intValue,
                double floatValue => floatValue,
                _ => fallback,
            };
        }
        return fallback;
    }

    internal bool HasEffectTagTyped(StringName tag)
    {
        if (tag == "" || EffectTags == null)
        {
            return false;
        }
        foreach (StringName effectTag in EffectTags)
        {
            if (effectTag == tag)
            {
                return true;
            }
        }
        return false;
    }

    internal IReadOnlyList<StringName> GetStringNameListParamTyped(string key)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return System.Array.Empty<StringName>();
        }
        if (Parameters.TryGetValue(key, out object value))
        {
            if (value is not IReadOnlyList<object> values)
                return System.Array.Empty<StringName>();
            var result = new List<StringName>(values.Count);
            foreach (object entry in values)
            {
                StringName normalized = entry switch
                {
                    StringName stringName => stringName,
                    string text => new StringName(text),
                    _ => default,
                };
                if (normalized != "")
                    result.Add(normalized);
            }
            return result.Count == 0
                ? System.Array.Empty<StringName>()
                : new ReadOnlyCollection<StringName>(result);
        }
        return System.Array.Empty<StringName>();
    }

    internal IReadOnlyDictionary<StringName, int> GetStringNameIntMapParamTyped(string key)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return new Dictionary<StringName, int>();
        }
        if (
            !Parameters.TryGetValue(key, out object value)
            || value is not IReadOnlyDictionary<string, object> dictionary
        )
        {
            return new Dictionary<StringName, int>();
        }
        var result = new Dictionary<StringName, int>();
        foreach ((string rawKey, object rawValue) in dictionary)
        {
            StringName id = new(rawKey);
            if (id == "")
                continue;
            if (
                rawValue is long intValue
                && intValue >= int.MinValue
                && intValue <= int.MaxValue
            )
                result[id] = (int)intValue;
        }
        return result;
    }

    internal CombatEffectDefinition WithEffectType(StringName effectType)
    {
        return new CombatEffectDefinition(
            effectType,
            EffectTargetTeamFilter,
            StatusId,
            SaveFailureStatusId,
            TerrainEffectId,
            TerrainReplaceTo,
            HeightDelta,
            RequiresWeapon,
            AddWeaponDice,
            PreventRepeatTarget,
            ForcedMoveMode,
            MinSkillLevel,
            MaxSkillLevel,
            DamageTag,
            DamageRatioPercent,
            PreResistanceDamageMultiplier,
            BonusCondition,
            HpRatioThresholdPercent,
            DamageCategory,
            DrBypassTag,
            DiceCount,
            DiceSides,
            DiceBonus,
            BonusDamageDiceCount,
            BonusDamageDiceSides,
            BonusDamageDiceBonus,
            SaveDc,
            SaveDcMode,
            SaveDcSourceAbility,
            SaveAbility,
            SavePartialOnSuccess,
            SaveTag,
            ThresholdBaseValue,
            ThresholdLevelAnchor,
            ThresholdLevelBonusPerDelta,
            ThresholdMaxHpRatioPercent,
            ThresholdCapMaxHpRatioPercent,
            SoulFractureDurationTu,
            HealMultiplierPercent,
            ShieldGainMultiplierPercent,
            AppliedStatusDurationTu,
            DurationTu,
            TickIntervalTu,
            EffectTags,
            TriggerCondition,
            Power,
            RangeBonus,
            ForcedMoveDistance,
            JumpBaseBudget,
            JumpStrScale,
            JumpArcRatio,
            JumpRangeMultiplier,
            DiceSidesBase,
            DiceSidesPerConstitutionMod,
            DiceSidesPerWillpowerMod,
            Parameters,
            EffectCategories,
            AllowRepeatHitsAcrossSteps,
            TickEffectType,
            LifetimePolicy,
            MoveCostDelta,
            RenderOverlayId,
            OverlayPriority,
            DisplayName,
            AccuracyModifierSpec,
            DoesNotStackWithStatusId,
            DoesNotStackWithStatusIds,
            DamageTags,
            UseWeaponPhysicalDamageTag,
            ResolveAsWeaponAttack,
            StopOnMiss,
            StopOnTargetDown,
            RemoveHarmful,
            RemoveHarmfulFromAllies,
            RemoveBeneficial,
            RemoveBeneficialFromEnemies,
            RequireDamageApplied,
            MaxStatusRemoved,
            MinHpAfterDamage,
            DeathPreventionPriority,
            AttackRollPenalty,
            Undispellable,
            DispellableMagic,
            DispellableHarmfulMagic,
            DispellableBeneficialMagic,
            MitigationTier,
            SecondaryHitDcBase,
            DebuffCountThreshold,
            BaseHeal,
            HealPerLevel,
            ConModBase,
            ConModPer2Levels,
            BodySizeCategory,
            StackBehavior,
            StackLimit,
            TriggerEvent,
            TriggerStatusId,
            ConsumedStatusId,
            RequiredTargetStatusId,
            RequiredTargetStatusMinStacks,
            DicePerConsumedStack,
            DiceSidesPerStack,
            ApGain,
            FreeMovePointsGain,
            CountsAsDebuffOverride,
            CountsAsDebuff,
            LockCounterattack,
            LockGuard,
            LockDodgeBonus,
            LockCrit,
            SaveBonus,
            ControlSaveBonus,
            PassiveReduction,
            ContentDr,
            GuardBlock,
            MainSkillLockOtherDebuffCount,
            SaveAdvantageTags,
            SaveDisadvantageTags,
            SaveImmunityTags,
            EquipmentDurabilitySlotWeights,
            RequiredTargetStatusSourceSelector,
            BonusConditionCreatureTypeTag,
            MitigationBypassDamageTags,
            MitigationBypassTiers,
            ExtraDamageSegments,
            TargetDamageMultiplierRules,
            attackRollAdvantage: AttackRollAdvantage,
            attackRollBonus: AttackRollBonus,
            consumeOnNextAttackCheck: ConsumeOnNextAttackCheck,
            consumeOnNextSave: ConsumeOnNextSave,
            sourceBoundWeaponBonusDamageDiceCount: SourceBoundWeaponBonusDamageDiceCount,
            sourceBoundWeaponBonusDamageDiceSides: SourceBoundWeaponBonusDamageDiceSides,
            sourceBoundWeaponBonusDamageDiceBonus: SourceBoundWeaponBonusDamageDiceBonus,
            chargeTrapImmunityMinSkillLevel: ChargeTrapImmunityMinSkillLevel,
            pathStepAreaPattern: PathStepAreaPattern,
            pathStepRadius: PathStepRadius,
            pathStepLogLabel: PathStepLogLabel,
            repeatHitStatusId: RepeatHitStatusId,
            repeatHitStatusThreshold: RepeatHitStatusThreshold,
            repeatHitStatusMinSkillLevel: RepeatHitStatusMinSkillLevel,
            repeatHitStatusPower: RepeatHitStatusPower,
            repeatHitStatusDurationTu: RepeatHitStatusDurationTu,
            repeatHitStatusLogTemplate: RepeatHitStatusLogTemplate,
            fixedAttackCount: FixedAttackCount,
            weaponDiceMultiplier: WeaponDiceMultiplier,
            bonusWeaponDiceMultiplier: BonusWeaponDiceMultiplier,
            bonusDamageSeparateEvent: BonusDamageSeparateEvent,
            meleeComboStackGainBonus: MeleeComboStackGainBonus,
            comboAttackBonusStatusId: ComboAttackBonusStatusId,
            comboAttackBonusStackDivisor: ComboAttackBonusStackDivisor,
            upkeepResource: UpkeepResource,
            upkeepIntervalTu: UpkeepIntervalTu,
            upkeepBaseCost: UpkeepBaseCost,
            upkeepEscalationIntervalTu: UpkeepEscalationIntervalTu,
            upkeepCostMultiplier: UpkeepCostMultiplier,
            breakOnHardControl: BreakOnHardControl,
            terminationStatusId: TerminationStatusId,
            terminationStatusDurationTu: TerminationStatusDurationTu,
            terminationAttackRollPenalty: TerminationAttackRollPenalty,
            terminationCooldownTu: TerminationCooldownTu
        );
    }

    internal CombatEffectDefinition WithWeaponDiceMultiplier(int multiplier) =>
        WithPreResistanceDamageMultiplier(
            PreResistanceDamageMultiplier,
            multiplier
        );

    internal CombatEffectDefinition WithPreResistanceDamageMultiplier(
        double multiplier,
        int? weaponDiceMultiplierOverride = null
    )
    {
        return new CombatEffectDefinition(
            EffectType,
            EffectTargetTeamFilter,
            StatusId,
            SaveFailureStatusId,
            TerrainEffectId,
            TerrainReplaceTo,
            HeightDelta,
            RequiresWeapon,
            AddWeaponDice,
            PreventRepeatTarget,
            ForcedMoveMode,
            MinSkillLevel,
            MaxSkillLevel,
            DamageTag,
            DamageRatioPercent,
            multiplier,
            BonusCondition,
            HpRatioThresholdPercent,
            DamageCategory,
            DrBypassTag,
            DiceCount,
            DiceSides,
            DiceBonus,
            BonusDamageDiceCount,
            BonusDamageDiceSides,
            BonusDamageDiceBonus,
            SaveDc,
            SaveDcMode,
            SaveDcSourceAbility,
            SaveAbility,
            SavePartialOnSuccess,
            SaveTag,
            ThresholdBaseValue,
            ThresholdLevelAnchor,
            ThresholdLevelBonusPerDelta,
            ThresholdMaxHpRatioPercent,
            ThresholdCapMaxHpRatioPercent,
            SoulFractureDurationTu,
            HealMultiplierPercent,
            ShieldGainMultiplierPercent,
            AppliedStatusDurationTu,
            DurationTu,
            TickIntervalTu,
            EffectTags,
            TriggerCondition,
            Power,
            RangeBonus,
            ForcedMoveDistance,
            JumpBaseBudget,
            JumpStrScale,
            JumpArcRatio,
            JumpRangeMultiplier,
            DiceSidesBase,
            DiceSidesPerConstitutionMod,
            DiceSidesPerWillpowerMod,
            Parameters,
            EffectCategories,
            AllowRepeatHitsAcrossSteps,
            TickEffectType,
            LifetimePolicy,
            MoveCostDelta,
            RenderOverlayId,
            OverlayPriority,
            DisplayName,
            AccuracyModifierSpec,
            DoesNotStackWithStatusId,
            DoesNotStackWithStatusIds,
            DamageTags,
            UseWeaponPhysicalDamageTag,
            ResolveAsWeaponAttack,
            StopOnMiss,
            StopOnTargetDown,
            RemoveHarmful,
            RemoveHarmfulFromAllies,
            RemoveBeneficial,
            RemoveBeneficialFromEnemies,
            RequireDamageApplied,
            MaxStatusRemoved,
            MinHpAfterDamage,
            DeathPreventionPriority,
            AttackRollPenalty,
            Undispellable,
            DispellableMagic,
            DispellableHarmfulMagic,
            DispellableBeneficialMagic,
            MitigationTier,
            SecondaryHitDcBase,
            DebuffCountThreshold,
            BaseHeal,
            HealPerLevel,
            ConModBase,
            ConModPer2Levels,
            BodySizeCategory,
            StackBehavior,
            StackLimit,
            TriggerEvent,
            TriggerStatusId,
            ConsumedStatusId,
            RequiredTargetStatusId,
            RequiredTargetStatusMinStacks,
            DicePerConsumedStack,
            DiceSidesPerStack,
            ApGain,
            FreeMovePointsGain,
            CountsAsDebuffOverride,
            CountsAsDebuff,
            LockCounterattack,
            LockGuard,
            LockDodgeBonus,
            LockCrit,
            SaveBonus,
            ControlSaveBonus,
            PassiveReduction,
            ContentDr,
            GuardBlock,
            MainSkillLockOtherDebuffCount,
            SaveAdvantageTags,
            SaveDisadvantageTags,
            SaveImmunityTags,
            EquipmentDurabilitySlotWeights,
            RequiredTargetStatusSourceSelector,
            BonusConditionCreatureTypeTag,
            MitigationBypassDamageTags,
            MitigationBypassTiers,
            ExtraDamageSegments,
            TargetDamageMultiplierRules,
            attackRollAdvantage: AttackRollAdvantage,
            attackRollBonus: AttackRollBonus,
            consumeOnNextAttackCheck: ConsumeOnNextAttackCheck,
            consumeOnNextSave: ConsumeOnNextSave,
            sourceBoundWeaponBonusDamageDiceCount: SourceBoundWeaponBonusDamageDiceCount,
            sourceBoundWeaponBonusDamageDiceSides: SourceBoundWeaponBonusDamageDiceSides,
            sourceBoundWeaponBonusDamageDiceBonus: SourceBoundWeaponBonusDamageDiceBonus,
            chargeTrapImmunityMinSkillLevel: ChargeTrapImmunityMinSkillLevel,
            pathStepAreaPattern: PathStepAreaPattern,
            pathStepRadius: PathStepRadius,
            pathStepLogLabel: PathStepLogLabel,
            repeatHitStatusId: RepeatHitStatusId,
            repeatHitStatusThreshold: RepeatHitStatusThreshold,
            repeatHitStatusMinSkillLevel: RepeatHitStatusMinSkillLevel,
            repeatHitStatusPower: RepeatHitStatusPower,
            repeatHitStatusDurationTu: RepeatHitStatusDurationTu,
            repeatHitStatusLogTemplate: RepeatHitStatusLogTemplate,
            fixedAttackCount: FixedAttackCount,
            weaponDiceMultiplier:
                weaponDiceMultiplierOverride ?? WeaponDiceMultiplier,
            bonusWeaponDiceMultiplier: BonusWeaponDiceMultiplier,
            bonusDamageSeparateEvent: BonusDamageSeparateEvent,
            meleeComboStackGainBonus: MeleeComboStackGainBonus,
            comboAttackBonusStatusId: ComboAttackBonusStatusId,
            comboAttackBonusStackDivisor: ComboAttackBonusStackDivisor,
            upkeepResource: UpkeepResource,
            upkeepIntervalTu: UpkeepIntervalTu,
            upkeepBaseCost: UpkeepBaseCost,
            upkeepEscalationIntervalTu: UpkeepEscalationIntervalTu,
            upkeepCostMultiplier: UpkeepCostMultiplier,
            breakOnHardControl: BreakOnHardControl,
            terminationStatusId: TerminationStatusId,
            terminationStatusDurationTu: TerminationStatusDurationTu,
            terminationAttackRollPenalty: TerminationAttackRollPenalty,
            terminationCooldownTu: TerminationCooldownTu
        );
    }

    internal static CombatEffectDefinition FromResource(
        CombatEffectDef source,
        string path
    )
    {
        return source == null
            ? null
            : new CombatEffectDefinition(
                source.effect_type,
                source.effect_target_team_filter,
                source.status_id,
                source.save_failure_status_id,
                source.terrain_effect_id,
                source.terrain_replace_to,
                source.height_delta,
                source.requires_weapon,
                source.add_weapon_dice,
                source.prevent_repeat_target,
                source.forced_move_mode,
                source.min_skill_level,
                source.max_skill_level,
                source.damage_tag,
                source.damage_ratio_percent,
                source.pre_resistance_damage_multiplier,
                source.bonus_condition,
                source.hp_ratio_threshold_percent,
                source.damage_category,
                source.dr_bypass_tag,
                source.dice_count,
                source.dice_sides,
                source.dice_bonus,
                source.bonus_damage_dice_count,
                source.bonus_damage_dice_sides,
                source.bonus_damage_dice_bonus,
                source.save_dc,
                source.save_dc_mode,
                source.save_dc_source_ability,
                source.save_ability,
                source.save_partial_on_success,
                source.save_tag,
                source.threshold_base_value,
                source.threshold_level_anchor,
                source.threshold_level_bonus_per_delta,
                source.threshold_max_hp_ratio_percent,
                source.threshold_cap_max_hp_ratio_percent,
                source.soul_fracture_duration_tu,
                source.heal_multiplier_percent,
                source.shield_gain_multiplier_percent,
                source.applied_status_duration_tu,
                source.duration_tu,
                source.tick_interval_tu,
                CopyStringNameArray(source.effect_tags),
                source.trigger_condition,
                source.power,
                source.range_bonus,
                source.forced_move_distance,
                source.jump_base_budget,
                source.jump_str_scale,
                source.jump_arc_ratio,
                source.jump_range_multiplier,
                source.dice_sides_base,
                source.dice_sides_per_constitution_mod,
                source.dice_sides_per_willpower_mod,
                ContentValueNormalizer.NormalizeDictionary(
                    source.@params,
                    $"{path}.params"
                ),
                CopyStringNameArray(source.effect_categories),
                source.allow_repeat_hits_across_steps,
                source.tick_effect_type,
                source.lifetime_policy,
                source.move_cost_delta,
                source.render_overlay_id,
                source.overlay_priority,
                source.display_name,
                source.accuracy_modifier_spec,
                source.does_not_stack_with_status_id,
                CopyStringNameArray(source.does_not_stack_with_status_ids),
                CopyStringNameArray(source.damage_tags),
                source.use_weapon_physical_damage_tag,
                source.resolve_as_weapon_attack,
                source.stop_on_miss,
                source.stop_on_target_down,
                source.remove_harmful,
                source.remove_harmful_from_allies,
                source.remove_beneficial,
                source.remove_beneficial_from_enemies,
                source.require_damage_applied,
                source.max_status_removed,
                source.min_hp_after_damage,
                source.death_prevention_priority,
                source.attack_roll_penalty,
                source.undispellable,
                source.dispellable_magic,
                source.dispellable_harmful_magic,
                source.dispellable_beneficial_magic,
                source.mitigation_tier,
                source.secondary_hit_dc_base,
                source.debuff_count_threshold,
                source.base_heal,
                source.heal_per_level,
                source.con_mod_base,
                source.con_mod_per_2_levels,
                source.body_size_category,
                source.stack_behavior,
                source.stack_limit,
                source.trigger_event,
                source.trigger_status_id,
                source.consumed_status_id,
                source.required_target_status_id,
                source.required_target_status_min_stacks,
                source.dice_per_consumed_stack,
                source.dice_sides_per_stack,
                source.ap_gain,
                source.free_move_points_gain,
                source.counts_as_debuff_override,
                source.counts_as_debuff,
                source.lock_counterattack,
                source.lock_guard,
                source.lock_dodge_bonus,
                source.lock_crit,
                source.save_bonus,
                source.control_save_bonus,
                source.passive_reduction,
                source.content_dr,
                source.guard_block,
                source.main_skill_lock_other_debuff_count,
                CopyStringNameArray(source.save_advantage_tags),
                CopyStringNameArray(source.save_disadvantage_tags),
                CopyStringNameArray(source.save_immunity_tags),
                ProjectEquipmentDurabilitySlotWeights(
                    source.equipment_durability_slot_weights
                ),
                source.required_target_status_source_selector,
                source.bonus_condition_creature_type_tag,
                CopyStringNameArray(source.mitigation_bypass_damage_tags),
                CopyStringNameArray(source.mitigation_bypass_tiers),
                CombatDamageSegmentDefinition.ProjectArray(source.extra_damage_segments),
                CombatTargetDamageMultiplierRuleDefinition.ProjectArray(
                    source.target_damage_multiplier_rules
                ),
                attackRollBonus: source.attack_roll_bonus,
                attackRollAdvantage: source.attack_roll_advantage,
                consumeOnNextAttackCheck: source.consume_on_next_attack_check,
                consumeOnNextSave: source.consume_on_next_save,
                sourceBoundWeaponBonusDamageDiceCount:
                    source.source_bound_weapon_bonus_damage_dice_count,
                sourceBoundWeaponBonusDamageDiceSides:
                    source.source_bound_weapon_bonus_damage_dice_sides,
                sourceBoundWeaponBonusDamageDiceBonus:
                    source.source_bound_weapon_bonus_damage_dice_bonus,
                chargeTrapImmunityMinSkillLevel:
                    source.charge_trap_immunity_min_skill_level,
                pathStepAreaPattern: source.path_step_area_pattern,
                pathStepRadius: source.path_step_radius,
                pathStepLogLabel: source.path_step_log_label,
                repeatHitStatusId: source.repeat_hit_status_id,
                repeatHitStatusThreshold: source.repeat_hit_status_threshold,
                repeatHitStatusMinSkillLevel: source.repeat_hit_status_min_skill_level,
                repeatHitStatusPower: source.repeat_hit_status_power,
                repeatHitStatusDurationTu: source.repeat_hit_status_duration_tu,
                repeatHitStatusLogTemplate: source.repeat_hit_status_log_template,
                fixedAttackCount: source.fixed_attack_count,
                weaponDiceMultiplier: source.weapon_dice_multiplier,
                bonusWeaponDiceMultiplier: source.bonus_weapon_dice_multiplier,
                bonusDamageSeparateEvent: source.bonus_damage_separate_event,
                meleeComboStackGainBonus: source.melee_combo_stack_gain_bonus,
                comboAttackBonusStatusId: source.combo_attack_bonus_status_id,
                comboAttackBonusStackDivisor: source.combo_attack_bonus_stack_divisor,
                upkeepResource: source.upkeep_resource,
                upkeepIntervalTu: source.upkeep_interval_tu,
                upkeepBaseCost: source.upkeep_base_cost,
                upkeepEscalationIntervalTu: source.upkeep_escalation_interval_tu,
                upkeepCostMultiplier: source.upkeep_cost_multiplier,
                breakOnHardControl: source.break_on_hard_control,
                terminationStatusId: source.termination_status_id,
                terminationStatusDurationTu: source.termination_status_duration_tu,
                terminationAttackRollPenalty: source.termination_attack_roll_penalty,
                terminationCooldownTu: source.termination_cooldown_tu
            );
    }

    private static IReadOnlyList<EquipmentSlotWeightDefinition> ProjectEquipmentDurabilitySlotWeights(
        Godot.Collections.Array<CombatEffectSlotWeightDef> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return System.Array.Empty<EquipmentSlotWeightDefinition>();
        }
        var result = new List<EquipmentSlotWeightDefinition>();
        foreach (CombatEffectSlotWeightDef value in values)
        {
            if (value == null)
            {
                continue;
            }
            StringName slotId = ProgressionDataUtils.to_string_name(value.slot_id);
            int weight = value.weight;
            if (slotId == "" || weight <= 0)
            {
                continue;
            }
            result.Add(
                new EquipmentSlotWeightDefinition
                {
                    SlotId = slotId,
                    Weight = weight,
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentSlotWeightDefinition>(result)
            : System.Array.Empty<EquipmentSlotWeightDefinition>();
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyStringNames;
        var result = new List<StringName>(values.Count);
        foreach (StringName value in values)
            result.Add(value);
        return new ReadOnlyCollection<StringName>(result);
    }

    private static IReadOnlyList<StringName> CopyStringNameArray(Godot.Collections.Array values)
    {
        if (values == null || values.Count == 0)
            return EmptyStringNames;
        var result = new List<StringName>(values.Count);
        foreach (object value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<StringName>(result)
            : EmptyStringNames;
    }

}
