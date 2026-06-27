using Godot;

public class BattleAttackCheckPolicyContext
{
    public BattleState battle_state { get; set; }
    public BattleUnitState attacker { get; set; }
    public BattleUnitState target { get; set; }
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
    internal bool HasReadView => attacker_view.IsValid || target_view.IsValid;
}
