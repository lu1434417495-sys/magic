using Godot;

internal enum CombatWeaponRangePolicy
{
    Unknown = 0,
    CurrentWeapon,
    Configured,
    CurrentWeaponPlusConfigured,
}

internal static class CombatWeaponRangePolicyRules
{
    private static readonly StringName CurrentWeapon = "current_weapon";
    private static readonly StringName Configured = "configured";
    private static readonly StringName CurrentWeaponPlusConfigured =
        "current_weapon_plus_configured";

    internal static CombatWeaponRangePolicy ToPolicy(StringName value)
    {
        if (value == "" || value == CurrentWeapon)
            return CombatWeaponRangePolicy.CurrentWeapon;
        if (value == Configured)
            return CombatWeaponRangePolicy.Configured;
        if (value == CurrentWeaponPlusConfigured)
            return CombatWeaponRangePolicy.CurrentWeaponPlusConfigured;
        return CombatWeaponRangePolicy.Unknown;
    }

    internal static StringName ToStringName(CombatWeaponRangePolicy policy)
    {
        return policy switch
        {
            CombatWeaponRangePolicy.CurrentWeapon => CurrentWeapon,
            CombatWeaponRangePolicy.Configured => Configured,
            CombatWeaponRangePolicy.CurrentWeaponPlusConfigured =>
                CurrentWeaponPlusConfigured,
            _ => "",
        };
    }
}
