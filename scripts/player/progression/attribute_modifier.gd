## 文件说明：该脚本属于属性修正相关的业务脚本，集中维护属性唯一标识、模式、数值等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AttributeModifier
extends Resource

const MODE_FLAT: StringName = &"flat"
const MODE_PERCENT: StringName = &"percent"

## 字段说明：在编辑器中暴露属性唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var attribute_id: StringName = &""
## 字段说明：在编辑器中暴露模式配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var mode: StringName = MODE_FLAT
## 字段说明：在编辑器中暴露数值配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var value := 0
## 字段说明：在编辑器中暴露每阶位数值增量配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var value_per_rank := 0
## 字段说明：在编辑器中暴露来源类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var source_type: StringName = &""
## 字段说明：在编辑器中暴露来源唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var source_id: StringName = &""


func get_value_for_rank(rank: int) -> int:
	var normalized_rank := maxi(rank, 1)
	return value + value_per_rank * (normalized_rank - 1)


func is_percent() -> bool:
	return mode == MODE_PERCENT


func is_flat() -> bool:
	return not is_percent()
