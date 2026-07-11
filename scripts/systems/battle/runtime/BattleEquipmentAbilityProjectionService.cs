using System;
using System.Collections.Generic;
using Godot;

internal static class BattleEquipmentAbilityProjectionService
{
    internal static List<BattleEquipmentAbilitySourceState> ProjectPlayerPersistentSources(
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        List<BattleEquipmentAbilitySourceState> result = new();
        if (unit != null)
            unit.temporal_progress_modifiers = new List<BattleTemporalProgressModifierState>();
        if (
            unit == null
            || unit.effective_trait_instances == null
            || bindings == null
            || bindings.Count == 0
        )
        {
            return result;
        }

        EquipmentState equipmentView = unit.GetEquipmentView();
        foreach (BattleEffectiveTraitInstanceState traitInstance in unit.effective_trait_instances)
        {
            if (traitInstance == null || traitInstance.trait_id == "")
                continue;

            TraitSourceKind traitSourceKind =
                TraitContentRules.ToSourceKind(traitInstance.source_type);
            if (
                traitSourceKind != TraitSourceKind.EquipmentFixed
                && traitSourceKind != TraitSourceKind.EquipmentRoll
            )
            {
                continue;
            }

            EquipmentEntryState equipmentEntry = FindEquipmentEntryByInstanceId(
                equipmentView,
                traitInstance.source_id
            );
            if (equipmentEntry == null || equipmentEntry.IsEmpty())
                continue;

            ItemDefinition sourceItem = ResolveItemDefinition(
                equipmentEntry.item_id,
                itemDefinitions
            );
            IReadOnlyList<EquipmentAbilityBindingDefinition> matchedBindings =
                EquipmentAbilityBindingMatcher.FindBindings(
                    bindings.Values,
                    traitInstance.trait_id,
                    traitSourceKind,
                    GetTraitCategories(traitInstance.trait_id, traitDefs),
                    sourceItem
                );
            if (matchedBindings.Count == 0)
                continue;

            result.Add(
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey = traitInstance.effective_instance_key,
                    EquipmentDefId = equipmentEntry.item_id,
                    SourceEquipmentInstanceId = equipmentEntry.instance_id,
                    SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                    AbilityIds = SortedBindingIds(matchedBindings),
                }
            );
            AddTemporalProgressModifiers(
                unit,
                matchedBindings,
                equipmentEntry.instance_id
            );
        }

