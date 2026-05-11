## 文件说明：该脚本属于成长服务相关的服务脚本，集中维护单位进度、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name ProgressionService
extends RefCounted

const SELECTION_KEY_QUALIFIER_SKILL_IDS := "selected_qualifier_skill_ids"
const SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS := "selected_assigned_core_skill_ids"
const SELECTION_KEY_HP_ROLL_OVERRIDE := "hp_roll_override"
const SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID := "_required_trigger_skill_id"
const HP_MAX_ATTRIBUTE_ID: StringName = &"hp_max"
const LOCK_HIT_BONUS_DEFAULT := 1
const PRACTICE_TRACK_MEDITATION: StringName = &"meditation"
const PRACTICE_TRACK_CULTIVATION: StringName = &"cultivation"
const MANUAL_LEARN_BLOCKED_SOURCES := {
	&"profession": true,
	&"race": true,
	&"subrace": true,
	&"ascension": true,
	&"bloodline": true,
}
const RACIAL_GRANT_SOURCES := {
	&"race": true,
	&"subrace": true,
	&"ascension": true,
	&"bloodline": true,
}
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const SKILL_MERGE_SERVICE_SCRIPT = preload("res://scripts/systems/progression/skill_merge_service.gd")
const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

## 字段说明：保存单位进度，便于顺序遍历、批量展示、批量运算和整体重建。
var _unit_progress: UnitProgress = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：缓存规则服务实例，会参与运行时状态流转、系统协作和存档恢复。
var _rule_service: ProfessionRuleService
## 字段说明：缓存分配服务实例，会参与运行时状态流转、系统协作和存档恢复。
var _assignment_service: ProfessionAssignmentService
## 字段说明：缓存技能合并服务实例，会参与复合升级的来源保留与核心迁移。
var _skill_merge_service: SkillMergeService


func setup(
	unit_progress: UnitProgress,
	skill_defs: Variant,
	profession_defs: Variant,
	rule_service: ProfessionRuleService = null,
	assignment_service: ProfessionAssignmentService = null,
	skill_merge_service: SkillMergeService = null
) -> void:
	_unit_progress = unit_progress
	_skill_defs = _index_skill_defs(skill_defs)
	_profession_defs = _index_profession_defs(profession_defs)

	_assignment_service = assignment_service if assignment_service != null else ProfessionAssignmentService.new()
	_assignment_service.setup(_unit_progress, _skill_defs, _profession_defs)

	_rule_service = rule_service if rule_service != null else ProfessionRuleService.new()
	_rule_service.setup(_unit_progress, _skill_defs, _profession_defs)

	_skill_merge_service = skill_merge_service if skill_merge_service != null else SKILL_MERGE_SERVICE_SCRIPT.new()
	_skill_merge_service.setup(_unit_progress, _skill_defs, _assignment_service)

	refresh_runtime_state()


func refresh_runtime_state() -> void:
	if _unit_progress == null:
		return

	_unit_progress.sync_active_core_skill_ids()
	_unit_progress.sync_default_combat_resource_unlocks()
	_normalize_skill_levels_to_effective_max()
	recalculate_character_level()
	if _rule_service != null:
		_rule_service.refresh_all_profession_states()
	_sync_combat_resource_unlocks_from_learned_skills()
	_refresh_cached_pending_profession_choices()


func learn_knowledge(knowledge_id: StringName) -> bool:
	if _unit_progress == null:
		return false
	if not _unit_progress.learn_knowledge(knowledge_id):
		return false
	refresh_runtime_state()
	return true


func learn_skill(skill_id: StringName) -> bool:
	var skill_def: SkillDef = _get_skill_def(skill_id)
	if _unit_progress == null or skill_def == null:
		return false
	if is_skill_relearn_blocked(skill_id):
		return false
	if _is_manual_skill_learn_source_blocked(skill_def.learn_source):
		return false
	if not _can_learn_skill_requirements(skill_def.learn_requirements):
		return false
	if not _can_satisfy_knowledge_requirements(skill_def.knowledge_requirements):
		return false
	if not _can_satisfy_skill_level_requirements(skill_def.skill_level_requirements):
		return false
	if not _can_satisfy_attribute_requirements(skill_def.attribute_requirements):
		return false
	if not _can_satisfy_achievement_requirements(skill_def.achievement_requirements):
		return false
	if skill_def.unlock_mode == &"composite_upgrade":
		if not _can_learn_composite_upgrade(skill_def):
			return false
		return _learn_composite_upgrade(skill_def)

	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_progress != null and skill_progress.is_learned:
		return false
	if skill_progress == null:
		skill_progress = _new_skill_progress()
		skill_progress.skill_id = skill_id

	skill_progress.is_learned = true
	_unit_progress.set_skill_progress(skill_progress)
	refresh_runtime_state()
	return true


