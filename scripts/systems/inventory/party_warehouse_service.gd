## 文件说明：该脚本属于队伍仓库服务相关的服务脚本，集中维护队伍状态、物品定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：装备类物品（is_equipment()==true）存入 equipment_instances；其余物品走 stacks 堆叠逻辑。

class_name PartyWarehouseService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

const STORAGE_SPACE_ATTRIBUTE_ID: StringName = &"storage_space"

## 字段说明：记录队伍状态，会参与运行时状态流转、系统协作和存档恢复。
var _party_state = PARTY_STATE_SCRIPT.new()
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：可选的队伍共享背包 view 覆盖；用于战斗局部背包状态，不直接改写 PartyState.warehouse_state。
var _party_backpack_view = null
## 字段说明：可选 world-level 装备实例 ID 分配器；生产路径由 GameSession 注入。
var _equipment_instance_id_allocator: Callable = Callable()
var _local_equipment_instance_serial := 1


func setup(party_state, item_defs: Dictionary = {}, equipment_instance_id_allocator: Callable = Callable()) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}
	_party_backpack_view = null
	_equipment_instance_id_allocator = equipment_instance_id_allocator


func setup_party_backpack_view(
	party_state,
	party_backpack_view,
	item_defs: Dictionary = {},
	equipment_instance_id_allocator: Callable = Callable()
) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}
	_party_backpack_view = party_backpack_view if party_backpack_view != null else WAREHOUSE_STATE_SCRIPT.new()
	_equipment_instance_id_allocator = equipment_instance_id_allocator


func get_total_capacity() -> int:
	if _party_state == null:
		return 0

	var total_capacity := 0
	for member_state in _party_state.member_states.values():
		if member_state == null or member_state.progression == null:
			continue
		var unit_base_attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
		if unit_base_attributes == null:
			continue
		total_capacity += maxi(unit_base_attributes.get_attribute_value(STORAGE_SPACE_ATTRIBUTE_ID), 0)
	return maxi(total_capacity, 0)


## 已占用格位数 = stacks 数 + equipment_instances 数。
func get_used_slots() -> int:
	var ws = _get_warehouse_state()
	return ws.get_non_empty_stacks().size() + ws.get_non_empty_instances().size()


func get_free_slots() -> int:
	return maxi(get_total_capacity() - get_used_slots(), 0)


func is_over_capacity() -> bool:
	return get_used_slots() > get_total_capacity()


## 返回指定物品的总数量：堆叠中的数量 + 装备实例数。
func count_item(item_id: StringName) -> int:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	if normalized_item_id == &"":
		return 0

	var total_quantity := 0
	var ws = _get_warehouse_state()
	for stack in ws.get_non_empty_stacks():
		if stack.item_id != normalized_item_id:
			continue
		total_quantity += maxi(int(stack.quantity), 0)
	for inst in ws.get_non_empty_instances():
		if inst.item_id == normalized_item_id:
			total_quantity += 1
	return total_quantity


func get_stacks() -> Array:
	return _get_warehouse_state().duplicate_state().stacks


## 返回共享仓库的展示条目。
## 该接口用于 UI / snapshot，避免调用方直接拼接 stacks 与 equipment_instances。
func get_inventory_entries() -> Array[Dictionary]:
	var warehouse_state = _get_warehouse_state().duplicate_state()
	var entries: Array[Dictionary] = []

	for stack in warehouse_state.get_non_empty_stacks():
		if stack == null or stack.is_empty():
			continue
		entries.append(_build_inventory_entry(stack.item_id, int(stack.quantity), &"stack"))

	var equipment_entries: Array[Dictionary] = []
	for inst in warehouse_state.get_non_empty_instances():
		if inst == null or inst.item_id == &"":
			continue
		equipment_entries.append(_build_inventory_entry(inst.item_id, 1, &"instance", inst))

	equipment_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var a_key := "%s:%s" % [String(a.get("item_id", "")), String(a.get("instance_id", ""))]
		var b_key := "%s:%s" % [String(b.get("item_id", "")), String(b.get("instance_id", ""))]
		return a_key < b_key
	)
	for entry in equipment_entries:
		entries.append(entry)

	return entries


