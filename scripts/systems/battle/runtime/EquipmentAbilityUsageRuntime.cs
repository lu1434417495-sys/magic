using Godot;
using System;
using System.Collections.Generic;

internal static class EquipmentAbilityUsageRuntime
{
    internal static readonly StringName PerActionTurnUseExhaustedReason =
        "equipment_skill_turn_use_exhausted";

    internal static bool IsAvailableForGrant(
        BattleUnitState unit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentGrantedActionDefinition grant,
        int worldStep,
        BattleState battleState,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex,
        out StringName disabledReason
    )
    {
        disabledReason = "";
        if (
            !AvailabilityConditionsPass(
                grant?.AvailabilityConditions,
                unit,
                source,
                binding,
                battleState,
                bindingIndex
            )
        )
        {
            disabledReason = "equipment_skill_availability_blocked";
            return false;
        }
        if (grant.UsagePeriodKind == EquipmentAbilityUsagePeriodKind.PerBattle)
        {
            StringName chargeKey = BuildPerBattleChargeKey(source, grant);
            if (chargeKey == "")
            {
                disabledReason = "equipment_skill_usage_unavailable";
                return false;
            }
            bool available =
                !unit.HasPerBattleChargeTyped(chargeKey)
                || unit.GetPerBattleChargeTyped(chargeKey, Math.Max(grant.MaxUsesPerPeriod, 1)) > 0;
            if (!available)
                disabledReason = "equipment_skill_usage_exhausted";
            if (!available)
                return false;
        }
        else if (IsLimited(grant))
        {
            int periodIndex = ResolvePeriodIndex(grant.UsagePeriodKind, worldStep);
            if (periodIndex < 0)
            {
                disabledReason = "equipment_skill_usage_unavailable";
                return false;
            }

            EquipmentInstanceState instance = FindEquipmentInstance(
                unit,
                source?.SourceEquipmentInstanceId ?? new StringName("")
            );
            if (instance == null)
            {
                disabledReason = "equipment_skill_usage_unavailable";
                return false;
            }

            int usedCount = GetUsedCount(
                instance,
                grant.GrantedActionId,
                grant.UsagePeriodKind,
                periodIndex
            );
            if (usedCount >= Math.Max(grant.MaxUsesPerPeriod, 1))
            {
                disabledReason = "equipment_skill_usage_exhausted";
                return false;
            }
        }

        if (HasPerActionTurnUse(unit, source, grant))
        {
            disabledReason = PerActionTurnUseExhaustedReason;
            return false;
        }
        return true;
    }

    private static bool AvailabilityConditionsPass(
        EquipmentConditionGroupDefinition group,
        BattleUnitState unit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        BattleState battleState,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex
    )
    {
        if (group == null)
            return true;
        bool anyMode = group.Mode == "any";
        bool sawAny = false;
        bool passed = anyMode ? false : true;

        foreach (EquipmentAbilityConditionDefinition condition in group.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            sawAny = true;
            bool conditionPassed = AvailabilityConditionPasses(
                condition,
                unit,
                source,
                binding,
                battleState,
                bindingIndex
            );
            passed = anyMode ? passed || conditionPassed : passed && conditionPassed;
        }
        foreach (EquipmentConditionGroupDefinition child in group.Groups ?? Array.Empty<EquipmentConditionGroupDefinition>())
        {
            sawAny = true;
            bool childPassed = AvailabilityConditionsPass(
                child,
                unit,
                source,
                binding,
                battleState,
                bindingIndex
            );
            passed = anyMode ? passed || childPassed : passed && childPassed;
        }
        if (!sawAny)
            passed = true;
        return group.Negate ? !passed : passed;
    }

    private static bool AvailabilityConditionPasses(
        EquipmentAbilityConditionDefinition condition,
        BattleUnitState unit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        BattleState battleState,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex
    )
    {
        if (
            condition?.Kind == "compare_fact"
            && condition.PayloadDefinition is CompareFactConditionPayloadDefinition payload
            && TryResolveAvailabilityFactInt(
                payload.Left,
                unit,
                source,
                binding,
                battleState,
                bindingIndex,
                out int left
            )
            && TryResolveAvailabilityFactInt(
                payload.Right,
                unit,
                source,
                binding,
                battleState,
                bindingIndex,
                out int right
            )
        )
        {
            return CompareInt(left, payload.Compare, right);
        }
        return false;
    }

