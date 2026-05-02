## 文件说明：该脚本属于聚落研究服务相关的服务脚本，集中处理 research 入口的正式交付、基础花费和 canonical 结果构造。
## 审查重点：重点核对 interaction_script_id、金币消耗、失败反馈以及 SettlementServiceResult 的字段稳定性。
## 备注：research 奖励通过 pending_character_rewards 延迟确认，避免直接改写角色成长真相源。

class_name SettlementResearchService
extends RefCounted

const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement/settlement_service_result.gd")

const RESEARCH_INTERACTION_ID := "service_research"
const RESEARCH_GOLD_COST := 200
const RESEARCH_SOURCE_TYPE: StringName = &"npc_teach"
const REQUIRED_SERVICE_PAYLOAD_STRING_FIELDS := [
	"facility_name",
	"npc_name",
	"service_type",
]
const REQUIRED_RESEARCH_CANDIDATE_STRING_FIELDS := [
	"research_id",
	"entry_type",
	"target_id",
	"target_label",
	"reason_text",
]
const RESEARCH_REWARD_CATALOG := [
	{
		"research_id": "research_field_manual",
		"entry_type": "knowledge_unlock",
		"target_id": "field_manual",
		"target_label": "野外手册",
		"reason_text": "研究员整理出一份可长期翻阅的野外手册抄本。",
	},
	{
		"research_id": "research_guard_break",
		"entry_type": "skill_unlock",
		"target_id": "warrior_guard_break",
		"target_label": "裂甲斩",
		"reason_text": "研究记录补全了裂甲斩的动作拆解。",
	},
]


func is_supported_interaction(interaction_script_id: String) -> bool:
	return interaction_script_id.strip_edges() == RESEARCH_INTERACTION_ID


func build_service_metadata(party_state, payload: Dictionary = {}) -> Dictionary:
	var can_afford_research: bool = party_state != null and party_state.can_afford(RESEARCH_GOLD_COST)
	var catalog_schema_error := _validate_research_catalog_schema()
	var member_availability := _build_member_research_availability(party_state, can_afford_research, catalog_schema_error)
	var requested_member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	var has_available_research := false
	var member_disabled_reason := ""
	if requested_member_id != &"":
		var selected_availability: Dictionary = member_availability.get(String(requested_member_id), {})
		has_available_research = bool(selected_availability.get("has_available_research", false))
		member_disabled_reason = String(selected_availability.get("disabled_reason", "暂无可研究内容"))
	else:
		for availability_variant in member_availability.values():
			if availability_variant is Dictionary and bool((availability_variant as Dictionary).get("has_available_research", false)):
				has_available_research = true
				break
	var is_enabled := can_afford_research and has_available_research
	var disabled_reason := ""
	if not catalog_schema_error.is_empty():
		disabled_reason = "研究配置无效"
	elif not can_afford_research:
		disabled_reason = "金币不足"
	elif not has_available_research:
		disabled_reason = member_disabled_reason if not member_disabled_reason.is_empty() else "暂无可研究内容"
	return {
		"cost_label": "%d 金" % RESEARCH_GOLD_COST,
		"is_enabled": is_enabled,
		"disabled_reason": disabled_reason,
		"member_availability": member_availability,
	}


func execute(
	settlement: Dictionary,
	payload: Dictionary,
	party_state,
	quest_progress_events: Array = []
) -> Dictionary:
	if party_state == null:
		return _build_result(false, "当前不存在队伍数据。", quest_progress_events)
	var schema_error := _validate_execution_schema(settlement, payload)
	if not schema_error.is_empty():
		return _build_result(false, schema_error, quest_progress_events)
	var catalog_schema_error := _validate_research_catalog_schema()
	if not catalog_schema_error.is_empty():
		return _build_result(false, catalog_schema_error, quest_progress_events)

	var member_state = _resolve_target_member_state(party_state, payload)
	if member_state == null or member_state.progression == null:
		return _build_result(false, "当前没有可承接研究的成员。", quest_progress_events)

	var research_candidate := _select_research_candidate(party_state, member_state)
	if research_candidate.is_empty():
		return _build_result(false, "%s 当前暂无可研究的新内容。" % _resolve_member_name(member_state), quest_progress_events)

	var facility_name := String(payload["facility_name"]).strip_edges()
	var npc_name := String(payload["npc_name"]).strip_edges()
	var service_type := String(payload["service_type"]).strip_edges()
	var pending_reward := _build_pending_research_reward(
		member_state,
		research_candidate,
		facility_name,
		npc_name,
		service_type
	)
	if pending_reward.is_empty():
		return _build_result(false, "当前研究成果构造失败。", quest_progress_events)

	var settlement_name := String(settlement["display_name"]).strip_edges()
	var reward_entry := _get_first_reward_entry(pending_reward)
	var reward_label := String(reward_entry.get("target_label", "")).strip_edges()
	if reward_label.is_empty():
		return _build_result(false, "当前研究成果构造失败。", quest_progress_events)
	if not party_state.spend_gold(RESEARCH_GOLD_COST):
		return _build_result(false, "金币不足，无法委托研究。", quest_progress_events)
	var message := "%s 的 %s 已收下 %d 金研究经费，由 %s 启动本次%s委托。" % [
		settlement_name,
		facility_name,
		RESEARCH_GOLD_COST,
		npc_name,
		service_type,
	]
	message += "已整理出新成果：%s。" % reward_label
	return _build_result(
		true,
		message,
		quest_progress_events,
		true,
		-RESEARCH_GOLD_COST,
		[pending_reward],
		{
			"research_interaction_id": RESEARCH_INTERACTION_ID,
			"gold_spent": RESEARCH_GOLD_COST,
			"facility_name": facility_name,
			"research_source_id": String(pending_reward.get("source_id", "")),
			"research_entry_type": String(reward_entry.get("entry_type", "")),
			"research_target_id": String(reward_entry.get("target_id", "")),
		}
	)