func grant_racial_skill(grant: RacialGrantedSkill, source_type: StringName, source_id: StringName) -> bool:
	if _unit_progress == null or grant == null:
		return false
	if not _is_racial_grant_source_type(source_type):
		return false
	if source_id == &"" or grant.skill_id == &"":
		return false
	if int(grant.minimum_skill_level) < 0:
		return false

	var skill_def: SkillDef = _get_skill_def(grant.skill_id)
	if skill_def == null or skill_def.learn_source != source_type:
		return false

	var skill_progress: Variant = _unit_progress.get_skill_progress(grant.skill_id)
	if skill_progress != null and skill_progress.is_learned:
		return false
	if skill_progress == null:
		skill_progress = _new_skill_progress()
		skill_progress.skill_id = grant.skill_id

	skill_progress.is_learned = true
	skill_progress.skill_level = int(grant.minimum_skill_level)
	skill_progress.granted_source_type = source_type
	skill_progress.granted_source_id = source_id

	_unit_progress.set_skill_progress(skill_progress)
	refresh_runtime_state()
	return true


func _learn_composite_upgrade(skill_def: SkillDef) -> bool:
	if _unit_progress == null or skill_def == null:
		return false
	if skill_def.skill_id == &"":
		return false
	if _unit_progress.get_skill_progress(skill_def.skill_id) != null and _unit_progress.get_skill_progress(skill_def.skill_id).is_learned:
		return false

	if _skill_merge_service != null and not skill_def.upgrade_source_skill_ids.is_empty():
		if not _skill_merge_service.apply_composite_upgrade_result(
			skill_def.skill_id,
			skill_def.upgrade_source_skill_ids,
			skill_def.retain_source_skills_on_unlock,
			skill_def.core_skill_transition_mode
		):
			return false
	else:
		var skill_progress: Variant = _unit_progress.get_skill_progress(skill_def.skill_id)
		if skill_progress == null:
			skill_progress = _new_skill_progress()
			skill_progress.skill_id = skill_def.skill_id
		skill_progress.is_learned = true
		skill_progress.merged_from_skill_ids = skill_def.upgrade_source_skill_ids.duplicate()
		_unit_progress.set_skill_progress(skill_progress)

	refresh_runtime_state()
	return true


func grant_skill_mastery(skill_id: StringName, amount: int, source_type: StringName) -> bool:
	if _unit_progress == null or amount <= 0:
		return false

	var skill_def: SkillDef = _get_skill_def(skill_id)
	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_def == null or skill_progress == null:
		return false
	if not skill_progress.is_learned:
		return false
	if not skill_def.mastery_sources.is_empty() and not skill_def.mastery_sources.has(source_type):
		return false

	skill_progress.total_mastery_earned += amount
	match source_type:
		&"training":
			skill_progress.mastery_from_training += amount
		&"battle":
			skill_progress.mastery_from_battle += amount
		_:
			pass

	var effective_max_level := _get_effective_skill_max_level(skill_def, skill_progress)
	if skill_progress.skill_level >= effective_max_level:
		skill_progress.skill_level = effective_max_level
		skill_progress.current_mastery = 0
		_unit_progress.set_skill_progress(skill_progress)
		refresh_runtime_state()
		return true

	skill_progress.current_mastery += amount
	while skill_progress.skill_level < effective_max_level:
		var mastery_required: int = skill_def.get_mastery_required_for_level(skill_progress.skill_level)
		if mastery_required <= 0:
			break
		if skill_progress.current_mastery < mastery_required:
			break

		skill_progress.current_mastery -= mastery_required
		skill_progress.skill_level += 1

	if skill_progress.skill_level >= effective_max_level:
		skill_progress.skill_level = effective_max_level
		skill_progress.current_mastery = 0

	_unit_progress.set_skill_progress(skill_progress)
	refresh_runtime_state()
	return true


func set_skill_core(skill_id: StringName, enabled: bool) -> bool:
	if _unit_progress == null:
		return false

	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_progress == null or not skill_progress.is_learned:
		return false

	if enabled:
		skill_progress.is_core = true
		_unit_progress.set_skill_progress(skill_progress)
		refresh_runtime_state()
		return true

	var previous_profession_id: StringName = skill_progress.assigned_profession_id
	skill_progress.is_core = false
	skill_progress.clear_profession_assignment()
	_unit_progress.set_skill_progress(skill_progress)

	if previous_profession_id != &"":
		var profession_progress: Variant = _unit_progress.get_profession_progress(previous_profession_id)
		if profession_progress != null:
			profession_progress.remove_core_skill(skill_id)

	refresh_runtime_state()
	return true


func recalculate_character_level() -> int:
	if _unit_progress == null:
		return 0

	var rank_total: int = 0
	for profession_progress in _unit_progress.professions.values():
		if profession_progress == null:
			continue
		rank_total += int(profession_progress.rank)

	_unit_progress.character_level = rank_total
	return rank_total


func can_promote_profession(profession_id: StringName) -> bool:
	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress == null or profession_progress.rank <= 0:
		return _rule_service != null and _rule_service.can_unlock_profession(profession_id)
	return _rule_service != null and _rule_service.can_rank_up_profession(profession_id)


