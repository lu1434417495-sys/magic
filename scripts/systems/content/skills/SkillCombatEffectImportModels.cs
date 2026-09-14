#nullable enable

using System;
using System.Collections.Generic;

internal sealed class EmptyCombatEffectPayloadImportModel
    : ICombatEffectPayloadImportModel
{
    internal static EmptyCombatEffectPayloadImportModel Instance { get; } = new();
    private EmptyCombatEffectPayloadImportModel() { }
}

internal sealed record StatusEffectPayloadImportModel(
    SkillImportStringName BreaksBarrierLayer,
    SkillImportStringName SourceSkillId
) : ICombatEffectPayloadImportModel;

internal sealed record HealEffectPayloadImportModel(bool ConModHeal)
    : ICombatEffectPayloadImportModel;

internal sealed record EquipmentDurabilityDamageEffectPayloadImportModel
    : ICombatEffectPayloadImportModel
{
    internal EquipmentDurabilityDamageEffectPayloadImportModel(
        int maxDamagedItems,
        IEnumerable<CombatEquipmentSlotImportKind>? targetSlots
    )
    {
        MaxDamagedItems = maxDamagedItems;
        TargetSlots = SkillImportCollections.Freeze(targetSlots);
    }

    internal int MaxDamagedItems { get; }
    internal IReadOnlyList<CombatEquipmentSlotImportKind> TargetSlots { get; }
}

internal sealed record RepeatAttackUntilFailEffectPayloadImportModel
    : ICombatEffectPayloadImportModel
{
    internal RepeatAttackUntilFailEffectPayloadImportModel(
        int baseAttackBonus,
        CombatResourceImportKind costResource,
        int followUpCostAddition,
        double followUpCostMultiplier,
        int followUpAttackPenalty,
        IEnumerable<KeyValuePair<int, int>>? penaltyFreeStagesByLevel,
        bool sameTargetOnly,
        int followUpFixedCost,
        bool exponentialPenalty,
        bool stopOnInsufficientResource
    )
    {
        BaseAttackBonus = baseAttackBonus;
        CostResource = costResource;
        FollowUpCostAddition = followUpCostAddition;
        FollowUpCostMultiplier = followUpCostMultiplier;
        FollowUpAttackPenalty = followUpAttackPenalty;
        PenaltyFreeStagesByLevel = SkillImportCollections.FreezeMap(
            penaltyFreeStagesByLevel
        );
        SameTargetOnly = sameTargetOnly;
        FollowUpFixedCost = followUpFixedCost;
        ExponentialPenalty = exponentialPenalty;
        StopOnInsufficientResource = stopOnInsufficientResource;
    }

    internal int BaseAttackBonus { get; }
    internal CombatResourceImportKind CostResource { get; }
    internal int FollowUpCostAddition { get; }
    internal double FollowUpCostMultiplier { get; }
    internal int FollowUpAttackPenalty { get; }
    internal IReadOnlyDictionary<int, int> PenaltyFreeStagesByLevel { get; }
    internal bool SameTargetOnly { get; }
    internal int FollowUpFixedCost { get; }
    internal bool ExponentialPenalty { get; }
    internal bool StopOnInsufficientResource { get; }
}

internal sealed record GradedSaveExecuteEffectPayloadImportModel(
    int CriticalFailureDamageDiceCount,
    int CriticalFailureDamageDiceSides,
    int CriticalFailureExecuteThresholdMaxHpPercent,
    int CriticalFailureFrightenedDurationTu,
    int CriticalFailureStunnedDurationTu,
    int FailureDamageDiceCount,
    int FailureDamageDiceSides,
    int FailureExecuteThresholdFixed,
    int FailureExecuteThresholdMaxHpPercent,
    int FailureFrightenedDurationTu,
    int FailureReactionLockDurationTu,
    SkillImportIdentifier ProfileId,
    int SuccessAftershockDurationTu
) : ICombatEffectPayloadImportModel;

