using System.Collections.Generic;
using Godot;

public sealed class AttackContext
{
    private readonly Queue<int> _attackRollOverrides = new();

    public BattleState BattleState;
    public StringName SkillId = new("");
    public bool HasIsDisadvantage;
    public bool IsDisadvantage;
    public bool ForceHitNoCrit;
    public int AttackRollOverride;

    public AttackContext() { }

    public AttackContext(IEnumerable<int> attackRollOverrides)
    {
        if (attackRollOverrides == null)
        {
            return;
        }
        foreach (int roll in attackRollOverrides)
        {
            _attackRollOverrides.Enqueue(roll);
        }
    }

    public void AddAttackRollOverride(int roll)
    {
        _attackRollOverrides.Enqueue(roll);
    }

    public bool TryConsumeAttackRollOverride(int dieSize, out int roll)
    {
        int normalizedDieSize = Mathf.Max(dieSize, 1);
        if (_attackRollOverrides.Count > 0)
        {
            roll = Mathf.Clamp(_attackRollOverrides.Dequeue(), 1, normalizedDieSize);
            return true;
        }
        if (AttackRollOverride > 0)
        {
            roll = Mathf.Clamp(AttackRollOverride, 1, normalizedDieSize);
            AttackRollOverride = 0;
            return true;
        }
        roll = 0;
        return false;
    }
}