func promote_profession(profession_id: StringName, selection: Dictionary = {}) -> bool:
	if _unit_progress == null or _rule_service == null or _assignment_service == null:
		return false
	if not can_promote_profession(profession_id):
		return false
	var trigger_skill_id := _get_ready_active_level_trigger_skill_id()
	if trigger_skill_id == &"":
		return false

	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return false

	var profession_progress: Variant = _get_profession_progress(profession_id)
	var is_unlock: bool = profession_progress == null or profession_progress.rank <= 0
	if profession_progress == null:
		profession_progress = _new_profession_progress()
		profession_progress.profession_id = profession_id
		_unit_progress.set_profession_progress(profession_progress)

	var target_rank: int = 1 if is_unlock else profession_progress.rank + 1
	var promotion_selection: Dictionary = _resolve_promotion_selection(
		profession_id,
		target_rank,
		is_unlock,
		_with_required_trigger_skill(selection, trigger_skill_id)
	)
	if promotion_selection.is_empty():
		return false
	if not _selection_includes_skill(promotion_selection, trigger_skill_id):
		return false

	var consumed_skill_ids: Array[StringName] = _get_selection_skill_ids(promotion_selection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS)
	var qualifier_skill_ids: Array[StringName] = _get_selection_skill_ids(promotion_selection, SELECTION_KEY_QUALIFIER_SKILL_IDS)

	if is_unlock:
		for skill_id in consumed_skill_ids:
			if not _assignment_service.can_assign_core_skill_to_profession(skill_id, profession_id):
				return false
		for skill_id in consumed_skill_ids:
			if not _assignment_service.assign_core_skill_to_profession(skill_id, profession_id):
				return false

	profession_progress.rank = target_rank
	var promotion_record: ProfessionPromotionRecord = ProfessionPromotionRecord.new()
	promotion_record.new_rank = target_rank
	promotion_record.consumed_skill_ids = consumed_skill_ids.duplicate()
	promotion_record.qualifier_skill_ids = qualifier_skill_ids.duplicate()
	promotion_record.snapshot_unit_base_attributes = _get_unit_base_attributes_snapshot()
	promotion_record.timestamp = int(Time.get_unix_time_from_system())
	profession_progress.add_promotion_record(promotion_record)

	_apply_profession_hit_point_gain(profession_def, selection)
	_grant_profession_skills(profession_def, profession_progress, target_rank)
	_lock_ready_active_level_trigger_skill(trigger_skill_id)
	_unit_progress.set_profession_progress(profession_progress)
	refresh_runtime_state()
	return true


static func calculate_profession_hit_point_gain(hit_die_roll: int, constitution_value: int) -> int:
	return maxi(1, maxi(hit_die_roll, 1) + calculate_constitution_modifier(constitution_value) * 2)


static func calculate_constitution_modifier(constitution_value: int) -> int:
	return ATTRIBUTE_SNAPSHOT_SCRIPT.calculate_score_modifier(constitution_value)


func get_profession_upgrade_candidates() -> Array[PendingProfessionChoice]:
	return _build_pending_profession_choices()


func is_skill_relearn_blocked(skill_id: StringName) -> bool:
	if _unit_progress == null:
		return false
	return _unit_progress.is_skill_relearn_blocked(skill_id)


func _apply_profession_hit_point_gain(profession_def: ProfessionDef, selection: Dictionary) -> void:
	if _unit_progress == null or _unit_progress.unit_base_attributes == null or profession_def == null:
		return
	var hit_die_sides := maxi(int(profession_def.hit_die_sides), 1)
	var hit_die_roll := _roll_profession_hit_die(hit_die_sides, selection)
	var constitution_value := int(_unit_progress.unit_base_attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION))
	var hp_gain := calculate_profession_hit_point_gain(hit_die_roll, constitution_value)
	var current_hp_max := int(_unit_progress.unit_base_attributes.get_attribute_value(HP_MAX_ATTRIBUTE_ID))
	_unit_progress.unit_base_attributes.set_attribute_value(HP_MAX_ATTRIBUTE_ID, current_hp_max + hp_gain)


func _roll_profession_hit_die(hit_die_sides: int, selection: Dictionary) -> int:
	var normalized_sides := maxi(hit_die_sides, 1)
	var override_value: Variant = selection.get(SELECTION_KEY_HP_ROLL_OVERRIDE, null)
	if override_value is int:
		return clampi(int(override_value), 1, normalized_sides)
	return int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, normalized_sides))


func _is_manual_skill_learn_source_blocked(learn_source: StringName) -> bool:
	return MANUAL_LEARN_BLOCKED_SOURCES.has(learn_source)


func _is_racial_grant_source_type(source_type: StringName) -> bool:
	return RACIAL_GRANT_SOURCES.has(source_type)