func get_item_def(item_id: StringName):
	return _item_defs.get(ProgressionDataUtils.to_string_name(item_id))


func preview_add_item(item_id: StringName, quantity: int) -> Dictionary:
	return _process_add(item_id, quantity, false, false)


func add_item(item_id: StringName, quantity: int) -> Dictionary:
	return _process_add(item_id, quantity, true, true)


func remove_item(item_id: StringName, quantity: int) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var requested_quantity := maxi(int(quantity), 0)
	var warehouse_state = _ensure_warehouse_state()
	_compact_state(warehouse_state)
	var used_slots_before: int = get_used_slots()

	if normalized_item_id == &"" or requested_quantity <= 0:
		return {
			"item_id": String(normalized_item_id),
			"requested_quantity": requested_quantity,
			"removed_quantity": 0,
			"remaining_quantity": requested_quantity,
			"used_slots_before": used_slots_before,
			"used_slots_after": used_slots_before,
			"free_slots_after": maxi(get_total_capacity() - used_slots_before, 0),
			"is_over_capacity": used_slots_before > get_total_capacity(),
			"error_code": "",
		}

	var remaining_quantity := requested_quantity
	var item_def = get_item_def(normalized_item_id)

	if item_def != null and item_def.is_equipment():
		# 装备类 item-only 移除只允许唯一匹配的便利路径，避免正式路径在重复实例中随意拿第一件。
		var matching_indexes := _find_equipment_instance_indexes_by_item(warehouse_state, normalized_item_id)
		if requested_quantity == 1 and matching_indexes.size() == 1:
			warehouse_state.equipment_instances.remove_at(int(matching_indexes[0]))
			remaining_quantity = 0
		else:
			var used_slots_after_reject: int = get_used_slots()
			return {
				"item_id": String(normalized_item_id),
				"requested_quantity": requested_quantity,
				"removed_quantity": 0,
				"remaining_quantity": requested_quantity,
				"used_slots_before": used_slots_before,
				"used_slots_after": used_slots_after_reject,
				"free_slots_after": maxi(get_total_capacity() - used_slots_after_reject, 0),
				"is_over_capacity": used_slots_after_reject > get_total_capacity(),
				"error_code": "equipment_instance_id_required",
			}
	else:
		# 非装备类：只从 stacks 移除。
		for index in range(warehouse_state.stacks.size() - 1, -1, -1):
			if remaining_quantity <= 0:
				break
			var stack = warehouse_state.stacks[index]
			if stack == null or stack.item_id != normalized_item_id:
				continue
			var removed_quantity := mini(maxi(int(stack.quantity), 0), remaining_quantity)
			stack.quantity -= removed_quantity
			remaining_quantity -= removed_quantity
			if stack.quantity <= 0:
				warehouse_state.stacks.remove_at(index)

	_compact_state(warehouse_state)
	var used_slots_after: int = get_used_slots()
	return {
		"item_id": String(normalized_item_id),
		"requested_quantity": requested_quantity,
		"removed_quantity": requested_quantity - remaining_quantity,
		"remaining_quantity": remaining_quantity,
		"used_slots_before": used_slots_before,
		"used_slots_after": used_slots_after,
		"free_slots_after": maxi(get_total_capacity() - used_slots_after, 0),
		"is_over_capacity": used_slots_after > get_total_capacity(),
		"error_code": "",
	}


## 组合预览：在仓库副本上模拟先取出 items_to_withdraw 再存入 items_to_deposit，不修改真实状态。
## 返回 { allowed: bool, error_code: String, blocked_item_id: String }
func preview_batch_swap(items_to_withdraw: Array[StringName], items_to_deposit: Array[StringName]) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, false)


## 原子提交：在仓库副本上执行全部操作，仅当整批成功时才提交到真实状态。
## 返回 { allowed: bool, error_code: String, blocked_item_id: String }
func commit_batch_swap(items_to_withdraw: Array[StringName], items_to_deposit: Array[StringName]) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, true)


