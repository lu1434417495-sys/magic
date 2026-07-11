using System;
using System.Collections.Generic;
using Godot;

public class EquipmentTraitRollService : IDisposable
{
    private readonly List<TraitDefinition> _traitDefs;
    private RuntimeRandom _runtimeRng;
    private Func<int, int, int> _rollRange;
    private Func<float> _rollUnit;

    public EquipmentTraitRollService(IEnumerable<TraitDefinition> traitDefs)
    {
        _traitDefs = new List<TraitDefinition>();
        if (traitDefs != null)
        {
            foreach (TraitDefinition traitDef in traitDefs)
                if (traitDef != null)
                    _traitDefs.Add(traitDef);
        }
        ConfigureRng();
    }

    public void ConfigureRng()
    {
        _runtimeRng = new RuntimeRandom(TrueRandomSeedService.GenerateSeed());
        _rollRange = _runtimeRng.RandiRange;
        _rollUnit = _runtimeRng.Randf;
    }

    public void Dispose()
    {
        _runtimeRng = null;
        _rollRange = null;
        _rollUnit = null;
    }

    public void SetRollHooksForTesting(Func<int, int, int> rollRange, Func<float> rollUnit)
    {
        if (rollRange != null)
            _rollRange = rollRange;
        if (rollUnit != null)
            _rollUnit = rollUnit;
    }

    public void MintWithRolls(EquipmentInstanceState instance, ItemDef itemDef)
    {
        if (instance == null || itemDef == null || instance.instance_id == "")
            return;

        List<TraitRollGroupDef> rollGroups = itemDef.GetTraitRollGroupsTyped();
        if (rollGroups.Count == 0)
            return;

        List<TraitInstanceState> nextTraits = new();
        foreach (TraitRollGroupDef group in rollGroups)
        {
            foreach (TraitRollGroupEntryDef entry in RollGroup(group))
            {
                TraitDefinition traitDef = FindTraitDef(entry.trait_id);
                if (traitDef == null)
                    continue;
                nextTraits.Add(
                    TraitInstanceState.Create(
                        BuildTraitInstanceId(instance.instance_id, nextTraits.Count + 1),
                        entry.trait_id,
                        TraitSourceKind.EquipmentRoll,
                        instance.instance_id,
                        rollValues: RollValuesFor(traitDef)
                    )
                );
            }
        }

        instance.trait_instances = nextTraits;
    }

    public bool ValidateRehydrated(EquipmentInstanceState instance)
    {
        if (instance == null)
            return false;
        foreach (TraitInstanceState trait in instance.trait_instances)
        {
            if (trait == null || trait.SourceKind != TraitSourceKind.EquipmentRoll)
                return false;
            TraitDefinition traitDef = FindTraitDef(trait.trait_id);
            if (traitDef == null)
                return false;
            if (trait.ValidateAgainstDef(traitDef).Length > 0)
                return false;
        }
        return true;
    }

    private List<TraitRollGroupEntryDef> RollGroup(TraitRollGroupDef group)
    {
        List<TraitRollGroupEntryDef> hits = new();
        if (group == null || group.roll_count < 1)
            return hits;

        int maxSatisfiableHits = CountMaxSatisfiableHits(group);
        if (group.roll_count > maxSatisfiableHits)
        {
            GameLog.Warning(
                $"Equipment trait roll group {group.group_id} is unsatisfiable: roll_count {group.roll_count} exceeds max satisfiable hits {maxSatisfiableHits}.",
                "equipment_traits.unsatisfiable_roll_group",
                "equipment"
            );
            return hits;
        }

        List<TraitRollGroupEntryDef> candidates = BuildValidCandidates(group);
        for (int rollIndex = 0; rollIndex < group.roll_count && candidates.Count > 0; rollIndex++)
        {
            TraitRollGroupEntryDef picked = WeightedPick(candidates);
            if (picked == null)
                break;
            hits.Add(picked);
            RemovePickedAndExclusivePeers(candidates, picked);
        }
        return hits;
    }

    private List<TraitRollGroupEntryDef> BuildValidCandidates(TraitRollGroupDef group)
    {
        List<TraitRollGroupEntryDef> candidates = new();
        foreach (TraitRollGroupEntryDef entry in group.entries)
        {
            if (entry == null || entry.trait_id == "" || entry.weight <= 0)
                continue;
            TraitDefinition traitDef = FindTraitDef(entry.trait_id);
            if (traitDef == null)
                continue;
            if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.EquipmentRoll))
                continue;
            candidates.Add(entry);
        }
        return candidates;
    }

    private TraitRollGroupEntryDef WeightedPick(List<TraitRollGroupEntryDef> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int totalWeight = 0;
        foreach (TraitRollGroupEntryDef entry in candidates)
            totalWeight += Mathf.Max(entry.weight, 0);
        if (totalWeight <= 0)
            return null;

        float roll = Mathf.Clamp(_rollUnit(), 0.0f, 0.999999f) * totalWeight;
        int cumulative = 0;
        foreach (TraitRollGroupEntryDef entry in candidates)
        {
            cumulative += Mathf.Max(entry.weight, 0);
            if (roll < cumulative)
                return entry;
        }
        return candidates[candidates.Count - 1];
    }

    private static void RemovePickedAndExclusivePeers(
        List<TraitRollGroupEntryDef> candidates,
        TraitRollGroupEntryDef picked
    )
    {
        if (picked.exclusive_group == "")
        {
            candidates.Remove(picked);
            return;
        }
        candidates.RemoveAll(entry => entry.exclusive_group == picked.exclusive_group);
    }

    private List<TraitRollValueState> RollValuesFor(TraitDefinition traitDef)
    {
        List<TraitRollValueState> values = new();
        foreach (TraitRollValueSchemaEntryDefinition schemaEntry in traitDef.RollValueSchema)
        {
            if (schemaEntry == null || schemaEntry.Key == "")
                continue;
            switch (schemaEntry.ValueTypeKind)
            {
                case TraitRollValueType.Int:
                    values.Add(
                        TraitRollValueState.CreateInt(
                            schemaEntry.Key,
                            _rollRange(schemaEntry.MinValue, schemaEntry.MaxValue)
                        )
                    );
                    break;
                case TraitRollValueType.StringName:
                    if (schemaEntry.AllowedValues.Count == 0)
                        break;
                    int index = _rollRange(0, schemaEntry.AllowedValues.Count - 1);
                    values.Add(
                        TraitRollValueState.CreateStringName(
                            schemaEntry.Key,
                            schemaEntry.AllowedValues[index]
                        )
                    );
                    break;
                case TraitRollValueType.Bool:
                    values.Add(
                        TraitRollValueState.CreateBool(schemaEntry.Key, _rollRange(0, 1) != 0)
                    );
                    break;
            }
        }
        return values;
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

    private static StringName BuildTraitInstanceId(StringName equipmentInstanceId, int ordinal) =>
        $"{equipmentInstanceId}_t{Mathf.Max(ordinal, 1):D2}";

    private TraitDefinition FindTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        foreach (TraitDefinition traitDef in _traitDefs)
            if (traitDef != null && traitDef.TraitId == normalizedTraitId)
                return traitDef;
        return null;
    }
}
