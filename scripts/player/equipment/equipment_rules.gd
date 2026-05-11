class_name EquipmentRules
extends RefCounted

const MAIN_HAND: StringName = &"main_hand"
const OFF_HAND: StringName = &"off_hand"
const HEAD: StringName = &"head"
const BODY: StringName = &"body"
const HANDS: StringName = &"hands"
const FEET: StringName = &"feet"
const CLOAK: StringName = &"cloak"
const NECKLACE: StringName = &"necklace"
const RING_1: StringName = &"ring_1"
const RING_2: StringName = &"ring_2"
const SPECIAL_TRINKET: StringName = &"special_trinket"
const BADGE: StringName = &"badge"

const SLOT_ORDER := [
	MAIN_HAND,
	OFF_HAND,
	HEAD,
	BODY,
	HANDS,
	FEET,
	CLOAK,
	NECKLACE,
	RING_1,
	RING_2,
	SPECIAL_TRINKET,
	BADGE,
]


static func get_all_slot_ids() -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(SLOT_ORDER)


static func is_valid_slot(slot_id: StringName) -> bool:
	return SLOT_ORDER.has(ProgressionDataUtils.to_string_name(slot_id))


static func normalize_slot_ids(values: Variant) -> Array[StringName]:
	var normalized: Array[StringName] = []
	var seen: Dictionary = {}
	for raw_value in ProgressionDataUtils.to_string_name_array(values):
		if not is_valid_slot(raw_value):
			continue
		if seen.has(raw_value):
			continue
		seen[raw_value] = true
		normalized.append(raw_value)
	return normalized


static func get_slot_label(slot_id: StringName) -> String:
	match ProgressionDataUtils.to_string_name(slot_id):
		MAIN_HAND:
			return "主手"
		OFF_HAND:
			return "副手"
		HEAD:
			return "头部"
		BODY:
			return "身躯"
		HANDS:
			return "手部"
		FEET:
			return "脚部"
		CLOAK:
			return "披风"
		NECKLACE:
			return "项链"
		RING_1:
			return "戒指一"
		RING_2:
			return "戒指二"
		SPECIAL_TRINKET:
			return "特殊饰品"
		BADGE:
			return "徽章"
		_:
			return String(slot_id)
