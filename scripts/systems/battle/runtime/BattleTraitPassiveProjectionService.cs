using System.Collections.Generic;
using Godot;

internal static class BattleTraitPassiveProjectionService
{
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

        foreach (BattleEffectiveTraitInstanceState instance in unitState.effective_trait_instances)
        {
            StringName traitId = ProgressionDataUtils.to_string_name(instance?.trait_id ?? "");
            if (traitId == "" || !traitDefs.TryGetValue(traitId, out TraitDef traitDef) || traitDef == null)
                continue;

            ProjectSaveTags(unitState, traitDef);
            ProjectDamageResistances(unitState, traitDef);
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
}