func preview_batch_swap_entries(items_to_withdraw: Array, items_to_deposit: Array) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, false)


func commit_batch_swap_entries(items_to_withdraw: Array, items_to_deposit: Array) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, true)


func get_equipment_instance_by_id(instance_id: StringName, expected_item_id: StringName = &""):
	var normalized_instance_id := ProgressionDataUtils.to_string_name(instance_id)
	var normalized_item_id := ProgressionDataUtils.to_string_name(expected_item_id)
	if normalized_instance_id == &"":
		return null
	for inst in _get_warehouse_state().get_non_empty_instances():
		if inst == null:
			continue
		if ProgressionDataUtils.to_string_name(inst.instance_id) != normalized_instance_id:
			continue
		if normalized_item_id != &"" and ProgressionDataUtils.to_string_name(inst.item_id) != normalized_item_id:
			return null
		return EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(inst.to_dict())
	return null


func has_equipment_instance(instance_id: StringName, expected_item_id: StringName = &"") -> bool:
	return get_equipment_instance_by_id(instance_id, expected_item_id) != null


## 从仓库取出第一个匹配 item_id 的装备实例并返回；若无匹配实例则返回 null。
## 调用方须在调用前通过 preview_equip / preview_batch_swap 确认仓库中存在该实例。
func take_equipment_instance_by_item(item_id: StringName):
	var normalized := ProgressionDataUtils.to_string_name(item_id)
	var warehouse_state = _ensure_warehouse_state()
	var matching_indexes := _find_equipment_instance_indexes_by_item(warehouse_state, normalized)
	if matching_indexes.size() != 1:
		return null
	var idx := int(matching_indexes[0])
	var inst = warehouse_state.equipment_instances[idx]
	warehouse_state.equipment_instances.remove_at(idx)
	return inst


func take_equipment_instance_by_instance_id(instance_id: StringName, expected_item_id: StringName = &""):
	var normalized_instance_id := ProgressionDataUtils.to_string_name(instance_id)
	var normalized_item_id := ProgressionDataUtils.to_string_name(expected_item_id)
	if normalized_instance_id == &"":
		return null
	var warehouse_state = _ensure_warehouse_state()
	for idx in range(warehouse_state.equipment_instances.size()):
		var inst = warehouse_state.equipment_instances[idx]
		if inst == null:
			continue
		if ProgressionDataUtils.to_string_name(inst.instance_id) != normalized_instance_id:
			continue
		if normalized_item_id != &"" and ProgressionDataUtils.to_string_name(inst.item_id) != normalized_item_id:
			return null
		warehouse_state.equipment_instances.remove_at(idx)
		return inst
	return null


func remove_equipment_instance(item_id: StringName, instance_id: StringName) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var normalized_instance_id := ProgressionDataUtils.to_string_name(instance_id)
	var warehouse_state = _ensure_warehouse_state()
	_compact_state(warehouse_state)
	var used_slots_before: int = get_used_slots()
	var item_def = get_item_def(normalized_item_id)
	var result := {
		"item_id": String(normalized_item_id),
		"instance_id": String(normalized_instance_id),
		"requested_quantity": 1,
		"removed_quantity": 0,
		"remaining_quantity": 1,
		"used_slots_before": used_slots_before,
		"used_slots_after": used_slots_before,
		"free_slots_after": maxi(get_total_capacity() - used_slots_before, 0),
		"is_over_capacity": used_slots_before > get_total_capacity(),
		"error_code": "",
	}
	if normalized_item_id == &"" or item_def == null:
		result["error_code"] = "item_not_found"
		return result
	if not item_def.is_equipment():
		result["error_code"] = "item_not_equipment"
		return result
	if normalized_instance_id == &"":
		result["error_code"] = "equipment_instance_id_required"
		return result
	var matched_any_instance := false
	for inst in warehouse_state.get_non_empty_instances():
		if inst == null:
			continue
		if ProgressionDataUtils.to_string_name(inst.instance_id) != normalized_instance_id:
			continue
		matched_any_instance = true
		if ProgressionDataUtils.to_string_name(inst.item_id) != normalized_item_id:
			result["error_code"] = "equipment_instance_item_mismatch"
			return result
		break
	if not matched_any_instance:
		result["error_code"] = "warehouse_missing_instance"
		return result
	var removed_instance = take_equipment_instance_by_instance_id(normalized_instance_id, normalized_item_id)
	if removed_instance == null:
		result["error_code"] = "warehouse_missing_instance"
		return result
	_compact_state(warehouse_state)
	var used_slots_after: int = get_used_slots()
	result["removed_quantity"] = 1
	result["remaining_quantity"] = 0
	result["used_slots_after"] = used_slots_after
	result["free_slots_after"] = maxi(get_total_capacity() - used_slots_after, 0)
	result["is_over_capacity"] = used_slots_after > get_total_capacity()
	return result


