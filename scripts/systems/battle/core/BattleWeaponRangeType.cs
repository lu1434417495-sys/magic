using Godot;

internal enum BattleWeaponRangeTypeKind
{
    Unknown = 0,
    Melee,
    Ranged,
}

internal enum BattleAttackDeliveryKind
{
    Unknown = 0,
    MeleeWeapon,
    RangedWeapon,
    NonWeapon,
}

internal static class BattleWeaponRangeTypeNames
{
    private static readonly StringName Melee = "melee";
    private static readonly StringName Ranged = "ranged";

    internal static BattleWeaponRangeTypeKind Parse(StringName value)
    {
        if (value == Melee)
            return BattleWeaponRangeTypeKind.Melee;
        if (value == Ranged)
            return BattleWeaponRangeTypeKind.Ranged;
        return BattleWeaponRangeTypeKind.Unknown;
    }

    internal static StringName ToStringName(BattleWeaponRangeTypeKind value) =>
        value switch
        {
            BattleWeaponRangeTypeKind.Melee => Melee,
            BattleWeaponRangeTypeKind.Ranged => Ranged,
            _ => new StringName(""),
        };
}
