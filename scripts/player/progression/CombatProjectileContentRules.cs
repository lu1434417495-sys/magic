using Godot;

internal enum CombatProjectileKind
{
    Unknown = 0,
    Inherit,
    None,
    Nonmagical,
    Magical,
    CurrentWeapon,
}

internal static class CombatProjectileContentRules
{
    internal static CombatProjectileKind ToProjectileKind(StringName value)
    {
        if (value == null || value == "")
            return CombatProjectileKind.Inherit;
        if (value == "none")
            return CombatProjectileKind.None;
        if (value == "nonmagical")
            return CombatProjectileKind.Nonmagical;
        if (value == "magical")
            return CombatProjectileKind.Magical;
        if (value == "current_weapon")
            return CombatProjectileKind.CurrentWeapon;
        return CombatProjectileKind.Unknown;
    }

    internal static StringName ToProjectileKindId(CombatProjectileKind value)
    {
        return value switch
        {
            CombatProjectileKind.Inherit => new StringName(""),
            CombatProjectileKind.None => new StringName("none"),
            CombatProjectileKind.Nonmagical => new StringName("nonmagical"),
            CombatProjectileKind.Magical => new StringName("magical"),
            CombatProjectileKind.CurrentWeapon => new StringName("current_weapon"),
            _ => new StringName(""),
        };
    }

    internal static CombatProjectileKind ResolveEffectiveKind(
        CombatProjectileKind baseKind,
        CombatProjectileKind overrideKind
    )
    {
        return overrideKind == CombatProjectileKind.Inherit ? baseKind : overrideKind;
    }

    internal static string ValidBaseKindLabel() =>
        "none, nonmagical, magical, or current_weapon";

    internal static string ValidOverrideKindLabel() =>
        "empty/inherit, none, nonmagical, magical, or current_weapon";
}
