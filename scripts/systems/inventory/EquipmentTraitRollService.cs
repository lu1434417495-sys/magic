using System;
using System.Collections.Generic;
using Godot;

public class EquipmentTraitRollService : IDisposable
{
    private readonly List<TraitDef> _traitDefs;
    private RandomNumberGenerator _rng;
    private bool _ownsRng;
    private bool _disposed;
    private Func<int, int, int> _rollRange;
    private Func<float> _rollUnit;

    internal bool IsDisposedForTests => _disposed;
    internal bool OwnsRngForTests => _ownsRng;
    internal RandomNumberGenerator RngForTests => _rng;
    internal bool HasLiveOwnedRngForTests =>
        _ownsRng && _rng != null && GodotObject.IsInstanceValid(_rng);

    public EquipmentTraitRollService(
        IEnumerable<TraitDef> traitDefs,
        RandomNumberGenerator rng = null
    )
        : this(traitDefs, rng, false) { }

    public EquipmentTraitRollService(
        IEnumerable<TraitDef> traitDefs,
        RandomNumberGenerator rng,
        bool ownsRng
    )
    {
        _traitDefs = new List<TraitDef>();
        if (traitDefs != null)
        {
            foreach (TraitDef traitDef in traitDefs)
                if (traitDef != null)
                    _traitDefs.Add(traitDef);
        }
        ConfigureRng(rng, ownsRng);
    }

    public void ConfigureRng(RandomNumberGenerator rng = null)
    {
        ConfigureRng(rng, false);
    }

    public void SetOwnedRng(RandomNumberGenerator rng)
    {
        ConfigureRng(rng, true);
    }

    private void ConfigureRng(RandomNumberGenerator rng, bool ownsRng)
    {
        ThrowIfDisposed();
        if (rng != null && ReferenceEquals(_rng, rng))
        {
            _ownsRng = _ownsRng || ownsRng;
            _rollRange = _rng.RandiRange;
            _rollUnit = _rng.Randf;
            return;
        }

        DisposeOwnedRng();
        _rng = rng ?? new RandomNumberGenerator();
        _ownsRng = rng == null || ownsRng;
        if (rng == null)
            _rng.Randomize();
        _rollRange = _rng.RandiRange;
        _rollUnit = _rng.Randf;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        GC.SuppressFinalize(this);
        DisposeOwnedRng();
        _rollRange = null;
        _rollUnit = null;
    }

    public void SetRollHooksForTesting(Func<int, int, int> rollRange, Func<float> rollUnit)
    {
        ThrowIfDisposed();
        if (rollRange != null)
            _rollRange = rollRange;
        if (rollUnit != null)
            _rollUnit = rollUnit;
    }

    public void MintWithRolls(EquipmentInstanceState instance, ItemDef itemDef)
    {
        ThrowIfDisposed();
        if (instance == null || itemDef == null || instance.instance_id == "")
            return;

        List<TraitRollGroupDef> rollGroups = itemDef.GetTraitRollGroupsTyped();
        if (rollGroups.Count == 0)
            return;

        Godot.Collections.Array<TraitInstanceState> nextTraits = new();
        foreach (TraitRollGroupDef group in rollGroups)
        {
            foreach (TraitRollGroupEntryDef entry in RollGroup(group))
            {
                TraitDef traitDef = FindTraitDef(entry.trait_id);
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
        ThrowIfDisposed();
        if (instance == null)
            return false;
        foreach (TraitInstanceState trait in instance.trait_instances)
        {
            if (trait == null || trait.SourceKind != TraitSourceKind.EquipmentRoll)
                return false;
            TraitDef traitDef = FindTraitDef(trait.trait_id);
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
            TraitDef traitDef = FindTraitDef(entry.trait_id);
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

    private Godot.Collections.Array<TraitRollValueState> RollValuesFor(TraitDef traitDef)
    {
        Godot.Collections.Array<TraitRollValueState> values = new();
        foreach (TraitRollValueSchemaEntry schemaEntry in traitDef.roll_value_schema)
        {
            if (schemaEntry == null || schemaEntry.key == "")
                continue;
            switch (schemaEntry.ValueTypeKind)
            {
                case TraitRollValueType.Int:
                    values.Add(
                        TraitRollValueState.CreateInt(
                            schemaEntry.key,
                            _rollRange(schemaEntry.min_value, schemaEntry.max_value)
                        )
                    );
                    break;
                case TraitRollValueType.StringName:
                    if (schemaEntry.allowed_values.Count == 0)
                        break;
                    int index = _rollRange(0, schemaEntry.allowed_values.Count - 1);
                    values.Add(
                        TraitRollValueState.CreateStringName(
                            schemaEntry.key,
                            schemaEntry.allowed_values[index]
                        )
                    );
                    break;
                case TraitRollValueType.Bool:
                    values.Add(
                        TraitRollValueState.CreateBool(schemaEntry.key, _rollRange(0, 1) != 0)
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

    private TraitDef FindTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        foreach (TraitDef traitDef in _traitDefs)
            if (traitDef != null && traitDef.trait_id == normalizedTraitId)
                return traitDef;
        return null;
    }

    private void DisposeOwnedRng()
    {
        RandomNumberGenerator rng = _rng;
        bool shouldDispose = _ownsRng;
        _rng = null;
        _ownsRng = false;
        if (shouldDispose && rng != null && GodotObject.IsInstanceValid(rng))
        {
            GC.SuppressFinalize(rng);
            rng.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EquipmentTraitRollService));
    }
}
