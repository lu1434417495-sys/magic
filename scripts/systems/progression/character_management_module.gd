## 文件说明：该脚本属于角色管理模块相关的模块脚本，集中维护队伍状态、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name CharacterManagementModule
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ACHIEVEMENT_PROGRESS_STATE_SCRIPT = preload("res://scripts/player/progression/achievement_progress_state.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/progression_service.gd")
const PROFESSION_RULE_SERVICE_SCRIPT = preload("res://scripts/systems/progression/profession_rule_service.gd")
const PROFESSION_ASSIGNMENT_SERVICE_SCRIPT = preload("res://scripts/systems/progression/profession_assignment_service.gd")
const SKILL_MERGE_SERVICE_SCRIPT = preload("res://scripts/systems/progression/skill_merge_service.gd")
const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const ATTRIBUTE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/attributes/attribute_source_context.gd")
const ATTRIBUTE_GROWTH_SERVICE_SCRIPT = preload("res://scripts/systems/progression/attribute_growth_service.gd")
const PASSIVE_SOURCE_CONTEXT_SCRIPT = preload("res://scripts/systems/progression/passive_source_context.gd")
const AGE_STAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/progression/age_stage_resolver.gd")
const BLOODLINE_APPLY_SERVICE_SCRIPT = preload("res://scripts/systems/progression/bloodline_apply_service.gd")
const ASCENSION_APPLY_SERVICE_SCRIPT = preload("res://scripts/systems/progression/ascension_apply_service.gd")
const STAGE_ADVANCEMENT_APPLY_SERVICE_SCRIPT = preload("res://scripts/systems/progression/stage_advancement_apply_service.gd")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const PARTY_EQUIPMENT_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_equipment_service.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const QUEST_PROGRESS_SERVICE_SCRIPT = preload("res://scripts/systems/progression/quest_progress_service.gd")
const QUEST_DEF_SCRIPT = preload("res://scripts/player/progression/quest_def.gd")
const CHARACTER_PROGRESSION_DELTA_SCRIPT = preload("res://scripts/systems/progression/character_progression_delta.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")
const PartyState = PARTY_STATE_SCRIPT
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const PassiveSourceContext = PASSIVE_SOURCE_CONTEXT_SCRIPT
const QuestDef = QUEST_DEF_SCRIPT
const AttributeSnapshot = ATTRIBUTE_SNAPSHOT_SCRIPT
const AttributeSourceContext = ATTRIBUTE_SOURCE_CONTEXT_SCRIPT
const CharacterProgressionDelta = CHARACTER_PROGRESSION_DELTA_SCRIPT
const PendingCharacterReward = PENDING_CHARACTER_REWARD_SCRIPT
const PendingCharacterRewardEntry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT
const BodySizeRules = BODY_SIZE_RULES_SCRIPT

