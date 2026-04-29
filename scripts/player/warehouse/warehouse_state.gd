## 文件说明：该脚本属于仓库状态相关的状态数据脚本，集中维护堆叠列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WarehouseState
extends RefCounted

const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")

## 字段说明：保存堆叠列表，便于顺序遍历、批量展示、批量运算和整体重建。
var stacks: Array = []
## 字段说明：保存装备实例列表；每件装备物品独立存储，不参与堆叠。
var equipment_instances: Array = []


func get_non_empty_stacks() -> Array:
	var result: Array = []
	for stack in stacks:
		if stack == null or stack.is_empty():
			continue
		result.append(stack)
	return result


## 返回所有有效装备实例（instance_id 与 item_id 均非空）。
func get_non_empty_instances() -> Array:
	var result: Array = []
	for inst in equipment_instances:
		if inst == null or inst.instance_id == &"" or inst.item_id == &"":
			continue
		result.append(inst)
	return result


func duplicate_state() -> WarehouseState:
	var copy := WAREHOUSE_STATE_SCRIPT.new()
	for stack in get_non_empty_stacks():
		copy.stacks.append(stack.duplicate_state())
	for inst in get_non_empty_instances():
		copy.equipment_instances.append(EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(inst.to_dict()))
	return copy


func to_dict() -> Dictionary:
	var stack_data: Array[Dictionary] = []
	for stack in get_non_empty_stacks():
		stack_data.append(stack.to_dict())
	var instance_data: Array[Dictionary] = []
	for inst in get_non_empty_instances():
		instance_data.append(inst.to_dict())
	return {
		"stacks": stack_data,
		"equipment_instances": instance_data,
	}


static func from_dict(data: Dictionary) -> WarehouseState:
	var state := WAREHOUSE_STATE_SCRIPT.new()

	var stacks_data: Variant = data.get("stacks", [])
	if stacks_data is Array:
		for stack_data in stacks_data:
			if stack_data is not Dictionary:
				continue
			var stack = WAREHOUSE_STACK_STATE_SCRIPT.from_dict(stack_data)
			if stack == null or stack.is_empty():
				continue
			state.stacks.append(stack)

	var instances_data: Variant = data.get("equipment_instances", [])
	if instances_data is Array:
		for inst_data in instances_data:
			var inst = EQUIPMENT_INSTANCE_STATE_SCRIPT.from_dict(inst_data)
			if inst == null or inst.instance_id == &"" or inst.item_id == &"":
				continue
			state.equipment_instances.append(inst)

	return state
