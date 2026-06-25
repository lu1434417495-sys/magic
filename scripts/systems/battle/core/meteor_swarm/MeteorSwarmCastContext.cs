using Godot;

internal sealed class MeteorSwarmCastContext
{
    public BattleUnitState active_unit { get; set; }
    public BattleCommand command { get; set; }
    public StringName skill_id { get; set; } = "mage_meteor_swarm";
    public MeteorSwarmProfile profile { get; set; }
    public Vector2I nominal_anchor_coord { get; set; } = new(-1, -1);
    public Vector2I final_anchor_coord { get; set; } = new(-1, -1);
    public BattleSpellControlResult spell_control_context { get; set; } =
        BattleSpellControlResult.None();
    public BattleGroundBacklashTargetResult drift_context { get; set; } =
        BattleGroundBacklashTargetResult.None();

    internal bool HasDrift()
    {
        return final_anchor_coord != nominal_anchor_coord;
    }
}
