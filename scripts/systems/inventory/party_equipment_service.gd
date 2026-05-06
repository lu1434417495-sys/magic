class_name PartyEquipmentService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

## 字段说明：记录队伍状态，会参与运行时状态流转、系统协作和存档恢复。
var _party_state = PARTY_STATE_SCRIPT.new()
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：记录共享仓库服务，负责装备出入仓与装备位之间的物品流转。
var _warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()


func setup(
	party_state,
	item_defs: Dictionary = {},
	warehouse_service = null,
	equipment_instance_id_allocator: Callable = Callable()
) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}
	_warehouse_service = warehouse_service if warehouse_service != null else PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	if _warehouse_service != null and _warehouse_service.has_method("setup"):
		_warehouse_service.setup(_party_state, _item_defs, equipment_instance_id_allocator)


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
			"instance_id": String(equipment_state.get_equipped_instance_id(slot_id)),
			"equipment_type_id": String(item_def.get_equipment_type_id_normalized()) if item_def != null else "",
			"display_name": item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(item_id),
			"icon": item_def.icon if item_def != null else "",
			"description": item_def.description if item_def != null else "",
		})
	return entries


## 属性修正结算：只遍历入口槽（避免双手武器重复计算）。
func build_attribute_modifiers(equipment_state_variant: Variant) -> Array[AttributeModifier]:
	var modifiers: Array[AttributeModifier] = []
	var equipment_state = _normalize_equipment_state(equipment_state_variant)
	for entry_slot_id in equipment_state.get_entry_slot_ids():
		var item_id: StringName = equipment_state.get_equipped_item_id(entry_slot_id)
		var item_def = get_item_def(item_id)
		if item_def == null or not item_def.is_equipment():
			continue
		for modifier in item_def.get_attribute_modifiers():
			if modifier is not AttributeModifier:
				continue
			modifiers.append(modifier)
		_append_armor_max_dex_modifier(modifiers, item_def)
	return modifiers


# ---------------------------------------------------------------------------
# preview_equip
# ---------------------------------------------------------------------------

