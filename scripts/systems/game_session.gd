## 文件说明：该脚本属于游戏会话相关的业务脚本，集中维护激活存档唯一标识、激活存档路径、激活存档元信息等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

extends Node

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_spawn_system.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const SKILL_BOOK_ITEM_FACTORY_SCRIPT = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")
const PROGRESSION_SERIALIZATION_SCRIPT = preload("res://scripts/systems/progression_serialization.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")

const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY
const LEGACY_SAVE_PATH := "user://world_map_state.dat"
const SAVE_VERSION := 5
const SAVE_INDEX_VERSION := 1
const MAX_ACTIVE_MEMBER_COUNT := 4

## 字段说明：记录激活存档唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _active_save_id := ""
## 字段说明：记录激活存档路径，供运行时加载场景、资源或存档文件时直接使用。
var _active_save_path := ""
## 字段说明：缓存激活存档元信息字典，集中保存可按键查询的运行时数据。
var _active_save_meta: Dictionary = {}
## 字段说明：记录生成配置路径，供运行时加载场景、资源或存档文件时直接使用。
var _generation_config_path: String = ""
## 字段说明：记录生成配置，会参与运行时状态流转、系统协作和存档恢复。
var _generation_config = null
## 字段说明：缓存世界数据字典，集中保存可按键查询的运行时数据。
var _world_data: Dictionary = {}
## 字段说明：记录玩家坐标，用于定位对象、绘制内容或执行网格计算。
var _player_coord: Vector2i = Vector2i.ZERO
## 字段说明：记录玩家阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _player_faction_id: String = "player"
## 字段说明：记录队伍状态，会参与运行时状态流转、系统协作和存档恢复。
var _party_state = PARTY_STATE_SCRIPT.new()
## 字段说明：用于标记当前是否已经具备激活世界，便于后续分支快速判断，会参与运行时状态流转、系统协作和存档恢复。
var _has_active_world := false
## 字段说明：标记战斗中的存档锁是否启用，启用后会阻止不安全时机的写盘操作。
var _battle_save_lock_enabled := false
## 字段说明：用于标记战斗存档是否已经发生变更，决定后续是否需要重新保存、重建或刷新。
var _battle_save_dirty := false

## 字段说明：记录成长内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _progression_content_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录物品内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _item_content_registry = ITEM_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录敌方内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _enemy_content_registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录技能书物品工厂，会参与运行时状态流转、系统协作和存档恢复。
var _skill_book_item_factory = SKILL_BOOK_ITEM_FACTORY_SCRIPT.new()
## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：缓存职业定义集合字典，集中保存可按键查询的运行时数据。
var _profession_defs: Dictionary = {}
## 字段说明：缓存成就定义集合字典，集中保存可按键查询的运行时数据。
var _achievement_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存敌方模板集合字典，集中保存可按键查询的运行时数据。
var _enemy_templates: Dictionary = {}
## 字段说明：缓存敌方 AI brain 集合字典，集中保存可按键查询的运行时数据。
var _enemy_ai_brains: Dictionary = {}
## 字段说明：缓存野外遭遇编队配置集合字典，集中保存可按键查询的运行时数据。
var _wild_encounter_rosters: Dictionary = {}


func _init() -> void:
	_report_progression_content_errors()
	_report_item_content_errors()
	_refresh_progression_content()
	_refresh_item_content()
	_refresh_enemy_content()


func ensure_world_ready(generation_config_path: String) -> int:
	if _has_active_world and _generation_config_path == generation_config_path:
		return OK

	if _try_load_game_state(generation_config_path):
		return OK

	return start_new_game(generation_config_path)


func start_new_game(generation_config_path: String) -> int:
	var preset_name := WORLD_PRESET_REGISTRY_SCRIPT.get_fallback_preset_name(generation_config_path)
	return create_new_save(generation_config_path, &"", preset_name)