    private static bool TryResolveAvailabilityFactInt(
        EquipmentAbilityFactQueryDefinition query,
        BattleUnitState unit,
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        BattleState battleState,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex,
        out int value
    )
    {
        value = 0;
        if (query == null)
            return false;
        if (query.QueryKind == "literal")
        {
            value = query.IntLiteral;
            return true;
        }
        if (query.QueryKind == "fact" && query.FactId == "current_tu")
        {
            value = Math.Max(battleState?.timeline?.current_tu ?? -1, -1);
            return value >= 0;
        }
        if (query.QueryKind == "fact" && query.FactId == "equipment_ability_state")
        {
            EquipmentAbilityBindingDefinition stateBinding = ResolveStateBinding(
                query.BindingId,
                binding,
                bindingIndex
            );
            StringName chargeKey = BuildBindingStateChargeKey(source, stateBinding, query.StateKey);
            if (unit == null || chargeKey == "")
                return false;
            value = IsPerBattleState(stateBinding, query.StateKey)
                ? unit.GetPerBattleChargeTyped(chargeKey, InitialStateValue(stateBinding, query.StateKey))
                : unit.GetPerTurnChargeTyped(chargeKey, InitialStateValue(stateBinding, query.StateKey));
            return true;
        }
        if (query.QueryKind == "fact" && query.FactId == "summoned_unit_count")
        {
            EquipmentAbilityBindingDefinition summonBinding = ResolveStateBinding(
                query.BindingId,
                binding,
                bindingIndex
            );
            value = CountLivingSummonedUnits(
                battleState,
                unit,
                source,
                summonBinding?.BindingId ?? query.BindingId,
                query.StateKey
            );
            return true;
        }
        return false;
    }

    private static int CountLivingSummonedUnits(
        BattleState battleState,
        BattleUnitState sourceUnit,
        BattleEquipmentAbilitySourceState source,
        StringName bindingId,
        StringName stateKey
    )
    {
        if (battleState == null || sourceUnit == null)
            return 0;
        int count = 0;
        StringName sourceEquipmentInstanceId = source?.SourceEquipmentInstanceId ?? "";
        foreach (BattleUnitState unit in battleState.GetUnitsTyped())
        {
            BattleAiBlackboard blackboard = unit?.ai_blackboard;
            if (unit == null || !unit.is_alive || blackboard?.summoned != true)
                continue;
            if (blackboard.summon_source_unit_id != sourceUnit.unit_id)
                continue;
            if (
                sourceEquipmentInstanceId != ""
                && blackboard.summon_source_equipment_instance_id != sourceEquipmentInstanceId
            )
            {
                continue;
            }
            if (bindingId != "" && blackboard.summon_binding_id != bindingId)
                continue;
            if (stateKey != "" && blackboard.summon_state_key != stateKey)
                continue;
            count++;
        }
        return count;
    }

    private static EquipmentAbilityBindingDefinition ResolveStateBinding(
        StringName bindingId,
        EquipmentAbilityBindingDefinition fallbackBinding,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex
    )
    {
        StringName normalizedBindingId = ProgressionDataUtils.to_string_name(bindingId);
        if (
            normalizedBindingId != ""
            && bindingIndex != null
            && bindingIndex.TryGetValue(normalizedBindingId, out EquipmentAbilityBindingDefinition binding)
            && binding != null
        )
        {
            return binding;
        }
        return fallbackBinding;
    }

    private static StringName BuildBindingStateChargeKey(
        BattleEquipmentAbilitySourceState source,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        StringName ownerSourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EquipmentDefId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        if (ownerSourceKey == "" || normalizedStateKey == "")
            return "";
        return new StringName(
            string.Join(
                "|",
                "equipment_ability",
                "state",
                ownerSourceKey.ToString(),
                normalizedStateKey.ToString()
            )
        );
    }

    private static int InitialStateValue(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    ) => Math.Max(FindStateSchema(binding, stateKey)?.InitialIntValue ?? 0, 0);

    private static bool IsPerBattleState(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName resetTiming = ProgressionDataUtils.to_string_name(
            FindStateSchema(binding, stateKey)?.ResetTiming ?? new StringName("")
        );
        return resetTiming == "per_battle" || resetTiming == "battle";
    }

    private static EquipmentAbilityStateSchemaDefinition FindStateSchema(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        if (binding == null || normalizedStateKey == "")
            return null;
        foreach (EquipmentAbilityStateSchemaDefinition schema in binding.StateSchemas ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>())
        {
            if (schema?.StateKey == normalizedStateKey)
                return schema;
        }
        return null;
    }

