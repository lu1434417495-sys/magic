class_name SkillMergeService
extends RefCounted

var _player_progress: PlayerProgress
var _skill_defs: Dictionary = {}
var _assignment_service: ProfessionAssignmentService


func setup(player_progress: PlayerProgress, skill_defs: Variant, assignment_service: ProfessionAssignmentService = null) -> void:
	_player_progress = player_progress
	_skill_defs = _index_skill_defs(skill_defs)
	_assignment_service = assignment_service


func merge_skills(
	source_skill_ids: Array[StringName],
	result_skill_id: StringName,
	keep_core: bool,
	target_profession_id: StringName
) -> bool:
	if _player_progress == null:
		return false
	if result_skill_id == &"":
		return false
	if _player_progress.is_skill_relearn_blocked(result_skill_id):
		return false

	var normalized_source_skill_ids := _normalize_source_skill_ids(source_skill_ids, result_skill_id)
	if normalized_source_skill_ids.is_empty():
		return false
	if not _all_source_skills_exist(normalized_source_skill_ids):
		return false

	var resolved_target_profession_id := target_profession_id
	if keep_core and resolved_target_profession_id == &"":
		resolved_target_profession_id = _infer_target_profession_id_from_sources(normalized_source_skill_ids)
	if keep_core and resolved_target_profession_id == &"":
		return false
	if keep_core and _get_profession_progress(resolved_target_profession_id) == null:
		return false

	var result_progress := _get_or_create_result_skill_progress(result_skill_id, normalized_source_skill_ids)
	if result_progress == null:
		return false

	detach_merged_source_skills(normalized_source_skill_ids)

	result_progress.is_learned = true
	result_progress.is_core = keep_core
	result_progress.merged_from_skill_ids = normalized_source_skill_ids.duplicate()
	if keep_core:
		result_progress.assigned_profession_id = resolved_target_profession_id
	else:
		result_progress.clear_profession_assignment()

	_player_progress.remember_merge_sources(result_skill_id, normalized_source_skill_ids)
	_player_progress.set_skill_progress(result_progress)
	return attach_merged_result_skill(result_skill_id, keep_core, resolved_target_profession_id)


func detach_merged_source_skills(source_skill_ids: Array[StringName]) -> void:
	if _player_progress == null:
		return

	var normalized_source_skill_ids := _normalize_source_skill_ids(source_skill_ids)
	for source_skill_id in normalized_source_skill_ids:
		var source_skill_progress := _player_progress.get_skill_progress(source_skill_id)
		if source_skill_progress == null:
			continue

		if not source_skill_progress.merged_from_skill_ids.is_empty():
			_player_progress.remember_merge_sources(source_skill_id, source_skill_progress.merged_from_skill_ids)

		if source_skill_progress.assigned_profession_id != &"":
			_remove_source_skill_from_profession(source_skill_id, source_skill_progress.assigned_profession_id)
		else:
			_remove_source_skill_from_all_professions(source_skill_id)

		source_skill_progress.clear_profession_assignment()
		_player_progress.block_skill_relearn(source_skill_id)
		_player_progress.remove_skill_progress(source_skill_id)

	_player_progress.sync_active_core_skill_ids()


func attach_merged_result_skill(result_skill_id: StringName, keep_core: bool, target_profession_id: StringName) -> bool:
	if _player_progress == null:
		return false

	var result_skill_progress := _player_progress.get_skill_progress(result_skill_id)
	if result_skill_progress == null:
		result_skill_progress = PlayerSkillProgress.new()
		result_skill_progress.skill_id = result_skill_id
		result_skill_progress.is_learned = true
		_player_progress.set_skill_progress(result_skill_progress)

	if not keep_core:
		_remove_source_skill_from_all_professions(result_skill_id)
		result_skill_progress.is_core = false
		result_skill_progress.clear_profession_assignment()
		_player_progress.set_skill_progress(result_skill_progress)
		_player_progress.sync_active_core_skill_ids()
		return true

	if target_profession_id == &"":
		return false

	var profession_progress := _get_profession_progress(target_profession_id)
	if profession_progress == null:
		return false

	_remove_source_skill_from_all_professions(result_skill_id, target_profession_id)
	result_skill_progress.is_learned = true
	result_skill_progress.is_core = true
	result_skill_progress.assigned_profession_id = target_profession_id
	profession_progress.add_core_skill(result_skill_id)
	_player_progress.set_skill_progress(result_skill_progress)
	_player_progress.sync_active_core_skill_ids()
	return true


func get_merged_source_skill_ids(skill_id: StringName) -> Array[StringName]:
	if _player_progress == null:
		return []
	return _player_progress.get_merged_source_skill_ids(skill_id)


func get_merged_source_skill_ids_recursive(skill_id: StringName) -> Array[StringName]:
	if _player_progress == null:
		return []
	return _player_progress.get_merged_source_skill_ids_recursive(skill_id)