func create_new_save(
	generation_config_path: String,
	preset_id: StringName = &"",
	preset_name: String = ""
) -> int:
	_reset_runtime_state()
	if generation_config_path.is_empty():
		push_error("GameSession requires a generation config path.")
		return ERR_INVALID_PARAMETER

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	var prepare_error := _prepare_new_world(generation_config_path, generation_config)
	if prepare_error != OK:
		return prepare_error

	var timestamp := int(Time.get_unix_time_from_system())
	var save_id := _generate_unique_save_id(timestamp)
	if save_id.is_empty():
		push_error("GameSession failed to allocate a unique save id.")
		_reset_runtime_state()
		return ERR_CANT_CREATE

	_active_save_id = save_id
	_active_save_path = _build_save_file_path(save_id)
	_active_save_meta = _build_save_meta(
		save_id,
		generation_config_path,
		preset_id,
		preset_name,
		generation_config.get_world_size_cells(),
		timestamp,
		timestamp
	)
	return _persist_game_state()


func load_bundled_save(
	template_path: String,
	save_id: String,
	display_name: String,
	world_preset_id: StringName = &"",
	world_preset_name: String = ""
) -> int:
	if template_path.is_empty() or save_id.is_empty():
		return ERR_INVALID_PARAMETER

	var read_result := _read_save_payload(template_path)
	var read_error := int(read_result.get("error", ERR_CANT_OPEN))
	if read_error != OK:
		return read_error

	var payload = read_result.get("payload", {})
	if typeof(payload) != TYPE_DICTIONARY:
		return ERR_INVALID_DATA

	var generation_config_path := String(payload.get("generation_config_path", ""))
	if generation_config_path.is_empty():
		push_error("Bundled save template %s is missing generation_config_path." % template_path)
		return ERR_INVALID_DATA

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	var timestamp := int(Time.get_unix_time_from_system())
	var save_meta := _build_save_meta(
		save_id,
		generation_config_path,
		world_preset_id,
		world_preset_name,
		generation_config.get_world_size_cells(),
		timestamp,
		timestamp
	)
	save_meta["display_name"] = display_name if not display_name.is_empty() else save_id
	payload["save_id"] = save_id
	payload["save_slot_meta"] = save_meta.duplicate(true)

	var load_error := _load_v5_payload(payload, generation_config_path, generation_config, save_meta)
	if load_error != OK:
		return load_error

	return _persist_game_state()


func list_save_slots() -> Array[Dictionary]:
	return _load_save_index_entries()


func load_save(save_id: String) -> int:
	if save_id.is_empty():
		return ERR_INVALID_PARAMETER

	var save_meta := _get_save_meta_by_id(save_id)
	if save_meta.is_empty():
		push_error("GameSession could not find save slot %s." % save_id)
		return ERR_DOES_NOT_EXIST

	var save_path := _build_save_file_path(save_id)
	var read_result := _read_save_payload(save_path)
	var read_error := int(read_result.get("error", ERR_CANT_OPEN))
	if read_error != OK:
		return read_error

	var payload = read_result.get("payload", {})
	if typeof(payload) != TYPE_DICTIONARY:
		push_error("GameSession loaded an invalid payload from %s." % save_path)
		return ERR_INVALID_DATA

	var generation_config_path := String(payload.get("generation_config_path", save_meta.get("generation_config_path", "")))
	if generation_config_path.is_empty():
		push_error("Save slot %s is missing generation_config_path." % save_id)
		return ERR_INVALID_DATA

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	return _load_v5_payload(payload, generation_config_path, generation_config, save_meta)


func has_active_world() -> bool:
	return _has_active_world


func get_active_save_id() -> String:
	return _active_save_id


func get_active_save_path() -> String:
	return _active_save_path


func get_active_save_meta() -> Dictionary:
	return _active_save_meta.duplicate(true)


func get_generation_config():
	return _generation_config


func get_generation_config_path() -> String:
	return _generation_config_path


func get_world_data() -> Dictionary:
	return _world_data


func set_world_data(world_data: Dictionary) -> int:
	_world_data = _normalize_world_data(world_data)
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


func get_party_state():
	return _party_state


func set_party_state(party_state) -> int:
	_party_state = _normalize_party_state(party_state)
	return save_game_state()


