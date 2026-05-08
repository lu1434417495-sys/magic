## 文件说明：该脚本属于职业内容注册表相关的注册表脚本，集中维护职业定义集合、扫描目录和校验错误列表等顶层字段。
## 审查重点：重点核对职业主键、技能/职业引用、阶位条件以及资源扫描失败提示是否保持稳定。
## 备注：当前注册表只负责 profession resource 的扫描、校验和索引，不承担职业规则执行逻辑。

class_name ProfessionContentRegistry
extends RefCounted

const PROFESSION_CONFIG_DIRECTORY := "res://data/configs/professions"
const PROFESSION_DEF_SCRIPT = preload("res://scripts/player/progression/profession_def.gd")

const VALID_BAB_PROGRESSIONS := {
	&"full": true,
	&"three_quarter": true,
	&"half": true,
}

const VALID_REACTIVATION_MODES := {
	&"auto": true,
	&"manual": true,
}

const VALID_DEPENDENCY_VISIBILITY_MODES := {
	&"count_when_hidden": true,
	&"ignore_when_hidden": true,
}

const VALID_GATE_CHECK_MODES := {
	&"historical": true,
	&"active_only": true,
}

## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []
## 字段说明：缓存技能定义集合，供职业技能引用校验使用。
var _skill_defs: Dictionary = {}


func _init(skill_defs: Dictionary = {}) -> void:
	setup(skill_defs)


func setup(skill_defs: Dictionary = {}) -> void:
	_skill_defs = skill_defs if skill_defs != null else {}
	rebuild()


func rebuild() -> void:
	_profession_defs.clear()
	_validation_errors.clear()
	_scan_directory(PROFESSION_CONFIG_DIRECTORY)
	_validation_errors.append_array(_collect_validation_errors())


func get_profession_defs() -> Dictionary:
	return _profession_defs


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("ProfessionContentRegistry could not find %s." % directory_path)
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("ProfessionContentRegistry could not open %s." % directory_path)
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue

		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_scan_directory(entry_path)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_profession_resource(entry_path)
	directory.list_dir_end()


func _register_profession_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load profession config %s." % resource_path)
		return
	if resource.get_script() != PROFESSION_DEF_SCRIPT:
		_validation_errors.append("Profession config %s is not a ProfessionDef." % resource_path)
		return

	var profession_def = resource as ProfessionDef
	if profession_def == null:
		_validation_errors.append("Profession config %s failed to cast to ProfessionDef." % resource_path)
		return
	if profession_def.profession_id == &"":
		_validation_errors.append("Profession config %s is missing profession_id." % resource_path)
		return
	if _profession_defs.has(profession_def.profession_id):
		_validation_errors.append("Duplicate profession_id registered: %s" % String(profession_def.profession_id))
		return

	_profession_defs[profession_def.profession_id] = profession_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []

	for profession_key in ProgressionDataUtils.sorted_string_keys(_profession_defs):
		var profession_id := StringName(profession_key)
		var profession_def := _profession_defs.get(profession_id) as ProfessionDef
		if profession_def == null:
			continue
		_append_profession_validation_errors(errors, profession_id, profession_def)

	return errors


func _append_profession_validation_errors(
	errors: Array[String],
	profession_id: StringName,
	profession_def: ProfessionDef
) -> void:
	if profession_def.max_rank <= 0:
		errors.append("Profession %s must have max_rank >= 1." % String(profession_id))

	if profession_def.hit_die_sides <= 0:
		errors.append("Profession %s must have hit_die_sides >= 1." % String(profession_id))

	if not VALID_BAB_PROGRESSIONS.has(profession_def.bab_progression):
		errors.append(
			"Profession %s uses unsupported bab_progression %s." % [
				String(profession_id),
				String(profession_def.bab_progression),
			]
		)

	if profession_def.requires_knowledge_unlock() and profession_def.unlock_knowledge_id == &"":
		errors.append("Profession %s is missing unlock_knowledge_id." % String(profession_id))

	if not VALID_REACTIVATION_MODES.has(profession_def.reactivation_mode):
		errors.append(
			"Profession %s uses unsupported reactivation_mode %s." % [
				String(profession_id),
				String(profession_def.reactivation_mode),
			]
		)

	if not VALID_DEPENDENCY_VISIBILITY_MODES.has(profession_def.dependency_visibility_mode):
		errors.append(
			"Profession %s uses unsupported dependency_visibility_mode %s." % [
				String(profession_id),
				String(profession_def.dependency_visibility_mode),
			]
		)

	_append_unlock_requirement_errors(errors, profession_id, profession_def.unlock_requirement)
	_append_granted_skill_errors(errors, profession_id, profession_def)
	_append_active_condition_errors(errors, profession_id, profession_def.active_conditions)
	_append_rank_requirement_errors(errors, profession_id, profession_def)