## 预览换装结果，不修改任何状态。
## 返回：
##   success, error_code, blockers,
##   entry_slot_id, occupied_slot_ids,
##   displaced_entries: Array[{ entry_slot_id, item_id }]
func preview_equip(
	member_id: StringName,
	item_id: StringName,
	requested_slot_id: StringName = &"",
	instance_id: StringName = &""
) -> Dictionary:
	var norm_member := ProgressionDataUtils.to_string_name(member_id)
	var norm_item   := ProgressionDataUtils.to_string_name(item_id)
	var norm_slot   := ProgressionDataUtils.to_string_name(requested_slot_id)
	var norm_instance := ProgressionDataUtils.to_string_name(instance_id)

	# 1. 成员存在性
	var member_state = _get_member_state(norm_member)
	if member_state == null:
		return _build_preview_fail(&"", [], [], "member_not_found")

	# 2. 物品定义
	var item_def = get_item_def(norm_item)
	if item_def == null:
		return _build_preview_fail(&"", [], [], "item_not_found")
	if not item_def.is_equipment():
		return _build_preview_fail(&"", [], [], "item_not_equipment")

	# 3. 仓库库存
	if _warehouse_service == null or _warehouse_service.count_item(norm_item) <= 0:
		return _build_preview_fail(&"", [], [], "warehouse_missing_item")
	if norm_instance != &"":
		if not _warehouse_service.has_equipment_instance(norm_instance, norm_item):
			if _warehouse_service.has_equipment_instance(norm_instance):
				return _build_preview_fail(&"", [], [], "equipment_instance_item_mismatch")
			return _build_preview_fail(&"", [], [], "warehouse_missing_instance")
	elif _warehouse_service.count_item(norm_item) > 1:
		return _build_preview_fail(&"", [], [], "equipment_instance_id_required")

	# 4. 解析入口槽
	var allowed_slots: Array[StringName] = item_def.get_equipment_slot_ids()
	var equipment_state = _ensure_equipment_state(member_state)
	var entry_slot := norm_slot
	if entry_slot == &"":
		entry_slot = _resolve_target_slot(allowed_slots, equipment_state)
	if entry_slot == &"":
		return _build_preview_fail(&"", [], [], "slot_unresolved")
	if not allowed_slots.has(entry_slot):
		return _build_preview_fail(entry_slot, [], [], "slot_not_allowed")

	# 5. 计算实际占用槽
	var occupied_slots: Array[StringName] = item_def.get_final_occupied_slot_ids(entry_slot)

	# 6. 找出被挤掉的现有条目（occupied_slots 与现有条目的 occupied 有重叠）
	var displaced_entries: Array[Dictionary] = []
	for existing_entry_slot in equipment_state.get_entry_slot_ids():
		var existing_item_id: StringName = equipment_state.get_equipped_item_id(existing_entry_slot)
		if existing_item_id == &"":
			continue
		var existing_occ: Array[StringName] = equipment_state.get_occupied_slot_ids_for_entry(existing_entry_slot)

		var conflicts := false
		for occ_slot in occupied_slots:
			if existing_occ.has(occ_slot):
				conflicts = true
				break
		if conflicts:
			displaced_entries.append({
				"entry_slot_id": String(existing_entry_slot),
				"item_id": String(existing_item_id),
				"instance_id": String(equipment_state.get_equipped_instance_id(existing_entry_slot)),
			})

	# 7. 资格校验
	var equip_req = item_def.equip_requirement
	if equip_req != null and equip_req.has_method("check"):
		var req_result: Dictionary = equip_req.check(member_state)
		if not bool(req_result.get("allowed", true)):
			var blockers: Array = req_result.get("blockers", [])
			var first_code: String = blockers[0] if not blockers.is_empty() else "requirement_failed"
			var blockers_str: Array[String] = []
			for b in blockers:
				blockers_str.append(String(b))
			return _build_preview_fail(entry_slot, occupied_slots, displaced_entries, first_code, blockers_str)

	# 8. 仓库批量预览（仅作容量检查）
	var withdraw_entries: Array = []
	if norm_instance != &"":
		withdraw_entries.append({
			"item_id": String(norm_item),
			"instance_id": String(norm_instance),
		})
	else:
		withdraw_entries.append(norm_item)
	var items_to_deposit: Array[StringName] = []
	for d in displaced_entries:
		items_to_deposit.append(ProgressionDataUtils.to_string_name(d.get("item_id", "")))

	var batch_preview: Dictionary = _warehouse_service.preview_batch_swap_entries(withdraw_entries, items_to_deposit)
	if not bool(batch_preview.get("allowed", false)):
		return _build_preview_fail(entry_slot, occupied_slots, displaced_entries, batch_preview.get("error_code", "warehouse_blocked_swap"))

	var occupied_str: Array[String] = []
	for s in occupied_slots:
		occupied_str.append(String(s))

	return {
		"success": true,
		"error_code": "",
		"blockers": [],
		"entry_slot_id": String(entry_slot),
		"instance_id": String(norm_instance),
		"occupied_slot_ids": occupied_str,
		"displaced_entries": displaced_entries,
	}


## 预览卸装结果，不修改任何状态。
## 返回：success, error_code, blockers, item_id, entry_slot_id
func preview_unequip(member_id: StringName, slot_id: StringName) -> Dictionary:
	var norm_slot := ProgressionDataUtils.to_string_name(slot_id)
	var member_state = _get_member_state(ProgressionDataUtils.to_string_name(member_id))
	if member_state == null:
		return {"success": false, "error_code": "member_not_found", "blockers": [], "item_id": "", "entry_slot_id": ""}
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_slot):
		return {"success": false, "error_code": "slot_invalid", "blockers": [], "item_id": "", "entry_slot_id": ""}

	var equipment_state = _ensure_equipment_state(member_state)
	var current_item_id: StringName = equipment_state.get_equipped_item_id(norm_slot)
	if current_item_id == &"":
		return {"success": false, "error_code": "slot_empty", "blockers": [], "item_id": "", "entry_slot_id": ""}

	var entry_slot: StringName = equipment_state.get_entry_slot_for_slot(norm_slot)

	var preview_result: Dictionary = _warehouse_service.preview_add_item(current_item_id, 1)
	if int(preview_result.get("remaining_quantity", 0)) > 0:
		return {"success": false, "error_code": "warehouse_full", "blockers": ["warehouse_full"], "item_id": String(current_item_id), "entry_slot_id": String(entry_slot)}

	return {
		"success": true,
		"error_code": "",
		"blockers": [],
		"item_id": String(current_item_id),
		"instance_id": String(equipment_state.get_equipped_instance_id(entry_slot)),
		"entry_slot_id": String(entry_slot),
	}