func set_battle_save_lock(enabled: bool) -> void:
	_battle_save_lock_enabled = enabled


func is_battle_save_locked() -> bool:
	return _battle_save_lock_enabled


func has_pending_save() -> bool:
	return _battle_save_dirty


func get_party_member_state(member_id: StringName):
	if _party_state == null:
		return null
	return _party_state.get_member_state(member_id)


func get_leader_member_state():
	if _party_state == null:
		return null
	return _party_state.get_member_state(_party_state.leader_member_id)


func get_progression_content_registry():
	return _progression_content_registry


func get_skill_defs() -> Dictionary:
	return _skill_defs


func get_profession_defs() -> Dictionary:
	return _profession_defs


func get_achievement_defs() -> Dictionary:
	return _achievement_defs


func get_item_defs() -> Dictionary:
	return _item_defs


func get_enemy_templates() -> Dictionary:
	return _enemy_templates


func get_enemy_ai_brains() -> Dictionary:
	return _enemy_ai_brains


func get_wild_encounter_rosters() -> Dictionary:
	return _wild_encounter_rosters


func save_world_state() -> int:
	return save_game_state()


func save_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _battle_save_lock_enabled:
		_battle_save_dirty = true
		return OK

	return _persist_game_state()


func flush_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _battle_save_lock_enabled:
		return ERR_BUSY
	if not _battle_save_dirty:
		return OK

	return _persist_game_state()


func clear_persisted_world() -> int:
	return clear_persisted_game()


func clear_persisted_game() -> int:
	_reset_runtime_state()

	var remove_error := _remove_directory_recursive(SAVE_DIRECTORY)
	if remove_error != OK:
		return remove_error

	if FileAccess.file_exists(LEGACY_SAVE_PATH):
		return DirAccess.remove_absolute(ProjectSettings.globalize_path(LEGACY_SAVE_PATH))
	return OK


func reset_runtime_cache() -> void:
	_reset_runtime_state()


func _try_load_game_state(generation_config_path: String) -> bool:
	if generation_config_path.is_empty():
		return false

	var save_meta := _find_most_recent_save_by_config(generation_config_path)
	if save_meta.is_empty():
		return false

	return load_save(String(save_meta.get("save_id", ""))) == OK


func _prepare_new_world(generation_config_path: String, generation_config) -> int:
	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(generation_config.world_size_in_chunks, generation_config.chunk_size)

	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var world_data: Dictionary = spawn_system.build_world(generation_config, grid_system)

	_generation_config_path = generation_config_path
	_generation_config = generation_config
	_world_data = _normalize_world_data(world_data)
	_player_coord = world_data.get("player_start_coord", generation_config.player_start_coord)
	_player_faction_id = "player"
	_party_state = _create_default_party_state()
	_has_active_world = true
	_battle_save_lock_enabled = false
	_battle_save_dirty = false
	return OK


func _persist_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _active_save_id.is_empty() or _active_save_path.is_empty():
		push_error("GameSession has world state but no active save slot.")
		return ERR_UNCONFIGURED

	var ensure_dir_error := _ensure_save_directory()
	if ensure_dir_error != OK:
		return ensure_dir_error

	var now := int(Time.get_unix_time_from_system())
	_active_save_meta = _build_save_meta(
		_active_save_id,
		_generation_config_path,
		StringName(String(_active_save_meta.get("world_preset_id", ""))),
		String(_active_save_meta.get("world_preset_name", "")),
		_generation_config.get_world_size_cells() if _generation_config != null else Vector2i.ZERO,
		int(_active_save_meta.get("created_at_unix_time", now)),
		now
	)

	var save_file := FileAccess.open(_active_save_path, FileAccess.WRITE)
	if save_file == null:
		var open_error := FileAccess.get_open_error()
		push_error("Failed to open save file %s. Error: %s" % [_active_save_path, open_error])
		return open_error

	save_file.store_var(_build_save_payload(now), false)
	save_file.close()

	var index_error := _write_save_index(_upsert_save_meta(_load_save_index_entries(), _active_save_meta))
	if index_error != OK:
		return index_error

	_battle_save_dirty = false
	return OK


