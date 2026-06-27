using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class BattleDamageResolver
{
    private DicePoolRollResult RollDamageDice(
        CombatEffectDefinition effectDefinition,
        bool includeBonus = true,
        string fieldPrefix = "damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDefinition == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDefinition.DiceCount, 0);
        int diceSides = Math.Max(effectDefinition.DiceSides, 0);
        int diceBonus = includeBonus ? effectDefinition.DiceBonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollBonusDamageDice(
        CombatEffectDefinition effectDefinition,
        bool includeBonus = true,
        string fieldPrefix = "bonus_damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDefinition == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDefinition.BonusDamageDiceCount, 0);
        int diceSides = Math.Max(effectDefinition.BonusDamageDiceSides, 0);
        int diceBonus = includeBonus ? effectDefinition.BonusDamageDiceBonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollWeaponDice(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        bool includeBonus = true,
        string fieldPrefix = "weapon_damage_dice",
        StringName rollMode = default
    )
    {
        if (!ShouldAddWeaponDice(effectDefinition))
        {
            return DicePoolRollResult.Empty;
        }
        WeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == null || dice.IsEmpty())
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(dice.dice_count, 0);
        int diceSides = Math.Max(dice.dice_sides, 0);
        int diceBonus = includeBonus ? dice.flat_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private FixedMitigationResult BuildFixedMitigation(
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        StringName damageTag
    )
    {
        FixedMitigationComponent buffReduction = ResolveBuffReductionResult(targetUnit);
        FixedMitigationComponent stanceReduction = ResolveStanceReductionResult(targetUnit, damageTag);
        FixedMitigationComponent passiveReduction = ResolvePassiveReductionResult(targetUnit);
        FixedMitigationComponent contentDr = ResolveContentDrResult(
            targetUnit,
            effectDefinition,
            damageTag
        );
        FixedMitigationComponent guardBlock = ResolveGuardBlockResult(targetUnit, damageTag);
        var result = new FixedMitigationResult
        {
            BuffReduction = buffReduction.Value,
            StanceReduction = stanceReduction.Value,
            PassiveReduction = passiveReduction.Value,
            ContentDr = contentDr.Value,
            GuardBlock = guardBlock.Value,
        };
        result.Sources.AddRange(buffReduction.Sources);
        result.Sources.AddRange(stanceReduction.Sources);
        result.Sources.AddRange(passiveReduction.Sources);
        result.Sources.AddRange(contentDr.Sources);
        result.Sources.AddRange(guardBlock.Sources);
        return result;
    }

    private FixedMitigationComponent ResolveContentDrResult(
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        StringName damageTag
    )
    {
        if (targetUnit == null || !IsPhysicalDamageTag(damageTag))
        {
            return ZeroSourceResult();
        }
        int maxContentDr = 0;
        var sources = new List<MitigationSourceResult>();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (!StatusAppliesToDamageTag(statusEntry, damageTag))
            {
                continue;
            }
            int contentDr = Math.Max(statusEntry.content_dr, 0);
            if (contentDr <= 0)
            {
                continue;
            }
            StringName bypassTag = statusEntry.dr_bypass_tag;
            if (bypassTag != "" && EffectHasBypassTag(effectDefinition, bypassTag))
            {
                continue;
            }
            if (contentDr > maxContentDr)
            {
                maxContentDr = contentDr;
                sources.Clear();
                sources.Add(BuildFixedMitigationSource(statusId, "content_dr", contentDr));
            }
            else if (contentDr == maxContentDr)
            {
                sources.Add(BuildFixedMitigationSource(statusId, "content_dr", contentDr));
            }
        }
        return new FixedMitigationComponent(maxContentDr, sources);
    }

    private static bool EffectHasBypassTag(
        CombatEffectDefinition effectDefinition,
        StringName bypassTag
    )
    {
        return effectDefinition != null
            && bypassTag != ""
            && ProgressionDataUtils.to_string_name(effectDefinition.DrBypassTag) == bypassTag;
    }

    private bool HasBonusCondition(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        if (effectDefinition == null || targetUnit == null)
        {
            return false;
        }
        if (effectDefinition.BonusCondition == BonusConditionTargetLowHp)
        {
            return IsTargetLowHp(effectDefinition, targetUnit);
        }
        if (effectDefinition.BonusCondition == BonusConditionTargetDebuffCount)
        {
            return TargetHasEnoughDebuffs(effectDefinition, targetUnit);
        }
        return false;
    }

    private static bool IsTargetLowHp(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        int maxHp = GetAttributeValue(
            targetUnit,
            AttributeService.ToStringName(AttributeIdKind.HpMax)
        );
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.current_hp, 1);
        }
        int thresholdPercent =
            effectDefinition != null && effectDefinition.HpRatioThresholdPercent > 0
                ? Math.Clamp(effectDefinition.HpRatioThresholdPercent, 0, 100)
                : 50;
        return targetUnit.current_hp * 100 <= maxHp * thresholdPercent;
    }

    private static bool TargetHasEnoughDebuffs(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        if (targetUnit == null)
        {
            return false;
        }
        int threshold = Math.Max(effectDefinition?.DebuffCountThreshold ?? 3, 1);
        int count = 0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            if (BattleStatusSemanticTable.IsHarmfulStatus(statusId))
            {
                count += 1;
                if (count >= threshold)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static double GetDamageRatioMultiplier(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition == null
            ? 1.0
            : Math.Max(effectDefinition.DamageRatioPercent / 100.0, 0.0);
    }

    private static double GetPreResistanceDamageMultiplier(
        CombatEffectDefinition effectDefinition
    )
    {
        return effectDefinition == null
            ? 1.0
            : Math.Max(effectDefinition.PreResistanceDamageMultiplier, 0.0);
    }

    private static bool ShouldAddWeaponDice(CombatEffectDefinition effectDefinition)
    {
        return DamageEffectRuntimeParameters.FromEffect(effectDefinition).AddWeaponDice;
    }

    private DamageOutcomeResult BuildInvalidDamageTagOutcome(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        StringName sourceLabel = "effect.damage_tag";
        StringName configuredTag;
        if (ShouldUseWeaponPhysicalDamageTag(effectDefinition))
        {
            sourceLabel = "weapon_physical_damage_tag";
            configuredTag = ProgressionDataUtils.to_string_name(
                sourceUnit != null ? sourceUnit.weapon_physical_damage_tag : new StringName("")
            );
        }
        else
        {
            configuredTag = ProgressionDataUtils.to_string_name(
                effectDefinition != null ? effectDefinition.DamageTag : new StringName("")
            );
        }
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
            sourceLabel.ToString(),
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

    private static GDictionary BuildInvalidDamageTagDiagnostic(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageOutcomeResult damageOutcome
    )
    {
        return new GDictionary
        {
            ["error_code"] = "invalid_damage_tag",
            ["reason"] = damageOutcome.Reason,
            ["damage_tag_source"] = damageOutcome.DamageTagSource,
            ["damage_tag"] = damageOutcome.DamageTag,
            ["effect_type"] = ProgressionDataUtils
                .to_string_name(
                    effectDefinition != null
                        ? effectDefinition.EffectType
                        : new StringName("")
                )
                .ToString(),
            ["source_unit_id"] = sourceUnit != null ? sourceUnit.unit_id.ToString() : "",
            ["target_unit_id"] = targetUnit != null ? targetUnit.unit_id.ToString() : "",
        };
    }

    private DispelEventResult ApplyDispelMagicEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (targetUnit == null || effectDefinition == null)
        {
            return new DispelEventResult { RemovedStatusIds = new GStringNameArray() };
        }
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDefinition
        );
        bool sameFaction = sourceUnit != null && sourceUnit.faction_id == targetUnit.faction_id;
        bool removeHarmful =
            parameters.RemoveHarmful || (sameFaction && parameters.RemoveHarmfulFromAllies);
        bool removeBeneficial =
            parameters.RemoveBeneficial
            || (!sameFaction && parameters.RemoveBeneficialFromEnemies);
        int maxRemoved = Math.Max(
            effectDefinition.MaxStatusRemoved > 0
                ? effectDefinition.MaxStatusRemoved
                : Math.Max(effectDefinition.Power, 1),
            1
        );
        var candidates = new List<StringName>();
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            if (
                removeHarmful
                && BattleStatusSemanticTable.IsDispellableHarmfulStatusEntry(statusEntry)
            )
            {
                candidates.Add(statusId);
            }
            else if (
                removeBeneficial
                && BattleStatusSemanticTable.IsDispellableBeneficialStatusEntry(statusEntry)
            )
            {
                candidates.Add(statusId);
            }
        }
        candidates.Sort(
            (left, right) =>
            {
                int priorityCompare = BattleStatusSemanticTable
                    .GetDispelPriority(right)
                    .CompareTo(BattleStatusSemanticTable.GetDispelPriority(left));
                return priorityCompare != 0
                    ? priorityCompare
                    : left.ToString().CompareTo(right.ToString());
            }
        );
        var removedStatusIds = new GStringNameArray();
        foreach (StringName statusId in candidates)
        {
            if (removedStatusIds.Count >= maxRemoved)
            {
                break;
            }
            targetUnit.EraseStatusEffect(statusId);
            removedStatusIds.Add(statusId);
            BattleTemporalStatusService.HandleTemporalStatusRemoved(
                targetUnit,
                statusId,
                TemporalStatusReleaseKind.Dispel,
                sourceUnit != null ? sourceUnit.unit_id : new StringName("")
            );
        }
        if (removedStatusIds.Count == 0)
        {
            return new DispelEventResult { RemovedStatusIds = new GStringNameArray() };
        }
        return new DispelEventResult
        {
            EffectType = BattleTypedNames.EffectDispelMagic,
            TargetUnitId = targetUnit.unit_id,
            Mode = sameFaction ? "ally_harmful" : "enemy_beneficial",
            MaxStatusRemoved = maxRemoved,
            RemovedStatusIds = removedStatusIds,
        };
    }

    private EquipmentDurabilityDamageEffectResult ApplyEquipmentDurabilityDamageEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        int totalDamage,
        int totalShieldAbsorbed
    )
    {
        if (targetUnit == null || effectDefinition == null)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        DamageResolutionContext resolvedContext = damageContext ?? DamageResolutionContext.Empty();
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDefinition
        );
        if (
            parameters.RequireDamageApplied
            && !resolvedContext.AttackSuccess
            && totalDamage <= 0
            && totalShieldAbsorbed <= 0
        )
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        EquipmentDurabilitySelection selection = SelectEquipmentForDurabilityDamage(
            targetUnit,
            effectDefinition,
            resolvedContext
        );
        if (!selection.IsValid)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        EquipmentState equipmentView = targetUnit.GetEquipmentView();
        StringName entrySlotId = selection.EntrySlotId;
        EquipmentInstanceState equipmentInstance = selection.EquipmentInstance;
        if (equipmentView == null || entrySlotId == "" || equipmentInstance == null)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        int before = Math.Max(equipmentInstance.current_durability, 0);
        if (before <= 0)
        {
            equipmentView.ClearEntrySlot(entrySlotId);
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        int rarity = equipmentInstance.rarity;
        EquipmentDurabilitySaveResolution saveResult = ResolveEquipmentDurabilitySave(
            sourceUnit,
            targetUnit,
            effectDefinition,
            resolvedContext,
            rarity
        );
        EquipmentDurabilityEventResult @event = new()
        {
            EffectType = EffectEquipmentDurabilityDamage,
            TargetUnitId = targetUnit.unit_id,
            EntrySlotId = entrySlotId,
            SlotId = selection.SlotId == "" ? entrySlotId : selection.SlotId,
            ItemId = equipmentInstance.item_id,
            EquipmentInstanceId = equipmentInstance.instance_id,
            Rarity = rarity,
            DurabilityBefore = before,
            DurabilityAfter = before,
            DurabilityLoss = 0,
            Destroyed = false,
            SaveResult = saveResult.Result,
        };
        if (saveResult.HasSave && saveResult.Success)
        {
            return new EquipmentDurabilityDamageEffectResult(@event, true, 0, false, saveResult);
        }
        int durabilityLoss = Math.Min(Math.Max(effectDefinition.Power, 0), before);
        int after = before - durabilityLoss;
        @event.DurabilityLoss = durabilityLoss;
        @event.DurabilityAfter = Math.Max(after, 0);
        if (after <= 0)
        {
            equipmentView.ClearEntrySlot(entrySlotId);
            @event.Destroyed = true;
        }
        else
        {
            equipmentInstance.current_durability = after;
        }
        return new EquipmentDurabilityDamageEffectResult(
            @event,
            true,
            durabilityLoss,
            after <= 0,
            saveResult
        );
    }

    private static EquipmentDurabilitySaveResolution ResolveEquipmentDurabilitySave(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext,
        int rarity
    )
    {
        BattleSaveResult baseSaveResult = BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            targetUnit,
            effectDefinition,
            (damageContext ?? DamageResolutionContext.Empty()).ToBattleSaveContext()
        );
        SaveResolutionResult saveResult = SaveResolutionFromBattleSave(baseSaveResult);
        int rarityBonus = EquipmentDurabilityRules.GetDisjunctionSaveBonusForRarity(rarity);
        saveResult.EquipmentRarityBonus = rarityBonus;
        if (!baseSaveResult.HasSave)
        {
            return new EquipmentDurabilitySaveResolution(saveResult, false, false);
        }
        saveResult.StatusSaveBonus = baseSaveResult.Bonus;
        saveResult.Bonus = baseSaveResult.Bonus + rarityBonus;
        if (baseSaveResult.Immune)
        {
            saveResult.Success = true;
            return new EquipmentDurabilitySaveResolution(saveResult, true, true);
        }
        int naturalRoll = baseSaveResult.NaturalRoll;
        int rollTotal = baseSaveResult.RollTotal + rarityBonus;
        saveResult.RollTotal = rollTotal;
        saveResult.Total = rollTotal;
        bool success = rollTotal >= baseSaveResult.Dc;
        if (naturalRoll <= 1)
        {
            success = false;
        }
        else if (naturalRoll >= 20)
        {
            success = true;
        }
        saveResult.Success = success;
        return new EquipmentDurabilitySaveResolution(saveResult, true, success);
    }

    private EquipmentDurabilitySelection SelectEquipmentForDurabilityDamage(
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext
    )
    {
        if (targetUnit == null)
        {
            return EquipmentDurabilitySelection.Empty;
        }
        EquipmentState equipmentView = targetUnit.GetEquipmentView();
        if (equipmentView == null)
        {
            return EquipmentDurabilitySelection.Empty;
        }
        StringName overrideSlot =
            damageContext?.EquipmentSlotOverride ?? new StringName("");
        if (overrideSlot == "" && effectDefinition != null)
        {
            overrideSlot = effectDefinition.GetStringNameParamTyped("equipment_slot_override");
        }
        if (overrideSlot != "")
        {
            StringName overrideEntrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(overrideSlot)
            );
            return BuildEquipmentDurabilitySelection(
                equipmentView,
                overrideEntrySlot,
                overrideSlot
            );
        }

        IReadOnlyList<StringName> allowedSlots = GetEquipmentDurabilityTargetSlots(
            effectDefinition
        );
        IReadOnlyDictionary<StringName, int> slotWeightMap =
            effectDefinition?.GetStringNameIntMapParamTyped("slot_weight_map")
            ?? (IReadOnlyDictionary<StringName, int>)new Dictionary<StringName, int>();
        var candidates = new List<EquipmentDurabilitySelectionCandidate>();
        int totalWeight = 0;
        foreach (StringName entrySlotId in equipmentView.GetEntrySlotIdsTyped())
        {
            EquipmentDurabilitySelection selection = BuildEquipmentDurabilitySelection(
                equipmentView,
                entrySlotId,
                entrySlotId
            );
            if (!selection.IsValid)
            {
                continue;
            }
            if (
                !IsEquipmentDurabilityEntryAllowed(
                    entrySlotId,
                    selection.OccupiedSlotIds,
                    allowedSlots
                )
            )
            {
                continue;
            }
            int weight = GetEquipmentDurabilitySlotWeight(
                slotWeightMap,
                entrySlotId,
                selection.OccupiedSlotIds
            );
            if (weight <= 0)
            {
                continue;
            }
            totalWeight += weight;
            candidates.Add(new EquipmentDurabilitySelectionCandidate(selection, weight));
        }
        if (candidates.Count == 0 || totalWeight <= 0)
        {
            return EquipmentDurabilitySelection.Empty;
        }
        int roll = TrueRandomSeedService.RandiRange(1, totalWeight);
        int cursor = 0;
        foreach (EquipmentDurabilitySelectionCandidate candidate in candidates)
        {
            cursor += candidate.Weight;
            if (roll <= cursor)
            {
                return candidate.Selection;
            }
        }
        return candidates[^1].Selection;
    }

    private static IReadOnlyList<StringName> GetEquipmentDurabilityTargetSlots(
        CombatEffectDefinition effectDefinition
    )
    {
        var result = new List<StringName>();
        if (effectDefinition == null)
        {
            return result;
        }
        foreach (StringName slotId in effectDefinition.GetStringNameListParamTyped("target_slots"))
        {
            if (EquipmentRules.IsValidSlot(slotId) && !result.Contains(slotId))
            {
                result.Add(slotId);
            }
        }
        return result;
    }

    private readonly record struct EquipmentDurabilitySelectionCandidate(
        EquipmentDurabilitySelection Selection,
        int Weight
    );

    private ExecuteEffectResult ResolveExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext context,
        GStringNameArray statusEffectIds,
        List<SaveResolutionResult> saveResults
    )
    {
        DamageResolutionContext resolutionContext = context ?? DamageResolutionContext.Empty();
        BattleExecutionRuleParams executionParams = BattleExecutionRuleParams.FromEffect(
            effectDefinition,
            resolutionContext.SkillId
        );
        BattleExecutePlan executePlan = BattleExecutionRules.BuildExecutePlan(
            sourceUnit,
            targetUnit,
            executionParams
        );
        if (!executePlan.CanExecute)
        {
            return ExecuteEffectResult.Empty;
        }
        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            targetUnit,
            effectDefinition,
            resolutionContext.ToBattleSaveContext()
        );
        if (saveResult.HasSave)
        {
            saveResults.Add(SaveResolutionFromBattleSave(saveResult));
        }
        if (saveResult.Success)
        {
            return new ExecuteEffectResult(
                TryApplyExecuteSoulFracture(
                    targetUnit,
                    sourceUnit,
                    executePlan.SoulFractureParams,
                    statusEffectIds
                ),
                0,
                "resisted",
                Array.Empty<AppliedDamageResult>()
            );
        }
        int fatalDamage = Math.Max(executePlan.FatalDamage, 0);
        DamageApplicationInput fatalDamageInput = BuildFatalExecuteDamageInput(
            effectDefinition,
            fatalDamage
        );
        AppliedDamageResult fatalResult = ApplyDamageToTargetResult(
            targetUnit,
            fatalDamageInput,
            sourceUnit,
            resolutionContext
        );
        TryApplyExecuteSoulFracture(
            targetUnit,
            sourceUnit,
            executePlan.SoulFractureParams,
            statusEffectIds
        );
        return new ExecuteEffectResult(
            true,
            2,
            "failed_save_fatal",
            new[] { fatalResult }
        );
    }

    private static DamageApplicationInput BuildFatalExecuteDamageInput(
        CombatEffectDefinition effectDefinition,
        int resolvedDamage
    )
    {
        return BuildFatalExecuteDamageInput(
            effectDefinition,
            resolvedDamage,
            BattleDeathResolutionRules.PowerWordKillExecuteContext()
        );
    }

    private static DamageApplicationInput BuildFatalExecuteDamageInput(
        CombatEffectDefinition effectDefinition,
        int resolvedDamage,
        DeathResolutionContext deathContext
    )
    {
        return BuildPhantasmalKillFatalDamageInput(
            effectDefinition?.DamageTag ?? new StringName(""),
            resolvedDamage,
            deathContext
        );
    }

    private GradedSaveExecuteEffectResult ResolveGradedSaveExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext context,
        GStringNameArray statusEffectIds,
        List<SaveResolutionResult> saveResults
    )
    {
        DamageResolutionContext resolutionContext = context ?? DamageResolutionContext.Empty();
        if (
            !BattleGradedSaveExecutionRules.TryReadPhantasmalKillProfile(
                effectDefinition,
                out BattleGradedSaveExecutionProfile profile,
                out string profileError
            )
        )
        {
            return DiagnosticGradedSaveExecuteResult(
                "invalid_graded_save_execute_profile",
                profileError
            );
        }

        BattleSaveResult saveResult = BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            targetUnit,
            effectDefinition,
            resolutionContext.ToBattleSaveContext()
        );
        if (saveResult.HasSave)
        {
            saveResults.Add(SaveResolutionFromBattleSave(saveResult));
        }
        if (saveResult.Immune)
        {
            return GradedSaveExecuteEffectResult.Empty;
        }

        GradedSaveExecutionGrade grade = BattleGradedSaveExecutionRules.ResolveGrade(saveResult);
        if (grade == GradedSaveExecutionGrade.CriticalSuccess)
        {
            return GradedSaveExecuteEffectResult.Empty;
        }
        if (grade == GradedSaveExecutionGrade.Success)
        {
            bool applied = ApplyPhantasmalKillStatus(
                targetUnit,
                sourceUnit,
                PhantasmalKillAftershockStatus,
                profile.SuccessAftershockDurationTu,
                lockCounterattack: true,
                lockGuard: true,
                statusEffectIds
            );
            return new GradedSaveExecuteEffectResult(
                applied,
                Array.Empty<AppliedDamageResult>(),
                Array.Empty<ResolutionDiagnostic>()
            );
        }

        int targetMaxHp = ResolveTargetMaxHp(targetUnit);
        if (grade == GradedSaveExecutionGrade.CriticalFailure)
        {
            int executeThreshold =
                BattleGradedSaveExecutionRules.ResolveCriticalFailureExecuteThreshold(
                    profile,
                    targetMaxHp
                );
            if (IsTargetWithinExecuteThreshold(targetUnit, executeThreshold))
            {
                return ApplyPhantasmalKillExecuteDamage(
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    resolutionContext
                );
            }
            GradedSaveExecuteEffectResult damageResult =
                ApplyPhantasmalKillNonExecuteDamage(
                    sourceUnit,
                    targetUnit,
                    effectDefinition,
                    resolutionContext,
                    profile.CriticalFailureDamageDiceCount,
                    profile.CriticalFailureDamageDiceSides
                );
            bool applied = damageResult.Applied;
            applied |= ApplyPhantasmalKillStatus(
                targetUnit,
                sourceUnit,
                PhantasmalKillFrightenedStatus,
                profile.CriticalFailureFrightenedDurationTu,
                lockCounterattack: false,
                lockGuard: false,
                statusEffectIds
            );
            if (
                ApplyPhantasmalKillStatus(
                    targetUnit,
                    sourceUnit,
                    PhantasmalKillStunnedStatus,
                    profile.CriticalFailureStunnedDurationTu,
                    lockCounterattack: true,
                    lockGuard: true,
                    statusEffectIds
                )
            )
            {
                targetUnit.SetCurrentAp(0);
                targetUnit.SetCurrentMovePoints(0);
                applied = true;
            }
            return damageResult with { Applied = applied };
        }

        int failureExecuteThreshold =
            BattleGradedSaveExecutionRules.ResolveFailureExecuteThreshold(
                profile,
                targetMaxHp
            );
        if (IsTargetWithinExecuteThreshold(targetUnit, failureExecuteThreshold))
        {
            return ApplyPhantasmalKillExecuteDamage(
                sourceUnit,
                targetUnit,
                effectDefinition,
                resolutionContext
            );
        }
        GradedSaveExecuteEffectResult failureDamageResult =
            ApplyPhantasmalKillNonExecuteDamage(
                sourceUnit,
                targetUnit,
                effectDefinition,
                resolutionContext,
                profile.FailureDamageDiceCount,
                profile.FailureDamageDiceSides
            );
        bool failureApplied = failureDamageResult.Applied;
        failureApplied |= ApplyPhantasmalKillStatus(
            targetUnit,
            sourceUnit,
            PhantasmalKillFrightenedStatus,
            profile.FailureFrightenedDurationTu,
            lockCounterattack: false,
            lockGuard: false,
            statusEffectIds
        );
        failureApplied |= ApplyPhantasmalKillStatus(
            targetUnit,
            sourceUnit,
            PhantasmalKillReactionLockStatus,
            profile.FailureReactionLockDurationTu,
            lockCounterattack: true,
            lockGuard: true,
            statusEffectIds
        );
        return failureDamageResult with { Applied = failureApplied };
    }

    private GradedSaveExecuteEffectResult ApplyPhantasmalKillExecuteDamage(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext resolutionContext
    )
    {
        int fatalDamage = Math.Max(targetUnit?.current_hp ?? 0, 0);
        DamageApplicationInput fatalDamageInput = BuildFatalExecuteDamageInput(
            effectDefinition,
            fatalDamage,
            PhantasmalKillExecuteContext()
        );
        AppliedDamageResult fatalResult = ApplyDamageToTargetResult(
            targetUnit,
            fatalDamageInput,
            sourceUnit,
            resolutionContext
        );
        return new GradedSaveExecuteEffectResult(
            true,
            new[] { fatalResult },
            Array.Empty<ResolutionDiagnostic>()
        );
    }

    private GradedSaveExecuteEffectResult ApplyPhantasmalKillNonExecuteDamage(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition sourceEffectDefinition,
        DamageResolutionContext resolutionContext,
        int diceCount,
        int diceSides
    )
    {
        DamageOutcomeResult damageOutcome = ResolvePhantasmalKillDamageOutcome(
            sourceUnit,
            targetUnit,
            ResolvePhantasmalKillDamageTag(sourceEffectDefinition),
            resolutionContext,
            diceCount,
            diceSides
        );
        if (damageOutcome.InvalidDamageTag)
        {
            return DiagnosticGradedSaveExecuteResult(
                damageOutcome.ErrorCode,
                damageOutcome.Reason
            );
        }
        AppliedDamageResult damageResult = ApplyDamageToTargetResult(
            targetUnit,
            damageOutcome,
            sourceUnit,
            resolutionContext
        );
        return new GradedSaveExecuteEffectResult(
            true,
            new[] { damageResult },
            Array.Empty<ResolutionDiagnostic>()
        );
    }

    private static StringName ResolvePhantasmalKillDamageTag(
        CombatEffectDefinition effectDefinition
    )
    {
        StringName damageTag = ProgressionDataUtils.to_string_name(
            effectDefinition?.DamageTag ?? new StringName("")
        );
        return damageTag == "" ? PhantasmalKillDamageTag : damageTag;
    }

    private int ResolveHealAmount(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        int healAmount = Math.Max(effectDefinition?.Power ?? 0, 0);
        DicePoolRollResult healDiceRoll = RollEffectDice(sourceUnit, effectDefinition);
        if (healDiceRoll.HasDice)
        {
            healAmount += healDiceRoll.TotalWithBonus;
        }
        return Math.Max(healAmount, 1);
    }

    private void ApplyStaminaRestore(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (targetUnit == null || effectDefinition == null)
        {
            return;
        }
        int staminaAmount = Math.Max(effectDefinition.Power, 0);
        DicePoolRollResult staminaDiceRoll = RollEffectDice(sourceUnit, effectDefinition);
        if (staminaDiceRoll.HasDice)
        {
            staminaAmount += staminaDiceRoll.TotalWithBonus;
        }
        if (staminaAmount <= 0)
        {
            return;
        }
        int maxStamina = Math.Max(
            GetAttributeValue(
                targetUnit,
                AttributeService.ToStringName(AttributeIdKind.StaminaMax)
            ),
            0
        );
        targetUnit.SetCurrentStamina(Math.Min(targetUnit.current_stamina + staminaAmount, maxStamina));
    }

    private DicePoolRollResult RollEffectDice(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDefinition.DiceCount, 0);
        int diceSides = ResolveEffectDiceSides(sourceUnit, effectDefinition);
        int diceBonus = effectDefinition.DiceBonus;
        return RollDicePoolValues(diceCount, diceSides, diceBonus);
    }

    private int ResolveEffectDiceSides(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null)
        {
            return 0;
        }
        if (effectDefinition.DiceSidesBase > 0)
        {
            return ResolveAttributeScaledDiceSides(sourceUnit, effectDefinition);
        }
        return Math.Max(effectDefinition.DiceSides, 0);
    }

    private int ResolveAttributeScaledDiceSides(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        int conMod = GetUnitBaseAttributeModifier(
            sourceUnit,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)
        );
        int willMod = GetUnitBaseAttributeModifier(
            sourceUnit,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower)
        );
        int baseSides = Math.Max(effectDefinition?.DiceSidesBase ?? 0, 0);
        int conModSides = Math.Max(effectDefinition?.DiceSidesPerConstitutionMod ?? 0, 0);
        int willModSides = Math.Max(effectDefinition?.DiceSidesPerWillpowerMod ?? 0, 0);
        long diceSidesRaw =
            (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
        return (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
    }

    private int ResolveHealFatalAmount(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext context
    )
    {
        if (effectDefinition == null || targetUnit == null)
        {
            return 0;
        }
        int skillLevel = Math.Max(context?.SourceSkillLevel ?? 0, 0);
        if (skillLevel <= 0 && sourceUnit != null && context != null && context.SkillId != "")
        {
            skillLevel = sourceUnit.GetKnownSkillLevelTyped(context.SkillId, fallback: 1);
        }
        skillLevel = Math.Max(skillLevel, 1);
        int conMod = GetUnitBaseAttributeModifier(
            targetUnit,
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)
        );
        int healAmount =
            effectDefinition.BaseHeal + effectDefinition.HealPerLevel * (skillLevel - 1);
        int conLevelBonus =
            effectDefinition.ConModBase
            + ((skillLevel - 1) / 2) * effectDefinition.ConModPer2Levels;
        healAmount += conMod * conLevelBonus;
        return Math.Max(healAmount, 1);
    }

    private bool ApplyStatusEffect(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        StringName statusIdOverride = default
    )
    {
        if (targetUnit == null || effectDefinition == null)
        {
            return false;
        }
        StringName resolvedStatusId = !IsEmpty(statusIdOverride)
            ? statusIdOverride
            : ProgressionDataUtils.to_string_name(effectDefinition.StatusId);
        if (resolvedStatusId == "")
        {
            return false;
        }
        if (IsCrownBreakSealStatus(resolvedStatusId))
        {
            ClearOtherCrownBreakSeals(targetUnit, resolvedStatusId);
        }
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            effectDefinition,
            sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            targetUnit.GetStatusEffect(resolvedStatusId),
            resolvedStatusId
        );
        if (statusEntry == null)
        {
            return false;
        }
        targetUnit.SetStatusEffect(statusEntry);
        return true;
    }

    private void GrantStatusOnHitToSource(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (sourceUnit == null || effectDefinition == null)
        {
            return;
        }
        StringName grantStatusId = effectDefinition.GetStringNameParamTyped(
            "grant_status_id",
            ""
        );
        if (grantStatusId == "")
        {
            return;
        }
        int grantPower = Math.Max(
            effectDefinition.GetIntParamTyped("grant_status_power", 1),
            1
        );
        int grantDuration = Math.Max(
            effectDefinition.GetIntParamTyped("grant_status_duration_tu", 180),
            0
        );
        int stackLimit = Math.Max(
            effectDefinition.GetIntParamTyped("grant_status_stack_limit", 20),
            1
        );
        BattleStatusEffectState existingEntry = sourceUnit.GetStatusEffect(grantStatusId);
        if (existingEntry != null)
        {
            int newStacks = Math.Min(existingEntry.stacks + grantPower, stackLimit);
            existingEntry.stacks = newStacks;
            existingEntry.duration = Math.Max(existingEntry.duration, grantDuration);
            existingEntry.power = newStacks;
            sourceUnit.SetStatusEffect(existingEntry);
            return;
        }
        BattleStatusEffectState statusEntry = BuildStackingSourceStatusEffect(
            grantStatusId,
            sourceUnit.unit_id,
            grantPower,
            grantDuration,
            stackLimit
        );
        sourceUnit.SetStatusEffect(statusEntry);
    }

    private DicePoolRollResult RollConsumedStackDice(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        StringName rollMode = default
    )
    {
        if (sourceUnit == null || effectDefinition == null)
        {
            return DicePoolRollResult.Empty;
        }
        StringName consumedId = ProgressionDataUtils.to_string_name(
            effectDefinition.ConsumedStatusId
        );
        int dicePerStack = Math.Max(effectDefinition.DicePerConsumedStack, 0);
        int diceSides = Math.Max(effectDefinition.DiceSidesPerStack, 0);
        if (
            consumedId == ""
            || dicePerStack <= 0
            || diceSides <= 0
            || !sourceUnit.HasStatusEffect(consumedId)
        )
        {
            return DicePoolRollResult.Empty;
        }
        BattleStatusEffectState statusEntry = sourceUnit.GetStatusEffect(consumedId);
        int stackCount = Math.Max(statusEntry?.stacks ?? 0, 0);
        if (stackCount <= 0)
        {
            return DicePoolRollResult.Empty;
        }
        sourceUnit.EraseStatusEffect(consumedId);
        return RollDicePool(
            dicePerStack * stackCount,
            diceSides,
            0,
            "consumed_stack_damage_dice",
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }
}
