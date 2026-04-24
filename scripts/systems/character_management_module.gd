## 文件说明：该脚本属于角色管理模块相关的模块脚本，集中维护队伍状态、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name CharacterManagementModule
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ACHIEVEMENT_PROGRESS_STATE_SCRIPT = preload("res://scripts/player/progression/achievement_progress_state.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression_service.gd")
const PROFESSION_RULE_SERVICE_SCRIPT = preload("res://scripts/systems/profession_rule_service.gd")
const PROFESSION_ASSIGNMENT_SERVICE_SCRIPT = preload("res://scripts/systems/profession_assignment_service.gd")
const SKILL_MERGE_SERVICE_SCRIPT = preload("res://scripts/systems/skill_merge_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")
const PARTY_EQUIPMENT_SERVICE_SCRIPT = preload("res://scripts/systems/party_equipment_service.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const QUEST_PROGRESS_SERVICE_SCRIPT = preload("res://scripts/systems/quest_progress_service.gd")
const QUEST_DEF_SCRIPT = preload("res://scripts/player/progression/quest_def.gd")
const CHARACTER_PROGRESSION_DELTA_SCRIPT = preload("res://scripts/systems/character_progression_delta.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const QuestDef = QUEST_DEF_SCRIPT
const AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT
const CharacterProgressionDelta = CHARACTER_PROGRESSION_DELTA_SCRIPT
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const PendingCharacterRewardEntry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT

const REWARD_TYPE_ACHIEVEMENT: StringName = &"achievement"
const REWARD_TYPE_QUEST: StringName = &"quest"
const REWARD_ENTRY_ORDER := {
	&"knowledge_unlock": 0,
	&"skill_unlock": 1,
	&"skill_mastery": 2,
	&"attribute_delta": 3,
}

## 字段说明：缓存队伍状态实例，会参与运行时状态流转、系统协作和存档恢复。
var _party_state: PartyState = PARTY_STATE_SCRIPT.new()
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存任务定义集合字典，集中保存可按键查询的运行时数据。
var _quest_defs: Dictionary = {}
## 字段说明：记录队伍仓库服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
## 字段说明：记录队伍装备服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_equipment_service = PARTY_EQUIPMENT_SERVICE_SCRIPT.new()
## 字段说明：记录任务进度服务，会参与运行时状态流转、系统协作和存档恢复。
var _quest_progress_service = QUEST_PROGRESS_SERVICE_SCRIPT.new()


func setup(
	party_state: PartyState,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	achievement_defs: Dictionary = {},
	item_defs: Dictionary = {},
	quest_defs: Dictionary = {}
) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_skill_defs = skill_defs if skill_defs != null else {}
	_profession_defs = profession_defs if profession_defs != null else {}
	_achievement_defs = achievement_defs if achievement_defs != null else {}
	_item_defs = item_defs if item_defs != null else {}
	_quest_defs = quest_defs if quest_defs != null else {}
	_party_warehouse_service.setup(_party_state, _item_defs)
	_party_equipment_service.setup(_party_state, _item_defs)
	_quest_progress_service.setup(_party_state, _quest_defs)


func get_party_state() -> PartyState:
	return _party_state


func set_party_state(party_state: PartyState) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_party_warehouse_service.setup(_party_state, _item_defs)
	_party_equipment_service.setup(_party_state, _item_defs)
	_quest_progress_service.setup(_party_state, _quest_defs)


func get_member_state(member_id: StringName) -> PartyMemberState:
	if _party_state == null:
		return null
	return _party_state.get_member_state(member_id)


func set_member_state(member_state: PartyMemberState) -> void:
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	if member_state == null:
		return
	_party_state.set_member_state(member_state)


func get_pending_character_rewards() -> Array[PendingCharacterReward]:
	if _party_state == null:
		return []
	return _party_state.pending_character_rewards.duplicate()


func get_active_quest_states() -> Array:
	return _quest_progress_service.get_active_quests() if _quest_progress_service != null else []


func get_claimable_quest_states() -> Array:
	return _quest_progress_service.get_claimable_quests() if _quest_progress_service != null else []


func get_claimable_quest_ids() -> Array[StringName]:
	var quest_ids_variant = _quest_progress_service.get_claimable_quest_ids() if _quest_progress_service != null else []
	return ProgressionDataUtils.to_string_name_array(quest_ids_variant)


func get_completed_quest_ids() -> Array[StringName]:
	var quest_ids_variant = _quest_progress_service.get_completed_quest_ids() if _quest_progress_service != null else []
	return ProgressionDataUtils.to_string_name_array(quest_ids_variant)


func accept_quest(quest_id: StringName, world_step: int = -1, allow_reaccept: bool = false) -> bool:
	if _quest_progress_service == null:
		return false
	var accepted := _quest_progress_service.accept_quest(quest_id, world_step, allow_reaccept)
	_party_state = _quest_progress_service.get_party_state()
	return accepted


func complete_quest(quest_id: StringName, world_step: int = -1) -> bool:
	if _quest_progress_service == null:
		return false
	var completed := _quest_progress_service.complete_quest(quest_id, world_step)
	_party_state = _quest_progress_service.get_party_state()
	return completed


func submit_item_objective(quest_id: StringName, objective_id: StringName = &"", world_step: int = -1) -> Dictionary:
	var result := {
		"ok": false,
		"error_code": "",
		"objective_id": "",
		"item_id": "",
		"target_value": 0,
		"required_quantity": 0,
		"submitted_quantity": 0,
		"accepted_quest_ids": [],
		"progressed_quest_ids": [],
		"claimable_quest_ids": [],
		"completed_quest_ids": [],
	}
	var submission_preview := _preview_quest_submit_item_objective(quest_id, objective_id)
	if not bool(submission_preview.get("ok", false)):
		result["error_code"] = String(submission_preview.get("error_code", "objective_not_found"))
		result["objective_id"] = String(submission_preview.get("objective_id", ""))
		result["item_id"] = String(submission_preview.get("item_id", ""))
		result["required_quantity"] = int(submission_preview.get("required_quantity", 0))
		return result

	var resolved_objective_id := ProgressionDataUtils.to_string_name(submission_preview.get("objective_id", ""))
	var item_id := ProgressionDataUtils.to_string_name(submission_preview.get("item_id", ""))
	var target_value := maxi(int(submission_preview.get("target_value", 0)), 0)
	var required_quantity := maxi(int(submission_preview.get("required_quantity", 0)), 0)
	result["objective_id"] = String(resolved_objective_id)
	result["item_id"] = String(item_id)
	result["target_value"] = target_value
	result["required_quantity"] = required_quantity
	if resolved_objective_id == &"" or item_id == &"" or target_value <= 0 or required_quantity <= 0:
		result["error_code"] = "invalid_submit_item_objective"
		return result

	var warehouse_state_before = _party_state.warehouse_state.duplicate_state() if _party_state != null and _party_state.warehouse_state != null else null
	var withdraw_item_ids := _build_repeated_item_ids(item_id, required_quantity)
	var warehouse_commit := _party_warehouse_service.commit_batch_swap(withdraw_item_ids, [])
	if not bool(warehouse_commit.get("allowed", false)):
		result["error_code"] = "submit_item_missing_inventory" if String(warehouse_commit.get("error_code", "")) == "warehouse_missing_item" else "submit_item_commit_failed"
		return result

	var summary := apply_quest_progress_events([{
		"event_type": "progress",
		"quest_id": String(quest_id),
		"objective_id": String(resolved_objective_id),
		"objective_type": String(QuestDef.OBJECTIVE_SUBMIT_ITEM),
		"target_id": String(item_id),
		"target_value": target_value,
		"progress_delta": required_quantity,
		"item_id": String(item_id),
		"quantity": required_quantity,
		"context": {
			"item_id": String(item_id),
			"submitted_quantity": required_quantity,
		},
	}], world_step)
	if not (summary.get("progressed_quest_ids", []) as Array).has(quest_id):
		_party_state.warehouse_state = warehouse_state_before
		_party_warehouse_service.setup(_party_state, _item_defs)
		_quest_progress_service.setup(_party_state, _quest_defs)
		result["error_code"] = "quest_progress_failed"
		return result

	result["ok"] = true
	result["submitted_quantity"] = required_quantity
	result["accepted_quest_ids"] = ProgressionDataUtils.to_string_name_array(summary.get("accepted_quest_ids", []))
	result["progressed_quest_ids"] = ProgressionDataUtils.to_string_name_array(summary.get("progressed_quest_ids", []))
	result["claimable_quest_ids"] = ProgressionDataUtils.to_string_name_array(summary.get("claimable_quest_ids", []))
	result["completed_quest_ids"] = ProgressionDataUtils.to_string_name_array(summary.get("completed_quest_ids", []))
	return result


func claim_quest_reward(quest_id: StringName, world_step: int = -1) -> Dictionary:
	var result := {
		"ok": false,
		"error_code": "",
		"gold_delta": 0,
		"item_rewards": [],
		"pending_character_rewards": [],
		"unsupported_reward_types": [],
	}
	if _party_state == null or quest_id == &"":
		result["error_code"] = "invalid_quest_id"
		return result
	if not _party_state.has_claimable_quest(quest_id):
		result["error_code"] = "quest_not_claimable"
		return result
	var quest_reward_data := _resolve_quest_reward_data(quest_id)
	if not bool(quest_reward_data.get("found", false)):
		result["error_code"] = "quest_def_missing"
		return result
	var reward_preview := _preview_quest_reward_claim(quest_id, quest_reward_data)
	if not bool(reward_preview.get("ok", false)):
		result["error_code"] = String(reward_preview.get("error_code", "invalid_reward_entry"))
		result["unsupported_reward_types"] = ProgressionDataUtils.to_string_name_array(
			reward_preview.get("unsupported_reward_types", [])
		)
		return result
	var reward_item_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(
		reward_preview.get("warehouse_deposit_item_ids", [])
	)
	var warehouse_state_before = _party_state.warehouse_state.duplicate_state() if _party_state.warehouse_state != null else null
	if not reward_item_ids.is_empty():
		var warehouse_commit := _party_warehouse_service.commit_batch_swap([], reward_item_ids)
		if not bool(warehouse_commit.get("allowed", false)):
			result["error_code"] = _resolve_quest_reward_warehouse_error_code(warehouse_commit)
			return result
	var gold_delta := int(reward_preview.get("gold_delta", 0))
	var gold_before_claim := _party_state.get_gold()
	if gold_delta > 0:
		_party_state.add_gold(gold_delta)
	if not _party_state.mark_quest_reward_claimed(quest_id, world_step):
		if gold_delta > 0:
			_party_state.set_gold(gold_before_claim)
		_party_state.warehouse_state = warehouse_state_before
		result["error_code"] = "quest_claim_failed"
		return result
	var pending_character_rewards: Array = reward_preview.get("pending_character_rewards", [])
	if not pending_character_rewards.is_empty():
		enqueue_pending_character_rewards(pending_character_rewards)
	result["ok"] = true
	result["gold_delta"] = gold_delta
	result["item_rewards"] = (reward_preview.get("item_rewards", []) as Array).duplicate(true)
	result["pending_character_rewards"] = _pending_character_reward_variants_to_dicts(pending_character_rewards)
	return result


func apply_quest_progress_events(event_variants: Array, world_step: int = -1) -> Dictionary:
	if _quest_progress_service == null:
		return {
			"accepted_quest_ids": [],
			"progressed_quest_ids": [],
			"claimable_quest_ids": [],
			"completed_quest_ids": [],
		}
	var summary: Dictionary = _quest_progress_service.apply_quest_progress_events(event_variants, world_step)
	_party_state = _quest_progress_service.get_party_state()
	return summary


func enqueue_pending_character_rewards(reward_variants: Array) -> void:
	if _party_state == null:
		_party_state = PARTY_STATE_SCRIPT.new()
	for reward_variant in reward_variants:
		var reward := _normalize_pending_character_reward_variant(reward_variant)
		if reward == null or reward.is_empty():
			continue
		_party_state.enqueue_pending_character_reward(reward)


func get_member_attribute_snapshot(member_id: StringName) -> AttributeSnapshot:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	return _build_attribute_service(member_state).get_snapshot()


func learn_skill(member_id: StringName, skill_id: StringName) -> bool:
	return _learn_skill_internal(member_id, skill_id)


func learn_knowledge(member_id: StringName, knowledge_id: StringName) -> bool:
	return _learn_knowledge_internal(member_id, knowledge_id)


func _learn_skill_internal(member_id: StringName, skill_id: StringName, unlocked_ids = null) -> bool:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return false

	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	if not progression_service.learn_skill(skill_id):
		return false
	var achievement_ids := record_achievement_event(member_id, &"skill_learned", 1, skill_id)
	if unlocked_ids is Array:
		_append_unique_string_names(unlocked_ids, achievement_ids)
	return true


func _learn_knowledge_internal(member_id: StringName, knowledge_id: StringName, unlocked_ids = null) -> bool:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return false

	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	if not progression_service.learn_knowledge(knowledge_id):
		return false
	var achievement_ids := record_achievement_event(member_id, &"knowledge_learned", 1, knowledge_id)
	if unlocked_ids is Array:
		_append_unique_string_names(unlocked_ids, achievement_ids)
	return true


func grant_battle_mastery(
	member_id: StringName,
	skill_id: StringName,
	amount: int
) -> CharacterProgressionDelta:
	return _grant_skill_mastery_internal(
		member_id,
		skill_id,
		amount,
		&"battle",
		_build_default_source_label(&"battle"),
		"",
		true
	)


func record_achievement_event(
	member_id: StringName,
	event_type: StringName,
	amount: int = 1,
	subject_id: StringName = &"",
	meta: Dictionary = {}
) -> Array[StringName]:
	var unlocked_ids: Array[StringName] = []
	if member_id == &"" or event_type == &"" or amount <= 0:
		return unlocked_ids

	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return unlocked_ids

	for achievement_def in _get_matching_achievement_defs(event_type, subject_id):
		if achievement_def == null:
			continue
		var progress_state = member_state.progression.get_achievement_progress_state(achievement_def.achievement_id)
		if progress_state == null:
			progress_state = ACHIEVEMENT_PROGRESS_STATE_SCRIPT.new()
			progress_state.achievement_id = achievement_def.achievement_id
		if progress_state.is_unlocked:
			continue

		progress_state.current_value += amount
		if progress_state.current_value >= achievement_def.threshold:
			_finalize_achievement_unlock(member_state, achievement_def, progress_state, meta)
			_append_unique_string_name(unlocked_ids, achievement_def.achievement_id)

		member_state.progression.set_achievement_progress_state(progress_state)

	return unlocked_ids


func unlock_achievement(
	member_id: StringName,
	achievement_id: StringName,
	meta: Dictionary = {}
) -> bool:
	if member_id == &"" or achievement_id == &"":
		return false

	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return false

	var achievement_def = _achievement_defs.get(achievement_id)
	if achievement_def == null:
		return false

	var progress_state = member_state.progression.get_achievement_progress_state(achievement_id)
	if progress_state == null:
		progress_state = ACHIEVEMENT_PROGRESS_STATE_SCRIPT.new()
		progress_state.achievement_id = achievement_id
	if progress_state.is_unlocked:
		member_state.progression.set_achievement_progress_state(progress_state)
		return false

	progress_state.current_value = maxi(progress_state.current_value, maxi(int(achievement_def.threshold), 1))
	_finalize_achievement_unlock(member_state, achievement_def, progress_state, meta)
	member_state.progression.set_achievement_progress_state(progress_state)
	return true


func build_pending_character_reward(
	member_id: StringName,
	reward_id: StringName,
	source_type: StringName,
	source_id: StringName,
	source_label: String,
	entry_variants: Array,
	summary_text: String = ""
) -> PendingCharacterReward:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return null

	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = reward_id if reward_id != &"" else _build_reward_id(member_id, source_id if source_id != &"" else source_type)
	reward.member_id = member_id
	reward.member_name = member_state.display_name if not member_state.display_name.is_empty() else String(member_id)
	reward.source_type = source_type
	reward.source_id = source_id if source_id != &"" else source_type
	reward.source_label = source_label if not source_label.is_empty() else _build_default_source_label(source_type)
	reward.summary_text = summary_text
	reward.entries = _normalize_pending_character_entries(entry_variants)
	return reward if not reward.is_empty() else null


func _finalize_achievement_unlock(
	member_state: PartyMemberState,
	achievement_def,
	progress_state,
	meta: Dictionary
) -> void:
	if member_state == null or achievement_def == null or progress_state == null:
		return
	progress_state.is_unlocked = true
	progress_state.unlocked_at_unix_time = int(Time.get_unix_time_from_system())
	var reward = _build_achievement_pending_reward(member_state, achievement_def, meta)
	if reward != null and not reward.is_empty():
		enqueue_pending_character_rewards([reward])


func build_pending_skill_mastery_reward(
	member_id: StringName,
	source_type: StringName,
	source_label: String,
	entry_variants: Array,
	summary_text: String = ""
) -> PendingCharacterReward:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return null

	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = _build_reward_id(member_id, source_type)
	reward.member_id = member_id
	reward.member_name = member_state.display_name if not member_state.display_name.is_empty() else String(member_id)
	reward.source_type = source_type
	reward.source_id = source_type
	reward.source_label = source_label if not source_label.is_empty() else _build_default_source_label(source_type)
	reward.summary_text = summary_text
	reward.entries = _normalize_pending_skill_mastery_entries(member_state.progression, entry_variants, source_type)
	return reward if not reward.is_empty() else null


func apply_pending_character_reward(reward: PendingCharacterReward) -> CharacterProgressionDelta:
	var normalized_reward: PendingCharacterReward = _normalize_pending_character_reward_variant(reward)
	var member_id: StringName = normalized_reward.member_id if normalized_reward != null else &""
	var delta: CharacterProgressionDelta = _new_delta(member_id)
	var member_state: PartyMemberState = get_member_state(member_id)
	if normalized_reward == null or normalized_reward.is_empty():
		return delta
	if member_state == null or member_state.progression == null:
		_remove_pending_character_reward_if_present(normalized_reward.reward_id)
		return delta

	var before_skill_levels: Dictionary = _capture_skill_levels(member_state.progression)
	var before_granted_skill_ids: Dictionary = _capture_granted_skill_ids(member_state.progression)
	var before_profession_ranks: Dictionary = _capture_profession_ranks(member_state.progression)
	delta.character_level_before = int(member_state.progression.character_level)
	_append_unique_string_name(delta.unlocked_achievement_ids, normalized_reward.source_id if normalized_reward.source_type == REWARD_TYPE_ACHIEVEMENT else &"")

	var attribute_service: AttributeService = _build_attribute_service(member_state)
	var mastery_source_type := _resolve_mastery_source_type(normalized_reward.source_type)
	var applied_any := false

	for entry in _sort_pending_reward_entries(normalized_reward.entries):
		if entry == null or entry.is_empty():
			continue

		match entry.entry_type:
			&"knowledge_unlock":
				if _learn_knowledge_internal(member_id, entry.target_id, delta.unlocked_achievement_ids):
					applied_any = true
					delta.knowledge_changes.append({
						"knowledge_id": entry.target_id,
						"knowledge_label": _resolve_reward_target_label(entry.entry_type, entry.target_id, entry.target_label),
						"reason_text": entry.reason_text,
					})
			&"skill_unlock":
				if _learn_skill_internal(member_id, entry.target_id, delta.unlocked_achievement_ids):
					applied_any = true
			&"skill_mastery":
				var mastery_delta := _grant_skill_mastery_internal(
					member_id,
					entry.target_id,
					entry.amount,
					mastery_source_type,
					normalized_reward.source_label,
					entry.reason_text,
					true
				)
				if not mastery_delta.mastery_changes.is_empty():
					applied_any = true
				_merge_delta(delta, mastery_delta)
			&"attribute_delta":
				if attribute_service.apply_permanent_attribute_change(
					entry.target_id,
					entry.amount,
					{
						"source_type": normalized_reward.source_type,
						"source_id": normalized_reward.source_id,
					}
				):
					applied_any = true
					delta.attribute_changes.append({
						"attribute_id": entry.target_id,
						"attribute_label": _resolve_reward_target_label(entry.entry_type, entry.target_id, entry.target_label),
						"delta": entry.amount,
						"reason_text": entry.reason_text,
					})
			_:
				continue

	_fill_delta_from_progression(
		delta,
		member_state.progression,
		before_skill_levels,
		before_granted_skill_ids,
		before_profession_ranks
	)
	if not applied_any and delta.mastery_changes.is_empty():
		delta.character_level_after = delta.character_level_before

	_remove_pending_character_reward_if_present(normalized_reward.reward_id)
	return delta


func get_member_achievement_summary(member_id: StringName) -> Dictionary:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return {
			"unlocked_count": 0,
			"in_progress_count": 0,
			"recent_unlocked_name": "",
			"active_progress_entries": [],
		}

	var unlocked_count := 0
	var in_progress_count := 0
	var recent_unlocked_name := ""
	var recent_unlocked_time := 0
	var active_progress_entries: Array[Dictionary] = []

	for achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs):
		var achievement_id := StringName(achievement_key)
		var achievement_def = _achievement_defs.get(achievement_id)
		if achievement_def == null:
			continue

		var progress_state = member_state.progression.get_achievement_progress_state(achievement_id)
		if progress_state != null and progress_state.is_unlocked:
			unlocked_count += 1
			var unlocked_at := int(progress_state.unlocked_at_unix_time)
			if unlocked_at >= recent_unlocked_time:
				recent_unlocked_time = unlocked_at
				recent_unlocked_name = achievement_def.display_name
			continue

		var current_value := int(progress_state.current_value) if progress_state != null else 0
		if current_value <= 0:
			continue

		in_progress_count += 1
		active_progress_entries.append({
			"achievement_id": achievement_id,
			"display_name": achievement_def.display_name,
			"description": achievement_def.description,
			"current_value": current_value,
			"threshold": int(achievement_def.threshold),
			"progress_ratio": float(current_value) / float(maxi(int(achievement_def.threshold), 1)),
		})

	active_progress_entries.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		var ratio_a := float(a.get("progress_ratio", 0.0))
		var ratio_b := float(b.get("progress_ratio", 0.0))
		if ratio_a == ratio_b:
			var current_a := int(a.get("current_value", 0))
			var current_b := int(b.get("current_value", 0))
			if current_a == current_b:
				return String(a.get("display_name", "")) < String(b.get("display_name", ""))
			return current_a > current_b
		return ratio_a > ratio_b
	)

	return {
		"unlocked_count": unlocked_count,
		"in_progress_count": in_progress_count,
		"recent_unlocked_name": recent_unlocked_name,
		"active_progress_entries": active_progress_entries,
	}


