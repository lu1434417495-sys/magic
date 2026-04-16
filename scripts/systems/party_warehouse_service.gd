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


func setup(party_state, item_defs: Dictionary = {}) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_item_defs = item_defs if item_defs != null else {}


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

	var equipment_counts: Dictionary = {}
	for inst in warehouse_state.get_non_empty_instances():
		if inst == null or inst.item_id == &"":
			continue
		var item_id := ProgressionDataUtils.to_string_name(inst.item_id)
		equipment_counts[item_id] = int(equipment_counts.get(item_id, 0)) + 1

	for item_id_str in ProgressionDataUtils.sorted_string_keys(equipment_counts):
		var item_id := StringName(item_id_str)
		entries.append(_build_inventory_entry(item_id, int(equipment_counts.get(item_id, 0)), &"instance"))

	return entries


func get_item_def(item_id: StringName):
	return _item_defs.get(ProgressionDataUtils.to_string_name(item_id))


func preview_add_item(item_id: StringName, quantity: int) -> Dictionary:
	return _process_add(item_id, quantity, false)


func add_item(item_id: StringName, quantity: int) -> Dictionary:
	return _process_add(item_id, quantity, true)


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
		}

	var remaining_quantity := requested_quantity
	var item_def = get_item_def(normalized_item_id)

	if item_def != null and item_def.is_equipment():
		# 装备类：只从 equipment_instances 移除。
		var i: int = warehouse_state.equipment_instances.size() - 1
		while i >= 0 and remaining_quantity > 0:
			var inst = warehouse_state.equipment_instances[i]
			if inst != null and inst.item_id == normalized_item_id:
				warehouse_state.equipment_instances.remove_at(i)
				remaining_quantity -= 1
			i -= 1
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
	}


## 组合预览：在仓库副本上模拟先取出 items_to_withdraw 再存入 items_to_deposit，不修改真实状态。
## 返回 { allowed: bool, error_code: String, blocked_item_id: String }
func preview_batch_swap(items_to_withdraw: Array[StringName], items_to_deposit: Array[StringName]) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, false)


## 原子提交：在仓库副本上执行全部操作，仅当整批成功时才提交到真实状态。
## 返回 { allowed: bool, error_code: String, blocked_item_id: String }
func commit_batch_swap(items_to_withdraw: Array[StringName], items_to_deposit: Array[StringName]) -> Dictionary:
	return _run_batch_swap_transaction(items_to_withdraw, items_to_deposit, true)


## 从仓库取出第一个匹配 item_id 的装备实例并返回；若无匹配实例则返回 null。
## 调用方须在调用前通过 preview_equip / preview_batch_swap 确认仓库中存在该实例。
func take_equipment_instance_by_item(item_id: StringName):
	var normalized := ProgressionDataUtils.to_string_name(item_id)
	var warehouse_state = _ensure_warehouse_state()
	for idx in range(warehouse_state.equipment_instances.size()):
		var inst = warehouse_state.equipment_instances[idx]
		if inst != null and inst.item_id == normalized:
			warehouse_state.equipment_instances.remove_at(idx)
			return inst
	return null


## 将装备实例直接存入仓库，不检查容量。调用方须在存入前通过预览确认有空余格位。
func deposit_equipment_instance(instance) -> void:
	if instance == null:
		return
	var warehouse_state = _ensure_warehouse_state()
	# 确保有效的 instance_id
	if instance.instance_id == &"":
		instance.instance_id = EQUIPMENT_INSTANCE_STATE_SCRIPT.generate_id()
	warehouse_state.equipment_instances.append(instance)


func _execute_batch_swap(items_to_withdraw: Array[StringName], items_to_deposit: Array[StringName]) -> Dictionary:
	for item_id in items_to_withdraw:
		var r := remove_item(item_id, 1)
		if int(r.get("removed_quantity", 0)) <= 0:
			return {"allowed": false, "error_code": "warehouse_missing_item", "blocked_item_id": String(item_id)}
	for item_id in items_to_deposit:
		var preview := preview_add_item(item_id, 1)
		if int(preview.get("remaining_quantity", 0)) > 0:
			return {"allowed": false, "error_code": "warehouse_blocked_swap", "blocked_item_id": String(item_id)}
		add_item(item_id, 1)
	return {"allowed": true, "error_code": "", "blocked_item_id": ""}


func _run_batch_swap_transaction(
	items_to_withdraw: Array[StringName],
	items_to_deposit: Array[StringName],
	commit_on_success: bool
) -> Dictionary:
	var baseline_state = _get_warehouse_state().duplicate_state()
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	var original_state = _party_state.warehouse_state

	_party_state.warehouse_state = baseline_state
	var result: Dictionary = _execute_batch_swap(items_to_withdraw, items_to_deposit)
	if bool(result.get("allowed", false)) and commit_on_success:
		return result

	_party_state.warehouse_state = original_state
	return result


func _process_add(item_id: StringName, quantity: int, mutate: bool) -> Dictionary:
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
		while remaining_quantity > 0 and available_new_slots > 0:
			var new_inst = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(normalized_item_id)
			target_state.equipment_instances.append(new_inst)
			remaining_quantity -= 1
			available_new_slots -= 1
			created += 1
		result["created_stack_count"] = created
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


func _ensure_warehouse_state():
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	if _party_state.warehouse_state == null:
		_party_state.warehouse_state = WAREHOUSE_STATE_SCRIPT.new()
	return _party_state.warehouse_state


func _get_warehouse_state():
	if _party_state == null:
		return WAREHOUSE_STATE_SCRIPT.new()
	if _party_state.warehouse_state == null:
		return WAREHOUSE_STATE_SCRIPT.new()
	return _party_state.warehouse_state


func _compact_state(warehouse_state) -> void:
	if warehouse_state == null:
		return
	warehouse_state.stacks = warehouse_state.get_non_empty_stacks()
	warehouse_state.equipment_instances = warehouse_state.get_non_empty_instances()


func _build_inventory_entry(item_id: StringName, quantity: int, storage_mode: StringName) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var resolved_quantity := maxi(int(quantity), 0)
	var item_def: ItemDef = get_item_def(normalized_item_id) as ItemDef
	var granted_skill_id: StringName = item_def.granted_skill_id if item_def != null else &""
	return {
		"item_id": String(normalized_item_id),
		"display_name": item_def.display_name if item_def != null and not item_def.display_name.is_empty() else String(normalized_item_id),
		"description": item_def.description if item_def != null else "该物品定义缺失，当前仅保留存档中的 item_id 与数量。",
		"icon": item_def.icon if item_def != null else "",
		"quantity": resolved_quantity,
		"total_quantity": count_item(normalized_item_id),
		"is_stackable": item_def.is_stackable if item_def != null else resolved_quantity > 1,
		"stack_limit": item_def.get_effective_max_stack() if item_def != null else maxi(resolved_quantity, 1),
		"item_category": String(item_def.item_category) if item_def != null else "",
		"is_skill_book": item_def != null and item_def.is_skill_book(),
		"granted_skill_id": String(granted_skill_id),
		"storage_mode": String(storage_mode),
	}