func _build_result(
	success: bool,
	message: String,
	quest_progress_events: Array,
	persist_party_state: bool = false,
	gold_delta: int = 0,
	pending_character_rewards: Array = [],
	service_side_effects: Dictionary = {}
) -> Dictionary:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.success = success
	result.message = message
	result.persist_party_state = persist_party_state
	result.gold_delta = gold_delta
	result.pending_character_rewards = _duplicate_dictionary_array(pending_character_rewards)
	result.quest_progress_events = _duplicate_dictionary_array(quest_progress_events)
	result.service_side_effects = service_side_effects.duplicate(true) if service_side_effects is Dictionary else {}
	return result.to_dictionary()


func _validate_execution_schema(settlement: Dictionary, payload: Dictionary) -> String:
	var payload_error := _validate_required_string_fields(
		payload,
		REQUIRED_SERVICE_PAYLOAD_STRING_FIELDS,
		"research payload"
	)
	if not payload_error.is_empty():
		return payload_error
	return _validate_required_string_fields(settlement, ["display_name"], "settlement")


func _validate_research_catalog_schema() -> String:
	var index := 0
	for candidate_variant in _get_research_reward_catalog():
		if candidate_variant is not Dictionary:
			return "研究候选配置无效：catalog[%d] 必须是 Dictionary。" % index
		var candidate_error := _validate_research_candidate_schema(candidate_variant, index)
		if not candidate_error.is_empty():
			return candidate_error
		index += 1
	return ""


func _validate_research_candidate_schema(research_candidate: Dictionary, index: int = -1) -> String:
	var schema_label := "research candidate"
	if index >= 0:
		schema_label = "research candidate[%d]" % index
	return _validate_required_string_fields(
		research_candidate,
		REQUIRED_RESEARCH_CANDIDATE_STRING_FIELDS,
		schema_label
	)


func _validate_required_string_fields(data: Dictionary, field_names: Array, schema_label: String) -> String:
	for field_name_variant in field_names:
		var field_name := String(field_name_variant)
		if not data.has(field_name):
			return "%s.%s 必须显式提供非空 String。" % [schema_label, field_name]
		var value: Variant = data[field_name]
		if value is not String:
			return "%s.%s 必须显式提供非空 String。" % [schema_label, field_name]
		if String(value).strip_edges().is_empty():
			return "%s.%s 必须显式提供非空 String。" % [schema_label, field_name]
	return ""


func _get_research_reward_catalog() -> Array:
	return RESEARCH_REWARD_CATALOG


func _resolve_target_member_state(party_state, payload: Dictionary):
	if party_state == null:
		return null
	var requested_member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if requested_member_id != &"":
		return party_state.get_member_state(requested_member_id)
	var default_member_id := _resolve_default_member_id(party_state)
	return party_state.get_member_state(default_member_id) if default_member_id != &"" else null


func _resolve_default_member_id(party_state) -> StringName:
	if party_state == null:
		return &""
	if party_state.leader_member_id != &"" and party_state.get_member_state(party_state.leader_member_id) != null:
		return party_state.leader_member_id
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"" and party_state.get_member_state(member_id) != null:
			return member_id
	return &""


func _build_member_research_availability(party_state, can_afford_research: bool, catalog_schema_error: String) -> Dictionary:
	var availability_by_member: Dictionary = {}
	if party_state == null:
		return availability_by_member
	for member_id in _collect_rostered_member_ids(party_state):
		var member_state = party_state.get_member_state(member_id)
		var has_candidate := catalog_schema_error.is_empty() and member_state != null and member_state.progression != null and not _select_research_candidate(party_state, member_state).is_empty()
		var disabled_reason := ""
		if not catalog_schema_error.is_empty():
			disabled_reason = "研究配置无效"
		elif not can_afford_research:
			disabled_reason = "金币不足"
		elif not has_candidate:
			disabled_reason = "暂无可研究内容"
		availability_by_member[String(member_id)] = {
			"member_id": String(member_id),
			"has_available_research": has_candidate,
			"is_enabled": can_afford_research and has_candidate,
			"disabled_reason": disabled_reason,
		}
	return availability_by_member


