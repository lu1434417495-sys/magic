using Godot;

internal enum RaceTraitEffectKind
{
    Unknown = 0,
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

[GlobalClass]
public partial class RaceTraitDef : Resource
{
    private static readonly StringName EffectDarkvision = "darkvision";

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

    private static readonly StringName EffectDwarvenCombatTraining = "dwarven_combat_training";

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

    [Export]
    public StringName trait_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public StringName trigger_type { get; set; } = "passive";

    [Export]
    public StringName effect_type { get; set; } = "";

    internal RaceTraitEffectKind EffectKind
    {
        get => ToEffectKind(effect_type);
        set => effect_type = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Dictionary @params { get; set; } = new();

    internal static RaceTraitEffectKind ToEffectKind(StringName value)
    {
        if (value == EffectDarkvision)
            return RaceTraitEffectKind.Darkvision;
        if (value == EffectSuperiorDarkvision)
            return RaceTraitEffectKind.SuperiorDarkvision;
        if (value == EffectFeyAncestry)
            return RaceTraitEffectKind.FeyAncestry;
        if (value == EffectBrave)
            return RaceTraitEffectKind.Brave;
        if (value == EffectHalflingLuck)
            return RaceTraitEffectKind.HalflingLuck;
        if (value == EffectSavageAttacks)
            return RaceTraitEffectKind.SavageAttacks;
        if (value == EffectRelentlessEndurance)
            return RaceTraitEffectKind.RelentlessEndurance;
        if (value == EffectGnomeCunning)
            return RaceTraitEffectKind.GnomeCunning;
        if (value == EffectDwarvenResilience)
            return RaceTraitEffectKind.DwarvenResilience;
        if (value == EffectDuergarResilience)
            return RaceTraitEffectKind.DuergarResilience;
        if (value == EffectHumanVersatility)
            return RaceTraitEffectKind.HumanVersatility;
        if (value == EffectSmallBody)
            return RaceTraitEffectKind.SmallBody;
        if (value == EffectFleetOfFoot)
            return RaceTraitEffectKind.FleetOfFoot;
        if (value == EffectDragonBreath)
            return RaceTraitEffectKind.DragonBreath;
        if (value == EffectRacialSpellGrant)
            return RaceTraitEffectKind.RacialSpellGrant;
        if (value == EffectDamageResistance)
            return RaceTraitEffectKind.DamageResistance;
        if (value == EffectSaveAdvantage)
            return RaceTraitEffectKind.SaveAdvantage;
        if (value == EffectCivilMilitia)
            return RaceTraitEffectKind.CivilMilitia;
        if (value == EffectKeenSenses)
            return RaceTraitEffectKind.KeenSenses;
        if (value == EffectTrance)
            return RaceTraitEffectKind.Trance;
        if (value == EffectElvenWeaponTraining)
            return RaceTraitEffectKind.ElvenWeaponTraining;
        if (value == EffectDrowWeaponTraining)
            return RaceTraitEffectKind.DrowWeaponTraining;
        if (value == EffectDwarvenCombatTraining)
            return RaceTraitEffectKind.DwarvenCombatTraining;
        if (value == EffectShieldDwarfArmorTraining)
            return RaceTraitEffectKind.ShieldDwarfArmorTraining;
        if (value == EffectDwarvenToughness)
            return RaceTraitEffectKind.DwarvenToughness;
        if (value == EffectMenacing)
            return RaceTraitEffectKind.Menacing;
        if (value == EffectHalflingNimbleness)
            return RaceTraitEffectKind.HalflingNimbleness;
        if (value == EffectNaturallyStealthy)
            return RaceTraitEffectKind.NaturallyStealthy;
        if (value == EffectMaskOfTheWild)
            return RaceTraitEffectKind.MaskOfTheWild;
        if (value == EffectStonecunning)
            return RaceTraitEffectKind.Stonecunning;
        if (value == EffectForestGnomeMagic)
            return RaceTraitEffectKind.ForestGnomeMagic;
        if (value == EffectDeepGnomeCamouflage)
            return RaceTraitEffectKind.DeepGnomeCamouflage;
        if (value == EffectArtificersLore)
            return RaceTraitEffectKind.ArtificersLore;
        if (value == EffectDuergarMagic)
            return RaceTraitEffectKind.DuergarMagic;
        if (value == EffectGithyankiMartialProdigy)
            return RaceTraitEffectKind.GithyankiMartialProdigy;
        if (value == EffectAstralKnowledge)
            return RaceTraitEffectKind.AstralKnowledge;
        if (value == EffectGithyankiPsionics)
            return RaceTraitEffectKind.GithyankiPsionics;
        if (value == EffectInfernalLegacy)
            return RaceTraitEffectKind.InfernalLegacy;
        if (value == EffectAsmodeusLegacy)
            return RaceTraitEffectKind.AsmodeusLegacy;
        if (value == EffectMephistophelesLegacy)
            return RaceTraitEffectKind.MephistophelesLegacy;
        if (value == EffectZarielLegacy)
            return RaceTraitEffectKind.ZarielLegacy;
        if (value == EffectDrowMagic)
            return RaceTraitEffectKind.DrowMagic;
        if (value == EffectDraconicAncestry)
            return RaceTraitEffectKind.DraconicAncestry;
        return RaceTraitEffectKind.Unknown;
    }

    internal static StringName ToStringName(RaceTraitEffectKind kind)
    {
        return kind switch
        {
            RaceTraitEffectKind.Darkvision => EffectDarkvision,
            RaceTraitEffectKind.SuperiorDarkvision => EffectSuperiorDarkvision,
            RaceTraitEffectKind.FeyAncestry => EffectFeyAncestry,
            RaceTraitEffectKind.Brave => EffectBrave,
            RaceTraitEffectKind.HalflingLuck => EffectHalflingLuck,
            RaceTraitEffectKind.SavageAttacks => EffectSavageAttacks,
            RaceTraitEffectKind.RelentlessEndurance => EffectRelentlessEndurance,
            RaceTraitEffectKind.GnomeCunning => EffectGnomeCunning,
            RaceTraitEffectKind.DwarvenResilience => EffectDwarvenResilience,
            RaceTraitEffectKind.DuergarResilience => EffectDuergarResilience,
            RaceTraitEffectKind.HumanVersatility => EffectHumanVersatility,
            RaceTraitEffectKind.SmallBody => EffectSmallBody,
            RaceTraitEffectKind.FleetOfFoot => EffectFleetOfFoot,
            RaceTraitEffectKind.DragonBreath => EffectDragonBreath,
            RaceTraitEffectKind.RacialSpellGrant => EffectRacialSpellGrant,
            RaceTraitEffectKind.DamageResistance => EffectDamageResistance,
            RaceTraitEffectKind.SaveAdvantage => EffectSaveAdvantage,
            RaceTraitEffectKind.CivilMilitia => EffectCivilMilitia,
            RaceTraitEffectKind.KeenSenses => EffectKeenSenses,
            RaceTraitEffectKind.Trance => EffectTrance,
            RaceTraitEffectKind.ElvenWeaponTraining => EffectElvenWeaponTraining,
            RaceTraitEffectKind.DrowWeaponTraining => EffectDrowWeaponTraining,
            RaceTraitEffectKind.DwarvenCombatTraining => EffectDwarvenCombatTraining,
            RaceTraitEffectKind.ShieldDwarfArmorTraining => EffectShieldDwarfArmorTraining,
            RaceTraitEffectKind.DwarvenToughness => EffectDwarvenToughness,
            RaceTraitEffectKind.Menacing => EffectMenacing,
            RaceTraitEffectKind.HalflingNimbleness => EffectHalflingNimbleness,
            RaceTraitEffectKind.NaturallyStealthy => EffectNaturallyStealthy,
            RaceTraitEffectKind.MaskOfTheWild => EffectMaskOfTheWild,
            RaceTraitEffectKind.Stonecunning => EffectStonecunning,
            RaceTraitEffectKind.ForestGnomeMagic => EffectForestGnomeMagic,
            RaceTraitEffectKind.DeepGnomeCamouflage => EffectDeepGnomeCamouflage,
            RaceTraitEffectKind.ArtificersLore => EffectArtificersLore,
            RaceTraitEffectKind.DuergarMagic => EffectDuergarMagic,
            RaceTraitEffectKind.GithyankiMartialProdigy => EffectGithyankiMartialProdigy,
            RaceTraitEffectKind.AstralKnowledge => EffectAstralKnowledge,
            RaceTraitEffectKind.GithyankiPsionics => EffectGithyankiPsionics,
            RaceTraitEffectKind.InfernalLegacy => EffectInfernalLegacy,
            RaceTraitEffectKind.AsmodeusLegacy => EffectAsmodeusLegacy,
            RaceTraitEffectKind.MephistophelesLegacy => EffectMephistophelesLegacy,
            RaceTraitEffectKind.ZarielLegacy => EffectZarielLegacy,
            RaceTraitEffectKind.DrowMagic => EffectDrowMagic,
            RaceTraitEffectKind.DraconicAncestry => EffectDraconicAncestry,
            _ => "",
        };
    }
}