func _load_v5_payload(
	payload: Dictionary,
	generation_config_path: String,
	generation_config,
	save_meta: Dictionary
) -> int:
	var save_version := int(payload.get("version", -1))
	if save_version != SAVE_VERSION:
		return ERR_INVALID_DATA

	var world_state_data = payload.get("world_state", {})
	if typeof(world_state_data) != TYPE_DICTIONARY:
		return ERR_INVALID_DATA
	var world_state: Dictionary = world_state_data

	var world_data_raw = world_state.get("world_data", {})
	if typeof(world_data_raw) != TYPE_DICTIONARY:
		return ERR_INVALID_DATA
	var world_data = _normalize_world_data(world_data_raw)
	if world_data.is_empty():
		return ERR_INVALID_DATA

	var payload_save_id := String(payload.get("save_id", save_meta.get("save_id", "")))
	if payload_save_id.is_empty():
		return ERR_INVALID_DATA

	var slot_meta_raw = payload.get("save_slot_meta", {})
	var merged_meta: Dictionary = save_meta.duplicate(true)
	if typeof(slot_meta_raw) == TYPE_DICTIONARY:
		for key in slot_meta_raw.keys():
			merged_meta[key] = slot_meta_raw[key]

	merged_meta["save_id"] = payload_save_id
	merged_meta["generation_config_path"] = generation_config_path
	if not merged_meta.has("world_size_cells") or _read_vector2i(merged_meta.get("world_size_cells", Vector2i.ZERO)) == Vector2i.ZERO:
		merged_meta["world_size_cells"] = generation_config.get_world_size_cells()

	_reset_runtime_state()
	_active_save_id = payload_save_id
	_active_save_path = _build_save_file_path(payload_save_id)
	_active_save_meta = _normalize_save_meta(merged_meta)
	_generation_config_path = generation_config_path
	_generation_config = generation_config
	_world_data = world_data
	_player_coord = world_state.get("player_coord", world_data.get("player_start_coord", generation_config.player_start_coord))
	_player_faction_id = world_state.get("player_faction_id", "player")
	_party_state = _normalize_party_state(
		PROGRESSION_SERIALIZATION_SCRIPT.deserialize_party_state(payload.get("party_state", {}))
	)
	_has_active_world = true
	return OK


func _build_save_payload(saved_at_unix_time: int) -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"save_id": _active_save_id,
		"generation_config_path": _generation_config_path,
		"world_state": _build_world_state_payload(),
		"party_state": PROGRESSION_SERIALIZATION_SCRIPT.serialize_party_state(_party_state),
		"meta": _build_meta_payload(saved_at_unix_time),
		"save_slot_meta": _active_save_meta.duplicate(true),
	}


func _build_world_state_payload() -> Dictionary:
	return {
		"world_data": _serialize_world_data(_world_data),
		"player_coord": _player_coord,
		"player_faction_id": _player_faction_id,
	}


func _build_meta_payload(saved_at_unix_time: int) -> Dictionary:
	return {
		"saved_at_unix_time": saved_at_unix_time,
		"save_format": "multi_save_total_save",
	}


func _build_save_meta(
	save_id: String,
	generation_config_path: String,
	preset_id: StringName,
	preset_name: String,
	world_size_cells: Vector2i,
	created_at_unix_time: int,
	updated_at_unix_time: int
) -> Dictionary:
	var resolved_preset_name := preset_name
	if resolved_preset_name.is_empty():
		resolved_preset_name = WORLD_PRESET_REGISTRY_SCRIPT.get_fallback_preset_name(generation_config_path)
	return _normalize_save_meta({
		"save_id": save_id,
		"display_name": save_id,
		"world_preset_id": String(preset_id),
		"world_preset_name": resolved_preset_name,
		"generation_config_path": generation_config_path,
		"world_size_cells": world_size_cells,
		"created_at_unix_time": created_at_unix_time,
		"updated_at_unix_time": updated_at_unix_time,
	})


