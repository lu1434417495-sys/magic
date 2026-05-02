class_name BattleChangeEquipmentResolver
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const CHANGE_EQUIPMENT_AP_COST := 2

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func preview_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	_preview_change_equipment_command(active_unit, command, preview)


func handle_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	_handle_change_equipment_command(active_unit, command, batch)


func build_result(allowed: bool, error_code: String, message: String, command: BattleCommand) -> Dictionary:
	return _build_change_equipment_result(allowed, error_code, message, command)


func append_report(batch: BattleEventBatch, active_unit: BattleUnitState, result: Dictionary, success: bool) -> void:
	_append_change_equipment_report(batch, active_unit, result, success)


func get_unit_hp_max(unit_state: BattleUnitState) -> int:
	return _get_unit_hp_max(unit_state)


func get_unit_stamina_max(unit_state: BattleUnitState) -> int:
	return _get_unit_stamina_max(unit_state)

func _preview_change_equipment_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void:
	var validation := _validate_change_equipment_command(active_unit, command)
	if not bool(validation.get("allowed", false)):
		preview.log_lines.append(String(validation.get("message", "换装命令无效。")))
		return

	var equipment_view = active_unit.get_equipment_view().duplicate_state()
	var backpack_view = _runtime._state.get_party_backpack_view().duplicate_state()
	var apply_result := _apply_change_equipment_to_views(command, validation, equipment_view, backpack_view)
	if bool(apply_result.get("allowed", false)):
		preview.allowed = true
		preview.log_lines.append(String(apply_result.get("message", "换装可执行。")))
	else:
		preview.log_lines.append(String(apply_result.get("message", "换装命令无效。")))


func _handle_change_equipment_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	var validation := _validate_change_equipment_command(active_unit, command)
	if not bool(validation.get("allowed", false)):
		_append_change_equipment_report(batch, active_unit, validation, false)
		return

	var equipment_view = active_unit.get_equipment_view().duplicate_state()
	var backpack_view = _runtime._state.get_party_backpack_view().duplicate_state()
	var apply_result := _apply_change_equipment_to_views(command, validation, equipment_view, backpack_view)
	if not bool(apply_result.get("allowed", false)):
		_append_change_equipment_report(batch, active_unit, apply_result, false)
		return

	var ap_before := int(active_unit.current_ap)
	active_unit.set_equipment_view(equipment_view)
	_runtime._state.set_party_backpack_view(backpack_view)
	_refresh_change_equipment_projection(active_unit, apply_result)
	active_unit.current_ap = maxi(active_unit.current_ap - CHANGE_EQUIPMENT_AP_COST, 0)
	apply_result["ap_before"] = ap_before
	apply_result["ap_after"] = int(active_unit.current_ap)
	_runtime._record_action_issued(active_unit, BattleCommand.TYPE_CHANGE_EQUIPMENT)
	batch.changed_unit_ids.append(active_unit.unit_id)
	_append_change_equipment_report(batch, active_unit, apply_result, true)


func _refresh_change_equipment_projection(active_unit: BattleUnitState, result: Dictionary) -> void:
	if active_unit == null:
		return
	var hp_before := int(active_unit.current_hp)
	var hp_max_before := _get_unit_hp_max(active_unit)
	if active_unit.source_member_id != &"" and _runtime._character_gateway != null:
		_runtime._unit_factory.refresh_equipment_projection(active_unit)
	var hp_max_after := _get_unit_hp_max(active_unit)
	var hp_clamped := false
	if hp_max_after > 0 and hp_max_after < hp_max_before and active_unit.current_hp > hp_max_after:
		active_unit.current_hp = hp_max_after
		hp_clamped = true
	if active_unit.current_hp < 0:
		active_unit.current_hp = 0
		hp_clamped = true
	active_unit.is_alive = active_unit.current_hp > 0
	result["hp_before"] = hp_before
	result["hp_after"] = int(active_unit.current_hp)
	result["hp_max_before"] = hp_max_before
	result["hp_max_after"] = hp_max_after
	result["hp_clamped"] = hp_clamped
	result["weapon_profile_kind"] = String(active_unit.weapon_profile_kind)
	result["weapon_item_id"] = String(active_unit.weapon_item_id)
	result["weapon_profile_type_id"] = String(active_unit.weapon_profile_type_id)
	result["weapon_current_grip"] = String(active_unit.weapon_current_grip)
	result["weapon_attack_range"] = int(active_unit.weapon_attack_range)
	result["weapon_uses_two_hands"] = bool(active_unit.weapon_uses_two_hands)
	result["weapon_physical_damage_tag"] = String(active_unit.weapon_physical_damage_tag)


