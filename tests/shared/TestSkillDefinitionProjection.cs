using System.Collections.Generic;
using Godot;

internal static class TestSkillDefinitionProjection
{
    internal static SkillDefinition LoadSkillDefinition(
        string resourcePath,
        string ownershipReason = ""
    )
    {
        SkillDef skillDef = ResourceLoader.Load<SkillDef>(resourcePath);
        if (skillDef != null)
        {
            GodotContentOwnership.RegisterBorrowedContent(
                skillDef,
                string.IsNullOrEmpty(ownershipReason)
                    ? $"test_skill_definition_projection:{resourcePath}"
                    : ownershipReason
            );
        }
        return SkillDefinition.FromResource(skillDef);
    }

    internal static SkillDefinition BuildSkill(
        StringName skillId,
        string displayName = "",
        CombatSkillDefinition combatProfile = null,
        StringName skillType = default,
        int maxLevel = 1,
        int nonCoreMaxLevel = 0,
        StringName learnSource = default,
        IReadOnlyList<StringName> tags = null,
        IReadOnlyList<int> masteryCurve = null,
        IReadOnlyList<StringName> masterySources = null,
        IReadOnlyList<StringName> knowledgeRequirements = null,
        IReadOnlyDictionary<StringName, int> skillLevelRequirements = null,
        IReadOnlyDictionary<StringName, int> attributeRequirements = null,
        IReadOnlyDictionary<StringName, int> attributeGrowthProgress = null,
        StringName practiceTier = default,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers = null,
        string levelDescriptionTemplate = "",
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelDescriptionConfigs = null,
        ContingencyAutomationDefinition contingencyAutomationProfile = null
    )
    {
        return new SkillDefinition(
            skillId: skillId,
            displayName: displayName ?? skillId.ToString(),
            iconId: "",
            description: "",
            skillType: DefaultName(skillType, "active"),
            maxLevel: maxLevel,
            nonCoreMaxLevel: nonCoreMaxLevel,
            dynamicMaxLevelStatId: "",
            dynamicMaxLevelBase: 0,
            dynamicMaxLevelPerStat: 0,
            masteryCurve: masteryCurve ?? System.Array.Empty<int>(),
            tags: tags ?? System.Array.Empty<StringName>(),
            learnSource: DefaultName(learnSource, "book"),
            learnRequirements: System.Array.Empty<StringName>(),
            unlockMode: "standard",
            knowledgeRequirements: knowledgeRequirements ?? System.Array.Empty<StringName>(),
            skillLevelRequirements: skillLevelRequirements ?? new Dictionary<StringName, int>(),
            attributeRequirements: attributeRequirements ?? new Dictionary<StringName, int>(),
            achievementRequirements: System.Array.Empty<StringName>(),
            upgradeSourceSkillIds: System.Array.Empty<StringName>(),
            retainSourceSkillsOnUnlock: false,
            coreSkillTransitionMode: "inherit",
            masterySources: masterySources ?? System.Array.Empty<StringName>(),
            growthTier: "",
            attributeGrowthProgress: attributeGrowthProgress ?? new Dictionary<StringName, int>(),
            practiceTier: DefaultName(practiceTier, ""),
            attributeModifiers: attributeModifiers
                ?? System.Array.Empty<AttributeModifierDefinition>(),
            levelDescriptionTemplate: levelDescriptionTemplate ?? "",
            levelDescriptionConfigs: levelDescriptionConfigs
                ?? new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            combatProfile: combatProfile,
            contingencyAutomationProfile: contingencyAutomationProfile
        );
    }

    internal static ContingencyAutomationDefinition BuildContingencyAutomation(
        bool canBeStoredInContingency = true,
        int minContingencySkillLevel = 1,
        StringName effectCategory = default,
        IReadOnlyList<StringName> tags = null,
        int contingencyLoadOverride = 0,
        IReadOnlyList<StringName> allowedTargetResolvers = null,
        bool requiresManualTargeting = false,
        IReadOnlyDictionary<string, object> allowedParameterBindings = null
    ) =>
        new(
            canBeStoredInContingency,
            minContingencySkillLevel,
            DefaultName(effectCategory, ""),
            tags ?? System.Array.Empty<StringName>(),
            contingencyLoadOverride,
            allowedTargetResolvers ?? System.Array.Empty<StringName>(),
            requiresManualTargeting,
            allowedParameterBindings ?? new Dictionary<string, object>()
        );