func _index_skill_defs(skill_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if skill_defs is Dictionary:
		for key in skill_defs.keys():
			var skill_def: Variant = skill_defs[key]
			if skill_def is SkillDef:
				var indexed_id: StringName = skill_def.skill_id if skill_def.skill_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = skill_def
	elif skill_defs is Array:
		for skill_def in skill_defs:
			if skill_def is SkillDef and skill_def.skill_id != &"":
				indexed_defs[skill_def.skill_id] = skill_def

	return indexed_defs


func _index_profession_defs(profession_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if profession_defs is Dictionary:
		for key in profession_defs.keys():
			var profession_def: Variant = profession_defs[key]
			if profession_def is ProfessionDef:
				var indexed_id: StringName = profession_def.profession_id if profession_def.profession_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = profession_def
	elif profession_defs is Array:
		for profession_def in profession_defs:
			if profession_def is ProfessionDef and profession_def.profession_id != &"":
				indexed_defs[profession_def.profession_id] = profession_def

	return indexed_defs


func _get_skill_def(skill_id: StringName) -> SkillDef:
	return _skill_defs.get(skill_id) as SkillDef


func _normalize_skill_levels_to_effective_max() -> void:
	if _unit_progress == null:
		return
	for skill_key in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills):
		var skill_id := StringName(skill_key)
		var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
		var skill_def := _get_skill_def(skill_id)
		if skill_progress == null or skill_def == null:
			continue
		var effective_max_level := _get_effective_skill_max_level(skill_def, skill_progress)
		if int(skill_progress.skill_level) <= effective_max_level:
			continue
		skill_progress.skill_level = effective_max_level
		skill_progress.current_mastery = 0
		_unit_progress.set_skill_progress(skill_progress)


func _get_effective_skill_max_level(skill_def: SkillDef, skill_progress) -> int:
	return SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.get_effective_max_level(skill_def, skill_progress, _unit_progress)


func _get_profession_def(profession_id: StringName) -> ProfessionDef:
	return _profession_defs.get(profession_id) as ProfessionDef


func _get_profession_progress(profession_id: StringName) -> UnitProfessionProgress:
	if _unit_progress == null:
		return null
	return _unit_progress.get_profession_progress(profession_id) as UnitProfessionProgress


func _can_learn_skill_requirements(requirements: Array[StringName]) -> bool:
	if _unit_progress == null:
		return false
	if requirements.is_empty():
		return true

	for required_skill_id in requirements:
		var required_skill_progress: Variant = _unit_progress.get_skill_progress(required_skill_id)
		if required_skill_progress == null or not required_skill_progress.is_learned:
			return false

	return true


func _can_learn_composite_upgrade(skill_def: SkillDef) -> bool:
	if _unit_progress == null or skill_def == null:
		return false
	if not _can_learn_skill_requirements(skill_def.learn_requirements):
		return false
	if not _can_satisfy_knowledge_requirements(skill_def.knowledge_requirements):
		return false
	if not _can_satisfy_skill_level_requirements(skill_def.skill_level_requirements):
		return false
	if not _can_satisfy_attribute_requirements(skill_def.attribute_requirements):
		return false
	if not _can_satisfy_achievement_requirements(skill_def.achievement_requirements):
		return false
	return true


func _can_satisfy_knowledge_requirements(required_knowledge_ids: Array[StringName]) -> bool:
	if _unit_progress == null:
		return false
	for knowledge_id in required_knowledge_ids:
		if not _unit_progress.has_knowledge(knowledge_id):
			return false
	return true


func _can_satisfy_skill_level_requirements(required_skill_level_map: Dictionary) -> bool:
	if _unit_progress == null:
		return false
	for required_skill_key in required_skill_level_map.keys():
		var required_skill_id := ProgressionDataUtils.to_string_name(required_skill_key)
		var required_level := int(required_skill_level_map.get(required_skill_key, 0))
		if required_skill_id == &"" or required_level <= 0:
			return false
		var required_skill_progress: Variant = _unit_progress.get_skill_progress(required_skill_id)
		if required_skill_progress == null or not required_skill_progress.is_learned:
			return false
		if int(required_skill_progress.skill_level) < required_level:
			return false
	return true


func _can_satisfy_attribute_requirements(required_attribute_map: Dictionary) -> bool:
	if _unit_progress == null or _unit_progress.unit_base_attributes == null:
		return false
	for attribute_key_variant in required_attribute_map.keys():
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key_variant)
		var required_value := int(required_attribute_map.get(attribute_key_variant, 0))
		if attribute_id == &"" or required_value <= 0:
			return false
		if int(_unit_progress.unit_base_attributes.get_attribute_value(attribute_id)) < required_value:
			return false
	return true


func _can_satisfy_achievement_requirements(required_achievement_ids: Array[StringName]) -> bool:
	if _unit_progress == null:
		return false
	for achievement_id in required_achievement_ids:
		var progress_state = _unit_progress.get_achievement_progress_state(achievement_id)
		if progress_state == null or not progress_state.is_unlocked:
			return false
	return true