const REWARD_TYPE_ACHIEVEMENT: StringName = &"achievement"
const REWARD_TYPE_QUEST: StringName = &"quest"
const REWARD_ENTRY_ORDER := {
	&"knowledge_unlock": 0,
	&"skill_unlock": 1,
	&"skill_mastery": 2,
	&"attribute_progress": 3,
	&"attribute_delta": 4,
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
## 字段说明：缓存 progression content bundle，供身份解析、属性上下文和 battle passive 投影共用。
var _progression_content_bundle: Dictionary = {}
## 字段说明：身份应用服务是血脉字段的唯一正式写入入口。
var _bloodline_apply_service = BLOODLINE_APPLY_SERVICE_SCRIPT.new()
## 字段说明：身份应用服务是升华字段的唯一正式写入入口。
var _ascension_apply_service = ASCENSION_APPLY_SERVICE_SCRIPT.new()
## 字段说明：身份应用服务是长期阶段提升列表的唯一正式写入入口。
var _stage_advancement_apply_service = STAGE_ADVANCEMENT_APPLY_SERVICE_SCRIPT.new()
## 字段说明：记录队伍仓库服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
## 字段说明：记录队伍装备服务，会参与运行时状态流转、系统协作和存档恢复。
var _party_equipment_service = PARTY_EQUIPMENT_SERVICE_SCRIPT.new()
## 字段说明：记录任务进度服务，会参与运行时状态流转、系统协作和存档恢复。
var _quest_progress_service = QUEST_PROGRESS_SERVICE_SCRIPT.new()
var _equipment_instance_id_allocator: Callable = Callable()


func setup(
	party_state: PartyState,
	skill_defs: Dictionary,
	profession_defs: Dictionary,
	achievement_defs: Dictionary = {},
	item_defs: Dictionary = {},
	quest_defs: Dictionary = {},
	equipment_instance_id_allocator: Callable = Callable(),
	progression_content_bundle: Dictionary = {}
) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_skill_defs = skill_defs if skill_defs != null else {}
	_profession_defs = profession_defs if profession_defs != null else {}
	_achievement_defs = achievement_defs if achievement_defs != null else {}
	_item_defs = item_defs if item_defs != null else {}
	_quest_defs = quest_defs if quest_defs != null else {}
	_progression_content_bundle = progression_content_bundle if progression_content_bundle != null else {}
	_equipment_instance_id_allocator = equipment_instance_id_allocator
	_party_warehouse_service.setup(_party_state, _item_defs, _equipment_instance_id_allocator)
	_party_equipment_service.setup(_party_state, _item_defs, _party_warehouse_service, _equipment_instance_id_allocator)
	_quest_progress_service.setup(_party_state, _quest_defs)
	_setup_identity_apply_services()


func get_party_state() -> PartyState:
	return _party_state


func get_item_defs() -> Dictionary:
	return _item_defs


func set_party_state(party_state: PartyState) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_party_warehouse_service.setup(_party_state, _item_defs, _equipment_instance_id_allocator)
	_party_equipment_service.setup(_party_state, _item_defs, _party_warehouse_service, _equipment_instance_id_allocator)
	_quest_progress_service.setup(_party_state, _quest_defs)
	_setup_identity_apply_services()


func get_race_def_for_member(member_id: StringName) -> RaceDef:
	var member_state := get_member_state(member_id)
	if member_state == null:
		return null
	return _get_content_def("race_defs", "race", member_state.race_id) as RaceDef


func get_subrace_def_for_member(member_id: StringName) -> SubraceDef:
	var member_state := get_member_state(member_id)
	if member_state == null:
		return null
	return _get_content_def("subrace_defs", "subrace", member_state.subrace_id) as SubraceDef


func get_bloodline_def_for_member(member_id: StringName) -> BloodlineDef:
	var member_state := get_member_state(member_id)
	if member_state == null or member_state.bloodline_id == &"":
		return null
	return _get_content_def("bloodline_defs", "bloodline", member_state.bloodline_id) as BloodlineDef


func get_bloodline_stage_def_for_member(member_id: StringName) -> BloodlineStageDef:
	var member_state := get_member_state(member_id)
	if member_state == null or member_state.bloodline_stage_id == &"":
		return null
	return _get_content_def("bloodline_stage_defs", "bloodline_stage", member_state.bloodline_stage_id) as BloodlineStageDef


func get_ascension_def_for_member(member_id: StringName) -> AscensionDef:
	var member_state := get_member_state(member_id)
	if member_state == null or member_state.ascension_id == &"":
		return null
	return _get_content_def("ascension_defs", "ascension", member_state.ascension_id) as AscensionDef


func get_ascension_stage_def_for_member(member_id: StringName) -> AscensionStageDef:
	var member_state := get_member_state(member_id)
	if member_state == null or member_state.ascension_stage_id == &"":
		return null
	return _get_content_def("ascension_stage_defs", "ascension_stage", member_state.ascension_stage_id) as AscensionStageDef


func get_age_stage_rule_for_member(member_id: StringName) -> AgeStageRule:
	var member_state := get_member_state(member_id)
	if member_state == null:
		return null
	var age_profile := _get_content_def("age_profile_defs", "age_profile", member_state.age_profile_id) as AgeProfileDef
	if age_profile == null:
		return null
	var effective_stage_id := member_state.effective_age_stage_id
	if effective_stage_id == &"":
		effective_stage_id = member_state.natural_age_stage_id
	for stage_rule in age_profile.stage_rules:
		if stage_rule == null or stage_rule.stage_id != effective_stage_id:
			continue
		return stage_rule
	return null


func build_attribute_source_context(member_id: StringName, equipment_state_override: Variant = null) -> AttributeSourceContext:
	var member_state := get_member_state(member_id)
	var context: AttributeSourceContext = ATTRIBUTE_SOURCE_CONTEXT_SCRIPT.new()
	if member_state == null:
		return context
	var equipment_state_variant: Variant = equipment_state_override if equipment_state_override != null else member_state.equipment_state
	context.unit_progress = member_state.progression
	context.skill_defs = _skill_defs
	context.profession_defs = _profession_defs
	context.race_def = get_race_def_for_member(member_id)
	context.subrace_def = get_subrace_def_for_member(member_id)
	context.age_stage_rule = get_age_stage_rule_for_member(member_id)
	context.age_stage_source_type = member_state.effective_age_stage_source_type
	context.age_stage_source_id = member_state.effective_age_stage_source_id
	context.bloodline_def = get_bloodline_def_for_member(member_id)
	context.bloodline_stage_def = get_bloodline_stage_def_for_member(member_id)
	context.ascension_def = get_ascension_def_for_member(member_id)
	context.ascension_stage_def = get_ascension_stage_def_for_member(member_id)
	context.versatility_pick = member_state.versatility_pick
	context.equipment_state = _party_equipment_service.build_attribute_modifiers(equipment_state_variant)
	context.stage_advancement_modifiers = _collect_active_stage_advancement_modifiers(member_state)
	return context


func build_passive_source_context(member_id: StringName, progression_state = null) -> PassiveSourceContext:
	var member_state := get_member_state(member_id)
	var context: PassiveSourceContext = PASSIVE_SOURCE_CONTEXT_SCRIPT.new()
	context.member_state = member_state
	context.unit_progress = progression_state if progression_state != null else (member_state.progression if member_state != null else null)
	if context.unit_progress != null:
		context.skill_progress_by_id = context.unit_progress.skills
	context.race_def = get_race_def_for_member(member_id)
	context.subrace_def = get_subrace_def_for_member(member_id)
	context.trait_defs = _get_content_bucket("race_trait_defs", "race_trait")
	context.bloodline_def = get_bloodline_def_for_member(member_id)
	context.bloodline_stage_def = get_bloodline_stage_def_for_member(member_id)
	context.ascension_def = get_ascension_def_for_member(member_id)
	context.ascension_stage_def = get_ascension_stage_def_for_member(member_id)
	context.stage_advancement_modifiers = _collect_active_stage_advancement_modifiers(member_state)
	return context


func get_identity_summary_for_member(member_id: StringName) -> Dictionary:
	var member_state := get_member_state(member_id)
	if member_state == null:
		return {}
	var race_def := get_race_def_for_member(member_id)
	var subrace_def := get_subrace_def_for_member(member_id)
	var bloodline_def := get_bloodline_def_for_member(member_id)
	var bloodline_stage_def := get_bloodline_stage_def_for_member(member_id)
	var ascension_def := get_ascension_def_for_member(member_id)
	var ascension_stage_def := get_ascension_stage_def_for_member(member_id)
	var natural_stage_label := _get_age_stage_display_label(
		member_state.age_profile_id,
		member_state.natural_age_stage_id
	)
	var effective_stage_label := _get_age_stage_display_label(
		member_state.age_profile_id,
		member_state.effective_age_stage_id
	)
	return {
		"race_label": _identity_def_label(race_def, member_state.race_id),
		"subrace_label": _identity_def_label(subrace_def, member_state.subrace_id),
		"age_years": int(member_state.age_years),
		"biological_age_years": int(member_state.biological_age_years),
		"astral_memory_years": int(member_state.astral_memory_years),
		"natural_age_stage_label": natural_stage_label,
		"effective_age_stage_label": effective_stage_label,
		"effective_age_stage_source_type": String(member_state.effective_age_stage_source_type),
		"effective_age_stage_source_id": String(member_state.effective_age_stage_source_id),
		"body_size": int(member_state.body_size),
		"body_size_category": String(member_state.body_size_category),
		"bloodline_label": _identity_def_label(bloodline_def, member_state.bloodline_id),
		"bloodline_stage_label": _identity_def_label(bloodline_stage_def, member_state.bloodline_stage_id),
		"ascension_label": _identity_def_label(ascension_def, member_state.ascension_id),
		"ascension_stage_label": _identity_def_label(ascension_stage_def, member_state.ascension_stage_id),
		"trait_summary": _build_identity_trait_summary_lines(
			race_def,
			subrace_def,
			get_age_stage_rule_for_member(member_id),
			bloodline_def,
			bloodline_stage_def,
			ascension_def,
			ascension_stage_def
		),
		"damage_resistances": _collect_identity_damage_resistances(race_def, subrace_def),
		"save_advantage_tags": _collect_identity_save_advantage_tags(race_def, subrace_def),
		"racial_skill_lines": _build_identity_granted_skill_lines(
			race_def,
			subrace_def,
			bloodline_def,
			bloodline_stage_def,
			ascension_def,
			ascension_stage_def
		),
	}


func apply_bloodline(member_id: StringName, bloodline_id: StringName, bloodline_stage_id: StringName) -> bool:
	var member_state := get_member_state(member_id)
	if not _bloodline_apply_service.apply_bloodline(member_state, bloodline_id, bloodline_stage_id):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func revoke_bloodline(member_id: StringName) -> bool:
	var member_state := get_member_state(member_id)
	if not _bloodline_apply_service.revoke_bloodline(member_state):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func apply_ascension(
	member_id: StringName,
	ascension_id: StringName,
	ascension_stage_id: StringName,
	current_world_step: int
) -> bool:
	var member_state := get_member_state(member_id)
	if not _ascension_apply_service.apply_ascension(member_state, ascension_id, ascension_stage_id, current_world_step):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func revoke_ascension(member_id: StringName, restore_original_race: bool = true) -> bool:
	var member_state := get_member_state(member_id)
	if not _ascension_apply_service.revoke_ascension(member_state, restore_original_race):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func add_stage_advancement_modifier(member_id: StringName, modifier_id: StringName) -> bool:
	var member_state := get_member_state(member_id)
	if not _stage_advancement_apply_service.add_stage_advancement_modifier(member_state, modifier_id):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func remove_stage_advancement_modifier(member_id: StringName, modifier_id: StringName) -> bool:
	var member_state := get_member_state(member_id)
	if not _stage_advancement_apply_service.remove_stage_advancement_modifier(member_state, modifier_id):
		return false
	_refresh_member_identity_after_apply(member_state)
	return true


func grant_racial_skill(member_id: StringName, grant: RacialGrantedSkill, source_type: StringName, source_id: StringName) -> bool:
	var member_state := get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return false
	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	return progression_service.grant_racial_skill(grant, source_type, source_id)


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
		"world_step": world_step,
		"item_id": String(item_id),
		"quantity": required_quantity,
		"context": {
			"item_id": String(item_id),
			"submitted_quantity": required_quantity,
		},
	}], world_step)
	if not (summary.get("progressed_quest_ids", []) as Array).has(quest_id):
		_party_state.warehouse_state = warehouse_state_before
		_party_warehouse_service.setup(_party_state, _item_defs, _equipment_instance_id_allocator)
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
	if not String(quest_reward_data.get("error_code", "")).is_empty():
		result["error_code"] = String(quest_reward_data["error_code"])
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


