using Godot;

public class BattleAttackCheckPolicyContext
{
    private BattleUnitState _attacker;
    private BattleUnitState _target;

    public BattleState battle_state { get; set; }

    // 规则计算一律读 *_view（唯一权威轨道）。State 引用仅保留给不纯消费方
    // （装备能力运行时需要可变单位）逃逸使用；footprint 由 BattleUnitState owner
    // 在 context 建立前归一化，setter 只同步引用与视图，避免 repeat stage 重复物化。
    public BattleUnitState attacker
    {
        get => _attacker;
        set
        {
            _attacker = value;
            attacker_view = value;
        }
    }

    public BattleUnitState target
    {
        get => _target;
        set
        {
            _target = value;
            target_view = value;
        }
    }

    internal BattleUnitReadView attacker_view { get; set; }
    internal BattleUnitReadView target_view { get; set; }
    internal SkillDefinition skill_definition { get; set; }
    public StringName roll_kind { get; set; } = "";
    public StringName check_route { get; set; } = "";
    public StringName trace_source { get; set; } = "";
    public int distance { get; set; } = -1;
    public bool force_hit_no_crit { get; set; }
    public Vector2I source_coord { get; set; } = new(-1, -1);
    public Vector2I target_coord { get; set; } = new(-1, -1);
    public BattleRepeatAttackStageSpec repeat_stage_spec;
    public bool has_repeat_stage_spec;
}
