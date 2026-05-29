using Godot;

[GlobalClass]
public partial class RacialGrantedSkill : Resource
{
    private static readonly StringName ChargeKindAtWill = "at_will";
    private static readonly StringName ChargeKindPerBattle = "per_battle";
    private static readonly StringName ChargeKindPerTurn = "per_turn";

    [Export]
    public StringName skill_id = "";

    [Export]
    public int minimum_skill_level = 1;

    [Export]
    public StringName charge_kind = ChargeKindPerBattle;

    [Export]
    public int charges = 1;

    public static StringName CHARGE_KIND_AT_WILL() => ChargeKindAtWill;

    public static StringName CHARGE_KIND_PER_BATTLE() => ChargeKindPerBattle;

    public static StringName CHARGE_KIND_PER_TURN() => ChargeKindPerTurn;

    public static Godot.Collections.Array<StringName> VALID_CHARGE_KINDS()
    {
        return new Godot.Collections.Array<StringName>
        {
            ChargeKindAtWill,
            ChargeKindPerBattle,
            ChargeKindPerTurn,
        };
    }
}