func _index_skill_defs(skill_defs: Variant) -> Dictionary:
	var indexed_defs: Dictionary = {}

	if skill_defs is Dictionary:
		for key in skill_defs.keys():
			var skill_def = skill_defs[key]
			if skill_def is SkillDef:
				var indexed_id: StringName = skill_def.skill_id if skill_def.skill_id != &"" else ProgressionDataUtils.to_string_name(key)
				indexed_defs[indexed_id] = skill_def
	elif skill_defs is Array:
		for skill_def in skill_defs:
			if skill_def is SkillDef and skill_def.skill_id != &"":
				indexed_defs[skill_def.skill_id] = skill_def

	return indexed_defs


func _normalize_source_skill_ids(
	source_skill_ids: Array[StringName],
	excluded_skill_id: StringName = &""
) -> Array[StringName]:
	var normalized_source_skill_ids: Array[StringName] = []
	var seen_skill_ids: Dictionary = {}

	for source_skill_id in source_skill_ids:
		if source_skill_id == &"":
			continue
		if excluded_skill_id != &"" and source_skill_id == excluded_skill_id:
			continue
		if seen_skill_ids.has(source_skill_id):
			continue

		seen_skill_ids[source_skill_id] = true
		normalized_source_skill_ids.append(source_skill_id)

	return normalized_source_skill_ids


func _all_source_skills_exist(source_skill_ids: Array[StringName]) -> bool:
	for source_skill_id in source_skill_ids:
		if _player_progress.get_skill_progress(source_skill_id) == null:
			return false
	return true


func _infer_target_profession_id_from_sources(source_skill_ids: Array[StringName]) -> StringName:
	var inferred_profession_id: StringName = &""
	for source_skill_id in source_skill_ids:
		var source_skill_progress := _player_progress.get_skill_progress(source_skill_id)
		if source_skill_progress == null:
			continue
		if not source_skill_progress.is_core:
			continue
		if source_skill_progress.assigned_profession_id == &"":
			continue

		if inferred_profession_id == &"":
			inferred_profession_id = source_skill_progress.assigned_profession_id
			continue

		if inferred_profession_id != source_skill_progress.assigned_profession_id:
			return &""

	return inferred_profession_id


func _get_or_create_result_skill_progress(
	result_skill_id: StringName,
	source_skill_ids: Array[StringName]
) -> PlayerSkillProgress:
	var existing_result_progress := _player_progress.get_skill_progress(result_skill_id)
	if existing_result_progress != null:
		return existing_result_progress

	var result_progress := PlayerSkillProgress.new()
	result_progress.skill_id = result_skill_id
	result_progress.is_learned = true

	var source_max_level := 0
	var source_total_mastery := 0
	var source_training_mastery := 0
	var source_battle_mastery := 0
	var source_current_mastery := 0
	var granted_by_profession: StringName = &""
	var granted_profession_conflicted := false

	for source_skill_id in source_skill_ids:
		var source_skill_progress := _player_progress.get_skill_progress(source_skill_id)
		if source_skill_progress == null:
			continue

		source_max_level = max(source_max_level, source_skill_progress.skill_level)
		source_total_mastery += source_skill_progress.total_mastery_earned
		source_training_mastery += source_skill_progress.mastery_from_training
		source_battle_mastery += source_skill_progress.mastery_from_battle
		source_current_mastery = max(source_current_mastery, source_skill_progress.current_mastery)

		if source_skill_progress.profession_granted_by == &"":
			continue
		if granted_by_profession == &"":
			granted_by_profession = source_skill_progress.profession_granted_by
			continue
		if granted_by_profession != source_skill_progress.profession_granted_by:
			granted_profession_conflicted = true

	var result_skill_def := _skill_defs.get(result_skill_id) as SkillDef
	if result_skill_def != null:
		source_max_level = min(source_max_level, result_skill_def.max_level)

	result_progress.skill_level = source_max_level
	result_progress.current_mastery = source_current_mastery
	result_progress.total_mastery_earned = source_total_mastery
	result_progress.mastery_from_training = source_training_mastery
	result_progress.mastery_from_battle = source_battle_mastery
	if not granted_profession_conflicted:
		result_progress.profession_granted_by = granted_by_profession

	return result_progress


func _remove_source_skill_from_profession(skill_id: StringName, profession_id: StringName) -> void:
	if _assignment_service != null:
		_assignment_service.remove_core_skill_from_profession(skill_id, profession_id)
		return

	var profession_progress := _get_profession_progress(profession_id)
	if profession_progress == null:
		return
	profession_progress.remove_core_skill(skill_id)


func _remove_source_skill_from_all_professions(skill_id: StringName, except_profession_id: StringName = &"") -> void:
	if _player_progress == null:
		return

	for profession_key in _player_progress.professions.keys():
		var profession_id := ProgressionDataUtils.to_string_name(profession_key)
		if except_profession_id != &"" and profession_id == except_profession_id:
			continue

		var profession_progress := _get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		profession_progress.remove_core_skill(skill_id)


func _get_profession_progress(profession_id: StringName) -> PlayerProfessionProgress:
	if _player_progress == null:
		return null
	return _player_progress.get_profession_progress(profession_id)