func promote_profession(
	member_id: StringName,
	profession_id: StringName,
	selection: Dictionary
) -> CharacterProgressionDelta:
	var member_state: PartyMemberState = get_member_state(member_id)
	var delta: CharacterProgressionDelta = _new_delta(member_id)
	if member_state == null or member_state.progression == null:
		return delta

	var before_skill_levels: Dictionary = _capture_skill_levels(member_state.progression)
	var before_granted_skill_ids: Dictionary = _capture_granted_skill_ids(member_state.progression)
	var before_profession_ranks: Dictionary = _capture_profession_ranks(member_state.progression)
	delta.character_level_before = int(member_state.progression.character_level)

	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	if progression_service.promote_profession(profession_id, selection):
		_fill_delta_from_progression(
			delta,
			member_state.progression,
			before_skill_levels,
			before_granted_skill_ids,
			before_profession_ranks
		)
		_append_unique_string_names(
			delta.unlocked_achievement_ids,
			record_achievement_event(member_id, &"profession_promoted", 1, profession_id)
		)

	return delta


func commit_battle_resources(member_id: StringName, current_hp: int, current_mp: int) -> void:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null:
		return
	var snapshot: AttributeSnapshot = get_member_attribute_snapshot(member_id)
	member_state.current_hp = clampi(current_hp, 0, maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX), 1))
	member_state.current_mp = clampi(current_mp, 0, maxi(snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX), 0))
	member_state.is_dead = false