func get_member_attribute_snapshot_for_equipment_view(member_id: StringName, equipment_view: Variant) -> AttributeSnapshot:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null or member_state.progression == null:
		return ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	return _build_attribute_service(member_state, _normalize_equipment_view(equipment_view)).get_snapshot()


func get_member_weapon_projection(member_id: StringName) -> Dictionary:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null:
		return {}
	return get_member_weapon_projection_for_equipment_view(member_id, member_state.equipment_state)


func get_member_weapon_projection_for_equipment_view(member_id: StringName, equipment_view: Variant) -> Dictionary:
	var member_state: PartyMemberState = get_member_state(member_id)
	if member_state == null:
		return {}
	var resolved_equipment_view = _normalize_equipment_view(equipment_view)
	if resolved_equipment_view == null:
		return _build_unarmed_weapon_projection()
	var weapon_item_id := ProgressionDataUtils.to_string_name(resolved_equipment_view.get_equipped_item_id(&"main_hand"))
	if weapon_item_id == &"":
		return _build_unarmed_weapon_projection()
	var item_def: ItemDef = _item_defs.get(weapon_item_id) as ItemDef
	if item_def == null or not item_def.is_weapon():
		return {}
	return _build_weapon_projection_from_item_def(item_def, resolved_equipment_view)


