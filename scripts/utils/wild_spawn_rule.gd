## 文件说明：该脚本属于野外生成规则相关的工具脚本，集中维护区域标签、怪物名称、每区块密度等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name WildSpawnRule
extends Resource

## 字段说明：在编辑器中暴露区域标签配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var region_tag: String = ""
## 字段说明：在编辑器中暴露怪物名称配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var monster_name: String = "野怪"
## 字段说明：在编辑器中暴露敌方 roster 模板标识配置，供正式战斗 runtime 稳定映射敌方模板与 AI brain。
@export var enemy_roster_template_id: StringName = &""
## 字段说明：在编辑器中暴露正式遭遇编队标识，用于让世界遭遇显式命中混编 roster，而不是只落回单模板敌人。
@export var encounter_profile_id: StringName = &""
## 字段说明：在编辑器中暴露每区块密度配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var density_per_chunk := 1
## 字段说明：在编辑器中暴露距聚落的最小距离参数，用于定义该对象生效或生成时的下限条件。
@export var min_distance_to_settlement := 2
## 字段说明：在编辑器中暴露视野范围参数，便于直接调整尺寸、范围、间距或视图表现。
@export var vision_range := 1
## 字段说明：在编辑器中暴露区块坐标列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var chunk_coords: Array[Vector2i] = []
