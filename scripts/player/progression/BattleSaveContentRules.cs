using Godot;

internal enum BattleSaveDcMode
{
    Unknown = 0,
    Static,
    CasterSpell,
}

internal enum BattleSaveAdvantageStateKind
{
    Unknown = 0,
    Normal,
    Advantage,
    Disadvantage,
}

internal enum BattleSaveTagKind
{
    Unknown = 0,
    Sleep,
    Paralysis,
    Charm,
    Poison,
    DragonBreath,
    Fireball,
    ChainLightning,
    EquipmentDisjunction,
    Magic,
    Illusion,
    Frightened,
    Execute,
    Temporal,
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
}

internal enum BattleSaveAbilityKind
{
    Unknown = 0,
    Strength,
    Agility,
    Constitution,
    Perception,
    Intelligence,
    Willpower,
}

internal static class BattleSaveContentRules
{
    private static readonly StringName SaveTagSleep = "sleep";

    private static readonly StringName SaveTagParalysis = "paralysis";

    private static readonly StringName SaveTagCharm = "charm";

    private static readonly StringName SaveTagPoison = "poison";

    private static readonly StringName SaveTagDragonBreath = "dragon_breath";

    private static readonly StringName SaveTagFireball = "fireball";

    private static readonly StringName SaveTagChainLightning = "chain_lightning";

    private static readonly StringName SaveTagEquipmentDisjunction = "equipment_disjunction";

    private static readonly StringName SaveTagMagic = "magic";

    private static readonly StringName SaveTagIllusion = "illusion";

    private static readonly StringName SaveTagFrightened = "frightened";

    private static readonly StringName SaveTagExecute = "execute";

    private static readonly StringName SaveTagTemporal = "temporal";

