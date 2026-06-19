using Godot;

internal enum TraitEffectKind
{
    Unknown = 0,
    AttributeModifier,
    Darkvision,
    SuperiorDarkvision,
    FeyAncestry,
    Brave,
    HalflingLuck,
    SavageAttacks,
    RelentlessEndurance,
    GnomeCunning,
    DwarvenResilience,
    DuergarResilience,
    HumanVersatility,
    SmallBody,
    FleetOfFoot,
    DragonBreath,
    RacialSpellGrant,
    DamageResistance,
    SaveAdvantage,
    CivilMilitia,
    KeenSenses,
    Trance,
    ElvenWeaponTraining,
    DrowWeaponTraining,
    DwarvenCombatTraining,
    ShieldDwarfArmorTraining,
    DwarvenToughness,
    Menacing,
    HalflingNimbleness,
    NaturallyStealthy,
    MaskOfTheWild,
    Stonecunning,
    ForestGnomeMagic,
    DeepGnomeCamouflage,
    ArtificersLore,
    DuergarMagic,
    GithyankiMartialProdigy,
    AstralKnowledge,
    GithyankiPsionics,
    InfernalLegacy,
    AsmodeusLegacy,
    MephistophelesLegacy,
    ZarielLegacy,
    DrowMagic,
    DraconicAncestry,
}

internal enum TraitStackPolicyKind
{
    Unknown = 0,
    UniqueByTrait,
    HighestRoll,
    Additive,
    StackByInstance,
}

internal enum TraitSourceKind
{
    Unknown = 0,
    Identity,
    Character,
    EquipmentFixed,
    EquipmentRoll,
}

internal enum TraitRollValueType
{
    Unknown = 0,
    Int,
    StringName,
    Bool,
}

internal enum TraitChargeScopeKind
{
    Unknown = 0,
    None,
    PerTurn,
    PerBattle,
}

internal enum TraitChargeResetTimingKind
{
    Unknown = 0,
    None,
    BattleStart,
    TurnStart,
}

public static class TraitContentRules
{
    private static readonly StringName EffectDarkvision = "darkvision";
    private static readonly StringName EffectAttributeModifier = "attribute_modifier";
    private static readonly StringName EffectSuperiorDarkvision = "superior_darkvision";
    private static readonly StringName EffectFeyAncestry = "fey_ancestry";
    private static readonly StringName EffectBrave = "brave";
    private static readonly StringName EffectHalflingLuck = "halfling_luck";
    private static readonly StringName EffectSavageAttacks = "savage_attacks";
    private static readonly StringName EffectRelentlessEndurance = "relentless_endurance";
    private static readonly StringName EffectGnomeCunning = "gnome_cunning";
    private static readonly StringName EffectDwarvenResilience = "dwarven_resilience";
    private static readonly StringName EffectDuergarResilience = "duergar_resilience";
    private static readonly StringName EffectHumanVersatility = "human_versatility";
    private static readonly StringName EffectSmallBody = "small_body";
    private static readonly StringName EffectFleetOfFoot = "fleet_of_foot";
    private static readonly StringName EffectDragonBreath = "dragon_breath";
    private static readonly StringName EffectRacialSpellGrant = "racial_spell_grant";
    private static readonly StringName EffectDamageResistance = "damage_resistance";
    private static readonly StringName EffectSaveAdvantage = "save_advantage";
    private static readonly StringName EffectCivilMilitia = "civil_militia";
    private static readonly StringName EffectKeenSenses = "keen_senses";
    private static readonly StringName EffectTrance = "trance";
    private static readonly StringName EffectElvenWeaponTraining = "elven_weapon_training";
    private static readonly StringName EffectDrowWeaponTraining = "drow_weapon_training";
    private static readonly StringName EffectDwarvenCombatTraining =
        "dwarven_combat_training";
    private static readonly StringName EffectShieldDwarfArmorTraining =
        "shield_dwarf_armor_training";
    private static readonly StringName EffectDwarvenToughness = "dwarven_toughness";
    private static readonly StringName EffectMenacing = "menacing";
    private static readonly StringName EffectHalflingNimbleness = "halfling_nimbleness";
    private static readonly StringName EffectNaturallyStealthy = "naturally_stealthy";
    private static readonly StringName EffectMaskOfTheWild = "mask_of_the_wild";
    private static readonly StringName EffectStonecunning = "stonecunning";
    private static readonly StringName EffectForestGnomeMagic = "forest_gnome_magic";
    private static readonly StringName EffectDeepGnomeCamouflage = "deep_gnome_camouflage";
    private static readonly StringName EffectArtificersLore = "artificers_lore";
    private static readonly StringName EffectDuergarMagic = "duergar_magic";
    private static readonly StringName EffectGithyankiMartialProdigy =
        "githyanki_martial_prodigy";
    private static readonly StringName EffectAstralKnowledge = "astral_knowledge";
    private static readonly StringName EffectGithyankiPsionics = "githyanki_psionics";
    private static readonly StringName EffectInfernalLegacy = "infernal_legacy";
    private static readonly StringName EffectAsmodeusLegacy = "asmodeus_legacy";
    private static readonly StringName EffectMephistophelesLegacy = "mephistopheles_legacy";
    private static readonly StringName EffectZarielLegacy = "zariel_legacy";
    private static readonly StringName EffectDrowMagic = "drow_magic";
    private static readonly StringName EffectDraconicAncestry = "draconic_ancestry";