# ---------------------------------------------------------------------------
# equip_item / unequip_item
# ---------------------------------------------------------------------------

func equip_item(
	member_id: StringName,
	item_id: StringName,
	requested_slot_id: StringName = &"",
	instance_id: StringName = &""
) -> Dictionary:
	var norm_member := ProgressionDataUtils.to_string_name(member_id)
	var norm_item   := ProgressionDataUtils.to_string_name(item_id)
	var norm_instance := ProgressionDataUtils.to_string_name(instance_id)

	# 走 preview 先验证（含容量、资格、槽位冲突）
	var preview := preview_equip(norm_member, norm_item, ProgressionDataUtils.to_string_name(requested_slot_id), norm_instance)
	if not bool(preview.get("success", false)):
		return _build_result(
			false, norm_member,
			ProgressionDataUtils.to_string_name(preview.get("entry_slot_id", "")),
			norm_item, &"",
			preview.get("error_code", "preview_failed"),
			norm_instance
		)

	var member_state = _get_member_state(norm_member)
	var equipment_state = _ensure_equipment_state(member_state)
	var entry_slot := ProgressionDataUtils.to_string_name(preview.get("entry_slot_id", ""))
	var occupied_slots: Array[StringName] = ProgressionDataUtils.to_string_name_array(preview.get("occupied_slot_ids", []))

	# 从仓库取出新装备实例
	var new_instance = _warehouse_service.take_equipment_instance_by_instance_id(norm_instance, norm_item) if norm_instance != &"" else _warehouse_service.take_equipment_instance_by_item(norm_item)
	if new_instance == null:
		return _build_result(false, norm_member, entry_slot, norm_item, &"", "warehouse_missing_instance", norm_instance)

	# 弹出被替换条目，把实例归还仓库
	for d in preview.get("displaced_entries", []):
		var displaced_entry_slot := ProgressionDataUtils.to_string_name(d.get("entry_slot_id", ""))
		var displaced_instance = equipment_state.pop_equipped_instance(displaced_entry_slot)
		if displaced_instance != null:
			_warehouse_service.deposit_equipment_instance(displaced_instance)
		elif displaced_entry_slot != &"":
			equipment_state.clear_entry_slot(displaced_entry_slot)

	# 写入新装备实例到装备槽
	equipment_state.set_equipped_entry(entry_slot, norm_item, occupied_slots, new_instance)

	var previous_item_id := &""
	var displaced: Array = preview.get("displaced_entries", [])
	if not displaced.is_empty():
		previous_item_id = ProgressionDataUtils.to_string_name(displaced[0].get("item_id", ""))
	var previous_instance_id := &""
	if not displaced.is_empty():
		previous_instance_id = ProgressionDataUtils.to_string_name(displaced[0].get("instance_id", ""))

	return _build_result(
		true,
		norm_member,
		entry_slot,
		norm_item,
		previous_item_id,
		"equipped",
		ProgressionDataUtils.to_string_name(new_instance.instance_id),
		previous_instance_id
	)


func unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	var norm_member := ProgressionDataUtils.to_string_name(member_id)
	var norm_slot   := ProgressionDataUtils.to_string_name(slot_id)

	var member_state = _get_member_state(norm_member)
	if member_state == null:
		return _build_result(false, norm_member, norm_slot, &"", &"", "member_not_found")
	if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(norm_slot):
		return _build_result(false, norm_member, norm_slot, &"", &"", "slot_invalid")

	var equipment_state = _ensure_equipment_state(member_state)
	var current_item_id: StringName = equipment_state.get_equipped_item_id(norm_slot)
	if current_item_id == &"":
		return _build_result(false, norm_member, norm_slot, &"", &"", "slot_empty")
	if get_item_def(current_item_id) == null:
		return _build_result(false, norm_member, norm_slot, current_item_id, &"", "item_not_found")

	# 先预览：确认仓库有空余格位
	var preview_result: Dictionary = _warehouse_service.preview_add_item(current_item_id, 1)
	if int(preview_result.get("remaining_quantity", 0)) > 0:
		return _build_result(false, norm_member, norm_slot, current_item_id, &"", "warehouse_full")

	var entry_slot: StringName = equipment_state.get_entry_slot_for_slot(norm_slot)

	# 弹出实例并归还仓库
	var instance = equipment_state.pop_equipped_instance(entry_slot)
	if instance != null:
		_warehouse_service.deposit_equipment_instance(instance)
	else:
		equipment_state.clear_slot(norm_slot)

	return _build_result(
		true,
		norm_member,
		norm_slot,
		current_item_id,
		&"",
		"unequipped",
		ProgressionDataUtils.to_string_name(instance.instance_id if instance != null else &"")
	)


# ---------------------------------------------------------------------------
# internals
# ---------------------------------------------------------------------------

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


func _append_armor_max_dex_modifier(modifiers: Array[AttributeModifier], item_def: ItemDef) -> void:
	if item_def == null or not item_def.is_armor():
		return
	var max_dex_bonus := item_def.get_max_dex_bonus()
	if max_dex_bonus < 0:
		return
	var modifier := AttributeModifier.new()
	modifier.attribute_id = ATTRIBUTE_SERVICE_SCRIPT.ARMOR_MAX_DEX_BONUS
	modifier.mode = AttributeModifier.MODE_FLAT
	modifier.value = max_dex_bonus
	modifier.source_type = &"equipment"
	modifier.source_id = item_def.item_id
	modifiers.append(modifier)


func _resolve_target_slot(allowed_slots: Array[StringName], equipment_state) -> StringName:
	# 优先找空槽
	for slot_id in allowed_slots:
		if equipment_state.get_equipped_item_id(slot_id) == &"" and equipment_state.get_entry_slot_for_slot(slot_id) == &"":
			return slot_id
	# 没有空槽则取第一个允许槽
	if not allowed_slots.is_empty():
		return allowed_slots[0]
	return &""


func _build_result(
	success: bool,
	member_id: StringName,
	slot_id: StringName,
	item_id: StringName,
	previous_item_id: StringName,
	error_code: String,
	instance_id: StringName = &"",
	previous_instance_id: StringName = &""
) -> Dictionary:
	return {
		"success": success,
		"member_id": String(member_id),
		"slot_id": String(slot_id),
		"slot_label": EQUIPMENT_RULES_SCRIPT.get_slot_label(slot_id),
		"item_id": String(item_id),
		"instance_id": String(instance_id),
		"previous_item_id": String(previous_item_id),
		"previous_instance_id": String(previous_instance_id),
		"error_code": error_code,
	}


func _build_preview_fail(
	entry_slot,
	occupied_slots,
	displaced_entries,
	error_code: String,
	blockers: Array[String] = []
) -> Dictionary:
	var occupied_str: Array[String] = []
	if occupied_slots is Array:
		for s in occupied_slots:
			occupied_str.append(String(s) if s != null else "")
	return {
		"success": false,
		"error_code": error_code,
		"blockers": blockers,
		"entry_slot_id": String(entry_slot) if entry_slot != null else "",
		"occupied_slot_ids": occupied_str,
		"displaced_entries": displaced_entries if displaced_entries is Array else [],
	}
