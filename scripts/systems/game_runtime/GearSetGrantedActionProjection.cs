using System;
using System.Collections.Generic;
using Godot;

// Typed projection of gear-set granted actions (active threshold -> granted trait ->
// equipment ability binding -> granted action). Shared by the party equipment page,
// the battle character-info view, the battle HUD snapshot, and the headless snapshot
// chain. The projection is pure read: it never mutates equipment instances or usage
// ledgers, and it works over any EquipmentState view (world or battle-local), so
// battle-local consumption/unequip is reflected by passing the battle-local view.
public sealed class GearSetGrantedActionSummary
{
    internal GearSetGrantedActionSummary(
        StringName gearSetId,
        StringName thresholdId,
        StringName grantedActionId,
        StringName skillId,
        string displayName,
        EquipmentAbilityUsagePeriodKind usagePeriodKind,
        int maxUsesPerPeriod,
        bool isAvailable,
        int remainingUses,
        StringName disabledReason
    )
    {
        GearSetId = gearSetId;
        ThresholdId = thresholdId;
        GrantedActionId = grantedActionId;
        SkillId = skillId;
        DisplayName = displayName ?? "";
        UsagePeriodKind = usagePeriodKind;
        MaxUsesPerPeriod = maxUsesPerPeriod;
        IsAvailable = isAvailable;
        RemainingUses = remainingUses;
        DisabledReason = disabledReason ?? new StringName("");
    }

    public StringName GearSetId { get; }
    public StringName ThresholdId { get; }
    public StringName GrantedActionId { get; }
    public StringName SkillId { get; }
    public string DisplayName { get; }
    public EquipmentAbilityUsagePeriodKind UsagePeriodKind { get; }
    public int MaxUsesPerPeriod { get; }
    public bool IsAvailable { get; }

    // -1 when the action is not usage-limited.
    public int RemainingUses { get; }

    // Empty when the action is available.
    public StringName DisabledReason { get; }
}

public static class GearSetGrantedActionProjection
{
    internal static readonly StringName UsageExhaustedReason = "equipment_skill_usage_exhausted";
    internal static readonly StringName UsageUnavailableReason = "equipment_skill_usage_unavailable";

    // Builds one summary per granted skill action of every active threshold, sorted
    // stably by (gear set id, threshold id, granted action id). Persistent-period
    // remaining uses are read from the usage anchor instance ledger inside the given
    // equipment view. Per-battle charges live on the battle-local unit rather than the
    // instance ledger; per-battle grants therefore report a full remaining count and
    // the canonical availability grid stays the authoritative consumer for that state.
    public static IReadOnlyList<GearSetGrantedActionSummary> Build(
        GearSetEvaluationSnapshot evaluation,
        EquipmentState equipmentView,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        int worldStep
    )
    {
        var result = new List<GearSetGrantedActionSummary>();
        if (evaluation == null || equipmentView == null || bindings == null || bindings.Count == 0)
            return result.AsReadOnly();

        foreach (GearSetDerivedTraitInstance derived in evaluation.DerivedTraitInstances)
        {
            if (derived == null || derived.TraitId == "")
                continue;
            ItemDefinition anchorItem = ResolveItemDefinition(derived.SourceItemId, itemDefinitions);
            IReadOnlyList<EquipmentAbilityBindingDefinition> matchedBindings =
                EquipmentAbilityBindingMatcher.FindBindings(
                    bindings.Values,
                    derived.TraitId,
                    TraitSourceKind.GearSetThreshold,
                    GetTraitCategories(derived.TraitId, traitDefinitions),
                    anchorItem
                );
            foreach (EquipmentAbilityBindingDefinition binding in matchedBindings)
            {
                foreach (EquipmentGrantedActionDefinition grant in binding.GrantedActions)
                {
                    if (
                        grant == null
                        || grant.GrantedKind != EquipmentGrantedActionKind.Skill
                        || grant.GrantedActionId == ""
                    )
                    {
                        continue;
                    }
                    result.Add(
                        BuildSummary(derived, grant, equipmentView, skillDefinitions, worldStep)
                    );
                }
            }
        }

        result.Sort(CompareSummaries);
        return result.AsReadOnly();
    }

