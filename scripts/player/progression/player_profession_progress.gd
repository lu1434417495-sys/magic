class_name PlayerProfessionProgress
extends RefCounted

var profession_id: StringName = &""
var rank := 0
var is_active := true
var is_hidden := false
var core_skill_ids: Array[StringName] = []
var granted_skill_ids: Array[StringName] = []
var promotion_history: Array[ProfessionPromotionRecord] = []
var inactive_reason: StringName = &""


func add_core_skill(skill_id: StringName) -> void:
	if core_skill_ids.has(skill_id):
		return
	core_skill_ids.append(skill_id)


func remove_core_skill(skill_id: StringName) -> void:
	core_skill_ids.erase(skill_id)


func add_granted_skill(skill_id: StringName) -> void:
	if granted_skill_ids.has(skill_id):
		return
	granted_skill_ids.append(skill_id)


func add_promotion_record(record: ProfessionPromotionRecord) -> void:
	if record == null:
		return
	promotion_history.append(record)


func to_dict() -> Dictionary:
	var promotion_history_data: Array[Dictionary] = []
	for record in promotion_history:
		if record != null:
			promotion_history_data.append(record.to_dict())

	return {
		"profession_id": String(profession_id),
		"rank": rank,
		"is_active": is_active,
		"is_hidden": is_hidden,
		"core_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(core_skill_ids),
		"granted_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(granted_skill_ids),
		"promotion_history": promotion_history_data,
		"inactive_reason": String(inactive_reason),
	}


static func from_dict(data: Dictionary) -> PlayerProfessionProgress:
	var progress := PlayerProfessionProgress.new()
	progress.profession_id = ProgressionDataUtils.to_string_name(data.get("profession_id", ""))
	progress.rank = int(data.get("rank", 0))
	progress.is_active = bool(data.get("is_active", true))
	progress.is_hidden = bool(data.get("is_hidden", false))
	progress.core_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("core_skill_ids", []))
	progress.granted_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("granted_skill_ids", []))
	progress.inactive_reason = ProgressionDataUtils.to_string_name(data.get("inactive_reason", ""))

	var promotion_history_data: Array = data.get("promotion_history", [])
	for record_data in promotion_history_data:
		if record_data is Dictionary:
			progress.promotion_history.append(ProfessionPromotionRecord.from_dict(record_data))

	return progress