func _get_unit_hp_max(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 1)


func _get_unit_stamina_max(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(int(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX)), 0)


func _validate_change_equipment_command(active_unit: BattleUnitState, command: BattleCommand) -> Dictionary:
	var result := _build_change_equipment_result(false, "invalid_command", "换装命令无效。", command)
	if _runtime._state == null or active_unit == null or command == null:
		return result

	var operation := _get_change_equipment_operation(command)
	var slot_id := _get_change_equipment_slot_id(command)
	result["operation"] = String(operation)
	result["slot_id"] = String(slot_id)
	result["target_unit_id"] = String(_resolve_change_equipment_target_unit_id(active_unit, command))
	result["item_id"] = String(_resolve_change_equipment_item_id(command))
	result["instance_id"] = String(_resolve_change_equipment_instance_id(command))
	result["occupied_slot_ids"] = _stringify_string_name_array(_resolve_change_equipment_occupied_slots(command, slot_id))

	var target_unit_id := _resolve_change_equipment_target_unit_id(active_unit, command)
	if _runtime._state.active_unit_id != active_unit.unit_id:
		return _with_change_equipment_error(result, "target_not_self", "只能为当前行动单位自己换装。")
	if target_unit_id != active_unit.unit_id:
		return _with_change_equipment_error(result, "target_not_self", "只能为当前行动单位自己换装。")
	if active_unit.current_ap < CHANGE_EQUIPMENT_AP_COST:
		return _with_change_equipment_error(result, "ap_insufficient", "AP不足，换装需要 %d 点 AP。" % CHANGE_EQUIPMENT_AP_COST)
	if operation != BattleCommand.EQUIPMENT_OPERATION_EQUIP and operation != BattleCommand.EQUIPMENT_OPERATION_UNEQUIP:
		return _with_change_equipment_error(result, "operation_invalid", "换装操作无效。")
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
		return _with_change_equipment_error(result, "slot_invalid", "装备槽无效：%s。" % String(slot_id))

	var equipment_view = active_unit.equipment_view
	if equipment_view == null or not (equipment_view is Object and equipment_view.has_method("get_equipped_item_id")):
		return _with_change_equipment_error(result, "equipment_view_unavailable", "战斗内装备状态不可用。")
	var backpack_view = _runtime._state.party_backpack_view
	if backpack_view == null or not (backpack_view is Object and backpack_view.has_method("duplicate_state")):
		return _with_change_equipment_error(result, "backpack_view_unavailable", "战斗内背包状态不可用。")

	if operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP:
		var instance_id := _resolve_change_equipment_instance_id(command)
		if instance_id == &"":
			return _with_change_equipment_error(result, "equipment_instance_required", "装备命令缺少装备实例。")
		var backpack_index := _find_backpack_equipment_instance_index(backpack_view, instance_id)
		if backpack_index < 0:
			return _with_change_equipment_error(result, "equipment_instance_not_found", "战斗背包中找不到装备实例 %s。" % String(instance_id))
		var backpack_instance = backpack_view.equipment_instances[backpack_index]
		var resolved_item_id := ProgressionDataUtils.to_string_name(backpack_instance.item_id)
		var command_item_id := _resolve_change_equipment_item_id(command)
		if command_item_id != &"" and command_item_id != resolved_item_id:
			return _with_change_equipment_error(result, "equipment_instance_item_mismatch", "装备实例与命令物品不一致。")
		result["item_id"] = String(resolved_item_id)
		var item_rule := _resolve_change_equipment_item_rule(resolved_item_id, slot_id, command, active_unit)
		if not bool(item_rule.get("allowed", false)):
			return _with_change_equipment_error(
				result,
				String(item_rule.get("error_code", "item_not_equipment")),
				String(item_rule.get("message", "装备实例不能放入该槽位。"))
			)
		result["occupied_slot_ids"] = item_rule.get("occupied_slot_ids", [])
	else:
		var entry_slot := ProgressionDataUtils.to_string_name(equipment_view.get_entry_slot_for_slot(slot_id))
		if entry_slot == &"":
			return _with_change_equipment_error(result, "slot_empty", "%s 当前没有已装备物品。" % EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id))
		var equipped_instance_id := ProgressionDataUtils.to_string_name(equipment_view.get_equipped_instance_id(slot_id))
		var command_instance_id := _resolve_change_equipment_instance_id(command)
		if command_instance_id != &"" and equipped_instance_id != &"" and command_instance_id != equipped_instance_id:
			return _with_change_equipment_error(result, "equipment_instance_item_mismatch", "装备实例与当前槽位不一致。")
		result["item_id"] = String(ProgressionDataUtils.to_string_name(equipment_view.get_equipped_item_id(slot_id)))
		result["instance_id"] = String(equipped_instance_id)
		result["occupied_slot_ids"] = _stringify_string_name_array(equipment_view.get_occupied_slot_ids_for_entry(entry_slot))

	result["allowed"] = true
	result["error_code"] = ""
	result["message"] = _build_change_equipment_success_message(active_unit, result)
	return result


