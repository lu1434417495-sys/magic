class_name RaceTraitDef
extends Resource

const EFFECT_DARKVISION: StringName = &"darkvision"
const EFFECT_SUPERIOR_DARKVISION: StringName = &"superior_darkvision"
const EFFECT_FEY_ANCESTRY: StringName = &"fey_ancestry"
const EFFECT_BRAVE: StringName = &"brave"
const EFFECT_HALFLING_LUCK: StringName = &"halfling_luck"
const EFFECT_SAVAGE_ATTACKS: StringName = &"savage_attacks"
const EFFECT_RELENTLESS_ENDURANCE: StringName = &"relentless_endurance"
const EFFECT_GNOME_CUNNING: StringName = &"gnome_cunning"
const EFFECT_DWARVEN_RESILIENCE: StringName = &"dwarven_resilience"
const EFFECT_DUERGAR_RESILIENCE: StringName = &"duergar_resilience"
const EFFECT_HUMAN_VERSATILITY: StringName = &"human_versatility"
const EFFECT_SMALL_BODY: StringName = &"small_body"
const EFFECT_FLEET_OF_FOOT: StringName = &"fleet_of_foot"
const EFFECT_DRAGON_BREATH: StringName = &"dragon_breath"
const EFFECT_RACIAL_SPELL_GRANT: StringName = &"racial_spell_grant"
const EFFECT_DAMAGE_RESISTANCE: StringName = &"damage_resistance"
const EFFECT_SAVE_ADVANTAGE: StringName = &"save_advantage"
const EFFECT_CIVIL_MILITIA: StringName = &"civil_militia"
const EFFECT_KEEN_SENSES: StringName = &"keen_senses"
const EFFECT_TRANCE: StringName = &"trance"
const EFFECT_ELVEN_WEAPON_TRAINING: StringName = &"elven_weapon_training"
const EFFECT_DROW_WEAPON_TRAINING: StringName = &"drow_weapon_training"
const EFFECT_DWARVEN_COMBAT_TRAINING: StringName = &"dwarven_combat_training"
const EFFECT_SHIELD_DWARF_ARMOR_TRAINING: StringName = &"shield_dwarf_armor_training"
const EFFECT_DWARVEN_TOUGHNESS: StringName = &"dwarven_toughness"
const EFFECT_MENACING: StringName = &"menacing"
const EFFECT_HALFLING_NIMBLENESS: StringName = &"halfling_nimbleness"
const EFFECT_NATURALLY_STEALTHY: StringName = &"naturally_stealthy"
const EFFECT_MASK_OF_THE_WILD: StringName = &"mask_of_the_wild"
const EFFECT_STONECUNNING: StringName = &"stonecunning"
const EFFECT_FOREST_GNOME_MAGIC: StringName = &"forest_gnome_magic"
const EFFECT_DEEP_GNOME_CAMOUFLAGE: StringName = &"deep_gnome_camouflage"
const EFFECT_ARTIFICERS_LORE: StringName = &"artificers_lore"
const EFFECT_DUERGAR_MAGIC: StringName = &"duergar_magic"
const EFFECT_GITHYANKI_MARTIAL_PRODIGY: StringName = &"githyanki_martial_prodigy"
const EFFECT_ASTRAL_KNOWLEDGE: StringName = &"astral_knowledge"
const EFFECT_GITHYANKI_PSIONICS: StringName = &"githyanki_psionics"
const EFFECT_INFERNAL_LEGACY: StringName = &"infernal_legacy"
const EFFECT_ASMODEUS_LEGACY: StringName = &"asmodeus_legacy"
const EFFECT_MEPHISTOPHELES_LEGACY: StringName = &"mephistopheles_legacy"
const EFFECT_ZARIEL_LEGACY: StringName = &"zariel_legacy"
const EFFECT_DROW_MAGIC: StringName = &"drow_magic"
const EFFECT_DRACONIC_ANCESTRY: StringName = &"draconic_ancestry"

const VALID_EFFECT_TYPES: Array[StringName] = [
	EFFECT_DARKVISION,
	EFFECT_SUPERIOR_DARKVISION,
	EFFECT_FEY_ANCESTRY,
	EFFECT_BRAVE,
	EFFECT_HALFLING_LUCK,
	EFFECT_SAVAGE_ATTACKS,
	EFFECT_RELENTLESS_ENDURANCE,
	EFFECT_GNOME_CUNNING,
	EFFECT_DWARVEN_RESILIENCE,
	EFFECT_DUERGAR_RESILIENCE,
	EFFECT_HUMAN_VERSATILITY,
	EFFECT_SMALL_BODY,
	EFFECT_FLEET_OF_FOOT,
	EFFECT_DRAGON_BREATH,
	EFFECT_RACIAL_SPELL_GRANT,
	EFFECT_DAMAGE_RESISTANCE,
	EFFECT_SAVE_ADVANTAGE,
	EFFECT_CIVIL_MILITIA,
	EFFECT_KEEN_SENSES,
	EFFECT_TRANCE,
	EFFECT_ELVEN_WEAPON_TRAINING,
	EFFECT_DROW_WEAPON_TRAINING,
	EFFECT_DWARVEN_COMBAT_TRAINING,
	EFFECT_SHIELD_DWARF_ARMOR_TRAINING,
	EFFECT_DWARVEN_TOUGHNESS,
	EFFECT_MENACING,
	EFFECT_HALFLING_NIMBLENESS,
	EFFECT_NATURALLY_STEALTHY,
	EFFECT_MASK_OF_THE_WILD,
	EFFECT_STONECUNNING,
	EFFECT_FOREST_GNOME_MAGIC,
	EFFECT_DEEP_GNOME_CAMOUFLAGE,
	EFFECT_ARTIFICERS_LORE,
	EFFECT_DUERGAR_MAGIC,
	EFFECT_GITHYANKI_MARTIAL_PRODIGY,
	EFFECT_ASTRAL_KNOWLEDGE,
	EFFECT_GITHYANKI_PSIONICS,
	EFFECT_INFERNAL_LEGACY,
	EFFECT_ASMODEUS_LEGACY,
	EFFECT_MEPHISTOPHELES_LEGACY,
	EFFECT_ZARIEL_LEGACY,
	EFFECT_DROW_MAGIC,
	EFFECT_DRACONIC_ANCESTRY,
]

@export var trait_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var trigger_type: StringName = &"passive"
@export var effect_type: StringName = &""
@export var params: Dictionary = {}