func get_member_weapon_physical_damage_tag(member_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name(get_member_weapon_projection(member_id).get("weapon_physical_damage_tag", ""))


func _build_weapon_projection_from_item_def(item_def: ItemDef, equipment_state) -> Dictionary:
	if item_def == null or not item_def.is_weapon():
		return {}
	var profile = item_def.get("weapon_profile")
	if profile == null:
		return {}
	var one_handed_dice := _weapon_dice_to_dict(profile.get("one_handed_dice"))
	var two_handed_dice := _weapon_dice_to_dict(profile.get("two_handed_dice"))
	var properties := _weapon_profile_properties(profile)
	var is_versatile := properties.has(&"versatile")
	var uses_two_hands := _resolve_weapon_uses_two_hands(
		item_def,
		equipment_state,
		one_handed_dice,
		two_handed_dice,
		is_versatile
	)
	return {
		"weapon_profile_kind": "equipped",
		"weapon_item_id": String(item_def.item_id),
		"weapon_profile_type_id": String(ProgressionDataUtils.to_string_name(profile.get("weapon_type_id"))),
		"weapon_family": String(ProgressionDataUtils.to_string_name(profile.get("family"))),
		"weapon_current_grip": String(_resolve_weapon_current_grip(one_handed_dice, two_handed_dice, uses_two_hands)),
		"weapon_attack_range": maxi(int(profile.get("attack_range")), 0),
		"weapon_one_handed_dice": one_handed_dice,
		"weapon_two_handed_dice": two_handed_dice,
		"weapon_is_versatile": is_versatile,
		"weapon_uses_two_hands": uses_two_hands,
		"weapon_physical_damage_tag": String(item_def.get_weapon_physical_damage_tag()),
	}


func _build_unarmed_weapon_projection() -> Dictionary:
	return {
		"weapon_profile_kind": "unarmed",
		"weapon_item_id": "",
		"weapon_profile_type_id": "unarmed",
		"weapon_family": "unarmed",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 4, "flat_bonus": 0},
		"weapon_two_handed_dice": {},
		"weapon_is_versatile": false,
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_blunt",
	}


func _resolve_weapon_uses_two_hands(
	item_def: ItemDef,
	equipment_state,
	one_handed_dice: Dictionary,
	two_handed_dice: Dictionary,
	is_versatile: bool
) -> bool:
	if item_def == null:
		return false
	var occupied_slots := item_def.get_final_occupied_slot_ids(&"main_hand")
	if occupied_slots.has(&"off_hand"):
		return true
	if one_handed_dice.is_empty() and not two_handed_dice.is_empty():
		return true
	if is_versatile and not two_handed_dice.is_empty():
		return _is_off_hand_free_for_versatile(equipment_state)
	return false


func _resolve_weapon_current_grip(
	one_handed_dice: Dictionary,
	two_handed_dice: Dictionary,
	uses_two_hands: bool
) -> StringName:
	if uses_two_hands:
		return &"two_handed"
	if not one_handed_dice.is_empty():
		return &"one_handed"
	if not two_handed_dice.is_empty():
		return &"two_handed"
	return &"none"


func _is_off_hand_free_for_versatile(equipment_state) -> bool:
	if equipment_state == null or not (equipment_state is Object):
		return true
	if equipment_state.has_method("get_entry_slot_for_slot"):
		return ProgressionDataUtils.to_string_name(equipment_state.call("get_entry_slot_for_slot", &"off_hand")) == &""
	if equipment_state.has_method("get_equipped_item_id"):
		return ProgressionDataUtils.to_string_name(equipment_state.call("get_equipped_item_id", &"off_hand")) == &""
	return true


func _weapon_profile_properties(profile) -> Array[StringName]:
	var result: Array[StringName] = []
	var raw_properties: Array = []
	if profile != null and profile.has_method("get_properties"):
		raw_properties = profile.call("get_properties")
	elif profile != null:
		raw_properties = profile.get("properties")
	for raw_property in raw_properties:
		var property_id := ProgressionDataUtils.to_string_name(raw_property)
		if property_id == &"" or result.has(property_id):
			continue
		result.append(property_id)
	return result


func _weapon_dice_to_dict(dice_resource) -> Dictionary:
	if dice_resource == null:
		return {}
	var dice_count := int(dice_resource.get("dice_count"))
	var dice_sides := int(dice_resource.get("dice_sides"))
	if dice_count <= 0 or dice_sides <= 0:
		return {}
	return {
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"flat_bonus": int(dice_resource.get("flat_bonus")),
	}


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
	return grant_skill_mastery_from_source(
		member_id,
		skill_id,
		amount,
		&"battle",
		_build_default_source_label(&"battle")
	)


