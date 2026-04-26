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
const RECIPE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/recipe_content_registry.gd")
const SKILL_BOOK_ITEM_FACTORY_SCRIPT = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")
const PROGRESSION_SERIALIZATION_SCRIPT = preload("res://scripts/systems/progression_serialization.gd")
const SAVE_SERIALIZER_SCRIPT = preload("res://scripts/systems/save_serializer.gd")
const GAME_LOG_SERVICE_SCRIPT = preload("res://scripts/systems/game_log_service.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const CHARACTER_CREATION_SERVICE_SCRIPT = preload("res://scripts/systems/character_creation_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY
const SAVE_VERSION := 5
const SAVE_INDEX_VERSION := 1
const MAX_ACTIVE_MEMBER_COUNT := 4
const CONTENT_VALIDATION_DOMAIN_ORDER := ["progression", "item", "recipe", "enemy"]
const RANDOM_START_SKILL_TIER_BASIC: StringName = &"basic"
const RANDOM_START_SKILL_TIER_INTERMEDIATE: StringName = &"intermediate"
const RANDOM_START_SKILL_TIER_ADVANCED: StringName = &"advanced"
const RANDOM_START_SKILL_TIER_ULTIMATE: StringName = &"ultimate"
const RANDOM_START_SKILL_LEVEL_BY_TIER := {
	RANDOM_START_SKILL_TIER_BASIC: 3,
	RANDOM_START_SKILL_TIER_INTERMEDIATE: 2,
	RANDOM_START_SKILL_TIER_ADVANCED: 1,
	RANDOM_START_SKILL_TIER_ULTIMATE: 0,
}
const RANDOM_START_SKILL_KEYWORDS_ULTIMATE := ["终极", "大招"]
const RANDOM_START_SKILL_KEYWORDS_ADVANCED := ["高阶", "招牌", "大型召唤"]
const RANDOM_START_SKILL_KEYWORDS_INTERMEDIATE := ["中段", "中后期"]
const RANDOM_START_SKILL_KEYWORDS_BASIC := ["基础", "低耗", "起手", "最小保障"]

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
## 字段说明：记录配方内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _recipe_content_registry = RECIPE_CONTENT_REGISTRY_SCRIPT.new()
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
## 字段说明：缓存任务定义集合字典，集中保存可按键查询的运行时数据。
var _quest_defs: Dictionary = {}
## 字段说明：缓存物品定义集合字典，集中保存可按键查询的运行时数据。
var _item_defs: Dictionary = {}
## 字段说明：缓存配方定义集合字典，集中保存可按键查询的运行时数据。
var _recipe_defs: Dictionary = {}
## 字段说明：缓存敌方模板集合字典，集中保存可按键查询的运行时数据。
var _enemy_templates: Dictionary = {}
## 字段说明：缓存敌方 AI brain 集合字典，集中保存可按键查询的运行时数据。
var _enemy_ai_brains: Dictionary = {}
## 字段说明：缓存野外遭遇编队配置集合字典，集中保存可按键查询的运行时数据。
var _wild_encounter_rosters: Dictionary = {}
## 字段说明：缓存内容校验快照，供 headless/test 快照稳定暴露各 domain 的错误摘要。
var _content_validation_snapshot: Dictionary = {}
var _save_serializer = SAVE_SERIALIZER_SCRIPT.new()
var _log_service = GAME_LOG_SERVICE_SCRIPT.new()


func _init() -> void:
	_save_serializer.setup(
		PROGRESSION_SERIALIZATION_SCRIPT,
		WORLD_PRESET_REGISTRY_SCRIPT,
		PARTY_STATE_SCRIPT,
		ENCOUNTER_ANCHOR_DATA_SCRIPT,
		SAVE_VERSION,
		SAVE_INDEX_VERSION,
		MAX_ACTIVE_MEMBER_COUNT
	)
	_refresh_progression_content()
	_refresh_item_content()
	_refresh_recipe_content()
	_refresh_enemy_content()
	_refresh_content_validation_snapshot()
	_report_content_validation_errors()


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
	preset_name: String = "",
	character_creation_payload: Dictionary = {}
) -> int:
	_reset_runtime_state()
	if generation_config_path.is_empty():
		_push_session_error(
			"session.save.create.invalid_generation_config",
			"GameSession requires a generation config path."
		)
		return ERR_INVALID_PARAMETER

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	var prepare_error := _prepare_new_world(generation_config_path, generation_config)
	if prepare_error != OK:
		return prepare_error

	_apply_character_creation_payload_to_main_character(character_creation_payload)

	var timestamp := int(Time.get_unix_time_from_system())
	var save_id := _generate_unique_save_id(timestamp)
	if save_id.is_empty():
		_push_session_error("session.save.create.allocate_id_failed", "GameSession failed to allocate a unique save id.")
		_reset_runtime_state()
		return ERR_CANT_CREATE

	_active_save_id = save_id
	_active_save_path = _build_save_file_path(save_id)
	var resolved_preset_name := preset_name if not preset_name.is_empty() else WORLD_PRESET_REGISTRY_SCRIPT.get_fallback_preset_name(generation_config_path)
	_active_save_meta = _build_save_meta(
		save_id,
		save_id,
		generation_config_path,
		preset_id,
		resolved_preset_name,
		generation_config.get_world_size_cells(),
		timestamp,
		timestamp
	)
	_rotate_log_session()
	var persist_error := _persist_game_state()
	if persist_error == OK:
		_log_session_info("session.save.create.ok", "已创建新存档。", {
			"save_id": _active_save_id,
			"generation_config_path": generation_config_path,
			"preset_id": String(preset_id),
			"preset_name": preset_name,
		})
	return persist_error


