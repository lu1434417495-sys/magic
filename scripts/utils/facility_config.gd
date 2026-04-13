## 文件说明：该脚本属于设施配置相关的配置资源脚本，集中维护设施唯一标识、显示名称、类别等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name FacilityConfig
extends Resource

## 字段说明：在编辑器中暴露设施唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var facility_id: String = ""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：在编辑器中暴露类别配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var category: String = ""
## 字段说明：在编辑器中暴露最小聚落等级层级参数，用于定义该对象生效或生成时的下限条件。
@export var min_settlement_tier := 0
## 字段说明：在编辑器中暴露允许槽位标签集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var allowed_slot_tags: Array[String] = []
## 字段说明：在编辑器中暴露绑定服务NPC集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var bound_service_npcs: Array = []
## 字段说明：在编辑器中暴露交互类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var interaction_type: String = ""


func get_primary_service_name() -> String:
	if bound_service_npcs.is_empty():
		return interaction_type.capitalize()

	return bound_service_npcs[0].service_type.capitalize()
