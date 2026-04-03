extends Node

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_spawn_system.gd")
const PLAYER_PROGRESS_SCRIPT = preload("res://scripts/player/progression/player_progress.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const PROGRESSION_SERIALIZATION_SCRIPT = preload("res://scripts/systems/progression_serialization.gd")
const PROFESSION_ASSIGNMENT_SERVICE_SCRIPT = preload("res://scripts/systems/profession_assignment_service.gd")
const PROFESSION_RULE_SERVICE_SCRIPT = preload("res://scripts/systems/profession_rule_service.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression_service.gd")
const SKILL_MERGE_SERVICE_SCRIPT = preload("res://scripts/systems/skill_merge_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const SAVE_PATH := "user://world_map_state.dat"
const SAVE_VERSION := 1

var _generation_config_path: String = ""
var _generation_config = null
var _world_data: Dictionary = {}
var _player_coord: Vector2i = Vector2i.ZERO
var _player_faction_id: String = "player"
var _player_progress: PlayerProgress = PLAYER_PROGRESS_SCRIPT.new()
var _has_active_world := false

var _progression_content_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
var _skill_defs: Dictionary = {}
var _profession_defs: Dictionary = {}
var _profession_assignment_service = PROFESSION_ASSIGNMENT_SERVICE_SCRIPT.new()
var _profession_rule_service = PROFESSION_RULE_SERVICE_SCRIPT.new()
var _progression_service = PROGRESSION_SERVICE_SCRIPT.new()
var _skill_merge_service = SKILL_MERGE_SERVICE_SCRIPT.new()
var _attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()


func _init() -> void:
	_report_progression_content_errors()
	_refresh_progression_runtime()


func ensure_world_ready(generation_config_path: String) -> int:
	if _has_active_world and _generation_config_path == generation_config_path:
		return OK

	if _try_load_game_state(generation_config_path):
		return OK

	return start_new_game(generation_config_path)


func start_new_game(generation_config_path: String) -> int:
	_reset_runtime_state()

	if generation_config_path.is_empty():
		push_error("GameSession requires a generation config path.")
		return ERR_INVALID_PARAMETER

	var generation_config = load(generation_config_path)
	if generation_config == null:
		push_error("GameSession failed to load config from %s." % generation_config_path)
		return ERR_CANT_OPEN

	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(generation_config.world_size_in_chunks, generation_config.chunk_size)

	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var world_data: Dictionary = spawn_system.build_world(generation_config, grid_system)

	_generation_config_path = generation_config_path
	_generation_config = generation_config
	_world_data = world_data
	_player_coord = world_data.get("player_start_coord", generation_config.player_start_coord)
	_player_faction_id = "player"
	_player_progress = _create_default_player_progress()
	_refresh_progression_runtime()
	_has_active_world = true

	return save_game_state()


func has_active_world() -> bool:
	return _has_active_world


func get_generation_config():
	return _generation_config


func get_generation_config_path() -> String:
	return _generation_config_path


func get_world_data() -> Dictionary:
	return _world_data


func set_world_data(world_data: Dictionary) -> int:
	_world_data = world_data.duplicate(true)
	return save_game_state()


func get_player_coord() -> Vector2i:
	return _player_coord


func set_player_coord(coord: Vector2i) -> int:
	_player_coord = coord
	return save_game_state()


func get_player_faction_id() -> String:
	return _player_faction_id


func set_player_faction_id(faction_id: String) -> int:
	_player_faction_id = faction_id
	return save_game_state()


func get_player_progress() -> PlayerProgress:
	return _player_progress


func set_player_progress(player_progress: PlayerProgress) -> int:
	_player_progress = _normalize_player_progress(player_progress)
	_refresh_progression_runtime()
	return save_game_state()


func get_progression_content_registry():
	return _progression_content_registry


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_profession_defs() -> Dictionary:
	return _profession_defs


func get_progression_service():
	return _progression_service


func get_profession_rule_service():
	return _profession_rule_service


func get_profession_assignment_service():
	return _profession_assignment_service


func get_skill_merge_service():
	return _skill_merge_service


func get_attribute_service():
	return _attribute_service


func learn_skill(skill_id: StringName) -> bool:
	if _progression_service == null:
		return false
	var did_learn_skill := _progression_service.learn_skill(skill_id)
	if did_learn_skill:
		_persist_runtime_state_if_active()
	return did_learn_skill


func grant_skill_mastery(skill_id: StringName, amount: int, source_type: StringName) -> bool:
	if _progression_service == null:
		return false
	var did_grant_mastery := _progression_service.grant_skill_mastery(skill_id, amount, source_type)
	if did_grant_mastery:
		_persist_runtime_state_if_active()
	return did_grant_mastery


func set_skill_core(skill_id: StringName, enabled: bool) -> bool:
	if _progression_service == null:
		return false
	var did_set_core := _progression_service.set_skill_core(skill_id, enabled)
	if did_set_core:
		_persist_runtime_state_if_active()
	return did_set_core


func can_promote_profession(profession_id: StringName) -> bool:
	if _progression_service == null:
		return false
	return _progression_service.can_promote_profession(profession_id)


func promote_profession(profession_id: StringName, selection: Dictionary = {}) -> bool:
	if _progression_service == null:
		return false
	var did_promote_profession := _progression_service.promote_profession(profession_id, selection)
	if did_promote_profession:
		_persist_runtime_state_if_active()
	return did_promote_profession


func get_profession_upgrade_candidates() -> Array[PendingProfessionChoice]:
	if _progression_service == null:
		return []
	return _progression_service.get_profession_upgrade_candidates()


func assign_core_skill_to_profession(skill_id: StringName, profession_id: StringName) -> bool:
	if _profession_assignment_service == null:
		return false
	var did_assign_skill := _profession_assignment_service.assign_core_skill_to_profession(skill_id, profession_id)
	if did_assign_skill:
		_refresh_progression_runtime()
		_persist_runtime_state_if_active()
	return did_assign_skill


func remove_core_skill_from_profession(skill_id: StringName, profession_id: StringName) -> bool:
	if _profession_assignment_service == null:
		return false
	var did_remove_skill := _profession_assignment_service.remove_core_skill_from_profession(skill_id, profession_id)
	if did_remove_skill:
		_refresh_progression_runtime()
		_persist_runtime_state_if_active()
	return did_remove_skill


func can_promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool:
	if _profession_assignment_service == null:
		return false
	return _profession_assignment_service.can_promote_non_core_to_core(skill_id, profession_id)


func promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool:
	if _profession_assignment_service == null:
		return false
	var did_promote_skill := _profession_assignment_service.promote_non_core_to_core(skill_id, profession_id)
	if did_promote_skill:
		_refresh_progression_runtime()
		_persist_runtime_state_if_active()
	return did_promote_skill


func merge_skills(
	source_skill_ids: Array[StringName],
	result_skill_id: StringName,
	keep_core: bool,
	target_profession_id: StringName
) -> bool:
	if _skill_merge_service == null:
		return false
	var did_merge_skills := _skill_merge_service.merge_skills(
		source_skill_ids,
		result_skill_id,
		keep_core,
		target_profession_id
	)
	if did_merge_skills:
		_refresh_progression_runtime()
		_persist_runtime_state_if_active()
	return did_merge_skills


func get_attribute_snapshot() -> AttributeSnapshot:
	if _attribute_service == null:
		return AttributeSnapshot.new()
	return _attribute_service.get_snapshot()


func get_total_attribute_value(attribute_id: StringName) -> int:
	if _attribute_service == null:
		return 0
	return _attribute_service.get_total_value(attribute_id)


func get_base_attribute_value(attribute_id: StringName) -> int:
	if _attribute_service == null:
		return 0
	return _attribute_service.get_base_value(attribute_id)


func save_world_state() -> int:
	return save_game_state()


func save_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED

	var save_file := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if save_file == null:
		var open_error := FileAccess.get_open_error()
		push_error("Failed to open save file %s. Error: %s" % [SAVE_PATH, open_error])
		return open_error

	save_file.store_var(_build_save_payload(), false)
	save_file.close()
	return OK


func clear_persisted_world() -> int:
	return clear_persisted_game()


func clear_persisted_game() -> int:
	_reset_runtime_state()
	if FileAccess.file_exists(SAVE_PATH):
		return DirAccess.remove_absolute(ProjectSettings.globalize_path(SAVE_PATH))
	return OK


func reset_runtime_cache() -> void:
	_reset_runtime_state()


func _try_load_game_state(generation_config_path: String) -> bool:
	if generation_config_path.is_empty():
		return false
	if not FileAccess.file_exists(SAVE_PATH):
		return false

	var save_file := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if save_file == null:
		push_error("Failed to open persisted save %s." % SAVE_PATH)
		return false

	var raw_payload = save_file.get_var(false)
	save_file.close()
	if typeof(raw_payload) != TYPE_DICTIONARY:
		return false

	var payload: Dictionary = raw_payload
	if int(payload.get("version", -1)) != SAVE_VERSION:
		return false
	if payload.get("generation_config_path", "") != generation_config_path:
		return false

	var world_state_data = payload.get("world_state", {})
	if typeof(world_state_data) != TYPE_DICTIONARY:
		return false
	var world_state: Dictionary = world_state_data
	var world_data_raw = world_state.get("world_data", {})
	if typeof(world_data_raw) != TYPE_DICTIONARY:
		return false
	var world_data: Dictionary = world_data_raw
	if world_data.is_empty():
		return false

	var generation_config = load(generation_config_path)
	if generation_config == null:
		push_error("Failed to load config for persisted save from %s." % generation_config_path)
		return false

	_generation_config_path = generation_config_path
	_generation_config = generation_config
	_world_data = world_data.duplicate(true)
	_player_coord = world_state.get("player_coord", world_data.get("player_start_coord", generation_config.player_start_coord))
	_player_faction_id = world_state.get("player_faction_id", "player")
	_player_progress = _deserialize_player_progress(payload.get("player_state", {}))
	_refresh_progression_runtime()
	_has_active_world = true
	return true


func _build_save_payload() -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"generation_config_path": _generation_config_path,
		"world_state": _build_world_state_payload(),
		"player_state": PROGRESSION_SERIALIZATION_SCRIPT.serialize_player_progress(_player_progress),
		"meta": _build_meta_payload(),
	}


func _build_world_state_payload() -> Dictionary:
	return {
		"world_data": _world_data.duplicate(true),
		"player_coord": _player_coord,
		"player_faction_id": _player_faction_id,
	}


func _build_meta_payload() -> Dictionary:
	return {
		"saved_at_unix_time": int(Time.get_unix_time_from_system()),
		"save_format": "single_file_total_save",
	}


func _create_default_player_progress() -> PlayerProgress:
	return PLAYER_PROGRESS_SCRIPT.new()


func _normalize_player_progress(player_progress: PlayerProgress) -> PlayerProgress:
	if player_progress == null:
		return _create_default_player_progress()
	return player_progress


func _deserialize_player_progress(data: Variant) -> PlayerProgress:
	if typeof(data) != TYPE_DICTIONARY:
		return _create_default_player_progress()
	return PROGRESSION_SERIALIZATION_SCRIPT.deserialize_player_progress(data)


func _reset_runtime_state() -> void:
	_generation_config_path = ""
	_generation_config = null
	_world_data = {}
	_player_coord = Vector2i.ZERO
	_player_faction_id = "player"
	_player_progress = _create_default_player_progress()
	_refresh_progression_runtime()
	_has_active_world = false


func _refresh_progression_runtime() -> void:
	if _progression_content_registry == null:
		return

	_skill_defs = _progression_content_registry.get_skill_defs()
	_profession_defs = _progression_content_registry.get_profession_defs()
	_player_progress = _normalize_player_progress(_player_progress)

	_profession_assignment_service.setup(_player_progress, _skill_defs, _profession_defs)
	_profession_rule_service.setup(_player_progress, _skill_defs, _profession_defs)
	_progression_service.setup(
		_player_progress,
		_skill_defs,
		_profession_defs,
		_profession_rule_service,
		_profession_assignment_service
	)
	_skill_merge_service.setup(_player_progress, _skill_defs, _profession_assignment_service)
	_attribute_service.setup(_player_progress, _skill_defs, _profession_defs)


func _persist_runtime_state_if_active() -> void:
	if _has_active_world:
		save_game_state()


func _report_progression_content_errors() -> void:
	if _progression_content_registry == null:
		return

	for validation_error in _progression_content_registry.validate():
		push_error("Progression content error: %s" % validation_error)
