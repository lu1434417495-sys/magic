class_name EquipmentEntryState
extends RefCounted

const SCRIPT = preload("res://scripts/player/equipment/equipment_entry_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

var item_id: StringName = &""
var occupied_slot_ids: Array[StringName] = []
var instance_id: StringName = &""
var equipment_instance = null


func is_empty() -> bool:
	return equipment_instance == null or item_id == &"" or instance_id == &""


func get_equipment_instance():
	return equipment_instance


func set_equipment_instance(instance) -> bool:
	if instance == null or not (instance is Object and instance.has_method("to_dict")):
		return false
	var instance_payload: Variant = instance.to_dict()
	var normalized_instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(instance_payload)
	if normalized_instance == null:
		return false
	if normalized_instance.item_id == &"" or normalized_instance.instance_id == &"":
		return false
	equipment_instance = normalized_instance
	item_id = normalized_instance.item_id
	instance_id = normalized_instance.instance_id
	return true


func duplicate_state() -> EquipmentEntryState:
	return SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	return {
		"occupied_slot_ids": ProgressionDataUtils.string_name_array_to_string_array(occupied_slot_ids),
		"equipment_instance": equipment_instance.to_dict() if equipment_instance != null else {},
	}


static func from_dict(data: Variant) -> EquipmentEntryState:
	if data is not Dictionary:
		return null
	var occupied_variant: Variant = data.get("occupied_slot_ids", null)
	if occupied_variant is not Array:
		return null
	var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(data.get("equipment_instance", null))
	if instance == null:
		return null
	var entry := SCRIPT.new()
	if not entry.set_equipment_instance(instance):
		return null
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