    private static readonly StringName StackUniqueByTrait = "unique_by_trait";
    private static readonly StringName StackHighestRoll = "highest_roll";
    private static readonly StringName StackAdditive = "additive";
    private static readonly StringName StackByInstance = "stack_by_instance";

    private static readonly StringName SourceIdentity = "identity";
    private static readonly StringName SourceCharacter = "character";
    private static readonly StringName SourceEquipmentFixed = "equipment_fixed";
    private static readonly StringName SourceEquipmentRoll = "equipment_roll";
    private static readonly StringName AttributeSourceTraitIdentity = "trait_identity";
    private static readonly StringName AttributeSourceTraitCharacter = "trait_character";
    private static readonly StringName AttributeSourceTraitEquipmentFixed = "trait_equipment_fixed";
    private static readonly StringName AttributeSourceTraitEquipmentRoll = "trait_equipment_roll";

    private static readonly StringName RollValueInt = "int";
    private static readonly StringName RollValueStringName = "string_name";
    private static readonly StringName RollValueBool = "bool";

    private static readonly StringName ChargeScopeNone = "none";
    private static readonly StringName ChargeScopePerTurn = "per_turn";
    private static readonly StringName ChargeScopePerBattle = "per_battle";

    private static readonly StringName ChargeResetNone = "none";
    private static readonly StringName ChargeResetBattleStart = "battle_start";
    private static readonly StringName ChargeResetTurnStart = "turn_start";