    private static bool CompareInt(int value, StringName compare, int threshold) =>
        ProgressionDataUtils.to_string_name(compare).ToString() switch
        {
            "lte" => value <= threshold,
            "lt" => value < threshold,
            "gte" => value >= threshold,
            "gt" => value > threshold,
            "eq" => value == threshold,
            _ => false,
        };

    internal static bool TryCommitUsage(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry,
        int worldStep
    )
    {
        if (
            unit == null
            || entry == null
            || entry.EntryRef.SourceKind != BattleSkillEntrySourceKind.EquipmentSkill
        )
        {
            return false;
        }

        if (HasPerActionTurnUse(unit, entry))
            return false;

        bool committedPeriodUsage = false;
        if (
            EquipmentAbilityUsagePeriodKinds.IsLimited(entry.EquipmentUsagePeriodKind)
            && entry.EquipmentMaxUsesPerPeriod > 0
        )
        {
            if (!TryCommitLimitedUsage(unit, entry, worldStep))
                return false;
            committedPeriodUsage = true;
        }

        bool committedTurnUsage = TryCommitPerActionTurnUse(unit, entry);
        return committedPeriodUsage || committedTurnUsage;
    }

    private static bool TryCommitLimitedUsage(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry,
        int worldStep
    )
    {
        if (entry.EquipmentUsagePeriodKind == EquipmentAbilityUsagePeriodKind.PerBattle)
        {
            StringName chargeKey = BuildPerBattleChargeKey(entry);
            if (chargeKey == "")
                return false;
            int maxUses = Math.Max(entry.EquipmentMaxUsesPerPeriod, 1);
            if (!unit.HasPerBattleChargeTyped(chargeKey))
                unit.SetPerBattleChargeTyped(chargeKey, maxUses);
            int remaining = unit.GetPerBattleChargeTyped(chargeKey, 0);
            if (remaining <= 0)
                return false;
            unit.SetPerBattleChargeTyped(chargeKey, remaining - 1);
            return true;
        }

        int periodIndex = ResolvePeriodIndex(entry.EquipmentUsagePeriodKind, worldStep);
        if (periodIndex < 0)
            return false;

        EquipmentInstanceState instance = FindEquipmentInstance(
            unit,
            entry.EntryRef.SourceEquipmentInstanceId
        );
        if (instance == null)
            return false;

        EquipmentAbilityUsagePeriodState usage = FindUsagePeriod(
            instance,
            entry.EquipmentGrantedActionId,
            entry.EquipmentUsagePeriodKind,
            periodIndex
        );
        if (usage == null)
        {
            instance.ability_usage_periods.Add(
                new EquipmentAbilityUsagePeriodState
                {
                    AbilityId = ToText(entry.EquipmentGrantedActionId),
                    PeriodKind = ToPeriodKindText(entry.EquipmentUsagePeriodKind),
                    PeriodIndex = periodIndex,
                    UsedCount = 1,
                }
            );
            return true;
        }

        if (usage.UsedCount >= entry.EquipmentMaxUsesPerPeriod)
            return false;
        usage.UsedCount = Math.Min(usage.UsedCount + 1, entry.EquipmentMaxUsesPerPeriod);
        return true;
    }

    internal static int ResolvePeriodIndex(EquipmentAbilityUsagePeriodKind kind, int worldStep) =>
        kind switch
        {
            EquipmentAbilityUsagePeriodKind.PerBattle => 0,
            EquipmentAbilityUsagePeriodKind.PerWorldDay => WorldTimeSystem.StepToDay(worldStep),
            EquipmentAbilityUsagePeriodKind.PerWorldMonth => WorldTimeSystem.StepToMonth(worldStep),
            _ => -1,
        };

