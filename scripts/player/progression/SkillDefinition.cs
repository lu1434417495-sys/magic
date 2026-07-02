using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class SkillDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyList<AttributeModifierDefinition> EmptyAttributeModifiers =
        System.Array.Empty<AttributeModifierDefinition>();
    private static readonly IReadOnlyDictionary<StringName, int> EmptyStringNameIntMap =
        new ReadOnlyDictionary<StringName, int>(new Dictionary<StringName, int>());
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> EmptyLevelDescriptionConfigs =
        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>>(
            new Dictionary<int, IReadOnlyDictionary<string, Variant>>()
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
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> levelDescriptionConfigs,
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
        MasteryCurve = masteryCurve ?? System.Array.Empty<int>();
        Tags = tags ?? EmptyStringNames;
        LearnSource = learnSource;
        LearnRequirements = learnRequirements ?? EmptyStringNames;
        UnlockMode = unlockMode;
        KnowledgeRequirements = knowledgeRequirements ?? EmptyStringNames;
        SkillLevelRequirements = skillLevelRequirements ?? EmptyStringNameIntMap;
        AttributeRequirements = attributeRequirements ?? EmptyStringNameIntMap;
        AchievementRequirements = achievementRequirements ?? EmptyStringNames;
        UpgradeSourceSkillIds = upgradeSourceSkillIds ?? EmptyStringNames;
        RetainSourceSkillsOnUnlock = retainSourceSkillsOnUnlock;
        CoreSkillTransitionMode = coreSkillTransitionMode;
        MasterySources = masterySources ?? EmptyStringNames;
        GrowthTier = growthTier;
        AttributeGrowthProgress = attributeGrowthProgress ?? EmptyStringNameIntMap;
        PracticeTier = practiceTier;
        AttributeModifiers = attributeModifiers ?? EmptyAttributeModifiers;
        LevelDescriptionTemplate = levelDescriptionTemplate ?? "";
        LevelDescriptionConfigs = levelDescriptionConfigs ?? EmptyLevelDescriptionConfigs;
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
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> LevelDescriptionConfigs { get; }
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
        return new SkillDefinition(
            source.skill_id,
            source.display_name,
            source.icon_id,
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
            CopyLevelConfigMap(source.LevelDescriptionConfigsTyped),
            CombatSkillDefinition.FromResource(source.combat_profile),
            ContingencyAutomationDefinition.FromResource(source.contingency_automation_profile)
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
            AttributeModifierDefinition definition = AttributeModifierDefinition.FromResource(
                modifier
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<AttributeModifierDefinition>(result)
            : EmptyAttributeModifiers;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> CopyLevelConfigMap(
        IReadOnlyDictionary<int, Dictionary<string, Variant>> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyLevelDescriptionConfigs;
        var result = new Dictionary<int, IReadOnlyDictionary<string, Variant>>(values.Count);
        foreach ((int level, Dictionary<string, Variant> config) in values)
        {
            result[level] = CopyVariantMap(config);
        }
        return new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>>(result);
    }

    internal static IReadOnlyDictionary<string, Variant> CopyVariantMap(
        IReadOnlyDictionary<string, Variant> values
    )
    {
        if (values == null || values.Count == 0)
            return new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());
        return new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>(values));
    }
}

public sealed class AttributeModifierDefinition
{
    public AttributeModifierDefinition(
        StringName attributeId,
        StringName mode,
        int value,
        int valuePerRank,
        StringName sourceType,
        StringName sourceId
    )
    {
        AttributeId = attributeId;
        Mode = mode;
        Value = value;
        ValuePerRank = valuePerRank;
        SourceType = sourceType;
        SourceId = sourceId;
    }

    public StringName AttributeId { get; }
    public StringName Mode { get; }
    public int Value { get; }
    public int ValuePerRank { get; }
    public StringName SourceType { get; }
    public StringName SourceId { get; }

    public int GetValueForRank(int rank)
    {
        int normalizedRank = Mathf.Max(rank, 1);
        return Value + ValuePerRank * (normalizedRank - 1);
    }

    public bool IsPercent() => AttributeModifier.ToMode(Mode) == AttributeModifierMode.Percent;