    private static readonly StringName SaveTagStrength = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength);

    private static readonly StringName SaveTagAgility = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility);

    private static readonly StringName SaveTagConstitution = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution);

    private static readonly StringName SaveTagPerception = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception);

    private static readonly StringName SaveTagIntelligence = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence);

    private static readonly StringName SaveTagWillpower = UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower);

    private static readonly StringName AdvantageStateNormal = "normal";

    private static readonly StringName AdvantageStateAdvantage = "advantage";

    private static readonly StringName AdvantageStateDisadvantage = "disadvantage";

    private static readonly StringName SaveDcModeStatic = "static";

    private static readonly StringName SaveDcModeCasterSpell = "caster_spell";

    internal static bool IsValidSaveTag(StringName value) =>
        ToSaveTagKind(value) != BattleSaveTagKind.Unknown;

    internal static bool IsValidSaveAbility(StringName value) =>
        ToSaveAbilityKind(value) != BattleSaveAbilityKind.Unknown;

    internal static bool IsControlSaveTag(StringName value) =>
        ToSaveTagKind(value)
        is BattleSaveTagKind.Sleep
            or BattleSaveTagKind.Paralysis
            or BattleSaveTagKind.Charm
            or BattleSaveTagKind.Illusion
            or BattleSaveTagKind.Frightened
            or BattleSaveTagKind.Temporal;

    internal static bool IsValidSaveDcMode(StringName value) =>
        ToSaveDcMode(value) != BattleSaveDcMode.Unknown;

    internal static BattleSaveDcMode ToSaveDcMode(StringName value)
    {
        if (value == SaveDcModeStatic)
            return BattleSaveDcMode.Static;
        if (value == SaveDcModeCasterSpell)
            return BattleSaveDcMode.CasterSpell;
        return BattleSaveDcMode.Unknown;
    }

    internal static BattleSaveTagKind ToSaveTagKind(StringName value)
    {
        if (value == SaveTagSleep)
            return BattleSaveTagKind.Sleep;
        if (value == SaveTagParalysis)
            return BattleSaveTagKind.Paralysis;
        if (value == SaveTagCharm)
            return BattleSaveTagKind.Charm;
        if (value == SaveTagPoison)
            return BattleSaveTagKind.Poison;
        if (value == SaveTagDragonBreath)
            return BattleSaveTagKind.DragonBreath;
        if (value == SaveTagFireball)
            return BattleSaveTagKind.Fireball;
        if (value == SaveTagChainLightning)
            return BattleSaveTagKind.ChainLightning;
        if (value == SaveTagEquipmentDisjunction)
            return BattleSaveTagKind.EquipmentDisjunction;
        if (value == SaveTagMagic)
            return BattleSaveTagKind.Magic;
        if (value == SaveTagIllusion)
            return BattleSaveTagKind.Illusion;
        if (value == SaveTagFrightened)
            return BattleSaveTagKind.Frightened;
        if (value == SaveTagExecute)
            return BattleSaveTagKind.Execute;
        if (value == SaveTagTemporal)
            return BattleSaveTagKind.Temporal;
        if (value == SaveTagStrength)
            return BattleSaveTagKind.Strength;
        if (value == SaveTagAgility)
            return BattleSaveTagKind.Agility;
        if (value == SaveTagConstitution)
            return BattleSaveTagKind.Constitution;
        if (value == SaveTagPerception)
            return BattleSaveTagKind.Perception;
        if (value == SaveTagIntelligence)
            return BattleSaveTagKind.Intelligence;
        if (value == SaveTagWillpower)
            return BattleSaveTagKind.Willpower;
        return BattleSaveTagKind.Unknown;
    }

    internal static BattleSaveAbilityKind ToSaveAbilityKind(StringName value)
    {
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength))
            return BattleSaveAbilityKind.Strength;
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility))
            return BattleSaveAbilityKind.Agility;
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution))
            return BattleSaveAbilityKind.Constitution;
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Perception))
            return BattleSaveAbilityKind.Perception;
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Intelligence))
            return BattleSaveAbilityKind.Intelligence;
        if (value == UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Willpower))
            return BattleSaveAbilityKind.Willpower;
        return BattleSaveAbilityKind.Unknown;
    }

    internal static StringName ToStringName(BattleSaveDcMode mode)
    {
        return mode switch
        {
            BattleSaveDcMode.Static => SaveDcModeStatic,
            BattleSaveDcMode.CasterSpell => SaveDcModeCasterSpell,
            _ => "",
        };
    }

    internal static StringName ToStringName(BattleSaveAdvantageStateKind kind)
    {
        return kind switch
        {
            BattleSaveAdvantageStateKind.Normal => AdvantageStateNormal,
            BattleSaveAdvantageStateKind.Advantage => AdvantageStateAdvantage,
            BattleSaveAdvantageStateKind.Disadvantage => AdvantageStateDisadvantage,
            _ => "",
        };
    }

    internal static StringName ToStringName(BattleSaveTagKind kind)
    {
        return kind switch
        {
            BattleSaveTagKind.Sleep => SaveTagSleep,
            BattleSaveTagKind.Paralysis => SaveTagParalysis,
            BattleSaveTagKind.Charm => SaveTagCharm,
            BattleSaveTagKind.Poison => SaveTagPoison,
            BattleSaveTagKind.DragonBreath => SaveTagDragonBreath,
            BattleSaveTagKind.Fireball => SaveTagFireball,
            BattleSaveTagKind.ChainLightning => SaveTagChainLightning,
            BattleSaveTagKind.EquipmentDisjunction => SaveTagEquipmentDisjunction,
            BattleSaveTagKind.Magic => SaveTagMagic,
            BattleSaveTagKind.Illusion => SaveTagIllusion,
            BattleSaveTagKind.Frightened => SaveTagFrightened,
            BattleSaveTagKind.Execute => SaveTagExecute,
            BattleSaveTagKind.Temporal => SaveTagTemporal,
            BattleSaveTagKind.Strength => SaveTagStrength,
            BattleSaveTagKind.Agility => SaveTagAgility,
            BattleSaveTagKind.Constitution => SaveTagConstitution,
            BattleSaveTagKind.Perception => SaveTagPerception,
            BattleSaveTagKind.Intelligence => SaveTagIntelligence,
            BattleSaveTagKind.Willpower => SaveTagWillpower,
            _ => "",
        };
    }
}