func _append_unlock_requirement_errors(
	errors: Array[String],
	profession_id: StringName,
	unlock_requirement: ProfessionPromotionRequirement
) -> void:
	if unlock_requirement == null:
		return

	for required_skill_id in unlock_requirement.required_skill_ids:
		if required_skill_id == &"":
			errors.append("Profession %s has an empty required_skill_id in unlock." % String(profession_id))
			continue
		if not _skill_defs.is_empty() and not _skill_defs.has(required_skill_id):
			errors.append(
				"Profession %s references missing skill %s in unlock.required_skill_ids." % [
					String(profession_id),
					String(required_skill_id),
				]
			)

	_append_profession_gate_errors(
		errors,
		profession_id,
		unlock_requirement.required_profession_ranks,
		"unlock.required_profession_ranks"
	)

	for attribute_rule in unlock_requirement.required_attribute_rules:
		if attribute_rule == null:
			continue
		if attribute_rule.attribute_id == &"":
			errors.append("Profession %s has an empty attribute_id in unlock.required_attribute_rules." % String(profession_id))

	for reputation_rule in unlock_requirement.required_reputation_rules:
		if reputation_rule == null:
			continue
		if reputation_rule.state_id == &"":
			errors.append("Profession %s has an empty state_id in unlock.required_reputation_rules." % String(profession_id))

	for tag_rule in unlock_requirement.required_tag_rules:
		_append_tag_rule_errors(errors, profession_id, tag_rule, "unlock.required_tag_rules")


func _append_rank_requirement_errors(
	errors: Array[String],
	profession_id: StringName,
	profession_def: ProfessionDef
) -> void:
	var seen_target_ranks: Dictionary = {}
	for rank_requirement in profession_def.rank_requirements:
		if rank_requirement == null:
			continue

		if rank_requirement.target_rank < 2 or rank_requirement.target_rank > profession_def.max_rank:
			errors.append(
				"Profession %s declares invalid target_rank %d." % [
					String(profession_id),
					rank_requirement.target_rank,
				]
			)
		elif seen_target_ranks.has(rank_requirement.target_rank):
			errors.append(
				"Profession %s declares duplicate rank requirement for rank %d." % [
					String(profession_id),
					rank_requirement.target_rank,
				]
			)
		else:
			seen_target_ranks[rank_requirement.target_rank] = true

		for tag_rule in rank_requirement.required_tag_rules:
			_append_tag_rule_errors(
				errors,
				profession_id,
				tag_rule,
				"rank_%d.required_tag_rules" % rank_requirement.target_rank
			)
		_append_profession_gate_errors(
			errors,
			profession_id,
			rank_requirement.required_profession_ranks,
			"rank_%d.required_profession_ranks" % rank_requirement.target_rank
		)

	var expected_rank := 2
	while expected_rank <= profession_def.max_rank:
		if not seen_target_ranks.has(expected_rank):
			errors.append(
				"Profession %s is missing a rank requirement for rank %d." % [
					String(profession_id),
					expected_rank,
				]
			)
		expected_rank += 1