func _apply_change_equipment_to_views(
	command: BattleCommand,
	validation: Dictionary,
	equipment_view,
	backpack_view
) -> Dictionary:
	if equipment_view == null or backpack_view == null:
		return _build_change_equipment_result(false, "state_unavailable", "战斗内换装状态不可用。", command)

	var operation := ProgressionDataUtils.to_string_name(validation.get("operation", ""))
	var slot_id := ProgressionDataUtils.to_string_name(validation.get("slot_id", ""))
	var item_id := ProgressionDataUtils.to_string_name(validation.get("item_id", ""))
	var instance_id := ProgressionDataUtils.to_string_name(validation.get("instance_id", ""))
	var result := validation.duplicate(true)

	if operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP:
		var backpack_index := _find_backpack_equipment_instance_index(backpack_view, instance_id)
		if backpack_index < 0:
			return _with_change_equipment_error(result, "equipment_instance_not_found", "战斗背包中找不到装备实例 %s。" % String(instance_id))
		var new_instance = backpack_view.equipment_instances[backpack_index]
		item_id = ProgressionDataUtils.to_string_name(new_instance.item_id)
		backpack_view.equipment_instances.remove_at(backpack_index)

		var occupied_slots := ProgressionDataUtils.to_string_name_array(validation.get("occupied_slot_ids", []))
		if occupied_slots.is_empty():
			occupied_slots = [slot_id]
		var displaced_entry_slots: Dictionary = {}
		for occupied_slot_id in occupied_slots:
			var existing_entry_slot := ProgressionDataUtils.to_string_name(equipment_view.get_entry_slot_for_slot(occupied_slot_id))
			if existing_entry_slot == &"" or displaced_entry_slots.has(existing_entry_slot):
				continue
			displaced_entry_slots[existing_entry_slot] = true
			var displaced_instance = equipment_view.pop_equipped_instance(existing_entry_slot)
			if displaced_instance != null:
				if _backpack_has_equipment_instance(backpack_view, ProgressionDataUtils.to_string_name(displaced_instance.instance_id)):
					return _with_change_equipment_error(result, "equipment_instance_already_in_backpack", "战斗背包中已存在装备实例 %s。" % String(displaced_instance.instance_id))
				backpack_view.equipment_instances.append(displaced_instance)
		equipment_view.set_equipped_entry(slot_id, item_id, occupied_slots, new_instance)
		result["item_id"] = String(item_id)
		result["instance_id"] = String(instance_id)
	else:
		var entry_slot := ProgressionDataUtils.to_string_name(equipment_view.get_entry_slot_for_slot(slot_id))
		if entry_slot == &"":
			return _with_change_equipment_error(result, "slot_empty", "%s 当前没有已装备物品。" % EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id))
		var removed_instance = equipment_view.pop_equipped_instance(entry_slot)
		if removed_instance == null:
			return _with_change_equipment_error(result, "slot_empty", "%s 当前没有已装备物品。" % EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id))
		var removed_instance_id := ProgressionDataUtils.to_string_name(removed_instance.instance_id)
		if removed_instance_id != &"" and _backpack_has_equipment_instance(backpack_view, removed_instance_id):
			return _with_change_equipment_error(result, "equipment_instance_already_in_backpack", "战斗背包中已存在装备实例 %s。" % String(removed_instance_id))
		backpack_view.equipment_instances.append(removed_instance)
		result["item_id"] = String(ProgressionDataUtils.to_string_name(removed_instance.item_id))
		result["instance_id"] = String(removed_instance_id)

	var ownership_result := _validate_change_equipment_instance_ownership(equipment_view, backpack_view)
	if not bool(ownership_result.get("allowed", false)):
		return _with_change_equipment_error(
			result,
			String(ownership_result.get("error_code", "equipment_instance_write_failed")),
			String(ownership_result.get("message", "装备实例写入失败。"))
		)
	var capacity_result := _validate_change_equipment_backpack_capacity(backpack_view)
	if not bool(capacity_result.get("allowed", false)):
		return _with_change_equipment_error(
			result,
			String(capacity_result.get("error_code", "backpack_capacity_exceeded")),
			String(capacity_result.get("message", "战斗背包容量不足。"))
		)

	result["allowed"] = true
	result["error_code"] = ""
	result["message"] = _build_change_equipment_success_message(null, result)
	return result