func grant_skill_mastery_from_source(
	member_id: StringName,
	skill_id: StringName,
	amount: int,
	source_type: StringName,
	source_label: String = "",
	reason_text: String = "",
	emit_achievement_event: bool = true
) -> CharacterProgressionDelta:
	return _grant_skill_mastery_internal(
		member_id,
		skill_id,
		amount,
		source_type,
		source_label if not source_label.is_empty() else _build_default_source_label(source_type),
		reason_text,
		emit_achievement_event
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
	var attribute_growth_service = ATTRIBUTE_GROWTH_SERVICE_SCRIPT.new()
	attribute_growth_service.setup(member_state.progression)
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
			&"attribute_progress":
				var growth_result: Dictionary = attribute_growth_service.apply_attribute_progress(
					entry.target_id,
					entry.amount,
					entry.reason_text
				)
				if bool(growth_result.get("applied", false)):
					applied_any = true
					delta.attribute_changes.append({
						"attribute_id": entry.target_id,
						"attribute_label": _resolve_reward_target_label(entry.entry_type, entry.target_id, entry.target_label),
						"progress_delta": int(growth_result.get("progress_delta", 0)),
						"progress_before": int(growth_result.get("progress_before", 0)),
						"progress_after": int(growth_result.get("progress_after", 0)),
						"delta": int(growth_result.get("attribute_delta", 0)),
						"attribute_before": int(growth_result.get("attribute_before", 0)),
						"attribute_after": int(growth_result.get("attribute_after", 0)),
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


func _setup_identity_apply_services() -> void:
	_bloodline_apply_service.setup(_progression_content_bundle)
	_ascension_apply_service.setup(_progression_content_bundle)
	_stage_advancement_apply_service.setup(_progression_content_bundle)


func _refresh_member_identity_after_apply(member_state: PartyMemberState) -> void:
	if member_state == null:
		return
	_resolve_member_body_size(member_state)
	_resolve_member_effective_age_stage(member_state)
	_revoke_orphan_racial_skills_for_member(member_state)
	_backfill_racial_granted_skills_for_member(member_state)


func _resolve_member_body_size(member_state: PartyMemberState) -> void:
	var category := _resolve_body_size_category_for_member(member_state)
	if category == &"":
		return
	member_state.body_size_category = category
	member_state.body_size = BodySizeRules.get_body_size_for_category(category)


func _resolve_body_size_category_for_member(member_state: PartyMemberState) -> StringName:
	if member_state == null:
		return &""
	var ascension_stage_def := get_ascension_stage_def_for_member(member_state.member_id)
	if ascension_stage_def != null \
		and ascension_stage_def.body_size_category_override != &"" \
		and BodySizeRules.is_valid_body_size_category(ascension_stage_def.body_size_category_override):
		return ascension_stage_def.body_size_category_override
	var subrace_def := get_subrace_def_for_member(member_state.member_id)
	if subrace_def != null \
		and subrace_def.body_size_category_override != &"" \
		and BodySizeRules.is_valid_body_size_category(subrace_def.body_size_category_override):
		return subrace_def.body_size_category_override
	var race_def := get_race_def_for_member(member_state.member_id)
	if race_def != null and BodySizeRules.is_valid_body_size_category(race_def.body_size_category):
		return race_def.body_size_category
	return &""


func _resolve_member_effective_age_stage(member_state: PartyMemberState) -> void:
	if member_state == null:
		return
	var age_profile := _get_content_def("age_profile_defs", "age_profile", member_state.age_profile_id) as AgeProfileDef
	var resolution := AGE_STAGE_RESOLVER_SCRIPT.resolve_effective_stage(
		member_state,
		age_profile,
		_collect_active_stage_advancement_modifiers(member_state),
		get_bloodline_def_for_member(member_state.member_id),
		get_bloodline_stage_def_for_member(member_state.member_id),
		get_ascension_def_for_member(member_state.member_id),
		get_ascension_stage_def_for_member(member_state.member_id)
	)
	var stage_id := ProgressionDataUtils.to_string_name(resolution.get("stage_id", ""))
	if stage_id == &"":
		stage_id = member_state.natural_age_stage_id if member_state.natural_age_stage_id != &"" else &"adult"
	member_state.effective_age_stage_id = stage_id
	member_state.effective_age_stage_source_type = ProgressionDataUtils.to_string_name(resolution.get("source_type", ""))
	member_state.effective_age_stage_source_id = ProgressionDataUtils.to_string_name(resolution.get("source_id", ""))


func _collect_active_stage_advancement_modifiers(member_state: PartyMemberState) -> Array:
	var modifiers: Array = []
	if member_state == null:
		return modifiers
	var stage_advancement_defs := _get_content_bucket("stage_advancement_defs", "stage_advancement")
	for modifier_id in member_state.active_stage_advancement_modifier_ids:
		var modifier := stage_advancement_defs.get(modifier_id) as StageAdvancementModifier
		if modifier == null:
			continue
		modifiers.append(modifier)
	return modifiers


func _backfill_racial_granted_skills_for_member(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null:
		return false
	var grant_entries := _collect_member_racial_grant_entries(member_state)
	if grant_entries.is_empty():
		return false
	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	var changed := false
	for grant_entry in grant_entries:
		var grant := grant_entry.get("grant") as RacialGrantedSkill
		var source_type := ProgressionDataUtils.to_string_name(grant_entry.get("source_type", ""))
		var source_id := ProgressionDataUtils.to_string_name(grant_entry.get("source_id", ""))
		if progression_service.grant_racial_skill(grant, source_type, source_id):
			changed = true
	return changed


func _revoke_orphan_racial_skills_for_member(member_state: PartyMemberState) -> bool:
	if member_state == null or member_state.progression == null:
		return false
	var active_grant_lookup := _collect_active_identity_grant_lookup(member_state)
	var skill_ids_to_remove: Array[StringName] = []
	for skill_key in ProgressionDataUtils.sorted_string_keys(member_state.progression.skills):
		var skill_id := StringName(skill_key)
		var skill_progress: Variant = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		var source_type := ProgressionDataUtils.to_string_name(skill_progress.granted_source_type)
		if not _is_racial_granted_source_type(source_type):
			continue
		if skill_progress.profession_granted_by != &"":
			continue
		var source_id := ProgressionDataUtils.to_string_name(skill_progress.granted_source_id)
		if active_grant_lookup.has(_identity_grant_key(source_type, source_id, skill_id)):
			continue
		skill_ids_to_remove.append(skill_id)
	if skill_ids_to_remove.is_empty():
		return false
	for skill_id in skill_ids_to_remove:
		member_state.progression.remove_skill_progress(skill_id)
	var progression_service: ProgressionService = _build_progression_service(member_state.progression)
	progression_service.refresh_runtime_state()
	return true


func _collect_active_identity_grant_lookup(member_state: PartyMemberState) -> Dictionary:
	var lookup: Dictionary = {}
	for grant_entry in _collect_member_racial_grant_entries(member_state):
		var grant := grant_entry.get("grant") as RacialGrantedSkill
		if grant == null:
			continue
		var source_type := ProgressionDataUtils.to_string_name(grant_entry.get("source_type", ""))
		var source_id := ProgressionDataUtils.to_string_name(grant_entry.get("source_id", ""))
		lookup[_identity_grant_key(source_type, source_id, grant.skill_id)] = true
	return lookup


func _collect_member_racial_grant_entries(member_state: PartyMemberState) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if member_state == null:
		return entries
	var race_def := _get_content_def("race_defs", "race", member_state.race_id) as RaceDef
	if race_def != null:
		_append_racial_grant_entries(
			entries,
			race_def.racial_granted_skills,
			UnitSkillProgress.GRANTED_SOURCE_RACE,
			member_state.race_id
		)
	var subrace_def := _get_content_def("subrace_defs", "subrace", member_state.subrace_id) as SubraceDef
	if subrace_def != null:
		_append_racial_grant_entries(
			entries,
			subrace_def.racial_granted_skills,
			UnitSkillProgress.GRANTED_SOURCE_SUBRACE,
			member_state.subrace_id
		)
	if member_state.bloodline_id != &"":
		var bloodline_def := _get_content_def("bloodline_defs", "bloodline", member_state.bloodline_id) as BloodlineDef
		if bloodline_def != null:
			_append_racial_grant_entries(
				entries,
				bloodline_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_BLOODLINE,
				member_state.bloodline_id
			)
	if member_state.bloodline_stage_id != &"":
		var bloodline_stage_def := _get_content_def("bloodline_stage_defs", "bloodline_stage", member_state.bloodline_stage_id) as BloodlineStageDef
		if bloodline_stage_def != null:
			_append_racial_grant_entries(
				entries,
				bloodline_stage_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_BLOODLINE,
				member_state.bloodline_stage_id
			)
	if member_state.ascension_id != &"":
		var ascension_def := _get_content_def("ascension_defs", "ascension", member_state.ascension_id) as AscensionDef
		if ascension_def != null:
			_append_racial_grant_entries(
				entries,
				ascension_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_ASCENSION,
				member_state.ascension_id
			)
	if member_state.ascension_stage_id != &"":
		var ascension_stage_def := _get_content_def("ascension_stage_defs", "ascension_stage", member_state.ascension_stage_id) as AscensionStageDef
		if ascension_stage_def != null:
			_append_racial_grant_entries(
				entries,
				ascension_stage_def.racial_granted_skills,
				UnitSkillProgress.GRANTED_SOURCE_ASCENSION,
				member_state.ascension_stage_id
			)
	return entries


func _append_racial_grant_entries(
	entries: Array[Dictionary],
	granted_skills: Array,
	source_type: StringName,
	source_id: StringName
) -> void:
	if source_id == &"":
		return
	for grant in granted_skills:
		if grant == null:
			continue
		entries.append({
			"grant": grant,
			"source_type": source_type,
			"source_id": source_id,
		})


func _identity_grant_key(source_type: StringName, source_id: StringName, skill_id: StringName) -> String:
	return "%s:%s:%s" % [String(source_type), String(source_id), String(skill_id)]


func _is_racial_granted_source_type(source_type: StringName) -> bool:
	return source_type == UnitSkillProgress.GRANTED_SOURCE_RACE \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_SUBRACE \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_ASCENSION \
		or source_type == UnitSkillProgress.GRANTED_SOURCE_BLOODLINE


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


func _get_content_def(primary_bucket: String, alias_bucket: String, entry_id: StringName):
	if entry_id == &"":
		return null
	var bucket := _get_content_bucket(primary_bucket, alias_bucket)
	return bucket.get(entry_id)


func _get_content_bucket(primary_bucket: String, alias_bucket: String) -> Dictionary:
	var bucket_variant: Variant = _progression_content_bundle.get(primary_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	bucket_variant = _progression_content_bundle.get(alias_bucket, {})
	if bucket_variant is Dictionary:
		return bucket_variant
	return {}


func _identity_def_label(definition, fallback_id: StringName) -> String:
	if definition != null:
		var display_name := String(definition.get("display_name")).strip_edges()
		if not display_name.is_empty():
			return display_name
	return String(fallback_id) if fallback_id != &"" else ""


func _get_age_stage_display_label(age_profile_id: StringName, stage_id: StringName) -> String:
	if stage_id == &"":
		return ""
	var age_profile := _get_content_def("age_profile_defs", "age_profile", age_profile_id) as AgeProfileDef
	if age_profile != null:
		for stage_rule in age_profile.stage_rules:
			if stage_rule == null or stage_rule.stage_id != stage_id:
				continue
			if not stage_rule.display_name.is_empty():
				return stage_rule.display_name
			break
	return String(stage_id)


func _build_identity_trait_summary_lines(
	race_def: RaceDef,
	subrace_def: SubraceDef,
	age_stage_rule: AgeStageRule,
	bloodline_def: BloodlineDef,
	bloodline_stage_def: BloodlineStageDef,
	ascension_def: AscensionDef,
	ascension_stage_def: AscensionStageDef
) -> Array[String]:
	var lines: Array[String] = []
	if race_def != null:
		_append_identity_text_lines(lines, race_def.racial_trait_summary)
	if subrace_def != null:
		_append_identity_text_lines(lines, subrace_def.racial_trait_summary)
	if age_stage_rule != null:
		_append_identity_text_lines(lines, age_stage_rule.trait_summary)
	if bloodline_def != null:
		_append_identity_text_lines(lines, bloodline_def.trait_summary)
	if bloodline_stage_def != null:
		_append_identity_text_lines(lines, bloodline_stage_def.trait_summary)
	if ascension_def != null:
		_append_identity_text_lines(lines, ascension_def.trait_summary)
	if ascension_stage_def != null:
		_append_identity_text_lines(lines, ascension_stage_def.trait_summary)
	return lines


func _append_identity_text_lines(target: Array[String], values: Array) -> void:
	for value in values:
		var text := String(value).strip_edges()
		if text.is_empty() or target.has(text):
			continue
		target.append(text)


func _collect_identity_damage_resistances(race_def: RaceDef, subrace_def: SubraceDef) -> Dictionary:
	var result := {}
	if race_def != null:
		_merge_identity_string_name_map(result, race_def.damage_resistances)
	if subrace_def != null:
		_merge_identity_string_name_map(result, subrace_def.damage_resistances)
	return result


func _merge_identity_string_name_map(target: Dictionary, source: Dictionary) -> void:
	for raw_key in source.keys():
		var key := ProgressionDataUtils.to_string_name(raw_key)
		var value := ProgressionDataUtils.to_string_name(source[raw_key])
		if key == &"" or value == &"":
			continue
		target[key] = value


func _collect_identity_save_advantage_tags(race_def: RaceDef, subrace_def: SubraceDef) -> Array[StringName]:
	var tags: Array[StringName] = []
	if race_def != null:
		_append_unique_string_names(tags, race_def.save_advantage_tags)
	if subrace_def != null:
		_append_unique_string_names(tags, subrace_def.save_advantage_tags)
	return tags


func _build_identity_granted_skill_lines(
	race_def: RaceDef,
	subrace_def: SubraceDef,
	bloodline_def: BloodlineDef,
	bloodline_stage_def: BloodlineStageDef,
	ascension_def: AscensionDef,
	ascension_stage_def: AscensionStageDef
) -> Array[String]:
	var lines: Array[String] = []
	if race_def != null:
		_append_identity_granted_skill_lines(lines, race_def.racial_granted_skills, _identity_def_label(race_def, race_def.race_id))
	if subrace_def != null:
		_append_identity_granted_skill_lines(lines, subrace_def.racial_granted_skills, _identity_def_label(subrace_def, subrace_def.subrace_id))
	if bloodline_def != null:
		_append_identity_granted_skill_lines(lines, bloodline_def.racial_granted_skills, _identity_def_label(bloodline_def, bloodline_def.bloodline_id))
	if bloodline_stage_def != null:
		_append_identity_granted_skill_lines(lines, bloodline_stage_def.racial_granted_skills, _identity_def_label(bloodline_stage_def, bloodline_stage_def.stage_id))
	if ascension_def != null:
		_append_identity_granted_skill_lines(lines, ascension_def.racial_granted_skills, _identity_def_label(ascension_def, ascension_def.ascension_id))
	if ascension_stage_def != null:
		_append_identity_granted_skill_lines(lines, ascension_stage_def.racial_granted_skills, _identity_def_label(ascension_stage_def, ascension_stage_def.stage_id))
	return lines


func _append_identity_granted_skill_lines(target: Array[String], grants: Array, source_label: String) -> void:
	for grant_variant in grants:
		var grant := grant_variant as RacialGrantedSkill
		if grant == null or grant.skill_id == &"":
			continue
		var line := "%s（%s，%s）" % [
			_resolve_skill_label(grant.skill_id),
			source_label,
			_format_identity_grant_charges(grant),
		]
		if target.has(line):
			continue
		target.append(line)


func _format_identity_grant_charges(grant: RacialGrantedSkill) -> String:
	if grant == null:
		return "无次数"
	match grant.charge_kind:
		RacialGrantedSkill.CHARGE_KIND_AT_WILL:
			return "随意"
		RacialGrantedSkill.CHARGE_KIND_PER_TURN:
			return "每回合 %d 次" % maxi(int(grant.charges), 0)
		RacialGrantedSkill.CHARGE_KIND_PER_BATTLE:
			return "每场战斗 %d 次" % maxi(int(grant.charges), 0)
		_:
			return "%s %d" % [String(grant.charge_kind), maxi(int(grant.charges), 0)]


func _build_attribute_service(member_state: PartyMemberState, equipment_state_override: Variant = null) -> AttributeService:
	var attribute_service: AttributeService = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.call("setup_context", build_attribute_source_context(member_state.member_id, equipment_state_override))
	return attribute_service


func _normalize_equipment_view(equipment_view: Variant):
	if equipment_view != null \
		and equipment_view is Object \
		and equipment_view.has_method("get_equipped_item_id"):
		return equipment_view
	if equipment_view is Dictionary:
		var restored = EQUIPMENT_STATE_SCRIPT.from_dict(equipment_view)
		return restored if restored != null else EQUIPMENT_STATE_SCRIPT.new()
	return EQUIPMENT_STATE_SCRIPT.new()


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
	_enqueue_core_max_attribute_growth_reward(member_state, skill_id, source_label)
	return delta


func _enqueue_core_max_attribute_growth_reward(
	member_state: PartyMemberState,
	skill_id: StringName,
	source_label: String
) -> void:
	if member_state == null or member_state.progression == null:
		return
	var skill_def: SkillDef = _skill_defs.get(skill_id) as SkillDef
	if skill_def == null or skill_def.attribute_growth_progress.is_empty():
		return
	var skill_progress = member_state.progression.get_skill_progress(skill_id)
	if skill_progress == null:
		return
	if not skill_progress.is_learned or not skill_progress.is_core:
		return
	if bool(skill_progress.core_max_growth_claimed):
		return
	if not SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(skill_def, skill_progress, member_state.progression):
		return

	var attribute_keys: Array[String] = []
	for raw_attribute_key in skill_def.attribute_growth_progress.keys():
		if typeof(raw_attribute_key) == TYPE_STRING:
			attribute_keys.append(String(raw_attribute_key))
	attribute_keys.sort()

	var entries: Array = []
	for attribute_key in attribute_keys:
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key)
		var amount := int(skill_def.attribute_growth_progress.get(attribute_key, 0))
		if amount <= 0:
			continue
		entries.append({
			"entry_type": "attribute_progress",
			"target_id": String(attribute_id),
			"target_label": _resolve_attribute_label(attribute_id),
			"amount": amount,
			"reason_text": "%s 满级成长" % _resolve_skill_label(skill_id),
		})
	if entries.is_empty():
		return

	skill_progress.core_max_growth_claimed = true
	member_state.progression.set_skill_progress(skill_progress)
	var reward := build_pending_character_reward(
		member_state.member_id,
		&"",
		&"skill_core_max",
		skill_id,
		source_label if not source_label.is_empty() else _resolve_skill_label(skill_id),
		entries,
		"%s 达到核心满级，获得属性成长进度。" % _resolve_skill_label(skill_id)
	)
	if reward != null and not reward.is_empty():
		enqueue_pending_character_rewards([reward])


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
		var entry_source_type := ProgressionDataUtils.to_string_name(entry_data.get(
			"mastery_source_type",
			entry_data.get("source_type", source_type)
		))
		var mastery_source_type := _resolve_mastery_source_type(entry_source_type)

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
	var quest_variant = _get_string_name_keyed_value(_quest_defs, quest_id)
	if quest_variant == null:
		return {
			"found": false,
			"error_code": "quest_def_missing",
			"display_name": "",
			"reward_entries": [],
		}
	if quest_variant is Dictionary:
		var quest_data := (quest_variant as Dictionary).duplicate(true)
		return _normalize_quest_reward_data(quest_data)
	if quest_variant is QuestDef:
		var quest_def: QuestDef = quest_variant
		return _normalize_quest_reward_data({
			"display_name": quest_def.display_name,
			"reward_entries": quest_def.reward_entries.duplicate(true),
		})
	if quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			return _normalize_quest_reward_data(quest_data_variant as Dictionary)
	return {
		"found": false,
		"error_code": "quest_def_missing",
		"display_name": "",
		"reward_entries": [],
	}


func _normalize_quest_reward_data(quest_data: Dictionary) -> Dictionary:
	var result := {
		"found": true,
		"error_code": "",
		"display_name": "",
		"reward_entries": quest_data.get("reward_entries", []),
	}
	if not quest_data.has("display_name") or quest_data["display_name"] is not String:
		result["error_code"] = "invalid_quest_display_name"
		return result
	var display_name := String(quest_data["display_name"]).strip_edges()
	if display_name.is_empty():
		result["error_code"] = "invalid_quest_display_name"
		return result
	result["display_name"] = display_name
	return result


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

	var quest_variant = _get_string_name_keyed_value(_quest_defs, quest_id)
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
		var target_value := 0
		if objective_data.has("target_value") and objective_data["target_value"] is int:
			target_value = maxi(int(objective_data["target_value"]), 0)
		result["objective_id"] = String(current_objective_id)
		result["item_id"] = String(item_id)
		result["target_value"] = target_value
		result["required_quantity"] = maxi(target_value - quest_state.get_objective_progress(current_objective_id), 0)
		if current_objective_id == &"" or item_id == &"" or target_value <= 0:
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


func _get_string_name_keyed_value(values: Dictionary, key: StringName) -> Variant:
	if key == &"":
		return null
	for value_key in values.keys():
		if typeof(value_key) != TYPE_STRING_NAME:
			continue
		if value_key == key:
			return values[value_key]
	return null


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
		result["ok"] = false
		result["error_code"] = "invalid_quest_display_name"
		return result
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
	var item_display_name := _resolve_item_reward_display_name(item_def)
	if item_display_name.is_empty():
		return {
			"ok": false,
			"error_code": "invalid_item_display_name",
		}
	return {
		"ok": true,
		"item_reward": {
			"item_id": String(reward_item_id),
			"display_name": item_display_name,
			"quantity": reward_quantity,
		},
		"warehouse_deposit_item_ids": ProgressionDataUtils.string_name_array_to_string_array(
			_build_repeated_item_ids(reward_item_id, reward_quantity)
		),
	}


func _resolve_item_reward_display_name(item_def_variant: Variant) -> String:
	if item_def_variant is Dictionary:
		var item_data := item_def_variant as Dictionary
		if not item_data.has("display_name") or item_data["display_name"] is not String:
			return ""
		return String(item_data["display_name"]).strip_edges()
	if item_def_variant is Object:
		var display_name_variant: Variant = (item_def_variant as Object).get("display_name")
		if display_name_variant is String:
			return String(display_name_variant).strip_edges()
	return ""


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
		var entry_data: Dictionary = entry_variant
		var entry_type := ProgressionDataUtils.to_string_name(entry_data.get("entry_type", ""))
		var target_id := ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
		var amount := int(entry_data.get("amount", 0))
		if entry_type == &"" or target_id == &"" or amount == 0:
			return null
		var entry = PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
		entry.entry_type = entry_type
		entry.target_id = target_id
		entry.amount = amount
		entry.target_label = String(entry_data.get("target_label", ""))
		if entry.target_label.is_empty():
			entry.target_label = _resolve_reward_target_label(entry.entry_type, entry.target_id, "")
		entry.reason_text = String(entry_data.get("reason_text", ""))
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
		&"attribute_delta", &"attribute_progress":
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
		ATTRIBUTE_SERVICE_SCRIPT.CHARACTER_HP_MAX_PERCENT_BONUS:
			return "人物生命加成%"
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
		ATTRIBUTE_SERVICE_SCRIPT.ARMOR_MAX_DEX_BONUS:
			return "护甲敏捷上限"
		_:
			return String(attribute_id)


func _resolve_mastery_source_type(source_type: StringName) -> StringName:
	var normalized_source_type := ProgressionDataUtils.to_string_name(source_type)
	match normalized_source_type:
		&"battle", &"battle_rating":
			return &"battle"
		&"training", &"npc_teach", &"npc", &"teaching":
			return &"training"
		&"heavy_hit_taken", &"max_damage_die_taken", &"elite_or_boss_damage_taken":
			return normalized_source_type
		&"":
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
