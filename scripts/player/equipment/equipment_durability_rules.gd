class_name EquipmentDurabilityRules
extends RefCounted

const RARITY_COMMON := 0
const RARITY_UNCOMMON := 1
const RARITY_RARE := 2
const RARITY_EPIC := 3
const RARITY_LEGENDARY := 4

const MAX_DURABILITY_BY_RARITY := {
	RARITY_COMMON: 56,
	RARITY_UNCOMMON: 84,
	RARITY_RARE: 120,
	RARITY_EPIC: 160,
	RARITY_LEGENDARY: 200,
}

const DISJUNCTION_SAVE_BONUS_BY_RARITY := {
	RARITY_COMMON: 0,
	RARITY_UNCOMMON: 2,
	RARITY_RARE: 4,
	RARITY_EPIC: 6,
	RARITY_LEGENDARY: 8,
}


static func get_max_durability_for_rarity(rarity: int) -> int:
	return int(MAX_DURABILITY_BY_RARITY.get(rarity, MAX_DURABILITY_BY_RARITY[RARITY_COMMON]))


static func get_default_current_durability(rarity: int) -> int:
	return get_max_durability_for_rarity(rarity)


static func get_disjunction_save_bonus_for_rarity(rarity: int) -> int:
	return int(DISJUNCTION_SAVE_BONUS_BY_RARITY.get(rarity, 0))


static func is_valid_current_durability(value: int, rarity: int) -> bool:
	return value >= 1 and value <= get_max_durability_for_rarity(rarity)
