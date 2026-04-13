## 文件说明：该脚本属于待处理职业选择相关的业务脚本，集中维护触发技能标识列表、候选职业标识列表、目标阶位映射表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name PendingProfessionChoice
extends RefCounted

## 字段说明：保存触发技能标识列表，便于批量遍历、交叉查找和界面展示。
var trigger_skill_ids: Array[StringName] = []
## 字段说明：保存候选职业标识列表，便于批量遍历、交叉查找和界面展示。
var candidate_profession_ids: Array[StringName] = []
## 字段说明：按键缓存目标阶位映射表，便于在较多对象中快速定位目标并减少重复遍历。
var target_rank_map: Dictionary = {}
## 字段说明：保存资格技能池标识列表，便于批量遍历、交叉查找和界面展示。
var qualifier_skill_pool_ids: Array[StringName] = []
## 字段说明：保存可指派技能候选标识列表，便于批量遍历、交叉查找和界面展示。
var assignable_skill_candidate_ids: Array[StringName] = []
## 字段说明：记录所需资格数量，用于控制生成规模、容量限制或批次数量。
var required_qualifier_count := 0
## 字段说明：记录所需已指派核心数量，用于控制生成规模、容量限制或批次数量。
var required_assigned_core_count := 0


func set_target_rank(profession_id: StringName, target_rank: int) -> void:
	target_rank_map[profession_id] = target_rank


func to_dict() -> Dictionary:
	return {
		"trigger_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(trigger_skill_ids),
		"candidate_profession_ids": ProgressionDataUtils.string_name_array_to_string_array(candidate_profession_ids),
		"target_rank_map": ProgressionDataUtils.string_name_int_map_to_string_dict(target_rank_map),
		"qualifier_skill_pool_ids": ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_pool_ids),
		"assignable_skill_candidate_ids": ProgressionDataUtils.string_name_array_to_string_array(assignable_skill_candidate_ids),
		"required_qualifier_count": required_qualifier_count,
		"required_assigned_core_count": required_assigned_core_count,
	}


static func from_dict(data: Dictionary) -> PendingProfessionChoice:
	var choice := PendingProfessionChoice.new()
	choice.trigger_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("trigger_skill_ids", []))
	choice.candidate_profession_ids = ProgressionDataUtils.to_string_name_array(data.get("candidate_profession_ids", []))
	choice.target_rank_map = ProgressionDataUtils.to_string_name_int_map(data.get("target_rank_map", {}))
	choice.qualifier_skill_pool_ids = ProgressionDataUtils.to_string_name_array(data.get("qualifier_skill_pool_ids", []))
	choice.assignable_skill_candidate_ids = ProgressionDataUtils.to_string_name_array(data.get("assignable_skill_candidate_ids", []))
	choice.required_qualifier_count = int(data.get("required_qualifier_count", 0))
	choice.required_assigned_core_count = int(data.get("required_assigned_core_count", 0))
	return choice
