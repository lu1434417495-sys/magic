using System.Collections.Generic;
using Godot;

internal sealed class EffectiveTraitInstance
{
    public StringName TraitId;
    public TraitDef TraitDef;
    public TraitInstanceState TraitInstance;
    public TraitSourceKind SourceKind;
    public StringName SourceId;
    public StringName EffectiveInstanceKey;
    public TraitStackPolicyKind StackPolicy;
    public TraitChargeScopeKind ChargeScope;
    public TraitChargeResetTimingKind ChargeResetTiming;
    public int Rank = 1;
    public int Stacks = 1;
    public Godot.Collections.Array<TraitRollValueState> RollValues = new();
}

internal sealed class EffectiveTraitSet
{
    private readonly List<EffectiveTraitInstance> _instances;

    public EffectiveTraitSet(List<EffectiveTraitInstance> instances)
    {
        _instances = instances ?? new List<EffectiveTraitInstance>();
    }

    public IReadOnlyList<EffectiveTraitInstance> Instances => _instances;

    public bool TryGetByKey(StringName key, out EffectiveTraitInstance instance)
    {
        StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
        foreach (EffectiveTraitInstance candidate in _instances)
        {
            if (candidate == null || candidate.EffectiveInstanceKey != normalizedKey)
                continue;
            instance = candidate;
            return true;
        }
        instance = null;
        return false;
    }

    public IReadOnlyList<EffectiveTraitInstance> GetByTraitId(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        List<EffectiveTraitInstance> result = new();
        foreach (EffectiveTraitInstance instance in _instances)
        {
            if (instance == null || instance.TraitId != normalizedTraitId)
                continue;
            result.Add(instance);
        }
        return result;
    }

    public IReadOnlyList<StringName> DeriveTraitIds()
    {
        List<StringName> ids = new();
        foreach (EffectiveTraitInstance instance in _instances)
        {
            if (instance == null || instance.TraitId == "" || Contains(ids, instance.TraitId))
                continue;
            ids.Add(instance.TraitId);
        }
        ids.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        return ids;
    }

    public List<BattleEffectiveTraitInstanceState> ToBattleEffectiveInstances()
    {
        List<EffectiveTraitInstance> sorted = new(_instances);
        sorted.Sort(
            (a, b) =>
                string.CompareOrdinal(
                    a?.EffectiveInstanceKey.ToString() ?? "",
                    b?.EffectiveInstanceKey.ToString() ?? ""
                )
        );

        List<BattleEffectiveTraitInstanceState> result = new();
        foreach (EffectiveTraitInstance instance in sorted)
        {
            if (instance == null || instance.TraitDef == null)
                continue;

            result.Add(
                new BattleEffectiveTraitInstanceState
                {
                    trait_id = instance.TraitId,
                    effective_instance_key = instance.EffectiveInstanceKey,
                    source_type = TraitContentRules.ToStringName(instance.SourceKind),
                    source_id = instance.SourceId,
                    effect_type = instance.TraitDef.effect_type,
                    trigger_type = instance.TraitDef.trigger_type,
                    charge_scope = instance.TraitDef.charge_scope,
                    charge_reset_timing = instance.TraitDef.charge_reset_timing,
                    rank = Mathf.Max(instance.Rank, 1),
                    stacks = Mathf.Max(instance.Stacks, 1),
                    roll_values = TraitInstanceState.NormalizeRollValues(instance.RollValues),
                }
            );
        }
        return result;
    }

    private static bool Contains(List<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }
}
