using Godot;

[GlobalClass]
public partial class RacialGrantedSkill : Resource
{
    public static readonly StringName CHARGE_KIND_AT_WILL = "at_will";
    public static readonly StringName CHARGE_KIND_PER_BATTLE = "per_battle";
    public static readonly StringName CHARGE_KIND_PER_TURN = "per_turn";
    public static readonly Godot.Collections.Array<StringName> VALID_CHARGE_KINDS = new() { CHARGE_KIND_AT_WILL, CHARGE_KIND_PER_BATTLE, CHARGE_KIND_PER_TURN };

    [Export] public StringName skill_id = "";
    [Export] public int minimum_skill_level = 1;
    [Export] public StringName charge_kind = CHARGE_KIND_PER_BATTLE;
    [Export] public int charges = 1;
}
