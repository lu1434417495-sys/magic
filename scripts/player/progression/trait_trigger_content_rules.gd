class_name TraitTriggerContentRules
extends RefCounted

const TRIGGER_PASSIVE: StringName = &"passive"
const TRIGGER_ON_NATURAL_ONE: StringName = &"on_natural_one"
const TRIGGER_ON_CRIT: StringName = &"on_crit"
const TRIGGER_ON_FATAL_DAMAGE: StringName = &"on_fatal_damage"
const TRIGGER_ON_BATTLE_START: StringName = &"on_battle_start"
const TRIGGER_ON_TURN_START: StringName = &"on_turn_start"

const TRAIT_HALFLING_LUCK: StringName = &"halfling_luck"
const TRAIT_SAVAGE_ATTACKS: StringName = &"savage_attacks"
const TRAIT_RELENTLESS_ENDURANCE: StringName = &"relentless_endurance"

const VALID_TRIGGER_TYPES := {
	TRIGGER_PASSIVE: true,
	TRIGGER_ON_NATURAL_ONE: true,
	TRIGGER_ON_CRIT: true,
	TRIGGER_ON_FATAL_DAMAGE: true,
	TRIGGER_ON_BATTLE_START: true,
	TRIGGER_ON_TURN_START: true,
}

const DISPATCH_TRIGGER_TYPES := {
	TRAIT_HALFLING_LUCK: {
		TRIGGER_ON_NATURAL_ONE: "_handle_halfling_luck",
	},
	TRAIT_SAVAGE_ATTACKS: {
		TRIGGER_ON_CRIT: "_handle_savage_attacks",
	},
	TRAIT_RELENTLESS_ENDURANCE: {
		TRIGGER_ON_FATAL_DAMAGE: "_handle_relentless_endurance",
	},
}


static func has_dispatch_for_trait_trigger(trait_id: StringName, trigger_type: StringName) -> bool:
	if trait_id == &"" or trigger_type == &"":
		return false
	var dispatch_entry: Dictionary = DISPATCH_TRIGGER_TYPES.get(trait_id, {})
	return dispatch_entry.has(trigger_type)


static func get_dispatch_method_name(trait_id: StringName, trigger_type: StringName) -> String:
	if trait_id == &"" or trigger_type == &"":
		return ""
	var dispatch_entry: Dictionary = DISPATCH_TRIGGER_TYPES.get(trait_id, {})
	return String(dispatch_entry.get(trigger_type, ""))


static func get_dispatch_trait_ids() -> Array[StringName]:
	var trait_ids: Array[StringName] = []
	for trait_id_variant in DISPATCH_TRIGGER_TYPES.keys():
		var trait_id := StringName(trait_id_variant)
		if trait_id != &"":
			trait_ids.append(trait_id)
	trait_ids.sort()
	return trait_ids
