## 文件说明：该脚本属于职业定义相关的定义资源脚本，集中维护职业唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProfessionDef
extends Resource

## 字段说明：在编辑器中暴露职业唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var profession_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
@export var display_name: String = ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
@export_multiline var description: String = ""
## 字段说明：在编辑器中暴露最大阶位参数，用于限制该对象可达到的上限并控制成长或容量边界。
@export var max_rank := 1
## 字段说明：在编辑器中暴露是否初始职业配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var is_initial_profession := false
## 字段说明：在编辑器中暴露解锁知识唯一标识配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var unlock_knowledge_id: StringName = &""
## 字段说明：在编辑器中暴露解锁条件配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var unlock_requirement: ProfessionPromotionRequirement
## 字段说明：在编辑器中暴露阶位条件集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var rank_requirements: Array[ProfessionRankRequirement] = []
## 字段说明：在编辑器中暴露授予技能集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var granted_skills: Array[ProfessionGrantedSkill] = []
## 字段说明：在编辑器中暴露属性修正列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var attribute_modifiers: Array[AttributeModifier] = []
## 字段说明：在编辑器中暴露激活条件集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var active_conditions: Array[ProfessionActiveCondition] = []
## 字段说明：在编辑器中暴露重新激活模式配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var reactivation_mode: StringName = &"auto"
## 字段说明：在编辑器中配置依赖项可见性模式，用于决定隐藏依赖是否仍参与职业规则统计。
@export var dependency_visibility_mode: StringName = &"count_when_hidden"


func requires_knowledge_unlock() -> bool:
	return not is_initial_profession


func get_rank_requirement(target_rank: int) -> ProfessionRankRequirement:
	for requirement in rank_requirements:
		if requirement != null and requirement.target_rank == target_rank:
			return requirement
	return null


func get_granted_skills_for_rank(target_rank: int) -> Array[ProfessionGrantedSkill]:
	var result: Array[ProfessionGrantedSkill] = []
	for granted_skill in granted_skills:
		if granted_skill != null and granted_skill.unlock_rank == target_rank:
			result.append(granted_skill)
	return result
