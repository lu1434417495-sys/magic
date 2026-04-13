## 文件说明：该脚本属于仓库状态相关的状态数据脚本，集中维护堆叠列表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WarehouseState
extends RefCounted

const WAREHOUSE_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_state.gd")
const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")

## 字段说明：保存堆叠列表，便于顺序遍历、批量展示、批量运算和整体重建。
var stacks: Array = []


func get_non_empty_stacks() -> Array:
	var result: Array = []
	for stack in stacks:
		if stack == null or stack.is_empty():
			continue
		result.append(stack)
	return result


func duplicate_state() -> WarehouseState:
	var copy := WAREHOUSE_STATE_SCRIPT.new()
	for stack in get_non_empty_stacks():
		copy.stacks.append(stack.duplicate_state())
	return copy


func to_dict() -> Dictionary:
	var stack_data: Array[Dictionary] = []
	for stack in get_non_empty_stacks():
		stack_data.append(stack.to_dict())
	return {
		"stacks": stack_data,
	}


static func from_dict(data: Dictionary) -> WarehouseState:
	var state := WAREHOUSE_STATE_SCRIPT.new()
	var stacks_data: Variant = data.get("stacks", [])
	if stacks_data is not Array:
		return state

	for stack_data in stacks_data:
		if stack_data is not Dictionary:
			continue
		var stack = WAREHOUSE_STACK_STATE_SCRIPT.from_dict(stack_data)
		if stack == null or stack.is_empty():
			continue
		state.stacks.append(stack)
	return state
