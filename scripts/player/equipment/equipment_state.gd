class_name EquipmentState
extends RefCounted

const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

## 字段说明：缓存槽位到物品的映射，集中保存角色当前已装备的物品唯一标识。
var equipped_slots: Dictionary = {}


func get_equipped_item_id(slot_id: StringName) -> StringName:
	var normalized_slot_id := ProgressionDataUtils.to_string_name(slot_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(normalized_slot_id):
		return &""
	return ProgressionDataUtils.to_string_name(equipped_slots.get(normalized_slot_id, ""))


func set_equipped_item(slot_id: StringName, item_id: StringName) -> bool:
	var normalized_slot_id := ProgressionDataUtils.to_string_name(slot_id)
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(normalized_slot_id):
		return false
	if normalized_item_id == &"":
		equipped_slots.erase(normalized_slot_id)
		return true
	equipped_slots[normalized_slot_id] = normalized_item_id
	return true


func clear_slot(slot_id: StringName) -> void:
	set_equipped_item(slot_id, &"")


func get_equipped_count() -> int:
	return get_filled_slot_ids().size()


func get_filled_slot_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		if get_equipped_item_id(slot_id) == &"":
			continue
		result.append(slot_id)
	return result


func duplicate_state():
	return EQUIPMENT_STATE_SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	var slot_data: Dictionary = {}
	for slot_id in get_filled_slot_ids():
		slot_data[String(slot_id)] = String(get_equipped_item_id(slot_id))
	return {
		"equipped_slots": slot_data,
	}


static func from_dict(data: Variant):
	var state = EQUIPMENT_STATE_SCRIPT.new()
	if data is not Dictionary:
		return state

	var slot_data: Variant = data.get("equipped_slots", data)
	if slot_data is not Dictionary:
		return state

	for key in slot_data.keys():
		var slot_id := ProgressionDataUtils.to_string_name(key)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			continue

		var raw_value: Variant = slot_data.get(key, "")
		var item_id := &""
		if raw_value is Dictionary:
			item_id = ProgressionDataUtils.to_string_name(raw_value.get("item_id", ""))
		else:
			item_id = ProgressionDataUtils.to_string_name(raw_value)
		if item_id == &"":
			continue
		state.equipped_slots[slot_id] = item_id

	return state
