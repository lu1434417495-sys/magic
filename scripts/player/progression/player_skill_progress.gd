class_name PlayerSkillProgress
extends RefCounted

var skill_id: StringName = &""
var is_learned := false
var skill_level := 0
var current_mastery := 0
var total_mastery_earned := 0
var is_core := false
var assigned_profession_id: StringName = &""
var merged_from_skill_ids: Array[StringName] = []
var mastery_from_training := 0
var mastery_from_battle := 0
var profession_granted_by: StringName = &""


func is_max_level(max_level: int) -> bool:
	return skill_level >= max_level


func clear_profession_assignment() -> void:
	assigned_profession_id = &""


func to_dict() -> Dictionary:
	return {
		"skill_id": String(skill_id),
		"is_learned": is_learned,
		"skill_level": skill_level,
		"current_mastery": current_mastery,
		"total_mastery_earned": total_mastery_earned,
		"is_core": is_core,
		"assigned_profession_id": String(assigned_profession_id),
		"merged_from_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(merged_from_skill_ids),
		"mastery_from_training": mastery_from_training,
		"mastery_from_battle": mastery_from_battle,
		"profession_granted_by": String(profession_granted_by),
	}


static func from_dict(data: Dictionary) -> PlayerSkillProgress:
	var progress := PlayerSkillProgress.new()
	progress.skill_id = ProgressionDataUtils.to_string_name(data.get("skill_id", ""))
	progress.is_learned = bool(data.get("is_learned", false))
	progress.skill_level = int(data.get("skill_level", 0))
	progress.current_mastery = int(data.get("current_mastery", 0))
	progress.total_mastery_earned = int(data.get("total_mastery_earned", 0))
	progress.is_core = bool(data.get("is_core", false))
	progress.assigned_profession_id = ProgressionDataUtils.to_string_name(data.get("assigned_profession_id", ""))
	progress.merged_from_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("merged_from_skill_ids", []))
	progress.mastery_from_training = int(data.get("mastery_from_training", 0))
	progress.mastery_from_battle = int(data.get("mastery_from_battle", 0))
	progress.profession_granted_by = ProgressionDataUtils.to_string_name(data.get("profession_granted_by", ""))
	return progress

