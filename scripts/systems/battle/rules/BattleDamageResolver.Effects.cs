using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：驱散/装备耐久/处决/治疗/状态等效果应用与结果构建。按阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private DispelEventResult ApplyDispelMagicEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return new DispelEventResult { RemovedStatusIds = new GStringNameArray() };
        }
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDef
        );
        bool sameFaction = sourceUnit != null && sourceUnit.faction_id == targetUnit.faction_id;
        bool removeHarmful =
            parameters.RemoveHarmful || (sameFaction && parameters.RemoveHarmfulFromAllies);
        bool removeBeneficial =
            parameters.RemoveBeneficial
            || (!sameFaction && parameters.RemoveBeneficialFromEnemies);
        int maxRemoved = Math.Max(
            effectDef.max_status_removed > 0 ? effectDef.max_status_removed : Math.Max(effectDef.power, 1),
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
        CombatEffectDef effectDef,
        DamageResolutionContext damageContext,
        int totalDamage,
        int totalShieldAbsorbed
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        DamageEffectRuntimeParameters parameters = DamageEffectRuntimeParameters.FromEffect(
            effectDef
        );
        bool attackSuccess = damageContext.AttackSuccess;
        if (
            parameters.RequireDamageApplied
            && !attackSuccess
            && totalDamage <= 0
            && totalShieldAbsorbed <= 0
        )
        {
            return EquipmentDurabilityDamageEffectResult.Empty;
        }
        EquipmentDurabilitySelection selection = SelectEquipmentForDurabilityDamage(
            targetUnit,
            effectDef,
            damageContext
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
            effectDef,
            damageContext,
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
        int durabilityLoss = Math.Min(Math.Max(effectDef.power, 0), before);
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
        CombatEffectDef effectDef,
        DamageResolutionContext damageContext,
        int rarity
    )
    {
        BattleSaveResult baseSaveResult = BattleSaveResolver.ResolveSaveResult(
            sourceUnit,
            targetUnit,
            effectDef,
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
            success = false;
        else if (naturalRoll >= 20)
            success = true;
        saveResult.Success = success;
        return new EquipmentDurabilitySaveResolution(saveResult, true, success);
    }

    private EquipmentDurabilitySelection SelectEquipmentForDurabilityDamage(
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
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
        if (overrideSlot == "" && effectDef != null)
        {
            overrideSlot = effectDef.GetStringNameParamTyped("equipment_slot_override");
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

        IReadOnlyList<StringName> allowedSlots = GetEquipmentDurabilityTargetSlots(effectDef);
        IReadOnlyDictionary<StringName, int> slotWeightMap =
            effectDef?.GetStringNameIntMapParamTyped("slot_weight_map")
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

    private static IReadOnlyList<StringName> GetEquipmentDurabilityTargetSlots(
        CombatEffectDef effectDef
    )
    {
        var result = new List<StringName>();
        if (effectDef == null)
        {
            return result;
        }
        foreach (StringName slotId in effectDef.GetStringNameListParamTyped("target_slots"))
        {
            if (EquipmentRules.IsValidSlot(slotId) && !result.Contains(slotId))
            {
                result.Add(slotId);
            }
        }
        return result;
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

    private readonly record struct EquipmentDurabilitySelectionCandidate(
        EquipmentDurabilitySelection Selection,
        int Weight
    );

    private ExecuteEffectResult ResolveExecuteEffect(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef,
        DamageResolutionContext context,
        GStringNameArray statusEffectIds,
        List<SaveResolutionResult> saveResults
    )
    {
        DamageResolutionContext resolutionContext = context ?? DamageResolutionContext.Empty();
        BattleExecutionRuleParams executionParams = BattleExecutionRuleParams.FromEffect(
            effectDef,
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
            effectDef,
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
            effectDef,
            fatalDamage
        );
        AppliedDamageResult fatalResult = ApplyDamageToTargetResult(
            targetUnit,
            fatalDamageInput,
            sourceUnit
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
        CombatEffectDef tempEffectDef = BuildSoulFractureStatusEffect(soulFractureParams);
        if (!ApplyStatusEffect(targetUnit, sourceUnit, tempEffectDef, tempEffectDef.status_id))
        {
            return false;
        }
        AddUnique(statusEffectIds, tempEffectDef.status_id);
        return true;
    }

    private static CombatEffectDef BuildSoulFractureStatusEffect(
        BattleExecuteSoulFractureParams soulFractureParams
    )
    {
        BattleExecuteSoulFractureParams resolvedParams = soulFractureParams.HasValue
            ? soulFractureParams
            : BattleExecuteSoulFractureParams.DefaultResisted;
        return new CombatEffectDef
        {
            effect_type = "apply_status",
            status_id = resolvedParams.StatusId,
            duration_tu = resolvedParams.DurationTu,
            heal_multiplier_percent = resolvedParams.HealMultiplierPercent,
            shield_gain_multiplier_percent = resolvedParams.ShieldGainMultiplierPercent,
        };
    }

    private static DamageApplicationInput BuildFatalExecuteDamageInput(
        CombatEffectDef effectDef,
        int resolvedDamage
    )
    {
        int normalizedDamage = Math.Max(resolvedDamage, 0);
        DeathResolutionContext deathContext =
            BattleDeathResolutionRules.PowerWordKillExecuteContext();
        DamageEventResult @event = new()
        {
            DamageTag = ProgressionDataUtils.to_string_name(effectDef?.damage_tag ?? ""),
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


    private int ResolveHealAmount(BattleUnitState sourceUnit, CombatEffectDef effectDef)
    {
        int healAmount = Math.Max(effectDef?.power ?? 0, 0);
        DicePoolRollResult healDiceRoll = RollEffectDice(sourceUnit, effectDef);
        if (healDiceRoll.HasDice)
        {
            healAmount += healDiceRoll.TotalWithBonus;
        }
        return Math.Max(healAmount, 1);
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

    private void ApplyStaminaRestore(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDef effectDef
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return;
        }
        int staminaAmount = Math.Max(effectDef.power, 0);
        DicePoolRollResult staminaDiceRoll = RollEffectDice(sourceUnit, effectDef);
        if (staminaDiceRoll.HasDice)
        {
            staminaAmount += staminaDiceRoll.TotalWithBonus;
        }
        if (staminaAmount <= 0)
        {
            return;
        }
        int maxStamina = Math.Max(
            GetAttributeValue(targetUnit, AttributeService.ToStringName(AttributeIdKind.StaminaMax)),
            0
        );
        targetUnit.SetCurrentStamina(Math.Min(targetUnit.current_stamina + staminaAmount, maxStamina));
    }

    private DicePoolRollResult RollEffectDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.dice_count, 0);
        int diceSides = ResolveEffectDiceSides(sourceUnit, effectDef);
        int diceBonus = effectDef.dice_bonus;
        return RollDicePoolValues(diceCount, diceSides, diceBonus);
    }

    private int ResolveEffectDiceSides(BattleUnitState sourceUnit, CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return 0;
        }
        if (effectDef.dice_sides_base > 0)
        {
            return ResolveAttributeScaledDiceSides(sourceUnit, effectDef);
        }
        return Math.Max(effectDef.dice_sides, 0);
    }

    private int ResolveAttributeScaledDiceSides(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        int conMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int willMod = GetUnitBaseAttributeModifier(sourceUnit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower));
        int baseSides = Math.Max(effectDef.dice_sides_base, 0);
        int conModSides = Math.Max(effectDef.dice_sides_per_constitution_mod, 0);
        int willModSides = Math.Max(effectDef.dice_sides_per_willpower_mod, 0);
        long diceSidesRaw =
            (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
        return (int)Math.Clamp(diceSidesRaw, 4L, int.MaxValue);
    }

    private int ResolveHealFatalAmount(BattleUnitState targetUnit, CombatEffectDef effectDef)
    {
        if (effectDef == null || targetUnit == null)
        {
            return 0;
        }
        int baseHeal = effectDef.base_heal;
        int healPerLevel = effectDef.heal_per_level;
        int conModBase = effectDef.con_mod_base;
        int conModPer2Levels = effectDef.con_mod_per_2_levels;
        int skillLevel = Math.Max(effectDef.skill_level, 1);
        int conMod = GetUnitBaseAttributeModifier(targetUnit, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution));
        int healAmount = baseHeal + healPerLevel * (skillLevel - 1);
        int conLevelBonus = conModBase + ((skillLevel - 1) / 2) * conModPer2Levels;
        healAmount += conMod * conLevelBonus;
        return Math.Max(healAmount, 1);
    }

    private bool ApplyStatusEffect(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        StringName statusIdOverride = default
    )
    {
        if (targetUnit == null || effectDef == null)
        {
            return false;
        }
        StringName resolvedStatusId = !IsEmpty(statusIdOverride)
            ? statusIdOverride
            : ProgressionDataUtils.to_string_name(effectDef.status_id);
        if (resolvedStatusId == "")
        {
            return false;
        }
        if (IsCrownBreakSealStatus(resolvedStatusId))
        {
            ClearOtherCrownBreakSeals(targetUnit, resolvedStatusId);
        }
        CombatEffectDef runtimeEffectDef = effectDef.DuplicateForRuntime();
        if (runtimeEffectDef == null)
        {
            return false;
        }
        runtimeEffectDef.status_id = resolvedStatusId;
        runtimeEffectDef.heal_multiplier_percent = effectDef.heal_multiplier_percent;
        runtimeEffectDef.shield_gain_multiplier_percent = effectDef.shield_gain_multiplier_percent;
        BattleStatusEffectState statusEntry = BattleStatusSemanticTable.MergeStatus(
            runtimeEffectDef,
            sourceUnit != null ? sourceUnit.unit_id : new StringName(""),
            targetUnit.GetStatusEffect(resolvedStatusId)
        );
        if (statusEntry == null)
        {
            return false;
        }
        targetUnit.SetStatusEffect(statusEntry);
        return true;
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

    private void GrantStatusOnHitToSource(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        if (sourceUnit == null || effectDef == null)
        {
            return;
        }
        StringName grantStatusId = effectDef.GetStringNameParamTyped("grant_status_id", "");
        if (grantStatusId == "")
        {
            return;
        }
        int grantPower = Math.Max(effectDef.GetIntParamTyped("grant_status_power", 1), 1);
        int grantDuration = Math.Max(effectDef.GetIntParamTyped("grant_status_duration_tu", 180), 0);
        int stackLimit = Math.Max(effectDef.GetIntParamTyped("grant_status_stack_limit", 20), 1);
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

    private DicePoolRollResult RollConsumedStackDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        StringName rollMode = default
    )
    {
        if (sourceUnit == null || effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        StringName consumedId = ProgressionDataUtils.to_string_name(effectDef.consumed_status_id);
        int dicePerStack = Math.Max(effectDef.dice_per_consumed_stack, 0);
        int diceSides = Math.Max(effectDef.dice_sides_per_stack, 0);
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
        if (_suppress_last_stand_mastery_records || targetUnit == null || baseAmount <= 0)
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
        SkillDef skillDef = GetSkillDefTyped(sourceSkillId);
        if (skillDef?.combat_profile == null)
        {
            return false;
        }
        StringName fatalStatusId = ProgressionDataUtils.to_string_name(deathWardEntry.status_id);
        foreach (CombatEffectDef effectDef in skillDef.combat_profile.passive_effect_defs)
        {
            if (
                effectDef == null
                || effectDef.TriggerConditionKind
                    != CombatEffectTriggerCondition.OnFatalDamage
            )
            {
                continue;
            }
            StringName requiredStatusId = ProgressionDataUtils.to_string_name(
                effectDef.trigger_status_id
            );
            if (requiredStatusId != "" && requiredStatusId != fatalStatusId)
            {
                continue;
            }
            int minLevel = Math.Max(effectDef.min_skill_level, 0);
            int maxLevel = effectDef.max_skill_level;
            if (skillLevel < minLevel || (maxLevel >= 0 && skillLevel > maxLevel))
            {
                continue;
            }
            CombatEffectDef runtimeEffectDef = effectDef.DuplicateForRuntime();
            if (runtimeEffectDef == null)
            {
                continue;
            }
            runtimeEffectDef.skill_level = skillLevel;
            ResolveEffects(targetUnit, targetUnit, new GArray { runtimeEffectDef });
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