## 将已有装备实例按共享背包容量规则存入仓库。
## 默认保留既有实例 ID；外部奖励可通过 force_new_instance_id 要求写入前改用 world-level 新 ID。
func add_equipment_instance(instance, force_new_instance_id: bool = false) -> Dictionary:
	var warehouse_state = _ensure_warehouse_state()
	_compact_state(warehouse_state)
	var used_slots_before: int = get_used_slots()
	var item_id := ProgressionDataUtils.to_string_name(instance.item_id if instance != null else &"")
	var item_def = get_item_def(item_id)
	var result := {
		"item_id": String(item_id),
		"requested_quantity": 1,
		"added_quantity": 0,
		"remaining_quantity": 1,
		"used_slots_before": used_slots_before,
		"used_slots_after": used_slots_before,
		"free_slots_after": maxi(get_total_capacity() - used_slots_before, 0),
		"is_over_capacity": used_slots_before > get_total_capacity(),
		"item_found": item_def != null,
		"is_equipment": item_def != null and item_def.is_equipment(),
		"allocated_equipment_instance_ids": [],
	}
	if instance == null or item_id == &"" or item_def == null or not item_def.is_equipment():
		return result
	if get_total_capacity() - used_slots_before <= 0:
		return result
	var allocated_instance_id := &""
	if force_new_instance_id or instance.instance_id == &"":
		allocated_instance_id = _allocate_equipment_instance_id(warehouse_state)
		instance.instance_id = allocated_instance_id
		if allocated_instance_id == &"":
			return result
	warehouse_state.equipment_instances.append(instance)
	_compact_state(warehouse_state)
	var used_slots_after: int = get_used_slots()
	result["added_quantity"] = 1
	result["remaining_quantity"] = 0
	result["used_slots_after"] = used_slots_after
	result["free_slots_after"] = maxi(get_total_capacity() - used_slots_after, 0)
	result["is_over_capacity"] = used_slots_after > get_total_capacity()
	if allocated_instance_id != &"":
		result["allocated_equipment_instance_ids"] = [String(allocated_instance_id)]
	return result


## 将装备实例直接存入仓库，不检查容量。调用方须在存入前通过预览确认有空余格位。
func deposit_equipment_instance(instance) -> bool:
	if instance == null:
		return false
	var warehouse_state = _ensure_warehouse_state()
	# 确保有效的 instance_id
	if instance.instance_id == &"":
		instance.instance_id = _allocate_equipment_instance_id(warehouse_state)
		if instance.instance_id == &"":
			return false
	warehouse_state.equipment_instances.append(instance)
	return true