    internal static CombatSkillDefinition BuildCombatProfile(
        StringName skillId,
        IReadOnlyList<CombatEffectDefinition> effects = null,
        StringName targetMode = default,
        StringName targetTeamFilter = default,
        int rangeValue = 1,
        int apCost = 0,
        int mpCost = 0,
        int staminaCost = 0,
        int auraCost = 0,
        int cooldownTu = 0,
        int castingTimeTu = 0,
        int castingMaintenanceDc = 0,
        int castingSpellControlDc = 0,
        StringName pendingCastBindingMode = default,
        int attackRollBonus = 0,
        StringName attackResolutionMode = default,
        StringName rangePattern = default,
        StringName areaPattern = default,
        int areaValue = 0,
        IReadOnlyList<StringName> aiTags = null,
        StringName targetSelectionMode = default,
        int minTargetCount = 0,
        int maxTargetCount = 0,
        bool allowRepeatTarget = false,
        int maxHitsPerTarget = 0,
        StringName specialResolutionProfileId = default,
        IReadOnlyList<CombatCastVariantDefinition> castVariants = null,
        IReadOnlyList<StringName> requiredWeaponFamilies = null,
        IReadOnlyList<StringName> deliveryCategories = null,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelOverrides = null,
        StringName masteryTriggerMode = default,
        StringName masteryAmountMode = default
    )
    {
        return new CombatSkillDefinition(
            skillId: skillId,
            targetMode: DefaultName(targetMode, "unit"),
            targetTeamFilter: DefaultName(targetTeamFilter, "enemy"),
            rangePattern: DefaultName(rangePattern, "single"),
            rangeValue: rangeValue,
            areaPattern: DefaultName(areaPattern, "single"),
            areaValue: areaValue,
            requiresLos: false,
            apCost: apCost,
            mpCost: mpCost,
            staminaCost: staminaCost,
            cooldownTu: cooldownTu,
            castingTimeTu: castingTimeTu,
            castingMaintenanceDc: castingMaintenanceDc,
            castingSpellControlDc: castingSpellControlDc,
            pendingCastBindingMode: DefaultName(pendingCastBindingMode, "soft_anchor"),
            attackRollBonus: attackRollBonus,
            attackResolutionMode: DefaultName(attackResolutionMode, ""),
            auraCost: auraCost,
            levelOverrides: levelOverrides ?? new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            masteryTriggerMode: masteryTriggerMode,
            masteryAmountMode: masteryAmountMode,
            spellFateMode: default,
            spellCriticalMode: default,
            spellCriticalMpRefundPercent: 0,
            fumbleProtectionCurve: System.Array.Empty<int>(),
            fumbleProtectionExtraMpPercent: 0,
            backlashMode: default,
            backlashTargetFilter: default,
            backlashOffsetRadius: 0,
            areaOriginMode: default,
            areaDirectionMode: default,
            aiTags: aiTags ?? System.Array.Empty<StringName>(),
            deliveryCategories: deliveryCategories ?? System.Array.Empty<StringName>(),
            specialResolutionProfileId: DefaultName(specialResolutionProfileId, ""),
            targetSelectionMode: DefaultName(
                targetSelectionMode,
                BattleTypedNames.ToStringName(BattleTargetSelectionMode.SingleUnit)
            ),
            minTargetCount: minTargetCount,
            maxTargetCount: maxTargetCount,
            allowRepeatTarget: allowRepeatTarget,
            maxHitsPerTarget: maxHitsPerTarget,
            selectionOrderMode: default,
            effectDefinitions: effects ?? System.Array.Empty<CombatEffectDefinition>(),
            passiveEffectDefinitions: System.Array.Empty<CombatEffectDefinition>(),
            castVariants: castVariants ?? System.Array.Empty<CombatCastVariantDefinition>(),
            requiredWeaponFamilies: requiredWeaponFamilies ?? System.Array.Empty<StringName>(),
            excludedWeaponFamilies: System.Array.Empty<StringName>(),
            excludedWeaponTypeIds: System.Array.Empty<StringName>(),
            requiresEquippedShield: false,
            masteryLowHpBonusMultiplier: 0,
            masteryLowHpThresholdPercent: 0
        );
    }