func _build_change_equipment_result(
	allowed: bool,
	error_code: String,
	message: String,
	command: BattleCommand
) -> Dictionary:
	var slot_id := _get_change_equipment_slot_id(command)
	return {
		"allowed": allowed,
		"error_code": error_code,
		"message": message,
		"operation": String(_get_change_equipment_operation(command)),
		"slot_id": String(slot_id),
		"slot_label": EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id),
		"target_unit_id": "",
		"item_id": String(_resolve_change_equipment_item_id(command)),
		"instance_id": String(_resolve_change_equipment_instance_id(command)),
		"occupied_slot_ids": _stringify_string_name_array(_resolve_change_equipment_occupied_slots(command, slot_id)),
	}


func _with_change_equipment_error(result: Dictionary, error_code: String, message: String) -> Dictionary:
	var output := result.duplicate(true)
	output["allowed"] = false
	output["error_code"] = error_code
	output["message"] = message
	return output


func _append_change_equipment_report(
	batch: BattleEventBatch,
	active_unit: BattleUnitState,
	result: Dictionary,
	success: bool
) -> void:
	var report_entry := {
		"entry_type": "change_equipment",
		"type": "change_equipment",
		"ok": success,
		"error_code": "" if success else String(result.get("error_code", "change_equipment_failed")),
		"reason_id": String(result.get("operation", "")),
		"event_tags": ["equipment", "change_equipment"],
		"unit_id": String(active_unit.unit_id) if active_unit != null else "",
		"target_unit_id": String(result.get("target_unit_id", "")),
		"operation": String(result.get("operation", "")),
		"slot_id": String(result.get("slot_id", "")),
		"slot_label": String(result.get("slot_label", "")),
		"item_id": String(result.get("item_id", "")),
		"instance_id": String(result.get("instance_id", "")),
		"ap_cost": CHANGE_EQUIPMENT_AP_COST if success else 0,
		"ap_before": int(result.get("ap_before", 0)),
		"ap_after": int(result.get("ap_after", active_unit.current_ap if active_unit != null else 0)),
		"current_ap": int(active_unit.current_ap) if active_unit != null else 0,
		"hp_before": int(result.get("hp_before", active_unit.current_hp if active_unit != null else 0)),
		"hp_after": int(result.get("hp_after", active_unit.current_hp if active_unit != null else 0)),
		"hp_max_before": int(result.get("hp_max_before", _get_unit_hp_max(active_unit))),
		"hp_max_after": int(result.get("hp_max_after", _get_unit_hp_max(active_unit))),
		"hp_clamped": bool(result.get("hp_clamped", false)),
		"weapon_profile_kind": String(result.get("weapon_profile_kind", active_unit.weapon_profile_kind if active_unit != null else "")),
		"weapon_item_id": String(result.get("weapon_item_id", active_unit.weapon_item_id if active_unit != null else "")),
		"weapon_profile_type_id": String(result.get("weapon_profile_type_id", active_unit.weapon_profile_type_id if active_unit != null else "")),
		"weapon_current_grip": String(result.get("weapon_current_grip", active_unit.weapon_current_grip if active_unit != null else "")),
		"weapon_attack_range": int(result.get("weapon_attack_range", active_unit.weapon_attack_range if active_unit != null else 0)),
		"weapon_uses_two_hands": bool(result.get("weapon_uses_two_hands", active_unit.weapon_uses_two_hands if active_unit != null else false)),
		"weapon_physical_damage_tag": String(result.get("weapon_physical_damage_tag", active_unit.weapon_physical_damage_tag if active_unit != null else "")),
		"text": String(result.get("message", "换装命令无效。")),
	}
	_runtime._append_report_entry_to_batch(batch, report_entry)


