## 文件说明：该脚本集中维护任务内容的正式 domain 校验，包括 schema、注册错误与跨表引用。
## 审查重点：新增 quest objective/reward/provider 语义时，需要在这里补齐对应引用检查，避免测试 helper 与运行时快照分叉。
## 备注：ProgressionContentRegistry 仍负责构建任务定义；本脚本负责把任务错误统一归入 quest validation domain。

class_name QuestContentValidator
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const PendingCharacterRewardContentRules = preload("res://scripts/player/progression/pending_character_reward_content_rules.gd")
const QuestProviderContentRules = preload("res://scripts/player/progression/quest_provider_content_rules.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")


static func validate(
	quest_defs: Dictionary,
	item_defs: Dictionary = {},
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {},
	registration_errors: Array = [],
	provider_ids: Dictionary = {}
) -> Array[String]:
	var quest_entries: Array[Dictionary] = []
	for quest_key in ProgressionDataUtils.sorted_string_keys(quest_defs):
		var quest_id := StringName(quest_key)
		quest_entries.append({
			"source": "quest_defs::%s" % String(quest_id),
			"quest_def": _get_dict_value_by_id(quest_defs, quest_id),
		})
	return validate_entries(
		"quest_defs",
		quest_entries,
		item_defs,
		skill_defs,
		enemy_templates,
		registration_errors,
		provider_ids
	)


static func validate_entries(
	label: String,
	quest_entries: Array[Dictionary],
	item_defs: Dictionary = {},
	skill_defs: Dictionary = {},
	enemy_templates: Dictionary = {},
	registration_errors: Array = [],
	provider_ids: Dictionary = {}
) -> Array[String]:
	var errors: Array[String] = []
	for registration_error in registration_errors:
		errors.append(String(registration_error))

	var seen_quest_ids: Dictionary = {}
	var supported_provider_ids := _resolve_provider_ids(provider_ids)
	for entry in quest_entries:
		var source_label := String(entry.get("source", label))
		var quest_def := entry.get("quest_def") as QuestDef
		if quest_def == null:
			errors.append("Quest entry %s failed to cast to QuestDef." % source_label)
			continue
		if quest_def.quest_id == &"":
			errors.append("Quest entry %s is missing quest_id." % source_label)
			continue
		if seen_quest_ids.has(quest_def.quest_id):
			errors.append("Duplicate quest_id registered: %s" % String(quest_def.quest_id))
			continue
		seen_quest_ids[quest_def.quest_id] = true

		for schema_error in quest_def.validate_schema():
			errors.append("Quest %s: %s" % [String(quest_def.quest_id), schema_error])

		_append_provider_reference_errors(errors, quest_def, supported_provider_ids)
		_append_objective_reference_errors(errors, quest_def, item_defs, enemy_templates)
		_append_reward_reference_errors(errors, quest_def, item_defs, skill_defs)
	return errors


static func _append_provider_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	supported_provider_ids: Dictionary
) -> void:
	if quest_def.provider_interaction_id == &"":
		errors.append("Quest %s is missing provider_interaction_id." % String(quest_def.quest_id))
		return
	if not supported_provider_ids.has(quest_def.provider_interaction_id):
		errors.append(
			"Quest %s references missing provider_interaction_id %s." % [
				String(quest_def.quest_id),
				String(quest_def.provider_interaction_id),
			]
		)


static func _append_objective_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	item_defs: Dictionary,
	enemy_templates: Dictionary
) -> void:
	for objective_variant in quest_def.objective_defs:
		if objective_variant is not Dictionary:
			continue
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		var objective_type := ProgressionDataUtils.to_string_name(objective_data.get("objective_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(objective_data.get("target_id", ""))
		match objective_type:
			QuestDef.OBJECTIVE_SUBMIT_ITEM:
				if target_id != &"" and not item_defs.is_empty() and _get_dict_value_by_id(item_defs, target_id) == null:
					errors.append(
						"Quest %s submit_item objective %s references missing item %s." % [
							String(quest_def.quest_id),
							String(objective_id),
							String(target_id),
						]
					)
			QuestDef.OBJECTIVE_DEFEAT_ENEMY:
				if target_id != &"" and not enemy_templates.is_empty() and _get_dict_value_by_id(enemy_templates, target_id) == null:
					errors.append(
						"Quest %s defeat_enemy objective %s references missing enemy %s." % [
							String(quest_def.quest_id),
							String(objective_id),
							String(target_id),
						]
					)


static func _append_reward_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	item_defs: Dictionary,
	skill_defs: Dictionary
) -> void:
	for reward_variant in quest_def.reward_entries:
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data.get("reward_type", ""))
		match reward_type:
			QuestDef.REWARD_ITEM:
				var reward_item_id := QuestDef.get_reward_item_id(reward_data)
				if reward_item_id != &"" and not item_defs.is_empty() and _get_dict_value_by_id(item_defs, reward_item_id) == null:
					errors.append(
						"Quest %s reward references missing item %s." % [
							String(quest_def.quest_id),
							String(reward_item_id),
						]
					)
			QuestDef.REWARD_PENDING_CHARACTER_REWARD:
				_append_pending_character_reward_reference_errors(errors, quest_def, reward_data, skill_defs)


static func _append_pending_character_reward_reference_errors(
	errors: Array[String],
	quest_def: QuestDef,
	reward_data: Dictionary,
	skill_defs: Dictionary
) -> void:
	var entries_variant: Variant = reward_data.get("entries", [])
	if entries_variant is not Array:
		return
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry_data := entry_variant as Dictionary
		var entry_type := ProgressionDataUtils.to_string_name(entry_data.get("entry_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
		if entry_type == &"" or not PendingCharacterRewardContentRules.is_supported_entry_type(entry_type):
			continue
		if PendingCharacterRewardContentRules.requires_skill_target(entry_type):
			if target_id != &"" and not skill_defs.is_empty() and _get_dict_value_by_id(skill_defs, target_id) == null:
				errors.append(
					"Quest %s pending_character_reward references missing skill %s." % [
						String(quest_def.quest_id),
						String(target_id),
					]
				)
		if PendingCharacterRewardContentRules.is_attribute_progress_entry(entry_type) \
			and target_id != &"" \
			and not PendingCharacterRewardContentRules.is_valid_attribute_progress_target(target_id):
			errors.append(
				"Quest %s pending_character_reward attribute_progress references unsupported attribute %s." % [
					String(quest_def.quest_id),
					String(target_id),
				]
			)


static func _resolve_provider_ids(provider_ids: Dictionary) -> Dictionary:
	if provider_ids.is_empty():
		return QuestProviderContentRules.supported_provider_ids()
	return provider_ids.duplicate()


static func _get_dict_value_by_id(source: Dictionary, content_id: StringName) -> Variant:
	if source.has(content_id):
		return source.get(content_id)
	var string_key := String(content_id)
	if source.has(string_key):
		return source.get(string_key)
	return null