func _resolve_promotion_selection(
	profession_id: StringName,
	target_rank: int,
	is_unlock: bool,
	selection: Dictionary
) -> Dictionary:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return {}

	var tag_rules: Array[TagRequirement] = _get_tag_rules_for_target(profession_def, target_rank, is_unlock)
	var qualifier_rules: Array[TagRequirement] = _get_tag_rules_for_role(tag_rules, TagRequirement.SELECTION_ROLE_QUALIFIER)
	var assigned_core_rules: Array[TagRequirement] = _get_tag_rules_for_role(tag_rules, TagRequirement.SELECTION_ROLE_ASSIGNED_CORE)
	var allow_unassigned: bool = is_unlock
	var required_skill_ids: Array[StringName] = _get_required_skill_ids_for_target(profession_def, is_unlock)
	var required_trigger_skill_id := _get_required_trigger_skill_id(selection)
	var trigger_as_assigned_core := false
	var trigger_as_qualifier := false
	if required_trigger_skill_id != &"":
		if _can_include_skill_in_selection(required_trigger_skill_id, profession_id, assigned_core_rules, allow_unassigned):
			trigger_as_assigned_core = true
			if not required_skill_ids.has(required_trigger_skill_id):
				required_skill_ids.append(required_trigger_skill_id)
		elif _can_include_skill_in_selection(required_trigger_skill_id, profession_id, qualifier_rules, allow_unassigned):
			trigger_as_qualifier = true
		else:
			return {}

	var has_explicit_assigned_core_selection: bool = selection.has(SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS)
	var assigned_core_skill_ids: Array[StringName] = []
	if has_explicit_assigned_core_selection:
		assigned_core_skill_ids = _normalize_skill_id_selection(
			selection.get(SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS, [])
		)
	if has_explicit_assigned_core_selection:
		if not _validate_explicit_selection(
			assigned_core_skill_ids,
			profession_id,
			assigned_core_rules,
			allow_unassigned,
			required_skill_ids
		):
			return {}
	else:
		assigned_core_skill_ids = _select_skill_ids_for_tag_rules(
			profession_id,
			assigned_core_rules,
			allow_unassigned,
			required_skill_ids
		)
		if assigned_core_skill_ids.is_empty() and (not assigned_core_rules.is_empty() or not required_skill_ids.is_empty()):
			return {}

	var has_explicit_qualifier_selection: bool = selection.has(SELECTION_KEY_QUALIFIER_SKILL_IDS)
	var qualifier_skill_ids: Array[StringName] = []
	if has_explicit_qualifier_selection:
		qualifier_skill_ids = _normalize_skill_id_selection(
			selection.get(SELECTION_KEY_QUALIFIER_SKILL_IDS, [])
		)
	var qualifier_locked_skill_ids: Array[StringName] = []
	if _assigned_core_must_be_subset_of_qualifiers(profession_def, is_unlock):
		qualifier_locked_skill_ids = assigned_core_skill_ids.duplicate()
	if trigger_as_qualifier and not qualifier_locked_skill_ids.has(required_trigger_skill_id):
		qualifier_locked_skill_ids.append(required_trigger_skill_id)

	if has_explicit_qualifier_selection:
		if not _validate_explicit_selection(
			qualifier_skill_ids,
			profession_id,
			qualifier_rules,
			allow_unassigned,
			qualifier_locked_skill_ids
		):
			return {}
	else:
		qualifier_skill_ids = _select_skill_ids_for_tag_rules(
			profession_id,
			qualifier_rules,
			allow_unassigned,
			qualifier_locked_skill_ids
		)
		if qualifier_skill_ids.is_empty() and not qualifier_rules.is_empty():
			return {}

	if _assigned_core_must_be_subset_of_qualifiers(profession_def, is_unlock):
		for skill_id in assigned_core_skill_ids:
			if not qualifier_skill_ids.has(skill_id):
				return {}

	return {
		SELECTION_KEY_QUALIFIER_SKILL_IDS: qualifier_skill_ids,
		SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS: assigned_core_skill_ids,
		"trigger_skill_ids": _merge_unique_skill_ids(qualifier_skill_ids, assigned_core_skill_ids),
	}


func _validate_explicit_selection(
	selected_skill_ids: Array[StringName],
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool,
	required_skill_ids: Array[StringName]
) -> bool:
	if not _selection_contains_required_skill_ids(selected_skill_ids, required_skill_ids):
		return false

	for skill_id in selected_skill_ids:
		if required_skill_ids.has(skill_id):
			if not _is_required_skill_id_selectable(skill_id, profession_id, allow_unassigned):
				return false
			continue
		if not _matches_any_tag_rule(skill_id, profession_id, tag_rules, allow_unassigned):
			return false

	return _are_tag_rules_satisfied(selected_skill_ids, profession_id, tag_rules, allow_unassigned)


func _selection_contains_required_skill_ids(
	selected_skill_ids: Array[StringName],
	required_skill_ids: Array[StringName]
) -> bool:
	for required_skill_id in required_skill_ids:
		if not selected_skill_ids.has(required_skill_id):
			return false
	return true


func _is_required_skill_id_selectable(
	skill_id: StringName,
	profession_id: StringName,
	allow_unassigned: bool
) -> bool:
	if _unit_progress == null:
		return false

	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	var skill_def: SkillDef = _get_skill_def(skill_id)
	if skill_progress == null or skill_def == null:
		return false
	if not skill_progress.is_learned:
		return false
	if not skill_progress.is_core:
		return false
	if not SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(skill_def, skill_progress, _unit_progress):
		return false
	if skill_progress.assigned_profession_id == &"":
		return allow_unassigned
	return skill_progress.assigned_profession_id == profession_id