func _append_granted_skill_errors(
	errors: Array[String],
	profession_id: StringName,
	profession_def: ProfessionDef
) -> void:
	for granted_skill in profession_def.granted_skills:
		if granted_skill == null:
			continue
		if granted_skill.skill_id == &"":
			errors.append("Profession %s has a granted skill without skill_id." % String(profession_id))
			continue
		if not _skill_defs.is_empty() and not _skill_defs.has(granted_skill.skill_id):
			errors.append(
				"Profession %s grants missing skill %s." % [
					String(profession_id),
					String(granted_skill.skill_id),
				]
			)
		elif not _skill_defs.is_empty():
			var skill_def := _skill_defs.get(granted_skill.skill_id) as SkillDef
			if skill_def != null and skill_def.learn_source != &"profession":
				errors.append(
					"Profession %s granted skill %s learn_source must be profession, got %s." % [
						String(profession_id),
						String(granted_skill.skill_id),
						String(skill_def.learn_source),
					]
				)
		if granted_skill.unlock_rank <= 0 or granted_skill.unlock_rank > profession_def.max_rank:
			errors.append(
				"Profession %s grants skill %s at invalid unlock_rank %d." % [
					String(profession_id),
					String(granted_skill.skill_id),
					granted_skill.unlock_rank,
				]
			)


func _append_active_condition_errors(
	errors: Array[String],
	profession_id: StringName,
	active_conditions: Array[ProfessionActiveCondition]
) -> void:
	for active_condition in active_conditions:
		if active_condition == null:
			continue

		match active_condition.condition_type:
			&"attribute_range":
				if active_condition.attribute_id == &"":
					errors.append("Profession %s has an attribute_range active condition without attribute_id." % String(profession_id))
			&"reputation_range":
				if active_condition.state_id == &"":
					errors.append("Profession %s has a reputation_range active condition without state_id." % String(profession_id))
			_:
				errors.append(
					"Profession %s uses unsupported active condition type %s." % [
						String(profession_id),
						String(active_condition.condition_type),
					]
				)


func _append_tag_rule_errors(
	errors: Array[String],
	profession_id: StringName,
	tag_rule: TagRequirement,
	context_label: String
) -> void:
	if tag_rule == null:
		return
	if tag_rule.tag == &"":
		errors.append(
			"Profession %s has an empty tag requirement in %s." % [
				String(profession_id),
				String(context_label),
			]
		)
	if tag_rule.count <= 0:
		errors.append(
			"Profession %s has a non-positive tag count in %s for tag %s." % [
				String(profession_id),
				String(context_label),
				String(tag_rule.tag),
			]
		)

	var normalized_skill_state := tag_rule.get_normalized_skill_state()
	if tag_rule.skill_state != normalized_skill_state:
		errors.append(
			"Profession %s uses unsupported skill_state %s in %s." % [
				String(profession_id),
				String(tag_rule.skill_state),
				String(context_label),
			]
		)

	var normalized_origin_filter := tag_rule.get_normalized_origin_filter()
	if tag_rule.origin_filter != normalized_origin_filter:
		errors.append(
			"Profession %s uses unsupported origin_filter %s in %s." % [
				String(profession_id),
				String(tag_rule.origin_filter),
				String(context_label),
			]
		)

	var normalized_selection_role := tag_rule.get_normalized_selection_role()
	if tag_rule.selection_role != normalized_selection_role:
		errors.append(
			"Profession %s uses unsupported selection_role %s in %s." % [
				String(profession_id),
				String(tag_rule.selection_role),
				String(context_label),
			]
		)


func _append_profession_gate_errors(
	errors: Array[String],
	profession_id: StringName,
	gates: Array[ProfessionRankGate],
	context_label: String
) -> void:
	for gate in gates:
		if gate == null:
			continue
		if gate.profession_id == &"":
			errors.append(
				"Profession %s has an empty profession gate in %s." % [
					String(profession_id),
					String(context_label),
				]
			)
			continue
		if not _profession_defs.has(gate.profession_id):
			errors.append(
				"Profession %s references missing profession %s in %s." % [
					String(profession_id),
					String(gate.profession_id),
					String(context_label),
				]
			)
		if gate.min_rank <= 0:
			errors.append(
				"Profession %s requires non-positive min_rank %d for gate %s in %s." % [
					String(profession_id),
					gate.min_rank,
					String(gate.profession_id),
					String(context_label),
				]
			)
		if gate.check_mode != &"" and not VALID_GATE_CHECK_MODES.has(gate.check_mode):
			errors.append(
				"Profession %s uses unsupported gate check_mode %s in %s." % [
					String(profession_id),
					String(gate.check_mode),
					String(context_label),
				]
			)
