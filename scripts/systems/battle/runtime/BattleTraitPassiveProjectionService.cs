using System.Collections.Generic;
using Godot;

internal static class BattleTraitPassiveProjectionService
{
    private static readonly StringName TraitPassiveStatusLayer = "trait_passive_status";

    internal static void ProjectEffectiveTraitPassives(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs
    )
    {
        if (
            unitState?.effective_trait_instances == null
            || traitDefs == null
            || traitDefs.Count == 0
        )
        {
            return;
        }

        ClearTraitPassiveStatuses(unitState);
        foreach (BattleEffectiveTraitInstanceState instance in unitState.effective_trait_instances)
        {
            StringName traitId = ProgressionDataUtils.to_string_name(instance?.trait_id ?? "");
            if (traitId == "" || !traitDefs.TryGetValue(traitId, out TraitDef traitDef) || traitDef == null)
                continue;

            ProjectSaveTags(unitState, traitDef);
            ProjectDamageResistances(unitState, traitDef);
            ProjectSaveBonuses(unitState, traitDef);
            ProjectPassiveStatuses(unitState, traitDef);
        }
    }

    private static void ClearTraitPassiveStatuses(BattleUnitState unitState)
    {
        List<StringName> toRemove = new();
        foreach (StringName statusId in unitState.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = unitState.GetStatusEffect(statusId);
            if (status != null && status.source_layer_id == TraitPassiveStatusLayer)
                toRemove.Add(statusId);
        }
        foreach (StringName statusId in toRemove)
            unitState.EraseStatusEffect(statusId);
    }

    private static void ProjectPassiveStatuses(BattleUnitState unitState, TraitDef traitDef)
    {
        if (unitState == null || traitDef?.passive_status_effects == null)
            return;

        foreach (TraitPassiveStatusEffectDef entry in traitDef.passive_status_effects)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(entry?.status_id ?? "");
            if (statusId == "")
                continue;
            BattleStatusEffectState status = new()
            {
                status_id = statusId,
                source_unit_id = unitState.unit_id,
                source_profile_id = traitDef.trait_id,
                source_layer_id = TraitPassiveStatusLayer,
                power = Mathf.Max(entry.power, 1),
                stacks = Mathf.Max(entry.stacks, 1),
                duration = -1,
                display_label = entry.display_label ?? "",
                undispellable = entry.undispellable,
                counts_as_debuff_override = entry.counts_as_debuff_override,
                counts_as_debuff = entry.counts_as_debuff,
                save_immunity_tags = BuildStringNameList(entry.save_immunity_tags),
            };
            unitState.SetStatusEffect(status);
        }
    }

    private static void ProjectSaveBonuses(BattleUnitState unitState, TraitDef traitDef)
    {
        if (unitState?.save_bonus_by_ability == null || traitDef?.save_bonus_entries == null)
            return;

        foreach (TraitSaveBonusEntryDef entry in traitDef.save_bonus_entries)
        {
            StringName saveAbility = ProgressionDataUtils.to_string_name(entry?.save_ability ?? "");
            int bonus = entry?.bonus ?? 0;
            if (saveAbility == "" || bonus == 0)
                continue;
            unitState.save_bonus_by_ability.TryGetValue(saveAbility, out int existing);
            unitState.save_bonus_by_ability.Put(saveAbility, existing + bonus);
        }
    }

    private static void ProjectSaveTags(BattleUnitState unitState, TraitDef traitDef)
    {
        if (unitState?.save_advantage_tags == null || traitDef?.save_advantage_tags == null)
            return;

        foreach (StringName rawTag in traitDef.save_advantage_tags)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawTag);
            if (tag == "" || unitState.save_advantage_tags.Contains(tag))
                continue;
            unitState.save_advantage_tags.Add(tag);
        }
    }

    private static void ProjectDamageResistances(BattleUnitState unitState, TraitDef traitDef)
    {
        if (
            unitState?.damage_resistances == null
            || traitDef?.damage_resistance_entries == null
        )
        {
            return;
        }

        foreach (TraitDamageResistanceEntryDef entry in traitDef.damage_resistance_entries)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(entry?.damage_tag ?? "");
            StringName mitigationTier = ProgressionDataUtils.to_string_name(entry?.mitigation_tier ?? "");
            if (damageTag == "" || mitigationTier == "")
                continue;

            if (
                !unitState.damage_resistances.TryGetValue(damageTag, out StringName existingTier)
                || IsStrongerMitigation(mitigationTier, existingTier)
            )
            {
                unitState.damage_resistances.Put(damageTag, mitigationTier);
            }
        }
    }

    private static bool IsStrongerMitigation(StringName candidate, StringName existing) =>
        MitigationRank(candidate) > MitigationRank(existing);

    private static int MitigationRank(StringName tier)
    {
        return DamageTagContentRules.ToMitigationTierKind(
            ProgressionDataUtils.to_string_name(tier)
        ) switch
        {
            DamageMitigationTierKind.Immune => 30,
            DamageMitigationTierKind.Half => 20,
            DamageMitigationTierKind.Normal => 10,
            DamageMitigationTierKind.Double => 0,
            _ => -1,
        };
    }

    private static List<StringName> BuildStringNameList(IEnumerable<StringName> values)
    {
        List<StringName> result = new();
        if (values == null)
            return result;
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized == "" || result.Contains(normalized))
                continue;
            result.Add(normalized);
        }
        return result;
    }
}