    internal static TraitEffectKind ToEffectKind(StringName value)
    {
        if (value == EffectAttributeModifier)
            return TraitEffectKind.AttributeModifier;
        if (value == EffectDarkvision)
            return TraitEffectKind.Darkvision;
        if (value == EffectSuperiorDarkvision)
            return TraitEffectKind.SuperiorDarkvision;
        if (value == EffectFeyAncestry)
            return TraitEffectKind.FeyAncestry;
        if (value == EffectBrave)
            return TraitEffectKind.Brave;
        if (value == EffectHalflingLuck)
            return TraitEffectKind.HalflingLuck;
        if (value == EffectSavageAttacks)
            return TraitEffectKind.SavageAttacks;
        if (value == EffectRelentlessEndurance)
            return TraitEffectKind.RelentlessEndurance;
        if (value == EffectGnomeCunning)
            return TraitEffectKind.GnomeCunning;
        if (value == EffectDwarvenResilience)
            return TraitEffectKind.DwarvenResilience;
        if (value == EffectDuergarResilience)
            return TraitEffectKind.DuergarResilience;
        if (value == EffectHumanVersatility)
            return TraitEffectKind.HumanVersatility;
        if (value == EffectSmallBody)
            return TraitEffectKind.SmallBody;
        if (value == EffectFleetOfFoot)
            return TraitEffectKind.FleetOfFoot;
        if (value == EffectDragonBreath)
            return TraitEffectKind.DragonBreath;
        if (value == EffectRacialSpellGrant)
            return TraitEffectKind.RacialSpellGrant;
        if (value == EffectDamageResistance)
            return TraitEffectKind.DamageResistance;
        if (value == EffectSaveAdvantage)
            return TraitEffectKind.SaveAdvantage;
        if (value == EffectCivilMilitia)
            return TraitEffectKind.CivilMilitia;
        if (value == EffectKeenSenses)
            return TraitEffectKind.KeenSenses;
        if (value == EffectTrance)
            return TraitEffectKind.Trance;
        if (value == EffectElvenWeaponTraining)
            return TraitEffectKind.ElvenWeaponTraining;
        if (value == EffectDrowWeaponTraining)
            return TraitEffectKind.DrowWeaponTraining;
        if (value == EffectDwarvenCombatTraining)
            return TraitEffectKind.DwarvenCombatTraining;
        if (value == EffectShieldDwarfArmorTraining)
            return TraitEffectKind.ShieldDwarfArmorTraining;
        if (value == EffectDwarvenToughness)
            return TraitEffectKind.DwarvenToughness;
        if (value == EffectMenacing)
            return TraitEffectKind.Menacing;
        if (value == EffectHalflingNimbleness)
            return TraitEffectKind.HalflingNimbleness;
        if (value == EffectNaturallyStealthy)
            return TraitEffectKind.NaturallyStealthy;
        if (value == EffectMaskOfTheWild)
            return TraitEffectKind.MaskOfTheWild;
        if (value == EffectStonecunning)
            return TraitEffectKind.Stonecunning;
        if (value == EffectForestGnomeMagic)
            return TraitEffectKind.ForestGnomeMagic;
        if (value == EffectDeepGnomeCamouflage)
            return TraitEffectKind.DeepGnomeCamouflage;
        if (value == EffectArtificersLore)
            return TraitEffectKind.ArtificersLore;
        if (value == EffectDuergarMagic)
            return TraitEffectKind.DuergarMagic;
        if (value == EffectGithyankiMartialProdigy)
            return TraitEffectKind.GithyankiMartialProdigy;
        if (value == EffectAstralKnowledge)
            return TraitEffectKind.AstralKnowledge;
        if (value == EffectGithyankiPsionics)
            return TraitEffectKind.GithyankiPsionics;
        if (value == EffectInfernalLegacy)
            return TraitEffectKind.InfernalLegacy;
        if (value == EffectAsmodeusLegacy)
            return TraitEffectKind.AsmodeusLegacy;
        if (value == EffectMephistophelesLegacy)
            return TraitEffectKind.MephistophelesLegacy;
        if (value == EffectZarielLegacy)
            return TraitEffectKind.ZarielLegacy;
        if (value == EffectDrowMagic)
            return TraitEffectKind.DrowMagic;
        if (value == EffectDraconicAncestry)
            return TraitEffectKind.DraconicAncestry;
        return TraitEffectKind.Unknown;
    }

