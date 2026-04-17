## 文件说明：该脚本属于设施NPC配置相关的配置资源脚本，集中维护NPC唯一标识、显示名称、服务类型等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name FacilityNpcConfig
extends Resource

## 字段说明：在编辑器中暴露NPC唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var npc_id: String = ""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：在编辑器中暴露服务类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var service_type: String = ""
## 字段说明：在编辑器中暴露交互脚本唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var interaction_script_id: String = ""
## 字段说明：在编辑器中暴露本地槽位唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var local_slot_id: String = ""


func get_template_id() -> String:
	return npc_id.strip_edges()
