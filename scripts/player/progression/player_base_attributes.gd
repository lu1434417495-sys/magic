class_name PlayerBaseAttributes
extends RefCounted

const STRENGTH: StringName = &"strength"
const AGILITY: StringName = &"agility"
const CONSTITUTION: StringName = &"constitution"
const PERCEPTION: StringName = &"perception"
const INTELLIGENCE: StringName = &"intelligence"
const WILLPOWER: StringName = &"willpower"

const BASE_ATTRIBUTE_IDS := [
	STRENGTH,
	AGILITY,
	CONSTITUTION,
	PERCEPTION,
	INTELLIGENCE,
	WILLPOWER,
]

var strength := 0
var agility := 0
var constitution := 0
var perception := 0
var intelligence := 0
var willpower := 0
var custom_stats: Dictionary = {}


func get_attribute_value(attribute_id: StringName) -> int:
	match attribute_id:
		STRENGTH:
			return strength
		AGILITY:
			return agility
		CONSTITUTION:
			return constitution
		PERCEPTION:
			return perception
		INTELLIGENCE:
			return intelligence
		WILLPOWER:
			return willpower
		_:
			return int(custom_stats.get(attribute_id, 0))


func set_attribute_value(attribute_id: StringName, value: int) -> void:
	match attribute_id:
		STRENGTH:
			strength = value
		AGILITY:
			agility = value
		CONSTITUTION:
			constitution = value
		PERCEPTION:
			perception = value
		INTELLIGENCE:
			intelligence = value
		WILLPOWER:
			willpower = value
		_:
			custom_stats[attribute_id] = value


func get_all_base_attribute_ids() -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(BASE_ATTRIBUTE_IDS)


func to_dict() -> Dictionary:
	return {
		"strength": strength,
		"agility": agility,
		"constitution": constitution,
		"perception": perception,
		"intelligence": intelligence,
		"willpower": willpower,
		"custom_stats": ProgressionDataUtils.string_name_int_map_to_string_dict(custom_stats),
	}


static func from_dict(data: Dictionary) -> PlayerBaseAttributes:
	var attributes := PlayerBaseAttributes.new()
	attributes.strength = int(data.get("strength", 0))
	attributes.agility = int(data.get("agility", 0))
	attributes.constitution = int(data.get("constitution", 0))
	attributes.perception = int(data.get("perception", 0))
	attributes.intelligence = int(data.get("intelligence", 0))
	attributes.willpower = int(data.get("willpower", 0))
	attributes.custom_stats = ProgressionDataUtils.to_string_name_int_map(data.get("custom_stats", {}))
	return attributes
