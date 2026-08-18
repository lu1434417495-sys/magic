#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal readonly record struct SkillImportStringName
{
    private SkillImportStringName(string value) => Value = value;

    internal string Value { get; }

    internal static bool TryCreate(string? value, out SkillImportStringName result)
    {
        result = default;
        if (value == "")
        {
            result = new SkillImportStringName("");
            return true;
        }
        if (!SkillJsonImportValueRules.IsSnakeCaseId(value))
            return false;
        result = new SkillImportStringName(value!);
        return true;
    }

    public override string ToString() => Value ?? "";
}

internal enum SkillImportUnlockMode { Standard, CompositeUpgrade }
internal enum SkillImportCoreSkillTransitionMode { Inherit, ReplaceSourcesWithResult }
internal enum SkillImportProgressionTier { None, Basic, Intermediate, Advanced, Ultimate }
internal enum AttributeModifierImportMode { Flat, Percent }
internal enum CombatWeaponRangePolicyImportKind { CurrentWeapon, Configured, CurrentWeaponPlusConfigured }
internal enum CombatMasteryTriggerImportKind
{
    SkillDamageDiceMax,
    WeaponAttackQuality,
    DamageDealt,
    StatusApplied,
    EffectApplied,
    IncomingPhysicalHit,
    SecondaryHit,
    SourceBoundWeaponBonusDamage,
    TerrainEffectiveTrigger,
}
internal enum CombatMasteryAmountImportKind { PerTargetRank, PerCastHpRatio }
internal enum CombatSpellFateImportKind { None, ControlRoll }
internal enum CombatSpellCriticalImportKind { None, MpRefund }
internal enum CombatBacklashImportKind { None, GroundAnchorDrift }
internal enum CombatAreaOriginImportKind { Target, Caster, AnchorCoord }
internal enum CombatAreaDirectionImportKind { TargetVector, TargetVectorPerpendicular, CasterFacing }
internal enum CombatProjectileImportKind { Inherit, None, Nonmagical, Magical, CurrentWeapon }
internal enum CombatBaseProjectileImportKind { None, Nonmagical, Magical, CurrentWeapon }
internal enum CombatTargetSelectionImportKind
{
    SingleUnit,
    MultiUnit,
    RandomChain,
    Self,
    SingleCoord,
    CoordPair,
}
internal enum DamageTagImportKind
{
    PhysicalSlash, PhysicalPierce, PhysicalBlunt, Fire, Freeze, Lightning,
    NegativeEnergy, Force, Psychic, Radiant, Thunder, Magic, Acid, Poison,
}
internal enum BattleTerrainImportKind
{
    Land, Forest, Water, ShallowWater, FlowingWater, DeepWater, Ice, Mud, Spike,
}
internal enum CombatUnitTargetResolutionImportKind { Aggregate, OrderedSlots }
internal enum CombatSelectionOrderImportKind { Stable, Manual }
internal enum CombatCastFootprintImportKind { Single, Line2, Square2, Unordered }
internal enum CombatSaveAbilityImportKind
{
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
}

internal abstract record ContingencyParameterBindingImportValue;
internal sealed record ContingencyBoolBindingImportValue(bool Value)
    : ContingencyParameterBindingImportValue;
internal sealed record ContingencyIntBindingImportValue(long Value)
    : ContingencyParameterBindingImportValue;
internal sealed record ContingencyFloatBindingImportValue(double Value)
    : ContingencyParameterBindingImportValue;
internal sealed record ContingencyStringBindingImportValue(string Value)
    : ContingencyParameterBindingImportValue;
internal sealed record ContingencyStringListBindingImportValue
    : ContingencyParameterBindingImportValue
{
    internal ContingencyStringListBindingImportValue(IEnumerable<string> values) =>
        Values = SkillImportCollections.Freeze(values);

    internal IReadOnlyList<string> Values { get; }
}

internal readonly record struct SkillImportAssetId
{
    private SkillImportAssetId(string value) => Value = value;

    internal string Value { get; }

    internal static SkillImportAssetId FromResource(string? value) => new(value ?? "");

    internal static bool TryCreate(string? value, out SkillImportAssetId result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        foreach (char character in value)
        {
            if (
                !char.IsAsciiLetterOrDigit(character)
                && character is not ('_' or '-' or '.')
            )
                return false;
        }
        result = new SkillImportAssetId(value);
        return true;
    }

    public override string ToString() => Value ?? "";
}

internal readonly record struct SkillImportStatId
{
    private SkillImportStatId(string value) => Value = value;
    internal string Value { get; }

    internal static bool TryCreate(string? value, out SkillImportStatId result)
    {
        result = default;
        if (value == "")
        {
            result = new SkillImportStatId("");
            return true;
        }
        string[] segments = (value ?? "").Split(':');
        if (
            segments.Length is < 1 or > 2
            || !SkillJsonImportValueRules.IsSnakeCaseId(segments[0])
            || (segments.Length == 2 && !SkillJsonImportValueRules.IsSnakeCaseId(segments[1]))
        )
            return false;
        result = new SkillImportStatId(value!);
        return true;
    }
}

