using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal readonly record struct EquipmentDurabilitySaveResolution(
    SaveResolutionResult Result,
    bool HasSave,
    bool Success
);

internal readonly record struct EquipmentDurabilityDamageEffectResult(
    EquipmentDurabilityEventResult Event,
    bool HasEvent,
    int DurabilityLoss,
    bool Destroyed,
    EquipmentDurabilitySaveResolution SaveResult
)
{
    public static EquipmentDurabilityDamageEffectResult Empty => new(
        new EquipmentDurabilityEventResult(),
        false,
        0,
        false,
        default
    );
}

internal sealed class BattleEquipmentDurabilityResolver
{
    internal EquipmentDurabilityDamageEffectResult ApplyEquipmentDurabilityDamageEffect(
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
        BattleDamageResolver.DamageEffectRuntimeParameters parameters = BattleDamageResolver.DamageEffectRuntimeParameters.FromEffect(
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
        BattleDamageResolver.EquipmentDurabilitySelectionResult selectionResult = SelectEquipmentForDurabilityDamage(
            BuildEquipmentDurabilitySelectionQueryFromEffect(
                targetUnit,
                effectDefinition,
                resolvedContext
            )
        );
        if (!selectionResult.HasSelection)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        EquipmentDurabilityCommitResult commitResult = ApplyEquipmentDurabilityDamageToSelection(
            new EquipmentDurabilityCommitRequest
            {
                SourceUnit = sourceUnit,
                TargetUnit = targetUnit,
                TargetEquipment = selectionResult.SelectedTarget,
                EffectDefinition = effectDefinition,
                DamageContext = resolvedContext,
                TotalDamage = totalDamage,
                TotalShieldAbsorbed = totalShieldAbsorbed,
                SourceKey = effectDefinition.EffectType,
                ActionId = effectDefinition.EffectType,
            }
        );
        return BuildEquipmentDurabilityDamageEffectResult(commitResult);
    }

    internal EquipmentDurabilityCommitResult ApplyEquipmentDurabilityDamageToSelection(
        EquipmentDurabilityCommitRequest request
    )
    {
        if (request == null || request.EffectDefinition == null || request.TargetEquipment == null)
        {
            return EquipmentDurabilityCommitResult.NoOp("invalid_request");
        }
        DamageResolutionContext resolvedContext = request.DamageContext ?? DamageResolutionContext.Empty();
        BattleDamageResolver.DamageEffectRuntimeParameters parameters = BattleDamageResolver.DamageEffectRuntimeParameters.FromEffect(
            request.EffectDefinition
        );
        if (
            parameters.RequireDamageApplied
            && !resolvedContext.AttackSuccess
            && request.TotalDamage <= 0
            && request.TotalShieldAbsorbed <= 0
        )
        {
            return EquipmentDurabilityCommitResult.NoOp("attack_not_successful");
        }
        if (
            !BattleDamageResolver.TryBuildDurabilityCommitSelection(
                request,
                out BattleDamageResolver.EquipmentDurabilitySelection selection,
                out StringName noOpReason
            )
        )
        {
            return EquipmentDurabilityCommitResult.NoOp(noOpReason);
        }
        return ApplyEquipmentDurabilityDamageToSelection(request, selection, resolvedContext);
    }

    internal EquipmentDurabilityCommitResult ApplyEquipmentDurabilityDamageToSelection(
        EquipmentDurabilityDirectCommitRequest request
    )
    {
        if (request == null || request.TargetEquipment == null)
        {
            return EquipmentDurabilityCommitResult.NoOp("invalid_request");
        }
        BattleUnitState targetUnit = request.TargetUnit;
        EquipmentAbilityEquipmentTargetRef targetEquipment = request.TargetEquipment;
        if (targetUnit == null || targetUnit.unit_id != targetEquipment.UnitId)
        {
            return EquipmentDurabilityCommitResult.NoOp("target_unit_missing");
        }
        EquipmentState equipmentView = targetUnit.GetEquipmentView();
        if (equipmentView == null)
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_missing");
        }

        StringName entrySlotId = ProgressionDataUtils.to_string_name(
            targetEquipment.EntrySlotId
        );
        StringName slotId = ProgressionDataUtils.to_string_name(targetEquipment.SlotId);
        if (entrySlotId == "" || slotId == "")
        {
            return EquipmentDurabilityCommitResult.NoOp("invalid_request");
        }

        EquipmentEntryState entry = equipmentView.GetEntry(entrySlotId);
        if (entry == null || entry.IsEmpty())
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_missing");
        }
        if (
            entry.item_id != targetEquipment.ItemId
            || entry.instance_id != targetEquipment.EquipmentInstanceId
            || !BattleDamageResolver.HasStringName(entry.occupied_slot_ids, slotId)
        )
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_changed");
        }
        foreach (StringName occupiedSlotId in targetEquipment.OccupiedSlotIds ?? Array.Empty<StringName>())
        {
            StringName normalizedOccupiedSlotId = ProgressionDataUtils.to_string_name(
                occupiedSlotId
            );
            if (
                normalizedOccupiedSlotId == ""
                || equipmentView.GetEntrySlotForSlot(normalizedOccupiedSlotId) != entrySlotId
            )
            {
                return EquipmentDurabilityCommitResult.NoOp("target_equipment_changed");
            }
        }

        EquipmentInstanceState equipmentInstance = entry.GetEquipmentInstance();
        if (equipmentInstance == null)
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_missing");
        }
        if (
            equipmentInstance.item_id != targetEquipment.ItemId
            || equipmentInstance.instance_id != targetEquipment.EquipmentInstanceId
        )
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_changed");
        }
        int before = Math.Max(equipmentInstance.current_durability, 0);
        if (before <= 0)
        {
            return EquipmentDurabilityCommitResult.NoOp("already_destroyed");
        }

        int rarity = equipmentInstance.rarity;
        int durabilityLoss = Math.Min(Math.Max(request.DurabilityLoss, 0), before);
        int after = before - durabilityLoss;
        if (after <= 0)
        {
            equipmentView.ClearEntrySlot(entrySlotId);
        }
        else
        {
            equipmentInstance.current_durability = after;
        }
        return new EquipmentDurabilityCommitResult
        {
            Resolved = true,
            TargetUnitId = targetUnit.unit_id,
            EntrySlotId = entrySlotId,
            SlotId = slotId,
            ItemId = entry.item_id,
            EquipmentInstanceId = entry.instance_id,
            Rarity = rarity,
            DurabilityBefore = before,
            DurabilityAfter = Math.Max(after, 0),
            DurabilityLoss = durabilityLoss,
            Destroyed = after <= 0,
            HasSave = false,
            SaveResult = new SaveResolutionResult(),
        };
    }

    private EquipmentDurabilityCommitResult ApplyEquipmentDurabilityDamageToSelection(
        EquipmentDurabilityCommitRequest request,
        BattleDamageResolver.EquipmentDurabilitySelection selection,
        DamageResolutionContext resolvedContext
    )
    {
        BattleUnitState targetUnit = request.TargetUnit;
        EquipmentState equipmentView = targetUnit.GetEquipmentView();
        StringName entrySlotId = selection.EntrySlotId;
        EquipmentInstanceState equipmentInstance = selection.EquipmentInstance;
        if (equipmentView == null || !selection.IsValid || equipmentInstance == null)
        {
            return EquipmentDurabilityCommitResult.NoOp("target_equipment_missing");
        }
        int before = Math.Max(equipmentInstance.current_durability, 0);
        if (before <= 0)
        {
            return EquipmentDurabilityCommitResult.NoOp("already_destroyed");
        }
        int rarity = equipmentInstance.rarity;
        EquipmentDurabilitySaveResolution saveResult = ResolveEquipmentDurabilitySave(
            request.SourceUnit,
            targetUnit,
            request.EffectDefinition,
            resolvedContext,
            rarity
        );
        StringName resolvedSlotId = selection.SlotId == "" ? entrySlotId : selection.SlotId;
        if (saveResult.HasSave && saveResult.Success)
        {
            return new EquipmentDurabilityCommitResult
            {
                Resolved = true,
                TargetUnitId = targetUnit.unit_id,
                EntrySlotId = entrySlotId,
                SlotId = resolvedSlotId,
                ItemId = selection.ItemId,
                EquipmentInstanceId = selection.EquipmentInstanceId,
                Rarity = rarity,
                DurabilityBefore = before,
                DurabilityAfter = before,
                DurabilityLoss = 0,
                Destroyed = false,
                HasSave = saveResult.HasSave,
                SaveResult = saveResult.Result,
            };
        }
        int durabilityLoss = Math.Min(Math.Max(request.EffectDefinition.Power, 0), before);
        int after = before - durabilityLoss;
        if (after <= 0)
        {
            equipmentView.ClearEntrySlot(entrySlotId);
        }
        else
        {
            equipmentInstance.current_durability = after;
        }
        return new EquipmentDurabilityCommitResult
        {
            Resolved = true,
            TargetUnitId = targetUnit.unit_id,
            EntrySlotId = entrySlotId,
            SlotId = resolvedSlotId,
            ItemId = selection.ItemId,
            EquipmentInstanceId = selection.EquipmentInstanceId,
            Rarity = rarity,
            DurabilityBefore = before,
            DurabilityAfter = Math.Max(after, 0),
            DurabilityLoss = durabilityLoss,
            Destroyed = after <= 0,
            HasSave = saveResult.HasSave,
            SaveResult = saveResult.Result,
        };
    }

    private static EquipmentDurabilityDamageEffectResult BuildEquipmentDurabilityDamageEffectResult(
        EquipmentDurabilityCommitResult result
    )
    {
        if (result == null || !result.Resolved)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        return new EquipmentDurabilityDamageEffectResult(
            BuildEquipmentDurabilityEventResult(result),
            true,
            result.DurabilityLoss,
            result.Destroyed,
            new EquipmentDurabilitySaveResolution(
                result.SaveResult,
                result.HasSave,
                result.SaveResult.Success
            )
        );
    }

    internal static EquipmentDurabilityEventResult BuildEquipmentDurabilityEventResult(
        EquipmentDurabilityCommitResult result
    ) =>
        new()
        {
            EffectType = BattleDamageResolver.EffectEquipmentDurabilityDamage,
            TargetUnitId = result.TargetUnitId,
            EntrySlotId = result.EntrySlotId,
            SlotId = result.SlotId == "" ? result.EntrySlotId : result.SlotId,
            ItemId = result.ItemId,
            EquipmentInstanceId = result.EquipmentInstanceId,
            Rarity = result.Rarity,
            DurabilityBefore = result.DurabilityBefore,
            DurabilityAfter = result.DurabilityAfter,
            DurabilityLoss = result.DurabilityLoss,
            Destroyed = result.Destroyed,
            SaveResult = result.SaveResult,
        };

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
        SaveResolutionResult saveResult = BattleDamageResolver.SaveResolutionFromBattleSave(baseSaveResult);
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

    private static BattleDamageResolver.EquipmentDurabilitySelectionQuery BuildEquipmentDurabilitySelectionQueryFromEffect(
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        DamageResolutionContext damageContext
    )
    {
        StringName overrideSlot =
            damageContext?.EquipmentSlotOverride ?? new StringName("");
        if (overrideSlot == "" && effectDefinition != null)
        {
            overrideSlot = effectDefinition.GetStringNameParamTyped("equipment_slot_override");
        }
        return new BattleDamageResolver.EquipmentDurabilitySelectionQuery
        {
            TargetUnit = targetUnit,
            TargetSlots = GetEquipmentDurabilityTargetSlots(effectDefinition),
            SlotWeights = GetEquipmentDurabilitySlotWeights(effectDefinition),
            ExplicitSlotOverride = overrideSlot,
            ConsumeRandom = true,
        };
    }

    internal BattleDamageResolver.EquipmentDurabilitySelectionResult SelectEquipmentForDurabilityDamage(
        BattleDamageResolver.EquipmentDurabilitySelectionQuery query
    )
    {
        if (query == null || query.TargetUnit == null)
        {
            return BattleDamageResolver.EquipmentDurabilitySelectionResult.NoTarget("target_unit_missing");
        }
        EquipmentState equipmentView = query.TargetUnit.GetEquipmentView();
        if (equipmentView == null)
        {
            return BattleDamageResolver.EquipmentDurabilitySelectionResult.NoTarget("target_equipment_missing");
        }
        if (query.ExplicitSlotOverride != "")
        {
            StringName overrideEntrySlot = ProgressionDataUtils.to_string_name(
                equipmentView.GetEntrySlotForSlot(query.ExplicitSlotOverride)
            );
            EquipmentAbilityEquipmentTargetRef selectedTarget = BattleDamageResolver.BuildEquipmentDurabilityTargetRef(
                equipmentView,
                query.TargetUnit,
                overrideEntrySlot,
                query.ExplicitSlotOverride
            );
            return selectedTarget != null
                ? BattleDamageResolver.EquipmentDurabilitySelectionResult.Selected(selectedTarget, roll: 0)
                : BattleDamageResolver.EquipmentDurabilitySelectionResult.NoTarget("target_equipment_missing");
        }

        IReadOnlyList<StringName> allowedSlots = query.TargetSlots ?? Array.Empty<StringName>();
        IReadOnlyDictionary<StringName, int> slotWeightMap =
            BuildEquipmentDurabilitySlotWeightIndex(query.SlotWeights);
        var candidatesByEntrySlot = new Dictionary<StringName, BattleDamageResolver.EquipmentDurabilitySelectionCandidate>();
        IReadOnlyList<StringName> selectorSlots =
            allowedSlots.Count > 0 ? allowedSlots : equipmentView.GetEntrySlotIdsTyped();
        foreach (StringName selectorSlotId in selectorSlots)
        {
            StringName entrySlotId =
                allowedSlots.Count > 0
                    ? ProgressionDataUtils.to_string_name(
                        equipmentView.GetEntrySlotForSlot(selectorSlotId)
                    )
                    : ProgressionDataUtils.to_string_name(selectorSlotId);
            EquipmentAbilityEquipmentTargetRef targetRef = BattleDamageResolver.BuildEquipmentDurabilityTargetRef(
                equipmentView,
                query.TargetUnit,
                entrySlotId,
                selectorSlotId
            );
            if (targetRef == null)
            {
                continue;
            }
            int weight =
                slotWeightMap.Count == 0
                    ? 1
                    : BattleDamageResolver.GetEquipmentDurabilityWeightForSlot(slotWeightMap, selectorSlotId);
            if (weight <= 0 && allowedSlots.Count == 0)
            {
                weight = BattleDamageResolver.GetEquipmentDurabilitySlotWeight(
                    slotWeightMap,
                    targetRef.EntrySlotId,
                    targetRef.OccupiedSlotIds,
                    allowedSlots
                );
            }
            if (weight <= 0)
            {
                continue;
            }
            if (
                candidatesByEntrySlot.TryGetValue(
                    targetRef.EntrySlotId,
                    out BattleDamageResolver.EquipmentDurabilitySelectionCandidate existing
                )
                && existing.Weight >= weight
            )
            {
                continue;
            }
            candidatesByEntrySlot[targetRef.EntrySlotId] =
                new BattleDamageResolver.EquipmentDurabilitySelectionCandidate(targetRef, weight);
        }
        var candidates = new List<BattleDamageResolver.EquipmentDurabilitySelectionCandidate>(
            candidatesByEntrySlot.Values
        );
        int totalWeight = 0;
        foreach (BattleDamageResolver.EquipmentDurabilitySelectionCandidate candidate in candidates)
        {
            totalWeight += candidate.Weight;
        }
        if (candidates.Count == 0 || totalWeight <= 0)
        {
            return BattleDamageResolver.EquipmentDurabilitySelectionResult.NoTarget("target_equipment_missing");
        }
        if (!query.ConsumeRandom)
        {
            return BattleDamageResolver.EquipmentDurabilitySelectionResult.CandidatesOnly(candidates, totalWeight);
        }
        int roll = TrueRandomSeedService.RandiRange(1, totalWeight);
        int cursor = 0;
        foreach (BattleDamageResolver.EquipmentDurabilitySelectionCandidate candidate in candidates)
        {
            cursor += candidate.Weight;
            if (roll <= cursor)
            {
                return BattleDamageResolver.EquipmentDurabilitySelectionResult.Selected(
                    candidate.Target,
                    roll,
                    candidates,
                    totalWeight
                );
            }
        }
        return BattleDamageResolver.EquipmentDurabilitySelectionResult.Selected(
            candidates[^1].Target,
            roll,
            candidates,
            totalWeight
        );
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

    private static IReadOnlyList<EquipmentSlotWeightDefinition> GetEquipmentDurabilitySlotWeights(
        CombatEffectDefinition effectDefinition
    )
    {
        IReadOnlyList<EquipmentSlotWeightDefinition> slotWeights =
            effectDefinition?.EquipmentDurabilitySlotWeights;
        if (slotWeights == null || slotWeights.Count == 0)
        {
            return Array.Empty<EquipmentSlotWeightDefinition>();
        }
        var result = new List<EquipmentSlotWeightDefinition>();
        foreach (EquipmentSlotWeightDefinition slotWeight in slotWeights)
        {
            if (slotWeight == null)
            {
                continue;
            }
            StringName normalizedSlotId = ProgressionDataUtils.to_string_name(
                slotWeight.SlotId
            );
            if (normalizedSlotId != "" && slotWeight.Weight > 0)
            {
                result.Add(
                    new EquipmentSlotWeightDefinition
                    {
                        SlotId = normalizedSlotId,
                        Weight = slotWeight.Weight,
                    }
                );
            }
        }
        return result.Count > 0
            ? result
            : Array.Empty<EquipmentSlotWeightDefinition>();
    }

    private static IReadOnlyDictionary<StringName, int> BuildEquipmentDurabilitySlotWeightIndex(
        IReadOnlyList<EquipmentSlotWeightDefinition> slotWeights
    )
    {
        var result = new Dictionary<StringName, int>();
        if (slotWeights == null || slotWeights.Count == 0)
        {
            return result;
        }
        foreach (EquipmentSlotWeightDefinition slotWeight in slotWeights)
        {
            if (slotWeight == null || slotWeight.SlotId == "")
            {
                continue;
            }
            result[slotWeight.SlotId] = slotWeight.Weight;
        }
        return result;
    }
}