func list_save_slots() -> Array[Dictionary]:
	return _load_save_index_entries()


func load_save(save_id: String) -> int:
	if save_id.is_empty():
		return ERR_INVALID_PARAMETER

	var save_meta := _get_save_meta_by_id(save_id)
	if save_meta.is_empty():
		_push_session_error("session.save.load.missing_slot", "GameSession could not find save slot %s." % save_id, {
			"save_id": save_id,
		})
		return ERR_DOES_NOT_EXIST

	var save_path := _build_save_file_path(save_id)
	var read_result := _read_save_payload(save_path)
	var read_error := int(read_result.get("error", ERR_CANT_OPEN))
	if read_error != OK:
		return read_error

	var payload = read_result.get("payload", {})
	if typeof(payload) != TYPE_DICTIONARY:
		_push_session_error("session.save.load.invalid_payload", "GameSession loaded an invalid payload from %s." % save_path, {
			"save_id": save_id,
			"save_path": save_path,
		})
		return ERR_INVALID_DATA

	var generation_config_path := String(payload.get("generation_config_path", save_meta.get("generation_config_path", "")))
	if generation_config_path.is_empty():
		_push_session_error("session.save.load.missing_generation_config", "Save slot %s is missing generation_config_path." % save_id, {
			"save_id": save_id,
			"save_path": save_path,
		})
		return ERR_INVALID_DATA

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	var load_error := _load_v5_payload(payload, generation_config_path, generation_config, save_meta)
	if load_error == OK:
		_rotate_log_session()
		_log_session_info("session.save.load.ok", "已加载存档。", {
			"save_id": save_id,
			"save_path": save_path,
			"generation_config_path": generation_config_path,
		})
	return load_error


func has_active_world() -> bool:
	return _has_active_world


func get_active_save_id() -> String:
	return _active_save_id


func get_active_save_path() -> String:
	return _active_save_path


func get_active_save_meta() -> Dictionary:
	return _active_save_meta.duplicate(true)


func get_log_service():
	return _log_service


func get_recent_logs(limit: int = 50) -> Array[Dictionary]:
	return _log_service.get_recent_entries(limit) if _log_service != null else []


