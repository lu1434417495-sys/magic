class_name ProfessionPromotionRecord
extends RefCounted

var new_rank := 0
var consumed_skill_ids: Array[StringName] = []
var qualifier_skill_ids: Array[StringName] = []
var snapshot_base_attributes: Dictionary = {}
var timestamp := 0


func to_dict() -> Dictionary:
	return {
		"new_rank": new_rank,
		"consumed_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(consumed_skill_ids),
		"qualifier_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_ids),
		"snapshot_base_attributes": snapshot_base_attributes.duplicate(true),
		"timestamp": timestamp,
	}


static func from_dict(data: Dictionary) -> ProfessionPromotionRecord:
	var record := ProfessionPromotionRecord.new()
	record.new_rank = int(data.get("new_rank", 0))
	record.consumed_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("consumed_skill_ids", []))
	record.qualifier_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("qualifier_skill_ids", []))
	record.snapshot_base_attributes = data.get("snapshot_base_attributes", {}).duplicate(true)
	record.timestamp = int(data.get("timestamp", 0))
	return record
