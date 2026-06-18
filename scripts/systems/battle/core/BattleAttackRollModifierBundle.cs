using System.Collections.Generic;
using Godot;

public class BattleAttackRollModifierBundle
{
    private readonly List<BattleAttackRollModifierSpec> _breakdown = new();

    public int TotalBonus { get; private set; }
    public int TotalPenalty { get; private set; }
    public IReadOnlyList<BattleAttackRollModifierSpec> Breakdown => _breakdown;

    public bool IsEmpty()
    {
        return TotalBonus == 0 && TotalPenalty == 0 && _breakdown.Count == 0;
    }

    public void AddSpec(BattleAttackRollModifierSpec spec)
    {
        if (spec == null || spec.modifier_delta == 0)
        {
            return;
        }
        _breakdown.Add(spec);
        if (spec.modifier_delta > 0)
        {
            TotalBonus += spec.modifier_delta;
        }
        else
        {
            TotalPenalty += Mathf.Abs(spec.modifier_delta);
        }
    }

    public int GetEffectiveModifierDelta()
    {
        return TotalBonus - TotalPenalty;
    }

    internal Godot.Collections.Array<Godot.Collections.Dictionary> BuildBreakdownPayload()
    {
        return BattleAttackRollModifierProjection.ProjectBreakdown(_breakdown);
    }
}