internal static class SkillImportCollections
{
    internal static IReadOnlyList<T> Freeze<T>(IEnumerable<T>? values) =>
        new ReadOnlyCollection<T>(new List<T>(values ?? Array.Empty<T>()));

    internal static IReadOnlyDictionary<TKey, TValue> FreezeMap<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>>? values,
        IComparer<TKey>? comparer = null
    ) where TKey : notnull
    {
        var copy = new SortedDictionary<TKey, TValue>(comparer);
        if (values != null)
        {
            foreach (KeyValuePair<TKey, TValue> pair in values)
                copy.Add(pair.Key, pair.Value);
        }
        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }
}

internal sealed record AttributeModifierImportModel(
    SkillImportStringName AttributeId,
    AttributeModifierImportMode Mode,
    int Value,
    int ValuePerRank,
    SkillImportStringName SourceType,
    SkillImportStringName SourceId
);

internal sealed class ContingencyAutomationImportModel
{
    internal ContingencyAutomationImportModel(
        bool canBeStoredInContingency,
        int minContingencySkillLevel,
        SkillImportStringName effectCategory,
        IEnumerable<SkillImportStringName> tags,
        int contingencyLoadOverride,
        IEnumerable<SkillImportStringName> allowedTargetResolvers,
        bool requiresManualTargeting,
        IEnumerable<KeyValuePair<SkillImportIdentifier, ContingencyParameterBindingImportValue>>
            allowedParameterBindings
    )
    {
        CanBeStoredInContingency = canBeStoredInContingency;
        MinContingencySkillLevel = minContingencySkillLevel;
        EffectCategory = effectCategory;
        Tags = SkillImportCollections.Freeze(tags);
        ContingencyLoadOverride = contingencyLoadOverride;
        AllowedTargetResolvers = SkillImportCollections.Freeze(allowedTargetResolvers);
        RequiresManualTargeting = requiresManualTargeting;
        AllowedParameterBindings = SkillImportCollections.FreezeMap(
            allowedParameterBindings,
            Comparer<SkillImportIdentifier>.Create(
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value)
            )
        );
    }

    internal bool CanBeStoredInContingency { get; }
    internal int MinContingencySkillLevel { get; }
    internal SkillImportStringName EffectCategory { get; }
    internal IReadOnlyList<SkillImportStringName> Tags { get; }
    internal int ContingencyLoadOverride { get; }
    internal IReadOnlyList<SkillImportStringName> AllowedTargetResolvers { get; }
    internal bool RequiresManualTargeting { get; }
    internal IReadOnlyDictionary<SkillImportIdentifier, ContingencyParameterBindingImportValue>
        AllowedParameterBindings { get; }
}

internal sealed partial class SkillImportModel
{
    internal SkillImportModel(
        SkillImportIdentifier skillId,
        string displayName,
        string description,
        SkillImportType skillType,
        int maxLevel,
        SkillImportLearnSource learnSource,
        IEnumerable<SkillImportIdentifier> tags,
        string levelDescriptionTemplate,
        IEnumerable<KeyValuePair<int, SkillDescriptionVariables>> levelDescriptionConfigs,
        CombatSkillImportModel? combatProfile,
        SkillImportAssetId iconId,
        int nonCoreMaxLevel,
        SkillImportStatId dynamicMaxLevelStatId,
        int dynamicMaxLevelBase,
        int dynamicMaxLevelPerStat,
        IEnumerable<int> masteryCurve,
        IEnumerable<SkillImportIdentifier> learnRequirements,
        SkillImportUnlockMode unlockMode,
        IEnumerable<SkillImportIdentifier> knowledgeRequirements,
        IEnumerable<KeyValuePair<SkillImportIdentifier, int>> skillLevelRequirements,
        IEnumerable<KeyValuePair<SkillImportIdentifier, int>> attributeRequirements,
        IEnumerable<SkillImportIdentifier> achievementRequirements,
        IEnumerable<SkillImportIdentifier> upgradeSourceSkillIds,
        bool retainSourceSkillsOnUnlock,
        SkillImportCoreSkillTransitionMode coreSkillTransitionMode,
        IEnumerable<SkillImportIdentifier> masterySources,
        SkillImportProgressionTier growthTier,
        IEnumerable<KeyValuePair<SkillImportIdentifier, int>> attributeGrowthProgress,
        SkillImportProgressionTier practiceTier,
        IEnumerable<AttributeModifierImportModel> attributeModifiers,
        ContingencyAutomationImportModel? contingencyAutomationProfile
    )
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(levelDescriptionTemplate);
        ArgumentNullException.ThrowIfNull(levelDescriptionConfigs);