func get_log_snapshot(limit: int = 50) -> Dictionary:
	return _log_service.build_snapshot(limit) if _log_service != null else {}


func get_active_log_file_path() -> String:
	return _log_service.get_log_path() if _log_service != null else ""


func allocate_unique_save_id(prefix: String = "save") -> String:
	return _generate_unique_save_id(int(Time.get_unix_time_from_system()), prefix)


func get_content_validation_snapshot() -> Dictionary:
	return _content_validation_snapshot.duplicate(true)


func refresh_content_validation_snapshot() -> Dictionary:
	_refresh_content_validation_snapshot()
	return get_content_validation_snapshot()


func log_event(level: String, domain: String, event_id: String, message: String, context: Dictionary = {}) -> Dictionary:
	return _log_service.append_entry(level, domain, event_id, message, context) if _log_service != null else {}


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


func discard_pending_save() -> void:
	_battle_save_dirty = false


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


func get_quest_defs() -> Dictionary:
	return _quest_defs


func get_item_defs() -> Dictionary:
	return _item_defs


func get_recipe_defs() -> Dictionary:
	return _recipe_defs


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
	_log_session_info("session.save.clear.ok", "已清理存档目录。")
	return OK


func reset_runtime_cache() -> void:
	_reset_runtime_state()


func unload_active_world() -> void:
	if not _has_active_world:
		return
	var unloaded_save_id := _active_save_id
	_reset_runtime_state()
	_rotate_log_session()
	_log_session_info("session.runtime.unload.ok", "已卸载当前运行中世界。", {
		"save_id": unloaded_save_id,
	})


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
		_push_session_error("session.save.persist.missing_slot", "GameSession has world state but no active save slot.")
		return ERR_UNCONFIGURED

	var ensure_dir_error := _ensure_save_directory()
	if ensure_dir_error != OK:
		return ensure_dir_error

	var now := int(Time.get_unix_time_from_system())
	var display_name := String(_active_save_meta.get("display_name", _active_save_id))
	_active_save_meta = _build_save_meta(
		_active_save_id,
		display_name,
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
		_push_session_error("session.save.persist.open_failed", "Failed to open save file %s. Error: %s" % [_active_save_path, open_error], {
			"save_id": _active_save_id,
			"save_path": _active_save_path,
			"open_error": open_error,
		})
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
	var decode_result := _save_serializer.decode_v5_payload(payload, generation_config_path, generation_config, save_meta)
	var decode_error := int(decode_result.get("error", ERR_INVALID_DATA))
	if decode_error != OK:
		return decode_error
	_reset_runtime_state()
	_active_save_id = String(decode_result.get("active_save_id", ""))
	_active_save_path = _build_save_file_path(_active_save_id)
	_active_save_meta = decode_result.get("active_save_meta", {}).duplicate(true)
	_generation_config_path = String(decode_result.get("generation_config_path", generation_config_path))
	_generation_config = decode_result.get("generation_config", generation_config)
	_world_data = decode_result.get("world_data", {}).duplicate(true)
	_player_coord = decode_result.get("player_coord", Vector2i.ZERO)
	_player_faction_id = String(decode_result.get("player_faction_id", "player"))
	_party_state = decode_result.get("party_state", PARTY_STATE_SCRIPT.new())
	_has_active_world = true
	return OK


func _build_save_payload(saved_at_unix_time: int) -> Dictionary:
	return _save_serializer.build_save_payload(
		_active_save_id,
		_generation_config_path,
		_active_save_meta,
		_world_data,
		_player_coord,
		_player_faction_id,
		_party_state,
		saved_at_unix_time
	)


func _build_world_state_payload() -> Dictionary:
	return _save_serializer.build_world_state_payload(_world_data, _player_coord, _player_faction_id)


func _build_meta_payload(saved_at_unix_time: int) -> Dictionary:
	return _save_serializer.build_meta_payload(saved_at_unix_time)


func _build_save_meta(
	save_id: String,
	display_name: String,
	generation_config_path: String,
	preset_id: StringName,
	preset_name: String,
	world_size_cells: Vector2i,
	created_at_unix_time: int,
	updated_at_unix_time: int
) -> Dictionary:
	return _save_serializer.build_save_meta(
		save_id,
		display_name,
		generation_config_path,
		preset_id,
		preset_name,
		world_size_cells,
		created_at_unix_time,
		updated_at_unix_time
	)


func _generate_unique_save_id(timestamp: int, prefix: String = "save") -> String:
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var datetime := Time.get_datetime_dict_from_unix_time(timestamp)
	var normalized_prefix := prefix.strip_edges().replace(" ", "_")
	if normalized_prefix.is_empty():
		normalized_prefix = "save"
	var id_prefix := "%s_%04d%02d%02d_%02d%02d%02d" % [
		normalized_prefix,
		int(datetime.get("year", 1970)),
		int(datetime.get("month", 1)),
		int(datetime.get("day", 1)),
		int(datetime.get("hour", 0)),
		int(datetime.get("minute", 0)),
		int(datetime.get("second", 0)),
	]

	for _attempt in range(128):
		var save_id := "%s_%06d" % [id_prefix, rng.randi_range(0, 999999)]
		if _get_save_meta_by_id(save_id).is_empty() and not FileAccess.file_exists(_build_save_file_path(save_id)):
			return save_id
	return ""


func _load_generation_config(generation_config_path: String):
	var generation_config = load(generation_config_path)
	if generation_config == null:
		_push_session_error("session.config.load_failed", "GameSession failed to load config from %s." % generation_config_path, {
			"generation_config_path": generation_config_path,
		})
	return generation_config


func _read_save_payload(save_path: String, emit_errors: bool = true) -> Dictionary:
	if not FileAccess.file_exists(save_path):
		if emit_errors:
			_push_session_error("session.save.read.missing_file", "GameSession could not find persisted save %s." % save_path, {
				"save_path": save_path,
			})
		return {"error": ERR_DOES_NOT_EXIST}

	var save_file := FileAccess.open(save_path, FileAccess.READ)
	if save_file == null:
		var open_error := FileAccess.get_open_error()
		if emit_errors:
			_push_session_error("session.save.read.open_failed", "Failed to open persisted save %s. Error: %s" % [save_path, open_error], {
				"save_path": save_path,
				"open_error": open_error,
			})
		return {"error": open_error}

	var save_size := int(save_file.get_length())
	# Corrupt or truncated files should be treated as invalid save payloads
	# without invoking Variant decoding, which would otherwise emit engine errors.
	if save_size < 8:
		save_file.close()
		return {"error": ERR_INVALID_DATA}

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
		return _rebuild_save_index_entries_from_save_files()

	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	if index_file == null:
		return _rebuild_save_index_entries_from_save_files()

	var raw_payload = _read_save_index_payload(index_file)
	index_file.close()

	var raw_entries: Array = []
	if typeof(raw_payload) == TYPE_DICTIONARY:
		raw_entries = raw_payload.get("saves", [])
	elif typeof(raw_payload) == TYPE_ARRAY:
		raw_entries = raw_payload
	else:
		var rebuilt_entries := _rebuild_save_index_entries_from_save_files()
		if not rebuilt_entries.is_empty():
			_write_save_index(rebuilt_entries)
		return rebuilt_entries

	var entries := _normalize_save_index_entries(raw_entries)
	var rebuilt_entries := _rebuild_save_index_entries_from_save_files()
	var merged_entries := _merge_save_index_entries(entries, rebuilt_entries)
	if merged_entries.size() != entries.size():
		_write_save_index(merged_entries)
	return merged_entries


func _write_save_index(entries: Array[Dictionary]) -> int:
	var ensure_dir_error := _ensure_save_directory()
	if ensure_dir_error != OK:
		return ensure_dir_error

	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	if index_file == null:
		# Save files remain authoritative; the index is a rebuildable cache.
		return OK

	var normalized_entries := _normalize_save_index_entries(entries)
	index_file.store_string(JSON.stringify({
		"version": SAVE_INDEX_VERSION,
		"saves": _serialize_save_index_entries(normalized_entries),
	}))
	index_file.close()
	return OK


func _read_save_index_payload(index_file: FileAccess) -> Variant:
	return _save_serializer.read_save_index_payload(index_file)


func _is_ascii_save_index_buffer(raw_bytes: PackedByteArray) -> bool:
	return _save_serializer.is_ascii_save_index_buffer(raw_bytes)


func _ascii_buffer_to_string(raw_bytes: PackedByteArray) -> String:
	return _save_serializer.ascii_buffer_to_string(raw_bytes)


func _normalize_save_index_entries(raw_entries: Array) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for raw_entry in raw_entries:
		if typeof(raw_entry) != TYPE_DICTIONARY:
			continue
		var entry := _normalize_save_meta(_deserialize_save_index_entry(raw_entry))
		if entry.is_empty():
			continue
		if not FileAccess.file_exists(_build_save_file_path(String(entry.get("save_id", "")))):
			continue
		entries.append(entry)
	entries.sort_custom(_sort_save_meta_newest_first)
	return entries


func _serialize_save_index_entries(entries: Array[Dictionary]) -> Array[Dictionary]:
	return _save_serializer.serialize_save_index_entries(entries)


func _deserialize_save_index_entry(raw_entry: Dictionary) -> Dictionary:
	return _save_serializer.deserialize_save_index_entry(raw_entry)


func _encode_save_index_string(value: String) -> String:
	if value.is_empty():
		return ""
	return Marshalls.raw_to_base64(value.to_utf8_buffer())


func _decode_save_index_string(value: String) -> String:
	if value.is_empty():
		return ""
	return Marshalls.base64_to_raw(value).get_string_from_utf8()


func _rebuild_save_index_entries_from_save_files() -> Array[Dictionary]:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(SAVE_DIRECTORY)):
		return []

	var save_dir := DirAccess.open(SAVE_DIRECTORY)
	if save_dir == null:
		return []

	var rebuilt_by_id: Dictionary = {}
	save_dir.list_dir_begin()
	while true:
		var file_name := save_dir.get_next()
		if file_name.is_empty():
			break
		if file_name == "." or file_name == ".." or save_dir.current_is_dir():
			continue
		if not file_name.ends_with(".dat") or file_name == "index.dat":
			continue
		var save_path := "%s/%s" % [SAVE_DIRECTORY, file_name]
		var read_result := _read_save_payload(save_path, false)
		if int(read_result.get("error", ERR_INVALID_DATA)) != OK:
			continue
		var payload_variant = read_result.get("payload", {})
		if typeof(payload_variant) != TYPE_DICTIONARY:
			continue
		var payload: Dictionary = payload_variant
		var save_meta := _extract_save_meta_from_payload(payload, file_name.trim_suffix(".dat"))
		if save_meta.is_empty():
			continue
		rebuilt_by_id[String(save_meta.get("save_id", ""))] = save_meta
	save_dir.list_dir_end()

	var rebuilt_entries: Array[Dictionary] = []
	for save_meta in rebuilt_by_id.values():
		rebuilt_entries.append(save_meta)
	rebuilt_entries.sort_custom(_sort_save_meta_newest_first)
	return rebuilt_entries