func _generate_unique_save_id(timestamp: int) -> String:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var datetime := Time.get_datetime_dict_from_unix_time(timestamp)
	var prefix := "save_%04d%02d%02d_%02d%02d%02d" % [
		int(datetime.get("year", 1970)),
		int(datetime.get("month", 1)),
		int(datetime.get("day", 1)),
		int(datetime.get("hour", 0)),
		int(datetime.get("minute", 0)),
		int(datetime.get("second", 0)),
	]

	for _attempt in range(128):
		var save_id := "%s_%06d" % [prefix, rng.randi_range(0, 999999)]
		if _get_save_meta_by_id(save_id).is_empty() and not FileAccess.file_exists(_build_save_file_path(save_id)):
			return save_id
	return ""


func _load_generation_config(generation_config_path: String):
	var generation_config = load(generation_config_path)
	if generation_config == null:
		push_error("GameSession failed to load config from %s." % generation_config_path)
	return generation_config


func _read_save_payload(save_path: String) -> Dictionary:
	if not FileAccess.file_exists(save_path):
		push_error("GameSession could not find persisted save %s." % save_path)
		return {"error": ERR_DOES_NOT_EXIST}

	var save_file := FileAccess.open(save_path, FileAccess.READ)
	if save_file == null:
		var open_error := FileAccess.get_open_error()
		push_error("Failed to open persisted save %s. Error: %s" % [save_path, open_error])
		return {"error": open_error}

	var raw_payload = save_file.get_var(false)
	save_file.close()
	if typeof(raw_payload) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}

	return {
		"error": OK,
		"payload": raw_payload,
	}


func _ensure_save_directory() -> int:
	return DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SAVE_DIRECTORY))


func _build_save_file_path(save_id: String) -> String:
	return "%s/%s.dat" % [SAVE_DIRECTORY, save_id]


func _load_save_index_entries() -> Array[Dictionary]:
	if not FileAccess.file_exists(SAVE_INDEX_PATH):
		return []

	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	if index_file == null:
		push_error("Failed to open save index %s." % SAVE_INDEX_PATH)
		return []

	var raw_payload = index_file.get_var(false)
	index_file.close()

	var raw_entries: Array = []
	if typeof(raw_payload) == TYPE_DICTIONARY:
		raw_entries = raw_payload.get("saves", [])
	elif typeof(raw_payload) == TYPE_ARRAY:
		raw_entries = raw_payload

	var entries: Array[Dictionary] = []
	for raw_entry in raw_entries:
		if typeof(raw_entry) != TYPE_DICTIONARY:
			continue
		var entry := _normalize_save_meta(raw_entry)
		if entry.is_empty():
			continue
		if not FileAccess.file_exists(_build_save_file_path(String(entry.get("save_id", "")))):
			continue
		entries.append(entry)

	entries.sort_custom(_sort_save_meta_newest_first)
	return entries


func _write_save_index(entries: Array[Dictionary]) -> int:
	var ensure_dir_error := _ensure_save_directory()
	if ensure_dir_error != OK:
		return ensure_dir_error

	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	if index_file == null:
		var open_error := FileAccess.get_open_error()
		push_error("Failed to open save index %s. Error: %s" % [SAVE_INDEX_PATH, open_error])
		return open_error

	var normalized_entries: Array[Dictionary] = []
	for entry in entries:
		var normalized_entry := _normalize_save_meta(entry)
		if normalized_entry.is_empty():
			continue
		normalized_entries.append(normalized_entry)

	index_file.store_var({
		"version": SAVE_INDEX_VERSION,
		"saves": normalized_entries,
	}, false)
	index_file.close()
	return OK


