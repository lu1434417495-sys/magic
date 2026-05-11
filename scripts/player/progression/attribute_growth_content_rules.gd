class_name AttributeGrowthContentRules
extends RefCounted

const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const VALID_GROWTH_TIERS := {
	&"basic": 60,
	&"intermediate": 120,
	&"advanced": 180,
	&"ultimate": 240,
}


static func get_tier_budget(growth_tier: StringName) -> int:
	return int(VALID_GROWTH_TIERS.get(growth_tier, 0))


static func is_valid_growth_tier(growth_tier: StringName) -> bool:
	return VALID_GROWTH_TIERS.has(growth_tier)


static func is_valid_attribute_id(attribute_id: StringName) -> bool:
	return UNIT_BASE_ATTRIBUTES_SCRIPT.BASE_ATTRIBUTE_IDS.has(attribute_id)
