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
]

@export var trait_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var trigger_type: StringName = &"passive"
@export var effect_type: StringName = &""
@export var params: Dictionary = {}
