using Godot;

internal enum RacialSkillChargeKind
{
    Unknown = 0,
    AtWill,
    PerBattle,
    PerTurn,
}

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

    internal RacialSkillChargeKind ChargeKind
    {
        get => ToChargeKind(charge_kind);
        set => charge_kind = ToStringName(value);
    }

    [Export]
    public int charges = 1;

    internal static RacialSkillChargeKind ToChargeKind(StringName value)
    {
        if (value == ChargeKindAtWill)
            return RacialSkillChargeKind.AtWill;
        if (value == ChargeKindPerBattle)
            return RacialSkillChargeKind.PerBattle;
        if (value == ChargeKindPerTurn)
            return RacialSkillChargeKind.PerTurn;
        return RacialSkillChargeKind.Unknown;
    }

    internal static StringName ToStringName(RacialSkillChargeKind value)
    {
        return value switch
        {
            RacialSkillChargeKind.AtWill => ChargeKindAtWill,
            RacialSkillChargeKind.PerBattle => ChargeKindPerBattle,
            RacialSkillChargeKind.PerTurn => ChargeKindPerTurn,
            _ => "",
        };
    }
}