func _merge_save_index_entries(primary_entries: Array[Dictionary], fallback_entries: Array[Dictionary]) -> Array[Dictionary]:
	return _save_serializer.merge_save_index_entries(primary_entries, fallback_entries)


func _extract_save_meta_from_payload(payload: Dictionary, fallback_save_id: String = "") -> Dictionary:
	return _save_serializer.extract_save_meta_from_payload(payload, fallback_save_id)


func _upsert_save_meta(entries: Array[Dictionary], save_meta: Dictionary) -> Array[Dictionary]:
	return _save_serializer.upsert_save_meta(entries, save_meta)


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
	return _save_serializer.normalize_save_meta(raw_meta)


func _read_vector2i(value: Variant, fallback: Vector2i = Vector2i.ZERO) -> Vector2i:
	return _save_serializer.read_vector2i(value, fallback)


func _sort_save_meta_newest_first(a: Dictionary, b: Dictionary) -> bool:
	return _save_serializer.sort_save_meta_newest_first(a, b)


func _remove_directory_recursive(virtual_path: String) -> int:
	var absolute_path := ProjectSettings.globalize_path(virtual_path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		return OK

	var dir := DirAccess.open(virtual_path)
	if dir == null:
		var open_error := DirAccess.get_open_error()
		_push_session_error("session.cleanup.open_directory_failed", "Failed to open directory %s for cleanup. Error: %s" % [virtual_path, open_error], {
			"virtual_path": virtual_path,
			"open_error": open_error,
		})
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


func _apply_character_creation_payload_to_main_character(payload: Dictionary) -> void:
	if payload == null or payload.is_empty():
		return
	if _party_state == null:
		return

	var main_member_id: StringName = _party_state.get_resolved_main_character_member_id()
	if main_member_id == &"":
		push_warning("GameSession: no resolvable main character; skipping character creation override.")
		return

	var member_state = _party_state.get_member_state(main_member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		push_warning("GameSession: main character %s missing progression; skipping character creation override." % String(main_member_id))
		return

	var display_name := String(payload.get("display_name", "")).strip_edges()
	if not display_name.is_empty():
		member_state.display_name = display_name
		member_state.progression.display_name = display_name

	var base_attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		if payload.has(String(attribute_id)):
			base_attributes.set_attribute_value(attribute_id, int(payload[String(attribute_id)]))

	var reroll_count_value: Variant = payload.get("reroll_count", 0)
	var attribute_service = ATTRIBUTE_SERVICE_SCRIPT.new()
	attribute_service.setup(member_state.progression)
	var creation_service = CHARACTER_CREATION_SERVICE_SCRIPT.new()
	var baked: bool = creation_service.bake_hidden_luck_at_birth(attribute_service, reroll_count_value)
	if not baked:
		push_warning("GameSession: failed to bake hidden_luck_at_birth for main character %s." % String(main_member_id))


func _create_default_party_state():
	var party_state = PARTY_STATE_SCRIPT.new()
	party_state.gold = 180

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
	party_state.main_character_member_id = &"player_sword_01"
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
	_grant_random_starting_book_skill(progression)
	progression.sync_active_core_skill_ids()

	member_state.progression = progression
	return member_state


func _grant_random_starting_book_skill(progression) -> void:
	if progression == null or _skill_defs.is_empty():
		return

	var eligible_skill_ids: Array[StringName] = []
	for skill_key in ProgressionDataUtils.sorted_string_keys(_skill_defs):
		var skill_id := StringName(skill_key)
		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if not _is_random_start_book_skill_candidate(skill_def, progression):
			continue
		eligible_skill_ids.append(skill_id)

	if eligible_skill_ids.is_empty():
		return

	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var selected_skill_id: StringName = eligible_skill_ids[rng.randi_range(0, eligible_skill_ids.size() - 1)]
	var selected_skill_def := _skill_defs.get(selected_skill_id) as SkillDef
	if selected_skill_def == null:
		return

	var skill_progress = progression.get_skill_progress(selected_skill_id)
	if skill_progress == null:
		skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.new()
		skill_progress.skill_id = selected_skill_id

	skill_progress.is_learned = true
	skill_progress.skill_level = _resolve_random_start_skill_initial_level(selected_skill_def)
	skill_progress.current_mastery = 0
	skill_progress.total_mastery_earned = 0
	progression.set_skill_progress(skill_progress)


func _is_random_start_book_skill_candidate(skill_def: SkillDef, progression) -> bool:
	if skill_def == null or skill_def.skill_id == &"":
		return false
	if skill_def.learn_source != &"book":
		return false
	if skill_def.unlock_mode == &"composite_upgrade":
		return false
	if not skill_def.learn_requirements.is_empty() \
		or not skill_def.knowledge_requirements.is_empty() \
		or not skill_def.skill_level_requirements.is_empty() \
		or not skill_def.attribute_requirements.is_empty() \
		or not skill_def.achievement_requirements.is_empty():
		return false
	var learned_progress = progression.get_skill_progress(skill_def.skill_id)
	return learned_progress == null or not learned_progress.is_learned


func _resolve_random_start_skill_initial_level(skill_def: SkillDef) -> int:
	if skill_def == null:
		return 0
	var mapped_level := int(RANDOM_START_SKILL_LEVEL_BY_TIER.get(_resolve_random_start_skill_tier(skill_def), 0))
	var max_initial_level := maxi(skill_def.max_level, 0)
	if skill_def.non_core_max_level > 0:
		max_initial_level = mini(max_initial_level, int(skill_def.non_core_max_level))
	return clampi(mapped_level, 0, max_initial_level)


func _resolve_random_start_skill_tier(skill_def: SkillDef) -> StringName:
	if skill_def == null:
		return RANDOM_START_SKILL_TIER_BASIC

	var description := String(skill_def.description)
	if _description_contains_any_keyword(description, RANDOM_START_SKILL_KEYWORDS_ULTIMATE):
		return RANDOM_START_SKILL_TIER_ULTIMATE
	if _description_contains_any_keyword(description, RANDOM_START_SKILL_KEYWORDS_ADVANCED):
		return RANDOM_START_SKILL_TIER_ADVANCED
	if _description_contains_any_keyword(description, RANDOM_START_SKILL_KEYWORDS_INTERMEDIATE):
		return RANDOM_START_SKILL_TIER_INTERMEDIATE
	if _description_contains_any_keyword(description, RANDOM_START_SKILL_KEYWORDS_BASIC):
		return RANDOM_START_SKILL_TIER_BASIC

	var tier_score := _build_random_start_skill_tier_score(skill_def)
	if tier_score >= 14:
		return RANDOM_START_SKILL_TIER_ULTIMATE
	if tier_score >= 9:
		return RANDOM_START_SKILL_TIER_ADVANCED
	if tier_score >= 6:
		return RANDOM_START_SKILL_TIER_INTERMEDIATE
	return RANDOM_START_SKILL_TIER_BASIC


func _description_contains_any_keyword(description: String, keywords: Array) -> bool:
	for keyword in keywords:
		if description.contains(keyword):
			return true
	return false


func _build_random_start_skill_tier_score(skill_def: SkillDef) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0

	var combat_profile = skill_def.combat_profile
	var score := 0
	score += int(combat_profile.ap_cost) * 2
	score += int(combat_profile.mp_cost)
	score += int(combat_profile.stamina_cost)
	score += int(combat_profile.aura_cost) * 2
	score += maxi(int(combat_profile.cooldown_tu) / 5 - 1, 0)
	if combat_profile.target_mode == &"ground":
		score += 1
	if combat_profile.area_pattern != &"" and combat_profile.area_pattern != &"single":
		score += 1
	if skill_def.tags.has(&"aoe"):
		score += 1
	if skill_def.tags.has(&"finisher"):
		score += 2
	if skill_def.unlock_mode == &"composite_upgrade":
		score += 2
	return score


func _normalize_party_state(party_state):
	return _save_serializer.normalize_party_state(party_state)


func _normalize_world_data(world_data: Dictionary) -> Dictionary:
	return _save_serializer.normalize_world_data(world_data)


func _serialize_world_data(world_data: Dictionary) -> Dictionary:
	return _save_serializer.serialize_world_data(world_data)


func _rotate_log_session() -> void:
	if _log_service != null:
		_log_service.start_new_session()


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
	_quest_defs = _progression_content_registry.get_quest_defs()


func _refresh_item_content() -> void:
	if _item_content_registry == null:
		return

	_item_defs = _item_content_registry.get_item_defs().duplicate()
	if _skill_book_item_factory != null:
		var generated_skill_book_defs := _skill_book_item_factory.build_generated_item_defs(_skill_defs, _item_defs)
		for item_id in generated_skill_book_defs.keys():
			_item_defs[item_id] = generated_skill_book_defs[item_id]


func _refresh_recipe_content() -> void:
	if _recipe_content_registry == null:
		return

	_recipe_content_registry.setup(_item_defs)
	_recipe_defs = _recipe_content_registry.get_recipe_defs().duplicate()


func _refresh_enemy_content() -> void:
	if _enemy_content_registry == null:
		return

	_enemy_templates = _enemy_content_registry.get_enemy_templates()
	_enemy_ai_brains = _enemy_content_registry.get_enemy_ai_brains()
	_wild_encounter_rosters = _enemy_content_registry.get_wild_encounter_rosters()


func _refresh_content_validation_snapshot() -> void:
	var domain_snapshots := {
		"progression": _build_content_validation_domain_snapshot(_progression_content_registry),
		"item": _build_content_validation_domain_snapshot(_item_content_registry),
		"recipe": _build_content_validation_domain_snapshot(_recipe_content_registry),
		"enemy": _build_content_validation_domain_snapshot(_enemy_content_registry),
	}
	var error_count := 0
	for domain_id in CONTENT_VALIDATION_DOMAIN_ORDER:
		error_count += int((domain_snapshots.get(domain_id, {}) as Dictionary).get("error_count", 0))
	_content_validation_snapshot = {
		"ok": error_count == 0,
		"error_count": error_count,
		"domain_order": CONTENT_VALIDATION_DOMAIN_ORDER.duplicate(),
		"domains": domain_snapshots,
	}


func _build_content_validation_domain_snapshot(registry) -> Dictionary:
	var errors: Array[String] = []
	if registry != null and registry.has_method("validate"):
		for validation_error in registry.validate():
			errors.append(String(validation_error))
	return {
		"ok": errors.is_empty(),
		"error_count": errors.size(),
		"errors": errors,
	}


func _report_content_validation_errors() -> void:
	var domains_variant = _content_validation_snapshot.get("domains", {})
	if domains_variant is not Dictionary:
		return
	var domains := domains_variant as Dictionary
	for domain_id in CONTENT_VALIDATION_DOMAIN_ORDER:
		var domain_snapshot_variant = domains.get(domain_id, {})
		if domain_snapshot_variant is not Dictionary:
			continue
		var domain_snapshot := domain_snapshot_variant as Dictionary
		for validation_error_variant in domain_snapshot.get("errors", []):
			_report_content_validation_error(domain_id, String(validation_error_variant))


func _report_content_validation_error(domain_id: String, validation_error: String) -> void:
	match domain_id:
		"progression":
			_push_session_error("session.content.progression_validation_failed", "Progression content error: %s" % validation_error)
		"item":
			_push_session_error("session.content.item_validation_failed", "Item content error: %s" % validation_error)
		"recipe":
			_push_session_error("session.content.recipe_validation_failed", "Recipe content error: %s" % validation_error)
		"enemy":
			_push_session_error("session.content.enemy_validation_failed", "Enemy content error: %s" % validation_error)


func _log_session_info(event_id: String, message: String, context: Dictionary = {}) -> void:
	log_event("info", "session", event_id, message, context)


func _push_session_error(event_id: String, message: String, context: Dictionary = {}) -> void:
	push_error(message)
	log_event("error", "session", event_id, message, context)