internal sealed record DispelMagicEffectPayloadImportModel(
    SkillImportStringName BreaksBarrierLayer
) : ICombatEffectPayloadImportModel;

internal sealed record OnKillGainResourcesEffectPayloadImportModel(
    CombatOnKillGrantScopeImportKind GrantScope,
    bool RequireTargetDefeatedBySameSkill,
    bool StackOnMultipleKills
) : ICombatEffectPayloadImportModel;

internal sealed record CombatEffectSlotWeightImportModel(
    CombatEquipmentSlotImportKind SlotId,
    int Weight
);

internal sealed record CombatDamageSegmentImportModel
{
    internal CombatDamageSegmentImportModel(
        DamageTagImportKind? damageTag,
        IEnumerable<DamageTagImportKind>? damageTags,
        IEnumerable<DamageTagImportKind>? mitigationBypassDamageTags,
        IEnumerable<DamageMitigationTierImportKind>? mitigationBypassTiers,
        int power,
        int diceCount,
        int diceSides,
        int diceBonus,
        bool doubleDiceOnCritical,
        double preResistanceDamageMultiplier
    )
    {
        DamageTag = damageTag;
        DamageTags = SkillImportCollections.Freeze(damageTags);
        MitigationBypassDamageTags = SkillImportCollections.Freeze(
            mitigationBypassDamageTags
        );
        MitigationBypassTiers = SkillImportCollections.Freeze(mitigationBypassTiers);
        Power = power;
        DiceCount = diceCount;
        DiceSides = diceSides;
        DiceBonus = diceBonus;
        DoubleDiceOnCritical = doubleDiceOnCritical;
        PreResistanceDamageMultiplier = preResistanceDamageMultiplier;
    }

    internal DamageTagImportKind? DamageTag { get; }
    internal IReadOnlyList<DamageTagImportKind> DamageTags { get; }
    internal IReadOnlyList<DamageTagImportKind> MitigationBypassDamageTags { get; }
    internal IReadOnlyList<DamageMitigationTierImportKind> MitigationBypassTiers { get; }
    internal int Power { get; }
    internal int DiceCount { get; }
    internal int DiceSides { get; }
    internal int DiceBonus { get; }
    internal bool DoubleDiceOnCritical { get; }
    internal double PreResistanceDamageMultiplier { get; }
}

internal sealed record CombatTargetDamageMultiplierRuleImportModel
{
    internal CombatTargetDamageMultiplierRuleImportModel(
        IEnumerable<SkillImportStringName>? anyCreatureTypeTags,
        IEnumerable<SkillImportStringName>? allCreatureTypeTags,
        IEnumerable<SkillImportStringName>? excludedCreatureTypeTags,
        int multiplierPercent
    )
    {
        AnyCreatureTypeTags = SkillImportCollections.Freeze(anyCreatureTypeTags);
        AllCreatureTypeTags = SkillImportCollections.Freeze(allCreatureTypeTags);
        ExcludedCreatureTypeTags = SkillImportCollections.Freeze(excludedCreatureTypeTags);
        MultiplierPercent = multiplierPercent;
    }

    internal IReadOnlyList<SkillImportStringName> AnyCreatureTypeTags { get; }
    internal IReadOnlyList<SkillImportStringName> AllCreatureTypeTags { get; }
    internal IReadOnlyList<SkillImportStringName> ExcludedCreatureTypeTags { get; }
    internal int MultiplierPercent { get; }
}

internal sealed record CombatWeightedStatusOutcomeImportModel(
    SkillImportStringName OutcomeId,
    int Weight,
    CombatEffectImportModel StatusEffect
);

