using System.Collections.Generic;
using Godot;

internal static class BattleTraitPassiveProjectionService
{
    private static readonly StringName TraitPassiveStatusLayer = "trait_passive_status";

    internal static void ProjectEffectiveTraitPassives(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs
    )
    {
        BattleUnitEffectiveTraitReadView effectiveTraits =
            unitState?.GetEffectiveTraitsReadViewTyped()
            ?? BattleUnitEffectiveTraitReadView.MissingOwner;
        if (
            unitState == null
            || !effectiveTraits.OwnerPresent
            || !effectiveTraits.Instances.IsPresent
            || traitDefs == null
            || traitDefs.Count == 0
        )
        {
            return;
        }

        ClearTraitPassiveStatuses(unitState);
        foreach (
            BattleEffectiveTraitInstanceReadView instance
            in effectiveTraits.Instances
        )
        {
            StringName traitId = ProgressionDataUtils.to_string_name(
                instance.IsPresent ? instance.TraitId : ""
            );
            if (traitId == "" || !traitDefs.TryGetValue(traitId, out TraitDefinition traitDef) || traitDef == null)
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

    private static void ProjectPassiveStatuses(BattleUnitState unitState, TraitDefinition traitDef)
    {
        if (unitState == null || traitDef == null)
            return;

        foreach (TraitPassiveStatusEffectDefinition entry in traitDef.PassiveStatusEffects)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(entry?.StatusId ?? "");
            if (statusId == "")
                continue;
            BattleStatusEffectState status = new()
            {
                status_id = statusId,
                source_unit_id = unitState.unit_id,
                source_profile_id = traitDef.TraitId,
                source_layer_id = TraitPassiveStatusLayer,
                power = Mathf.Max(entry.Power, 1),
                stacks = Mathf.Max(entry.Stacks, 1),
                duration = -1,
                display_label = entry.DisplayLabel ?? "",
                undispellable = entry.Undispellable,
                counts_as_debuff_override = entry.CountsAsDebuffOverride,
                counts_as_debuff = entry.CountsAsDebuff,
                save_immunity_tags = BuildStringNameList(entry.SaveImmunityTags),
            };
            unitState.SetStatusEffect(status);
        }
    }

    private static void ProjectSaveBonuses(BattleUnitState unitState, TraitDefinition traitDef)
    {
        if (unitState == null || traitDef == null)
            return;

        foreach (TraitSaveBonusEntryDefinition entry in traitDef.SaveBonusEntries)
        {
            StringName saveAbility = ProgressionDataUtils.to_string_name(entry?.SaveAbility ?? "");
            int bonus = entry?.Bonus ?? 0;
            if (saveAbility == "" || bonus == 0)
                continue;
            unitState.AddSaveBonusByAbilityTyped(saveAbility, bonus);
        }
    }

    private static void ProjectSaveTags(BattleUnitState unitState, TraitDefinition traitDef)
    {
        if (unitState == null || traitDef == null)
            return;

        unitState.AppendSaveTagsTyped(
            traitDef.SaveAdvantageTags,
            traitDef.SaveDisadvantageTags,
            traitDef.SaveImmunityTags
        );
    }

    private static void ProjectDamageResistances(BattleUnitState unitState, TraitDefinition traitDef)
    {
        if (unitState == null || traitDef == null)
        {
            return;
        }

        foreach (TraitDamageResistanceEntryDefinition entry in traitDef.DamageResistanceEntries)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(entry?.DamageTag ?? "");
            StringName mitigationTier = ProgressionDataUtils.to_string_name(entry?.MitigationTier ?? "");
            if (damageTag == "" || mitigationTier == "")
                continue;

            if (
                !unitState.TryGetDamageResistanceTyped(
                    damageTag,
                    out StringName existingTier
                )
                || IsStrongerMitigation(mitigationTier, existingTier)
            )
            {
                unitState.SetDamageResistanceTyped(
                    damageTag,
                    mitigationTier
                );
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
