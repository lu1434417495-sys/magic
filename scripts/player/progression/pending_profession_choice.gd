class_name PendingProfessionChoice
extends RefCounted

var trigger_skill_ids: Array[StringName] = []
var candidate_profession_ids: Array[StringName] = []
var target_rank_map: Dictionary = {}
var qualifier_skill_pool_ids: Array[StringName] = []
var assignable_skill_candidate_ids: Array[StringName] = []
var required_qualifier_count := 0
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
