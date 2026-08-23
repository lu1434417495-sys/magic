using Godot;

[GlobalClass]
public partial class CombatSpellReactionDef : RefCounted
{
    [Export]
    public StringName trigger_delivery_category { get; set; } = "spell";

    [Export]
    public StringName reaction_skill_id { get; set; } = "basic_attack";

    [Export]
    public StringName readiness_status_id { get; set; } = "";

    [Export]
    public StringName required_weapon_family { get; set; } = "";

    [Export]
    public StringName save_ability { get; set; } = "constitution";

    [Export]
    public StringName save_tag { get; set; } = "spell_maintenance";

    [Export]
    public int base_save_dc { get; set; } = 10;

    [Export]
    public int hp_damage_divisor { get; set; } = 2;

    [Export]
    public int[] attack_roll_bonus_by_skill_level { get; set; } = System.Array.Empty<int>();

    [Export]
    public int[] save_dc_bonus_by_skill_level { get; set; } = System.Array.Empty<int>();

    [Export]
    public bool require_hp_damage { get; set; } = true;

    [Export]
    public bool consume_on_trigger { get; set; } = true;

    [Export]
    public bool expire_on_owner_turn_start { get; set; } = true;
}
