#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Godot;

/// <summary>
/// The single business projection from the normalized plain skill import graph to the
/// immutable runtime definition graph. Resource and JSON sources must converge before
/// entering this projector.
/// </summary>
internal static class SkillDefinitionProjector
{
    internal static SkillDefinition Project(SkillImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new SkillDefinition(
            Name(source.SkillId),
            source.DisplayName,
            Name(source.IconId),
            source.Description,
            Name(SkillJsonImportValueRules.GetWireValue(source.SkillType)),
            source.MaxLevel,
            source.NonCoreMaxLevel,
            Name(source.DynamicMaxLevelStatId),
            source.DynamicMaxLevelBase,
            source.DynamicMaxLevelPerStat,
            source.MasteryCurve,
            Names(source.Tags),
            Name(SkillJsonImportValueRules.GetWireValue(source.LearnSource)),
            Names(source.LearnRequirements),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.UnlockMode)),
            Names(source.KnowledgeRequirements),
            NameIntMap(source.SkillLevelRequirements),
            NameIntMap(source.AttributeRequirements),
            Names(source.AchievementRequirements),
            Names(source.UpgradeSourceSkillIds),
            source.RetainSourceSkillsOnUnlock,
            Name(
                SkillRootCombatImportValueRules.GetWireValue(
                    source.CoreSkillTransitionMode
                )
            ),
            Names(source.MasterySources),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.GrowthTier)),
            NameIntMap(source.AttributeGrowthProgress),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.PracticeTier)),
            ProjectAttributeModifiers(source.AttributeModifiers),
            source.LevelDescriptionTemplate,
            source.LevelDescriptionConfigs,
            ProjectCombat(source.CombatProfile, source.SkillId),
            ProjectContingency(source.ContingencyAutomationProfile)
        );
    }

    internal static IReadOnlyDictionary<StringName, SkillDefinition> ProjectIndex(
        IEnumerable<SkillImportModel>? sources
    )
    {
        var result = new Dictionary<StringName, SkillDefinition>();
        foreach (SkillImportModel source in sources ?? Array.Empty<SkillImportModel>())
        {
            if (source == null)
                continue;
            StringName skillId = Name(source.SkillId);
            if (skillId == "")
                continue;
            result.Add(skillId, Project(source));
        }
        return new ReadOnlyDictionary<StringName, SkillDefinition>(result);
    }

    internal static CombatSkillDefinition? ProjectCombat(
        CombatSkillImportModel? source,
        SkillImportIdentifier fallbackSkillId
    )
    {
        if (source == null)
            return null;
        StringName skillId = Name(source.SkillId);
        if (skillId == "")
            skillId = Name(fallbackSkillId);
        return new CombatSkillDefinition(
            skillId,
            Name(SkillJsonImportValueRules.GetWireValue(source.TargetMode)),
            Name(SkillJsonImportValueRules.GetWireValue(source.TargetTeamFilter)),
            Name(SkillJsonImportValueRules.GetWireValue(source.RangePattern)),
            source.RangeValue,
            Name(SkillJsonImportValueRules.GetWireValue(source.AreaPattern)),
            source.AreaValue,
            source.RequiresLos,
            source.ApCost,
            source.MpCost,
            source.StaminaCost,
            source.CooldownTu,
            source.CastingTimeTu,
            source.CastingMaintenanceDc,
            source.CastingSpellControlDc,
            Name(SkillJsonImportValueRules.GetWireValue(source.PendingCastBindingMode)),
            source.AttackRollBonus,
            Name(SkillJsonImportValueRules.GetWireValue(source.AttackResolutionMode)),
            source.AuraCost,
            source.LevelOverrides,
            Name(SkillRootCombatImportValueRules.GetWireValue(source.MasteryTriggerMode)),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.MasteryAmountMode)),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.SpellFateMode)),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.SpellCriticalMode)),
            source.SpellCriticalMpRefundPercent,
            source.FumbleProtectionCurve,
            source.FumbleProtectionExtraMpPercent,
            Name(SkillRootCombatImportValueRules.GetWireValue(source.BacklashMode)),
            source.BacklashTargetFilter.HasValue
                ? Name(
                    SkillJsonImportValueRules.GetWireValue(
                        source.BacklashTargetFilter.Value
                    )
                )
                : default,
            source.BacklashOffsetRadius,
            Name(SkillRootCombatImportValueRules.GetWireValue(source.AreaOriginMode)),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.AreaDirectionMode)),
            Names(source.AiTags),
            Names(source.DeliveryCategories),
            Name(source.SpecialResolutionProfileId),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.TargetSelectionMode)),
            source.MinTargetCount,
            source.MaxTargetCount,
            source.AllowRepeatTarget,
            source.MaxHitsPerTarget,
            Name(SkillRootCombatImportValueRules.GetWireValue(source.SelectionOrderMode)),
            ProjectEffects(source.EffectDefs),
            ProjectEffects(source.PassiveEffectDefs),
            ProjectCastVariants(source.CastVariants),
            Names(source.RequiredWeaponFamilies),
            Names(source.ExcludedWeaponFamilies),
            Names(source.ExcludedWeaponTypeIds),
            source.RequiresEquippedShield,
            source.MasteryLowHpBonusMultiplier,
            source.MasteryLowHpThresholdPercent,
            weaponRangePolicy: Name(
                SkillRootCombatImportValueRules.GetWireValue(source.WeaponRangePolicy)
            ),
            projectileKind: Name(
                SkillRootCombatImportValueRules.GetWireValue(source.ProjectileKind)
            ),
            attackRollBonusStatusId: Name(source.AttackRollBonusStatusId),
            attackRollBonusStatusStackDivisor: source.AttackRollBonusStatusStackDivisor,
            randomChainAttackCount: source.RandomChainAttackCount,
            randomChainContinueOnMiss: source.RandomChainContinueOnMiss,
            requiredWeaponTypeIds: Names(source.RequiredWeaponTypeIds),
            allowsNaturalWeapon: source.AllowsNaturalWeapon,
            windup: ProjectWindup(source.WindupProfile),
            requiresHeavyWeapon: source.RequiresHeavyWeapon,
            attackDefenseMode: Name(
                SkillJsonImportValueRules.GetWireValue(source.AttackDefenseMode)
            ),
            spellReaction: ProjectSpellReaction(source.SpellReactionProfile),
            rangeMovePointCapacityMultiplier: source.RangeMovePointCapacityMultiplier,
            directionalPiercing: ProjectDirectional(source.DirectionalPiercingProfile),
            groundEffectRequireFullArea: source.GroundEffectRequireFullArea,
            groundEffectRequireEmpty: source.GroundEffectRequireEmpty,
            groundEffectRequireTraversable: source.GroundEffectRequireTraversable,
            approachAttack: source.ApproachAttackProfile == null
                ? null
                : new CombatApproachAttackDefinition(
                    source.ApproachAttackProfile.MaximumPathHeightDeltaFromOrigin
                ),
            lineThroughAttack: ProjectLineThrough(source.LineThroughAttackProfile),
            masteryBaseAmount: source.MasteryBaseAmount,
            rangedWeaponReaction: ProjectRangedReaction(
                source.RangedWeaponReactionProfile
            ),
            sequentialLineHit: ProjectSequential(source.SequentialLineHitProfile),
            unitTargetResolutionMode: Name(
                SkillRootCombatImportValueRules.GetWireValue(
                    source.UnitTargetResolutionMode
                )
            ),
            mpCostPerTargetSlot: source.MpCostPerTargetSlot,
            staminaCostPerTargetSlot: source.StaminaCostPerTargetSlot,
            excludedTargetCreatureTypeTags: Names(
                source.ExcludedTargetCreatureTypeTags
            )
        );
    }

    internal static CombatEffectDefinition ProjectEffect(CombatEffectImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new CombatEffectDefinition(
            effectType: Name(SkillFullCombatEffectClosedSpec.GetWireValue(source.Kind)),
            effectTargetTeamFilter: NullableName(
                source.EffectTargetTeamFilter,
                SkillCombatEffectValueRules.GetWireValue
            ),
            statusId: Name(source.StatusId),
            saveFailureStatusId: Name(source.SaveFailureStatusId),
            terrainEffectId: Name(source.TerrainEffectId),
            terrainReplaceTo: Name(source.TerrainReplaceTo),
            heightDelta: source.HeightDelta,
            requiresWeapon: source.RequiresWeapon,
            addWeaponDice: source.AddWeaponDice,
            preventRepeatTarget: source.PreventRepeatTarget,
            forcedMoveMode: NullableName(
                source.ForcedMoveMode,
                SkillCombatEffectValueRules.GetWireValue
            ),
            minSkillLevel: source.MinSkillLevel,
            maxSkillLevel: source.MaxSkillLevel,
            damageTag: NullableName(
                source.DamageTag,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            damageRatioPercent: source.DamageRatioPercent,
            preResistanceDamageMultiplier: source.PreResistanceDamageMultiplier,
            bonusCondition: NullableName(
                source.BonusCondition,
                SkillCombatEffectValueRules.GetWireValue
            ),
            hpRatioThresholdPercent: source.HpRatioThresholdPercent,
            damageCategory: NullableName(
                source.DamageCategory,
                SkillCombatEffectValueRules.GetWireValue
            ),
            drBypassTag: Name(source.DrBypassTag),
            diceCount: source.DiceCount,
            diceSides: source.DiceSides,
            diceBonus: source.DiceBonus,
            bonusDamageDiceCount: source.BonusDamageDiceCount,
            bonusDamageDiceSides: source.BonusDamageDiceSides,
            bonusDamageDiceBonus: source.BonusDamageDiceBonus,
            saveDc: source.SaveDc,
            saveDcMode: Name(
                SkillCombatEffectValueRules.GetWireValue(source.SaveDcMode)
            ),
            saveDcSourceAbility: NullableName(
                source.SaveDcSourceAbility,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            saveAbility: NullableName(
                source.SaveAbility,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            savePartialOnSuccess: source.SavePartialOnSuccess,
            saveTag: NullableName(
                source.SaveTag,
                SkillCombatEffectValueRules.GetWireValue
            ),
            thresholdBaseValue: source.ThresholdBaseValue,
            thresholdLevelAnchor: source.ThresholdLevelAnchor,
            thresholdLevelBonusPerDelta: source.ThresholdLevelBonusPerDelta,
            thresholdMaxHpRatioPercent: source.ThresholdMaxHpRatioPercent,
            thresholdCapMaxHpRatioPercent: source.ThresholdCapMaxHpRatioPercent,
            soulFractureDurationTu: source.SoulFractureDurationTu,
            healMultiplierPercent: source.HealMultiplierPercent,
            shieldGainMultiplierPercent: source.ShieldGainMultiplierPercent,
            appliedStatusDurationTu: source.AppliedStatusDurationTu,
            durationTu: source.DurationTu,
            tickIntervalTu: source.TickIntervalTu,
            effectTags: Names(source.EffectTags),
            triggerCondition: NullableName(
                source.TriggerCondition,
                SkillCombatEffectValueRules.GetWireValue
            ),
            power: source.Power,
            rangeBonus: source.RangeBonus,
            forcedMoveDistance: source.ForcedMoveDistance,
            jumpBaseBudget: source.JumpBaseBudget,
            jumpStrScale: source.JumpStrScale,
            jumpArcRatio: source.JumpArcRatio,
            jumpRangeMultiplier: source.JumpRangeMultiplier,
            diceSidesBase: source.DiceSidesBase,
            diceSidesPerConstitutionMod: source.DiceSidesPerConstitutionMod,
            diceSidesPerWillpowerMod: source.DiceSidesPerWillpowerMod,
            parameters: ProjectParameters(source.Payload),
            effectCategories: Names(source.EffectCategories),
            allowRepeatHitsAcrossSteps: source.AllowRepeatHitsAcrossSteps,
            tickEffectType: Name(
                SkillCombatEffectValueRules.GetWireValue(source.TickEffectType)
            ),
            lifetimePolicy: Name(
                SkillCombatEffectValueRules.GetWireValue(source.LifetimePolicy)
            ),
            moveCostDelta: source.MoveCostDelta,
            renderOverlayId: Name(source.RenderOverlayId),
            overlayPriority: source.OverlayPriority,
            displayName: source.DisplayName,
            accuracyModifierSpec: null,
            doesNotStackWithStatusId: Name(source.DoesNotStackWithStatusId),
            doesNotStackWithStatusIds: Names(source.DoesNotStackWithStatusIds),
            damageTags: Names(
                source.DamageTags,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            useWeaponPhysicalDamageTag: source.UseWeaponPhysicalDamageTag,
            resolveAsWeaponAttack: source.ResolveAsWeaponAttack,
            stopOnMiss: source.StopOnMiss,
            stopOnTargetDown: source.StopOnTargetDown,
            removeHarmful: source.RemoveHarmful,
            removeHarmfulFromAllies: source.RemoveHarmfulFromAllies,
            removeBeneficial: source.RemoveBeneficial,
            removeBeneficialFromEnemies: source.RemoveBeneficialFromEnemies,
            requireDamageApplied: source.RequireDamageApplied,
            maxStatusRemoved: source.MaxStatusRemoved,
            minHpAfterDamage: source.MinHpAfterDamage,
            deathPreventionPriority: source.DeathPreventionPriority,
            attackRollPenalty: source.AttackRollPenalty,
            undispellable: source.Undispellable,
            dispellableMagic: source.DispellableMagic,
            dispellableHarmfulMagic: source.DispellableHarmfulMagic,
            dispellableBeneficialMagic: source.DispellableBeneficialMagic,
            mitigationTier: NullableName(
                source.MitigationTier,
                SkillCombatEffectValueRules.GetWireValue
            ),
            secondaryHitDcBase: source.SecondaryHitDcBase,
            debuffCountThreshold: source.DebuffCountThreshold,
            baseHeal: source.BaseHeal,
            healPerLevel: source.HealPerLevel,
            conModBase: source.ConModBase,
            conModPer2Levels: source.ConModPer2Levels,
            bodySizeCategory: NullableName(
                source.BodySizeCategory,
                SkillCombatEffectValueRules.GetWireValue
            ),
            stackBehavior: Name(
                SkillCombatEffectValueRules.GetWireValue(source.StackBehavior)
            ),
            stackLimit: source.StackLimit,
            triggerEvent: NullableName(
                source.TriggerEvent,
                SkillCombatEffectValueRules.GetWireValue
            ),
            triggerStatusId: Name(source.TriggerStatusId),
            consumedStatusId: Name(source.ConsumedStatusId),
            requiredTargetStatusId: Name(source.RequiredTargetStatusId),
            requiredTargetStatusMinStacks: source.RequiredTargetStatusMinStacks,
            dicePerConsumedStack: source.DicePerConsumedStack,
            diceSidesPerStack: source.DiceSidesPerStack,
            apGain: source.ApGain,
            freeMovePointsGain: source.FreeMovePointsGain,
            countsAsDebuffOverride: source.CountsAsDebuffOverride,
            countsAsDebuff: source.CountsAsDebuff,
            lockCounterattack: source.LockCounterattack,
            lockGuard: source.LockGuard,
            lockDodgeBonus: source.LockDodgeBonus,
            lockCrit: source.LockCrit,
            saveBonus: source.SaveBonus,
            controlSaveBonus: source.ControlSaveBonus,
            passiveReduction: source.PassiveReduction,
            contentDr: source.ContentDr,
            guardBlock: source.GuardBlock,
            mainSkillLockOtherDebuffCount: source.MainSkillLockOtherDebuffCount,
            saveAdvantageTags: Names(
                source.SaveAdvantageTags,
                SkillCombatEffectValueRules.GetWireValue
            ),
            saveDisadvantageTags: Names(
                source.SaveDisadvantageTags,
                SkillCombatEffectValueRules.GetWireValue
            ),
            saveImmunityTags: Names(
                source.SaveImmunityTags,
                SkillCombatEffectValueRules.GetWireValue
            ),
            equipmentDurabilitySlotWeights: ProjectSlotWeights(
                source.EquipmentDurabilitySlotWeights
            ),
            requiredTargetStatusSourceSelector: NullableName(
                source.RequiredTargetStatusSourceSelector,
                SkillCombatEffectValueRules.GetWireValue
            ),
            bonusConditionCreatureTypeTag: Name(
                source.BonusConditionCreatureTypeTag
            ),
            mitigationBypassDamageTags: Names(
                source.MitigationBypassDamageTags,
                SkillRootCombatImportValueRules.GetWireValue
            ),
            mitigationBypassTiers: Names(
                source.MitigationBypassTiers,
                SkillCombatEffectValueRules.GetWireValue
            ),
            extraDamageSegments: ProjectDamageSegments(source.ExtraDamageSegments),
            targetDamageMultiplierRules: ProjectTargetDamageMultiplierRules(
                source.TargetDamageMultiplierRules
            ),
            attackRollBonus: source.AttackRollBonus,
            consumeOnNextAttackCheck: source.ConsumeOnNextAttackCheck,
            consumeOnNextSave: source.ConsumeOnNextSave,
            attackRollAdvantage: source.AttackRollAdvantage,
            sourceBoundWeaponBonusDamageDiceCount:
                source.SourceBoundWeaponBonusDamageDiceCount,
            sourceBoundWeaponBonusDamageDiceSides:
                source.SourceBoundWeaponBonusDamageDiceSides,
            sourceBoundWeaponBonusDamageDiceBonus:
                source.SourceBoundWeaponBonusDamageDiceBonus,
            chargeTrapImmunityMinSkillLevel: source.ChargeTrapImmunityMinSkillLevel,
            pathStepAreaPattern: Name(
                SkillCombatEffectValueRules.GetWireValue(source.PathStepAreaPattern)
            ),
            pathStepRadius: source.PathStepRadius,
            pathStepLogLabel: source.PathStepLogLabel,
            repeatHitStatusId: Name(source.RepeatHitStatusId),
            repeatHitStatusThreshold: source.RepeatHitStatusThreshold,
            repeatHitStatusMinSkillLevel: source.RepeatHitStatusMinSkillLevel,
            repeatHitStatusPower: source.RepeatHitStatusPower,
            repeatHitStatusDurationTu: source.RepeatHitStatusDurationTu,
            repeatHitStatusLogTemplate: source.RepeatHitStatusLogTemplate,
            fixedAttackCount: source.FixedAttackCount,
            weaponDiceMultiplier: source.WeaponDiceMultiplier,
            bonusWeaponDiceMultiplier: source.BonusWeaponDiceMultiplier,
            bonusDamageSeparateEvent: source.BonusDamageSeparateEvent,
            meleeComboStackGainBonus: source.MeleeComboStackGainBonus,
            comboAttackBonusStatusId: Name(source.ComboAttackBonusStatusId),
            comboAttackBonusStackDivisor: source.ComboAttackBonusStackDivisor,
            upkeepResource: NullableName(
                source.UpkeepResource,
                SkillCombatEffectValueRules.GetWireValue
            ),
            upkeepIntervalTu: source.UpkeepIntervalTu,
            upkeepBaseCost: source.UpkeepBaseCost,
            upkeepEscalationIntervalTu: source.UpkeepEscalationIntervalTu,
            upkeepCostMultiplier: source.UpkeepCostMultiplier,
            breakOnHardControl: source.BreakOnHardControl,
            terminationStatusId: Name(source.TerminationStatusId),
            terminationStatusDurationTu: source.TerminationStatusDurationTu,
            terminationAttackRollPenalty: source.TerminationAttackRollPenalty,
            terminationCooldownTu: source.TerminationCooldownTu,
            requiredTargetCreatureTypeTag: Name(source.RequiredTargetCreatureTypeTag),
            requiredTargetMinCognition: source.RequiredTargetMinCognition.HasValue
                ? BattleCognitionContentRules.ToKind(
                    Name(
                        SkillCombatEffectValueRules.GetWireValue(
                            source.RequiredTargetMinCognition.Value
                        )
                    )
                )
                : BattleCognitionKind.Unknown,
            sourceRetreatDistance: source.SourceRetreatDistance,
            grappleMaxHeightGain: source.GrappleMaxHeightGain,
            healToHpPercentFloor: source.HealToHpPercentFloor,
            healMissingHpPercent: source.HealMissingHpPercent,
            maxAffectedTargets: source.MaxAffectedTargets,
            excludeSource: source.ExcludeSource,
            targetOrder: NullableName(
                source.TargetOrder,
                SkillCombatEffectValueRules.GetWireValue
            ),
            terrainContactMode: NullableName(
                source.TerrainContactMode,
                SkillCombatEffectValueRules.GetWireValue
            ),
            terrainEffectiveTriggerCount: source.TerrainEffectiveTriggerCount,
            terrainRequiresGroundContact: source.TerrainRequiresGroundContact,
            terrainRecheckFromInside: source.TerrainRecheckFromInside,
            terrainMaxActiveInstancesPerSource:
                source.TerrainMaxActiveInstancesPerSource,
            terrainReplaceExistingFromSource: source.TerrainReplaceExistingFromSource,
            shieldFamily: Name(source.ShieldFamily),
            shieldAttributeModifierId: NullableName(
                source.ShieldAttributeModifierId,
                SkillCombatEffectValueRules.GetWireValue
            ),
            shieldRollPerTarget: source.ShieldRollPerTarget,
            followUpDamageMultiplierPercent: source.FollowUpDamageMultiplierPercent,
            followUpAttackRollBonusCurve: source.FollowUpAttackRollBonusCurve,
            forcedMoveMaxTargetBodySize: source.ForcedMoveMaxTargetBodySize,
            saveFailureStatusOutcomes: ProjectWeightedOutcomes(
                source.SaveFailureStatusOutcomes
            ),
            chainDamage: HasChainDamageFields(source)
                ? new CombatChainDamageDefinition(
                    source.ChainBaseHopRange,
                    source.ChainConductiveHopRange,
                    source.ChainMaxTotalTargets,
                    Names(source.ChainConductiveStatusIds),
                    Names(source.ChainConductiveTerrainEffectIds),
                    source.ChainBacklashHopRangeBonus
                )
                : null,
            saveDcBonus: source.SaveDcBonus,
            skipTurn: source.SkipTurn,
            breakOnPositiveDamage: source.BreakOnPositiveDamage,
            onRemovedStatusId: Name(source.OnRemovedStatusId),
            onRemovedStatusSaveImmunityTags: Names(
                source.OnRemovedStatusSaveImmunityTags,
                SkillCombatEffectValueRules.GetWireValue
            ),
            onRemovedStatusUndispellable: source.OnRemovedStatusUndispellable,
            onRemovedStatusConsumeAfterNormalTurn:
                source.OnRemovedStatusConsumeAfterNormalTurn
        );
    }

    private static bool HasChainDamageFields(CombatEffectImportModel source) =>
        source.Kind == CombatEffectImportKind.ChainDamage
        || source.ChainBaseHopRange != 0
        || source.ChainConductiveHopRange != 0
        || source.ChainMaxTotalTargets != 0
        || source.ChainBacklashHopRangeBonus != 0
        || source.ChainConductiveStatusIds.Count > 0
        || source.ChainConductiveTerrainEffectIds.Count > 0;

    private static IReadOnlyList<AttributeModifierDefinition> ProjectAttributeModifiers(
        IReadOnlyList<AttributeModifierImportModel> values
    ) => values.Select(value => new AttributeModifierDefinition(
        Name(value.AttributeId),
        Name(SkillRootCombatImportValueRules.GetWireValue(value.Mode)),
        value.Value,
        value.ValuePerRank,
        Name(value.SourceType),
        Name(value.SourceId)
    )).ToArray();

    private static ContingencyAutomationDefinition? ProjectContingency(
        ContingencyAutomationImportModel? source
    ) => source == null
        ? null
        : new ContingencyAutomationDefinition(
            source.CanBeStoredInContingency,
            source.MinContingencySkillLevel,
            Name(source.EffectCategory),
            Names(source.Tags),
            source.ContingencyLoadOverride,
            Names(source.AllowedTargetResolvers),
            source.RequiresManualTargeting,
            source.AllowedParameterBindings.ToDictionary(
                pair => pair.Key.Value,
                pair => ProjectBindingValue(pair.Value),
                StringComparer.Ordinal
            )
        );

    private static object ProjectBindingValue(ContingencyParameterBindingImportValue value) =>
        value switch
        {
            ContingencyBoolBindingImportValue binding => binding.Value,
            ContingencyIntBindingImportValue binding => binding.Value,
            ContingencyFloatBindingImportValue binding => binding.Value,
            ContingencyStringBindingImportValue binding => binding.Value,
            ContingencyStringListBindingImportValue binding => binding.Values.ToArray(),
            _ => throw new InvalidOperationException(
                $"Unsupported contingency binding import value {value?.GetType().Name}."
            ),
        };

    private static CombatWindupDefinition? ProjectWindup(CombatWindupImportModel? source) =>
        source == null
            ? null
            : new CombatWindupDefinition(
                source.StaminaCostPerTier,
                source.WeaponDicePerTier,
                source.SkillLevelTierCaps,
                source.BaseWeaponDiceMultipliers
            );

    private static CombatDirectionalPiercingDefinition? ProjectDirectional(
        CombatDirectionalPiercingImportModel? source
    ) => source == null
        ? null
        : new CombatDirectionalPiercingDefinition(
            source.BaseDamagePercentCurve,
            source.SuccessfulHitDecayPercent,
            source.MinimumDamagePercent,
            source.StaminaFlatBase,
            source.StaminaRangeSquareCoefficient,
            source.StaminaStrengthSquareScale,
            source.MinimumStaminaCost,
            source.MaximumHeightDelta
        );

    private static CombatLineThroughAttackDefinition? ProjectLineThrough(
        CombatLineThroughAttackImportModel? source
    ) => source == null
        ? null
        : new CombatLineThroughAttackDefinition(
            source.MaximumWeaponRange,
            source.IntermediateWeaponDiceMultiplier,
            source.PrimaryWeaponDiceMultiplierCurve,
            source.PrimaryAttackRollBonusCurve,
            source.SuccessfulIntermediateHitBonusWeaponDice,
            source.SuccessfulIntermediateHitAttackRollBonus,
            source.SuccessfulIntermediateHitBonusCapCurve
        );

    private static CombatSequentialLineHitDefinition? ProjectSequential(
        CombatSequentialLineHitImportModel? source
    ) => source == null
        ? null
        : new CombatSequentialLineHitDefinition(
            source.MinimumPrimaryDistanceCurve,
            source.ContinuationRangeCurve,
            source.FollowUpAttackPenaltyCurve
        );

    private static CombatSpellReactionDefinition? ProjectSpellReaction(
        CombatSpellReactionImportModel? source
    ) => source == null
        ? null
        : new CombatSpellReactionDefinition(
            Name(source.TriggerDeliveryCategory),
            Name(source.ReactionSkillId),
            Name(source.ReadinessStatusId),
            Name(source.RequiredWeaponFamily),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.SaveAbility)),
            Name(source.SaveTag),
            source.BaseSaveDc,
            source.HpDamageDivisor,
            source.AttackRollBonusBySkillLevel,
            source.SaveDcBonusBySkillLevel,
            source.RequireHpDamage,
            source.ConsumeOnTrigger,
            source.ExpireOnOwnerTurnStart
        );

    private static CombatRangedWeaponReactionDefinition? ProjectRangedReaction(
        CombatRangedWeaponReactionImportModel? source
    ) => source == null
        ? null
        : new CombatRangedWeaponReactionDefinition(
            Name(source.ReadinessStatusId),
            Names(source.TriggerWeaponFamilies),
            Name(SkillRootCombatImportValueRules.GetWireValue(source.DamageTag)),
            Name(SkillJsonImportValueRules.GetWireValue(source.AttackDefenseMode)),
            source.AttackRollBonusBySkillLevel,
            source.ConsumeStatusStacks,
            source.TriggerOnHit,
            source.TriggerOnMiss,
            source.AllowCritical
        );

    private static IReadOnlyList<CombatEffectDefinition> ProjectEffects(
        IReadOnlyList<CombatEffectImportModel> values
    ) => values.Select(ProjectEffect).ToArray();

    private static IReadOnlyList<CombatCastVariantDefinition> ProjectCastVariants(
        IReadOnlyList<CombatCastVariantImportModel> values
    ) => values.Select(value => new CombatCastVariantDefinition(
        Name(value.VariantId),
        value.DisplayName,
        value.Description,
        value.MinSkillLevel,
        Name(SkillJsonImportValueRules.GetWireValue(value.TargetMode)),
        Name(SkillRootCombatImportValueRules.GetWireValue(value.FootprintPattern)),
        value.RequiredCoordCount,
        Names(value.AllowedBaseTerrains, SkillRootCombatImportValueRules.GetWireValue),
        ProjectEffects(value.EffectDefs),
        ProjectCastVariantParameters(value.Payload),
        Name(SkillRootCombatImportValueRules.GetWireValue(value.ProjectileKindOverride))
    )).ToArray();

    private static IReadOnlyDictionary<string, object> ProjectCastVariantParameters(
        CombatCastVariantPayloadImportModel payload
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (payload.Square2Corner.HasValue)
        {
            result["square2_corner"] =
                SkillRootCombatImportValueRules.GetWireValue(payload.Square2Corner.Value);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object> ProjectParameters(
        ICombatEffectPayloadImportModel payload
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        switch (payload)
        {
            case EmptyCombatEffectPayloadImportModel:
                break;
            case StatusEffectPayloadImportModel value:
                AddName(result, "breaks_barrier_layer", value.BreaksBarrierLayer);
                AddName(result, "source_skill_id", value.SourceSkillId);
                break;
            case HealEffectPayloadImportModel value:
                result["con_mod_heal"] = value.ConModHeal;
                break;
            case EquipmentDurabilityDamageEffectPayloadImportModel value:
                result["max_damaged_items"] = value.MaxDamagedItems;
                result["target_slots"] = Names(
                    value.TargetSlots,
                    SkillCombatEffectValueRules.GetWireValue
                );
                break;
            case RepeatAttackUntilFailEffectPayloadImportModel value:
                result["base_attack_bonus"] = value.BaseAttackBonus;
                result["cost_resource"] =
                    SkillCombatEffectValueRules.GetWireValue(value.CostResource);
                result["follow_up_cost_addition"] = value.FollowUpCostAddition;
                result["follow_up_cost_multiplier"] = value.FollowUpCostMultiplier;
                result["follow_up_attack_penalty"] = value.FollowUpAttackPenalty;
                result["penalty_free_stages_by_level"] = value.PenaltyFreeStagesByLevel
                    .ToDictionary(
                        pair => pair.Key.ToString(CultureInfo.InvariantCulture),
                        pair => (object)pair.Value,
                        StringComparer.Ordinal
                    );
                result["same_target_only"] = value.SameTargetOnly;
                result["follow_up_fixed_cost"] = value.FollowUpFixedCost;
                result["exponential_penalty"] = value.ExponentialPenalty;
                result["stop_on_insufficient_resource"] =
                    value.StopOnInsufficientResource;
                break;
            case LayeredBarrierEffectPayloadImportModel value:
                result["area_pattern"] =
                    SkillJsonImportValueRules.GetWireValue(value.AreaPattern);
                result["profile_id"] = value.ProfileId.Value;
                result["radius_cells"] = value.RadiusCells;
                result["save_dc"] = value.SaveDc;
                break;
            case GradedSaveExecuteEffectPayloadImportModel value:
                result["critical_failure_damage_dice_count"] =
                    value.CriticalFailureDamageDiceCount;
                result["critical_failure_damage_dice_sides"] =
                    value.CriticalFailureDamageDiceSides;
                result["critical_failure_execute_threshold_max_hp_percent"] =
                    value.CriticalFailureExecuteThresholdMaxHpPercent;
                result["critical_failure_frightened_duration_tu"] =
                    value.CriticalFailureFrightenedDurationTu;
                result["critical_failure_stunned_duration_tu"] =
                    value.CriticalFailureStunnedDurationTu;
                result["failure_damage_dice_count"] = value.FailureDamageDiceCount;
                result["failure_damage_dice_sides"] = value.FailureDamageDiceSides;
                result["failure_execute_threshold_fixed"] =
                    value.FailureExecuteThresholdFixed;
                result["failure_execute_threshold_max_hp_percent"] =
                    value.FailureExecuteThresholdMaxHpPercent;
                result["failure_frightened_duration_tu"] =
                    value.FailureFrightenedDurationTu;
                result["failure_reaction_lock_duration_tu"] =
                    value.FailureReactionLockDurationTu;
                result["profile_id"] = value.ProfileId.Value;
                result["success_aftershock_duration_tu"] =
                    value.SuccessAftershockDurationTu;
                break;
            case DispelMagicEffectPayloadImportModel value:
                AddName(result, "breaks_barrier_layer", value.BreaksBarrierLayer);
                break;
            case OnKillGainResourcesEffectPayloadImportModel value:
                result["grant_scope"] =
                    SkillCombatEffectValueRules.GetWireValue(value.GrantScope);
                result["require_target_defeated_by_same_skill"] =
                    value.RequireTargetDefeatedBySameSkill;
                result["stack_on_multiple_kills"] = value.StackOnMultipleKills;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported combat effect payload import model {payload.GetType().Name}."
                );
        }
        return result;
    }

    private static void AddName(
        IDictionary<string, object> target,
        string key,
        SkillImportStringName value
    )
    {
        string projected = value.Value;
        if (projected.Length > 0)
            target[key] = projected;
    }

    private static IReadOnlyList<EquipmentSlotWeightDefinition> ProjectSlotWeights(
        IReadOnlyList<CombatEffectSlotWeightImportModel> values
    ) => values.Select(value => new EquipmentSlotWeightDefinition
    {
        SlotId = Name(SkillCombatEffectValueRules.GetWireValue(value.SlotId)),
        Weight = value.Weight,
    }).ToArray();

    private static IReadOnlyList<CombatDamageSegmentDefinition> ProjectDamageSegments(
        IReadOnlyList<CombatDamageSegmentImportModel> values
    ) => values.Select(value => new CombatDamageSegmentDefinition(
        NullableName(value.DamageTag, SkillRootCombatImportValueRules.GetWireValue),
        value.Power,
        value.DiceCount,
        value.DiceSides,
        value.DiceBonus,
        value.PreResistanceDamageMultiplier,
        Names(value.DamageTags, SkillRootCombatImportValueRules.GetWireValue),
        Names(
            value.MitigationBypassDamageTags,
            SkillRootCombatImportValueRules.GetWireValue
        ),
        Names(
            value.MitigationBypassTiers,
            SkillCombatEffectValueRules.GetWireValue
        ),
        value.DoubleDiceOnCritical
    )).ToArray();

    private static IReadOnlyList<CombatTargetDamageMultiplierRuleDefinition>
        ProjectTargetDamageMultiplierRules(
            IReadOnlyList<CombatTargetDamageMultiplierRuleImportModel> values
        ) => values.Select(value => new CombatTargetDamageMultiplierRuleDefinition(
            Names(value.AnyCreatureTypeTags),
            Names(value.AllCreatureTypeTags),
            Names(value.ExcludedCreatureTypeTags),
            value.MultiplierPercent
        )).ToArray();

    private static IReadOnlyList<CombatWeightedStatusOutcomeDefinition>
        ProjectWeightedOutcomes(
            IReadOnlyList<CombatWeightedStatusOutcomeImportModel> values
        ) => values.Select(value => new CombatWeightedStatusOutcomeDefinition(
            Name(value.OutcomeId),
            value.Weight,
            ProjectEffect(value.StatusEffect)
        )).ToArray();

    private static IReadOnlyList<StringName> Names(
        IEnumerable<SkillImportIdentifier>? values
    ) => values?.Select(Name).ToArray() ?? Array.Empty<StringName>();

    private static IReadOnlyList<StringName> Names(
        IEnumerable<SkillImportStringName>? values
    ) => values?.Select(Name).ToArray() ?? Array.Empty<StringName>();

    private static IReadOnlyList<StringName> Names<T>(
        IEnumerable<T>? values,
        Func<T, string> wireValue
    ) => values?.Select(value => Name(wireValue(value))).ToArray()
        ?? Array.Empty<StringName>();

    private static IReadOnlyDictionary<StringName, int> NameIntMap(
        IEnumerable<KeyValuePair<SkillImportIdentifier, int>> values
    ) => new ReadOnlyDictionary<StringName, int>(
        values.ToDictionary(pair => Name(pair.Key), pair => pair.Value)
    );

    private static StringName NullableName<T>(T? value, Func<T, string> wireValue)
        where T : struct => value.HasValue
            ? Name(wireValue(value.Value))
            : new StringName("");

    private static StringName Name(SkillImportIdentifier value) => Name(value.Value);
    private static StringName Name(SkillImportStringName value) => Name(value.Value);
    private static StringName Name(SkillImportAssetId value) => Name(value.Value);
    private static StringName Name(SkillImportStatId value) => Name(value.Value);
    private static StringName Name(string? value) => new(value ?? "");
}