internal sealed partial class CombatEffectImportModel
{
    private IReadOnlyList<SkillImportStringName> _doesNotStackWithStatusIds = Array.Empty<SkillImportStringName>();
    private IReadOnlyList<DamageTagImportKind> _damageTags = Array.Empty<DamageTagImportKind>();
    private IReadOnlyList<DamageTagImportKind> _mitigationBypassDamageTags = Array.Empty<DamageTagImportKind>();
    private IReadOnlyList<DamageMitigationTierImportKind> _mitigationBypassTiers = Array.Empty<DamageMitigationTierImportKind>();
    private IReadOnlyList<SkillImportStringName> _chainConductiveStatusIds = Array.Empty<SkillImportStringName>();
    private IReadOnlyList<SkillImportStringName> _chainConductiveTerrainEffectIds = Array.Empty<SkillImportStringName>();
    private IReadOnlyList<int> _followUpAttackRollBonusCurve = Array.Empty<int>();
    private IReadOnlyList<SkillImportStringName> _effectCategories = Array.Empty<SkillImportStringName>();
    private IReadOnlyList<CombatWeightedStatusOutcomeImportModel> _saveFailureStatusOutcomes = Array.Empty<CombatWeightedStatusOutcomeImportModel>();
    private IReadOnlyList<CombatSaveTagImportKind> _onRemovedStatusSaveImmunityTags = Array.Empty<CombatSaveTagImportKind>();
    private IReadOnlyList<CombatSaveTagImportKind> _saveAdvantageTags = Array.Empty<CombatSaveTagImportKind>();
    private IReadOnlyList<CombatSaveTagImportKind> _saveDisadvantageTags = Array.Empty<CombatSaveTagImportKind>();
    private IReadOnlyList<CombatSaveTagImportKind> _saveImmunityTags = Array.Empty<CombatSaveTagImportKind>();
    private IReadOnlyList<SkillImportStringName> _effectTags = Array.Empty<SkillImportStringName>();
    private IReadOnlyList<CombatEffectSlotWeightImportModel> _equipmentDurabilitySlotWeights = Array.Empty<CombatEffectSlotWeightImportModel>();
    private IReadOnlyList<CombatDamageSegmentImportModel> _extraDamageSegments = Array.Empty<CombatDamageSegmentImportModel>();
    private IReadOnlyList<CombatTargetDamageMultiplierRuleImportModel> _targetDamageMultiplierRules = Array.Empty<CombatTargetDamageMultiplierRuleImportModel>();

