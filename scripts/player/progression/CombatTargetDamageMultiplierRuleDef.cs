using Godot;

[GlobalClass]
public sealed partial class CombatTargetDamageMultiplierRuleDef : Resource
{
    [Export]
    public Godot.Collections.Array<StringName> any_creature_type_tags { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<StringName> all_creature_type_tags { get; set; } =
        new();

    [Export]
    public Godot.Collections.Array<StringName> excluded_creature_type_tags { get; set; } =
        new();

    [Export]
    public int multiplier_percent { get; set; } = 100;
}
