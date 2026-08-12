using Godot;

[GlobalClass]
public partial class CombatDirectionalPiercingDef : Resource
{
    [Export]
    public int[] base_damage_percent_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public int successful_hit_decay_percent { get; set; } = 20;

    [Export]
    public int minimum_damage_percent { get; set; } = 40;

    [Export]
    public int stamina_flat_base { get; set; } = 32;

    [Export]
    public int stamina_range_square_coefficient { get; set; } = 1;

    [Export]
    public int stamina_strength_square_scale { get; set; } = 100;

    [Export]
    public int minimum_stamina_cost { get; set; } = 1;

    [Export]
    public int maximum_height_delta { get; set; } = 1;
}
