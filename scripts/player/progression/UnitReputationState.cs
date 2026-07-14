using Godot;
using System;
using System.Collections.Generic;

internal enum UnitReputationKind
{
    Unknown = 0,
    Morality,
}

public class UnitReputationState
{
    private static readonly StringName Morality = "morality";

    public int morality;
    public UnitReputationMap custom_states { get; private set; } = new();

    public int GetReputationValue(StringName state_id)
    {
        if (ToReputationKind(state_id) == UnitReputationKind.Morality)
            return morality;
        return custom_states.Get(state_id);
    }

    public void SetReputationValue(StringName state_id, int value)
    {
        if (ToReputationKind(state_id) == UnitReputationKind.Morality)
            morality = value;
        else
            custom_states.Set(state_id, value);
    }

    public UnitReputationState DuplicateState()
    {
        return new UnitReputationState
        {
            morality = morality,
            custom_states = custom_states?.DuplicateState() ?? new UnitReputationMap(),
        };
    }

    public Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            { "morality", morality },
            {
                "custom_states",
                custom_states.ToDictionary()
            },
        };

    public static UnitReputationState FromDictionary(Godot.Collections.Dictionary data)
    {
        if (!_hfs(data, new[] { "morality", "custom_states" }))
            return null;
        var csv = data["custom_states"];
        if (csv.VariantType != Variant.Type.Dictionary)
            return null;
        if (data["morality"].VariantType != Variant.Type.Int)
            return null;
        UnitReputationMap pcs;
        try
        {
            pcs = UnitReputationMap.FromDictionary(csv.AsGodotDictionary());
        }
        catch (ArgumentException)
        {
            return null;
        }
        return new UnitReputationState
        {
            morality = data["morality"].AsInt32(),
            custom_states = pcs,
        };
    }

    private static bool _hfs(Godot.Collections.Dictionary d, IReadOnlyCollection<string> f)
    {
        if (d.Count != f.Count)
            return false;
        foreach (string n in f)
            if (!d.ContainsKey(n))
                return false;
        return true;
    }

    internal static StringName ToStringName(UnitReputationKind kind) =>
        kind == UnitReputationKind.Morality ? Morality : "";

    internal static UnitReputationKind ToReputationKind(StringName stateId) =>
        stateId == Morality ? UnitReputationKind.Morality : UnitReputationKind.Unknown;
}