func _collect_rostered_member_ids(party_state) -> Array[StringName]:
	var member_ids: Array[StringName] = []
	if party_state == null:
		return member_ids
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"" and not member_ids.has(member_id):
			member_ids.append(member_id)
	for member_id_variant in party_state.reserve_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"" and not member_ids.has(member_id):
			member_ids.append(member_id)
	return member_ids


func _select_research_candidate(party_state, member_state) -> Dictionary:
	if member_state == null or member_state.progression == null:
		return {}
	var reserved_targets := _collect_pending_reward_targets(party_state, member_state.member_id)
	for candidate_variant in _get_research_reward_catalog():
		if candidate_variant is not Dictionary:
			continue
		var candidate: Dictionary = (candidate_variant as Dictionary).duplicate(true)
		var entry_type := StringName(String(candidate["entry_type"]).strip_edges())
		var target_id := StringName(String(candidate["target_id"]).strip_edges())
		if target_id == &"":
			continue
		if reserved_targets.has(_build_reward_target_key(entry_type, target_id)):
			continue
		match entry_type:
			&"knowledge_unlock":
				if not member_state.progression.has_knowledge(target_id):
					return candidate
			&"skill_unlock":
				var skill_progress = member_state.progression.get_skill_progress(target_id)
				if skill_progress == null or not skill_progress.is_learned:
					return candidate
			_:
				continue
	return {}


func _collect_pending_reward_targets(party_state, member_id: StringName) -> Dictionary:
	var targets := {}
	if party_state == null or member_id == &"":
		return targets
	var pending_rewards_variant: Variant = party_state.pending_character_rewards
	if pending_rewards_variant is not Array:
		return targets
	for reward_variant in pending_rewards_variant:
		if reward_variant == null or reward_variant.member_id != member_id:
			continue
		for entry_variant in reward_variant.entries:
			if entry_variant == null or entry_variant.is_empty():
				continue
			targets[_build_reward_target_key(entry_variant.entry_type, entry_variant.target_id)] = true
	return targets


func _build_reward_target_key(entry_type: StringName, target_id: StringName) -> StringName:
	return StringName("%s|%s" % [String(entry_type), String(target_id)])


func _build_pending_research_reward(
	member_state,
	research_candidate: Dictionary,
	facility_name: String,
	npc_name: String,
	service_type: String
) -> Dictionary:
	if member_state == null or member_state.progression == null or research_candidate.is_empty():
		return {}
	if not _validate_research_candidate_schema(research_candidate).is_empty():
		return {}
	var target_id := String(research_candidate["target_id"]).strip_edges()
	var target_label := String(research_candidate["target_label"]).strip_edges()
	var research_id := String(research_candidate["research_id"]).strip_edges()
	var entry_type := String(research_candidate["entry_type"]).strip_edges()
	var reason_text := String(research_candidate["reason_text"]).strip_edges()
	var source_label := _build_reward_source_label(facility_name, npc_name, service_type)
	var summary_text := "%s 为 %s 整理出新的研究成果：%s。" % [
		npc_name,
		_resolve_member_name(member_state),
		target_label,
	]
	return {
		"reward_id": "%s_%s_reward" % [String(member_state.member_id), research_id],
		"member_id": String(member_state.member_id),
		"member_name": _resolve_member_name(member_state),
		"source_type": String(RESEARCH_SOURCE_TYPE),
		"source_id": research_id,
		"source_label": source_label,
		"summary_text": summary_text,
		"entries": [
			{
				"entry_type": entry_type,
				"target_id": target_id,
				"target_label": target_label,
				"amount": 1,
				"reason_text": reason_text,
			},
		],
	}


func _build_reward_source_label(_facility_name: String, npc_name: String, service_type: String) -> String:
	return "%s·%s" % [npc_name, service_type]


func _resolve_member_name(member_state) -> String:
	if member_state == null:
		return "成员"
	var display_name := String(member_state.display_name)
	if not display_name.is_empty():
		return display_name
	return String(member_state.member_id)


func _get_first_reward_entry(reward_data: Dictionary) -> Dictionary:
	var entries_variant = reward_data.get("entries", [])
	if entries_variant is not Array or (entries_variant as Array).is_empty():
		return {}
	var first_entry = (entries_variant as Array)[0]
	return (first_entry as Dictionary).duplicate(true) if first_entry is Dictionary else {}


func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result
