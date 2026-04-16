class_name QuestDef
extends Resource

const QUEST_DEF_SCRIPT = preload("res://scripts/player/progression/quest_def.gd")

const OBJECTIVE_SUBMIT_ITEM: StringName = &"submit_item"
const OBJECTIVE_DEFEAT_ENEMY: StringName = &"defeat_enemy"
const OBJECTIVE_SETTLEMENT_ACTION: StringName = &"settlement_action"

const REWARD_GOLD: StringName = &"gold"
const REWARD_ITEM: StringName = &"item"
const REWARD_PENDING_CHARACTER_REWARD: StringName = &"pending_character_reward"

@export var quest_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var provider_interaction_id: StringName = &""
@export var tags: Array[StringName] = []
@export var accept_requirements: Array[Dictionary] = []
@export var objective_defs: Array[Dictionary] = []
@export var reward_entries: Array[Dictionary] = []
@export var is_repeatable := false


func is_empty() -> bool:
	return quest_id == &""


func get_objective_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for objective_variant in objective_defs:
		if objective_variant is not Dictionary:
			continue
		var objective_id := ProgressionDataUtils.to_string_name((objective_variant as Dictionary).get("objective_id", ""))
		if objective_id != &"":
			result.append(objective_id)
	return result


func get_objective_def(objective_id: StringName) -> Dictionary:
	for objective_variant in objective_defs:
		if objective_variant is not Dictionary:
			continue
		var objective_data := objective_variant as Dictionary
		if ProgressionDataUtils.to_string_name(objective_data.get("objective_id", "")) == objective_id:
			return objective_data.duplicate(true)
	return {}


func validate_schema() -> Array[String]:
	var errors: Array[String] = []
	if quest_id == &"":
		errors.append("QuestDef 缺少 quest_id。")
	if objective_defs.is_empty():
		errors.append("QuestDef %s 至少需要一个 objective_def。" % String(quest_id))

	var seen_objective_ids: Dictionary = {}
	for objective_variant in objective_defs:
		if objective_variant is not Dictionary:
			errors.append("QuestDef %s 包含非 Dictionary objective_def。" % String(quest_id))
			continue
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		var objective_type := ProgressionDataUtils.to_string_name(objective_data.get("objective_type", ""))
		var target_value := int(objective_data.get("target_value", 1))
		if objective_id == &"":
			errors.append("QuestDef %s 存在空 objective_id。" % String(quest_id))
			continue
		if seen_objective_ids.has(objective_id):
			errors.append("QuestDef %s 存在重复 objective_id %s。" % [String(quest_id), String(objective_id)])
			continue
		seen_objective_ids[objective_id] = true
		if objective_type == &"":
			errors.append("QuestDef %s 的 objective %s 缺少 objective_type。" % [String(quest_id), String(objective_id)])
		elif not _get_supported_objective_types().has(objective_type):
			errors.append("QuestDef %s 的 objective %s 使用了不支持的 objective_type %s。" % [
				String(quest_id),
				String(objective_id),
				String(objective_type),
			])
		if target_value <= 0:
			errors.append("QuestDef %s 的 objective %s 必须有正 target_value。" % [String(quest_id), String(objective_id)])

	for reward_variant in reward_entries:
		if reward_variant is not Dictionary:
			errors.append("QuestDef %s 包含非 Dictionary reward_entry。" % String(quest_id))
			continue
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data.get("reward_type", ""))
		if reward_type == &"":
			errors.append("QuestDef %s 存在缺少 reward_type 的 reward_entry。" % String(quest_id))
			continue
		if not _get_supported_reward_types().has(reward_type):
			errors.append("QuestDef %s 使用了不支持的 reward_type %s。" % [String(quest_id), String(reward_type)])
	return errors


func to_dict() -> Dictionary:
	return {
		"quest_id": String(quest_id),
		"display_name": display_name,
		"description": description,
		"provider_interaction_id": String(provider_interaction_id),
		"tags": ProgressionDataUtils.string_name_array_to_string_array(tags),
		"accept_requirements": accept_requirements.duplicate(true),
		"objective_defs": objective_defs.duplicate(true),
		"reward_entries": reward_entries.duplicate(true),
		"is_repeatable": is_repeatable,
	}


static func from_dict(data: Dictionary):
	var quest_def = QUEST_DEF_SCRIPT.new()
	quest_def.quest_id = ProgressionDataUtils.to_string_name(data.get("quest_id", ""))
	quest_def.display_name = String(data.get("display_name", ""))
	quest_def.description = String(data.get("description", ""))
	quest_def.provider_interaction_id = ProgressionDataUtils.to_string_name(data.get("provider_interaction_id", ""))
	quest_def.tags = ProgressionDataUtils.to_string_name_array(data.get("tags", []))

	var accept_requirements_variant: Variant = data.get("accept_requirements", [])
	if accept_requirements_variant is Array:
		for requirement_variant in accept_requirements_variant:
			if requirement_variant is Dictionary:
				quest_def.accept_requirements.append((requirement_variant as Dictionary).duplicate(true))

	var objective_defs_variant: Variant = data.get("objective_defs", [])
	if objective_defs_variant is Array:
		for objective_variant in objective_defs_variant:
			if objective_variant is Dictionary:
				quest_def.objective_defs.append((objective_variant as Dictionary).duplicate(true))

	var reward_entries_variant: Variant = data.get("reward_entries", [])
	if reward_entries_variant is Array:
		for reward_variant in reward_entries_variant:
			if reward_variant is Dictionary:
				quest_def.reward_entries.append((reward_variant as Dictionary).duplicate(true))

	quest_def.is_repeatable = bool(data.get("is_repeatable", false))
	return quest_def


static func _get_supported_objective_types() -> Array[StringName]:
	return [
		OBJECTIVE_SUBMIT_ITEM,
		OBJECTIVE_DEFEAT_ENEMY,
		OBJECTIVE_SETTLEMENT_ACTION,
	]


static func _get_supported_reward_types() -> Array[StringName]:
	return [
		REWARD_GOLD,
		REWARD_ITEM,
		REWARD_PENDING_CHARACTER_REWARD,
	]