    internal static CombatEffectDefinition BuildEffect(
        StringName effectType,
        StringName effectTargetTeamFilter = default,
        StringName statusId = default,
        StringName saveFailureStatusId = default,
        int power = 0,
        StringName terrainEffectId = default,
        bool preventRepeatTarget = true,
        StringName forcedMoveMode = default,
        int forcedMoveDistance = 0,
        IReadOnlyDictionary<string, object> parameters = null,
        StringName bonusCondition = default,
        StringName triggerEvent = default,
        StringName triggerCondition = default,
        int hpRatioThresholdPercent = 0,
        int bonusDamageDiceCount = 0,
        int bonusDamageDiceSides = 0,
        int appliedStatusDurationTu = 0,
        int saveDc = 0,
        StringName saveDcMode = default,
        StringName saveDcSourceAbility = default,
        StringName saveAbility = default,
        bool savePartialOnSuccess = false,
        StringName saveTag = default,
        StringName damageTag = default,
        StringName drBypassTag = default,
        bool requiresWeapon = false,
        bool addWeaponDice = false,
        bool useWeaponPhysicalDamageTag = false,
        bool resolveAsWeaponAttack = false,
        int diceCount = 0,
        int diceSides = 0,
        int diceBonus = 0,
        int diceSidesBase = 0,
        int diceSidesPerConstitutionMod = 0,
        int diceSidesPerWillpowerMod = 0,
        int jumpBaseBudget = 0,
        double jumpStrScale = 0.0,
        double jumpArcRatio = 0.0,
        int jumpRangeMultiplier = 1,
        int thresholdBaseValue = 0,
        int thresholdMaxHpRatioPercent = 0,
        int thresholdLevelAnchor = 0,
        int thresholdLevelBonusPerDelta = 0,
        int thresholdCapMaxHpRatioPercent = 0,
        int soulFractureDurationTu = 0,
        int healMultiplierPercent = 0,
        int shieldGainMultiplierPercent = 0,
        int durationTu = 0,
        int tickIntervalTu = 0,
        StringName tickEffectType = default,
        StringName lifetimePolicy = default,
        int moveCostDelta = 0,
        StringName renderOverlayId = default,
        int overlayPriority = 0,
        string displayName = "",
        BattleAttackRollModifierSpec accuracyModifierSpec = null,
        StringName stackBehavior = default,
        int stackLimit = 0,
        bool requireDamageApplied = false,
        bool removeHarmful = false,
        int maxStatusRemoved = 0,
        int baseHeal = 8,
        int healPerLevel = 4,
        int conModBase = 2,
        int conModPer2Levels = 1,
        IReadOnlyList<StringName> effectTags = null,
        IReadOnlyList<StringName> effectCategories = null
    )
    {
        return new CombatEffectDefinition(
            effectType: effectType,
            effectTargetTeamFilter: effectTargetTeamFilter,
            statusId: statusId,
            saveFailureStatusId: saveFailureStatusId,
            terrainEffectId: terrainEffectId,
            terrainReplaceTo: default,
            heightDelta: 0,
            requiresWeapon: requiresWeapon,
            addWeaponDice: addWeaponDice,
            preventRepeatTarget: preventRepeatTarget,
            forcedMoveMode: forcedMoveMode,
            minSkillLevel: 0,
            maxSkillLevel: -1,
            damageTag: damageTag,
            damageRatioPercent: 100,
            preResistanceDamageMultiplier: 1.0,
            bonusCondition: bonusCondition,
            hpRatioThresholdPercent: hpRatioThresholdPercent,
            damageCategory: default,
            drBypassTag: drBypassTag,
            diceCount: diceCount,
            diceSides: diceSides,
            diceBonus: diceBonus,
            bonusDamageDiceCount: bonusDamageDiceCount,
            bonusDamageDiceSides: bonusDamageDiceSides,
            bonusDamageDiceBonus: 0,
            sourceBoundWeaponBonusDamageDiceCount: 0,
            sourceBoundWeaponBonusDamageDiceSides: 0,
            sourceBoundWeaponBonusDamageDiceBonus: 0,
            saveDc: saveDc,
            saveDcMode: saveDcMode,
            saveDcSourceAbility: saveDcSourceAbility,
            saveAbility: saveAbility,
            savePartialOnSuccess: savePartialOnSuccess,
            saveTag: saveTag,
            thresholdBaseValue: thresholdBaseValue,
            thresholdLevelAnchor: thresholdLevelAnchor,
            thresholdLevelBonusPerDelta: thresholdLevelBonusPerDelta,
            thresholdMaxHpRatioPercent: thresholdMaxHpRatioPercent,
            thresholdCapMaxHpRatioPercent: thresholdCapMaxHpRatioPercent,
            soulFractureDurationTu: soulFractureDurationTu,
            healMultiplierPercent: healMultiplierPercent,
            shieldGainMultiplierPercent: shieldGainMultiplierPercent,
            appliedStatusDurationTu: appliedStatusDurationTu,
            durationTu: durationTu,
            tickIntervalTu: tickIntervalTu,
            effectTags: effectTags ?? System.Array.Empty<StringName>(),
            triggerEvent: DefaultName(triggerEvent, ""),
            triggerCondition: DefaultName(triggerCondition, ""),
            power: power,
            parameters: parameters,
            forcedMoveDistance: forcedMoveDistance,
            jumpBaseBudget: jumpBaseBudget,
            jumpStrScale: jumpStrScale,
            jumpArcRatio: jumpArcRatio,
            jumpRangeMultiplier: jumpRangeMultiplier,
            diceSidesBase: diceSidesBase,
            diceSidesPerConstitutionMod: diceSidesPerConstitutionMod,
            diceSidesPerWillpowerMod: diceSidesPerWillpowerMod,
            useWeaponPhysicalDamageTag: useWeaponPhysicalDamageTag,
            resolveAsWeaponAttack: resolveAsWeaponAttack,
            tickEffectType: tickEffectType,
            lifetimePolicy: lifetimePolicy,
            moveCostDelta: moveCostDelta,
            renderOverlayId: renderOverlayId,
            overlayPriority: overlayPriority,
            displayName: displayName,
            accuracyModifierSpec: accuracyModifierSpec,
            stackBehavior: stackBehavior,
            stackLimit: stackLimit,
            requireDamageApplied: requireDamageApplied,
            removeHarmful: removeHarmful,
            maxStatusRemoved: maxStatusRemoved,
            baseHeal: baseHeal,
            healPerLevel: healPerLevel,
            conModBase: conModBase,
            conModPer2Levels: conModPer2Levels,
            effectCategories: effectCategories ?? System.Array.Empty<StringName>()
        );
    }

    internal static CombatCastVariantDefinition BuildCastVariant(
        StringName variantId,
        int minSkillLevel,
        IReadOnlyList<CombatEffectDefinition> effects,
        StringName targetMode = default,
        StringName footprintPattern = default,
        int requiredCoordCount = 0,
        IReadOnlyDictionary<string, object> parameters = null
    )
    {
        return new CombatCastVariantDefinition(
            variantId: variantId,
            displayName: "",
            description: "",
            minSkillLevel: minSkillLevel,
            targetMode: targetMode,
            footprintPattern: footprintPattern,
            requiredCoordCount: requiredCoordCount,
            allowedBaseTerrains: System.Array.Empty<StringName>(),
            effectDefinitions: effects ?? System.Array.Empty<CombatEffectDefinition>(),
            parameters: parameters ?? new Dictionary<string, object>()
        );
    }

    private static StringName DefaultName(StringName value, StringName fallback)
    {
        if (value == default || value == "" || string.IsNullOrEmpty(value.ToString()))
        {
            return fallback;
        }
        return value;
    }
}