func _build_change_equipment_success_message(active_unit: BattleUnitState, result: Dictionary) -> String:
	var unit_name := active_unit.display_name if active_unit != null and not active_unit.display_name.is_empty() else String(result.get("target_unit_id", ""))
	if unit_name.is_empty():
		unit_name = "当前单位"
	var operation := ProgressionDataUtils.to_string_name(result.get("operation", ""))
	var slot_label := String(result.get("slot_label", EQUIPMENT_RULES_SCRIPT.get_slot_label(ProgressionDataUtils.to_string_name(result.get("slot_id", "")))))
	var item_id := String(result.get("item_id", ""))
	var instance_id := String(result.get("instance_id", ""))
	if operation == BattleCommand.EQUIPMENT_OPERATION_EQUIP:
		return "%s 换装：%s 装备 %s（实例 %s），消耗 %d AP。" % [unit_name, slot_label, item_id, instance_id, CHANGE_EQUIPMENT_AP_COST]
	return "%s 换装：卸下 %s 的 %s（实例 %s），消耗 %d AP。" % [unit_name, slot_label, item_id, instance_id, CHANGE_EQUIPMENT_AP_COST]


func _get_change_equipment_operation(command: BattleCommand) -> StringName:
	if command == null:
		return &""
	return ProgressionDataUtils.to_string_name(command.equipment_operation)


func _get_change_equipment_slot_id(command: BattleCommand) -> StringName:
	if command == null:
		return &""
	return ProgressionDataUtils.to_string_name(command.equipment_slot_id)


func _resolve_change_equipment_target_unit_id(active_unit: BattleUnitState, command: BattleCommand) -> StringName:
	if command == null:
		return &""
	var explicit_target := ProgressionDataUtils.to_string_name(command.target_unit_id)
	if explicit_target != &"":
		return explicit_target
	return active_unit.unit_id if active_unit != null else &""


