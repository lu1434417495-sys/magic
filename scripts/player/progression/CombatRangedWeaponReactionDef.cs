using Godot;

[GlobalClass]
public partial class CombatRangedWeaponReactionDef : RefCounted
{
    [Export]
    public StringName readiness_status_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> trigger_weapon_families { get; set; } = new();

    [Export]
    public StringName damage_tag { get; set; } = "force";

    [Export]
    public StringName attack_defense_mode { get; set; } = "touch";

    [Export]
    public int[] attack_roll_bonus_by_skill_level { get; set; } = System.Array.Empty<int>();

    [Export]
    public int consume_status_stacks { get; set; } = 1;

    [Export]
    public bool trigger_on_hit { get; set; } = true;

    [Export]
    public bool trigger_on_miss { get; set; } = true;

    [Export]
    public bool allow_critical { get; set; }
}