func commit_battle_death(member_id: StringName) -> void:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null:
		return
	_salvage_member_equipment(member_state)
	member_state.current_hp = 0
	member_state.current_mp = 0
	member_state.is_dead = true
	if _party_state != null:
		_party_state.remove_member_from_rosters(member_id)


func commit_battle_ko(member_id: StringName) -> void:
	commit_battle_death(member_id)


func flush_after_battle() -> int:
	return OK


func _salvage_member_equipment(member_state: PartyMemberState) -> void:
	if member_state == null:
		return
	var equipment_state = member_state.equipment_state
	if equipment_state == null \
		or not (equipment_state is Object and equipment_state.has_method("get_entry_slot_ids") and equipment_state.has_method("pop_equipped_instance")):
		return
	var entry_slot_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(equipment_state.get_entry_slot_ids())
	for entry_slot_id in entry_slot_ids:
		var equipped_instance = equipment_state.pop_equipped_instance(entry_slot_id)
		if equipped_instance != null:
			_party_warehouse_service.deposit_equipment_instance(equipped_instance)


func _collect_known_active_skill_ids(progression_state) -> Array[StringName]:
	var skill_ids: Array[StringName] = []
	if progression_state == null:
		return skill_ids

	for skill_key in ProgressionDataUtils.sorted_string_keys(progression_state.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if skill_def.skill_type != &"active":
			continue
		if not skill_def.can_use_in_combat():
			continue
		skill_ids.append(skill_id)

	return skill_ids


func _collect_known_skill_level_map(progression_state) -> Dictionary:
	var skill_levels: Dictionary = {}
	if progression_state == null:
		return skill_levels

	for skill_key in ProgressionDataUtils.sorted_string_keys(progression_state.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if skill_def.skill_type != &"active":
			continue
		skill_levels[skill_id] = int(skill_progress.skill_level)

	return skill_levels


func _build_progression_service(progression_state) -> ProgressionService:
	var assignment_service: ProfessionAssignmentService = PROFESSION_ASSIGNMENT_SERVICE_SCRIPT.new()
	assignment_service.setup(progression_state, _skill_defs, _profession_defs)

	var merge_service: SkillMergeService = SKILL_MERGE_SERVICE_SCRIPT.new()
	merge_service.setup(progression_state, _skill_defs, assignment_service)

	var rule_service: ProfessionRuleService = PROFESSION_RULE_SERVICE_SCRIPT.new()
	rule_service.setup(progression_state, _skill_defs, _profession_defs)

	var progression_service: ProgressionService = PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(
		progression_state,
		_skill_defs,
		_profession_defs,
		rule_service,
		assignment_service,
		merge_service
	)
	return progression_service


func _build_attribute_service(member_state: PartyMemberState) -> AttributeService:
	var attribute_service: AttributeService = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(
		member_state.progression,
		_skill_defs,
		_profession_defs,
		_party_equipment_service.build_attribute_modifiers(member_state.equipment_state)
	)
	return attribute_service


func _grant_skill_mastery_internal(
	member_id: StringName,
	skill_id: StringName,
	amount: int,
	source_type: StringName,
	source_label: String,
	reason_text: String,
	emit_achievement_event: bool
) -> CharacterProgressionDelta:
	var member_state: PartyMemberState = get_member_state(member_id)
	var delta: CharacterProgressionDelta = _new_delta(member_id)
	if member_state == null or member_state.progression == null or amount <= 0:
		return delta

	var before_skill_levels: Dictionary = _capture_skill_levels(member_state.progression)
	var before_granted_skill_ids: Dictionary = _capture_granted_skill_ids(member_state.progression)
	var before_profession_ranks: Dictionary = _capture_profession_ranks(member_state.progression)
	delta.character_level_before = int(member_state.progression.character_level)

	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	var mastery_source_type := _resolve_mastery_source_type(source_type)
	if not progression_service.grant_skill_mastery(skill_id, amount, mastery_source_type):
		delta.character_level_after = delta.character_level_before
		return delta

	delta.mastery_changes.append({
		"skill_id": skill_id,
		"skill_name": _resolve_skill_label(skill_id),
		"mastery_amount": amount,
		"source_type": source_type,
		"source_label": source_label if not source_label.is_empty() else _build_default_source_label(source_type),
		"reason_text": reason_text,
	})
	_fill_delta_from_progression(
		delta,
		member_state.progression,
		before_skill_levels,
		before_granted_skill_ids,
		before_profession_ranks
	)
	if emit_achievement_event:
		_append_unique_string_names(
			delta.unlocked_achievement_ids,
			record_achievement_event(member_id, &"skill_mastery_gained", amount, skill_id)
		)
	return delta


func _capture_skill_levels(progression_state) -> Dictionary:
	var skill_levels: Dictionary = {}
	if progression_state == null:
		return skill_levels
	for skill_key in progression_state.skills.keys():
		var skill_id: StringName = ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		skill_levels[skill_id] = int(skill_progress.skill_level)
	return skill_levels


func _capture_granted_skill_ids(progression_state) -> Dictionary:
	var granted_skill_ids: Dictionary = {}
	if progression_state == null:
		return granted_skill_ids
	for skill_key in progression_state.skills.keys():
		var skill_id: StringName = ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		if skill_progress.profession_granted_by != &"":
			granted_skill_ids[skill_id] = true
	return granted_skill_ids


func _capture_profession_ranks(progression_state) -> Dictionary:
	var profession_ranks: Dictionary = {}
	if progression_state == null:
		return profession_ranks
	for profession_key in progression_state.professions.keys():
		var profession_id: StringName = ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress: Variant = progression_state.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		profession_ranks[profession_id] = int(profession_progress.rank)
	return profession_ranks


func _normalize_pending_skill_mastery_entries(
	progression_state,
	entry_variants: Array,
	source_type: StringName
) -> Array[PendingCharacterRewardEntry]:
	var normalized_entries: Array[PendingCharacterRewardEntry] = []
	if progression_state == null:
		return normalized_entries

	var entry_map: Dictionary = {}
	var mastery_source_type := _resolve_mastery_source_type(source_type)
	for entry_variant in entry_variants:
		if entry_variant is not Dictionary:
			continue
		var entry_data: Dictionary = entry_variant
		if ProgressionDataUtils.to_string_name(entry_data.get("entry_type", &"skill_mastery")) != &"skill_mastery":
			continue
		var skill_id := ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
		var mastery_amount := int(entry_data.get("amount", 0))
		if skill_id == &"" or mastery_amount <= 0:
			continue

		var skill_progress = progression_state.get_skill_progress(skill_id)
		var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
		if skill_progress == null or skill_def == null:
			continue
		if not skill_progress.is_learned:
			continue
		if not skill_def.mastery_sources.is_empty() and not skill_def.mastery_sources.has(mastery_source_type):
			continue

		var reward_entry: PendingCharacterRewardEntry = entry_map.get(skill_id) as PendingCharacterRewardEntry
		if reward_entry == null:
			reward_entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
			reward_entry.entry_type = &"skill_mastery"
			reward_entry.target_id = skill_id
			reward_entry.target_label = _resolve_skill_label(skill_id)
			reward_entry.reason_text = String(entry_data.get("reason_text", ""))
			entry_map[skill_id] = reward_entry
			normalized_entries.append(reward_entry)

		reward_entry.amount += mastery_amount
		if reward_entry.reason_text.is_empty():
			reward_entry.reason_text = String(entry_data.get("reason_text", ""))

	return normalized_entries


func _normalize_pending_character_reward_variant(reward_variant) -> PendingCharacterReward:
	if reward_variant == null:
		return null
	if reward_variant is PendingCharacterReward:
		var typed_reward := reward_variant as PendingCharacterReward
		if typed_reward.reward_id == &"":
			typed_reward.reward_id = _build_reward_id(typed_reward.member_id, typed_reward.source_id if typed_reward.source_id != &"" else typed_reward.source_type)
		return typed_reward if not typed_reward.is_empty() else null
	if reward_variant is Dictionary:
		var normalized_reward = PENDING_CHARACTER_REWARD_SCRIPT.from_variant(reward_variant)
		if normalized_reward == null or normalized_reward.is_empty():
			return null
		if normalized_reward.reward_id == &"":
			normalized_reward.reward_id = _build_reward_id(
				normalized_reward.member_id,
				normalized_reward.source_id if normalized_reward.source_id != &"" else normalized_reward.source_type
			)
		return normalized_reward
	return null


func _resolve_quest_reward_data(quest_id: StringName) -> Dictionary:
	var quest_variant = _quest_defs.get(quest_id, _quest_defs.get(String(quest_id), null))
	if quest_variant == null:
		return {
			"found": false,
			"display_name": "",
			"reward_entries": [],
		}
	if quest_variant is Dictionary:
		var quest_data := (quest_variant as Dictionary).duplicate(true)
		return {
			"found": true,
			"display_name": String(quest_data.get("display_name", "")),
			"reward_entries": quest_data.get("reward_entries", []),
		}
	if quest_variant is QuestDef:
		var quest_def: QuestDef = quest_variant
		return {
			"found": true,
			"display_name": quest_def.display_name,
			"reward_entries": quest_def.reward_entries.duplicate(true),
		}
	if quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			return {
				"found": true,
				"display_name": String((quest_data_variant as Dictionary).get("display_name", "")),
				"reward_entries": (quest_data_variant as Dictionary).get("reward_entries", []),
			}
	return {
		"found": false,
		"display_name": "",
		"reward_entries": [],
	}


func _preview_quest_submit_item_objective(quest_id: StringName, objective_id: StringName = &"") -> Dictionary:
	var result := {
		"ok": false,
		"error_code": "",
		"objective_id": "",
		"item_id": "",
		"target_value": 0,
		"required_quantity": 0,
	}
	if _party_state == null or quest_id == &"":
		result["error_code"] = "invalid_quest_id"
		return result
	var quest_state = _party_state.get_active_quest_state(quest_id)
	if quest_state == null:
		result["error_code"] = "quest_not_active"
		return result

	var quest_variant = _quest_defs.get(quest_id, _quest_defs.get(String(quest_id), null))
	if quest_variant == null:
		result["error_code"] = "quest_def_missing"
		return result

	var objective_defs_variant = []
	if quest_variant is QuestDef:
		objective_defs_variant = (quest_variant as QuestDef).objective_defs
	elif quest_variant is Dictionary:
		objective_defs_variant = (quest_variant as Dictionary).get("objective_defs", [])
	elif quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			objective_defs_variant = (quest_data_variant as Dictionary).get("objective_defs", [])
	if objective_defs_variant is not Array:
		result["error_code"] = "quest_def_missing"
		return result

	var requested_objective_id := ProgressionDataUtils.to_string_name(objective_id)
	var found_submit_item_objective := false
	var found_completed_submit_item_objective := false
	for objective_variant in objective_defs_variant:
		if objective_variant is not Dictionary:
			continue
		var objective_data := objective_variant as Dictionary
		if ProgressionDataUtils.to_string_name(objective_data.get("objective_type", "")) != QuestDef.OBJECTIVE_SUBMIT_ITEM:
			continue
		found_submit_item_objective = true
		var current_objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		if requested_objective_id != &"" and current_objective_id != requested_objective_id:
			continue
		var item_id := ProgressionDataUtils.to_string_name(objective_data.get("target_id", ""))
		var target_value := maxi(int(objective_data.get("target_value", 1)), 1)
		result["objective_id"] = String(current_objective_id)
		result["item_id"] = String(item_id)
		result["target_value"] = target_value
		result["required_quantity"] = maxi(target_value - quest_state.get_objective_progress(current_objective_id), 0)
		if current_objective_id == &"" or item_id == &"":
			result["error_code"] = "invalid_submit_item_objective"
			return result
		if quest_state.is_objective_complete(current_objective_id, target_value):
			found_completed_submit_item_objective = true
			result["error_code"] = "objective_already_complete"
			if requested_objective_id != &"":
				return result
			continue
		result["ok"] = true
		result["error_code"] = ""
		return result

	result["error_code"] = "objective_already_complete" if found_completed_submit_item_objective else "objective_not_found"
	return result


func _preview_quest_reward_claim(quest_id: StringName, quest_reward_data: Dictionary) -> Dictionary:
	var result := {
		"ok": true,
		"error_code": "",
		"gold_delta": 0,
		"item_rewards": [],
		"warehouse_deposit_item_ids": [],
		"pending_character_rewards": [],
		"unsupported_reward_types": [],
	}
	var reward_entries_variant = quest_reward_data.get("reward_entries", [])
	if reward_entries_variant is not Array:
		return result
	var quest_label := String(quest_reward_data.get("display_name", "")).strip_edges()
	if quest_label.is_empty():
		quest_label = String(quest_id)
	var unsupported_reward_types: Array[StringName] = []
	var reward_item_entries: Array[Dictionary] = []
	var reward_item_ids: Array[StringName] = []
	var pending_character_rewards: Array[PendingCharacterReward] = []
	for reward_variant in reward_entries_variant:
		if reward_variant is not Dictionary:
			result["ok"] = false
			result["error_code"] = "invalid_reward_entry"
			return result
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data.get("reward_type", ""))
		if reward_type == &"":
			result["ok"] = false
			result["error_code"] = "invalid_reward_entry"
			return result
		match reward_type:
			QUEST_DEF_SCRIPT.REWARD_GOLD:
				var amount := int(reward_data.get("amount", 0))
				if amount <= 0:
					result["ok"] = false
					result["error_code"] = "invalid_gold_amount"
					return result
				result["gold_delta"] = int(result.get("gold_delta", 0)) + amount
			QUEST_DEF_SCRIPT.REWARD_ITEM:
				var item_reward_result := _preview_quest_item_reward_entry(reward_data)
				if not bool(item_reward_result.get("ok", false)):
					result["ok"] = false
					result["error_code"] = String(item_reward_result.get("error_code", "invalid_item_reward"))
					return result
				var reward_item_entry: Variant = item_reward_result.get("item_reward", {})
				if reward_item_entry is Dictionary and not (reward_item_entry as Dictionary).is_empty():
					reward_item_entries.append((reward_item_entry as Dictionary).duplicate(true))
				reward_item_ids.append_array(
					ProgressionDataUtils.to_string_name_array(item_reward_result.get("warehouse_deposit_item_ids", []))
				)
			QUEST_DEF_SCRIPT.REWARD_PENDING_CHARACTER_REWARD:
				var pending_reward_result := _preview_quest_pending_character_reward_entry(quest_id, quest_label, reward_data)
				if not bool(pending_reward_result.get("ok", false)):
					result["ok"] = false
					result["error_code"] = String(pending_reward_result.get("error_code", "invalid_pending_character_reward"))
					return result
				var pending_reward: Variant = pending_reward_result.get("pending_character_reward", null)
				if pending_reward is PendingCharacterReward and not (pending_reward as PendingCharacterReward).is_empty():
					pending_character_rewards.append(pending_reward as PendingCharacterReward)
			_:
				_append_unique_string_name(unsupported_reward_types, reward_type)
	if not unsupported_reward_types.is_empty():
		result["ok"] = false
		result["error_code"] = "unsupported_reward_types"
		result["unsupported_reward_types"] = unsupported_reward_types
		return result
	if not reward_item_ids.is_empty():
		var warehouse_preview := _party_warehouse_service.preview_batch_swap([], reward_item_ids)
		if not bool(warehouse_preview.get("allowed", false)):
			result["ok"] = false
			result["error_code"] = _resolve_quest_reward_warehouse_error_code(warehouse_preview)
			return result
	result["item_rewards"] = reward_item_entries
	result["warehouse_deposit_item_ids"] = ProgressionDataUtils.string_name_array_to_string_array(reward_item_ids)
	result["pending_character_rewards"] = pending_character_rewards
	return result


func _preview_quest_item_reward_entry(reward_data: Dictionary) -> Dictionary:
	var reward_item_id := QUEST_DEF_SCRIPT.get_reward_item_id(reward_data)
	var reward_quantity := QUEST_DEF_SCRIPT.get_reward_quantity(reward_data)
	if reward_item_id == &"" or reward_quantity <= 0:
		return {
			"ok": false,
			"error_code": "invalid_item_reward",
		}
	var item_def = _item_defs.get(reward_item_id)
	if item_def == null:
		return {
			"ok": false,
			"error_code": "item_reward_missing_def",
		}
	return {
		"ok": true,
		"item_reward": {
			"item_id": String(reward_item_id),
			"display_name": item_def.display_name if not item_def.display_name.is_empty() else String(reward_item_id),
			"quantity": reward_quantity,
		},
		"warehouse_deposit_item_ids": ProgressionDataUtils.string_name_array_to_string_array(
			_build_repeated_item_ids(reward_item_id, reward_quantity)
		),
	}


func _preview_quest_pending_character_reward_entry(
	quest_id: StringName,
	quest_label: String,
	reward_data: Dictionary
) -> Dictionary:
	var member_id := ProgressionDataUtils.to_string_name(reward_data.get("member_id", ""))
	var entry_variants = reward_data.get("entries", [])
	if member_id == &"" or entry_variants is not Array or (entry_variants as Array).is_empty():
		return {
			"ok": false,
			"error_code": "invalid_pending_character_reward",
		}
	var source_type := ProgressionDataUtils.to_string_name(reward_data.get("source_type", REWARD_TYPE_QUEST))
	if source_type == &"":
		source_type = REWARD_TYPE_QUEST
	var source_id := ProgressionDataUtils.to_string_name(reward_data.get("source_id", quest_id))
	if source_id == &"":
		source_id = quest_id if quest_id != &"" else source_type
	var source_label := String(reward_data.get("source_label", "")).strip_edges()
	if source_label.is_empty():
		source_label = quest_label
	var summary_text := String(reward_data.get("summary_text", "")).strip_edges()
	var reward_id := ProgressionDataUtils.to_string_name(reward_data.get("reward_id", ""))
	var pending_reward := build_pending_character_reward(
		member_id,
		reward_id,
		source_type,
		source_id,
		source_label,
		entry_variants,
		summary_text
	)
	if pending_reward == null or pending_reward.is_empty():
		return {
			"ok": false,
			"error_code": "invalid_pending_character_reward",
		}
	return {
		"ok": true,
		"pending_character_reward": pending_reward,
	}


func _build_repeated_item_ids(item_id: StringName, quantity: int) -> Array[StringName]:
	var item_ids: Array[StringName] = []
	var resolved_quantity := maxi(quantity, 0)
	for _index in range(resolved_quantity):
		item_ids.append(item_id)
	return item_ids


func _resolve_quest_reward_warehouse_error_code(warehouse_result: Dictionary) -> String:
	return "reward_overflow" if String(warehouse_result.get("error_code", "")) == "warehouse_blocked_swap" else "quest_reward_commit_failed"


func _pending_character_reward_variants_to_dicts(reward_variants: Array) -> Array[Dictionary]:
	var reward_dicts: Array[Dictionary] = []
	for reward_variant in reward_variants:
		var reward := _normalize_pending_character_reward_variant(reward_variant)
		if reward == null or reward.is_empty():
			continue
		reward_dicts.append(reward.to_dict())
	return reward_dicts


func _normalize_pending_character_entries(entry_variants: Array) -> Array[PendingCharacterRewardEntry]:
	var normalized_entries: Array[PendingCharacterRewardEntry] = []
	for entry_variant in entry_variants:
		var entry := _normalize_pending_character_entry(entry_variant)
		if entry == null or entry.is_empty():
			continue
		normalized_entries.append(entry)
	return normalized_entries


func _normalize_pending_character_entry(entry_variant) -> PendingCharacterRewardEntry:
	if entry_variant == null:
		return null
	if entry_variant is PendingCharacterRewardEntry:
		return PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.from_dict((entry_variant as PendingCharacterRewardEntry).to_dict())
	if entry_variant is Dictionary:
		var entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.from_variant(entry_variant)
		if entry == null:
			return null
		if entry.target_label.is_empty():
			entry.target_label = _resolve_reward_target_label(entry.entry_type, entry.target_id, "")
		return entry
	return null


func _build_achievement_pending_reward(member_state: PartyMemberState, achievement_def, meta: Dictionary) -> PendingCharacterReward:
	if member_state == null or achievement_def == null:
		return null

	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = _build_reward_id(member_state.member_id, achievement_def.achievement_id)
	reward.member_id = member_state.member_id
	reward.member_name = member_state.display_name if not member_state.display_name.is_empty() else String(member_state.member_id)
	reward.source_type = REWARD_TYPE_ACHIEVEMENT
	reward.source_id = achievement_def.achievement_id
	reward.source_label = achievement_def.display_name if not achievement_def.display_name.is_empty() else String(achievement_def.achievement_id)
	reward.summary_text = String(meta.get("summary_text", achievement_def.description))
	reward.entries = _build_achievement_reward_entries(achievement_def)
	return reward if not reward.is_empty() else null


func _build_achievement_reward_entries(achievement_def) -> Array[PendingCharacterRewardEntry]:
	var entries: Array[PendingCharacterRewardEntry] = []
	if achievement_def == null:
		return entries

	for reward_def in achievement_def.rewards:
		if reward_def == null or reward_def.is_empty():
			continue
		var entry := PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
		entry.entry_type = reward_def.reward_type
		entry.target_id = reward_def.target_id
		entry.target_label = _resolve_reward_target_label(reward_def.reward_type, reward_def.target_id, reward_def.target_label)
		entry.amount = reward_def.amount
		entry.reason_text = reward_def.reason_text if not reward_def.reason_text.is_empty() else achievement_def.display_name
		if entry.is_empty():
			continue
		entries.append(entry)
	return entries


func _get_matching_achievement_defs(event_type: StringName, subject_id: StringName) -> Array:
	var matches: Array = []
	for achievement_key in ProgressionDataUtils.sorted_string_keys(_achievement_defs):
		var achievement_id := StringName(achievement_key)
		var achievement_def = _achievement_defs.get(achievement_id)
		if achievement_def == null or not achievement_def.matches_event(event_type, subject_id):
			continue
		matches.append(achievement_def)
	return matches


func _sort_pending_reward_entries(entries: Array[PendingCharacterRewardEntry]) -> Array[PendingCharacterRewardEntry]:
	var sorted_entries: Array[PendingCharacterRewardEntry] = []
	for entry in entries:
		if entry == null:
			continue
		sorted_entries.append(entry)

	sorted_entries.sort_custom(func(a: PendingCharacterRewardEntry, b: PendingCharacterRewardEntry) -> bool:
		var order_a := int(REWARD_ENTRY_ORDER.get(a.entry_type, 99))
		var order_b := int(REWARD_ENTRY_ORDER.get(b.entry_type, 99))
		if order_a == order_b:
			var label_a := a.target_label if not a.target_label.is_empty() else String(a.target_id)
			var label_b := b.target_label if not b.target_label.is_empty() else String(b.target_id)
			return label_a < label_b
		return order_a < order_b
	)
	return sorted_entries


func _fill_delta_from_progression(
	delta: CharacterProgressionDelta,
	progression_state,
	before_skill_levels: Dictionary,
	before_granted_skill_ids: Dictionary,
	before_profession_ranks: Dictionary
) -> void:
	delta.character_level_after = int(progression_state.character_level)
	delta.pending_profession_choices = progression_state.pending_profession_choices.duplicate()
	delta.needs_promotion_modal = not delta.pending_profession_choices.is_empty()

	for skill_key in progression_state.skills.keys():
		var skill_id: StringName = ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress: Variant = progression_state.get_skill_progress(skill_id)
		if skill_progress == null:
			continue

		var before_level: int = int(before_skill_levels.get(skill_id, -1))
		if before_level >= 0 and int(skill_progress.skill_level) > before_level:
			_append_unique_string_name(delta.leveled_skill_ids, skill_id)

		if skill_progress.profession_granted_by != &"" and not before_granted_skill_ids.has(skill_id):
			_append_unique_string_name(delta.granted_skill_ids, skill_id)

	for profession_key in progression_state.professions.keys():
		var profession_id: StringName = ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress: Variant = progression_state.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		var before_rank: int = int(before_profession_ranks.get(profession_id, 0))
		if int(profession_progress.rank) != before_rank:
			_append_unique_string_name(delta.changed_profession_ids, profession_id)


func _merge_delta(target: CharacterProgressionDelta, source: CharacterProgressionDelta) -> void:
	if target == null or source == null:
		return

	_append_unique_string_names(target.leveled_skill_ids, source.leveled_skill_ids)
	_append_unique_string_names(target.granted_skill_ids, source.granted_skill_ids)
	_append_unique_string_names(target.changed_profession_ids, source.changed_profession_ids)
	_append_unique_string_names(target.unlocked_achievement_ids, source.unlocked_achievement_ids)
	target.mastery_changes.append_array(source.mastery_changes)
	target.knowledge_changes.append_array(source.knowledge_changes)
	target.attribute_changes.append_array(source.attribute_changes)
	target.pending_profession_choices = source.pending_profession_choices if not source.pending_profession_choices.is_empty() else target.pending_profession_choices
	target.needs_promotion_modal = target.needs_promotion_modal or source.needs_promotion_modal
	target.character_level_after = maxi(target.character_level_after, source.character_level_after)


func _new_delta(member_id: StringName) -> CharacterProgressionDelta:
	var delta: CharacterProgressionDelta = CHARACTER_PROGRESSION_DELTA_SCRIPT.new()
	delta.member_id = member_id
	return delta


func _remove_pending_character_reward_if_present(reward_id: StringName) -> void:
	if _party_state == null or reward_id == &"":
		return
	_party_state.remove_pending_character_reward(reward_id)


func _build_reward_id(member_id: StringName, source_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name(
		"%s_%s_%d" % [
			String(member_id),
			String(source_id),
			Time.get_ticks_usec(),
		]
	)


func _append_unique_string_names(target: Array[StringName], values: Array[StringName]) -> void:
	for value in values:
		_append_unique_string_name(target, value)


func _append_unique_string_name(target: Array[StringName], value: StringName) -> void:
	if value == &"" or target.has(value):
		return
	target.append(value)


func _resolve_skill_label(skill_id: StringName) -> String:
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	if skill_def != null and not skill_def.display_name.is_empty():
		return skill_def.display_name
	return String(skill_id)


func _resolve_reward_target_label(entry_type: StringName, target_id: StringName, fallback_label: String) -> String:
	if not fallback_label.is_empty():
		return fallback_label

	match entry_type:
		&"skill_unlock", &"skill_mastery":
			return _resolve_skill_label(target_id)
		&"attribute_delta":
			return _resolve_attribute_label(target_id)
		&"knowledge_unlock":
			return String(target_id)
		_:
			return String(target_id)


func _resolve_attribute_label(attribute_id: StringName) -> String:
	match attribute_id:
		UnitBaseAttributes.STRENGTH:
			return "力量"
		UnitBaseAttributes.AGILITY:
			return "敏捷"
		UnitBaseAttributes.CONSTITUTION:
			return "体质"
		UnitBaseAttributes.PERCEPTION:
			return "感知"
		UnitBaseAttributes.INTELLIGENCE:
			return "智力"
		UnitBaseAttributes.WILLPOWER:
			return "意志"
		ATTRIBUTE_SERVICE_SCRIPT.HP_MAX:
			return "生命上限"
		ATTRIBUTE_SERVICE_SCRIPT.MP_MAX:
			return "法力上限"
		ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX:
			return "体力上限"
		ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS:
			return "行动点"
		ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS:
			return "攻击加值"
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS:
			return "AC"
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_AC_BONUS:
			return "护甲 AC"
		ATTRIBUTE_SERVICE_SCRIPT.SHIELD_AC_BONUS:
			return "盾牌 AC"
		ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS:
			return "闪避加值"
		ATTRIBUTE_SERVICE_SCRIPT.DEFLECTION_BONUS:
			return "偏斜加值"
		_:
			return String(attribute_id)


func _resolve_mastery_source_type(source_type: StringName) -> StringName:
	match source_type:
		&"battle", &"battle_rating":
			return &"battle"
		&"training", &"npc_teach", &"npc", &"teaching":
			return &"training"
		_:
			return &"training"


func _build_default_source_label(source_type: StringName) -> String:
	match source_type:
		REWARD_TYPE_ACHIEVEMENT:
			return "成就奖励"
		&"battle_rating":
			return "战斗结算"
		&"battle":
			return "战斗奖励"
		&"npc_teach", &"npc", &"teaching":
			return "NPC 传授"
		&"training":
			return "训练收获"
		_:
			return "角色奖励"
