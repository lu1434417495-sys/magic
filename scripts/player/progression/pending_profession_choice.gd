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
	for field_name in [
		"trigger_skill_ids",
		"candidate_profession_ids",
		"target_rank_map",
		"qualifier_skill_pool_ids",
		"assignable_skill_candidate_ids",
		"required_qualifier_count",
		"required_assigned_core_count",
	]:
		if not data.has(field_name):
			return null
	var trigger_skill_ids_variant: Variant = data["trigger_skill_ids"]
	var candidate_profession_ids_variant: Variant = data["candidate_profession_ids"]
	var target_rank_map_variant: Variant = data["target_rank_map"]
	var qualifier_skill_pool_ids_variant: Variant = data["qualifier_skill_pool_ids"]
	var assignable_skill_candidate_ids_variant: Variant = data["assignable_skill_candidate_ids"]
	if trigger_skill_ids_variant is not Array:
		return null
	if candidate_profession_ids_variant is not Array:
		return null
	if target_rank_map_variant is not Dictionary:
		return null
	if qualifier_skill_pool_ids_variant is not Array:
		return null
	if assignable_skill_candidate_ids_variant is not Array:
		return null
	var trigger_skill_ids = _parse_unique_string_name_array(trigger_skill_ids_variant)
	if trigger_skill_ids == null:
		return null
	var candidate_profession_ids = _parse_unique_string_name_array(candidate_profession_ids_variant)
	if candidate_profession_ids == null:
		return null
	var target_rank_map = _parse_nonnegative_int_map(target_rank_map_variant)
	if target_rank_map == null:
		return null
	var qualifier_skill_pool_ids = _parse_unique_string_name_array(qualifier_skill_pool_ids_variant)
	if qualifier_skill_pool_ids == null:
		return null
	var assignable_skill_candidate_ids = _parse_unique_string_name_array(assignable_skill_candidate_ids_variant)
	if assignable_skill_candidate_ids == null:
		return null
	var required_qualifier_count_variant: Variant = data["required_qualifier_count"]
	if required_qualifier_count_variant is not int or int(required_qualifier_count_variant) < 0:
		return null
	var required_assigned_core_count_variant: Variant = data["required_assigned_core_count"]
	if required_assigned_core_count_variant is not int or int(required_assigned_core_count_variant) < 0:
		return null

	var choice := PendingProfessionChoice.new()
	choice.trigger_skill_ids = trigger_skill_ids
	choice.candidate_profession_ids = candidate_profession_ids
	choice.target_rank_map = target_rank_map
	choice.qualifier_skill_pool_ids = qualifier_skill_pool_ids
	choice.assignable_skill_candidate_ids = assignable_skill_candidate_ids
	choice.required_qualifier_count = int(required_qualifier_count_variant)
	choice.required_assigned_core_count = int(required_assigned_core_count_variant)
	return choice


static func _parse_string_name_field(value: Variant):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"":
		return null
	return parsed_value


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_string_name_field(raw_value)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values


static func _parse_nonnegative_int_map(values: Dictionary):
	var parsed_values: Dictionary = {}
	var seen_keys: Dictionary = {}
	for raw_key in values.keys():
		var parsed_key = _parse_string_name_field(raw_key)
		if parsed_key == null or seen_keys.has(parsed_key):
			return null
		var raw_value: Variant = values[raw_key]
		if raw_value is not int or int(raw_value) < 0:
			return null
		seen_keys[parsed_key] = true
		parsed_values[parsed_key] = int(raw_value)
	return parsed_values