        return result;
    }

    internal static List<BattleEquipmentAbilitySourceState> ProjectEnemyBattleOnlySources(
        BattleUnitState unit,
        EnemyTemplateDef template,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        List<BattleEquipmentAbilitySourceState> result = new();
        if (unit != null)
            unit.temporal_progress_modifiers = new List<BattleTemporalProgressModifierState>();
        if (unit == null || template == null || bindings == null || bindings.Count == 0)
            return result;

        StringName attackEquipmentItemId = ProgressionDataUtils.to_string_name(
            template.attack_equipment_item_id
        );
        if (attackEquipmentItemId == "")
            return result;

        ItemDefinition sourceItem = ResolveItemDefinition(
            attackEquipmentItemId,
            itemDefinitions
        );
        if (sourceItem == null)
            return result;

        foreach (StringName traitId in sourceItem.GetTraitIdsTyped())
        {
            StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
            if (normalizedTraitId == "")
                continue;
            IReadOnlyList<EquipmentAbilityBindingDefinition> matchedBindings =
                EquipmentAbilityBindingMatcher.FindBindings(
                    bindings.Values,
                    normalizedTraitId,
                    TraitSourceKind.EquipmentFixed,
                    GetTraitCategories(normalizedTraitId, traitDefs),
                    sourceItem
                );
            if (matchedBindings.Count == 0)
                continue;

            result.Add(
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey = new StringName(
                        $"enemy_battle_only_equipment::{unit.unit_id}::{attackEquipmentItemId}::{normalizedTraitId}"
                    ),
                    EquipmentDefId = attackEquipmentItemId,
                    SourceEquipmentInstanceId = "",
                    SourceKind = EquipmentAbilitySourceKind.EnemyBattleOnlyEquipment,
                    AbilityIds = SortedBindingIds(matchedBindings),
                }
            );
            AddTemporalProgressModifiers(unit, matchedBindings, "");
        }

        return result;
    }

    internal static StringNameList ProjectCreatureTypeTags(EnemyTemplateDef template)
    {
        StringNameList result = new();
        HashSet<StringName> seen = new();
        if (template?.tags == null)
            return result;
        foreach (StringName tag in template.tags)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(tag);
            if (normalized != "" && seen.Add(normalized))
                result.Add(normalized);
        }
        return result;
    }

    internal static bool UnitHasCreatureTypeTag(BattleUnitState unit, StringName tag)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(tag);
        return unit != null && normalized != "" && unit.creature_type_tags.Contains(normalized);
    }

    private static EquipmentEntryState FindEquipmentEntryByInstanceId(
        EquipmentState equipmentView,
        StringName instanceId
    )
    {
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        if (equipmentView == null || normalizedInstanceId == "")
            return null;

        foreach (StringName entrySlotId in equipmentView.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipmentView.GetEntry(entrySlotId);
            if (entry != null && entry.instance_id == normalizedInstanceId)
                return entry;
        }
        return null;
    }

    private static ItemDefinition ResolveItemDefinition(
        StringName itemId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "" || itemDefinitions == null)
            return null;
        return itemDefinitions.TryGetValue(
            normalizedItemId,
            out ItemDefinition itemDefinition
        )
            ? itemDefinition
            : null;
    }

    private static IReadOnlySet<StringName> GetTraitCategories(
        StringName traitId,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs
    )
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        if (
            normalizedTraitId == ""
            || traitDefs == null
            || !traitDefs.TryGetValue(normalizedTraitId, out TraitDefinition traitDef)
            || traitDef == null
        )
        {
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        }

        HashSet<StringName> result = new();
        foreach (StringName category in traitDef.Categories)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(category);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static List<StringName> SortedBindingIds(
        IReadOnlyList<EquipmentAbilityBindingDefinition> bindings
    )
    {
        List<StringName> result = new();
        if (bindings != null)
        {
            foreach (EquipmentAbilityBindingDefinition binding in bindings)
            {
                if (binding != null && binding.BindingId != "")
                    result.Add(binding.BindingId);
            }
        }
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static void AddTemporalProgressModifiers(
        BattleUnitState unit,
        IReadOnlyList<EquipmentAbilityBindingDefinition> bindings,
        StringName sourceEquipmentInstanceId
    )
    {
        if (unit == null || bindings == null)
            return;
        unit.temporal_progress_modifiers ??= new List<BattleTemporalProgressModifierState>();
        foreach (EquipmentAbilityBindingDefinition binding in bindings)
        {
            foreach (EquipmentTemporalProgressModifierDefinition modifier in binding?.TemporalProgressModifiers ?? Array.Empty<EquipmentTemporalProgressModifierDefinition>())
            {
                if (modifier == null || modifier.ModifierId == "")
                    continue;
                unit.temporal_progress_modifiers.Add(
                    new BattleTemporalProgressModifierState
                    {
                        ModifierId = modifier.ModifierId,
                        BindingId = binding.BindingId,
                        SourceEquipmentInstanceId = sourceEquipmentInstanceId,
                        AppliesToActionProgress = modifier.AppliesToActionProgress,
                        AppliesToCastProgress = modifier.AppliesToCastProgress,
                        SaveDc = modifier.SaveDc,
                        AttributeModifierId = modifier.AttributeModifierId,
                        SuccessRatePercent = modifier.SuccessRatePercent,
                        FailureRatePercent = modifier.FailureRatePercent,
                        Label = modifier.Label,
                    }
                );
            }
        }
    }
}