        SkillId = skillId;
        DisplayName = displayName;
        Description = description;
        SkillType = skillType;
        MaxLevel = maxLevel;
        LearnSource = learnSource;
        Tags = SkillImportCollections.Freeze(tags);
        LevelDescriptionTemplate = levelDescriptionTemplate;
        var levelDescriptionCopy = new SortedDictionary<int, SkillDescriptionVariables>();
        foreach (KeyValuePair<int, SkillDescriptionVariables> pair in levelDescriptionConfigs)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            levelDescriptionCopy.Add(pair.Key, pair.Value);
        }
        LevelDescriptionConfigs = new ReadOnlyDictionary<int, SkillDescriptionVariables>(
            levelDescriptionCopy
        );
        CombatProfile = combatProfile;
        IconId = iconId;
        NonCoreMaxLevel = nonCoreMaxLevel;
        DynamicMaxLevelStatId = dynamicMaxLevelStatId;
        DynamicMaxLevelBase = dynamicMaxLevelBase;
        DynamicMaxLevelPerStat = dynamicMaxLevelPerStat;
        MasteryCurve = SkillImportCollections.Freeze(masteryCurve);
        LearnRequirements = SkillImportCollections.Freeze(learnRequirements);
        UnlockMode = unlockMode;
        KnowledgeRequirements = SkillImportCollections.Freeze(knowledgeRequirements);
        SkillLevelRequirements = SkillImportCollections.FreezeMap(
            skillLevelRequirements,
            Comparer<SkillImportIdentifier>.Create(
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value)
            )
        );
        AttributeRequirements = SkillImportCollections.FreezeMap(
            attributeRequirements,
            Comparer<SkillImportIdentifier>.Create(
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value)
            )
        );
        AchievementRequirements = SkillImportCollections.Freeze(achievementRequirements);
        UpgradeSourceSkillIds = SkillImportCollections.Freeze(upgradeSourceSkillIds);
        RetainSourceSkillsOnUnlock = retainSourceSkillsOnUnlock;
        CoreSkillTransitionMode = coreSkillTransitionMode;
        MasterySources = SkillImportCollections.Freeze(masterySources);
        GrowthTier = growthTier;
        AttributeGrowthProgress = SkillImportCollections.FreezeMap(
            attributeGrowthProgress,
            Comparer<SkillImportIdentifier>.Create(
                (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value)
            )
        );
        PracticeTier = practiceTier;
        AttributeModifiers = SkillImportCollections.Freeze(attributeModifiers);
        ContingencyAutomationProfile = contingencyAutomationProfile;
    }

    internal SkillImportAssetId IconId { get; }
    internal int NonCoreMaxLevel { get; }
    internal SkillImportStatId DynamicMaxLevelStatId { get; }
    internal int DynamicMaxLevelBase { get; }
    internal int DynamicMaxLevelPerStat { get; }
    internal IReadOnlyList<int> MasteryCurve { get; }
    internal IReadOnlyList<SkillImportIdentifier> LearnRequirements { get; }
    internal SkillImportUnlockMode UnlockMode { get; }
    internal IReadOnlyList<SkillImportIdentifier> KnowledgeRequirements { get; }
    internal IReadOnlyDictionary<SkillImportIdentifier, int> SkillLevelRequirements { get; }
    internal IReadOnlyDictionary<SkillImportIdentifier, int> AttributeRequirements { get; }
    internal IReadOnlyList<SkillImportIdentifier> AchievementRequirements { get; }
    internal IReadOnlyList<SkillImportIdentifier> UpgradeSourceSkillIds { get; }
    internal bool RetainSourceSkillsOnUnlock { get; }
    internal SkillImportCoreSkillTransitionMode CoreSkillTransitionMode { get; }
    internal IReadOnlyList<SkillImportIdentifier> MasterySources { get; }
    internal SkillImportProgressionTier GrowthTier { get; }
    internal IReadOnlyDictionary<SkillImportIdentifier, int> AttributeGrowthProgress { get; }
    internal SkillImportProgressionTier PracticeTier { get; }
    internal IReadOnlyList<AttributeModifierImportModel> AttributeModifiers { get; }
    internal ContingencyAutomationImportModel? ContingencyAutomationProfile { get; }
}

internal sealed class CombatWindupImportModel
{
    internal CombatWindupImportModel(
        int staminaCostPerTier,
        int weaponDicePerTier,
        IEnumerable<int> skillLevelTierCaps,
        IEnumerable<int> baseWeaponDiceMultipliers
    )
    {
        StaminaCostPerTier = staminaCostPerTier;
        WeaponDicePerTier = weaponDicePerTier;
        SkillLevelTierCaps = SkillImportCollections.Freeze(skillLevelTierCaps);
        BaseWeaponDiceMultipliers = SkillImportCollections.Freeze(baseWeaponDiceMultipliers);
    }

    internal int StaminaCostPerTier { get; }
    internal int WeaponDicePerTier { get; }
    internal IReadOnlyList<int> SkillLevelTierCaps { get; }
    internal IReadOnlyList<int> BaseWeaponDiceMultipliers { get; }
}

