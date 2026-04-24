## 文件说明：该脚本属于战斗效果定义相关的定义资源脚本，集中维护效果类型、跳点效果类型、强度等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name CombatEffectDef
extends Resource

## 字段说明：在编辑器中暴露效果类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_type: StringName = &""
## 字段说明：在编辑器中暴露跳点效果类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tick_effect_type: StringName = &""
## 字段说明：在编辑器中暴露强度配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var power := 0
## 字段说明：在编辑器中暴露伤害倍率百分比配置，便于定义相对基准伤害而非固定强度。
@export var damage_ratio_percent := 100
## 字段说明：在编辑器中暴露伤害标签配置，便于命中后选择抗性、减伤和日志语义。
@export var damage_tag: StringName = &""
## 字段说明：在编辑器中暴露抗性属性唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var resistance_attribute_id: StringName = &""
## 字段说明：在编辑器中暴露效果目标队伍过滤配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_target_team_filter: StringName = &""
## 字段说明：在编辑器中暴露状态唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var status_id: StringName = &""
## 字段说明：在编辑器中暴露地形效果唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var terrain_effect_id: StringName = &""
## 字段说明：在编辑器中暴露地形替换配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var terrain_replace_to: StringName = &""
## 字段说明：在编辑器中暴露高度增量配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var height_delta := 0
## 字段说明：在编辑器中暴露强制位移模式配置，用于表达击退、拉拽、跳斩等位移效果。
@export var forced_move_mode: StringName = &""
## 字段说明：在编辑器中暴露强制位移距离配置，用于表达击退、拉拽等位移数值。
@export var forced_move_distance := 0
## 字段说明：在编辑器中暴露持续时间TU参数，便于直接调整尺寸、范围、间距或视图表现。
@export var duration_tu := 0
## 字段说明：在编辑器中暴露跳点间隔TU配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tick_interval_tu := 0
## 字段说明：在编辑器中暴露堆叠行为配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var stack_behavior: StringName = &"refresh"
## 字段说明：在编辑器中暴露叠层上限配置，便于后续状态或特效链按统一规则约束堆叠。
@export var stack_limit := 0
## 字段说明：在编辑器中暴露额外条件标识配置，便于定义低血、潮湿、护甲破碎等条件增益。
@export var bonus_condition: StringName = &""
## 字段说明：在编辑器中暴露触发事件标识配置，便于描述命中后、击杀后等触发型效果。
@export var trigger_event: StringName = &""
## 字段说明：在编辑器中暴露参数配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var params: Dictionary = {}
