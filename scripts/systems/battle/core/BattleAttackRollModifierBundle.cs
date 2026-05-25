using Godot;

[GlobalClass]
public partial class BattleAttackRollModifierBundle : RefCounted
{
    public int total_bonus { get; set; }
    public int total_penalty { get; set; }
    public Godot.Collections.Array<BattleAttackRollModifierSpec> breakdown { get; set; } = new();

    public bool is_empty()
    {
        return total_bonus == 0 && total_penalty == 0 && breakdown.Count == 0;
    }

    public void add_spec(BattleAttackRollModifierSpec spec)
    {
        if (spec == null || spec.modifier_delta == 0)
        {
            return;
        }
        breakdown.Add(spec);
        if (spec.modifier_delta > 0)
        {
            total_bonus += spec.modifier_delta;
        }
        else
        {
            total_penalty += Mathf.Abs(spec.modifier_delta);
        }
    }

    public int get_effective_modifier_delta()
    {
        return total_bonus - total_penalty;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_breakdown_payload()
    {
        var payloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (BattleAttackRollModifierSpec spec in breakdown)
        {
            if (spec == null)
            {
                continue;
            }
            payloads.Add(spec.to_dict_with_effective_modifier_delta(spec.modifier_delta));
        }
        return payloads;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary
        {
            ["total_bonus"] = total_bonus,
            ["total_penalty"] = total_penalty,
            ["effective_modifier_delta"] = get_effective_modifier_delta(),
            ["breakdown"] = get_breakdown_payload(),
        };
    }
}