    private static GearSetGrantedActionSummary BuildSummary(
        GearSetDerivedTraitInstance derived,
        EquipmentGrantedActionDefinition grant,
        EquipmentState equipmentView,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        int worldStep
    )
    {
        int maxUses = Math.Max(grant.MaxUsesPerPeriod, 0);
        bool isLimited =
            EquipmentAbilityUsagePeriodKinds.IsLimited(grant.UsagePeriodKind) && maxUses > 0;
        bool available = true;
        int remainingUses = -1;
        StringName disabledReason = "";
        if (isLimited && grant.UsagePeriodKind == EquipmentAbilityUsagePeriodKind.PerBattle)
        {
            remainingUses = maxUses;
        }
        else if (isLimited)
        {
            int periodIndex = EquipmentAbilityUsageRuntime.ResolvePeriodIndex(
                grant.UsagePeriodKind,
                worldStep
            );
            EquipmentInstanceState anchorInstance = FindInstance(
                equipmentView,
                derived.SourceEquipmentInstanceId
            );
            if (periodIndex < 0 || anchorInstance == null)
            {
                available = false;
                remainingUses = 0;
                disabledReason = UsageUnavailableReason;
            }
            else
            {
                int usedCount = EquipmentAbilityUsageRuntime.GetUsedCount(
                    anchorInstance,
                    grant.GrantedActionId,
                    grant.UsagePeriodKind,
                    periodIndex
                );
                remainingUses = Math.Max(maxUses - usedCount, 0);
                if (remainingUses <= 0)
                {
                    available = false;
                    disabledReason = UsageExhaustedReason;
                }
            }
        }
        return new GearSetGrantedActionSummary(
            derived.GearSetId,
            derived.ThresholdId,
            grant.GrantedActionId,
            grant.SkillId,
            ResolveDisplayName(grant.SkillId, skillDefinitions),
            grant.UsagePeriodKind,
            maxUses,
            available,
            remainingUses,
            disabledReason
        );
    }

    private static int CompareSummaries(
        GearSetGrantedActionSummary left,
        GearSetGrantedActionSummary right
    )
    {
        int bySet = string.CompareOrdinal(
            left?.GearSetId.ToString() ?? "",
            right?.GearSetId.ToString() ?? ""
        );
        if (bySet != 0)
            return bySet;
        int byThreshold = string.CompareOrdinal(
            left?.ThresholdId.ToString() ?? "",
            right?.ThresholdId.ToString() ?? ""
        );
        if (byThreshold != 0)
            return byThreshold;
        return string.CompareOrdinal(
            left?.GrantedActionId.ToString() ?? "",
            right?.GrantedActionId.ToString() ?? ""
        );
    }

    private static EquipmentInstanceState FindInstance(
        EquipmentState equipment,
        StringName instanceId
    )
    {
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
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

    private static ItemDefinition ResolveItemDefinition(
        StringName itemId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        return normalizedItemId != ""
            && itemDefinitions != null
            && itemDefinitions.TryGetValue(normalizedItemId, out ItemDefinition itemDefinition)
            ? itemDefinition
            : null;
    }

    private static IReadOnlySet<StringName> GetTraitCategories(
        StringName traitId,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions
    )
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        if (
            normalizedTraitId == ""
            || traitDefinitions == null
            || !traitDefinitions.TryGetValue(normalizedTraitId, out TraitDefinition traitDef)
            || traitDef == null
        )
        {
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        }

        var result = new HashSet<StringName>();
        foreach (StringName category in traitDef.Categories)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(category);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static string ResolveDisplayName(
        StringName skillId,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        if (
            skillId != ""
            && skillDefinitions != null
            && skillDefinitions.TryGetValue(skillId, out SkillDefinition skillDefinition)
            && skillDefinition != null
            && !string.IsNullOrEmpty(skillDefinition.DisplayName)
        )
        {
            return skillDefinition.DisplayName;
        }
        return skillId.ToString();
    }
}
