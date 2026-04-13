## 文件说明：该脚本属于职业晋升条件相关的业务脚本，集中维护要求技能标识列表、要求标签规则集合、要求职业阶位集合等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProfessionPromotionRequirement
extends Resource

## 字段说明：在编辑器中暴露要求技能标识列表配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_skill_ids: Array[StringName] = []
## 字段说明：在编辑器中暴露要求标签规则集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_tag_rules: Array[TagRequirement] = []
## 字段说明：在编辑器中暴露要求职业阶位集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_profession_ranks: Array[ProfessionRankGate] = []
## 字段说明：在编辑器中暴露要求属性规则集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_attribute_rules: Array[AttributeRequirement] = []
## 字段说明：在编辑器中暴露要求声望规则集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_reputation_rules: Array[ReputationRequirement] = []
## 字段说明：标记已分配的核心技能是否必须来自资格技能集合，用于约束职业晋升时的可选范围。
@export var assigned_core_must_be_subset_of_qualifiers := false


func is_empty() -> bool:
	return required_skill_ids.is_empty() \
		and required_tag_rules.is_empty() \
		and required_profession_ranks.is_empty() \
		and required_attribute_rules.is_empty() \
		and required_reputation_rules.is_empty()