func _select_skill_ids_for_tag_rules(
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool,
	locked_skill_ids: Array[StringName]
) -> Array[StringName]:
	var selected_skill_ids: Array[StringName] = []
	var normalized_locked_skill_ids: Array[StringName] = _normalize_skill_id_selection(locked_skill_ids)

	for skill_id in normalized_locked_skill_ids:
		if not _can_include_skill_in_selection(skill_id, profession_id, tag_rules, allow_unassigned):
			return []
		selected_skill_ids.append(skill_id)

	if tag_rules.is_empty():
		return selected_skill_ids

	var candidate_skill_ids: Array[StringName] = _get_role_candidate_skill_ids(profession_id, tag_rules, allow_unassigned)
	while true:
		var deficits: Dictionary = _calculate_tag_rule_deficits(selected_skill_ids, profession_id, tag_rules, allow_unassigned)
		if deficits.is_empty():
			return _prune_selection(selected_skill_ids, profession_id, tag_rules, allow_unassigned, normalized_locked_skill_ids)

		var best_skill_id: StringName = &""
		var best_score: int = 0
		for skill_id in candidate_skill_ids:
			if selected_skill_ids.has(skill_id):
				continue

			var score: int = _score_skill_against_deficits(
				skill_id,
				profession_id,
				tag_rules,
				allow_unassigned,
				deficits
			)
			if score > best_score:
				best_score = score
				best_skill_id = skill_id

		if best_score <= 0 or best_skill_id == &"":
			return []

		selected_skill_ids.append(best_skill_id)

	return []