func _execute_batch_swap(
	items_to_withdraw: Array,
	items_to_deposit: Array,
	consume_allocator: bool
) -> Dictionary:
	for withdraw_variant in items_to_withdraw:
		var withdraw_entry := _normalize_batch_item_entry(withdraw_variant)
		var item_id := ProgressionDataUtils.to_string_name(withdraw_entry.get("item_id", ""))
		var instance_id := ProgressionDataUtils.to_string_name(withdraw_entry.get("instance_id", ""))
		var item_def = get_item_def(item_id)
		var r: Dictionary = {}
		if item_def != null and item_def.is_equipment() and instance_id != &"":
			r = remove_equipment_instance(item_id, instance_id)
		else:
			r = remove_item(item_id, 1)
		if int(r.get("removed_quantity", 0)) <= 0:
			return {
				"allowed": false,
				"error_code": String(r.get("error_code", "warehouse_missing_item")) if not String(r.get("error_code", "")).is_empty() else "warehouse_missing_item",
				"blocked_item_id": String(item_id),
				"blocked_instance_id": String(instance_id),
			}
	for deposit_variant in items_to_deposit:
		var deposit_entry := _normalize_batch_item_entry(deposit_variant)
		var item_id := ProgressionDataUtils.to_string_name(deposit_entry.get("item_id", ""))
		var preview := preview_add_item(item_id, 1)
		if int(preview.get("remaining_quantity", 0)) > 0:
			return {
				"allowed": false,
				"error_code": "warehouse_blocked_swap",
				"blocked_item_id": String(item_id),
				"blocked_instance_id": String(ProgressionDataUtils.to_string_name(deposit_entry.get("instance_id", ""))),
			}
		var equipment_instance_variant: Variant = deposit_entry.get("equipment_instance", null)
		if equipment_instance_variant != null:
			var instance = equipment_instance_variant
			if equipment_instance_variant is Dictionary:
				instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(equipment_instance_variant)
			var add_instance_result: Dictionary = add_equipment_instance(instance, false)
			if int(add_instance_result.get("added_quantity", 0)) <= 0:
				return {
					"allowed": false,
					"error_code": String(add_instance_result.get("error_code", "warehouse_blocked_swap")),
					"blocked_item_id": String(item_id),
					"blocked_instance_id": String(ProgressionDataUtils.to_string_name(deposit_entry.get("instance_id", ""))),
				}
		else:
			_process_add(item_id, 1, true, consume_allocator)
	return {"allowed": true, "error_code": "", "blocked_item_id": "", "blocked_instance_id": ""}


func _run_batch_swap_transaction(
	items_to_withdraw: Array,
	items_to_deposit: Array,
	commit_on_success: bool
) -> Dictionary:
	var baseline_state = _get_warehouse_state().duplicate_state()
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	var original_state = _party_backpack_view if _party_backpack_view != null else _party_state.warehouse_state

	_set_transaction_warehouse_state(baseline_state)
	var result: Dictionary = _execute_batch_swap(items_to_withdraw, items_to_deposit, commit_on_success)
	if bool(result.get("allowed", false)) and commit_on_success:
		if _party_backpack_view != null:
			_copy_warehouse_state(baseline_state, original_state)
			_party_backpack_view = original_state
		return result

	_set_transaction_warehouse_state(original_state)
	return result


func _normalize_batch_item_entry(entry_variant: Variant) -> Dictionary:
	if entry_variant is Dictionary:
		var entry: Dictionary = entry_variant
		var item_id := ProgressionDataUtils.to_string_name(entry.get("item_id", ""))
		var instance_id := ProgressionDataUtils.to_string_name(entry.get("instance_id", ""))
		var result := {
			"item_id": item_id,
			"instance_id": instance_id,
		}
		if entry.has("equipment_instance"):
			result["equipment_instance"] = entry["equipment_instance"]
		return result
	return {
		"item_id": ProgressionDataUtils.to_string_name(entry_variant),
		"instance_id": &"",
	}


