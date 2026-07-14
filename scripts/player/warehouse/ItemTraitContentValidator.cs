using System.Collections.Generic;
using Godot;

public static class ItemTraitContentValidator
{
    public static List<string> Validate(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        string contextPath = "item_defs"
    )
    {
        List<string> errors = new();
        if (itemDefs == null)
            return errors;

        foreach (KeyValuePair<StringName, ItemDefinition> kv in itemDefs)
        {
            ItemDefinition itemDef = kv.Value;
            if (itemDef == null)
            {
                errors.Add($"{contextPath}.{kv.Key} is null.");
                continue;
            }
            ValidateItem(
                itemDef,
                traitDefinitions ?? new Dictionary<StringName, TraitDefinition>(),
                $"{contextPath}.{itemDef.ItemId}",
                errors
            );
        }
        return errors;
    }

    private static void ValidateItem(
        ItemDefinition itemDef,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        string itemLabel,
        List<string> errors
    )
    {
        List<StringName> fixedTraitIds = itemDef.GetTraitIdsTyped();
        List<TraitRollGroupDefinition> rollGroups = itemDef.GetTraitRollGroupsTyped();
        bool hasTraitDeclarations = fixedTraitIds.Count > 0 || rollGroups.Count > 0;

        if (!itemDef.HasEquipmentCategory())
        {
            if (hasTraitDeclarations)
                errors.Add($"{itemLabel} declares equipment trait fields but is not equipment.");
            return;
        }

        ValidateFixedTraits(fixedTraitIds, traitDefinitions, itemLabel, errors);
        ValidateRollGroups(rollGroups, traitDefinitions, itemLabel, errors);
    }

    private static void ValidateFixedTraits(
        IReadOnlyList<StringName> fixedTraitIds,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
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
            if (
                !traitDefinitions.TryGetValue(
                    traitId,
                    out TraitDefinition traitDefinition
                )
                || traitDefinition == null
            )
            {
                errors.Add($"{traitLabel} references missing trait {traitId}.");
                continue;
            }
            if (
                !TraitContentRules.IsSourceKindAllowed(
                    traitDefinition,
                    TraitSourceKind.EquipmentFixed
                )
            )
            {
                errors.Add(
                    $"{traitLabel} trait {traitId} must allow equipment_fixed source."
                );
            }
        }
    }

    private static void ValidateRollGroups(
        IReadOnlyList<TraitRollGroupDefinition> rollGroups,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions,
        string itemLabel,
        List<string> errors
    )
    {
        for (int groupIndex = 0; groupIndex < rollGroups.Count; groupIndex++)
        {
            TraitRollGroupDefinition group = rollGroups[groupIndex];
            string groupLabel = $"{itemLabel}.trait_roll_groups[{groupIndex}]";
            if (group == null)
            {
                errors.Add($"{groupLabel} must be a TraitRollGroupDef.");
                continue;
            }
            if (group.GroupId == "")
                errors.Add($"{groupLabel}.group_id must be non-empty.");
            if (group.RollCount < 1)
                errors.Add($"{groupLabel}.roll_count must be >= 1.");

            int maxSatisfiableHits = CountMaxSatisfiableHits(group);
            if (group.RollCount > maxSatisfiableHits)
            {
                errors.Add(
                    $"{groupLabel} is unsatisfiable: roll_count {group.RollCount} exceeds max satisfiable hits {maxSatisfiableHits}."
                );
            }

            for (int entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
            {
                TraitRollGroupEntryDefinition entry = group.Entries[entryIndex];
                string entryLabel = $"{groupLabel}.entries[{entryIndex}]";
                if (entry == null)
                {
                    errors.Add($"{entryLabel} must be a TraitRollGroupEntryDef.");
                    continue;
                }
                if (entry.TraitId == "")
                {
                    errors.Add($"{entryLabel}.trait_id must be non-empty.");
                    continue;
                }
                if (entry.Weight <= 0)
                    errors.Add($"{entryLabel}.weight must be > 0.");
                if (
                    !traitDefinitions.TryGetValue(
                        entry.TraitId,
                        out TraitDefinition traitDefinition
                    )
                    || traitDefinition == null
                )
                {
                    errors.Add($"{entryLabel} references missing trait {entry.TraitId}.");
                    continue;
                }
                if (
                    !TraitContentRules.IsSourceKindAllowed(
                        traitDefinition,
                        TraitSourceKind.EquipmentRoll
                    )
                )
                {
                    errors.Add(
                        $"{entryLabel} trait {entry.TraitId} must allow equipment_roll source."
                    );
                }
                AppendRollSchemaErrors(traitDefinition, entryLabel, errors);
            }
        }
    }

    private static int CountMaxSatisfiableHits(TraitRollGroupDefinition group)
    {
        int count = 0;
        HashSet<StringName> exclusiveGroups = new();
        foreach (TraitRollGroupEntryDefinition entry in group.Entries)
        {
            if (entry == null)
                continue;
            if (entry.ExclusiveGroup == "")
            {
                count++;
                continue;
            }
            if (exclusiveGroups.Add(entry.ExclusiveGroup))
                count++;
        }
        return count;
    }

    private static void AppendRollSchemaErrors(
        TraitDefinition traitDefinition,
        string entryLabel,
        List<string> errors
    )
    {
        for (
            int schemaIndex = 0;
            schemaIndex < traitDefinition.RollValueSchema.Count;
            schemaIndex++
        )
        {
            TraitRollValueSchemaEntryDefinition schemaEntry =
                traitDefinition.RollValueSchema[schemaIndex];
            string schemaLabel = $"{entryLabel}.roll_value_schema[{schemaIndex}]";
            if (schemaEntry == null)
            {
                errors.Add($"{schemaLabel} must be a TraitRollValueSchemaEntryDefinition.");
                continue;
            }
            schemaEntry.AppendSchemaErrors(errors, schemaLabel);
        }
    }
}
