using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAbilityProjectionResult
{
    internal BattleEquipmentAbilityProjectionResult(
        IReadOnlyList<BattleEquipmentAbilitySourceState> sources,
        IReadOnlyList<BattleTemporalProgressModifierState>
            temporalProgressModifiers
    )
    {
        Sources =
            sources
            ?? Array.Empty<
                BattleEquipmentAbilitySourceState
            >();
        TemporalProgressModifiers =
            temporalProgressModifiers
            ?? Array.Empty<
                BattleTemporalProgressModifierState
            >();
    }

    internal IReadOnlyList<BattleEquipmentAbilitySourceState>
        Sources { get; }

    internal IReadOnlyList<BattleTemporalProgressModifierState>
        TemporalProgressModifiers { get; }
}

internal static class BattleEquipmentAbilityProjectionService
{
    internal static BattleEquipmentAbilityProjectionResult
        ProjectPlayerPersistent(
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        List<BattleEquipmentAbilitySourceState> sources =
            new();
        List<BattleTemporalProgressModifierState>
            temporalProgressModifiers = new();
        BattleUnitEffectiveTraitReadView effectiveTraits =
            unit?.GetEffectiveTraitsReadViewTyped()
            ?? BattleUnitEffectiveTraitReadView.MissingOwner;
        if (
            unit == null
            || !effectiveTraits.OwnerPresent
            || !effectiveTraits.Instances.IsPresent
            || bindings == null
            || bindings.Count == 0
        )
        {
            return new BattleEquipmentAbilityProjectionResult(
                sources,
                temporalProgressModifiers
            );
        }

        EquipmentState equipmentView = unit.GetEquipmentView();
        foreach (
            BattleEffectiveTraitInstanceReadView traitInstance
            in effectiveTraits.Instances
        )
        {
            if (!traitInstance.IsPresent || traitInstance.TraitId == "")
                continue;

            TraitSourceKind traitSourceKind =
                TraitContentRules.ToSourceKind(traitInstance.SourceType);
            if (
                traitSourceKind != TraitSourceKind.EquipmentFixed
                && traitSourceKind != TraitSourceKind.EquipmentRoll
            )
            {
                continue;
            }

            EquipmentEntryState equipmentEntry = FindEquipmentEntryByInstanceId(
                equipmentView,
                traitInstance.SourceId
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
                    traitInstance.TraitId,
                    traitSourceKind,
                    GetTraitCategories(traitInstance.TraitId, traitDefs),
                    sourceItem
                );
            if (matchedBindings.Count == 0)
                continue;

            sources.Add(
                new BattleEquipmentAbilitySourceState
                {
                    EffectiveInstanceKey = traitInstance.EffectiveInstanceKey,
                    EquipmentDefId = equipmentEntry.item_id,
                    SourceEquipmentInstanceId = equipmentEntry.instance_id,
                    SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                    AbilityIds = SortedBindingIds(matchedBindings),
                }
            );
            AddTemporalProgressModifiers(
                temporalProgressModifiers,
                matchedBindings,
                equipmentEntry.instance_id
            );
        }

        return new BattleEquipmentAbilityProjectionResult(
            sources,
            temporalProgressModifiers
        );
    }

    internal static BattleEquipmentAbilityProjectionResult
        ProjectEnemyBattleOnly(
        BattleUnitState unit,
        EnemyTemplateDefinition template,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindings,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        List<BattleEquipmentAbilitySourceState> sources =
            new();
        List<BattleTemporalProgressModifierState>
            temporalProgressModifiers = new();
        if (unit == null || template == null || bindings == null || bindings.Count == 0)
        {
            return new BattleEquipmentAbilityProjectionResult(
                sources,
                temporalProgressModifiers
            );
        }

        StringName attackEquipmentItemId = ProgressionDataUtils.to_string_name(
            template.AttackEquipmentItemId
        );
        if (attackEquipmentItemId == "")
        {
            return new BattleEquipmentAbilityProjectionResult(
                sources,
                temporalProgressModifiers
            );
        }

        ItemDefinition sourceItem = ResolveItemDefinition(
            attackEquipmentItemId,
            itemDefinitions
        );
        if (sourceItem == null)
        {
            return new BattleEquipmentAbilityProjectionResult(
                sources,
                temporalProgressModifiers
            );
        }

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

            sources.Add(
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
            AddTemporalProgressModifiers(
                temporalProgressModifiers,
                matchedBindings,
                ""
            );
        }

        return new BattleEquipmentAbilityProjectionResult(
            sources,
            temporalProgressModifiers
        );
    }

    internal static StringNameList ProjectCreatureTypeTags(EnemyTemplateDefinition template)
    {
        StringNameList result = new();
        HashSet<StringName> seen = new();
        if (template?.Tags == null)
            return result;
        foreach (StringName tag in template.Tags)
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
        return unit != null
            && normalized != ""
            && unit.HasCreatureTypeTag(normalized);
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
        List<BattleTemporalProgressModifierState>
            destination,
        IReadOnlyList<EquipmentAbilityBindingDefinition> bindings,
        StringName sourceEquipmentInstanceId
    )
    {
        if (destination == null || bindings == null)
            return;
        foreach (EquipmentAbilityBindingDefinition binding in bindings)
        {
            foreach (EquipmentTemporalProgressModifierDefinition modifier in binding?.TemporalProgressModifiers ?? Array.Empty<EquipmentTemporalProgressModifierDefinition>())
            {
                if (modifier == null || modifier.ModifierId == "")
                    continue;
                destination.Add(
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