func _process_add(item_id: StringName, quantity: int, mutate: bool, consume_allocator: bool) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var requested_quantity := maxi(int(quantity), 0)
	var used_slots_before := get_used_slots()
	var item_def = get_item_def(normalized_item_id)
	var target_state = _ensure_warehouse_state() if mutate else _get_warehouse_state().duplicate_state()
	_compact_state(target_state)

	var current_used: int = target_state.stacks.size() + target_state.get_non_empty_instances().size()
	var result := {
		"item_id": String(normalized_item_id),
		"requested_quantity": requested_quantity,
		"added_quantity": 0,
		"remaining_quantity": requested_quantity,
		"used_slots_before": used_slots_before,
		"used_slots_after": current_used,
		"free_slots_after": maxi(get_total_capacity() - current_used, 0),
		"created_stack_count": 0,
		"filled_existing_quantity": 0,
		"is_over_capacity": current_used > get_total_capacity(),
		"item_found": item_def != null,
	}
	if normalized_item_id == &"" or requested_quantity <= 0 or item_def == null:
		return result

	var remaining_quantity := requested_quantity

	if item_def.is_equipment():
		# 装备类：每件独立占一个格位，不堆叠。
		var available_new_slots := maxi(get_total_capacity() - target_state.stacks.size() - target_state.equipment_instances.size(), 0)
		var created := 0
		var allocated_instance_ids: Array[String] = []
		while remaining_quantity > 0 and available_new_slots > 0:
			var new_inst = _create_equipment_instance(normalized_item_id, target_state, consume_allocator)
			if new_inst.instance_id == &"":
				break
			if consume_allocator and new_inst.instance_id != &"":
				allocated_instance_ids.append(String(new_inst.instance_id))
			target_state.equipment_instances.append(new_inst)
			remaining_quantity -= 1
			available_new_slots -= 1
			created += 1
		result["created_stack_count"] = created
		result["allocated_equipment_instance_ids"] = allocated_instance_ids
	else:
		# 非装备类：优先补满已有堆叠，再开新堆叠。
		var filled_existing_quantity := 0
		var max_stack: int = int(item_def.get_effective_max_stack())

		for stack in target_state.stacks:
			if remaining_quantity <= 0:
				break
			if stack == null or stack.item_id != normalized_item_id:
				continue
			if int(stack.quantity) >= max_stack:
				continue
			var accepted_quantity := mini(max_stack - int(stack.quantity), remaining_quantity)
			if accepted_quantity <= 0:
				continue
			stack.quantity += accepted_quantity
			remaining_quantity -= accepted_quantity
			filled_existing_quantity += accepted_quantity

		var created_stack_count := 0
		var available_new_stacks := maxi(get_total_capacity() - target_state.stacks.size() - target_state.equipment_instances.size(), 0)
		while remaining_quantity > 0 and available_new_stacks > 0:
			var new_stack := WAREHOUSE_STACK_STATE_SCRIPT.new()
			new_stack.item_id = normalized_item_id
			new_stack.quantity = mini(max_stack, remaining_quantity)
			target_state.stacks.append(new_stack)
			remaining_quantity -= new_stack.quantity
			available_new_stacks -= 1
			created_stack_count += 1

		_compact_state(target_state)
		result["filled_existing_quantity"] = filled_existing_quantity
		result["created_stack_count"] = created_stack_count

	var used_slots_after: int = target_state.stacks.size() + target_state.get_non_empty_instances().size()
	result["added_quantity"] = requested_quantity - remaining_quantity
	result["remaining_quantity"] = remaining_quantity
	result["used_slots_after"] = used_slots_after
	result["free_slots_after"] = maxi(get_total_capacity() - used_slots_after, 0)
	result["is_over_capacity"] = used_slots_after > get_total_capacity()
	return result


func _create_equipment_instance(item_id: StringName, target_state, consume_allocator: bool):
	var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(item_id)
	instance.instance_id = _allocate_equipment_instance_id(target_state) if consume_allocator else _allocate_preview_equipment_instance_id(target_state)
	return instance


func _allocate_equipment_instance_id(target_state = null) -> StringName:
	if _equipment_instance_id_allocator.is_valid():
		var allocated_variant: Variant = _equipment_instance_id_allocator.call()
		return ProgressionDataUtils.to_string_name(allocated_variant)
	while true:
		var candidate := EQUIPMENT_INSTANCE_STATE_SCRIPT.format_instance_id(_local_equipment_instance_serial)
		_local_equipment_instance_serial += 1
		if not _equipment_instance_id_exists(candidate, target_state):
			return candidate
	return &""


func _allocate_preview_equipment_instance_id(target_state = null) -> StringName:
	var serial := 1
	while true:
		var candidate := EQUIPMENT_INSTANCE_STATE_SCRIPT.format_preview_instance_id(serial)
		serial += 1
		if not _equipment_instance_id_exists(candidate, target_state):
			return candidate
	return &""


