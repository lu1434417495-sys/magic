## 文件说明：该脚本属于职业规则服务相关的服务脚本，集中维护单位进度、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name ProfessionRuleService
extends RefCounted

const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")

## 字段说明：保存单位进度，便于顺序遍历、批量展示、批量运算和整体重建。
var _unit_progress: UnitProgress = null
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}


func setup(unit_progress: UnitProgress, skill_defs: Variant, profession_defs: Variant) -> void:
	_unit_progress = unit_progress
	_skill_defs = _index_skill_defs(skill_defs)
	_profession_defs = _index_profession_defs(profession_defs)


func is_profession_knowledge_unlocked(profession_id: StringName) -> bool:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return false
	if not profession_def.requires_knowledge_unlock():
		return true
	if _unit_progress == null:
		return false
	return _unit_progress.has_knowledge(profession_def.unlock_knowledge_id)


func can_unlock_profession(profession_id: StringName) -> bool:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return false
	if not is_profession_knowledge_unlocked(profession_id):
		return false

	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress != null and profession_progress.rank > 0:
		return false

	var unlock_requirement: ProfessionPromotionRequirement = profession_def.unlock_requirement
	if unlock_requirement == null:
		return true

	if not _can_satisfy_required_skill_ids_for_unlock(profession_id, unlock_requirement.required_skill_ids):
		return false
	if not _can_satisfy_tag_rules_for_unlock(profession_id, unlock_requirement.required_tag_rules):
		return false
	if not can_satisfy_profession_gates(unlock_requirement.required_profession_ranks):
		return false
	if not can_satisfy_attribute_rules(unlock_requirement.required_attribute_rules):
		return false
	if not can_satisfy_reputation_rules(unlock_requirement.required_reputation_rules):
		return false

	return true


func can_rank_up_profession(profession_id: StringName) -> bool:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	if profession_def == null:
		return false
	if not is_profession_knowledge_unlocked(profession_id):
		return false

	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress == null or profession_progress.rank <= 0:
		return false
	if profession_progress.rank >= profession_def.max_rank:
		return false

	var target_rank: int = profession_progress.rank + 1
	var rank_requirement: ProfessionRankRequirement = profession_def.get_rank_requirement(target_rank)
	if rank_requirement == null:
		return false

	var preview_assigned_skill_ids := _get_rank_up_preview_assigned_core_skill_ids(profession_id)
	if not _can_satisfy_tag_rules_with_skill_ids(
		_get_rank_up_candidate_skill_ids(profession_id, preview_assigned_skill_ids),
		profession_id,
		rank_requirement.required_tag_rules,
		false,
		preview_assigned_skill_ids
	):
		return false
	if not can_satisfy_profession_gates(rank_requirement.required_profession_ranks):
		return false

	return true


func can_satisfy_tag_rules(profession_id: StringName, tag_rules: Array[TagRequirement]) -> bool:
	return _can_satisfy_tag_rules_with_skill_ids(
		_get_rank_up_candidate_skill_ids(profession_id),
		profession_id,
		tag_rules,
		false
	)


func can_satisfy_profession_gates(gates: Array[ProfessionRankGate]) -> bool:
	for gate in gates:
		if gate == null:
			continue

		var profession_progress: Variant = _get_profession_progress(gate.profession_id)
		if profession_progress == null:
			return false
		if profession_progress.rank < gate.min_rank:
			return false

		var check_mode: StringName = _resolve_gate_check_mode(gate)
		if check_mode == &"active_only":
			if not profession_progress.is_active or profession_progress.is_hidden:
				return false

	return true


func can_satisfy_attribute_rules(rules: Array[AttributeRequirement]) -> bool:
	var unit_base_attributes: UnitBaseAttributes = _get_unit_base_attributes()
	if unit_base_attributes == null:
		return rules.is_empty()

	for rule in rules:
		if rule == null:
			continue

		var value: int = int(unit_base_attributes.get_attribute_value(rule.attribute_id))
		if not rule.matches_value(value):
			return false

	return true


