using Godot;

[GlobalClass]
public partial class UnitReputationState : RefCounted
{
    public static readonly StringName MORALITY = "morality";

    public int morality;
    public Godot.Collections.Dictionary custom_states = new();

    public int get_reputation_value(StringName state_id)
    {
        if (state_id == MORALITY)
            return morality;
        return custom_states.ContainsKey(state_id) ? custom_states[state_id].AsInt32() : 0;
    }

    public void set_reputation_value(StringName state_id, int value)
    {
        if (state_id == MORALITY)
            morality = value;
        else
            custom_states[state_id] = value;
    }

    public UnitReputationState duplicate_state()
    {
        return new UnitReputationState
        {
            morality = morality,
            custom_states = custom_states?.Duplicate(true) ?? new Godot.Collections.Dictionary(),
        };
    }

    public Godot.Collections.Dictionary to_dict() =>
        new()
        {
            { "morality", morality },
            {
                "custom_states",
                ProgressionDataUtils.string_name_int_map_to_string_dict(custom_states)
            },
        };

    public static UnitReputationState from_dict(Godot.Collections.Dictionary data)
    {
        if (!_hfs(data, new Godot.Collections.Array<string> { "morality", "custom_states" }))
            return null;
        var csv = data["custom_states"];
        if (csv.VariantType != Variant.Type.Dictionary)
            return null;
        if (data["morality"].VariantType != Variant.Type.Int)
            return null;
        var pcs = _parse_int_map(csv.AsGodotDictionary());
        if (pcs == null)
            return null;
        return new UnitReputationState
        {
            morality = data["morality"].AsInt32(),
            custom_states = pcs,
        };
    }

    private static bool _hfs(Godot.Collections.Dictionary d, Godot.Collections.Array<string> f)
    {
        if (d.Count != f.Count)
            return false;
        foreach (string n in f)
            if (!d.ContainsKey(n))
                return false;
        return true;
    }

    private static Godot.Collections.Dictionary _parse_int_map(Godot.Collections.Dictionary v)
    {
        var p = new Godot.Collections.Dictionary();
        var s = new Godot.Collections.Dictionary();
        foreach (var r in v.Keys)
        {
            var kt = r.VariantType;
            if (kt != Variant.Type.String && kt != Variant.Type.StringName)
                return null;
            var pk = ProgressionDataUtils.to_string_name(r);
            if ((string)pk == "" || s.ContainsKey(pk))
                return null;
            if (v[r].VariantType != Variant.Type.Int)
                return null;
            s[pk] = true;
            p[pk] = v[r].AsInt32();
        }
        return p;
    }
}