    internal static EquipmentInstanceState FindEquipmentInstance(
        BattleUnitState unit,
        StringName instanceId
    )
    {
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        EquipmentState equipment = unit?.GetEquipmentView();
        if (equipment == null || normalizedInstanceId == "")
            return null;

        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            if (entry != null && entry.instance_id == normalizedInstanceId)
                return entry.GetEquipmentInstance();
        }
        return null;
    }

    internal static int GetUsedCount(
        EquipmentInstanceState instance,
        StringName abilityId,
        EquipmentAbilityUsagePeriodKind periodKind,
        int periodIndex
    )
    {
        EquipmentAbilityUsagePeriodState usage = FindUsagePeriod(
            instance,
            abilityId,
            periodKind,
            periodIndex
        );
        return Math.Max(usage?.UsedCount ?? 0, 0);
    }

    private static bool IsLimited(EquipmentGrantedActionDefinition grant) =>
        grant != null
        && EquipmentAbilityUsagePeriodKinds.IsLimited(grant.UsagePeriodKind)
        && grant.MaxUsesPerPeriod > 0;

    private static bool HasPerActionTurnUse(
        BattleUnitState unit,
        BattleEquipmentAbilitySourceState source,
        EquipmentGrantedActionDefinition grant
    )
    {
        StringName chargeKey = BuildPerActionTurnUseKey(source, grant);
        return unit != null && chargeKey != "" && unit.HasPerTurnChargeTyped(chargeKey);
    }

    private static bool HasPerActionTurnUse(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry
    )
    {
        StringName chargeKey = BuildPerActionTurnUseKey(entry);
        return unit != null && chargeKey != "" && unit.HasPerTurnChargeTyped(chargeKey);
    }

    private static bool TryCommitPerActionTurnUse(
        BattleUnitState unit,
        BattleAvailableSkillEntry entry
    )
    {
        StringName chargeKey = BuildPerActionTurnUseKey(entry);
        if (unit == null || chargeKey == "" || unit.HasPerTurnChargeTyped(chargeKey))
            return false;
        unit.SetPerTurnChargeTyped(chargeKey, 1);
        return true;
    }

    private static StringName BuildPerBattleChargeKey(
        BattleEquipmentAbilitySourceState source,
        EquipmentGrantedActionDefinition grant
    )
    {
        if (source == null || grant == null || grant.GrantedActionId == "")
            return "";
        return new StringName(
            $"equipment_skill:{source.SourceEquipmentInstanceId}:{grant.GrantedActionId}"
        );
    }

    private static StringName BuildPerBattleChargeKey(BattleAvailableSkillEntry entry)
    {
        if (entry?.EntryRef == null || entry.EquipmentGrantedActionId == "")
            return "";
        return new StringName(
            $"equipment_skill:{entry.EntryRef.SourceEquipmentInstanceId}:{entry.EquipmentGrantedActionId}"
        );
    }

    private static StringName BuildPerActionTurnUseKey(
        BattleEquipmentAbilitySourceState source,
        EquipmentGrantedActionDefinition grant
    )
    {
        if (source == null || grant == null || grant.GrantedActionId == "")
            return "";
        StringName ownerSourceKey = source.SourceEquipmentInstanceId;
        if (ownerSourceKey == "")
            ownerSourceKey = source.EquipmentDefId;
        if (ownerSourceKey == "")
            ownerSourceKey = source.EffectiveInstanceKey;
        if (ownerSourceKey == "")
            return "";
        return new StringName(
            $"equipment_skill_turn_use:{ownerSourceKey}:{grant.GrantedActionId}"
        );
    }

    private static StringName BuildPerActionTurnUseKey(BattleAvailableSkillEntry entry)
    {
        if (entry?.EntryRef == null || entry.EquipmentGrantedActionId == "")
            return "";
        StringName ownerSourceKey = entry.EntryRef.SourceEquipmentInstanceId;
        if (ownerSourceKey == "")
            return "";
        return new StringName(
            $"equipment_skill_turn_use:{ownerSourceKey}:{entry.EquipmentGrantedActionId}"
        );
    }

    private static EquipmentAbilityUsagePeriodState FindUsagePeriod(
        EquipmentInstanceState instance,
        StringName abilityId,
        EquipmentAbilityUsagePeriodKind periodKind,
        int periodIndex
    )
    {
        if (
            instance?.ability_usage_periods == null
            || abilityId == ""
            || periodIndex < 0
            || !EquipmentAbilityUsagePeriodKinds.IsLimited(periodKind)
        )
        {
            return null;
        }

        string abilityText = ToText(abilityId);
        string periodKindText = ToPeriodKindText(periodKind);
        foreach (EquipmentAbilityUsagePeriodState usage in instance.ability_usage_periods)
        {
            if (
                usage != null
                && string.Equals(usage.AbilityId ?? "", abilityText, StringComparison.Ordinal)
                && string.Equals(usage.PeriodKind ?? "", periodKindText, StringComparison.Ordinal)
                && usage.PeriodIndex == periodIndex
            )
            {
                return usage;
            }
        }
        return null;
    }

    private static string ToPeriodKindText(EquipmentAbilityUsagePeriodKind kind) =>
        EquipmentAbilityUsagePeriodKinds.ToStringName(kind).ToString();

    private static string ToText(StringName value) =>
        ProgressionDataUtils.to_string_name(value).ToString();
}
