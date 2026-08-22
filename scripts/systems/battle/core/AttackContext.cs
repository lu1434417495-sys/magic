using System.Collections.Generic;
using Godot;

public sealed class AttackContext
{
    private readonly Queue<int> _attackRollOverrides = new();
    private readonly List<int> _saveRollOverrides = new();

    public BattleState BattleState;
    public StringName SkillId = new("");
    public bool HasIsDisadvantage;
    public bool IsDisadvantage;
    public bool HasIsAdvantage;
    public bool IsAdvantage;
    public bool ForceHitNoCrit;
    public bool ForceHitAllowCrit;
    public BattleEventBatch EventBatch;
    public int AttackRollOverride;
    // 伤害生产者入口显式标注的 DamageOriginKind；默认 Unknown 即 fail-closed，
    // 主技能/攻击路径必须显式标 main_direct_effect，equipment/terrain 等来源各自标注。
    internal BattleDamageOriginKind DamageOriginKind = BattleDamageOriginKind.Unknown;
    public IReadOnlyList<int> SaveRollOverrides => _saveRollOverrides;

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

    public void AddSaveRollOverride(int roll)
    {
        _saveRollOverrides.Add(Mathf.Clamp(roll, 1, 20));
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
