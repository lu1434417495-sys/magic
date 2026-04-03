class_name PlayerProgress
extends RefCounted

const PlayerReputationState = preload("res://scripts/player/progression/player_reputation_state.gd")

var character_level := 0
var base_attributes: PlayerBaseAttributes = PlayerBaseAttributes.new()
var reputation_state: PlayerReputationState = PlayerReputationState.new()
var skills: Dictionary = {}
var professions: Dictionary = {}
var active_core_skill_ids: Array[StringName] = []
var pending_profession_choices: Array[PendingProfessionChoice] = []
var blocked_relearn_skill_ids: Array[StringName] = []
var merged_skill_source_map: Dictionary = {}
var version := 1


func set_skill_progress(skill_progress: PlayerSkillProgress) -> void:
	if skill_progress == null:
		return

	skills[skill_progress.skill_id] = skill_progress
	if not skill_progress.merged_from_skill_ids.is_empty():
		remember_merge_sources(skill_progress.skill_id, skill_progress.merged_from_skill_ids)
	sync_active_core_skill_ids()


func get_skill_progress(skill_id: StringName) -> PlayerSkillProgress:
	return skills.get(skill_id) as PlayerSkillProgress


func remove_skill_progress(skill_id: StringName) -> void:
	skills.erase(skill_id)
	sync_active_core_skill_ids()


func set_profession_progress(profession_progress: PlayerProfessionProgress) -> void:
	if profession_progress == null:
		return
	professions[profession_progress.profession_id] = profession_progress


func get_profession_progress(profession_id: StringName) -> PlayerProfessionProgress:
	return professions.get(profession_id) as PlayerProfessionProgress


func sync_active_core_skill_ids() -> void:
	var next_core_skill_ids: Array[StringName] = []
	for key in ProgressionDataUtils.sorted_string_keys(skills):
		var skill_id := StringName(key)
		var skill_progress := get_skill_progress(skill_id)
		if skill_progress == null:
			continue
		if skill_progress.is_learned and skill_progress.is_core:
			next_core_skill_ids.append(skill_id)
	active_core_skill_ids = next_core_skill_ids


func is_skill_relearn_blocked(skill_id: StringName) -> bool:
	return blocked_relearn_skill_ids.has(skill_id)


func block_skill_relearn(skill_id: StringName) -> void:
	if blocked_relearn_skill_ids.has(skill_id):
		return
	blocked_relearn_skill_ids.append(skill_id)


func remember_merge_sources(skill_id: StringName, source_skill_ids: Array[StringName]) -> void:
	var deduped_sources: Array[StringName] = []
	var seen_sources: Dictionary = {}
	for source_skill_id in source_skill_ids:
		if source_skill_id == skill_id:
			continue
		if seen_sources.has(source_skill_id):
			continue
		seen_sources[source_skill_id] = true
		deduped_sources.append(source_skill_id)

	merged_skill_source_map[skill_id] = deduped_sources.duplicate()
	var skill_progress := get_skill_progress(skill_id)
	if skill_progress != null:
		skill_progress.merged_from_skill_ids = deduped_sources.duplicate()


func get_merged_source_skill_ids(skill_id: StringName) -> Array[StringName]:
	if merged_skill_source_map.has(skill_id):
		return ProgressionDataUtils.to_string_name_array(merged_skill_source_map.get(skill_id, []))

	var skill_progress := get_skill_progress(skill_id)
	if skill_progress != null and not skill_progress.merged_from_skill_ids.is_empty():
		return skill_progress.merged_from_skill_ids.duplicate()

	return []


func get_merged_source_skill_ids_recursive(skill_id: StringName) -> Array[StringName]:
	var ordered_results: Array[StringName] = []
	var visited: Dictionary = {}
	for source_skill_id in get_merged_source_skill_ids(skill_id):
		_append_recursive_merge_source(source_skill_id, ordered_results, visited)
	return ordered_results


func _append_recursive_merge_source(
	source_skill_id: StringName,
	ordered_results: Array[StringName],
	visited: Dictionary
) -> void:
	if visited.has(source_skill_id):
		return

	for nested_source_id in get_merged_source_skill_ids(source_skill_id):
		_append_recursive_merge_source(nested_source_id, ordered_results, visited)

	if visited.has(source_skill_id):
		return

	visited[source_skill_id] = true
	ordered_results.append(source_skill_id)


func to_dict() -> Dictionary:
	sync_active_core_skill_ids()

	var skills_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(skills):
		var skill_id := StringName(key)
		var skill_progress := get_skill_progress(skill_id)
		if skill_progress != null:
			skills_data[key] = skill_progress.to_dict()

	var professions_data: Dictionary = {}
	for key in ProgressionDataUtils.sorted_string_keys(professions):
		var profession_id := StringName(key)
		var profession_progress := get_profession_progress(profession_id)
		if profession_progress != null:
			professions_data[key] = profession_progress.to_dict()

	var pending_choices_data: Array[Dictionary] = []
	for pending_choice in pending_profession_choices:
		if pending_choice != null:
			pending_choices_data.append(pending_choice.to_dict())

	return {
		"version": version,
		"character_level": character_level,
		"base_attributes": base_attributes.to_dict() if base_attributes != null else {},
		"reputation_state": reputation_state.to_dict() if reputation_state != null else {},
		"skills": skills_data,
		"professions": professions_data,
		"active_core_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(active_core_skill_ids),
		"pending_profession_choices": pending_choices_data,
		"blocked_relearn_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(blocked_relearn_skill_ids),
		"merged_skill_source_map": ProgressionDataUtils.string_name_array_map_to_string_dict(merged_skill_source_map),
	}


static func from_dict(data: Dictionary) -> PlayerProgress:
	var progress := PlayerProgress.new()
	progress.version = int(data.get("version", 1))
	progress.character_level = int(data.get("character_level", 0))
	progress.base_attributes = PlayerBaseAttributes.from_dict(data.get("base_attributes", {}))
	progress.reputation_state = PlayerReputationState.from_dict(data.get("reputation_state", {}))
	progress.blocked_relearn_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("blocked_relearn_skill_ids", []))
	progress.merged_skill_source_map = ProgressionDataUtils.to_string_name_array_map(data.get("merged_skill_source_map", {}))

	var skills_data: Variant = data.get("skills", {})
	if skills_data is Dictionary:
		for key in skills_data.keys():
			var skill_progress := PlayerSkillProgress.from_dict(skills_data[key])
			if skill_progress.skill_id == &"":
				skill_progress.skill_id = ProgressionDataUtils.to_string_name(key)
			progress.skills[skill_progress.skill_id] = skill_progress
			if not skill_progress.merged_from_skill_ids.is_empty():
				progress.merged_skill_source_map[skill_progress.skill_id] = skill_progress.merged_from_skill_ids.duplicate()

	var professions_data: Variant = data.get("professions", {})
	if professions_data is Dictionary:
		for key in professions_data.keys():
			var profession_progress := PlayerProfessionProgress.from_dict(professions_data[key])
			if profession_progress.profession_id == &"":
				profession_progress.profession_id = ProgressionDataUtils.to_string_name(key)
			progress.professions[profession_progress.profession_id] = profession_progress

	var pending_choices_data: Variant = data.get("pending_profession_choices", [])
	if pending_choices_data is Array:
		for pending_choice_data in pending_choices_data:
			if pending_choice_data is Dictionary:
				progress.pending_profession_choices.append(PendingProfessionChoice.from_dict(pending_choice_data))

	progress.active_core_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("active_core_skill_ids", []))
	progress.sync_active_core_skill_ids()
	return progress
