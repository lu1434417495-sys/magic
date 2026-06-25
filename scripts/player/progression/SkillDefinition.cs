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
        CombatSkillDefinition combatProfile
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

    public bool CanUseInCombat() => CombatProfile != null;

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
            CombatSkillDefinition.FromResource(source.combat_profile)
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

public sealed class CombatSkillDefinition
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames =
        System.Array.Empty<StringName>();
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
    public IReadOnlyList<CombatCastVariantDefinition> CastVariants { get; }
    public IReadOnlyList<StringName> RequiredWeaponFamilies { get; }
    public IReadOnlyList<StringName> ExcludedWeaponFamilies { get; }
    public IReadOnlyList<StringName> ExcludedWeaponTypeIds { get; }
    public bool RequiresEquippedShield { get; }
    public int MasteryLowHpBonusMultiplier { get; }
    public int MasteryLowHpThresholdPercent { get; }

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
    public IReadOnlyDictionary<string, Variant> Parameters { get; }

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
            CopyVariantDictionary(source.@params)
        );
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
