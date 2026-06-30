using System;
using System.Collections.Generic;
using Godot;

internal static class BattleEquipmentAbilityProjectionService
{
    internal static List<BattleEquipmentAbilitySourceState> ProjectPlayerPersistentSources(
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        List<BattleEquipmentAbilitySourceState> result = new();
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

            ItemDef sourceItem = ResolveItemDef(equipmentEntry.item_id, itemDefs);
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
        }

        return result;
    }

    internal static List<BattleEquipmentAbilitySourceState> ProjectEnemyBattleOnlySources(
        BattleUnitState unit,
        EnemyTemplateDef template,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        List<BattleEquipmentAbilitySourceState> result = new();
        if (unit == null || template == null || bindings == null || bindings.Count == 0)
            return result;

        StringName attackEquipmentItemId = ProgressionDataUtils.to_string_name(
            template.attack_equipment_item_id
        );
        if (attackEquipmentItemId == "")
            return result;

        ItemDef sourceItem = ResolveItemDef(attackEquipmentItemId, itemDefs);
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

    private static ItemDef ResolveItemDef(
        StringName itemId,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "" || itemDefs == null)
            return null;
        return itemDefs.TryGetValue(normalizedItemId, out ItemDef itemDef) ? itemDef : null;
    }

    private static IReadOnlySet<StringName> GetTraitCategories(
        StringName traitId,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs
    )
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        if (
            normalizedTraitId == ""
            || traitDefs == null
            || !traitDefs.TryGetValue(normalizedTraitId, out TraitDef traitDef)
            || traitDef?.categories == null
        )
        {
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        }

        HashSet<StringName> result = new();
        foreach (StringName category in traitDef.categories)
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
}