func _resolve_change_equipment_item_id(command: BattleCommand) -> StringName:
	if command == null:
		return &""
	var item_id := ProgressionDataUtils.to_string_name(command.equipment_item_id)
	if item_id != &"":
		return item_id
	var instance_payload: Dictionary = command.equipment_instance if command.equipment_instance is Dictionary else {}
	return ProgressionDataUtils.to_string_name(instance_payload.get("item_id", ""))


func _resolve_change_equipment_instance_id(command: BattleCommand) -> StringName:
	if command == null:
		return &""
	var instance_id := ProgressionDataUtils.to_string_name(command.equipment_instance_id)
	if instance_id != &"":
		return instance_id
	var instance_payload: Dictionary = command.equipment_instance if command.equipment_instance is Dictionary else {}
	return ProgressionDataUtils.to_string_name(instance_payload.get("instance_id", ""))


func _resolve_change_equipment_occupied_slots(command: BattleCommand, slot_id: StringName) -> Array[StringName]:
	var occupied_slots: Array[StringName] = []
	if command != null:
		occupied_slots = EQUIPMENT_RULES_SCRIPT.normalize_slot_ids(command.equipment_occupied_slot_ids)
	var norm_slot := ProgressionDataUtils.to_string_name(slot_id)
	if occupied_slots.is_empty() and EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_slot):
		occupied_slots.append(norm_slot)
	elif EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_slot) and not occupied_slots.has(norm_slot):
		occupied_slots.insert(0, norm_slot)
	return occupied_slots


func _resolve_change_equipment_item_rule(
	item_id: StringName,
	slot_id: StringName,
	command: BattleCommand,
	active_unit: BattleUnitState
) -> Dictionary:
	var norm_item := ProgressionDataUtils.to_string_name(item_id)
	var norm_slot := ProgressionDataUtils.to_string_name(slot_id)
	var fallback_occupied := _resolve_change_equipment_occupied_slots(command, norm_slot)
	var result := {
		"allowed": true,
		"error_code": "",
		"message": "",
		"occupied_slot_ids": _stringify_string_name_array(fallback_occupied),
	}
	var item_def = _get_change_equipment_item_def(norm_item)
	if item_def == null:
		if _has_change_equipment_item_catalog():
			result["allowed"] = false
			result["error_code"] = "item_not_found"
			result["message"] = "找不到装备定义：%s。" % String(norm_item)
		return result
	if not item_def.is_equipment():
		result["allowed"] = false
		result["error_code"] = "item_not_equipment"
		result["message"] = "%s 不是可装备物品。" % String(norm_item)
		return result
	var allowed_slots: Array[StringName] = item_def.get_equipment_slot_ids()
	if not allowed_slots.has(norm_slot):
		result["allowed"] = false
		result["error_code"] = "slot_not_allowed"
		result["message"] = "%s 不能装备到 %s。" % [String(norm_item), EQUIPMENT_RULES_SCRIPT.get_slot_label(norm_slot)]
		return result
	var requirement_rule := _resolve_change_equipment_requirement_rule(active_unit, item_def, norm_item)
	if not bool(requirement_rule.get("allowed", true)):
		return requirement_rule
	var occupied_slots: Array[StringName] = item_def.get_final_occupied_slot_ids(norm_slot)
	if occupied_slots.is_empty():
		occupied_slots = [norm_slot]
	elif not occupied_slots.has(norm_slot):
		occupied_slots.insert(0, norm_slot)
	result["occupied_slot_ids"] = _stringify_string_name_array(occupied_slots)
	return result


