using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleRuntimeEffectDefinitions
{
    private static readonly IReadOnlyList<StringName> EmptyStringNames = Array.Empty<StringName>();
    private static readonly StringName Empty = "";
    private static readonly StringName StatusEffectType = "status";
    private static readonly StringName StaticSaveDcMode = "static";

    internal static CombatEffectDefinition Status(
        StringName statusId,
        int power,
        int durationTu,
        IReadOnlyDictionary<string, object> parameters = null,
        StringName stackBehavior = default,
        int stackLimit = 0,
        int attackRollPenalty = -1,
        int sourceBoundAttackRollPenalty = 0,
        int sourceBoundAttackRollPenaltyMinStacks = 1,
        int sourceBoundIncomingAttackRollBonusPerStack = 0,
        int sourceBoundIncomingAttackRollBonusMinStacks = 1,
        string displayName = "",
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false,
        bool undispellable = false,
        bool dispellableMagic = false,
        bool dispellableHarmfulMagic = false,
        bool dispellableBeneficialMagic = false,
        bool lockCounterattack = false,
        bool lockGuard = false,
        bool lockDodgeBonus = false,
        bool attackRollAdvantage = false
    )
    {
        IReadOnlyDictionary<string, object> mergedParameters = parameters;
        if (
            sourceBoundAttackRollPenalty > 0
            || sourceBoundIncomingAttackRollBonusPerStack > 0
        )
        {
            var nextParameters = new Dictionary<string, object>(StringComparer.Ordinal);
            if (parameters != null)
            {
                foreach (KeyValuePair<string, object> entry in parameters)
                    nextParameters[entry.Key] = entry.Value;
            }
            if (sourceBoundAttackRollPenalty > 0)
            {
                nextParameters["source_bound_attack_roll_penalty"] =
                    sourceBoundAttackRollPenalty;
                nextParameters["source_bound_attack_roll_penalty_min_stacks"] =
                    Math.Max(sourceBoundAttackRollPenaltyMinStacks, 1);
            }
            if (sourceBoundIncomingAttackRollBonusPerStack > 0)
            {
                nextParameters["source_bound_incoming_attack_roll_bonus_per_stack"] =
                    sourceBoundIncomingAttackRollBonusPerStack;
                nextParameters["source_bound_incoming_attack_roll_bonus_min_stacks"] =
                    Math.Max(sourceBoundIncomingAttackRollBonusMinStacks, 1);
            }
            mergedParameters = nextParameters;
        }
        return Create(
            effectType: StatusEffectType,
            statusId: Normalize(statusId),
            power: Math.Max(power, 0),
            durationTu: Math.Max(durationTu, 0),
            parameters: mergedParameters,
            stackBehavior: Normalize(stackBehavior),
            stackLimit: Math.Max(stackLimit, 0),
            displayName: displayName ?? "",
            attackRollPenalty: attackRollPenalty,
            undispellable: undispellable,
            dispellableMagic: dispellableMagic,
            dispellableHarmfulMagic: dispellableHarmfulMagic,
            dispellableBeneficialMagic: dispellableBeneficialMagic,
            countsAsDebuffOverride: countsAsDebuffOverride,
            countsAsDebuff: countsAsDebuff,
            lockCounterattack: lockCounterattack,
            lockGuard: lockGuard,
            lockDodgeBonus: lockDodgeBonus,
            attackRollAdvantage: attackRollAdvantage
        );
    }

    internal static CombatEffectDefinition StaticSave(
        int saveDc,
        StringName saveAbility,
        StringName saveTag,
        StringName effectType = default
    )
    {
        return Create(
            effectType: Normalize(effectType) == Empty ? StatusEffectType : Normalize(effectType),
            saveDc: Math.Max(saveDc, 0),
            saveDcMode: StaticSaveDcMode,
            saveAbility: Normalize(saveAbility),
            saveTag: Normalize(saveTag)
        );
    }

    internal static CombatEffectDefinition Damage(
        StringName damageTag,
        int diceCount,
        int diceSides,
        int diceBonus,
        IReadOnlyList<StringName> damageTags = null,
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null,
        int power = 0
    )
    {
        return Create(
            effectType: "damage",
            damageTag: Normalize(damageTag),
            diceCount: Math.Max(diceCount, 0),
            diceSides: Math.Max(diceSides, 0),
            diceBonus: Math.Max(diceBonus, 0),
            power: Math.Max(power, 0),
            damageTags: damageTags ?? EmptyStringNames,
            mitigationBypassDamageTags: mitigationBypassDamageTags ?? EmptyStringNames,
            mitigationBypassTiers: mitigationBypassTiers ?? EmptyStringNames
        );
    }

    internal static CombatEffectDefinition Heal(
        int diceCount,
        int diceSides,
        int diceBonus,
        int power = 0
    )
    {
        return Create(
            effectType: "heal",
            diceCount: Math.Max(diceCount, 0),
            diceSides: Math.Max(diceSides, 0),
            diceBonus: Math.Max(diceBonus, 0),
            power: Math.Max(power, 0)
        );
    }

    internal static IReadOnlyDictionary<string, object> CopyVariantDictionary(GDictionary source)
    {
        return ContentValueNormalizer.NormalizeDictionary(
            source,
            "BattleRuntimeEffectDefinitions.parameters"
        );
    }

    private static CombatEffectDefinition Create(
        StringName effectType,
        StringName effectTargetTeamFilter = default,
        StringName statusId = default,
        StringName saveFailureStatusId = default,
        StringName terrainEffectId = default,
        StringName terrainReplaceTo = default,
        int heightDelta = 0,
        bool requiresWeapon = false,
        bool addWeaponDice = false,
        bool preventRepeatTarget = false,
        StringName forcedMoveMode = default,
        int minSkillLevel = 0,
        int maxSkillLevel = 0,
        StringName damageTag = default,
        int damageRatioPercent = 100,
        double preResistanceDamageMultiplier = 1.0,
        StringName bonusCondition = default,
        StringName bonusConditionCreatureTypeTag = default,
        int hpRatioThresholdPercent = 0,
        StringName damageCategory = default,
        StringName drBypassTag = default,
        int diceCount = 0,
        int diceSides = 0,
        int diceBonus = 0,
        int bonusDamageDiceCount = 0,
        int bonusDamageDiceSides = 0,
        int bonusDamageDiceBonus = 0,
        int saveDc = 0,
        StringName saveDcMode = default,
        StringName saveDcSourceAbility = default,
        StringName saveAbility = default,
        bool savePartialOnSuccess = false,
        StringName saveTag = default,
        int thresholdBaseValue = 0,
        int thresholdLevelAnchor = 0,
        int thresholdLevelBonusPerDelta = 0,
        int thresholdMaxHpRatioPercent = 0,
        int thresholdCapMaxHpRatioPercent = 0,
        int soulFractureDurationTu = 0,
        int healMultiplierPercent = 0,
        int shieldGainMultiplierPercent = 0,
        int appliedStatusDurationTu = 0,
        int durationTu = 0,
        int tickIntervalTu = 0,
        IReadOnlyList<StringName> effectTags = null,
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
        IReadOnlyList<StringName> mitigationBypassDamageTags = null,
        IReadOnlyList<StringName> mitigationBypassTiers = null,
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
        int attackRollBonus = 0,
        bool attackRollAdvantage = false,
        bool consumeOnNextAttackCheck = false,
        bool consumeOnNextSave = false,
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
        IReadOnlyList<StringName> saveTags = null
    )
    {
        return new CombatEffectDefinition(
            Normalize(effectType),
            Normalize(effectTargetTeamFilter),
            Normalize(statusId),
            Normalize(saveFailureStatusId),
            Normalize(terrainEffectId),
            Normalize(terrainReplaceTo),
            heightDelta,
            requiresWeapon,
            addWeaponDice,
            preventRepeatTarget,
            Normalize(forcedMoveMode),
            minSkillLevel,
            maxSkillLevel,
            Normalize(damageTag),
            damageRatioPercent,
            preResistanceDamageMultiplier,
            Normalize(bonusCondition),
            hpRatioThresholdPercent,
            Normalize(damageCategory),
            Normalize(drBypassTag),
            diceCount,
            diceSides,
            diceBonus,
            bonusDamageDiceCount,
            bonusDamageDiceSides,
            bonusDamageDiceBonus,
            saveDc,
            Normalize(saveDcMode),
            Normalize(saveDcSourceAbility),
            Normalize(saveAbility),
            savePartialOnSuccess,
            Normalize(saveTag),
            thresholdBaseValue,
            thresholdLevelAnchor,
            thresholdLevelBonusPerDelta,
            thresholdMaxHpRatioPercent,
            thresholdCapMaxHpRatioPercent,
            soulFractureDurationTu,
            healMultiplierPercent,
            shieldGainMultiplierPercent,
            appliedStatusDurationTu,
            durationTu,
            tickIntervalTu,
            effectTags ?? EmptyStringNames,
            Normalize(triggerCondition),
            power,
            rangeBonus,
            forcedMoveDistance,
            jumpBaseBudget,
            jumpStrScale,
            jumpArcRatio,
            jumpRangeMultiplier,
            diceSidesBase,
            diceSidesPerConstitutionMod,
            diceSidesPerWillpowerMod,
            parameters,
            effectCategories ?? EmptyStringNames,
            allowRepeatHitsAcrossSteps,
            Normalize(tickEffectType),
            Normalize(lifetimePolicy),
            moveCostDelta,
            Normalize(renderOverlayId),
            overlayPriority,
            displayName,
            accuracyModifierSpec,
            Normalize(doesNotStackWithStatusId),
            doesNotStackWithStatusIds ?? EmptyStringNames,
            damageTags ?? EmptyStringNames,
            useWeaponPhysicalDamageTag,
            resolveAsWeaponAttack,
            stopOnMiss,
            stopOnTargetDown,
            removeHarmful,
            removeHarmfulFromAllies,
            removeBeneficial,
            removeBeneficialFromEnemies,
            requireDamageApplied,
            maxStatusRemoved,
            minHpAfterDamage,
            deathPreventionPriority,
            attackRollPenalty,
            undispellable,
            dispellableMagic,
            dispellableHarmfulMagic,
            dispellableBeneficialMagic,
            Normalize(mitigationTier),
            secondaryHitDcBase,
            debuffCountThreshold,
            baseHeal,
            healPerLevel,
            conModBase,
            conModPer2Levels,
            Normalize(bodySizeCategory),
            Normalize(stackBehavior),
            stackLimit,
            Normalize(triggerEvent),
            Normalize(triggerStatusId),
            Normalize(consumedStatusId),
            default,
            0,
            dicePerConsumedStack,
            diceSidesPerStack,
            apGain,
            freeMovePointsGain,
            countsAsDebuffOverride,
            countsAsDebuff,
            lockCounterattack,
            lockGuard,
            lockDodgeBonus,
            lockCrit,
            saveBonus,
            controlSaveBonus,
            passiveReduction,
            contentDr,
            guardBlock,
            mainSkillLockOtherDebuffCount,
            saveAdvantageTags ?? EmptyStringNames,
            saveDisadvantageTags ?? EmptyStringNames,
            saveImmunityTags ?? EmptyStringNames,
            saveTags ?? EmptyStringNames,
            bonusConditionCreatureTypeTag: Normalize(bonusConditionCreatureTypeTag),
            mitigationBypassDamageTags: mitigationBypassDamageTags ?? EmptyStringNames,
            mitigationBypassTiers: mitigationBypassTiers ?? EmptyStringNames,
            attackRollBonus: attackRollBonus,
            attackRollAdvantage: attackRollAdvantage,
            consumeOnNextAttackCheck: consumeOnNextAttackCheck,
            consumeOnNextSave: consumeOnNextSave
        );
    }

    private static StringName Normalize(StringName value)
    {
        return value == null ? Empty : value;
    }
}
