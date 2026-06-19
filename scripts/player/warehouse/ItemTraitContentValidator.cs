using System.Collections.Generic;
using Godot;

public static class ItemTraitContentValidator
{
    public static List<string> Validate(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        string contextPath = "item_defs"
    )
    {
        List<string> errors = new();
        if (itemDefs == null)
            return errors;

        foreach (KeyValuePair<StringName, ItemDef> kv in itemDefs)
        {
            ItemDef itemDef = kv.Value;
            if (itemDef == null)
            {
                errors.Add($"{contextPath}.{kv.Key} is null.");
                continue;
            }
            ValidateItem(
                itemDef,
                traitDefs ?? new Dictionary<StringName, TraitDef>(),
                $"{contextPath}.{itemDef.item_id}",
                errors
            );
        }
        return errors;
    }

    private static void ValidateItem(
        ItemDef itemDef,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        string itemLabel,
        List<string> errors
    )
    {
        List<StringName> fixedTraitIds = itemDef.GetTraitIdsTyped();
        List<TraitRollGroupDef> rollGroups = itemDef.GetTraitRollGroupsTyped();
        bool hasTraitDeclarations = fixedTraitIds.Count > 0 || rollGroups.Count > 0;

        if (!itemDef.HasEquipmentCategory())
        {
            if (hasTraitDeclarations)
                errors.Add($"{itemLabel} declares equipment trait fields but is not equipment.");
            return;
        }

        ValidateFixedTraits(fixedTraitIds, traitDefs, itemLabel, errors);
        ValidateRollGroups(rollGroups, traitDefs, itemLabel, errors);
    }

    private static void ValidateFixedTraits(
        IReadOnlyList<StringName> fixedTraitIds,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        string itemLabel,
        List<string> errors
    )
    {
        for (int index = 0; index < fixedTraitIds.Count; index++)
        {
            StringName traitId = fixedTraitIds[index];
            string traitLabel = $"{itemLabel}.trait_ids[{index}]";
            if (traitId == "")
            {
                errors.Add($"{traitLabel} must be non-empty.");
                continue;
            }
            if (!traitDefs.TryGetValue(traitId, out TraitDef traitDef) || traitDef == null)
            {
                errors.Add($"{traitLabel} references missing trait {traitId}.");
                continue;
            }
            if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.EquipmentFixed))
            {
                errors.Add(
                    $"{traitLabel} trait {traitId} must allow equipment_fixed source."
                );
            }
        }
    }

    private static void ValidateRollGroups(
        IReadOnlyList<TraitRollGroupDef> rollGroups,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        string itemLabel,
        List<string> errors
    )
    {
        for (int groupIndex = 0; groupIndex < rollGroups.Count; groupIndex++)
        {
            TraitRollGroupDef group = rollGroups[groupIndex];
            string groupLabel = $"{itemLabel}.trait_roll_groups[{groupIndex}]";
            if (group == null)
            {
                errors.Add($"{groupLabel} must be a TraitRollGroupDef.");
                continue;
            }
            if (group.group_id == "")
                errors.Add($"{groupLabel}.group_id must be non-empty.");
            if (group.roll_count < 1)
                errors.Add($"{groupLabel}.roll_count must be >= 1.");

            int maxSatisfiableHits = CountMaxSatisfiableHits(group);
            if (group.roll_count > maxSatisfiableHits)
            {
                errors.Add(
                    $"{groupLabel} is unsatisfiable: roll_count {group.roll_count} exceeds max satisfiable hits {maxSatisfiableHits}."
                );
            }

            for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
            {
                TraitRollGroupEntryDef entry = group.entries[entryIndex];
                string entryLabel = $"{groupLabel}.entries[{entryIndex}]";
                if (entry == null)
                {
                    errors.Add($"{entryLabel} must be a TraitRollGroupEntryDef.");
                    continue;
                }
                if (entry.trait_id == "")
                {
                    errors.Add($"{entryLabel}.trait_id must be non-empty.");
                    continue;
                }
                if (entry.weight <= 0)
                    errors.Add($"{entryLabel}.weight must be > 0.");
                if (!traitDefs.TryGetValue(entry.trait_id, out TraitDef traitDef) || traitDef == null)
                {
                    errors.Add($"{entryLabel} references missing trait {entry.trait_id}.");
                    continue;
                }
                if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.EquipmentRoll))
                {
                    errors.Add(
                        $"{entryLabel} trait {entry.trait_id} must allow equipment_roll source."
                    );
                }
                AppendRollSchemaErrors(traitDef, entryLabel, errors);
            }
        }
    }

    private static int CountMaxSatisfiableHits(TraitRollGroupDef group)
    {
        int count = 0;
        HashSet<StringName> exclusiveGroups = new();
        foreach (TraitRollGroupEntryDef entry in group.entries)
        {
            if (entry == null)
                continue;
            if (entry.exclusive_group == "")
            {
                count++;
                continue;
            }
            if (exclusiveGroups.Add(entry.exclusive_group))
                count++;
        }
        return count;
    }

    private static void AppendRollSchemaErrors(
        TraitDef traitDef,
        string entryLabel,
        List<string> errors
    )
    {
        for (int schemaIndex = 0; schemaIndex < traitDef.roll_value_schema.Count; schemaIndex++)
        {
            TraitRollValueSchemaEntry schemaEntry = traitDef.roll_value_schema[schemaIndex];
            string schemaLabel = $"{entryLabel}.roll_value_schema[{schemaIndex}]";
            if (schemaEntry == null)
            {
                errors.Add($"{schemaLabel} must be a TraitRollValueSchemaEntry.");
                continue;
            }
            schemaEntry.AppendSchemaErrors(errors, schemaLabel);
        }
    }
}
