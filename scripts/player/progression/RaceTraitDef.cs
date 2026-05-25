using Godot;

[GlobalClass]
public partial class RaceTraitDef : Resource
{
    public static readonly StringName EFFECT_DARKVISION = "darkvision";
    public static readonly StringName EFFECT_SUPERIOR_DARKVISION = "superior_darkvision";
    public static readonly StringName EFFECT_FEY_ANCESTRY = "fey_ancestry";
    public static readonly StringName EFFECT_BRAVE = "brave";
    public static readonly StringName EFFECT_HALFLING_LUCK = "halfling_luck";
    public static readonly StringName EFFECT_SAVAGE_ATTACKS = "savage_attacks";
    public static readonly StringName EFFECT_RELENTLESS_ENDURANCE = "relentless_endurance";
    public static readonly StringName EFFECT_GNOME_CUNNING = "gnome_cunning";
    public static readonly StringName EFFECT_DWARVEN_RESILIENCE = "dwarven_resilience";
    public static readonly StringName EFFECT_DUERGAR_RESILIENCE = "duergar_resilience";
    public static readonly StringName EFFECT_HUMAN_VERSATILITY = "human_versatility";
    public static readonly StringName EFFECT_SMALL_BODY = "small_body";
    public static readonly StringName EFFECT_FLEET_OF_FOOT = "fleet_of_foot";
    public static readonly StringName EFFECT_DRAGON_BREATH = "dragon_breath";
    public static readonly StringName EFFECT_RACIAL_SPELL_GRANT = "racial_spell_grant";
    public static readonly StringName EFFECT_DAMAGE_RESISTANCE = "damage_resistance";
    public static readonly StringName EFFECT_SAVE_ADVANTAGE = "save_advantage";
    public static readonly StringName EFFECT_CIVIL_MILITIA = "civil_militia";
    public static readonly StringName EFFECT_KEEN_SENSES = "keen_senses";
    public static readonly StringName EFFECT_TRANCE = "trance";
    public static readonly StringName EFFECT_ELVEN_WEAPON_TRAINING = "elven_weapon_training";
    public static readonly StringName EFFECT_DROW_WEAPON_TRAINING = "drow_weapon_training";
    public static readonly StringName EFFECT_DWARVEN_COMBAT_TRAINING = "dwarven_combat_training";
    public static readonly StringName EFFECT_SHIELD_DWARF_ARMOR_TRAINING = "shield_dwarf_armor_training";
    public static readonly StringName EFFECT_DWARVEN_TOUGHNESS = "dwarven_toughness";
    public static readonly StringName EFFECT_MENACING = "menacing";
    public static readonly StringName EFFECT_HALFLING_NIMBLENESS = "halfling_nimbleness";
    public static readonly StringName EFFECT_NATURALLY_STEALTHY = "naturally_stealthy";
    public static readonly StringName EFFECT_MASK_OF_THE_WILD = "mask_of_the_wild";
    public static readonly StringName EFFECT_STONECUNNING = "stonecunning";
    public static readonly StringName EFFECT_FOREST_GNOME_MAGIC = "forest_gnome_magic";
    public static readonly StringName EFFECT_DEEP_GNOME_CAMOUFLAGE = "deep_gnome_camouflage";
    public static readonly StringName EFFECT_ARTIFICERS_LORE = "artificers_lore";
    public static readonly StringName EFFECT_DUERGAR_MAGIC = "duergar_magic";
    public static readonly StringName EFFECT_GITHYANKI_MARTIAL_PRODIGY = "githyanki_martial_prodigy";
    public static readonly StringName EFFECT_ASTRAL_KNOWLEDGE = "astral_knowledge";
    public static readonly StringName EFFECT_GITHYANKI_PSIONICS = "githyanki_psionics";
    public static readonly StringName EFFECT_INFERNAL_LEGACY = "infernal_legacy";
    public static readonly StringName EFFECT_ASMODEUS_LEGACY = "asmodeus_legacy";
    public static readonly StringName EFFECT_MEPHISTOPHELES_LEGACY = "mephistopheles_legacy";
    public static readonly StringName EFFECT_ZARIEL_LEGACY = "zariel_legacy";
    public static readonly StringName EFFECT_DROW_MAGIC = "drow_magic";
    public static readonly StringName EFFECT_DRACONIC_ANCESTRY = "draconic_ancestry";

    public static readonly Godot.Collections.Array<StringName> VALID_EFFECT_TYPES = new()
    {
        EFFECT_DARKVISION, EFFECT_SUPERIOR_DARKVISION, EFFECT_FEY_ANCESTRY, EFFECT_BRAVE, EFFECT_HALFLING_LUCK,
        EFFECT_SAVAGE_ATTACKS, EFFECT_RELENTLESS_ENDURANCE, EFFECT_GNOME_CUNNING, EFFECT_DWARVEN_RESILIENCE,
        EFFECT_DUERGAR_RESILIENCE, EFFECT_HUMAN_VERSATILITY, EFFECT_SMALL_BODY, EFFECT_FLEET_OF_FOOT,
        EFFECT_DRAGON_BREATH, EFFECT_RACIAL_SPELL_GRANT, EFFECT_DAMAGE_RESISTANCE, EFFECT_SAVE_ADVANTAGE,
        EFFECT_CIVIL_MILITIA, EFFECT_KEEN_SENSES, EFFECT_TRANCE, EFFECT_ELVEN_WEAPON_TRAINING, EFFECT_DROW_WEAPON_TRAINING,
        EFFECT_DWARVEN_COMBAT_TRAINING, EFFECT_SHIELD_DWARF_ARMOR_TRAINING, EFFECT_DWARVEN_TOUGHNESS, EFFECT_MENACING,
        EFFECT_HALFLING_NIMBLENESS, EFFECT_NATURALLY_STEALTHY, EFFECT_MASK_OF_THE_WILD, EFFECT_STONECUNNING,
        EFFECT_FOREST_GNOME_MAGIC, EFFECT_DEEP_GNOME_CAMOUFLAGE, EFFECT_ARTIFICERS_LORE, EFFECT_DUERGAR_MAGIC,
        EFFECT_GITHYANKI_MARTIAL_PRODIGY, EFFECT_ASTRAL_KNOWLEDGE, EFFECT_GITHYANKI_PSIONICS, EFFECT_INFERNAL_LEGACY,
        EFFECT_ASMODEUS_LEGACY, EFFECT_MEPHISTOPHELES_LEGACY, EFFECT_ZARIEL_LEGACY, EFFECT_DROW_MAGIC, EFFECT_DRACONIC_ANCESTRY,
    };

    [Export] public StringName trait_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";
    [Export] public StringName trigger_type { get; set; } = "passive";
    [Export] public StringName effect_type { get; set; } = "";
    [Export] public Godot.Collections.Dictionary @params { get; set; } = new();
}
