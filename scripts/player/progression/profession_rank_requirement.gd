## 文件说明：该脚本属于职业阶位条件相关的业务脚本，集中维护目标阶位、要求标签规则集合、要求职业阶位集合等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name ProfessionRankRequirement
extends Resource

## 字段说明：在编辑器中暴露目标阶位配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var target_rank := 1
## 字段说明：在编辑器中暴露要求标签规则集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_tag_rules: Array[TagRequirement] = []
## 字段说明：在编辑器中暴露要求职业阶位集合配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export var required_profession_ranks: Array[ProfessionRankGate] = []


func is_empty() -> bool:
	return required_tag_rules.is_empty() and required_profession_ranks.is_empty()