func _upsert_save_meta(entries: Array[Dictionary], save_meta: Dictionary) -> Array[Dictionary]:
	var normalized_meta := _normalize_save_meta(save_meta)
	if normalized_meta.is_empty():
		return entries

	var updated_entries: Array[Dictionary] = []
	var replaced := false
	for entry in entries:
		if String(entry.get("save_id", "")) == String(normalized_meta.get("save_id", "")):
			updated_entries.append(normalized_meta)
			replaced = true
		else:
			updated_entries.append(_normalize_save_meta(entry))

	if not replaced:
		updated_entries.append(normalized_meta)

	updated_entries.sort_custom(_sort_save_meta_newest_first)
	return updated_entries


func _get_save_meta_by_id(save_id: String) -> Dictionary:
	for entry in _load_save_index_entries():
		if String(entry.get("save_id", "")) == save_id:
			return entry
	return {}


func _find_most_recent_save_by_config(generation_config_path: String) -> Dictionary:
	for entry in _load_save_index_entries():
		if String(entry.get("generation_config_path", "")) == generation_config_path:
			return entry
	return {}


func _normalize_save_meta(raw_meta: Dictionary) -> Dictionary:
	var save_id := String(raw_meta.get("save_id", "")).strip_edges()
	if save_id.is_empty():
		return {}

	var generation_config_path := String(raw_meta.get("generation_config_path", ""))
	var display_name := String(raw_meta.get("display_name", save_id))
	if display_name.is_empty():
		display_name = save_id

	var world_preset_name := String(raw_meta.get("world_preset_name", ""))
	if world_preset_name.is_empty():
		world_preset_name = WORLD_PRESET_REGISTRY_SCRIPT.get_fallback_preset_name(generation_config_path)

	var created_at := int(raw_meta.get("created_at_unix_time", 0))
	var updated_at := int(raw_meta.get("updated_at_unix_time", created_at))
	if created_at <= 0:
		created_at = updated_at
	if updated_at <= 0:
		updated_at = created_at

	return {
		"save_id": save_id,
		"display_name": display_name,
		"world_preset_id": String(raw_meta.get("world_preset_id", "")),
		"world_preset_name": world_preset_name,
		"generation_config_path": generation_config_path,
		"world_size_cells": _read_vector2i(raw_meta.get("world_size_cells", Vector2i.ZERO)),
		"created_at_unix_time": created_at,
		"updated_at_unix_time": updated_at,
	}


func _read_vector2i(value: Variant, fallback: Vector2i = Vector2i.ZERO) -> Vector2i:
	if value is Vector2i:
		return value
	if value is Vector2:
		var vector2_value := value as Vector2
		return Vector2i(int(vector2_value.x), int(vector2_value.y))
	return fallback


func _sort_save_meta_newest_first(a: Dictionary, b: Dictionary) -> bool:
	var updated_a := int(a.get("updated_at_unix_time", 0))
	var updated_b := int(b.get("updated_at_unix_time", 0))
	if updated_a == updated_b:
		var created_a := int(a.get("created_at_unix_time", 0))
		var created_b := int(b.get("created_at_unix_time", 0))
		if created_a == created_b:
			return String(a.get("save_id", "")) > String(b.get("save_id", ""))
		return created_a > created_b
	return updated_a > updated_b


