## 文件说明：该脚本属于队伍仓库服务相关的服务脚本，集中维护队伍状态、物品定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name PartyWarehouseService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")

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


func get_used_slots() -> int:
	return _get_warehouse_state().get_non_empty_stacks().size()


func get_free_slots() -> int:
	return maxi(get_total_capacity() - get_used_slots(), 0)


func is_over_capacity() -> bool:
	return get_used_slots() > get_total_capacity()


func count_item(item_id: StringName) -> int:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	if normalized_item_id == &"":
		return 0

	var total_quantity := 0
	for stack in _get_warehouse_state().get_non_empty_stacks():
		if stack.item_id != normalized_item_id:
			continue
		total_quantity += maxi(int(stack.quantity), 0)
	return total_quantity


func get_stacks() -> Array:
	return _get_warehouse_state().duplicate_state().stacks


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
	var used_slots_before: int = warehouse_state.stacks.size()

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
	var used_slots_after: int = warehouse_state.stacks.size()
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


func _process_add(item_id: StringName, quantity: int, mutate: bool) -> Dictionary:
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var requested_quantity := maxi(int(quantity), 0)
	var used_slots_before := get_used_slots()
	var item_def = get_item_def(normalized_item_id)
	var target_state = _ensure_warehouse_state() if mutate else _get_warehouse_state().duplicate_state()
	_compact_state(target_state)

	var result := {
		"item_id": String(normalized_item_id),
		"requested_quantity": requested_quantity,
		"added_quantity": 0,
		"remaining_quantity": requested_quantity,
		"used_slots_before": used_slots_before,
		"used_slots_after": target_state.stacks.size(),
		"free_slots_after": maxi(get_total_capacity() - target_state.stacks.size(), 0),
		"created_stack_count": 0,
		"filled_existing_quantity": 0,
		"is_over_capacity": target_state.stacks.size() > get_total_capacity(),
		"item_found": item_def != null,
	}
	if normalized_item_id == &"" or requested_quantity <= 0 or item_def == null:
		return result

	var remaining_quantity := requested_quantity
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
	var available_new_stacks := maxi(get_total_capacity() - target_state.stacks.size(), 0)
	while remaining_quantity > 0 and available_new_stacks > 0:
		var new_stack := WAREHOUSE_STACK_STATE_SCRIPT.new()
		new_stack.item_id = normalized_item_id
		new_stack.quantity = mini(max_stack, remaining_quantity)
		target_state.stacks.append(new_stack)
		remaining_quantity -= new_stack.quantity
		available_new_stacks -= 1
		created_stack_count += 1

	_compact_state(target_state)
	var used_slots_after: int = target_state.stacks.size()
	result["added_quantity"] = requested_quantity - remaining_quantity
	result["remaining_quantity"] = remaining_quantity
	result["used_slots_after"] = used_slots_after
	result["free_slots_after"] = maxi(get_total_capacity() - used_slots_after, 0)
	result["created_stack_count"] = created_stack_count
	result["filled_existing_quantity"] = filled_existing_quantity
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