internal sealed class CombatDirectionalPiercingImportModel
{
    internal CombatDirectionalPiercingImportModel(
        IEnumerable<int> baseDamagePercentCurve,
        int successfulHitDecayPercent,
        int minimumDamagePercent,
        int staminaFlatBase,
        int staminaRangeSquareCoefficient,
        int staminaStrengthSquareScale,
        int minimumStaminaCost,
        int maximumHeightDelta
    )
    {
        BaseDamagePercentCurve = SkillImportCollections.Freeze(baseDamagePercentCurve);
        SuccessfulHitDecayPercent = successfulHitDecayPercent;
        MinimumDamagePercent = minimumDamagePercent;
        StaminaFlatBase = staminaFlatBase;
        StaminaRangeSquareCoefficient = staminaRangeSquareCoefficient;
        StaminaStrengthSquareScale = staminaStrengthSquareScale;
        MinimumStaminaCost = minimumStaminaCost;
        MaximumHeightDelta = maximumHeightDelta;
    }

    internal IReadOnlyList<int> BaseDamagePercentCurve { get; }
    internal int SuccessfulHitDecayPercent { get; }
    internal int MinimumDamagePercent { get; }
    internal int StaminaFlatBase { get; }
    internal int StaminaRangeSquareCoefficient { get; }
    internal int StaminaStrengthSquareScale { get; }
    internal int MinimumStaminaCost { get; }
    internal int MaximumHeightDelta { get; }
}

internal sealed record CombatApproachAttackImportModel(int MaximumPathHeightDeltaFromOrigin);

internal sealed class CombatLineThroughAttackImportModel
{
    internal CombatLineThroughAttackImportModel(
        int maximumWeaponRange,
        int intermediateWeaponDiceMultiplier,
        IEnumerable<int> primaryWeaponDiceMultiplierCurve,
        IEnumerable<int> primaryAttackRollBonusCurve,
        int successfulIntermediateHitBonusWeaponDice,
        int successfulIntermediateHitAttackRollBonus,
        IEnumerable<int> successfulIntermediateHitBonusCapCurve
    )
    {
        MaximumWeaponRange = maximumWeaponRange;
        IntermediateWeaponDiceMultiplier = intermediateWeaponDiceMultiplier;
        PrimaryWeaponDiceMultiplierCurve = SkillImportCollections.Freeze(primaryWeaponDiceMultiplierCurve);
        PrimaryAttackRollBonusCurve = SkillImportCollections.Freeze(primaryAttackRollBonusCurve);
        SuccessfulIntermediateHitBonusWeaponDice = successfulIntermediateHitBonusWeaponDice;
        SuccessfulIntermediateHitAttackRollBonus = successfulIntermediateHitAttackRollBonus;
        SuccessfulIntermediateHitBonusCapCurve = SkillImportCollections.Freeze(successfulIntermediateHitBonusCapCurve);
    }

    internal int MaximumWeaponRange { get; }
    internal int IntermediateWeaponDiceMultiplier { get; }
    internal IReadOnlyList<int> PrimaryWeaponDiceMultiplierCurve { get; }
    internal IReadOnlyList<int> PrimaryAttackRollBonusCurve { get; }
    internal int SuccessfulIntermediateHitBonusWeaponDice { get; }
    internal int SuccessfulIntermediateHitAttackRollBonus { get; }
    internal IReadOnlyList<int> SuccessfulIntermediateHitBonusCapCurve { get; }
}

internal sealed class CombatSequentialLineHitImportModel
{
    internal CombatSequentialLineHitImportModel(
        IEnumerable<int> minimumPrimaryDistanceCurve,
        IEnumerable<int> continuationRangeCurve,
        IEnumerable<int> followUpAttackPenaltyCurve
    )
    {
        MinimumPrimaryDistanceCurve = SkillImportCollections.Freeze(minimumPrimaryDistanceCurve);
        ContinuationRangeCurve = SkillImportCollections.Freeze(continuationRangeCurve);
        FollowUpAttackPenaltyCurve = SkillImportCollections.Freeze(followUpAttackPenaltyCurve);
    }

    internal IReadOnlyList<int> MinimumPrimaryDistanceCurve { get; }
    internal IReadOnlyList<int> ContinuationRangeCurve { get; }
    internal IReadOnlyList<int> FollowUpAttackPenaltyCurve { get; }
}

internal sealed class CombatSpellReactionImportModel
{
    internal CombatSpellReactionImportModel(
        SkillImportStringName triggerDeliveryCategory,
        SkillImportStringName reactionSkillId,
        SkillImportStringName readinessStatusId,
        SkillImportStringName requiredWeaponFamily,
        CombatSaveAbilityImportKind saveAbility,
        SkillImportStringName saveTag,
        int baseSaveDc,
        int hpDamageDivisor,
        IEnumerable<int> attackRollBonusBySkillLevel,
        IEnumerable<int> saveDcBonusBySkillLevel,
        bool requireHpDamage,
        bool consumeOnTrigger,
        bool expireOnOwnerTurnStart
    )
    {
        TriggerDeliveryCategory = triggerDeliveryCategory;
        ReactionSkillId = reactionSkillId;
        ReadinessStatusId = readinessStatusId;
        RequiredWeaponFamily = requiredWeaponFamily;
        SaveAbility = saveAbility;
        SaveTag = saveTag;
        BaseSaveDc = baseSaveDc;
        HpDamageDivisor = hpDamageDivisor;
        AttackRollBonusBySkillLevel = SkillImportCollections.Freeze(attackRollBonusBySkillLevel);
        SaveDcBonusBySkillLevel = SkillImportCollections.Freeze(saveDcBonusBySkillLevel);
        RequireHpDamage = requireHpDamage;
        ConsumeOnTrigger = consumeOnTrigger;
        ExpireOnOwnerTurnStart = expireOnOwnerTurnStart;
    }

