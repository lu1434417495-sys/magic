using Godot;

[GlobalClass]
public partial class CombatLineThroughAttackDef : RefCounted
{
    [Export]
    public int maximum_weapon_range { get; set; } = 2;

    [Export]
    public int intermediate_weapon_dice_multiplier { get; set; } = 1;

    [Export]
    public int[] primary_weapon_dice_multiplier_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public int[] primary_attack_roll_bonus_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public int successful_intermediate_hit_bonus_weapon_dice { get; set; } = 1;

    [Export]
    public int successful_intermediate_hit_attack_roll_bonus { get; set; } = 1;

    [Export]
    public int[] successful_intermediate_hit_bonus_cap_curve { get; set; } =
        System.Array.Empty<int>();
}
