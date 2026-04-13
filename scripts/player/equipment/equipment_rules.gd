class_name EquipmentRules
extends RefCounted

const MAIN_HAND: StringName = &"main_hand"
const OFF_HAND: StringName = &"off_hand"
const HEAD: StringName = &"head"
const BODY: StringName = &"body"
const ACCESSORY_1: StringName = &"accessory_1"
const ACCESSORY_2: StringName = &"accessory_2"

const SLOT_ORDER := [
	MAIN_HAND,
	OFF_HAND,
	HEAD,
	BODY,
	ACCESSORY_1,
	ACCESSORY_2,
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
		ACCESSORY_1:
			return "饰品一"
		ACCESSORY_2:
			return "饰品二"
		_:
			return String(slot_id)
