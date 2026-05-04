## 文件说明：该脚本属于技能定义相关的定义资源脚本，集中维护技能唯一标识、显示名称、图标唯一标识等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name SkillDef
extends Resource

const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")

## 字段说明：在编辑器中暴露技能唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var skill_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：在编辑器中暴露图标唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var icon_id: StringName = &""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
@export_multiline var description: String = ""
## 字段说明：在编辑器中暴露技能类型配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var skill_type: StringName = &"active"
## 字段说明：在编辑器中暴露最大等级参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export var max_level := 1
## 字段说明：非核心状态下的有效等级上限；0 表示不低于 max_level 另行限制。
@export var non_core_max_level := 0
## 字段说明：动态等级上限读取的属性或 custom stat；为空时使用静态 max_level。
@export var dynamic_max_level_stat_id: StringName = &""
## 字段说明：动态等级上限基础值；配置 dynamic_max_level_stat_id 后作为公式起点。
@export var dynamic_max_level_base := 0
## 字段说明：动态等级上限随 stat 每点增加的等级数；配置 dynamic_max_level_stat_id 后必须为正数。
@export var dynamic_max_level_per_stat := 0
## 字段说明：在编辑器中暴露熟练度曲线配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var mastery_curve: PackedInt32Array = PackedInt32Array()
## 字段说明：在编辑器中暴露标签集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var tags: Array[StringName] = []
## 字段说明：在编辑器中暴露学习来源配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var learn_source: StringName = &"book"
## 字段说明：在编辑器中暴露学习条件集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var learn_requirements: Array[StringName] = []
## 字段说明：在编辑器中暴露解锁模式配置，便于描述普通学习、复合升级等不同来源。
@export var unlock_mode: StringName = &"standard"
## 字段说明：在编辑器中暴露知识前置集合配置，便于策划表达文档中的知识门槛。
@export var knowledge_requirements: Array[StringName] = []
## 字段说明：在编辑器中暴露技能等级前置映射，键为技能标识、值为所需等级。
@export var skill_level_requirements: Dictionary = {}
## 字段说明：在编辑器中暴露基础属性前置映射，键为属性标识、值为所需数值。
@export var attribute_requirements: Dictionary = {}
## 字段说明：在编辑器中暴露成就前置集合配置，便于策划表达成就解锁条件。
@export var achievement_requirements: Array[StringName] = []
## 字段说明：在编辑器中暴露升级来源技能集合配置，用于记录复合技能的来源血缘。
@export var upgrade_source_skill_ids: Array[StringName] = []
## 字段说明：在编辑器中暴露是否保留来源技能配置，用于兼容非破坏式合并规则。
@export var retain_source_skills_on_unlock := true
## 字段说明：在编辑器中暴露核心技能切换模式配置，便于后续职业核心位分配规则读取。
@export var core_skill_transition_mode: StringName = &"inherit"
## 字段说明：在编辑器中暴露熟练度来源列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var mastery_sources: Array[StringName] = []
## 字段说明：在编辑器中暴露满级成长档位；未配置属性成长时可留空。
@export var growth_tier: StringName = &""
## 字段说明：技能达到当前允许最高等级后提供的基础属性进度，键为基础属性标识、值为进度量。
@export var attribute_growth_progress: Dictionary = {}
## 字段说明：在编辑器中暴露属性修正列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var attribute_modifiers: Array[AttributeModifier] = []
## 字段说明：等级描述模板字符串，支持 {key} 变量替换和 {{?key}}...{{/key}} 条件块；
## 条件块在配置中存在对应 key 且值非空时保留内容，否则整段删除。
@export_multiline var level_description_template: String = ""
## 字段说明：按技能等级提供的模板变量配置字典；每个等级只写需要显示的字段，
## 未写的字段对应条件块自动隐藏，不需要显式写 false 或空字符串。
@export var level_description_configs: Dictionary = {}
## 字段说明：在编辑器中暴露战斗配置档配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var combat_profile: CombatSkillDef


func get_mastery_required_for_level(level: int) -> int:
	if level < 0:
		return 0
	if level < mastery_curve.size():
		return mastery_curve[level]
	if mastery_curve.size() <= 0:
		return 0
	if mastery_curve.size() == 1:
		return int(mastery_curve[0])
	var last_index := mastery_curve.size() - 1
	var delta := maxi(int(mastery_curve[last_index]) - int(mastery_curve[last_index - 1]), 1)
	return int(mastery_curve[last_index]) + delta * (level - last_index)

func is_profession_skill() -> bool:
	return learn_source == &"profession"


func can_use_in_combat() -> bool:
	return combat_profile != null