    internal SkillImportStringName TriggerDeliveryCategory { get; }
    internal SkillImportStringName ReactionSkillId { get; }
    internal SkillImportStringName ReadinessStatusId { get; }
    internal SkillImportStringName RequiredWeaponFamily { get; }
    internal CombatSaveAbilityImportKind SaveAbility { get; }
    internal SkillImportStringName SaveTag { get; }
    internal int BaseSaveDc { get; }
    internal int HpDamageDivisor { get; }
    internal IReadOnlyList<int> AttackRollBonusBySkillLevel { get; }
    internal IReadOnlyList<int> SaveDcBonusBySkillLevel { get; }
    internal bool RequireHpDamage { get; }
    internal bool ConsumeOnTrigger { get; }
    internal bool ExpireOnOwnerTurnStart { get; }
}

internal sealed class CombatRangedWeaponReactionImportModel
{
    internal CombatRangedWeaponReactionImportModel(
        SkillImportStringName readinessStatusId,
        IEnumerable<SkillImportStringName> triggerWeaponFamilies,
        DamageTagImportKind damageTag,
        CombatSkillLevelOverrideAttackDefenseMode attackDefenseMode,
        IEnumerable<int> attackRollBonusBySkillLevel,
        int consumeStatusStacks,
        bool triggerOnHit,
        bool triggerOnMiss,
        bool allowCritical
    )
    {
        ReadinessStatusId = readinessStatusId;
        TriggerWeaponFamilies = SkillImportCollections.Freeze(triggerWeaponFamilies);
        DamageTag = damageTag;
        AttackDefenseMode = attackDefenseMode;
        AttackRollBonusBySkillLevel = SkillImportCollections.Freeze(attackRollBonusBySkillLevel);
        ConsumeStatusStacks = consumeStatusStacks;
        TriggerOnHit = triggerOnHit;
        TriggerOnMiss = triggerOnMiss;
        AllowCritical = allowCritical;
    }

    internal SkillImportStringName ReadinessStatusId { get; }
    internal IReadOnlyList<SkillImportStringName> TriggerWeaponFamilies { get; }
    internal DamageTagImportKind DamageTag { get; }
    internal CombatSkillLevelOverrideAttackDefenseMode AttackDefenseMode { get; }
    internal IReadOnlyList<int> AttackRollBonusBySkillLevel { get; }
    internal int ConsumeStatusStacks { get; }
    internal bool TriggerOnHit { get; }
    internal bool TriggerOnMiss { get; }
    internal bool AllowCritical { get; }
}

internal enum CombatCastSquare2Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

internal sealed record CombatCastVariantPayloadImportModel(CombatCastSquare2Corner? Square2Corner);

internal sealed class CombatCastVariantImportModel
{
    internal CombatCastVariantImportModel(
        SkillImportIdentifier variantId,
        string displayName,
        string description,
        int minSkillLevel,
        CombatSkillImportTargetMode targetMode,
        CombatCastFootprintImportKind footprintPattern,
        int requiredCoordCount,
        IEnumerable<BattleTerrainImportKind> allowedBaseTerrains,
        CombatProjectileImportKind projectileKindOverride,
        IEnumerable<CombatEffectImportModel> effectDefs,
        CombatCastVariantPayloadImportModel payload
    )
    {
        VariantId = variantId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        MinSkillLevel = minSkillLevel;
        TargetMode = targetMode;
        FootprintPattern = footprintPattern;
        RequiredCoordCount = requiredCoordCount;
        AllowedBaseTerrains = SkillImportCollections.Freeze(allowedBaseTerrains);
        ProjectileKindOverride = projectileKindOverride;
        EffectDefs = SkillImportCollections.Freeze(effectDefs);
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
    }

    internal SkillImportIdentifier VariantId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal int MinSkillLevel { get; }
    internal CombatSkillImportTargetMode TargetMode { get; }
    internal CombatCastFootprintImportKind FootprintPattern { get; }
    internal int RequiredCoordCount { get; }
    internal IReadOnlyList<BattleTerrainImportKind> AllowedBaseTerrains { get; }
    internal CombatProjectileImportKind ProjectileKindOverride { get; }
    internal IReadOnlyList<CombatEffectImportModel> EffectDefs { get; }
    internal CombatCastVariantPayloadImportModel Payload { get; }
}