func _resolve_change_equipment_requirement_rule(active_unit: BattleUnitState, item_def, item_id: StringName) -> Dictionary:
	var result := {
		"allowed": true,
		"error_code": "",
		"message": "",
		"occupied_slot_ids": [],
		"blockers": [],
	}
	if item_def == null or not (item_def is Object):
		return result
	var equip_req = item_def.equip_requirement
	if equip_req == null or not (equip_req is Object) or not equip_req.has_method("check"):
		return result
	var item_label := _get_change_equipment_item_display_name(item_def, item_id)
	if active_unit == null or active_unit.source_member_id == &"":
		result["allowed"] = false
		result["error_code"] = "member_not_found"
		result["message"] = "%s 有装备需求，但当前单位没有队伍成员来源。" % item_label
		return result
	if _runtime._character_gateway == null or not _runtime._character_gateway.has_method("get_member_state"):
		result["allowed"] = false
		result["error_code"] = "member_not_found"
		result["message"] = "%s 有装备需求，但当前无法读取队伍成员状态。" % item_label
		return result
	var member_state = _runtime._character_gateway.call("get_member_state", active_unit.source_member_id)
	if member_state == null:
		result["allowed"] = false
		result["error_code"] = "member_not_found"
		result["message"] = "%s 有装备需求，但找不到成员 %s。" % [item_label, String(active_unit.source_member_id)]
		return result
	var req_result: Dictionary = equip_req.check(member_state)
	if bool(req_result.get("allowed", true)):
		return result
	var blockers: Array = req_result.get("blockers", [])
	var first_code := String(blockers[0]) if not blockers.is_empty() else "requirement_failed"
	var blocker_strings: Array[String] = []
	for blocker in blockers:
		blocker_strings.append(String(blocker))
	result["allowed"] = false
	result["error_code"] = first_code
	result["blockers"] = blocker_strings
	result["message"] = _build_change_equipment_requirement_failure_message(item_label, blocker_strings)
	return result


func _build_change_equipment_requirement_failure_message(item_label: String, blockers: Array[String]) -> String:
	var reason_labels: Array[String] = []
	for blocker in blockers:
		match blocker:
			"missing_profession":
				reason_labels.append("缺少所需职业")
			"body_size_too_small":
				reason_labels.append("体型过小")
			"body_size_too_large":
				reason_labels.append("体型过大")
			_:
				reason_labels.append(blocker)
	var reason_text := "需求未满足"
	if not reason_labels.is_empty():
		reason_text = "、".join(PackedStringArray(reason_labels))
	return "%s 装备需求未满足：%s。" % [item_label, reason_text]


func _get_change_equipment_item_display_name(item_def, item_id: StringName) -> String:
	if item_def != null and not String(item_def.display_name).is_empty():
		return String(item_def.display_name)
	return String(item_id)


func _get_change_equipment_item_def(item_id: StringName):
	var normalized := ProgressionDataUtils.to_string_name(item_id)
	if normalized == &"" or _runtime._item_defs == null:
		return null
	for key in _runtime._item_defs.keys():
		if typeof(key) != TYPE_STRING_NAME:
			continue
		if key == normalized:
			return _runtime._item_defs[key]
	return null


func _has_change_equipment_item_catalog() -> bool:
	return _runtime._item_defs != null and not _runtime._item_defs.is_empty()


func _validate_change_equipment_instance_ownership(equipment_view, backpack_view) -> Dictionary:
	var owners: Dictionary = {}
	if backpack_view == null or not (backpack_view is Object) or not backpack_view.has_method("get_non_empty_instances"):
		return {
			"allowed": false,
			"error_code": "backpack_view_unavailable",
			"message": "战斗内背包状态不可用。",
		}
	for instance in backpack_view.get_non_empty_instances():
		var instance_id := ProgressionDataUtils.to_string_name(instance.instance_id if instance != null else &"")
		var item_id := ProgressionDataUtils.to_string_name(instance.item_id if instance != null else &"")
		var owner_result := _claim_change_equipment_instance_owner(owners, instance_id, item_id, "backpack")
		if not bool(owner_result.get("allowed", false)):
			return owner_result
	if equipment_view == null or not (equipment_view is Object) or not equipment_view.has_method("get_entry_slot_ids"):
		return {
			"allowed": false,
			"error_code": "equipment_view_unavailable",
			"message": "战斗内装备状态不可用。",
		}
	for entry_slot_id in equipment_view.get_entry_slot_ids():
		var item_id := ProgressionDataUtils.to_string_name(equipment_view.get_equipped_item_id(entry_slot_id))
		var instance_id := ProgressionDataUtils.to_string_name(equipment_view.get_equipped_instance_id(entry_slot_id))
		var owner_name := "equipment:%s" % String(entry_slot_id)
		var owner_result := _claim_change_equipment_instance_owner(owners, instance_id, item_id, owner_name)
		if not bool(owner_result.get("allowed", false)):
			return owner_result
	return {"allowed": true, "error_code": "", "message": ""}


