using Godot;

[GlobalClass]
public partial class CombatSequentialLineHitDef : Resource
{
    [Export]
    public int[] minimum_primary_distance_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public int[] continuation_range_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public int[] follow_up_attack_penalty_curve { get; set; } = System.Array.Empty<int>();
}
