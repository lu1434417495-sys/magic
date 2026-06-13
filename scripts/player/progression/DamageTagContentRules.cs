using Godot;

internal enum DamageTagKind
{
    Unknown = 0,
    PhysicalSlash,
    PhysicalPierce,
    PhysicalBlunt,
    Fire,
    Freeze,
    Lightning,
    NegativeEnergy,
    Force,
    Psychic,
    Radiant,
    Thunder,
    Magic,
    Acid,
    Poison,
}

internal enum DamageMitigationTierKind
{
    Unknown = 0,
    Normal,
    Half,
    Double,
    Immune,
}

internal enum DamageCategoryKind
{
    Unknown = 0,
    Physical,
    Spell,
    Magic,
    Energy,
}

public static class DamageTagContentRules
{
    private static readonly StringName DamageTagPhysicalSlash = "physical_slash";

    private static readonly StringName DamageTagPhysicalPierce = "physical_pierce";

    private static readonly StringName DamageTagPhysicalBlunt = "physical_blunt";

    internal static DamageTagKind ToDamageTagKind(StringName value)
    {
        if (value == DamageTagPhysicalSlash)
            return DamageTagKind.PhysicalSlash;
        if (value == DamageTagPhysicalPierce)
            return DamageTagKind.PhysicalPierce;
        if (value == DamageTagPhysicalBlunt)
            return DamageTagKind.PhysicalBlunt;
        return value.ToString() switch
        {
            "fire" => DamageTagKind.Fire,
            "freeze" => DamageTagKind.Freeze,
            "lightning" => DamageTagKind.Lightning,
            "negative_energy" => DamageTagKind.NegativeEnergy,
            "force" => DamageTagKind.Force,
            "psychic" => DamageTagKind.Psychic,
            "radiant" => DamageTagKind.Radiant,
            "thunder" => DamageTagKind.Thunder,
            "magic" => DamageTagKind.Magic,
            "acid" => DamageTagKind.Acid,
            "poison" => DamageTagKind.Poison,
            _ => DamageTagKind.Unknown,
        };
    }

    internal static DamageMitigationTierKind ToMitigationTierKind(StringName value)
    {
        return value.ToString() switch
        {
            "normal" => DamageMitigationTierKind.Normal,
            "half" => DamageMitigationTierKind.Half,
            "double" => DamageMitigationTierKind.Double,
            "immune" => DamageMitigationTierKind.Immune,
            _ => DamageMitigationTierKind.Unknown,
        };
    }

    internal static DamageCategoryKind ToDamageCategoryKind(StringName value)
    {
        return value.ToString() switch
        {
            "physical" => DamageCategoryKind.Physical,
            "spell" => DamageCategoryKind.Spell,
            "magic" => DamageCategoryKind.Magic,
            "energy" => DamageCategoryKind.Energy,
            _ => DamageCategoryKind.Unknown,
        };
    }

    internal static bool IsPhysicalDamageTag(DamageTagKind kind)
    {
        return kind
            is DamageTagKind.PhysicalSlash
                or DamageTagKind.PhysicalPierce
                or DamageTagKind.PhysicalBlunt;
    }

    public static string ValidDamageTagLabel()
    {
        return "acid, fire, force, freeze, lightning, magic, negative_energy, physical_blunt, physical_pierce, physical_slash, poison, psychic, radiant, thunder";
    }

    public static string ValidMitigationTierLabel()
    {
        return "double, half, immune, normal";
    }

    public static string ValidDamageCategoryLabel()
    {
        return "energy, magic, physical, spell";
    }
}