    internal static StringName ToStringName(TraitEffectKind kind)
    {
        return kind switch
        {
            TraitEffectKind.AttributeModifier => EffectAttributeModifier,
            TraitEffectKind.Darkvision => EffectDarkvision,
            TraitEffectKind.SuperiorDarkvision => EffectSuperiorDarkvision,
            TraitEffectKind.FeyAncestry => EffectFeyAncestry,
            TraitEffectKind.Brave => EffectBrave,
            TraitEffectKind.HalflingLuck => EffectHalflingLuck,
            TraitEffectKind.SavageAttacks => EffectSavageAttacks,
            TraitEffectKind.RelentlessEndurance => EffectRelentlessEndurance,
            TraitEffectKind.GnomeCunning => EffectGnomeCunning,
            TraitEffectKind.DwarvenResilience => EffectDwarvenResilience,
            TraitEffectKind.DuergarResilience => EffectDuergarResilience,
            TraitEffectKind.HumanVersatility => EffectHumanVersatility,
            TraitEffectKind.SmallBody => EffectSmallBody,
            TraitEffectKind.FleetOfFoot => EffectFleetOfFoot,
            TraitEffectKind.DragonBreath => EffectDragonBreath,
            TraitEffectKind.RacialSpellGrant => EffectRacialSpellGrant,
            TraitEffectKind.DamageResistance => EffectDamageResistance,
            TraitEffectKind.SaveAdvantage => EffectSaveAdvantage,
            TraitEffectKind.CivilMilitia => EffectCivilMilitia,
            TraitEffectKind.KeenSenses => EffectKeenSenses,
            TraitEffectKind.Trance => EffectTrance,
            TraitEffectKind.ElvenWeaponTraining => EffectElvenWeaponTraining,
            TraitEffectKind.DrowWeaponTraining => EffectDrowWeaponTraining,
            TraitEffectKind.DwarvenCombatTraining => EffectDwarvenCombatTraining,
            TraitEffectKind.ShieldDwarfArmorTraining => EffectShieldDwarfArmorTraining,
            TraitEffectKind.DwarvenToughness => EffectDwarvenToughness,
            TraitEffectKind.Menacing => EffectMenacing,
            TraitEffectKind.HalflingNimbleness => EffectHalflingNimbleness,
            TraitEffectKind.NaturallyStealthy => EffectNaturallyStealthy,
            TraitEffectKind.MaskOfTheWild => EffectMaskOfTheWild,
            TraitEffectKind.Stonecunning => EffectStonecunning,
            TraitEffectKind.ForestGnomeMagic => EffectForestGnomeMagic,
            TraitEffectKind.DeepGnomeCamouflage => EffectDeepGnomeCamouflage,
            TraitEffectKind.ArtificersLore => EffectArtificersLore,
            TraitEffectKind.DuergarMagic => EffectDuergarMagic,
            TraitEffectKind.GithyankiMartialProdigy => EffectGithyankiMartialProdigy,
            TraitEffectKind.AstralKnowledge => EffectAstralKnowledge,
            TraitEffectKind.GithyankiPsionics => EffectGithyankiPsionics,
            TraitEffectKind.InfernalLegacy => EffectInfernalLegacy,
            TraitEffectKind.AsmodeusLegacy => EffectAsmodeusLegacy,
            TraitEffectKind.MephistophelesLegacy => EffectMephistophelesLegacy,
            TraitEffectKind.ZarielLegacy => EffectZarielLegacy,
            TraitEffectKind.DrowMagic => EffectDrowMagic,
            TraitEffectKind.DraconicAncestry => EffectDraconicAncestry,
            _ => "",
        };
    }

    internal static bool IsValidEffectType(StringName value)
    {
        return ToEffectKind(value) != TraitEffectKind.Unknown;
    }

    internal static TraitStackPolicyKind ToStackPolicyKind(StringName value)
    {
        if (value == StackUniqueByTrait)
            return TraitStackPolicyKind.UniqueByTrait;
        if (value == StackHighestRoll)
            return TraitStackPolicyKind.HighestRoll;
        if (value == StackAdditive)
            return TraitStackPolicyKind.Additive;
        if (value == StackByInstance)
            return TraitStackPolicyKind.StackByInstance;
        return TraitStackPolicyKind.Unknown;
    }

    internal static StringName ToStringName(TraitStackPolicyKind value)
    {
        return value switch
        {
            TraitStackPolicyKind.UniqueByTrait => StackUniqueByTrait,
            TraitStackPolicyKind.HighestRoll => StackHighestRoll,
            TraitStackPolicyKind.Additive => StackAdditive,
            TraitStackPolicyKind.StackByInstance => StackByInstance,
            _ => "",
        };
    }

    internal static bool IsValidStackPolicy(StringName value)
    {
        return ToStackPolicyKind(value) != TraitStackPolicyKind.Unknown;
    }

    internal static TraitSourceKind ToSourceKind(StringName value)
    {
        if (value == SourceIdentity)
            return TraitSourceKind.Identity;
        if (value == SourceCharacter)
            return TraitSourceKind.Character;
        if (value == SourceEquipmentFixed)
            return TraitSourceKind.EquipmentFixed;
        if (value == SourceEquipmentRoll)
            return TraitSourceKind.EquipmentRoll;
        return TraitSourceKind.Unknown;
    }

