class_name QuestDef
extends Resource

const QUEST_DEF_SCRIPT = preload("res://scripts/player/progression/quest_def.gd")
const PENDING_CHARACTER_REWARD_CONTENT_RULES = preload("res://scripts/player/progression/pending_character_reward_content_rules.gd")

const OBJECTIVE_SUBMIT_ITEM: StringName = &"submit_item"
const OBJECTIVE_DEFEAT_ENEMY: StringName = &"defeat_enemy"
const OBJECTIVE_SETTLEMENT_ACTION: StringName = &"settlement_action"

const REWARD_GOLD: StringName = &"gold"
const REWARD_ITEM: StringName = &"item"
const REWARD_PENDING_CHARACTER_REWARD: StringName = &"pending_character_reward"

const REQUIRED_SERIALIZED_FIELDS := [
	"quest_id",
	"display_name",
	"description",
	"provider_interaction_id",
	"tags",
	"accept_requirements",
	"objective_defs",
	"reward_entries",
	"is_repeatable",
]

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
	if display_name.strip_edges().is_empty():
		errors.append("QuestDef %s 缺少 display_name。" % String(quest_id))
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
		if not objective_data.has("target_value") or objective_data["target_value"] is not int:
			errors.append("QuestDef %s 的 objective %s 必须显式提供 int target_value。" % [String(quest_id), String(objective_id)])
			continue
		var target_value := int(objective_data["target_value"])
		if target_value <= 0:
			errors.append("QuestDef %s 的 objective %s 必须有正 target_value。" % [String(quest_id), String(objective_id)])
		if objective_type == OBJECTIVE_SUBMIT_ITEM:
			var submit_item_id := ProgressionDataUtils.to_string_name(objective_data.get("target_id", ""))
			if submit_item_id == &"":
				errors.append("QuestDef %s 的 submit_item objective %s 缺少 target_id。" % [
					String(quest_id),
					String(objective_id),
				])

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
			continue
		match reward_type:
			REWARD_GOLD:
				if not reward_data.has("amount") or reward_data["amount"] is not int or int(reward_data["amount"]) <= 0:
					errors.append("QuestDef %s 的 gold reward 必须有正 amount。" % String(quest_id))
			REWARD_ITEM:
				var reward_item_id := get_reward_item_id(reward_data)
				if reward_item_id == &"":
					errors.append("QuestDef %s 的 item reward 缺少 item_id。" % String(quest_id))
				if get_reward_quantity(reward_data) <= 0:
					errors.append("QuestDef %s 的 item reward 必须有正 quantity。" % String(quest_id))
			REWARD_PENDING_CHARACTER_REWARD:
				errors.append_array(_validate_pending_character_reward(quest_id, reward_data))
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


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if not _has_exact_serialized_fields(payload):
		return null

	var quest_id_value := _read_required_string_name(payload["quest_id"])
	var provider_interaction_id_value := _read_required_string_name(payload["provider_interaction_id"])
	if quest_id_value == &"" or provider_interaction_id_value == &"":
		return null

	var display_name_variant: Variant = payload["display_name"]
	if display_name_variant is not String:
		return null
	var display_name_value := String(display_name_variant)
	if display_name_value.strip_edges().is_empty():
		return null

	var description_variant: Variant = payload["description"]
	if description_variant is not String:
		return null

	var tags_variant: Variant = payload["tags"]
	if tags_variant is not Array:
		return null
	var tag_values: Array[StringName] = []
	for tag_variant in tags_variant:
		var tag_value := _read_required_string_name(tag_variant)
		if tag_value == &"":
			return null
		tag_values.append(tag_value)

	var accept_requirements_variant: Variant = payload["accept_requirements"]
	if accept_requirements_variant is not Array:
		return null
	var accept_requirement_values: Array[Dictionary] = []
	for requirement_variant in accept_requirements_variant:
		if requirement_variant is not Dictionary:
			return null
		accept_requirement_values.append((requirement_variant as Dictionary).duplicate(true))

	var objective_defs_variant: Variant = payload["objective_defs"]
	if objective_defs_variant is not Array:
		return null
	if (objective_defs_variant as Array).is_empty():
		return null
	var objective_def_values: Array[Dictionary] = []
	for objective_variant in objective_defs_variant:
		if objective_variant is not Dictionary:
			return null
		objective_def_values.append((objective_variant as Dictionary).duplicate(true))

	var reward_entries_variant: Variant = payload["reward_entries"]
	if reward_entries_variant is not Array:
		return null
	var reward_entry_values: Array[Dictionary] = []
	for reward_variant in reward_entries_variant:
		if reward_variant is not Dictionary:
			return null
		reward_entry_values.append((reward_variant as Dictionary).duplicate(true))

	if payload["is_repeatable"] is not bool:
		return null

	var quest_def = QUEST_DEF_SCRIPT.new()
	quest_def.quest_id = quest_id_value
	quest_def.display_name = display_name_value
	quest_def.description = String(description_variant)
	quest_def.provider_interaction_id = provider_interaction_id_value
	quest_def.tags = tag_values
	quest_def.accept_requirements = accept_requirement_values
	quest_def.objective_defs = objective_def_values
	quest_def.reward_entries = reward_entry_values
	quest_def.is_repeatable = bool(payload["is_repeatable"])
	if not quest_def.validate_schema().is_empty():
		return null
	return quest_def