    public bool IsFlat() => AttributeModifier.ToMode(Mode) == AttributeModifierMode.Flat;

    internal static AttributeModifierDefinition FromResource(AttributeModifier source)
    {
        return source == null
            ? null
            : new AttributeModifierDefinition(
                source.attribute_id,
                source.mode,
                source.value,
                source.value_per_rank,
                source.source_type,
                source.source_id
            );
    }
}

public sealed class ContingencyAutomationDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
    private static readonly IReadOnlyDictionary<string, Variant> EmptyBindings =
        new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());

    public ContingencyAutomationDefinition(
        bool canBeStoredInContingency,
        int minContingencySkillLevel,
        StringName effectCategory,
        IReadOnlyList<StringName> tags,
        int contingencyLoadOverride,
        IReadOnlyList<StringName> allowedTargetResolvers,
        bool requiresManualTargeting,
        IReadOnlyDictionary<string, Variant> allowedParameterBindings
    )
    {
        CanBeStoredInContingency = canBeStoredInContingency;
        MinContingencySkillLevel = minContingencySkillLevel;
        EffectCategory = effectCategory;
        Tags = tags ?? EmptyStringNames;
        ContingencyLoadOverride = contingencyLoadOverride;
        AllowedTargetResolvers = allowedTargetResolvers ?? EmptyStringNames;
        RequiresManualTargeting = requiresManualTargeting;
        AllowedParameterBindings = allowedParameterBindings ?? EmptyBindings;
    }

    public bool CanBeStoredInContingency { get; }
    public int MinContingencySkillLevel { get; }
    public StringName EffectCategory { get; }
    public IReadOnlyList<StringName> Tags { get; }
    public int ContingencyLoadOverride { get; }
    public IReadOnlyList<StringName> AllowedTargetResolvers { get; }
    public bool RequiresManualTargeting { get; }
    public IReadOnlyDictionary<string, Variant> AllowedParameterBindings { get; }

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
        ContingencyAutomationDef source
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
            CopyBindings(source.allowed_parameter_bindings)
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

    private static IReadOnlyDictionary<string, Variant> CopyBindings(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || source.Count == 0)
            return EmptyBindings;
        var result = new Dictionary<string, Variant>();
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (!string.IsNullOrEmpty(key))
                result[key] = source[rawKey];
        }
        return result.Count > 0
            ? new ReadOnlyDictionary<string, Variant>(result)
            : EmptyBindings;
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
    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> EmptyLevelOverrides =
        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>>(
            new Dictionary<int, IReadOnlyDictionary<string, Variant>>()
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
        int auraCost,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> levelOverrides,
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
        int masteryLowHpThresholdPercent
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
        AuraCost = auraCost;
        LevelOverrides = levelOverrides ?? EmptyLevelOverrides;
        MasteryTriggerMode = masteryTriggerMode;
        MasteryAmountMode = masteryAmountMode;
        SpellFateMode = spellFateMode;
        SpellCriticalMode = spellCriticalMode;
        SpellCriticalMpRefundPercent = Mathf.Clamp(spellCriticalMpRefundPercent, 0, 100);
        FumbleProtectionCurve = fumbleProtectionCurve ?? System.Array.Empty<int>();
        FumbleProtectionExtraMpPercent = Mathf.Max(fumbleProtectionExtraMpPercent, 0);
        BacklashMode = backlashMode;
        BacklashTargetFilter = backlashTargetFilter;
        BacklashOffsetRadius = backlashOffsetRadius;
        AreaOriginMode = areaOriginMode;
        AreaDirectionMode = areaDirectionMode;
        AiTags = aiTags ?? EmptyStringNames;
        DeliveryCategories = deliveryCategories ?? EmptyStringNames;
        SpecialResolutionProfileId = specialResolutionProfileId;
        TargetSelectionMode = targetSelectionMode;
        MinTargetCount = minTargetCount;
        MaxTargetCount = maxTargetCount;
        AllowRepeatTarget = allowRepeatTarget;
        MaxHitsPerTarget = maxHitsPerTarget;
        SelectionOrderMode = selectionOrderMode;
        EffectDefinitions = effectDefinitions ?? EmptyEffectDefinitions;
        PassiveEffectDefinitions = passiveEffectDefinitions ?? EmptyEffectDefinitions;
        CastVariants = castVariants ?? EmptyCastVariants;
        RequiredWeaponFamilies = requiredWeaponFamilies ?? EmptyStringNames;
        ExcludedWeaponFamilies = excludedWeaponFamilies ?? EmptyStringNames;
        ExcludedWeaponTypeIds = excludedWeaponTypeIds ?? EmptyStringNames;
        RequiresEquippedShield = requiresEquippedShield;
        MasteryLowHpBonusMultiplier = masteryLowHpBonusMultiplier;
        MasteryLowHpThresholdPercent = masteryLowHpThresholdPercent;
    }

    public StringName SkillId { get; }
    public StringName TargetMode { get; }
    public StringName TargetTeamFilter { get; }
    public StringName RangePattern { get; }
    public int RangeValue { get; }
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
    public int AuraCost { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> LevelOverrides { get; }
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
    public StringName SpecialResolutionProfileId { get; }
    public StringName TargetSelectionMode { get; }
    public int MinTargetCount { get; }
    public int MaxTargetCount { get; }
    public bool AllowRepeatTarget { get; }
    public int MaxHitsPerTarget { get; }
    public StringName SelectionOrderMode { get; }
    public IReadOnlyList<CombatEffectDefinition> EffectDefinitions { get; }
    public IReadOnlyList<CombatEffectDefinition> PassiveEffectDefinitions { get; }
    public IReadOnlyList<CombatCastVariantDefinition> CastVariants { get; }
    public IReadOnlyList<StringName> RequiredWeaponFamilies { get; }
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
        IReadOnlyDictionary<string, Variant> overrides = BuildLevelOverride(skillLevel);
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
        IReadOnlyDictionary<string, Variant> overrides = BuildLevelOverride(skillLevel);
        return overrides != null
            && overrides.TryGetValue("pending_cast_binding_mode", out Variant rawValue)
            ? BattleTypedNames.ToPendingCastBindingMode(
                ProgressionDataUtils.to_string_name(rawValue)
            )
            : PendingCastBindingModeKind;
    }

    public StringName GetEffectiveAreaPattern(int skillLevel)
    {
        IReadOnlyDictionary<string, Variant> overrides = BuildLevelOverride(skillLevel);
        return overrides != null && overrides.TryGetValue("area_pattern", out Variant rawValue)
            ? ProgressionDataUtils.to_string_name(rawValue)
            : AreaPattern;
    }

    public int GetEffectiveAreaValue(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "area_value", AreaValue);

    public int GetEffectiveRangeValue(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "range_value", RangeValue);

    public int GetEffectiveMaxTargetCount(int skillLevel) =>
        ReadIntOverride(BuildLevelOverride(skillLevel), "max_target_count", MaxTargetCount);

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
            MasteryLowHpThresholdPercent
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
            MasteryLowHpThresholdPercent
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

    internal static CombatSkillDefinition FromResource(CombatSkillDef source)
    {
        if (source == null)
            return null;
        return new CombatSkillDefinition(
            source.skill_id,
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
            source.aura_cost,
            ProjectLevelOverrides(source.level_overrides),
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
            ProjectEffectDefinitions(source.effect_defs),
            ProjectEffectDefinitions(source.passive_effect_defs),
            ProjectCastVariants(source.cast_variants),
            CopyStringNameArray(source.required_weapon_families),
            CopyStringNameArray(source.excluded_weapon_families),
            CopyStringNameArray(source.excluded_weapon_type_ids),
            source.requires_equipped_shield,
            source.mastery_low_hp_bonus_multiplier,
            source.mastery_low_hp_threshold_percent
        );
    }

    private IReadOnlyDictionary<string, Variant> BuildLevelOverride(int skillLevel)
    {
        if (LevelOverrides.Count == 0)
            return SkillDefinition.CopyVariantMap(null);
        var merged = new Dictionary<string, Variant>();
        var eligible = new List<int>();
        foreach (int level in LevelOverrides.Keys)
        {
            if (level >= 0 && level <= skillLevel)
                eligible.Add(level);
        }
        eligible.Sort();
        foreach (int level in eligible)
        {
            foreach ((string key, Variant value) in LevelOverrides[level])
                merged[key] = value;
        }
        return new ReadOnlyDictionary<string, Variant>(merged);
    }

    private static int ReadIntOverride(
        IReadOnlyDictionary<string, Variant> overrides,
        string key,
        int fallback
    )
    {
        return TryReadIntOverride(overrides, key, out int value) ? value : fallback;
    }

    private static bool TryReadIntOverride(
        IReadOnlyDictionary<string, Variant> overrides,
        string key,
        out int value
    )
    {
        if (overrides != null && overrides.TryGetValue(key, out Variant rawValue))
        {
            if (rawValue.VariantType == Variant.Type.Int)
            {
                value = rawValue.AsInt32();
                return true;
            }
            if (rawValue.VariantType == Variant.Type.Float)
            {
                value = (int)rawValue.AsDouble();
                return true;
            }
        }
        value = 0;
        return false;
    }

    private static IReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>> ProjectLevelOverrides(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || source.Count == 0)
            return EmptyLevelOverrides;
        var result = new Dictionary<int, IReadOnlyDictionary<string, Variant>>();
        foreach (Variant rawKey in source.Keys)
        {
            if (!TryReadLevelKey(rawKey, out int level))
                continue;
            Variant rawValue = source[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
                continue;
            result[level] = CopyVariantDictionary(rawValue.AsGodotDictionary());
        }
        return result.Count > 0
            ? new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Variant>>(result)
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

    private static IReadOnlyDictionary<string, Variant> CopyVariantDictionary(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || source.Count == 0)
            return SkillDefinition.CopyVariantMap(null);
        var result = new Dictionary<string, Variant>();
        foreach (Variant rawKey in source.Keys)
        {
            string key = ReadVariantKey(rawKey);
            if (string.IsNullOrEmpty(key))
                continue;
            result[key] = source[rawKey];
        }
        return new ReadOnlyDictionary<string, Variant>(result);
    }

    private static string ReadVariantKey(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.String => rawKey.AsString(),
            Variant.Type.StringName => rawKey.AsStringName().ToString(),
            _ => "",
        };
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
        Godot.Collections.Array<CombatCastVariantDef> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyCastVariants;
        var result = new List<CombatCastVariantDefinition>(values.Count);
        foreach (CombatCastVariantDef variant in values)
        {
            CombatCastVariantDefinition definition = CombatCastVariantDefinition.FromResource(
                variant
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.Count > 0
            ? new ReadOnlyCollection<CombatCastVariantDefinition>(result)
            : EmptyCastVariants;
    }

    private static IReadOnlyList<CombatEffectDefinition> ProjectEffectDefinitions(
        Godot.Collections.Array<CombatEffectDef> values
    )
    {
        if (values == null || values.Count == 0)
            return EmptyEffectDefinitions;
        var result = new List<CombatEffectDefinition>(values.Count);
        foreach (CombatEffectDef effect in values)
        {
            CombatEffectDefinition definition = CombatEffectDefinition.FromResource(effect);
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
        IReadOnlyDictionary<string, Variant> parameters
    )
    {
        VariantId = variantId;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        MinSkillLevel = minSkillLevel;
        TargetMode = targetMode;
        FootprintPattern = footprintPattern;
        RequiredCoordCount = requiredCoordCount;
        AllowedBaseTerrains = allowedBaseTerrains ?? System.Array.Empty<StringName>();
        EffectDefinitions =
            effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>();
        Parameters =
            parameters
            ?? new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());
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
    public IReadOnlyDictionary<string, Variant> Parameters { get; }
    internal BattleTargetMode TargetModeKind => BattleTypedNames.ToTargetMode(TargetMode);
    internal CombatCastFootprintPattern FootprintPatternKind =>
        CombatSkillTargetingContentRules.ToFootprintPattern(FootprintPattern);

    internal static CombatCastVariantDefinition FromResource(CombatCastVariantDef source)
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
            CopyEffectDefinitions(source.effect_defs),
            CopyVariantDictionary(source.@params)
        );
    }

    private static IReadOnlyList<CombatEffectDefinition> CopyEffectDefinitions(
        Godot.Collections.Array<CombatEffectDef> values
    )
    {
        if (values == null || values.Count == 0)
            return System.Array.Empty<CombatEffectDefinition>();
        var result = new List<CombatEffectDefinition>(values.Count);
        foreach (CombatEffectDef effect in values)
        {
            CombatEffectDefinition definition = CombatEffectDefinition.FromResource(effect);
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

    private static IReadOnlyDictionary<string, Variant> CopyVariantDictionary(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || source.Count == 0)
            return new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());
        var result = new Dictionary<string, Variant>();
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (!string.IsNullOrEmpty(key))
                result[key] = source[rawKey];
        }
        return new ReadOnlyDictionary<string, Variant>(result);
    }
}