    internal static StringName ToStringName(TraitSourceKind value)
    {
        return value switch
        {
            TraitSourceKind.Identity => SourceIdentity,
            TraitSourceKind.Character => SourceCharacter,
            TraitSourceKind.EquipmentFixed => SourceEquipmentFixed,
            TraitSourceKind.EquipmentRoll => SourceEquipmentRoll,
            _ => "",
        };
    }

    internal static bool IsValidSourceType(StringName value)
    {
        return ToSourceKind(value) != TraitSourceKind.Unknown;
    }

    internal static bool IsSourceKindAllowed(TraitDef traitDef, TraitSourceKind sourceKind)
    {
        if (traitDef == null || sourceKind == TraitSourceKind.Unknown)
            return false;

        StringName expectedSource = ToStringName(sourceKind);
        foreach (StringName allowedSource in traitDef.allowed_source_kinds)
        {
            if (allowedSource == expectedSource)
                return true;
        }
        return false;
    }

    internal static StringName ToAttributeSourceType(TraitSourceKind value)
    {
        return value switch
        {
            TraitSourceKind.Identity => AttributeSourceTraitIdentity,
            TraitSourceKind.Character => AttributeSourceTraitCharacter,
            TraitSourceKind.EquipmentFixed => AttributeSourceTraitEquipmentFixed,
            TraitSourceKind.EquipmentRoll => AttributeSourceTraitEquipmentRoll,
            _ => "",
        };
    }

    internal static TraitRollValueType ToRollValueType(StringName value)
    {
        if (value == RollValueInt)
            return TraitRollValueType.Int;
        if (value == RollValueStringName)
            return TraitRollValueType.StringName;
        if (value == RollValueBool)
            return TraitRollValueType.Bool;
        return TraitRollValueType.Unknown;
    }

    internal static StringName ToStringName(TraitRollValueType value)
    {
        return value switch
        {
            TraitRollValueType.Int => RollValueInt,
            TraitRollValueType.StringName => RollValueStringName,
            TraitRollValueType.Bool => RollValueBool,
            _ => "",
        };
    }

    internal static bool IsValidRollValueType(StringName value)
    {
        return ToRollValueType(value) != TraitRollValueType.Unknown;
    }

    internal static TraitChargeScopeKind ToChargeScopeKind(StringName value)
    {
        if (value == ChargeScopeNone)
            return TraitChargeScopeKind.None;
        if (value == ChargeScopePerTurn)
            return TraitChargeScopeKind.PerTurn;
        if (value == ChargeScopePerBattle)
            return TraitChargeScopeKind.PerBattle;
        return TraitChargeScopeKind.Unknown;
    }

    internal static StringName ToStringName(TraitChargeScopeKind value)
    {
        return value switch
        {
            TraitChargeScopeKind.None => ChargeScopeNone,
            TraitChargeScopeKind.PerTurn => ChargeScopePerTurn,
            TraitChargeScopeKind.PerBattle => ChargeScopePerBattle,
            _ => "",
        };
    }

    internal static bool IsValidChargeScope(StringName value)
    {
        return ToChargeScopeKind(value) != TraitChargeScopeKind.Unknown;
    }

    internal static TraitChargeResetTimingKind ToChargeResetTimingKind(StringName value)
    {
        if (value == ChargeResetNone)
            return TraitChargeResetTimingKind.None;
        if (value == ChargeResetBattleStart)
            return TraitChargeResetTimingKind.BattleStart;
        if (value == ChargeResetTurnStart)
            return TraitChargeResetTimingKind.TurnStart;
        return TraitChargeResetTimingKind.Unknown;
    }

    internal static StringName ToStringName(TraitChargeResetTimingKind value)
    {
        return value switch
        {
            TraitChargeResetTimingKind.None => ChargeResetNone,
            TraitChargeResetTimingKind.BattleStart => ChargeResetBattleStart,
            TraitChargeResetTimingKind.TurnStart => ChargeResetTurnStart,
            _ => "",
        };
    }

    internal static bool IsValidChargeResetTiming(StringName value)
    {
        return ToChargeResetTimingKind(value) != TraitChargeResetTimingKind.Unknown;
    }
}