    internal CombatTickEffectImportKind TickEffectType { get; init; }
    internal CombatEffectLifetimeImportKind LifetimePolicy { get; init; } = CombatEffectLifetimeImportKind.Timed;
    internal int HealToHpPercentFloor { get; init; }
    internal int HealMissingHpPercent { get; init; }
    internal int MoveCostDelta { get; init; }
    internal SkillImportStringName RenderOverlayId { get; init; }
    internal int OverlayPriority { get; init; }
    internal string DisplayName { get; init; } = "";
    internal SkillImportStringName DoesNotStackWithStatusId { get; init; }
    internal IReadOnlyList<SkillImportStringName> DoesNotStackWithStatusIds { get => _doesNotStackWithStatusIds; init => _doesNotStackWithStatusIds = SkillImportCollections.Freeze(value); }
    internal int DamageRatioPercent { get; init; } = 100;
    internal double PreResistanceDamageMultiplier { get; init; } = 1.0;
    internal int WeaponDiceMultiplier { get; init; } = 1;
    internal int BonusWeaponDiceMultiplier { get; init; }
    internal DamageTagImportKind? DamageTag { get; init; }
    internal IReadOnlyList<DamageTagImportKind> DamageTags { get => _damageTags; init => _damageTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<DamageTagImportKind> MitigationBypassDamageTags { get => _mitigationBypassDamageTags; init => _mitigationBypassDamageTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<DamageMitigationTierImportKind> MitigationBypassTiers { get => _mitigationBypassTiers; init => _mitigationBypassTiers = SkillImportCollections.Freeze(value); }
    internal DamageCategoryImportKind? DamageCategory { get; init; }
    internal SkillImportStringName DrBypassTag { get; init; }
    internal int HpRatioThresholdPercent { get; init; }
    internal int DiceCount { get; init; }
    internal int DiceSides { get; init; }
    internal int DiceBonus { get; init; }
    internal int DiceSidesBase { get; init; }
    internal int DiceSidesPerConstitutionMod { get; init; }
    internal int DiceSidesPerWillpowerMod { get; init; }
    internal SkillImportStringName ShieldFamily { get; init; }
    internal ShieldAttributeModifierImportKind? ShieldAttributeModifierId { get; init; }
    internal bool ShieldRollPerTarget { get; init; }
    internal int BonusDamageDiceCount { get; init; }
    internal int BonusDamageDiceSides { get; init; }
    internal int BonusDamageDiceBonus { get; init; }
    internal bool BonusDamageSeparateEvent { get; init; }
    internal int SourceBoundWeaponBonusDamageDiceCount { get; init; }
    internal int SourceBoundWeaponBonusDamageDiceSides { get; init; }
    internal int SourceBoundWeaponBonusDamageDiceBonus { get; init; }
    internal bool AddWeaponDice { get; init; }
    internal bool RequiresWeapon { get; init; }
    internal bool UseWeaponPhysicalDamageTag { get; init; }
    internal bool ResolveAsWeaponAttack { get; init; }
    internal bool AllowRepeatHitsAcrossSteps { get; init; }
    internal CombatPathStepAreaPatternImportKind PathStepAreaPattern { get; init; } = CombatPathStepAreaPatternImportKind.Diamond;
    internal int PathStepRadius { get; init; } = 1;
    internal string PathStepLogLabel { get; init; } = "";
    internal SkillImportStringName RepeatHitStatusId { get; init; }
    internal int RepeatHitStatusThreshold { get; init; }
    internal int RepeatHitStatusMinSkillLevel { get; init; }
    internal int RepeatHitStatusPower { get; init; } = 1;
    internal int RepeatHitStatusDurationTu { get; init; }
    internal string RepeatHitStatusLogTemplate { get; init; } = "";
    internal bool PreventRepeatTarget { get; init; } = true;
    internal int ChainBaseHopRange { get; init; }
    internal int ChainConductiveHopRange { get; init; }
    internal int ChainMaxTotalTargets { get; init; }
    internal IReadOnlyList<SkillImportStringName> ChainConductiveStatusIds { get => _chainConductiveStatusIds; init => _chainConductiveStatusIds = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> ChainConductiveTerrainEffectIds { get => _chainConductiveTerrainEffectIds; init => _chainConductiveTerrainEffectIds = SkillImportCollections.Freeze(value); }
    internal int ChainBacklashHopRangeBonus { get; init; }
    internal bool StopOnMiss { get; init; } = true;
    internal bool StopOnTargetDown { get; init; } = true;
    internal int FixedAttackCount { get; init; }
    internal int FollowUpDamageMultiplierPercent { get; init; } = 100;
    internal IReadOnlyList<int> FollowUpAttackRollBonusCurve { get => _followUpAttackRollBonusCurve; init => _followUpAttackRollBonusCurve = SkillImportCollections.Freeze(value); }
    internal bool RemoveHarmful { get; init; }
    internal bool RemoveHarmfulFromAllies { get; init; } = true;
    internal bool RemoveBeneficial { get; init; }
    internal bool RemoveBeneficialFromEnemies { get; init; } = true;
    internal bool RequireDamageApplied { get; init; }
    internal int MaxStatusRemoved { get; init; }
    internal int MinHpAfterDamage { get; init; } = 1;
    internal int DeathPreventionPriority { get; init; }
    internal int ThresholdBaseValue { get; init; }
    internal int ThresholdLevelAnchor { get; init; } = 17;
    internal int ThresholdLevelBonusPerDelta { get; init; } = 5;
    internal int ThresholdMaxHpRatioPercent { get; init; } = 20;
    internal int ThresholdCapMaxHpRatioPercent { get; init; } = 50;
    internal int SoulFractureDurationTu { get; init; }
    internal int HealMultiplierPercent { get; init; } = 100;
    internal int ShieldGainMultiplierPercent { get; init; } = 100;
    internal int AttackRollPenalty { get; init; } = -1;
    internal int AttackRollBonus { get; init; }
    internal bool AttackRollAdvantage { get; init; }
    internal bool ConsumeOnNextAttackCheck { get; init; }
    internal bool ConsumeOnNextSave { get; init; }
    internal bool Undispellable { get; init; }
    internal bool DispellableMagic { get; init; }
    internal bool DispellableHarmfulMagic { get; init; }
    internal bool DispellableBeneficialMagic { get; init; }
    internal DamageMitigationTierImportKind? MitigationTier { get; init; }
    internal int SecondaryHitDcBase { get; init; } = 10;
    internal int DebuffCountThreshold { get; init; } = 3;
    internal int BaseHeal { get; init; } = 8;
    internal int HealPerLevel { get; init; } = 4;
    internal int ConModBase { get; init; } = 2;
    internal int ConModPer2Levels { get; init; } = 1;
    internal IReadOnlyList<SkillImportStringName> EffectCategories { get => _effectCategories; init => _effectCategories = SkillImportCollections.Freeze(value); }
    internal CombatEffectTargetTeamFilterImportKind? EffectTargetTeamFilter { get; init; }
    internal int MaxAffectedTargets { get; init; }
    internal bool ExcludeSource { get; init; }
    internal CombatEffectTargetOrderImportKind? TargetOrder { get; init; }
    internal SkillImportStringName RequiredTargetCreatureTypeTag { get; init; }
    internal CombatCognitionImportKind? RequiredTargetMinCognition { get; init; }
    internal SkillImportStringName StatusId { get; init; }
    internal int AppliedStatusDurationTu { get; init; }
    internal SkillImportStringName TerrainEffectId { get; init; }
    internal CombatTerrainContactImportKind? TerrainContactMode { get; init; }
    internal int TerrainEffectiveTriggerCount { get; init; }
    internal bool TerrainRequiresGroundContact { get; init; }
    internal bool TerrainRecheckFromInside { get; init; }
    internal int TerrainMaxActiveInstancesPerSource { get; init; }
    internal bool TerrainReplaceExistingFromSource { get; init; }
    internal SkillImportStringName TerrainReplaceTo { get; init; }
    internal int HeightDelta { get; init; }
    internal CombatBodySizeImportKind? BodySizeCategory { get; init; }
    internal CombatForcedMoveImportKind? ForcedMoveMode { get; init; }
    internal int ForcedMoveDistance { get; init; }
    internal int ForcedMoveMaxTargetBodySize { get; init; }
    internal int GrappleMaxHeightGain { get; init; }
    internal int SourceRetreatDistance { get; init; }
    internal int ChargeTrapImmunityMinSkillLevel { get; init; } = -1;
    internal int JumpBaseBudget { get; init; }
    internal double JumpStrScale { get; init; }
    internal double JumpArcRatio { get; init; }
    internal int JumpRangeMultiplier { get; init; } = 1;
    internal int TickIntervalTu { get; init; }
    internal CombatStackBehaviorImportKind StackBehavior { get; init; } = CombatStackBehaviorImportKind.Refresh;
    internal int StackLimit { get; init; }
    internal CombatDamageBonusConditionImportKind? BonusCondition { get; init; }
    internal SkillImportStringName BonusConditionCreatureTypeTag { get; init; }
    internal CombatEffectTriggerEventImportKind? TriggerEvent { get; init; }
    internal CombatEffectTriggerConditionImportKind? TriggerCondition { get; init; }
    internal SkillImportStringName TriggerStatusId { get; init; }
    internal int SaveDc { get; init; }
    internal int SaveDcBonus { get; init; }
    internal CombatSaveDcModeImportKind SaveDcMode { get; init; } = CombatSaveDcModeImportKind.Static;
    internal CombatSaveAbilityImportKind? SaveDcSourceAbility { get; init; }
    internal CombatSaveAbilityImportKind? SaveAbility { get; init; }
    internal SkillImportStringName SaveFailureStatusId { get; init; }
    internal IReadOnlyList<CombatWeightedStatusOutcomeImportModel> SaveFailureStatusOutcomes { get => _saveFailureStatusOutcomes; init => _saveFailureStatusOutcomes = SkillImportCollections.Freeze(value); }
    internal bool SavePartialOnSuccess { get; init; }
    internal CombatSaveTagImportKind? SaveTag { get; init; }
    internal SkillImportStringName ConsumedStatusId { get; init; }
    internal SkillImportStringName RequiredTargetStatusId { get; init; }
    internal int RequiredTargetStatusMinStacks { get; init; }
    internal CombatStatusSourceSelectorImportKind? RequiredTargetStatusSourceSelector { get; init; }
    internal int DicePerConsumedStack { get; init; }
    internal int DiceSidesPerStack { get; init; }
    internal int ApGain { get; init; }
    internal int FreeMovePointsGain { get; init; }
    internal bool CountsAsDebuffOverride { get; init; }
    internal bool CountsAsDebuff { get; init; }
    internal bool LockCounterattack { get; init; }
    internal bool LockGuard { get; init; }
    internal bool LockDodgeBonus { get; init; }
    internal bool LockCrit { get; init; }
    internal bool SkipTurn { get; init; }
    internal bool BreakOnPositiveDamage { get; init; }
    internal SkillImportStringName OnRemovedStatusId { get; init; }
    internal IReadOnlyList<CombatSaveTagImportKind> OnRemovedStatusSaveImmunityTags { get => _onRemovedStatusSaveImmunityTags; init => _onRemovedStatusSaveImmunityTags = SkillImportCollections.Freeze(value); }
    internal bool OnRemovedStatusUndispellable { get; init; }
    internal bool OnRemovedStatusConsumeAfterNormalTurn { get; init; }
    internal int SaveBonus { get; init; }
    internal int ControlSaveBonus { get; init; }
    internal int PassiveReduction { get; init; }
    internal int ContentDr { get; init; }
    internal int GuardBlock { get; init; }
    internal int RangeBonus { get; init; }
    internal int MainSkillLockOtherDebuffCount { get; init; }
    internal int MeleeComboStackGainBonus { get; init; }
    internal SkillImportStringName ComboAttackBonusStatusId { get; init; }
    internal int ComboAttackBonusStackDivisor { get; init; }
    internal CombatResourceImportKind? UpkeepResource { get; init; }
    internal int UpkeepIntervalTu { get; init; }
    internal int UpkeepBaseCost { get; init; }
    internal int UpkeepEscalationIntervalTu { get; init; }
    internal int UpkeepCostMultiplier { get; init; } = 1;
    internal bool BreakOnHardControl { get; init; }
    internal SkillImportStringName TerminationStatusId { get; init; }
    internal int TerminationStatusDurationTu { get; init; }
    internal int TerminationAttackRollPenalty { get; init; }
    internal int TerminationCooldownTu { get; init; }
    internal IReadOnlyList<CombatSaveTagImportKind> SaveAdvantageTags { get => _saveAdvantageTags; init => _saveAdvantageTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatSaveTagImportKind> SaveDisadvantageTags { get => _saveDisadvantageTags; init => _saveDisadvantageTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatSaveTagImportKind> SaveImmunityTags { get => _saveImmunityTags; init => _saveImmunityTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<SkillImportStringName> EffectTags { get => _effectTags; init => _effectTags = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatEffectSlotWeightImportModel> EquipmentDurabilitySlotWeights { get => _equipmentDurabilitySlotWeights; init => _equipmentDurabilitySlotWeights = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatDamageSegmentImportModel> ExtraDamageSegments { get => _extraDamageSegments; init => _extraDamageSegments = SkillImportCollections.Freeze(value); }
    internal IReadOnlyList<CombatTargetDamageMultiplierRuleImportModel> TargetDamageMultiplierRules { get => _targetDamageMultiplierRules; init => _targetDamageMultiplierRules = SkillImportCollections.Freeze(value); }

}