public sealed class CombatEffectDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();

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
        IReadOnlyDictionary<string, Variant> parameters = null,
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
        IReadOnlyList<StringName> saveTags = null,
        IReadOnlyList<EquipmentSlotWeightDefinition> equipmentDurabilitySlotWeights = null
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
        BonusCondition = bonusCondition;
        HpRatioThresholdPercent = hpRatioThresholdPercent;
        DamageCategory = damageCategory;
        DrBypassTag = drBypassTag;
        DiceCount = diceCount;
        DiceSides = diceSides;
        DiceBonus = diceBonus;
        BonusDamageDiceCount = bonusDamageDiceCount;
        BonusDamageDiceSides = bonusDamageDiceSides;
        BonusDamageDiceBonus = bonusDamageDiceBonus;
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
        EffectTags = effectTags ?? EmptyStringNames;
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
        Parameters =
            parameters
            ?? new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());
        EffectCategories = effectCategories ?? EmptyStringNames;
        AllowRepeatHitsAcrossSteps = allowRepeatHitsAcrossSteps;
        TickEffectType = tickEffectType;
        LifetimePolicy = lifetimePolicy == "" ? (StringName)"timed" : lifetimePolicy;
        MoveCostDelta = moveCostDelta;
        RenderOverlayId = renderOverlayId;
        OverlayPriority = overlayPriority;
        DisplayName = displayName ?? "";
        AccuracyModifierSpec = accuracyModifierSpec?.Clone();
        DoesNotStackWithStatusId = doesNotStackWithStatusId;
        DoesNotStackWithStatusIds = doesNotStackWithStatusIds ?? EmptyStringNames;
        DamageTags = damageTags ?? EmptyStringNames;
        UseWeaponPhysicalDamageTag = useWeaponPhysicalDamageTag;
        ResolveAsWeaponAttack = resolveAsWeaponAttack;
        StopOnMiss = stopOnMiss;
        StopOnTargetDown = stopOnTargetDown;
        RemoveHarmful = removeHarmful;
        RemoveHarmfulFromAllies = removeHarmfulFromAllies;
        RemoveBeneficial = removeBeneficial;
        RemoveBeneficialFromEnemies = removeBeneficialFromEnemies;
        RequireDamageApplied = requireDamageApplied;
        MaxStatusRemoved = maxStatusRemoved;
        MinHpAfterDamage = minHpAfterDamage;
        DeathPreventionPriority = deathPreventionPriority;
        AttackRollPenalty = attackRollPenalty;
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
        SaveAdvantageTags = saveAdvantageTags ?? EmptyStringNames;
        SaveDisadvantageTags = saveDisadvantageTags ?? EmptyStringNames;
        SaveImmunityTags = saveImmunityTags ?? EmptyStringNames;
        SaveTags = saveTags ?? EmptyStringNames;
        EquipmentDurabilitySlotWeights =
            equipmentDurabilitySlotWeights ?? System.Array.Empty<EquipmentSlotWeightDefinition>();
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
    public StringName BonusCondition { get; }
    public int HpRatioThresholdPercent { get; }
    public StringName DamageCategory { get; }
    public StringName DrBypassTag { get; }
    public int DiceCount { get; }
    public int DiceSides { get; }
    public int DiceBonus { get; }
    public int BonusDamageDiceCount { get; }
    public int BonusDamageDiceSides { get; }
    public int BonusDamageDiceBonus { get; }
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
    public IReadOnlyDictionary<string, Variant> Parameters { get; }
    public IReadOnlyList<StringName> EffectCategories { get; }
    public bool AllowRepeatHitsAcrossSteps { get; }
    public StringName TickEffectType { get; }
    public StringName LifetimePolicy { get; }
    public int MoveCostDelta { get; }
    public StringName RenderOverlayId { get; }
    public int OverlayPriority { get; }
    public string DisplayName { get; }
    public BattleAttackRollModifierSpec AccuracyModifierSpec { get; }
    public StringName DoesNotStackWithStatusId { get; }
    public IReadOnlyList<StringName> DoesNotStackWithStatusIds { get; }
    public IReadOnlyList<StringName> DamageTags { get; }
    public bool UseWeaponPhysicalDamageTag { get; }
    public bool ResolveAsWeaponAttack { get; }
    public bool StopOnMiss { get; }
    public bool StopOnTargetDown { get; }
    public bool RemoveHarmful { get; }
    public bool RemoveHarmfulFromAllies { get; }
    public bool RemoveBeneficial { get; }
    public bool RemoveBeneficialFromEnemies { get; }
    public bool RequireDamageApplied { get; }
    public int MaxStatusRemoved { get; }
    public int MinHpAfterDamage { get; }
    public int DeathPreventionPriority { get; }
    public int AttackRollPenalty { get; }
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
    public IReadOnlyList<StringName> SaveTags { get; }
    public IReadOnlyList<EquipmentSlotWeightDefinition> EquipmentDurabilitySlotWeights { get; }
    internal BattleEffectKind EffectKind => BattleTypedNames.ToEffectKind(EffectType);
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
        if (Parameters.TryGetValue(key, out Variant value))
        {
            return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
        }
        return fallback;
    }

    internal StringName GetStringNameParamTyped(string key, StringName fallback = default)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return fallback;
        }
        if (Parameters.TryGetValue(key, out Variant value))
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
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
        if (Parameters.TryGetValue(key, out Variant value))
        {
            return value.VariantType switch
            {
                Variant.Type.Int => value.AsInt64(),
                Variant.Type.Float => value.AsDouble(),
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
        if (Parameters.TryGetValue(key, out Variant value))
        {
            return ProgressionDataUtils.to_string_name_array(value);
        }
        return System.Array.Empty<StringName>();
    }

    internal IReadOnlyDictionary<StringName, int> GetStringNameIntMapParamTyped(string key)
    {
        if (string.IsNullOrEmpty(key) || Parameters == null)
        {
            return new Dictionary<StringName, int>();
        }
        if (!Parameters.TryGetValue(key, out Variant value) || value.VariantType != Variant.Type.Dictionary)
        {
            return new Dictionary<StringName, int>();
        }
        var result = new Dictionary<StringName, int>();
        Godot.Collections.Dictionary dictionary = value.AsGodotDictionary();
        foreach (Variant rawKey in dictionary.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName id = rawKey.AsStringName();
            if (id == "")
            {
                continue;
            }
            Variant rawValue = dictionary[rawKey];
            if (rawValue.VariantType == Variant.Type.Int)
            {
                result[id] = rawValue.AsInt32();
            }
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
            SaveTags,
            EquipmentDurabilitySlotWeights
        );
    }

    internal CombatEffectDefinition WithPreResistanceDamageMultiplier(double multiplier)
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
            SaveTags,
            EquipmentDurabilitySlotWeights
        );
    }

    internal static CombatEffectDefinition FromResource(CombatEffectDef source)
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
                CopyVariantDictionary(source.@params),
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
                CopyStringNameArray(source.save_tags),
                ProjectEquipmentDurabilitySlotWeights(
                    source.equipment_durability_slot_weights
                )
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

    private static IReadOnlyDictionary<string, Variant> CopyVariantDictionary(
        Godot.Collections.Dictionary source
    )
    {
        if (source == null || source.Count == 0)
            return new ReadOnlyDictionary<string, Variant>(new Dictionary<string, Variant>());
        var result = new Dictionary<string, Variant>();
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (!string.IsNullOrEmpty(key))
                result[key] = source[rawKey];
        }
        return new ReadOnlyDictionary<string, Variant>(result);
    }
}