static func _has_exact_serialized_fields(payload: Dictionary) -> bool:
	if payload.size() != REQUIRED_SERIALIZED_FIELDS.size():
		return false
	for field_name in REQUIRED_SERIALIZED_FIELDS:
		if not payload.has(field_name):
			return false
	return true


static func _read_required_string_name(value: Variant) -> StringName:
	if value is not String and value is not StringName:
		return &""
	var text := String(value)
	if text.strip_edges().is_empty():
		return &""
	return StringName(text)


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


static func get_reward_item_id(reward_data: Dictionary) -> StringName:
	if not reward_data.has("item_id"):
		return &""
	var item_id_variant: Variant = reward_data["item_id"]
	if item_id_variant is not String and item_id_variant is not StringName:
		return &""
	return ProgressionDataUtils.to_string_name(item_id_variant)


static func get_reward_quantity(reward_data: Dictionary) -> int:
	if not reward_data.has("quantity") or reward_data["quantity"] is not int:
		return 0
	return int(reward_data["quantity"])


static func _validate_pending_character_reward(quest_id_value: StringName, reward_data: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	var quest_id_text := String(quest_id_value)
	var member_id := ProgressionDataUtils.to_string_name(reward_data.get("member_id", ""))
	if member_id == &"":
		errors.append("QuestDef %s 的 pending_character_reward 缺少 member_id。" % quest_id_text)

	var entries_variant: Variant = reward_data.get("entries", [])
	if entries_variant is not Array or (entries_variant as Array).is_empty():
		errors.append("QuestDef %s 的 pending_character_reward 至少需要一条 entries。" % quest_id_text)
		return errors

	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			errors.append("QuestDef %s 的 pending_character_reward 包含非 Dictionary entry。" % quest_id_text)
			continue
		var entry_data := entry_variant as Dictionary
		var entry_type := ProgressionDataUtils.to_string_name(entry_data.get("entry_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
		var amount := int(entry_data.get("amount", 0))
		if entry_type == &"":
			errors.append("QuestDef %s 的 pending_character_reward entry 缺少 entry_type。" % quest_id_text)
		elif not PENDING_CHARACTER_REWARD_CONTENT_RULES.is_supported_entry_type(entry_type):
			errors.append(
				"QuestDef %s has unsupported pending_character_reward entry_type %s. Supported: %s." % [
					quest_id_text,
					String(entry_type),
					PENDING_CHARACTER_REWARD_CONTENT_RULES.valid_entry_type_label(),
				]
			)
		if target_id == &"":
			errors.append("QuestDef %s 的 pending_character_reward entry 缺少 target_id。" % quest_id_text)
		if amount == 0:
			errors.append("QuestDef %s 的 pending_character_reward entry amount 不能为 0。" % quest_id_text)
	return errors
