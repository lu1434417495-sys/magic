using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MeteorSwarmCastContext : RefCounted
{
    public BattleUnitState active_unit { get; set; }
    public BattleCommand command { get; set; }
    public SkillDef skill_def { get; set; }
    public CombatCastVariantDef cast_variant { get; set; }
    public MeteorSwarmProfile profile { get; set; }
    public Vector2I nominal_anchor_coord { get; set; } = new(-1, -1);
    public Vector2I final_anchor_coord { get; set; } = new(-1, -1);
    public GDictionary spell_control_context { get; set; } = new();
    public GDictionary drift_context { get; set; } = new();

    public bool has_drift()
    {
        return final_anchor_coord != nominal_anchor_coord;
    }
}
