using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：驱散/装备耐久/处决/治疗/状态等效果应用与结果构建。按阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private static readonly StringName PhantasmalKillExecuteDeathSource =
        BattleDeathResolutionRules.PhantasmalKillExecuteDeathSource;
    private static readonly StringName PhantasmalKillDamageTag = "psychic";
    private static readonly StringName PhantasmalKillAftershockStatus = "aftershock";
    private static readonly StringName PhantasmalKillReactionLockStatus = "reaction_lock";
    private static readonly StringName PhantasmalKillFrightenedStatus = "frightened";
    private static readonly StringName PhantasmalKillStunnedStatus = "stunned";









    private static EquipmentDurabilitySelection BuildEquipmentDurabilitySelection(
        EquipmentState equipmentView,
        StringName entrySlotId,
        StringName slotId
    )
    {
        StringName normalizedEntrySlot = ProgressionDataUtils.to_string_name(entrySlotId);
        if (equipmentView == null || normalizedEntrySlot == "")
        {
            return EquipmentDurabilitySelection.Empty;
        }
        EquipmentEntryState entry = equipmentView.GetEntry(normalizedEntrySlot);
        if (entry == null || entry.IsEmpty())
        {
            return EquipmentDurabilitySelection.Empty;
        }
        EquipmentInstanceState equipmentInstance = entry.GetEquipmentInstance();
        if (equipmentInstance == null || equipmentInstance.current_durability <= 0)
        {
            return EquipmentDurabilitySelection.Empty;
        }
        return new EquipmentDurabilitySelection(
            normalizedEntrySlot,
            ProgressionDataUtils.to_string_name(slotId),
            new List<StringName>(entry.occupied_slot_ids),
            equipmentInstance
        );
    }



    private static bool IsEquipmentDurabilityEntryAllowed(
        StringName entrySlotId,
        IReadOnlyList<StringName> occupiedSlots,
        IReadOnlyList<StringName> allowedSlots
    )
    {
        if (allowedSlots.Count == 0 || HasStringName(allowedSlots, entrySlotId))
        {
            return true;
        }
        foreach (StringName occupiedSlotId in occupiedSlots)
        {
            if (HasStringName(allowedSlots, occupiedSlotId))
            {
                return true;
            }
        }
        return false;
    }

    private static int GetEquipmentDurabilitySlotWeight(
        IReadOnlyDictionary<StringName, int> weightMap,
        StringName entrySlotId,
        IReadOnlyList<StringName> occupiedSlots
    )
    {
        if (weightMap.Count == 0)
        {
            return 1;
        }
        int weight = GetEquipmentDurabilityWeightForSlot(weightMap, entrySlotId);
        foreach (StringName occupiedSlotId in occupiedSlots)
        {
            weight = Math.Max(
                weight,
                GetEquipmentDurabilityWeightForSlot(weightMap, occupiedSlotId)
            );
        }
        return Math.Max(weight, 1);
    }

    private static int GetEquipmentDurabilityWeightForSlot(
        IReadOnlyDictionary<StringName, int> weightMap,
        StringName slotId
    )
    {
        if (weightMap == null || IsEmpty(slotId))
        {
            return 0;
        }
        if (weightMap.TryGetValue(slotId, out int directValue))
        {
            return directValue;
        }
        return 0;
    }

    private readonly record struct EquipmentDurabilitySelection(
        StringName EntrySlotId,
        StringName SlotId,
        IReadOnlyList<StringName> OccupiedSlotIds,
        EquipmentInstanceState EquipmentInstance
    )
    {
        public static EquipmentDurabilitySelection Empty =>
            new(
                new StringName(""),
                new StringName(""),
                Array.Empty<StringName>(),
                null
            );

        public bool IsValid => EntrySlotId != "" && EquipmentInstance != null;
    }



    private bool TryApplyExecuteSoulFracture(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        BattleExecuteSoulFractureParams soulFractureParams,
        GStringNameArray statusEffectIds
    )
    {
        if (targetUnit == null || !targetUnit.is_alive || targetUnit.current_hp <= 0)
        {
            return false;
        }
        BattleExecuteSoulFractureParams resolvedParams = soulFractureParams.HasValue
            ? soulFractureParams
            : BattleExecuteSoulFractureParams.DefaultResisted;
        BattleStatusEffectState statusEntry = BuildSoulFractureStatusEntry(
            targetUnit,
            sourceUnit,
            resolvedParams
        );
        if (statusEntry == null)
        {
            return false;
        }
        targetUnit.SetStatusEffect(statusEntry);
        AddUnique(statusEffectIds, statusEntry.status_id);
        return true;
    }

    private static BattleStatusEffectState BuildSoulFractureStatusEntry(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        BattleExecuteSoulFractureParams soulFractureParams
    )
    {
        StringName statusId = ProgressionDataUtils.to_string_name(soulFractureParams.StatusId);
        if (targetUnit == null || statusId == "")
        {
            return null;
        }
        BattleStatusEffectState statusEntry = BattleStatusEffectState.CreateOrDuplicate(
            targetUnit.GetStatusEffect(statusId)
        );
        int previousPower = Math.Max(statusEntry.power, 0);
        int durationTu = Math.Max(soulFractureParams.DurationTu, 0);
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = sourceUnit != null ? sourceUnit.unit_id : new StringName("");
        statusEntry.@params = new GDictionary();
        statusEntry.stack_behavior = "refresh";
        statusEntry.stack_limit = 1;
        statusEntry.power = Math.Max(previousPower, 1);
        statusEntry.stacks = 1;
        if (durationTu > 0)
        {
            statusEntry.duration = Math.Max(durationTu, statusEntry.duration);
        }
        statusEntry.heal_multiplier_percent = soulFractureParams.HealMultiplierPercent;
        statusEntry.shield_gain_multiplier_percent = soulFractureParams.ShieldGainMultiplierPercent;
        return statusEntry;
    }











    private bool ApplyPhantasmalKillStatus(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        StringName statusId,
        int durationTu,
        bool lockCounterattack,
        bool lockGuard,
        GStringNameArray statusEffectIds
    )
    {
        if (targetUnit == null || IsEmpty(statusId) || durationTu <= 0)
        {
            return false;
        }
        BattleStatusEffectState statusEntry = BuildPhantasmalKillStatusEntry(
            targetUnit,
            sourceUnit,
            statusId,
            durationTu,
            lockCounterattack,
            lockGuard
        );
        if (statusEntry == null)
        {
            return false;
        }
        targetUnit.SetStatusEffect(statusEntry);
        AddUnique(statusEffectIds, statusId);
        return true;
    }

    private static BattleStatusEffectState BuildPhantasmalKillStatusEntry(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        StringName statusId,
        int durationTu,
        bool lockCounterattack,
        bool lockGuard
    )
    {
        if (targetUnit == null || statusId == "" || durationTu <= 0)
        {
            return null;
        }
        BattleStatusEffectState statusEntry = BattleStatusEffectState.CreateOrDuplicate(
            targetUnit.GetStatusEffect(statusId)
        );
        int previousPower = Math.Max(statusEntry.power, 0);
        statusEntry.status_id = statusId;
        statusEntry.source_unit_id = sourceUnit != null ? sourceUnit.unit_id : new StringName("");
        statusEntry.@params = new GDictionary();
        statusEntry.stack_behavior = "refresh";
        statusEntry.stack_limit = 1;
        statusEntry.power = Math.Max(previousPower, 1);
        statusEntry.stacks = 1;
        statusEntry.duration = Math.Max(Math.Max(durationTu, 0), statusEntry.duration);
        statusEntry.lock_counterattack = lockCounterattack;
        statusEntry.lock_guard = lockGuard;
        return statusEntry;
    }

    private DamageOutcomeResult ResolvePhantasmalKillDamageOutcome(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        StringName damageTag,
        DamageResolutionContext damageContext,
        int diceCount,
        int diceSides
    )
    {
        StringName resolvedDamageTag = DamageTagContentRules.ToDamageTagKind(damageTag)
            != DamageTagKind.Unknown
                ? damageTag
                : new StringName("");
        if (resolvedDamageTag == "")
        {
            return BuildInvalidDamageTagOutcomeFromTag(damageTag);
        }
        StringName rollMode =
            (damageContext ?? DamageResolutionContext.Empty()).DamageRollMode;
        DicePoolRollResult damageRoll = RollDicePool(
            Math.Max(diceCount, 0),
            Math.Max(diceSides, 0),
            0,
            "damage_dice",
            rollMode
        );
        int baseDamage = damageRoll.TotalWithBonus;
        double offenseMultiplier = BuildOffenseMultiplier(sourceUnit, targetUnit, (CombatEffectDefinition)null);
        int rolledDamage = Math.Max(RoundToInt(baseDamage * offenseMultiplier), 0);
        GDictionary mitigationTierResult = ResolveMitigationTierResult(targetUnit, resolvedDamageTag);
        StringName mitigationTier = DictStringName(
            mitigationTierResult,
            "tier",
            MitigationTierNormal
        );
        int tierAdjustedDamage = rolledDamage;
        if (mitigationTier == MitigationTierImmune)
        {
            tierAdjustedDamage = 0;
        }
        else if (mitigationTier == MitigationTierHalf)
        {
            tierAdjustedDamage /= 2;
        }
        else if (mitigationTier == MitigationTierDouble)
        {
            tierAdjustedDamage *= 2;
        }

        FixedMitigationResult mitigation = BuildFixedMitigation(
            targetUnit,
            (CombatEffectDefinition)null,
            resolvedDamageTag
        );
        ApplyBlackStarBrandGuardIgnore(mitigation, targetUnit);
        bool lowLuckBlackStarWedgeTriggered = ApplyLowLuckBlackStarWedgeGuardIgnore(
            mitigation,
            sourceUnit
        );
        TrimFixedMitigationSources(mitigation);
        int fixedMitigationTotal = mitigation.Total;
        int resolvedDamage = Math.Max(tierAdjustedDamage - fixedMitigationTotal, MinDamageFloor);
        DamageDiceEventFlags damageDiceEventFlags = BuildDamageDiceEventFlags(
            false,
            damageRoll,
            DicePoolRollResult.Empty
        );
        DamageDiceEventSnapshot diceSnapshot = damageDiceEventFlags.Snapshot;
        DamageEventResult @event = new()
        {
            DamageTag = resolvedDamageTag,
            MitigationTier = AttackEffectResolutionResultReader.ParseMitigationTier(
                mitigationTier
            ),
            MitigationSources =
                AttackEffectResolutionResultReader.ReadMitigationSourcesFromArray(
                    GetArray(mitigationTierResult, "sources")
                ),
            BaseDamage = baseDamage,
            CriticalHit = false,
            AddWeaponDice = false,
            DamageDice = damageRoll.ToDamageDiceRollDetail(),
            BonusDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            WeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraBonusDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            CriticalExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            TraitExtraWeaponDamageDice = DicePoolRollResult.Empty.ToDamageDiceRollDetail(),
            OffenseMultiplier = offenseMultiplier,
            RolledDamage = rolledDamage,
            TierAdjustedDamage = tierAdjustedDamage,
            ResolvedDamage = resolvedDamage,
            BuffReduction = mitigation.BuffReduction,
            StanceReduction = mitigation.StanceReduction,
            PassiveReduction = mitigation.PassiveReduction,
            ContentDr = mitigation.ContentDr,
            GuardBlock = mitigation.GuardBlock,
            GuardIgnoreApplied = mitigation.GuardIgnoreApplied,
            FixedMitigationSourceLabels = mitigation.SourceLabels(),
            LowLuckBlackStarWedgeTriggered = lowLuckBlackStarWedgeTriggered,
            FixedMitigationTotal = fixedMitigationTotal,
            FullyAbsorbedByMitigation =
                resolvedDamage <= 0
                && mitigationTier != MitigationTierImmune
                && tierAdjustedDamage > 0,
            TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
            DamageDiceHighTotalRoll = diceSnapshot.DamageDiceHighTotalRoll,
            DamageDiceHighTotalRollReason = diceSnapshot.DamageDiceHighTotalRollReason,
            SkillDamageDiceIsMax = diceSnapshot.SkillDamageDiceIsMax,
            SkillDamageDiceIsMaxReason = diceSnapshot.SkillDamageDiceIsMaxReason,
            WeaponDamageDiceIsMax = diceSnapshot.WeaponDamageDiceIsMax,
            WeaponDamageDiceIsMaxReason = diceSnapshot.WeaponDamageDiceIsMaxReason,
        };
        return new DamageOutcomeResult(
            @event,
            false,
            "",
            "",
            "",
            resolvedDamageTag,
            resolvedDamage,
            false,
            false,
            100.0,
            0,
            lowLuckBlackStarWedgeTriggered,
            damageDiceEventFlags.Snapshot
        );
    }

    private static DamageOutcomeResult BuildInvalidDamageTagOutcomeFromTag(StringName damageTag)
    {
        StringName configuredTag = ProgressionDataUtils.to_string_name(damageTag);
        StringName reason = configuredTag == "" ? "missing_damage_tag" : "unsupported_damage_tag";
        DamageEventResult @event = new()
        {
            DamageTag = configuredTag,
            MitigationTier = MitigationTierKind.Normal,
            MitigationSources = Array.Empty<MitigationSourceResult>(),
            BaseDamage = 0,
            RolledDamage = 0,
            TierAdjustedDamage = 0,
            ResolvedDamage = 0,
            FixedMitigationSourceLabels = Array.Empty<string>(),
            FixedMitigationTotal = 0,
            FullyAbsorbedByMitigation = false,
        };
        return new DamageOutcomeResult(
            @event,
            true,
            "invalid_damage_tag",
            reason.ToString(),
            "effect.damage_tag",
            configuredTag,
            0,
            false,
            false,
            100.0,
            0,
            false,
            DamageDiceEventSnapshot.Empty
        );
    }



    private static DamageApplicationInput BuildPhantasmalKillFatalDamageInput(
        StringName damageTag,
        int resolvedDamage,
        DeathResolutionContext deathContext
    )
    {
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        DamageEventResult @event = new()
        {
            DamageTag = ProgressionDataUtils.to_string_name(damageTag),
            ResolvedDamage = normalizedDamage,
            MinHpAfterDamage = 0,
            BypassShield = true,
            BypassDeathPrevention = false,
            ShieldAbsorptionPercent = 0.0,
            DeathSource = deathContext.DeathSource,
            DeathSourcePriority = deathContext.DeathSourcePriority,
        };
        return DamageApplicationInput.Create(
            @event,
            normalizedDamage,
            bypassShield: true,
            bypassDeathPrevention: false,
            shieldAbsorptionPercent: 0.0
        );
    }

    private static DeathResolutionContext PhantasmalKillExecuteContext()
    {
        return BattleDeathResolutionRules.PhantasmalKillExecuteContext();
    }

    private static bool IsTargetWithinExecuteThreshold(
        BattleUnitState targetUnit,
        int executeThreshold
    )
    {
        return targetUnit != null
            && Math.Max(targetUnit.current_hp, 0) <= Math.Max(executeThreshold, 0);
    }

    private static int ResolveTargetMaxHp(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        int maxHp = GetAttributeValue(
            targetUnit,
            AttributeService.ToStringName(AttributeIdKind.HpMax)
        );
        return Math.Max(maxHp, Math.Max(targetUnit.current_hp, 0));
    }

    private static GradedSaveExecuteEffectResult DiagnosticGradedSaveExecuteResult(
        string errorCode,
        string message
    )
    {
        return new GradedSaveExecuteEffectResult(
            false,
            Array.Empty<AppliedDamageResult>(),
            new[]
            {
                new ResolutionDiagnostic
                {
                    ErrorCode = string.IsNullOrEmpty(errorCode)
                        ? "graded_save_execute_error"
                        : errorCode,
                    Message = message ?? "",
                },
            }
        );
    }



    private static void ApplyHealing(BattleUnitState targetUnit, int healAmount)
    {
        if (targetUnit == null || healAmount <= 0)
        {
            return;
        }
        int maxHp = Math.Max(GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.HpMax)), 0);
        targetUnit.ApplyHealing(healAmount, maxHp);
    }













    private static bool IsCrownBreakSealStatus(StringName statusId)
    {
        return statusId == StatusCrownBreakBrokenFang
            || statusId == StatusCrownBreakBrokenHand
            || statusId == StatusCrownBreakBlindedEye;
    }

    private static void ClearOtherCrownBreakSeals(
        BattleUnitState targetUnit,
        StringName keptStatusId
    )
    {
        if (targetUnit == null)
        {
            return;
        }
        foreach (
            StringName sealStatusId in new[]
            {
                StatusCrownBreakBrokenFang,
                StatusCrownBreakBrokenHand,
                StatusCrownBreakBlindedEye,
            }
        )
        {
            if (sealStatusId != keptStatusId)
            {
                targetUnit.EraseStatusEffect(sealStatusId);
            }
        }
    }

    private static bool HasStatusEffect(BattleUnitState unitState, StringName statusId)
    {
        return unitState != null && unitState.HasStatusEffect(statusId);
    }

    private static int GetStatusStrength(BattleUnitState unitState, StringName statusId)
    {
        BattleStatusEffectState statusEntry = unitState?.GetStatusEffect(statusId);
        return statusEntry == null ? 0 : Math.Max(statusEntry.power, 1);
    }

    private double GetTargetIncomingDamageMultiplier(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 1.0;
        }
        double multiplier = 1.0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            double statusMultiplier = statusEntry.incoming_damage_multiplier ?? 1.0;
            if (statusMultiplier > multiplier)
            {
                multiplier = statusMultiplier;
            }
        }
        return Math.Max(multiplier, 1.0);
    }

    private double GetSourceOutgoingDamageMultiplier(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return 1.0;
        }
        double multiplier = 1.0;
        foreach (StringName statusId in sourceUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = sourceUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            double statusMultiplier = statusEntry.outgoing_damage_multiplier ?? 1.0;
            if (statusMultiplier > 0.0)
            {
                multiplier *= statusMultiplier;
            }
        }
        return Math.Max(multiplier, 0.0);
    }

    private static double GetLowLuckBloodDebtMultiplier(BattleUnitState targetUnit)
    {
        if (!LowLuckRelicRules.UnitHasFlag(targetUnit, LowLuckRelicRules.ToStringName(LowLuckRelicAttributeKind.BloodDebtShawl)))
        {
            return 1.0;
        }
        if (!IsUnitBelowHpRatio(targetUnit, LowLuckRelicRules.BloodDebtLowHpThresholdRatio))
        {
            return 1.0;
        }
        return LowLuckRelicRules.BloodDebtDamageMultiplier;
    }

    private bool ApplyLowLuckBlackStarWedgeExposed(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null)
        {
            return false;
        }
        ApplyRuntimeStatus(
            sourceUnit,
            LowLuckRelicRules.ToStringName(LowLuckRelicStatusKind.BlackStarWedgeExposed),
            LowLuckRelicRules.BlackStarWedgeExposedDurationTu,
            countsAsDebuffOverride: true,
            countsAsDebuff: true,
            incomingDamageMultiplier:
                LowLuckRelicRules.BlackStarWedgeExposedIncomingDamageMultiplier
        );
        return true;
    }

    private static void ApplyRuntimeStatus(
        BattleUnitState unitState,
        StringName statusId,
        int durationTu,
        GDictionary @params = null,
        StringName sourceUnitId = default,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false,
        double? incomingDamageMultiplier = null,
        double? outgoingDamageMultiplier = null
    )
    {
        if (unitState == null || statusId == "")
        {
            return;
        }
        BattleStatusEffectState statusEntry = BuildRuntimeStatusEffect(
            statusId,
            durationTu,
            @params,
            sourceUnitId,
            countsAsDebuffOverride,
            countsAsDebuff,
            incomingDamageMultiplier,
            outgoingDamageMultiplier
        );
        unitState.SetStatusEffect(statusEntry);
    }

    private static bool IsUnitBelowHpRatio(BattleUnitState unitState, double thresholdRatio)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return false;
        }
        int maxHp = Math.Max(GetAttributeValue(unitState, AttributeService.ToStringName(AttributeIdKind.HpMax)), 0);
        return maxHp > 0 && unitState.current_hp <= maxHp * Math.Clamp(thresholdRatio, 0.0, 1.0);
    }



    private static BattleStatusEffectState BuildRuntimeStatusEffect(
        StringName statusId,
        int durationTu,
        GDictionary @params = null,
        StringName sourceUnitId = default,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false,
        double? incomingDamageMultiplier = null,
        double? outgoingDamageMultiplier = null
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = IsEmpty(sourceUnitId) ? new StringName("") : sourceUnitId,
            power = 1,
            stacks = 1,
            duration = Math.Max(durationTu, -1),
            @params = BattleStatusEffectState.CopyResidualParams(@params),
            counts_as_debuff_override = countsAsDebuffOverride,
            counts_as_debuff = countsAsDebuff,
            incoming_damage_multiplier = incomingDamageMultiplier,
            outgoing_damage_multiplier = outgoingDamageMultiplier,
        };
    }

    private static BattleStatusEffectState BuildStackingSourceStatusEffect(
        StringName statusId,
        StringName sourceUnitId,
        int power,
        int durationTu,
        int stackLimit
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = sourceUnitId,
            stack_behavior = "add",
            stack_limit = stackLimit,
            power = power,
            stacks = power,
            duration = durationTu,
            @params = new GDictionary(),
        };
    }



    private static void ClearComboStackOnMiss(BattleUnitState sourceUnit)
    {
        if (sourceUnit != null && sourceUnit.HasStatusEffect("combo_stack"))
        {
            sourceUnit.EraseStatusEffect("combo_stack");
        }
    }

    private void RecordLastStandMastery(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        StringName sourceType,
        int baseAmount
    )
    {
        if (targetUnit == null || baseAmount <= 0)
        {
            return;
        }
        _last_stand_mastery_records.Add(
            new BattleSkillMasteryGrant
            {
                MemberId = targetUnit.source_member_id,
                SkillId = "warrior_last_stand",
                Amount = baseAmount,
                SourceType = sourceType,
                SourceLabel = "不屈",
                ReasonText = sourceType == "last_stand_triggered" ? "触发免死" : "极限承伤",
                AllowUnlocks = true,
            }
        );
    }

    private bool TriggerLastStand(BattleUnitState targetUnit, BattleUnitState sourceUnit = null)
    {
        BattleStatusEffectState deathWardEntry = targetUnit.GetStatusEffect("death_ward");
        if (deathWardEntry == null)
        {
            return false;
        }
        StringName sourceSkillId = ProgressionDataUtils.to_string_name(
            deathWardEntry.source_skill_id
        );
        int skillLevel = deathWardEntry.source_skill_level ?? 0;
        if (sourceSkillId == null || sourceSkillId == "")
        {
            return false;
        }
        SkillDefinition skillDefinition = GetSkillDefinitionTyped(sourceSkillId);
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
        {
            return false;
        }
        StringName fatalStatusId = ProgressionDataUtils.to_string_name(deathWardEntry.status_id);
        foreach (CombatEffectDefinition effectDefinition in combatProfile.PassiveEffectDefinitions)
        {
            if (
                effectDefinition == null
                || effectDefinition.TriggerConditionKind
                    != CombatEffectTriggerCondition.OnFatalDamage
            )
            {
                continue;
            }
            StringName requiredStatusId = ProgressionDataUtils.to_string_name(
                effectDefinition.TriggerStatusId
            );
            if (requiredStatusId != "" && requiredStatusId != fatalStatusId)
            {
                continue;
            }
            int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
            int maxLevel = effectDefinition.MaxSkillLevel;
            if (skillLevel < minLevel || (maxLevel >= 0 && skillLevel > maxLevel))
            {
                continue;
            }
            ResolveEffects(
                targetUnit,
                targetUnit,
                new[] { effectDefinition },
                DamageResolutionContext
                    .ForSkill(sourceSkillId)
                    .WithSourceSkillLevel(Math.Max(skillLevel, 1))
            );
        }
        bool triggered = targetUnit.current_hp > 0;
        if (triggered)
        {
            RecordLastStandMastery(targetUnit, sourceUnit, "last_stand_triggered", 50);
            targetUnit.EraseStatusEffect("death_ward");
            targetUnit.death_ward_consumed_this_battle = true;
        }
        return triggered;
    }


    private static AppliedDamageResult BuildAppliedDamageResult(
        DamageApplicationInput damageInput,
        int hpDamage,
        int shieldAbsorbed,
        bool shieldBroken
    )
    {
        DamageEventResult result = damageInput.Event;
        result.Damage = hpDamage;
        result.HpDamage = hpDamage;
        result.ShieldAbsorbed = shieldAbsorbed;
        result.ShieldBroken = shieldBroken;
        result.FullyAbsorbedByShield = hpDamage <= 0 && shieldAbsorbed > 0;
        result.ShieldAbsorptionPercent = damageInput.ShieldAbsorptionPercent;
        result.BypassShield = damageInput.BypassShield;
        result.BypassDeathPrevention = damageInput.BypassDeathPrevention;
        result.MinHpAfterDamage = damageInput.MinHpAfterDamage;
        result.LowLuckBlackStarWedgeTriggered = damageInput.LowLuckBlackStarWedgeTriggered;
        return new AppliedDamageResult(
            result,
            hpDamage,
            hpDamage,
            shieldAbsorbed,
            shieldBroken,
            damageInput.LowLuckBlackStarWedgeTriggered,
            damageInput.DamageDiceEvent
        );
    }

    private static AttackEffectResolutionResult BuildEnvironmentalDamageResult(
        AppliedDamageResult damageResult
    )
    {
        return AttackEffectResolutionResultReader.FinalizeTypedResult(
            new AttackEffectResolutionResult
            {
                Applied = damageResult.HasAppliedDamage,
                Damage = damageResult.Damage,
                HpDamage = damageResult.HpDamage,
                ShieldAbsorbed = damageResult.ShieldAbsorbed,
                ShieldBroken = damageResult.ShieldBroken,
                DamageEvents = new[] { damageResult.Event },
                ExecuteStage = -1,
            }
        );
    }

    internal static AttackEffectResolutionResult BuildEmptyResolutionResult(
        StringName skillId = default
    )
    {
        return AttackEffectResolutionResultReader.FinalizeTypedResult(
            new AttackEffectResolutionResult
            {
                Applied = false,
                Damage = 0,
                HpDamage = 0,
                Healing = 0,
                ShieldAbsorbed = 0,
                ShieldBroken = false,
                DamageEvents = Array.Empty<DamageEventResult>(),
                EquipmentDurabilityEvents = Array.Empty<EquipmentDurabilityEventResult>(),
                DispelEvents = Array.Empty<DispelEventResult>(),
                StatusEffectIds = new GStringNameArray(),
                RemovedStatusEffectIds = new GStringNameArray(),
                SourceStatusEffectIds = new GStringNameArray(),
                TerrainEffectIds = new GStringNameArray(),
                SaveResults = Array.Empty<SaveResolutionResult>(),
                HeightDelta = 0,
                Diagnostics = Array.Empty<ResolutionDiagnostic>(),
                ExecuteStage = -1,
                SkillId = skillId,
                AttackCheck = new AttackCheckInput(skillId: skillId),
                TraitTriggerResults = Array.Empty<TraitTriggerEventResult>(),
            }
        );
    }

}