func _equipment_instance_id_exists(instance_id: StringName, target_state = null) -> bool:
	var normalized_id := ProgressionDataUtils.to_string_name(instance_id)
	if normalized_id == &"":
		return false
	var states: Array = []
	if target_state != null:
		states.append(target_state)
	var current_state = _get_warehouse_state()
	if current_state != null and current_state != target_state:
		states.append(current_state)
	for state in states:
		if state == null:
			continue
		for instance in state.get_non_empty_instances():
			if instance != null and ProgressionDataUtils.to_string_name(instance.instance_id) == normalized_id:
				return true
	return false


func _find_equipment_instance_indexes_by_item(warehouse_state, item_id: StringName) -> Array[int]:
	var result: Array[int] = []
	if warehouse_state == null:
		return result
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	for idx in range(warehouse_state.equipment_instances.size()):
		var inst = warehouse_state.equipment_instances[idx]
		if inst == null:
			continue
		if ProgressionDataUtils.to_string_name(inst.item_id) == normalized_item_id:
			result.append(idx)
	return result


func _ensure_warehouse_state():
	if _party_backpack_view != null:
		return _party_backpack_view
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	if _party_state.warehouse_state == null:
		_party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()
	return _party_state.warehouse_state


func _get_warehouse_state():
	if _party_backpack_view != null:
		return _party_backpack_view
	if _party_state == null:
		return WAREHOUSE_STATE_SCRIPT.new()
	if _party_state.warehouse_state == null:
		return WAREHOUSE_STATE_SCRIPT.new()
	return _party_state.warehouse_state


func _set_transaction_warehouse_state(warehouse_state) -> void:
	if _party_backpack_view != null:
		_party_backpack_view = warehouse_state
		return
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	_party_state.warehouse_state = warehouse_state


func _copy_warehouse_state(source_state, target_state) -> void:
	if source_state == null or target_state == null:
		return
	target_state.stacks = []
	target_state.equipment_instances = []
	for stack in source_state.get_non_empty_stacks():
		target_state.stacks.append(stack.duplicate_state())
	for inst in source_state.get_non_empty_instances():
		target_state.equipment_instances.append(EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(inst.to_dict()))


func _compact_state(warehouse_state) -> void:
	if warehouse_state == null:
		return
	warehouse_state.stacks = warehouse_state.get_non_empty_stacks()
	warehouse_state.equipment_instances = warehouse_state.get_non_empty_instances()


func _build_inventory_entry(item_id: StringName, quantity: int, storage_mode: StringName, equipment_instance = null) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var resolved_quantity := maxi(int(quantity), 0)
	var item_def: ItemDef = get_item_def(normalized_item_id) as ItemDef
	var granted_skill_id: StringName = item_def.granted_skill_id if item_def != null else &""
	var entry := {
		"item_id": String(normalized_item_id),
		"display_name": item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(normalized_item_id),
		"description": item_def.description if item_def != null else "该物品定义缺失，当前仅保留存档中的 item_id 与数量。",
		"icon": item_def.icon if item_def != null else "",
		"quantity": resolved_quantity,
		"total_quantity": count_item(normalized_item_id),
		"is_stackable": item_def.is_stackable if item_def != null else resolved_quantity > 1,
		"stack_limit": item_def.get_effective_max_stack() if item_def != null else maxi(resolved_quantity, 1),
		"item_category": String(item_def.get_item_category_normalized()) if item_def != null else "",
		"is_skill_book": item_def != null and item_def.is_skill_book(),
		"granted_skill_id": String(granted_skill_id),
		"storage_mode": String(storage_mode),
	}
	if equipment_instance != null:
		entry["instance_id"] = String(ProgressionDataUtils.to_string_name(equipment_instance.instance_id))
		entry["rarity"] = int(equipment_instance.rarity)
		entry["current_durability"] = int(equipment_instance.current_durability)
		entry["armor_wear_progress"] = float(equipment_instance.armor_wear_progress)
		entry["weapon_wear_progress"] = float(equipment_instance.weapon_wear_progress)
	return entry
