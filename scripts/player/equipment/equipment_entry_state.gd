class_name EquipmentEntryState
extends RefCounted

const SCRIPT = preload("res://scripts/player/equipment/equipment_entry_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")

var item_id: StringName = &""
var occupied_slot_ids: Array[StringName] = []
var instance_id: StringName = &""


func is_empty() -> bool:
	return item_id == &""


func duplicate_state() -> EquipmentEntryState:
	return SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	return {
		"item_id": String(item_id),
		"occupied_slot_ids": ProgressionDataUtils.string_name_array_to_string_array(occupied_slot_ids),
		"instance_id": String(instance_id),
	}


static func from_dict(data: Variant, fallback_slot_id: StringName = &"") -> EquipmentEntryState:
	if data is not Dictionary:
		return null
	var occupied_variant: Variant = data.get("occupied_slot_ids", null)
	if occupied_variant is not Array:
		return null
	var entry := SCRIPT.new()
	entry.item_id = ProgressionDataUtils.to_string_name(data.get("item_id", ""))
	entry.instance_id = ProgressionDataUtils.to_string_name(data.get("instance_id", ""))
	for raw_slot_id in occupied_variant:
		var slot_id := ProgressionDataUtils.to_string_name(raw_slot_id)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			continue
		if entry.occupied_slot_ids.has(slot_id):
			continue
		entry.occupied_slot_ids.append(slot_id)
	if entry.item_id == &"" or entry.occupied_slot_ids.is_empty():
		return null
	return entry
