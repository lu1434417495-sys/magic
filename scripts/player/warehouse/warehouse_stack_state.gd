## 文件说明：该脚本属于仓库堆叠状态相关的状态数据脚本，集中维护物品唯一标识、数量等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WarehouseStackState
extends RefCounted

const WAREHOUSE_STACK_STATE_SCRIPT = preload("res://scripts/player/warehouse/warehouse_stack_state.gd")

## 字段说明：记录物品唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var item_id: StringName = &""
## 字段说明：记录数量，会参与仓库规则判定、序列化和界面展示。
var quantity := 0


func is_empty() -> bool:
	return item_id == &"" or quantity <= 0


func duplicate_state() -> WarehouseStackState:
	return WAREHOUSE_STACK_STATE_SCRIPT.from_dict(to_dict())


func to_dict() -> Dictionary:
	return {
		"item_id": String(item_id),
		"quantity": maxi(int(quantity), 0),
	}


static func from_dict(data: Variant) -> WarehouseStackState:
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if payload.size() != 2:
		return null
	if not payload.has("item_id") or not payload.has("quantity"):
		return null
	var item_id_variant: Variant = payload["item_id"]
	if not _is_string_name_payload_value(item_id_variant):
		return null
	var quantity_variant: Variant = payload["quantity"]
	if quantity_variant is not int:
		return null
	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id_variant)
	var quantity_value := int(quantity_variant)
	if normalized_item_id == &"" or quantity_value <= 0:
		return null
	var stack := WAREHOUSE_STACK_STATE_SCRIPT.new()
	stack.item_id = normalized_item_id
	stack.quantity = quantity_value
	return stack


static func _is_string_name_payload_value(value: Variant) -> bool:
	var value_type := typeof(value)
	return value_type == TYPE_STRING or value_type == TYPE_STRING_NAME
