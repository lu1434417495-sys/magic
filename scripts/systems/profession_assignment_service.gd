## 文件说明：该脚本属于职业分配服务相关的服务脚本，集中维护单位进度、技能定义集合、职业定义集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name ProfessionAssignmentService
extends RefCounted

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


func assign_core_skill_to_profession(skill_id: StringName, profession_id: StringName) -> bool:
	var skill_progress: Variant = _get_skill_progress(skill_id)
	var profession_progress: Variant = _get_profession_progress(profession_id)
	var skill_def: SkillDef = _get_skill_def(skill_id)
	if skill_progress == null or profession_progress == null or skill_def == null:
		return false
	if not skill_progress.is_learned:
		return false
	if not skill_progress.is_core:
		return false
	if not skill_progress.is_max_level(skill_def.max_level):
		return false
	if skill_progress.assigned_profession_id != &"" and skill_progress.assigned_profession_id != profession_id:
		return false

	_remove_skill_from_all_professions(skill_id, profession_id)
	skill_progress.assigned_profession_id = profession_id
	profession_progress.add_core_skill(skill_id)
	_unit_progress.sync_active_core_skill_ids()
	return true


func remove_core_skill_from_profession(skill_id: StringName, profession_id: StringName) -> bool:
	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress == null:
		return false

	var had_skill: bool = profession_progress.core_skill_ids.has(skill_id)
	if not had_skill:
		return false

	profession_progress.remove_core_skill(skill_id)
	var skill_progress: Variant = _get_skill_progress(skill_id)
	if skill_progress != null and skill_progress.assigned_profession_id == profession_id:
		skill_progress.clear_profession_assignment()

	_unit_progress.sync_active_core_skill_ids()
	return true


func can_promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool:
	if _unit_progress == null:
		return false

	var profession_progress: Variant = _get_profession_progress(profession_id)
	var profession_def: ProfessionDef = _get_profession_def(profession_id)
	var skill_progress: Variant = _get_skill_progress(skill_id)
	var skill_def: SkillDef = _get_skill_def(skill_id)
	if profession_progress == null or profession_def == null or skill_progress == null or skill_def == null:
		return false

	var current_character_level: int = _get_effective_character_level()
	_unit_progress.sync_active_core_skill_ids()
	if _unit_progress.active_core_skill_ids.size() >= current_character_level:
		return false
	if profession_progress.core_skill_ids.size() >= profession_progress.rank:
		return false
	if profession_progress.rank <= 0:
		return false
	if not skill_progress.is_learned:
		return false
	if skill_progress.is_core:
		return false
	if skill_progress.assigned_profession_id != &"":
		return false
	if not skill_progress.is_max_level(skill_def.max_level):
		return false

	var accepted_tags: Array[StringName] = _get_profession_accepted_tags(profession_def)
	if accepted_tags.is_empty():
		return false

	for tag in skill_def.tags:
		if accepted_tags.has(tag):
			return true

	return false


func promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool:
	if not can_promote_non_core_to_core(skill_id, profession_id):
		return false

	var skill_progress: Variant = _get_skill_progress(skill_id)
	if skill_progress == null:
		return false

	skill_progress.is_core = true
	skill_progress.assigned_profession_id = profession_id

	var profession_progress: Variant = _get_profession_progress(profession_id)
	profession_progress.add_core_skill(skill_id)
	_unit_progress.sync_active_core_skill_ids()
	return true


func get_profession_core_skills(profession_id: StringName) -> Array[StringName]:
	var profession_progress: Variant = _get_profession_progress(profession_id)
	if profession_progress == null:
		return []
	return profession_progress.core_skill_ids.duplicate()


func get_skill_assigned_profession(skill_id: StringName) -> StringName:
	var skill_progress: Variant = _get_skill_progress(skill_id)
	if skill_progress == null:
		return &""
	return skill_progress.assigned_profession_id


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


func _get_skill_progress(skill_id: StringName) -> Variant:
	if _unit_progress == null:
		return null
	return _unit_progress.get_skill_progress(skill_id)


func _get_profession_progress(profession_id: StringName) -> Variant:
	if _unit_progress == null:
		return null
	return _unit_progress.get_profession_progress(profession_id)


func _get_skill_def(skill_id: StringName) -> SkillDef:
	return _skill_defs.get(skill_id) as SkillDef


func _get_profession_def(profession_id: StringName) -> ProfessionDef:
	return _profession_defs.get(profession_id) as ProfessionDef


func _remove_skill_from_all_professions(skill_id: StringName, except_profession_id: StringName = &"") -> void:
	if _unit_progress == null:
		return

	for profession_key in _unit_progress.professions.keys():
		var profession_id: StringName = ProgressionDataUtils.to_string_name(profession_key)
		if except_profession_id != &"" and profession_id == except_profession_id:
			continue

		var profession_progress: Variant = _get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		profession_progress.remove_core_skill(skill_id)


func _get_profession_accepted_tags(profession_def: ProfessionDef) -> Array[StringName]:
	var accepted_tags: Array[StringName] = []
	var seen_tags: Dictionary = {}

	if profession_def == null:
		return accepted_tags

	if profession_def.unlock_requirement != null:
		for tag_rule in profession_def.unlock_requirement.required_tag_rules:
			if tag_rule == null or tag_rule.tag == &"" or seen_tags.has(tag_rule.tag):
				continue
			seen_tags[tag_rule.tag] = true
			accepted_tags.append(tag_rule.tag)

	for rank_requirement in profession_def.rank_requirements:
		if rank_requirement == null:
			continue
		for tag_rule in rank_requirement.required_tag_rules:
			if tag_rule == null or tag_rule.tag == &"" or seen_tags.has(tag_rule.tag):
				continue
			seen_tags[tag_rule.tag] = true
			accepted_tags.append(tag_rule.tag)

	return accepted_tags


func _get_effective_character_level() -> int:
	if _unit_progress == null:
		return 0

	var rank_total: int = 0
	for profession_progress in _unit_progress.professions.values():
		if profession_progress == null:
			continue
		rank_total += int(profession_progress.rank)

	return rank_total
