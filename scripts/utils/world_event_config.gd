## 文件说明：该脚本属于世界事件配置相关的配置资源脚本，集中维护事件坐标、发现条件和事件动作等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与世界运行时事件链之间的对应关系。
## 备注：后续如果扩展更多事件类型，需要同步检查 world spawn、runtime 事件分发和存档兼容逻辑。

class_name WorldEventConfig
extends Resource

const EVENT_TYPE_ENTER_SUBMAP: StringName = &"enter_submap"

## 字段说明：在编辑器中暴露事件唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var event_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：在编辑器中暴露世界坐标配置，便于直接控制事件入口在大地图中的挂载位置。
@export var world_coord: Vector2i = Vector2i.ZERO
## 字段说明：在编辑器中暴露事件类型配置，用于决定运行时应分发到哪条交互链。
@export var event_type: StringName = EVENT_TYPE_ENTER_SUBMAP
## 字段说明：在编辑器中暴露目标子地图标识配置，供进入子地图事件查表和持久化使用。
@export var target_submap_id: StringName = &""
## 字段说明：在编辑器中暴露发现条件标识配置，供运行时决定事件何时显现。
@export var discovery_condition_id: StringName = &"always_true"
## 字段说明：用于界面展示的确认标题文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var prompt_title: String = ""
## 字段说明：用于界面展示的确认说明文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export_multiline var prompt_text: String = ""
