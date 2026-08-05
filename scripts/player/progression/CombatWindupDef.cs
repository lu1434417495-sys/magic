using Godot;

[GlobalClass]
public partial class CombatWindupDef : Resource
{
    [Export]
    public int stamina_cost_per_tier { get; set; } = 6;

    [Export]
    public int weapon_dice_per_tier { get; set; } = 1;

    // 0 means the skill itself has no cap at that level. The natural
    // constitution cap still applies.
    [Export]
    public int[] skill_level_tier_caps { get; set; } = new[] { 1, 1, 2, 2, 3, 0 };

    [Export]
    public int[] base_weapon_dice_multipliers { get; set; } =
        new[] { 1, 1, 1, 1, 1, 2 };
}