func _get_role_candidate_skill_ids(
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> Array[StringName]:
	if _rule_service == null or tag_rules.is_empty():
		return []
	return _rule_service.get_eligible_skill_ids(profession_id, tag_rules, allow_unassigned)


func _can_include_skill_in_selection(
	skill_id: StringName,
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> bool:
	if tag_rules.is_empty():
		return _is_required_skill_id_selectable(skill_id, profession_id, allow_unassigned)
	return _matches_any_tag_rule(skill_id, profession_id, tag_rules, allow_unassigned)


func _calculate_tag_rule_deficits(
	selected_skill_ids: Array[StringName],
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> Dictionary:
	var deficits: Dictionary = {}
	for index in range(tag_rules.size()):
		var tag_rule: TagRequirement = tag_rules[index]
		if tag_rule == null or tag_rule.tag == &"":
			continue

		var matched_count: int = 0
		for skill_id in selected_skill_ids:
			if _rule_service != null and _rule_service.skill_matches_tag_requirement(
				skill_id,
				profession_id,
				tag_rule,
				allow_unassigned
			):
				matched_count += 1

		var remaining: int = tag_rule.count - matched_count
		if remaining > 0:
			deficits[index] = remaining

	return deficits


func _score_skill_against_deficits(
	skill_id: StringName,
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool,
	deficits: Dictionary
) -> int:
	if _rule_service == null:
		return 0

	var score: int = 0
	for index in deficits.keys():
		var tag_rule: TagRequirement = tag_rules[int(index)]
		if tag_rule == null:
			continue
		if _rule_service.skill_matches_tag_requirement(skill_id, profession_id, tag_rule, allow_unassigned):
			score += 1

	return score


func _prune_selection(
	selected_skill_ids: Array[StringName],
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool,
	locked_skill_ids: Array[StringName]
) -> Array[StringName]:
	var pruned_selection: Array[StringName] = selected_skill_ids.duplicate()
	var normalized_locked_skill_ids: Array[StringName] = _normalize_skill_id_selection(locked_skill_ids)

	for index in range(pruned_selection.size() - 1, -1, -1):
		var skill_id: StringName = pruned_selection[index]
		if normalized_locked_skill_ids.has(skill_id):
			continue

		var trial_selection: Array[StringName] = pruned_selection.duplicate()
		trial_selection.remove_at(index)
		if _are_tag_rules_satisfied(trial_selection, profession_id, tag_rules, allow_unassigned):
			pruned_selection = trial_selection

	return pruned_selection


func _are_tag_rules_satisfied(
	selected_skill_ids: Array[StringName],
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> bool:
	return _calculate_tag_rule_deficits(selected_skill_ids, profession_id, tag_rules, allow_unassigned).is_empty()


func _matches_any_tag_rule(
	skill_id: StringName,
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> bool:
	if _rule_service == null:
		return false

	for tag_rule in tag_rules:
		if _rule_service.skill_matches_tag_requirement(skill_id, profession_id, tag_rule, allow_unassigned):
			return true
	return false


func _normalize_skill_id_selection(values: Variant) -> Array[StringName]:
	var normalized_skill_ids: Array[StringName] = []
	var seen_skill_ids: Dictionary = {}
	for skill_id in ProgressionDataUtils.to_string_name_array(values):
		if skill_id == &"" or seen_skill_ids.has(skill_id):
			continue
		seen_skill_ids[skill_id] = true
		normalized_skill_ids.append(skill_id)
	return normalized_skill_ids


func _get_tag_rules_for_target(
	profession_def: ProfessionDef,
	target_rank: int,
	is_unlock: bool
) -> Array[TagRequirement]:
	var empty_rules: Array[TagRequirement] = []
	if profession_def == null:
		return empty_rules
	if is_unlock:
		return profession_def.unlock_requirement.required_tag_rules if profession_def.unlock_requirement != null else empty_rules

	var rank_requirement: ProfessionRankRequirement = profession_def.get_rank_requirement(target_rank)
	if rank_requirement == null:
		return empty_rules
	return rank_requirement.required_tag_rules


func _get_required_skill_ids_for_target(profession_def: ProfessionDef, is_unlock: bool) -> Array[StringName]:
	if not is_unlock or profession_def == null or profession_def.unlock_requirement == null:
		return []
	return profession_def.unlock_requirement.required_skill_ids.duplicate()


func _assigned_core_must_be_subset_of_qualifiers(profession_def: ProfessionDef, is_unlock: bool) -> bool:
	return is_unlock \
		and profession_def != null \
		and profession_def.unlock_requirement != null \
		and profession_def.unlock_requirement.assigned_core_must_be_subset_of_qualifiers


func _get_tag_rules_for_role(
	tag_rules: Array[TagRequirement],
	selection_role: StringName
) -> Array[TagRequirement]:
	var role_rules: Array[TagRequirement] = []
	for tag_rule in tag_rules:
		if tag_rule == null:
			continue
		if tag_rule.get_normalized_selection_role() != selection_role:
			continue
		role_rules.append(tag_rule)
	return role_rules


func _get_selection_skill_ids(selection: Dictionary, key: String) -> Array[StringName]:
	return _normalize_skill_id_selection(selection.get(key, []))


func _merge_unique_skill_ids(
	first_skill_ids: Array[StringName],
	second_skill_ids: Array[StringName]
) -> Array[StringName]:
	var merged_skill_ids: Array[StringName] = []
	var seen_skill_ids: Dictionary = {}

	for skill_id in first_skill_ids:
		if skill_id == &"" or seen_skill_ids.has(skill_id):
			continue
		seen_skill_ids[skill_id] = true
		merged_skill_ids.append(skill_id)

	for skill_id in second_skill_ids:
		if skill_id == &"" or seen_skill_ids.has(skill_id):
			continue
		seen_skill_ids[skill_id] = true
		merged_skill_ids.append(skill_id)

	return merged_skill_ids


func _build_pending_profession_choices() -> Array[PendingProfessionChoice]:
	var results: Array[PendingProfessionChoice] = []
	if _unit_progress == null:
		return results
	var trigger_skill_id := _get_ready_active_level_trigger_skill_id()
	if trigger_skill_id == &"":
		return results

	for profession_id in _get_sorted_profession_ids():
		if not can_promote_profession(profession_id):
			continue

		var profession_progress: Variant = _get_profession_progress(profession_id)
		var is_unlock: bool = profession_progress == null or profession_progress.rank <= 0
		var target_rank: int = 1 if is_unlock else profession_progress.rank + 1
		var choice: PendingProfessionChoice = _build_pending_profession_choice(profession_id, target_rank, is_unlock, trigger_skill_id)
		if choice != null:
			results.append(choice)

	return results


func _build_pending_profession_choice(
	profession_id: StringName,
	target_rank: int,
	is_unlock: bool,
	trigger_skill_id: StringName = &""
) -> PendingProfessionChoice:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return null

	var tag_rules: Array[TagRequirement] = _get_tag_rules_for_target(profession_def, target_rank, is_unlock)
	var qualifier_rules: Array[TagRequirement] = _get_tag_rules_for_role(tag_rules, TagRequirement.SELECTION_ROLE_QUALIFIER)
	var assigned_core_rules: Array[TagRequirement] = _get_tag_rules_for_role(tag_rules, TagRequirement.SELECTION_ROLE_ASSIGNED_CORE)
	var allow_unassigned: bool = is_unlock

	var choice: PendingProfessionChoice = PendingProfessionChoice.new()
	choice.candidate_profession_ids.append(profession_id)
	choice.set_target_rank(profession_id, target_rank)
	choice.qualifier_skill_pool_ids = _get_role_candidate_skill_ids(profession_id, qualifier_rules, allow_unassigned)
	choice.assignable_skill_candidate_ids = _get_role_candidate_skill_ids(profession_id, assigned_core_rules, allow_unassigned)

	for required_skill_id in _get_required_skill_ids_for_target(profession_def, is_unlock):
		if not choice.assignable_skill_candidate_ids.has(required_skill_id):
			choice.assignable_skill_candidate_ids.append(required_skill_id)

	var default_selection: Dictionary = _resolve_promotion_selection(
		profession_id,
		target_rank,
		is_unlock,
		_with_required_trigger_skill({}, trigger_skill_id)
	)
	if trigger_skill_id != &"" and default_selection.is_empty():
		return null
	if not default_selection.is_empty():
		choice.trigger_skill_ids = _get_selection_skill_ids(default_selection, "trigger_skill_ids")
		choice.required_qualifier_count = _get_selection_skill_ids(default_selection, SELECTION_KEY_QUALIFIER_SKILL_IDS).size()
		choice.required_assigned_core_count = _get_selection_skill_ids(default_selection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS).size()

	return choice


func _get_ready_active_level_trigger_skill_id() -> StringName:
	if _unit_progress == null:
		return &""
	var trigger_skill_id := _unit_progress.active_level_trigger_core_skill_id
	if trigger_skill_id == &"":
		return &""
	var skill_progress: Variant = _unit_progress.get_skill_progress(trigger_skill_id)
	var skill_def := _get_skill_def(trigger_skill_id)
	if skill_progress == null or skill_def == null:
		return &""
	if not bool(skill_progress.is_learned) or not bool(skill_progress.is_core):
		return &""
	if bool(skill_progress.is_level_trigger_locked):
		return &""
	if _unit_progress.locked_level_trigger_skill_ids.has(trigger_skill_id):
		return &""
	if not SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(skill_def, skill_progress, _unit_progress):
		return &""
	return trigger_skill_id


func _with_required_trigger_skill(selection: Dictionary, trigger_skill_id: StringName) -> Dictionary:
	var resolved_selection := selection.duplicate(true) if selection != null else {}
	if trigger_skill_id != &"":
		resolved_selection[SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID] = trigger_skill_id
	return resolved_selection


func _get_required_trigger_skill_id(selection: Dictionary) -> StringName:
	if selection == null:
		return &""
	return ProgressionDataUtils.to_string_name(selection.get(SELECTION_KEY_REQUIRED_TRIGGER_SKILL_ID, ""))


func _selection_includes_skill(selection: Dictionary, skill_id: StringName) -> bool:
	if skill_id == &"":
		return false
	return _get_selection_skill_ids(selection, "trigger_skill_ids").has(skill_id) \
		or _get_selection_skill_ids(selection, SELECTION_KEY_QUALIFIER_SKILL_IDS).has(skill_id) \
		or _get_selection_skill_ids(selection, SELECTION_KEY_ASSIGNED_CORE_SKILL_IDS).has(skill_id)


func _lock_ready_active_level_trigger_skill(skill_id: StringName) -> bool:
	if _unit_progress == null or skill_id == &"":
		return false
	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_progress == null:
		return false
	skill_progress.is_level_trigger_active = false
	skill_progress.is_level_trigger_locked = true
	skill_progress.bonus_to_hit_from_lock = LOCK_HIT_BONUS_DEFAULT
	_unit_progress.active_level_trigger_core_skill_id = &""
	if not _unit_progress.locked_level_trigger_skill_ids.has(skill_id):
		_unit_progress.locked_level_trigger_skill_ids.append(skill_id)
	_unit_progress.set_skill_progress(skill_progress)
	return true


func _refresh_cached_pending_profession_choices() -> void:
	if _unit_progress == null:
		return
	_unit_progress.pending_profession_choices = _build_pending_profession_choices()


func _grant_profession_skills(
	profession_def: ProfessionDef,
	profession_progress: Variant,
	target_rank: int
) -> void:
	if profession_def == null or profession_progress == null:
		return

	for granted_skill: ProfessionGrantedSkill in profession_def.get_granted_skills_for_rank(target_rank):
		if granted_skill == null:
			continue
		if granted_skill.skill_id == &"":
			continue

		profession_progress.add_granted_skill(granted_skill.skill_id)

		var skill_progress: Variant = _unit_progress.get_skill_progress(granted_skill.skill_id)
		var was_already_learned := skill_progress != null and bool(skill_progress.is_learned)
		if skill_progress == null:
			skill_progress = _new_skill_progress()
			skill_progress.skill_id = granted_skill.skill_id

		skill_progress.is_learned = true
		if skill_progress.profession_granted_by == &"":
			skill_progress.profession_granted_by = profession_def.profession_id
		if not was_already_learned:
			skill_progress.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PROFESSION
			skill_progress.granted_source_id = profession_def.profession_id

		_unit_progress.set_skill_progress(skill_progress)


func _sync_combat_resource_unlocks_from_learned_skills() -> void:
	if _unit_progress == null:
		return
	for skill_key in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills):
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		var skill_def := _get_skill_def(skill_id)
		if skill_def != null and skill_def.tags.has(PRACTICE_TRACK_MEDITATION):
			_unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_MP)
		if skill_def != null and skill_def.tags.has(PRACTICE_TRACK_CULTIVATION):
			_unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_AURA)
		_unlock_combat_resources_for_skill(skill_def, maxi(int(skill_progress.skill_level), 1))


func _unlock_combat_resources_for_skill(skill_def: SkillDef, skill_level: int) -> void:
	if _unit_progress == null or skill_def == null or skill_def.combat_profile == null:
		return
	var costs := skill_def.combat_profile.get_effective_resource_costs(skill_level)
	if int(costs.get("mp_cost", 0)) > 0:
		_unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_MP)
	if int(costs.get("aura_cost", 0)) > 0:
		_unit_progress.unlock_combat_resource(UnitProgress.COMBAT_RESOURCE_AURA)


func _get_unit_base_attributes_snapshot() -> Dictionary:
	if _unit_progress == null or _unit_progress.unit_base_attributes == null:
		return {}
	return _unit_progress.unit_base_attributes.to_dict()


func _get_sorted_profession_ids() -> Array[StringName]:
	var sorted_ids: Array[StringName] = []
	for profession_id_str in ProgressionDataUtils.sorted_string_keys(_profession_defs):
		sorted_ids.append(StringName(profession_id_str))
	return sorted_ids


func _new_skill_progress() -> UnitSkillProgress:
	return UNIT_SKILL_PROGRESS_SCRIPT.new()


func _new_profession_progress() -> UnitProfessionProgress:
	return UNIT_PROFESSION_PROGRESS_SCRIPT.new()