func _remove_directory_recursive(virtual_path: String) -> int:
	var absolute_path := ProjectSettings.globalize_path(virtual_path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		return OK

	var dir := DirAccess.open(virtual_path)
	if dir == null:
		var open_error := DirAccess.get_open_error()
		push_error("Failed to open directory %s for cleanup. Error: %s" % [virtual_path, open_error])
		return open_error

	dir.list_dir_begin()
	while true:
		var name := dir.get_next()
		if name.is_empty():
			break
		if name == "." or name == "..":
			continue

		var child_virtual_path := "%s/%s" % [virtual_path, name]
		if dir.current_is_dir():
			var nested_error := _remove_directory_recursive(child_virtual_path)
			if nested_error != OK:
				dir.list_dir_end()
				return nested_error
			continue

		var remove_file_error := DirAccess.remove_absolute(ProjectSettings.globalize_path(child_virtual_path))
		if remove_file_error != OK:
			dir.list_dir_end()
			return remove_file_error

	dir.list_dir_end()
	return DirAccess.remove_absolute(absolute_path)


func _create_default_party_state():
	var party_state = PARTY_STATE_SCRIPT.new()

	var sword_member = _build_default_member_state(
		&"player_sword_01",
		"剑士",
		&"warrior_heavy_strike",
		&"portrait_sword",
		34,
		0,
		4,
		2,
		3,
		1,
		1,
		1,
		24,
		12
	)

	party_state.set_member_state(sword_member)
	party_state.leader_member_id = &"player_sword_01"
	party_state.active_member_ids = ProgressionDataUtils.to_string_name_array([
		"player_sword_01",
	])
	party_state.reserve_member_ids = ProgressionDataUtils.to_string_name_array([])
	return party_state


func _build_default_member_state(
	member_id: StringName,
	display_name: String,
	starting_skill_id: StringName,
	portrait_id: StringName,
	current_hp: int,
	current_mp: int,
	strength: int,
	agility: int,
	constitution: int,
	perception: int,
	intelligence: int,
	willpower: int,
	base_hp_max: int,
	storage_space: int = 0
):
	var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.faction_id = &"player"
	member_state.portrait_id = portrait_id
	member_state.control_mode = &"manual"
	member_state.current_hp = current_hp
	member_state.current_mp = current_mp
	member_state.body_size = 1

	var progression = UNIT_PROGRESS_SCRIPT.new()
	progression.unit_id = member_id
	progression.display_name = display_name
	progression.character_level = 1

	var unit_base_attributes: UnitBaseAttributes = UNIT_BASE_ATTRIBUTES_SCRIPT.new()
	unit_base_attributes.strength = strength
	unit_base_attributes.agility = agility
	unit_base_attributes.constitution = constitution
	unit_base_attributes.perception = perception
	unit_base_attributes.intelligence = intelligence
	unit_base_attributes.willpower = willpower
	unit_base_attributes.custom_stats[&"hp_max"] = base_hp_max
	unit_base_attributes.custom_stats[&"mp_max"] = current_mp
	unit_base_attributes.custom_stats[&"storage_space"] = maxi(storage_space, 0)
	progression.unit_base_attributes = unit_base_attributes

	var starter_skill = UNIT_SKILL_PROGRESS_SCRIPT.new()
	starter_skill.skill_id = starting_skill_id
	starter_skill.is_learned = true
	starter_skill.is_core = true
	starter_skill.assigned_profession_id = &"warrior"
	progression.set_skill_progress(starter_skill)

	var warrior_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.new()
	warrior_progress.profession_id = &"warrior"
	warrior_progress.rank = 1
	warrior_progress.is_active = true
	warrior_progress.add_core_skill(starting_skill_id)
	progression.set_profession_progress(warrior_progress)
	progression.sync_active_core_skill_ids()

	member_state.progression = progression
	return member_state


func _normalize_party_state(party_state):
	if party_state == null:
		return PARTY_STATE_SCRIPT.new()

	var normalized = PROGRESSION_SERIALIZATION_SCRIPT.deserialize_party_state(
		PROGRESSION_SERIALIZATION_SCRIPT.serialize_party_state(party_state)
	)
	var ordered_member_ids: Array[StringName] = []
	for key in ProgressionDataUtils.sorted_string_keys(normalized.member_states):
		ordered_member_ids.append(StringName(key))

	var seen_ids: Dictionary = {}
	var active_member_ids: Array[StringName] = []
	for member_id in normalized.active_member_ids:
		if member_id == &"" or seen_ids.has(member_id):
			continue
		if normalized.get_member_state(member_id) == null:
			continue
		if active_member_ids.size() >= MAX_ACTIVE_MEMBER_COUNT:
			continue
		seen_ids[member_id] = true
		active_member_ids.append(member_id)

	var reserve_member_ids: Array[StringName] = []
	for member_id in normalized.reserve_member_ids:
		if member_id == &"" or seen_ids.has(member_id):
			continue
		if normalized.get_member_state(member_id) == null:
			continue
		seen_ids[member_id] = true
		reserve_member_ids.append(member_id)

	for member_id in ordered_member_ids:
		if seen_ids.has(member_id):
			continue
		if active_member_ids.size() < MAX_ACTIVE_MEMBER_COUNT:
			active_member_ids.append(member_id)
		else:
			reserve_member_ids.append(member_id)
		seen_ids[member_id] = true

	if active_member_ids.is_empty() and not ordered_member_ids.is_empty():
		active_member_ids.append(ordered_member_ids[0])

	if normalized.leader_member_id == &"" or not active_member_ids.has(normalized.leader_member_id):
		normalized.leader_member_id = active_member_ids[0] if not active_member_ids.is_empty() else &""

	normalized.active_member_ids = ProgressionDataUtils.to_string_name_array(active_member_ids)
	normalized.reserve_member_ids = ProgressionDataUtils.to_string_name_array(reserve_member_ids)
	return normalized


func _normalize_world_data(world_data: Dictionary) -> Dictionary:
	var normalized = world_data.duplicate(true)
	normalized["world_step"] = maxi(int(world_data.get("world_step", 0)), 0)
	var encounter_anchors: Array = []
	for encounter_anchor_data in world_data.get("encounter_anchors", []):
		if encounter_anchor_data is RefCounted and encounter_anchor_data.get_script() == ENCOUNTER_ANCHOR_DATA_SCRIPT:
			encounter_anchors.append(encounter_anchor_data)
		elif encounter_anchor_data is Dictionary:
			encounter_anchors.append(PROGRESSION_SERIALIZATION_SCRIPT.deserialize_encounter_anchor(encounter_anchor_data))
	normalized["encounter_anchors"] = encounter_anchors
	return normalized


func _serialize_world_data(world_data: Dictionary) -> Dictionary:
	var serialized_world_data = world_data.duplicate(true)
	var encounter_anchor_payloads: Array[Dictionary] = []
	for encounter_anchor_data in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_anchor_data
		if encounter_anchor == null:
			continue
		encounter_anchor_payloads.append(
			PROGRESSION_SERIALIZATION_SCRIPT.serialize_encounter_anchor(encounter_anchor)
		)
	serialized_world_data["encounter_anchors"] = encounter_anchor_payloads
	return serialized_world_data


func _reset_runtime_state() -> void:
	_active_save_id = ""
	_active_save_path = ""
	_active_save_meta = {}
	_generation_config_path = ""
	_generation_config = null
	_world_data = {}
	_player_coord = Vector2i.ZERO
	_player_faction_id = "player"
	_party_state = PARTY_STATE_SCRIPT.new()
	_has_active_world = false
	_battle_save_lock_enabled = false
	_battle_save_dirty = false


func _refresh_progression_content() -> void:
	if _progression_content_registry == null:
		return

	_skill_defs = _progression_content_registry.get_skill_defs()
	_profession_defs = _progression_content_registry.get_profession_defs()
	_achievement_defs = _progression_content_registry.get_achievement_defs()


func _refresh_item_content() -> void:
	if _item_content_registry == null:
		return

	_item_defs = _item_content_registry.get_item_defs().duplicate()
	if _skill_book_item_factory != null:
		var generated_skill_book_defs := _skill_book_item_factory.build_generated_item_defs(_skill_defs, _item_defs)
		for item_id in generated_skill_book_defs.keys():
			_item_defs[item_id] = generated_skill_book_defs[item_id]


func _refresh_enemy_content() -> void:
	if _enemy_content_registry == null:
		return

	_enemy_templates = _enemy_content_registry.get_enemy_templates()
	_enemy_ai_brains = _enemy_content_registry.get_enemy_ai_brains()
	_wild_encounter_rosters = _enemy_content_registry.get_wild_encounter_rosters()


func _report_progression_content_errors() -> void:
	if _progression_content_registry == null:
		return

	for validation_error in _progression_content_registry.validate():
		push_error("Progression content error: %s" % validation_error)


func _report_item_content_errors() -> void:
	if _item_content_registry == null:
		return

	for validation_error in _item_content_registry.validate():
		push_error("Item content error: %s" % validation_error)