internal sealed partial class CombatSkillImportModel
{
    private IReadOnlyList<SkillImportStringName> _excludedTargetCreatureTypeTags = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<int> _fumbleProtectionCurve = SkillImportCollections.Freeze<int>(null);
    private IReadOnlyList<SkillImportStringName> _aiTags = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<SkillImportStringName> _deliveryCategories = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<CombatEffectImportModel> _passiveEffectDefs = SkillImportCollections.Freeze<CombatEffectImportModel>(null);
    private IReadOnlyList<CombatCastVariantImportModel> _castVariants = SkillImportCollections.Freeze<CombatCastVariantImportModel>(null);
    private IReadOnlyList<SkillImportStringName> _requiredWeaponFamilies = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<SkillImportStringName> _requiredWeaponTypeIds = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<SkillImportStringName> _excludedWeaponFamilies = SkillImportCollections.Freeze<SkillImportStringName>(null);
    private IReadOnlyList<SkillImportStringName> _excludedWeaponTypeIds = SkillImportCollections.Freeze<SkillImportStringName>(null);

    internal CombatSkillImportModel(
        SkillImportIdentifier skillId,
        CombatSkillImportTargetMode targetMode,
        CombatSkillImportTargetTeamFilter targetTeamFilter,
        CombatSkillImportRangePattern rangePattern,
        int rangeValue,
        CombatSkillImportAreaPattern areaPattern,
        int apCost,
        int mpCost,
        int cooldownTu,
        IEnumerable<CombatEffectImportModel> effectDefs,
        IEnumerable<KeyValuePair<int, CombatSkillLevelOverrideImportModel>> levelOverrides,
        IEnumerable<SkillImportStringName> excludedTargetCreatureTypeTags,
        int rangeMovePointCapacityMultiplier,
        CombatWeaponRangePolicyImportKind weaponRangePolicy,
        int areaValue,
        bool requiresLos,
        bool groundEffectRequireFullArea,
        bool groundEffectRequireEmpty,
        bool groundEffectRequireTraversable,
        int staminaCost,
        int mpCostPerTargetSlot,
        int staminaCostPerTargetSlot,
        int castingTimeTu,
        int castingMaintenanceDc,
        int castingSpellControlDc,
        CombatWindupImportModel? windupProfile,
        CombatDirectionalPiercingImportModel? directionalPiercingProfile,
        CombatApproachAttackImportModel? approachAttackProfile,
        CombatLineThroughAttackImportModel? lineThroughAttackProfile,
        CombatSequentialLineHitImportModel? sequentialLineHitProfile,
        CombatSpellReactionImportModel? spellReactionProfile,
        CombatRangedWeaponReactionImportModel? rangedWeaponReactionProfile,
        PendingCastBindingModeKind pendingCastBindingMode,
        int attackRollBonus,
        CombatSkillLevelOverrideAttackResolutionMode attackResolutionMode,
        CombatSkillLevelOverrideAttackDefenseMode attackDefenseMode,
        int auraCost,
        CombatMasteryTriggerImportKind masteryTriggerMode,
        CombatMasteryAmountImportKind masteryAmountMode,
        int masteryBaseAmount,
        CombatSpellFateImportKind spellFateMode,
        CombatSpellCriticalImportKind spellCriticalMode,
        int spellCriticalMpRefundPercent,
        IEnumerable<int> fumbleProtectionCurve,
        int fumbleProtectionExtraMpPercent,
        CombatBacklashImportKind backlashMode,
        CombatSkillImportTargetTeamFilter? backlashTargetFilter,
        int backlashOffsetRadius,
        CombatAreaOriginImportKind areaOriginMode,
        CombatAreaDirectionImportKind areaDirectionMode,
        IEnumerable<SkillImportStringName> aiTags,
        IEnumerable<SkillImportStringName> deliveryCategories,
        SkillImportStringName attackRollBonusStatusId,
        int attackRollBonusStatusStackDivisor,
        CombatBaseProjectileImportKind projectileKind,
        SkillImportStringName specialResolutionProfileId,
        CombatTargetSelectionImportKind targetSelectionMode,
        int minTargetCount,
        int maxTargetCount,
        bool allowRepeatTarget,
        CombatUnitTargetResolutionImportKind unitTargetResolutionMode,
        int maxHitsPerTarget,
        int randomChainAttackCount,
        bool randomChainContinueOnMiss,
        CombatSelectionOrderImportKind selectionOrderMode,
        IEnumerable<CombatEffectImportModel> passiveEffectDefs,
        IEnumerable<CombatCastVariantImportModel> castVariants,
        IEnumerable<SkillImportStringName> requiredWeaponFamilies,
        bool allowsNaturalWeapon,
        bool requiresHeavyWeapon,
        IEnumerable<SkillImportStringName> requiredWeaponTypeIds,
        IEnumerable<SkillImportStringName> excludedWeaponFamilies,
        IEnumerable<SkillImportStringName> excludedWeaponTypeIds,
        bool requiresEquippedShield,
        int masteryLowHpBonusMultiplier,
        int masteryLowHpThresholdPercent
    )
    {
        ArgumentNullException.ThrowIfNull(effectDefs);
        ArgumentNullException.ThrowIfNull(levelOverrides);
        SkillId = skillId;
        TargetMode = targetMode;
        TargetTeamFilter = targetTeamFilter;
        RangePattern = rangePattern;
        RangeValue = rangeValue;
        AreaPattern = areaPattern;
        ApCost = apCost;
        MpCost = mpCost;
        CooldownTu = cooldownTu;
        EffectDefs = SkillImportCollections.Freeze(effectDefs);
        var levelOverrideCopy = new SortedDictionary<int, CombatSkillLevelOverrideImportModel>();
        foreach (KeyValuePair<int, CombatSkillLevelOverrideImportModel> pair in levelOverrides)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            levelOverrideCopy.Add(pair.Key, pair.Value);
        }
        LevelOverrides = new ReadOnlyDictionary<int, CombatSkillLevelOverrideImportModel>(
            levelOverrideCopy
        );
        ExcludedTargetCreatureTypeTags = SkillImportCollections.Freeze(excludedTargetCreatureTypeTags);
        RangeMovePointCapacityMultiplier = rangeMovePointCapacityMultiplier;
        WeaponRangePolicy = weaponRangePolicy;
        AreaValue = areaValue;
        RequiresLos = requiresLos;
        GroundEffectRequireFullArea = groundEffectRequireFullArea;
        GroundEffectRequireEmpty = groundEffectRequireEmpty;
        GroundEffectRequireTraversable = groundEffectRequireTraversable;
        StaminaCost = staminaCost;
        MpCostPerTargetSlot = mpCostPerTargetSlot;
        StaminaCostPerTargetSlot = staminaCostPerTargetSlot;
        CastingTimeTu = castingTimeTu;
        CastingMaintenanceDc = castingMaintenanceDc;
        CastingSpellControlDc = castingSpellControlDc;
        WindupProfile = windupProfile;
        DirectionalPiercingProfile = directionalPiercingProfile;
        ApproachAttackProfile = approachAttackProfile;
        LineThroughAttackProfile = lineThroughAttackProfile;
        SequentialLineHitProfile = sequentialLineHitProfile;
        SpellReactionProfile = spellReactionProfile;
        RangedWeaponReactionProfile = rangedWeaponReactionProfile;
        PendingCastBindingMode = pendingCastBindingMode;
        AttackRollBonus = attackRollBonus;
        AttackResolutionMode = attackResolutionMode;
        AttackDefenseMode = attackDefenseMode;
        AuraCost = auraCost;
        MasteryTriggerMode = masteryTriggerMode;
        MasteryAmountMode = masteryAmountMode;
        MasteryBaseAmount = masteryBaseAmount;
        SpellFateMode = spellFateMode;
        SpellCriticalMode = spellCriticalMode;
        SpellCriticalMpRefundPercent = spellCriticalMpRefundPercent;
        FumbleProtectionCurve = SkillImportCollections.Freeze(fumbleProtectionCurve);
        FumbleProtectionExtraMpPercent = fumbleProtectionExtraMpPercent;
        BacklashMode = backlashMode;
        BacklashTargetFilter = backlashTargetFilter;
        BacklashOffsetRadius = backlashOffsetRadius;
        AreaOriginMode = areaOriginMode;
        AreaDirectionMode = areaDirectionMode;
        AiTags = SkillImportCollections.Freeze(aiTags);
        DeliveryCategories = SkillImportCollections.Freeze(deliveryCategories);
        AttackRollBonusStatusId = attackRollBonusStatusId;
        AttackRollBonusStatusStackDivisor = attackRollBonusStatusStackDivisor;
        ProjectileKind = projectileKind;
        SpecialResolutionProfileId = specialResolutionProfileId;
        TargetSelectionMode = targetSelectionMode;
        MinTargetCount = minTargetCount;
        MaxTargetCount = maxTargetCount;
        AllowRepeatTarget = allowRepeatTarget;
        UnitTargetResolutionMode = unitTargetResolutionMode;
        MaxHitsPerTarget = maxHitsPerTarget;
        RandomChainAttackCount = randomChainAttackCount;
        RandomChainContinueOnMiss = randomChainContinueOnMiss;
        SelectionOrderMode = selectionOrderMode;
        PassiveEffectDefs = SkillImportCollections.Freeze(passiveEffectDefs);
        CastVariants = SkillImportCollections.Freeze(castVariants);
        RequiredWeaponFamilies = SkillImportCollections.Freeze(requiredWeaponFamilies);
        AllowsNaturalWeapon = allowsNaturalWeapon;
        RequiresHeavyWeapon = requiresHeavyWeapon;
        RequiredWeaponTypeIds = SkillImportCollections.Freeze(requiredWeaponTypeIds);
        ExcludedWeaponFamilies = SkillImportCollections.Freeze(excludedWeaponFamilies);
        ExcludedWeaponTypeIds = SkillImportCollections.Freeze(excludedWeaponTypeIds);
        RequiresEquippedShield = requiresEquippedShield;
        MasteryLowHpBonusMultiplier = masteryLowHpBonusMultiplier;
        MasteryLowHpThresholdPercent = masteryLowHpThresholdPercent;
    }

    internal IReadOnlyList<SkillImportStringName> ExcludedTargetCreatureTypeTags { get => _excludedTargetCreatureTypeTags; init => _excludedTargetCreatureTypeTags = SkillImportCollections.Freeze(value); }
    internal int RangeMovePointCapacityMultiplier { get; init; }
    internal CombatWeaponRangePolicyImportKind WeaponRangePolicy { get; init; }
    internal int AreaValue { get; init; }
    internal bool RequiresLos { get; init; }
    internal bool GroundEffectRequireFullArea { get; init; }
    internal bool GroundEffectRequireEmpty { get; init; }
    internal bool GroundEffectRequireTraversable { get; init; }
    internal int StaminaCost { get; init; }
    internal int MpCostPerTargetSlot { get; init; }
    internal int StaminaCostPerTargetSlot { get; init; }
    internal int CastingTimeTu { get; init; }
    internal int CastingMaintenanceDc { get; init; }
    internal int CastingSpellControlDc { get; init; }
    internal CombatWindupImportModel? WindupProfile { get; init; }
    internal CombatDirectionalPiercingImportModel? DirectionalPiercingProfile { get; init; }
    internal CombatApproachAttackImportModel? ApproachAttackProfile { get; init; }
    internal CombatLineThroughAttackImportModel? LineThroughAttackProfile { get; init; }
    internal CombatSequentialLineHitImportModel? SequentialLineHitProfile { get; init; }
    internal CombatSpellReactionImportModel? SpellReactionProfile { get; init; }
    internal CombatRangedWeaponReactionImportModel? RangedWeaponReactionProfile { get; init; }
    internal PendingCastBindingModeKind PendingCastBindingMode { get; init; }
    internal int AttackRollBonus { get; init; }
    internal CombatSkillLevelOverrideAttackResolutionMode AttackResolutionMode { get; init; }
    internal CombatSkillLevelOverrideAttackDefenseMode AttackDefenseMode { get; init; }
    internal int AuraCost { get; init; }
    internal CombatMasteryTriggerImportKind MasteryTriggerMode { get; init; }
    internal CombatMasteryAmountImportKind MasteryAmountMode { get; init; }
    internal int MasteryBaseAmount { get; init; }
    internal CombatSpellFateImportKind SpellFateMode { get; init; }
    internal CombatSpellCriticalImportKind SpellCriticalMode { get; init; }
    internal int SpellCriticalMpRefundPercent { get; init; }
    internal IReadOnlyList<int> FumbleProtectionCurve { get => _fumbleProtectionCurve; init => _fumbleProtectionCurve = SkillImportCollections.Freeze(value); }
    internal int FumbleProtectionExtraMpPercent { get; init; }
    internal CombatBacklashImportKind BacklashMode { get; init; }
    internal CombatSkillImportTargetTeamFilter? BacklashTargetFilter { get; init; }
    internal int BacklashOffsetRadius { get; init; }
    internal CombatAreaOriginImportKind AreaOriginMode { get; init; }
    internal CombatAreaDirectionImportKind AreaDirectionMode { get; init; }
    internal IReadOnlyList<SkillImportStringName> AiTags { get => _aiTags; init => _aiTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> DeliveryCategories { get => _deliveryCategories; init => _deliveryCategories = SkillImportCollections.Freeze(value); }
    internal SkillImportStringName AttackRollBonusStatusId { get; init; }
    internal int AttackRollBonusStatusStackDivisor { get; init; }
    internal CombatBaseProjectileImportKind ProjectileKind { get; init; }
    internal SkillImportStringName SpecialResolutionProfileId { get; init; }
    internal CombatTargetSelectionImportKind TargetSelectionMode { get; init; }
    internal int MinTargetCount { get; init; }
    internal int MaxTargetCount { get; init; }
    internal bool AllowRepeatTarget { get; init; }
    internal CombatUnitTargetResolutionImportKind UnitTargetResolutionMode { get; init; }
    internal int MaxHitsPerTarget { get; init; }
    internal int RandomChainAttackCount { get; init; }
    internal bool RandomChainContinueOnMiss { get; init; }
    internal CombatSelectionOrderImportKind SelectionOrderMode { get; init; }
    internal IReadOnlyList<CombatEffectImportModel> PassiveEffectDefs { get => _passiveEffectDefs; init => _passiveEffectDefs = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatCastVariantImportModel> CastVariants { get => _castVariants; init => _castVariants = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> RequiredWeaponFamilies { get => _requiredWeaponFamilies; init => _requiredWeaponFamilies = SkillImportCollections.Freeze(value); }
    internal bool AllowsNaturalWeapon { get; init; }
    internal bool RequiresHeavyWeapon { get; init; }
    internal IReadOnlyList<SkillImportStringName> RequiredWeaponTypeIds { get => _requiredWeaponTypeIds; init => _requiredWeaponTypeIds = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> ExcludedWeaponFamilies { get => _excludedWeaponFamilies; init => _excludedWeaponFamilies = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> ExcludedWeaponTypeIds { get => _excludedWeaponTypeIds; init => _excludedWeaponTypeIds = SkillImportCollections.Freeze(value); }
    internal bool RequiresEquippedShield { get; init; }
    internal int MasteryLowHpBonusMultiplier { get; init; }
    internal int MasteryLowHpThresholdPercent { get; init; }
}
