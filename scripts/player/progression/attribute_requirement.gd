## 文件说明：该脚本属于属性条件相关的业务脚本，集中维护属性唯一标识、最小数值、最大数值等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AttributeRequirement
extends Resource

## 字段说明：在编辑器中暴露属性唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var attribute_id: StringName = &""
## 字段说明：在编辑器中暴露最小数值参数，用于定义该对象生效或生成时的下限条件。
@export var min_value := 0
## 字段说明：在编辑器中暴露最大数值参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export var max_value := 0


func matches_value(value: int) -> bool:
	if value < min_value:
		return false
	return max_value <= 0 or value <= max_value
