class_name PartyEquipmentService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")

## 字段说明：记录队伍状态，会参与运行时状态流转、系统协作和存档恢复。
var _party_state = PARTY_STATE_SCRIPT.new()
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：记录共享仓库服务，负责装备出入仓与装备位之间的物品流转。
var _warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()


func setup(party_state, item_defs: Dictionary = {}, warehouse_service = null) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}
	_warehouse_service = warehouse_service if warehouse_service != null else PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	if _warehouse_service != null and _warehouse_service.has_method("setup"):
		_warehouse_service.setup(_party_state, _item_defs)


func get_item_def(item_id: StringName):
	return _item_defs.get(ProgressionDataUtils.to_string_name(item_id))


func get_equipment_state(member_id: StringName):
	var member_state = _get_member_state(member_id)
	if member_state == null:
		return EQUIPMENT_STATE_SCRIPT.new()
	return _ensure_equipment_state(member_state)


func get_equipped_entries(member_id: StringName) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var equipment_state = get_equipment_state(member_id)
	for slot_id in EQUIPMENT_RULES_SCRIPT.get_all_slot_ids():
		var item_id: StringName = equipment_state.get_equipped_item_id(slot_id)
		if item_id == &"":
			continue
		var item_def = get_item_def(item_id)
		entries.append({
			"slot_id": String(slot_id),
			"slot_label": EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id),
			"item_id": String(item_id),
			"display_name": item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(item_id),
			"icon": item_def.icon if item_def != null else "",
			"description": item_def.description if item_def != null else "",
		})
	return entries


func build_attribute_modifiers(equipment_state_variant: Variant) -> Array[AttributeModifier]:
	var modifiers: Array[AttributeModifier] = []
	var equipment_state = _normalize_equipment_state(equipment_state_variant)
	for slot_id in equipment_state.get_filled_slot_ids():
		var item_id: StringName = equipment_state.get_equipped_item_id(slot_id)
		var item_def = get_item_def(item_id)
		if item_def == null or not item_def.is_equipment():
			continue
		for modifier in item_def.get_attribute_modifiers():
			if modifier is not AttributeModifier:
				continue
			modifiers.append(modifier)
	return modifiers


func equip_item(member_id: StringName, item_id: StringName, requested_slot_id: StringName = &"") -> Dictionary:
	var member_state = _get_member_state(member_id)
	if member_state == null:
		return _build_result(false, member_id, requested_slot_id, item_id, &"", "member_not_found")

	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var item_def = get_item_def(normalized_item_id)
	if item_def == null:
		return _build_result(false, member_id, requested_slot_id, normalized_item_id, &"", "item_not_found")
	if not item_def.is_equipment():
		return _build_result(false, member_id, requested_slot_id, normalized_item_id, &"", "item_not_equipment")

	var allowed_slots: Array[StringName] = item_def.get_equipment_slot_ids()
	var equipment_state = _ensure_equipment_state(member_state)
	var target_slot := ProgressionDataUtils.to_string_name(requested_slot_id)
	if target_slot == &"":
		target_slot = _resolve_target_slot(allowed_slots, equipment_state)
	if target_slot == &"":
		return _build_result(false, member_id, target_slot, normalized_item_id, &"", "slot_unresolved")
	if not allowed_slots.has(target_slot):
		return _build_result(false, member_id, target_slot, normalized_item_id, &"", "slot_not_allowed")
	if _warehouse_service == null or _warehouse_service.count_item(normalized_item_id) <= 0:
		return _build_result(false, member_id, target_slot, normalized_item_id, &"", "warehouse_missing_item")

	var remove_result: Dictionary = _warehouse_service.remove_item(normalized_item_id, 1)
	if int(remove_result.get("removed_quantity", 0)) <= 0:
		return _build_result(false, member_id, target_slot, normalized_item_id, &"", "warehouse_missing_item")

	var previous_item_id: StringName = equipment_state.get_equipped_item_id(target_slot)
	if previous_item_id != &"":
		var return_result: Dictionary = _warehouse_service.add_item(previous_item_id, 1)
		if int(return_result.get("remaining_quantity", 0)) > 0:
			_warehouse_service.add_item(normalized_item_id, 1)
			return _build_result(false, member_id, target_slot, normalized_item_id, previous_item_id, "warehouse_blocked_swap")

	equipment_state.set_equipped_item(target_slot, normalized_item_id)
	return _build_result(true, member_id, target_slot, normalized_item_id, previous_item_id, "equipped")


func unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	var member_state = _get_member_state(member_id)
	var normalized_slot_id := ProgressionDataUtils.to_string_name(slot_id)
	if member_state == null:
		return _build_result(false, member_id, normalized_slot_id, &"", &"", "member_not_found")
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(normalized_slot_id):
		return _build_result(false, member_id, normalized_slot_id, &"", &"", "slot_invalid")

	var equipment_state = _ensure_equipment_state(member_state)
	var current_item_id: StringName = equipment_state.get_equipped_item_id(normalized_slot_id)
	if current_item_id == &"":
		return _build_result(false, member_id, normalized_slot_id, &"", &"", "slot_empty")
	if get_item_def(current_item_id) == null:
		return _build_result(false, member_id, normalized_slot_id, current_item_id, &"", "item_not_found")

	var preview_result: Dictionary = _warehouse_service.preview_add_item(current_item_id, 1)
	if int(preview_result.get("remaining_quantity", 0)) > 0:
		return _build_result(false, member_id, normalized_slot_id, current_item_id, &"", "warehouse_full")

	var add_result: Dictionary = _warehouse_service.add_item(current_item_id, 1)
	if int(add_result.get("remaining_quantity", 0)) > 0:
		return _build_result(false, member_id, normalized_slot_id, current_item_id, &"", "warehouse_full")

	equipment_state.clear_slot(normalized_slot_id)
	return _build_result(true, member_id, normalized_slot_id, current_item_id, &"", "unequipped")


func _get_member_state(member_id: StringName):
	if _party_state == null:
		return null
	return _party_state.get_member_state(ProgressionDataUtils.to_string_name(member_id))


func _ensure_equipment_state(member_state):
	if member_state == null:
		return EQUIPMENT_STATE_SCRIPT.new()
	if member_state.equipment_state == null or not (member_state.equipment_state is Object and member_state.equipment_state.has_method("get_equipped_item_id")):
		member_state.equipment_state = _normalize_equipment_state(member_state.equipment_state)
	return member_state.equipment_state


func _normalize_equipment_state(equipment_state_variant: Variant):
	if equipment_state_variant is Object and equipment_state_variant.has_method("get_equipped_item_id"):
		return equipment_state_variant
	return EQUIPMENT_STATE_SCRIPT.from_dict(equipment_state_variant if equipment_state_variant != null else {})


func _resolve_target_slot(allowed_slots: Array[StringName], equipment_state) -> StringName:
	for slot_id in allowed_slots:
		if equipment_state.get_equipped_item_id(slot_id) == &"":
			return slot_id
	if not allowed_slots.is_empty():
		return allowed_slots[0]
	return &""


func _build_result(
	success: bool,
	member_id: StringName,
	slot_id: StringName,
	item_id: StringName,
	previous_item_id: StringName,
	error_code: String
) -> Dictionary:
	return {
		"success": success,
		"member_id": String(member_id),
		"slot_id": String(slot_id),
		"slot_label": EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id),
		"item_id": String(item_id),
		"previous_item_id": String(previous_item_id),
		"error_code": error_code,
	}
