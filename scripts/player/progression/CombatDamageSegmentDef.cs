using Godot;

[GlobalClass]
public sealed partial class CombatDamageSegmentDef : Resource
{
    [Export]
    public StringName damage_tag { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> damage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> mitigation_bypass_damage_tags { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<StringName> mitigation_bypass_tiers { get; set; } =
        new();

    [Export]
    public int power { get; set; }

    [Export]
    public int dice_count { get; set; }

    [Export]
    public int dice_sides { get; set; }

    [Export]
    public int dice_bonus { get; set; }

    [Export]
    public double pre_resistance_damage_multiplier { get; set; } = 1.0;
}