func get_eligible_skill_ids(
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> Array[StringName]:
	var eligible_skill_ids: Array[StringName] = []
	if _unit_progress == null or tag_rules.is_empty():
		return eligible_skill_ids

	for skill_id in _get_all_learned_skill_ids():
		if _matches_any_tag_rule(skill_id, profession_id, tag_rules, allow_unassigned):
			eligible_skill_ids.append(skill_id)

	return eligible_skill_ids


func skill_matches_tag_requirement(
	skill_id: StringName,
	profession_id: StringName,
	tag_rule: TagRequirement,
	allow_unassigned: bool,
	preview_assigned_skill_ids: Array[StringName] = []
) -> bool:
	return _matches_tag_requirement(skill_id, profession_id, tag_rule, allow_unassigned, preview_assigned_skill_ids)


func can_satisfy_reputation_rules(rules: Array[ReputationRequirement]) -> bool:
	var reputation_state: Variant = _get_reputation_state()
	if reputation_state == null:
		return rules.is_empty()

	for rule in rules:
		if rule == null:
			continue

		var value: int = int(reputation_state.get_reputation_value(rule.state_id))
		if not rule.matches_value(value):
			return false

	return true


func evaluate_profession_active_state(profession_id: StringName) -> bool:
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_def == null or profession_progress == null:
		return false
	if profession_progress.rank <= 0:
		return false

	return _are_active_conditions_satisfied(profession_def)


func refresh_all_profession_states() -> void:
	if _unit_progress == null:
		return

	for profession_key in _unit_progress.professions.keys():
		var profession_id: StringName = ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress: Variant = _get_profession_progress(profession_id)
		var profession_def: ProfessionDef = _get_profession_def(profession_id)
		if profession_progress == null or profession_def == null:
			continue

		if profession_progress.rank <= 0:
			profession_progress.is_active = false
			profession_progress.is_hidden = false
			profession_progress.inactive_reason = &""
			continue

		var conditions_satisfied: bool = _are_active_conditions_satisfied(profession_def)
		if conditions_satisfied:
			if profession_progress.is_active:
				profession_progress.is_hidden = false
				profession_progress.inactive_reason = &""
				continue

			if profession_def.reactivation_mode == &"auto":
				profession_progress.is_active = true
				profession_progress.is_hidden = false
				profession_progress.inactive_reason = &""
			else:
				profession_progress.is_hidden = true
				profession_progress.inactive_reason = &"manual_reactivation_required"
		else:
			profession_progress.is_active = false
			profession_progress.is_hidden = true
			profession_progress.inactive_reason = &"active_conditions_not_met"


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


func _get_profession_def(profession_id: StringName) -> ProfessionDef:
	return _profession_defs.get(profession_id) as ProfessionDef


func _get_skill_def(skill_id: StringName) -> SkillDef:
	return _skill_defs.get(skill_id) as SkillDef


func _get_profession_progress(profession_id: StringName) -> Variant:
	if _unit_progress == null:
		return null
	return _unit_progress.get_profession_progress(profession_id)


func _get_unit_base_attributes() -> UnitBaseAttributes:
	if _unit_progress == null:
		return null
	return _unit_progress.unit_base_attributes


func _get_reputation_state() -> Variant:
	if _unit_progress == null:
		return null
	return _unit_progress.reputation_state


func _can_satisfy_required_skill_ids_for_unlock(
	profession_id: StringName,
	required_skill_ids: Array[StringName]
) -> bool:
	if required_skill_ids.is_empty():
		return true

	for required_skill_id in required_skill_ids:
		if not _is_skill_eligible_for_unlock(required_skill_id, profession_id):
			return false

	return true


func _can_satisfy_tag_rules_for_unlock(
	profession_id: StringName,
	tag_rules: Array[TagRequirement]
) -> bool:
	return _can_satisfy_tag_rules_with_skill_ids(
		_get_unlock_candidate_skill_ids(profession_id),
		profession_id,
		tag_rules,
		true
	)


func _can_satisfy_tag_rules_with_skill_ids(
	candidate_skill_ids: Array[StringName],
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool,
	preview_assigned_skill_ids: Array[StringName] = []
) -> bool:
	if tag_rules.is_empty():
		return true

	for tag_rule in tag_rules:
		if tag_rule == null or tag_rule.tag == &"":
			continue

		var matched_count: int = 0
		for skill_id in candidate_skill_ids:
			if _matches_tag_requirement(skill_id, profession_id, tag_rule, allow_unassigned, preview_assigned_skill_ids):
				matched_count += 1

		if matched_count < tag_rule.count:
			return false

	return true


func _get_unlock_candidate_skill_ids(profession_id: StringName) -> Array[StringName]:
	return _get_all_learned_skill_ids()


func _get_rank_up_candidate_skill_ids(
	profession_id: StringName,
	preview_assigned_skill_ids: Array[StringName] = []
) -> Array[StringName]:
	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress == null:
		return []
	var candidate_skill_ids: Array[StringName] = profession_progress.core_skill_ids.duplicate()
	for skill_id in preview_assigned_skill_ids:
		if skill_id == &"" or candidate_skill_ids.has(skill_id):
			continue
		candidate_skill_ids.append(skill_id)
	return candidate_skill_ids


func _get_rank_up_preview_assigned_core_skill_ids(profession_id: StringName) -> Array[StringName]:
	var preview_skill_ids: Array[StringName] = []
	var trigger_skill_id := _get_ready_active_level_trigger_skill_id()
	if trigger_skill_id == &"":
		return preview_skill_ids
	if not _is_skill_eligible_for_profession(trigger_skill_id, profession_id, true):
		return preview_skill_ids
	var skill_progress: Variant = _unit_progress.get_skill_progress(trigger_skill_id)
	if skill_progress == null:
		return preview_skill_ids
	if skill_progress.assigned_profession_id != &"":
		return preview_skill_ids
	preview_skill_ids.append(trigger_skill_id)
	return preview_skill_ids


func _is_skill_eligible_for_unlock(skill_id: StringName, profession_id: StringName) -> bool:
	return _is_skill_eligible_for_profession(skill_id, profession_id, true)


func _is_skill_eligible_for_profession(
	skill_id: StringName,
	profession_id: StringName,
	allow_unassigned: bool
) -> bool:
	if _unit_progress == null:
		return false

	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_progress == null:
		return false
	if not skill_progress.is_learned:
		return false
	if not skill_progress.is_core:
		return false

	var skill_def: SkillDef = _get_skill_def(skill_id)
	if skill_def == null:
		return false
	if not SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(skill_def, skill_progress, _unit_progress):
		return false

	if skill_progress.assigned_profession_id == &"":
		return allow_unassigned

	return skill_progress.assigned_profession_id == profession_id


func _matches_any_tag_rule(
	skill_id: StringName,
	profession_id: StringName,
	tag_rules: Array[TagRequirement],
	allow_unassigned: bool
) -> bool:
	for tag_rule in tag_rules:
		if _matches_tag_requirement(skill_id, profession_id, tag_rule, allow_unassigned):
			return true
	return false


func _matches_tag_requirement(
	skill_id: StringName,
	profession_id: StringName,
	tag_rule: TagRequirement,
	allow_unassigned: bool,
	preview_assigned_skill_ids: Array[StringName] = []
) -> bool:
	if tag_rule == null or tag_rule.tag == &"":
		return false
	if _unit_progress == null:
		return false

	var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
	if skill_progress == null or not skill_progress.is_learned:
		return false

	var skill_def: SkillDef = _get_skill_def(skill_id)
	if skill_def == null:
		return false
	if not skill_def.tags.has(tag_rule.tag):
		return false
	if not _matches_skill_state(skill_progress, skill_def, tag_rule):
		return false
	if not _matches_origin_filter(skill_progress, tag_rule):
		return false
	return _matches_assignment(skill_progress, profession_id, allow_unassigned, preview_assigned_skill_ids)


func _matches_skill_state(skill_progress: Variant, skill_def: SkillDef, tag_rule: TagRequirement) -> bool:
	match tag_rule.get_normalized_skill_state():
		TagRequirement.SKILL_STATE_LEARNED:
			return skill_progress.is_learned
		TagRequirement.SKILL_STATE_CORE:
			return skill_progress.is_core
		TagRequirement.SKILL_STATE_CORE_MAX:
			return skill_progress.is_core and SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.is_at_effective_max_level(
				skill_def,
				skill_progress,
				_unit_progress
			)
		_:
			return false


func _matches_origin_filter(skill_progress: Variant, tag_rule: TagRequirement) -> bool:
	match tag_rule.get_normalized_origin_filter():
		TagRequirement.ORIGIN_FILTER_ANY:
			return true
		TagRequirement.ORIGIN_FILTER_UNMERGED_ONLY:
			return skill_progress.merged_from_skill_ids.is_empty()
		TagRequirement.ORIGIN_FILTER_MERGED_ONLY:
			return not skill_progress.merged_from_skill_ids.is_empty()
		_:
			return false


func _matches_assignment(
	skill_progress: Variant,
	profession_id: StringName,
	allow_unassigned: bool,
	preview_assigned_skill_ids: Array[StringName] = []
) -> bool:
	if profession_id != &"" and skill_progress.assigned_profession_id == profession_id:
		return true
	if skill_progress.assigned_profession_id != &"":
		return false
	return allow_unassigned or preview_assigned_skill_ids.has(skill_progress.skill_id)


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


func _get_all_learned_skill_ids() -> Array[StringName]:
	var learned_skill_ids: Array[StringName] = []
	if _unit_progress == null:
		return learned_skill_ids

	for skill_key in ProgressionDataUtils.sorted_string_keys(_unit_progress.skills):
		var skill_id: StringName = StringName(skill_key)
		var skill_progress: Variant = _unit_progress.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		learned_skill_ids.append(skill_id)

	return learned_skill_ids


func _resolve_gate_check_mode(gate: ProfessionRankGate) -> StringName:
	if gate.check_mode != &"":
		return gate.check_mode

	var source_profession_def: ProfessionDef = _get_profession_def(gate.profession_id)
	if source_profession_def == null:
		return &"historical"
	if source_profession_def.dependency_visibility_mode == &"ignore_when_hidden":
		return &"active_only"
	return &"historical"


func _are_active_conditions_satisfied(profession_def: ProfessionDef) -> bool:
	if profession_def.active_conditions.is_empty():
		return true

	var unit_base_attributes: UnitBaseAttributes = _get_unit_base_attributes()
	var reputation_state: Variant = _get_reputation_state()

	for active_condition in profession_def.active_conditions:
		if active_condition == null:
			continue

		match active_condition.condition_type:
			&"attribute_range":
				if unit_base_attributes == null:
					return false
				var value: int = int(unit_base_attributes.get_attribute_value(active_condition.attribute_id))
				if not active_condition.matches_value(value):
					return false
			&"reputation_range":
				if reputation_state == null:
					return false
				var reputation_value: int = int(reputation_state.get_reputation_value(active_condition.state_id))
				if not active_condition.matches_value(reputation_value):
					return false
			_:
				push_warning("Unsupported profession active condition type: %s" % active_condition.condition_type)
				return false

	return true