func _claim_change_equipment_instance_owner(
	owners: Dictionary,
	instance_id: StringName,
	item_id: StringName,
	owner_name: String
) -> Dictionary:
	if instance_id == &"" or item_id == &"":
		return {
			"allowed": false,
			"error_code": "equipment_instance_write_failed",
			"message": "装备实例写入失败：%s 存在空实例或空物品。" % owner_name,
		}
	if owners.has(instance_id):
		return {
			"allowed": false,
			"error_code": "equipment_instance_duplicate_owner",
			"message": "装备实例 %s 同时存在于多个位置。" % String(instance_id),
		}
	owners[instance_id] = {
		"item_id": item_id,
		"owner": owner_name,
	}
	return {"allowed": true, "error_code": "", "message": ""}


func _validate_change_equipment_backpack_capacity(backpack_view) -> Dictionary:
	var capacity := _get_change_equipment_backpack_capacity()
	if capacity < 0:
		return {"allowed": true, "error_code": "", "message": ""}
	var used_slots := _get_change_equipment_backpack_used_slots(backpack_view)
	if used_slots <= capacity:
		return {"allowed": true, "error_code": "", "message": ""}
	return {
		"allowed": false,
		"error_code": "backpack_capacity_exceeded",
		"message": "战斗背包容量不足：需要 %d 格，当前容量 %d 格。" % [used_slots, capacity],
	}


func _get_change_equipment_backpack_capacity() -> int:
	if _runtime._character_gateway == null or not _runtime._character_gateway.has_method("get_party_state"):
		return -1
	var party_state = _runtime._character_gateway.call("get_party_state")
	if party_state == null or not (party_state is Object):
		return -1
	var total_capacity := 0
	for member_state in party_state.member_states.values():
		if member_state == null or member_state.progression == null:
			continue
		var unit_base_attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
		if unit_base_attributes == null:
			continue
		total_capacity += maxi(unit_base_attributes.get_attribute_value(&"storage_space"), 0)
	return maxi(total_capacity, 0)


func _get_change_equipment_backpack_used_slots(backpack_view) -> int:
	if backpack_view == null or not (backpack_view is Object):
		return 0
	var stack_count := 0
	var instance_count := 0
	if backpack_view.has_method("get_non_empty_stacks"):
		stack_count = backpack_view.get_non_empty_stacks().size()
	else:
		var raw_stacks: Variant = backpack_view.get("stacks")
		stack_count = raw_stacks.size() if raw_stacks is Array else 0
	if backpack_view.has_method("get_non_empty_instances"):
		instance_count = backpack_view.get_non_empty_instances().size()
	else:
		var raw_instances: Variant = backpack_view.get("equipment_instances")
		instance_count = raw_instances.size() if raw_instances is Array else 0
	return stack_count + instance_count


func _find_backpack_equipment_instance_index(backpack_view, instance_id: StringName) -> int:
	var normalized_id := ProgressionDataUtils.to_string_name(instance_id)
	if backpack_view == null or normalized_id == &"":
		return -1
	for index in range(backpack_view.equipment_instances.size()):
		var instance = backpack_view.equipment_instances[index]
		if instance == null:
			continue
		if ProgressionDataUtils.to_string_name(instance.instance_id) == normalized_id:
			return index
	return -1


func _backpack_has_equipment_instance(backpack_view, instance_id: StringName) -> bool:
	return _find_backpack_equipment_instance_index(backpack_view, instance_id) >= 0


func _stringify_string_name_array(values: Array[StringName]) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result
