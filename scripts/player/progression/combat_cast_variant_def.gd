## 文件说明：该脚本属于战斗施放变体定义相关的定义资源脚本，集中维护变体唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name CombatCastVariantDef
extends Resource

const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

## 字段说明：在编辑器中暴露变体唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var variant_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
@export_multiline var description: String = ""
## 字段说明：在编辑器中暴露最小技能等级参数，用于定义该对象生效或生成时的下限条件。
@export var min_skill_level := 0
## 字段说明：在编辑器中暴露目标模式配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var target_mode: StringName = &"ground"
## 字段说明：在编辑器中暴露占位图案配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var footprint_pattern: StringName = &"single"
## 字段说明：在编辑器中暴露所需坐标数量参数，便于直接调整生成数量、奖励数量或容量规模。
@export var required_coord_count := 1
## 字段说明：在编辑器中暴露允许的基础地形集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var allowed_base_terrains: Array[StringName] = []
## 字段说明：在编辑器中暴露效果定义集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var effect_defs: Array[CombatEffectDef] = []
