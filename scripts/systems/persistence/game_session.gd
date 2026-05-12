## 文件说明：该脚本属于游戏会话相关的业务脚本，集中维护激活存档唯一标识、激活存档路径、激活存档元信息等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

extends Node

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_grid_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_spawn_system.gd")
const WORLD_MAP_GENERATION_CONFIG_SCRIPT = preload("res://scripts/utils/world_map_generation_config.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const UNIT_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_progress.gd")
const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")
const UNIT_PROFESSION_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_profession_progress.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const RECIPE_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/recipe_content_registry.gd")
const SKILL_BOOK_ITEM_FACTORY_SCRIPT = preload("res://scripts/player/warehouse/skill_book_item_factory.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")
const PROGRESSION_SERIALIZATION_SCRIPT = preload("res://scripts/systems/persistence/progression_serialization.gd")
const SAVE_SERIALIZER_SCRIPT = preload("res://scripts/systems/persistence/save_serializer.gd")
const FILE_IO_COORDINATOR_SCRIPT = preload("res://scripts/systems/persistence/file_io_coordinator.gd")
const GAME_LOG_SERVICE_SCRIPT = preload("res://scripts/systems/persistence/game_log_service.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const WORLD_MAP_CONTENT_VALIDATOR_SCRIPT = preload("res://scripts/utils/world_map_content_validator.gd")
const BATTLE_SPECIAL_PROFILE_REGISTRY_SCRIPT = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
const CHARACTER_CREATION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/character_creation_service.gd")
const PROGRESSION_SERVICE_SCRIPT = preload("res://scripts/systems/progression/progression_service.gd")
const RACIAL_SKILL_GRANT_SERVICE_SCRIPT = preload("res://scripts/systems/progression/racial_skill_grant_service.gd")
const SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT = preload("res://scripts/systems/progression/skill_effective_max_level_rules.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const BODY_SIZE_RULES_SCRIPT = preload("res://scripts/systems/progression/body_size_rules.gd")
const BodySizeRules = BODY_SIZE_RULES_SCRIPT

const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY
const SAVE_VERSION := 7
const SAVE_INDEX_VERSION := 3
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD
const MAX_ACTIVE_MEMBER_COUNT := 4
const CONTENT_VALIDATION_DOMAIN_ORDER := ["progression", "battle_special_profile", "item", "recipe", "enemy", "world"]
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
const WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY := "next_equipment_instance_serial"
const SAVE_DIRTY_SCOPE_WORLD_DATA: StringName = &"world_data"
const SAVE_DIRTY_SCOPE_PLAYER_COORD: StringName = &"player_coord"
const SAVE_DIRTY_SCOPE_PLAYER_FACTION_ID: StringName = &"player_faction_id"
const SAVE_DIRTY_SCOPE_PARTY_STATE: StringName = &"party_state"
const SAVE_DIRTY_SCOPE_POST_DECODE_REPAIR: StringName = &"post_decode_repair"
const SAVE_DIRTY_SCOPE_BATTLE_LOCKED_SAVE: StringName = &"battle_locked_save"
const STARTING_MELEE_WEAPON_ITEM_ID: StringName = &"steel_longsword"
const STARTING_ARCHER_WEAPON_ITEM_ID: StringName = &"ash_shortbow"
const STARTING_CROSSBOW_WEAPON_ITEM_ID: StringName = &"militia_light_crossbow"
const STARTING_MAGE_WEAPON_ITEM_ID: StringName = &"oak_quarterstaff"
const STARTING_PRIEST_WEAPON_ITEM_ID: StringName = &"watchman_mace"

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
## 字段说明：记录运行时状态已有未提交变更，由统一 commit_runtime_state() 负责落盘。
var _runtime_save_dirty := false
## 字段说明：记录未提交变更覆盖的状态域，便于调试、UI 状态和失败后重试。
var _runtime_save_dirty_scopes: Array[StringName] = []
## 字段说明：记录最近一次统一保存失败的错误码；OK 表示当前没有保存错误待处理。
var _last_save_error := OK
## 字段说明：记录最近一次统一保存失败的原因标签，方便运行时日志和测试定位。
var _last_save_error_reason: StringName = &""
## 字段说明：标记解码后是否有 runtime 修正需要写回当前存档。
var _post_decode_save_pending := false
## 字段说明：记录解码后写回原因，便于调试追踪。
var _post_decode_save_reasons: Array[StringName] = []

## 字段说明：记录成长内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _progression_content_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录物品内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _item_content_registry = ITEM_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录配方内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _recipe_content_registry = RECIPE_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录敌方内容注册表，会参与运行时状态流转、系统协作和存档恢复。
var _enemy_content_registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
## 字段说明：记录特殊战斗技能 profile 注册表，只负责 manifest/profile 静态校验与 runtime snapshot 投影。
var _battle_special_profile_registry = BATTLE_SPECIAL_PROFILE_REGISTRY_SCRIPT.new()
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
var _world_content_validator = WORLD_MAP_CONTENT_VALIDATOR_SCRIPT.new()
var _save_index_entries_cache: Array[Dictionary] = []
var _save_index_cache_valid := false
var _save_index_cache_signature: Dictionary = {}


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
	# Refresh order is intentional: items can be generated from skills, recipes read items,
	# and world validation reads enemy rosters after enemy content is cached.
	_refresh_progression_content()
	_refresh_battle_special_profiles()
	_refresh_item_content()
	_refresh_recipe_content()
	_refresh_enemy_content()
	_refresh_content_validation_snapshot()
	_report_content_validation_errors()


func ensure_world_ready(generation_config_path: String) -> int:
	var content_validation_error := _require_content_validation_for_runtime(&"ensure_world_ready")
	if content_validation_error != OK:
		return content_validation_error
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
	var content_validation_error := _require_content_validation_for_runtime(&"create_new_save")
	if content_validation_error != OK:
		return content_validation_error
	var previous_runtime_state := _capture_runtime_state()
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
		_restore_runtime_state(previous_runtime_state)
		return prepare_error

	_apply_character_creation_payload_to_main_character(character_creation_payload)

	var timestamp := int(Time.get_unix_time_from_system())
	var save_id := _generate_unique_save_id(timestamp)
	if save_id.is_empty():
		_push_session_error("session.save.create.allocate_id_failed", "GameSession failed to allocate a unique save id.")
		_restore_runtime_state(previous_runtime_state)
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
	else:
		_restore_runtime_state(previous_runtime_state)
	return persist_error


func list_save_slots() -> Array[Dictionary]:
	return _load_save_index_entries()


func peek_save_slots() -> Array[Dictionary]:
	return _peek_save_index_entries_read_only()


func load_save(save_id: String) -> int:
	if not _save_serializer.is_valid_save_id_token(save_id):
		return ERR_INVALID_PARAMETER
	var content_validation_error := _require_content_validation_for_runtime(&"load_save")
	if content_validation_error != OK:
		return content_validation_error

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

	if not payload.has("generation_config_path"):
		_push_session_error("session.save.load.missing_generation_config", "Save slot %s is missing generation_config_path." % save_id, {
			"save_id": save_id,
			"save_path": save_path,
		})
		return ERR_INVALID_DATA
	var generation_config_path := String(payload.get("generation_config_path", "")).strip_edges()
	if generation_config_path.is_empty():
		_push_session_error("session.save.load.missing_generation_config", "Save slot %s is missing generation_config_path." % save_id, {
			"save_id": save_id,
			"save_path": save_path,
		})
		return ERR_INVALID_DATA

	var generation_config = _load_generation_config(generation_config_path)
	if generation_config == null:
		return ERR_CANT_OPEN

	var previous_runtime_state := _capture_runtime_state()
	var load_error := _load_current_payload(payload, generation_config_path, generation_config, save_meta)
	if load_error == OK:
		_rotate_log_session()
		_log_session_info("session.save.load.ok", "已加载存档。", {
			"save_id": save_id,
			"save_path": save_path,
			"generation_config_path": generation_config_path,
		})
	else:
		_restore_runtime_state(previous_runtime_state)
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


func is_content_validation_ok() -> bool:
	return bool(_content_validation_snapshot.get("ok", false))


func log_event(level: String, domain: String, event_id: String, message: String, context: Dictionary = {}) -> Dictionary:
	return _log_service.append_entry(level, domain, event_id, message, context) if _log_service != null else {}


func get_generation_config():
	return _generation_config


func get_generation_config_path() -> String:
	return _generation_config_path


func get_world_data() -> Dictionary:
	return _world_data


func allocate_equipment_instance_id() -> StringName:
	if _world_data == null or not _world_data.has(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY):
		return &""
	var used_ids := _collect_persistent_equipment_instance_ids()
	var serial := int(_world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY, 0))
	if serial < 1:
		return &""
	while true:
		var candidate := EQUIPMENT_INSTANCE_STATE_SCRIPT.format_instance_id(serial)
		serial += 1
		_world_data[WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY] = serial
		if not used_ids.has(String(candidate)):
			_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_WORLD_DATA)
			return candidate
	return &""


func set_world_data(world_data: Dictionary) -> int:
	var normalized_world_data := _normalize_world_data(world_data)
	if normalized_world_data.is_empty():
		return ERR_INVALID_DATA
	_world_data = normalized_world_data
	_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_WORLD_DATA)
	return OK


func get_player_coord() -> Vector2i:
	return _player_coord


func set_player_coord(coord: Vector2i) -> int:
	_player_coord = coord
	_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_PLAYER_COORD)
	return OK


func get_player_faction_id() -> String:
	return _player_faction_id


func set_player_faction_id(faction_id: String) -> int:
	_player_faction_id = faction_id
	_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_PLAYER_FACTION_ID)
	return OK


func get_party_state():
	return _party_state


func set_party_state(party_state) -> int:
	_party_state = _normalize_party_state(party_state)
	_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_PARTY_STATE)
	return OK


func set_battle_save_lock(enabled: bool) -> void:
	_battle_save_lock_enabled = enabled


func is_battle_save_locked() -> bool:
	return _battle_save_lock_enabled


func has_pending_save() -> bool:
	return _runtime_save_dirty or _battle_save_dirty or _post_decode_save_pending


func discard_pending_save() -> void:
	_battle_save_dirty = false
	_runtime_save_dirty = false
	_runtime_save_dirty_scopes.clear()
	_post_decode_save_pending = false
	_post_decode_save_reasons.clear()


func get_save_status() -> Dictionary:
	return {
		"has_pending_save": has_pending_save(),
		"dirty_scopes": _runtime_save_dirty_scopes.duplicate(),
		"battle_save_locked": _battle_save_lock_enabled,
		"last_error": _last_save_error,
		"last_error_reason": _last_save_error_reason,
		"post_decode_save_pending": _post_decode_save_pending,
		"post_decode_save_reasons": _post_decode_save_reasons.duplicate(),
	}


func _mark_runtime_state_dirty(scope: StringName) -> void:
	_runtime_save_dirty = true
	if scope == &"" or _runtime_save_dirty_scopes.has(scope):
		return
	_runtime_save_dirty_scopes.append(scope)


func _clear_runtime_save_dirty() -> void:
	_battle_save_dirty = false
	_runtime_save_dirty = false
	_runtime_save_dirty_scopes.clear()
	_post_decode_save_pending = false
	_post_decode_save_reasons.clear()


func _record_save_error(error_code: int, reason: StringName) -> void:
	_last_save_error = error_code
	_last_save_error_reason = reason


func _clear_last_save_error() -> void:
	_last_save_error = OK
	_last_save_error_reason = &""


func queue_post_decode_save(reason: StringName) -> void:
	_post_decode_save_pending = true
	_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_POST_DECODE_REPAIR)
	if reason == &"" or _post_decode_save_reasons.has(reason):
		return
	_post_decode_save_reasons.append(reason)


func get_party_member_state(member_id: StringName):
	if _party_state == null:
		return null
	return _party_state.get_member_state(member_id)


func get_leader_member_state():
	if _party_state == null:
		return null
	return _party_state.get_member_state(_party_state.leader_member_id)


func _collect_persistent_equipment_instance_ids() -> Dictionary:
	var used_ids: Dictionary = {}
	if _party_state == null:
		return used_ids
	_collect_warehouse_equipment_instance_ids(_party_state.warehouse_state, used_ids)
	for member_state in _party_state.member_states.values():
		if member_state == null:
			continue
		var equipment_state = member_state.equipment_state
		if equipment_state == null or not (equipment_state is Object and equipment_state.has_method("get_entry_slot_ids")):
			continue
		for entry_slot_id in equipment_state.get_entry_slot_ids():
			var instance_id := ProgressionDataUtils.to_string_name(equipment_state.get_equipped_instance_id(entry_slot_id))
			if instance_id == &"":
				continue
			used_ids[String(instance_id)] = true
	return used_ids


func _collect_warehouse_equipment_instance_ids(warehouse_state, used_ids: Dictionary) -> void:
	if warehouse_state == null or not (warehouse_state is Object and warehouse_state.has_method("get_non_empty_instances")):
		return
	for instance in warehouse_state.get_non_empty_instances():
		if instance == null:
			continue
		var instance_id := ProgressionDataUtils.to_string_name(instance.instance_id)
		if instance_id == &"":
			continue
		used_ids[String(instance_id)] = true


func get_progression_content_registry():
	return _progression_content_registry


func get_progression_content_bundle() -> Dictionary:
	if _progression_content_registry == null:
		return {}
	return _duplicate_content_bundle(_progression_content_registry.get_bundle())


func get_skill_defs() -> Dictionary:
	return _skill_defs.duplicate()


func get_battle_special_profile_registry_snapshot() -> Dictionary:
	return _battle_special_profile_registry.get_snapshot() if _battle_special_profile_registry != null else {}


func get_profession_defs() -> Dictionary:
	return _profession_defs.duplicate()


func get_achievement_defs() -> Dictionary:
	return _achievement_defs.duplicate()


func get_quest_defs() -> Dictionary:
	return _quest_defs.duplicate()


func get_item_defs() -> Dictionary:
	return _item_defs.duplicate()


func get_recipe_defs() -> Dictionary:
	return _recipe_defs.duplicate()


func get_enemy_templates() -> Dictionary:
	return _enemy_templates.duplicate()


func get_enemy_ai_brains() -> Dictionary:
	return _enemy_ai_brains.duplicate()


func get_wild_encounter_rosters() -> Dictionary:
	return _wild_encounter_rosters.duplicate()


func install_test_content_def(domain_id: StringName, content_key: Variant, content_def: Variant) -> int:
	if content_def == null:
		return ERR_INVALID_PARAMETER
	if content_key is not String and content_key is not StringName:
		return ERR_INVALID_PARAMETER
	if String(content_key).is_empty():
		return ERR_INVALID_PARAMETER
	match domain_id:
		&"skill":
			_skill_defs[content_key] = content_def
			_refresh_battle_special_profiles()
		&"profession":
			_profession_defs[content_key] = content_def
		&"achievement":
			_achievement_defs[content_key] = content_def
		&"quest":
			_quest_defs[content_key] = content_def
		&"item":
			_item_defs[content_key] = content_def
		&"recipe":
			_recipe_defs[content_key] = content_def
		&"enemy_template":
			_enemy_templates[content_key] = content_def
		&"enemy_ai_brain":
			_enemy_ai_brains[content_key] = content_def
		&"wild_encounter_roster":
			_wild_encounter_rosters[content_key] = content_def
		_:
			return ERR_INVALID_PARAMETER
	return OK


func save_world_state() -> int:
	return save_game_state()


func save_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _battle_save_lock_enabled:
		_battle_save_dirty = true
		_mark_runtime_state_dirty(SAVE_DIRTY_SCOPE_BATTLE_LOCKED_SAVE)
		return OK

	return commit_runtime_state(&"save_game_state")


func commit_runtime_state(reason: StringName = &"runtime") -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _battle_save_lock_enabled:
		_record_save_error(ERR_BUSY, reason)
		return ERR_BUSY

	var persist_error := _persist_game_state()
	if persist_error != OK:
		_record_save_error(persist_error, reason)
		return persist_error

	_clear_runtime_save_dirty()
	_clear_last_save_error()
	return OK


func flush_game_state() -> int:
	if not _has_active_world:
		return ERR_UNCONFIGURED
	if _battle_save_lock_enabled:
		_record_save_error(ERR_BUSY, &"flush_game_state")
		return ERR_BUSY
	if not has_pending_save():
		return OK

	return commit_runtime_state(&"flush_game_state")


func clear_persisted_world() -> int:
	return clear_persisted_game()


func clear_persisted_game() -> int:
	_reset_runtime_state()
	_invalidate_save_index_cache()

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
	if has_pending_save():
		if _battle_save_lock_enabled:
			_record_save_error(ERR_BUSY, &"unload_active_world")
			_push_session_error("session.runtime.unload.save_locked", "GameSession cannot unload active world while battle save lock is enabled.")
			return
		var unload_save_error := commit_runtime_state(&"unload_active_world")
		if unload_save_error != OK:
			_push_session_error(
				"session.runtime.unload.commit_failed",
				"GameSession failed to commit pending save before unloading active world.",
				{"error": unload_save_error}
			)
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

	var attempted_candidate := false
	for save_meta in _load_save_index_entries():
		if String(save_meta.get("generation_config_path", "")) != generation_config_path:
			continue
		attempted_candidate = true
		var candidate_save_id := String(save_meta.get("save_id", ""))
		if load_save(candidate_save_id) == OK:
			return true
		_log_session_info("session.save.autoload.skip_bad_candidate", "自动载入跳过坏存档 %s。" % candidate_save_id, {
			"save_id": candidate_save_id,
			"generation_config_path": generation_config_path,
		})
	return false if attempted_candidate else false


func _prepare_new_world(generation_config_path: String, generation_config: WorldMapGenerationConfig) -> int:
	if generation_config == null:
		return ERR_INVALID_PARAMETER

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
	_refresh_party_body_sizes_from_identity(_party_state)
	_backfill_racial_granted_skills(_party_state)
	_has_active_world = true
	_battle_save_lock_enabled = false
	_clear_runtime_save_dirty()
	_clear_last_save_error()
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

	var payload_write_error := _write_save_payload_atomically(_active_save_path, _build_save_payload(now))
	if payload_write_error != OK:
		return payload_write_error

	var index_error := _write_save_index(_upsert_save_meta(_load_save_index_entries(), _active_save_meta))
	if index_error != OK:
		return index_error

	_battle_save_dirty = false
	return OK


func _load_current_payload(
	payload: Dictionary,
	generation_config_path: String,
	generation_config,
	save_meta: Dictionary
) -> int:
	var decode_result := _save_serializer.decode_payload(payload, generation_config_path, generation_config, save_meta)
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
	var body_size_changed := _refresh_party_body_sizes_from_identity(_party_state)
	var racial_grants_changed := false
	racial_grants_changed = _revoke_orphan_racial_skills(_party_state) or racial_grants_changed
	racial_grants_changed = _backfill_racial_granted_skills(_party_state) or racial_grants_changed
	if body_size_changed:
		queue_post_decode_save(&"identity_body_size")
	if racial_grants_changed:
		queue_post_decode_save(&"racial_granted_skills")
	return OK


func _flush_post_decode_save() -> int:
	if not _post_decode_save_pending:
		return OK
	return commit_runtime_state(&"post_decode_repair")


func _refresh_party_body_sizes_from_identity(party_state) -> bool:
	if party_state == null:
		return false
	var changed := false
	for member_state in party_state.member_states.values():
		changed = _refresh_member_body_size_from_identity(member_state) or changed
	return changed


func _backfill_racial_granted_skills(party_state) -> bool:
	return RACIAL_SKILL_GRANT_SERVICE_SCRIPT.backfill_party(
		party_state,
		get_progression_content_bundle(),
		_skill_defs,
		_profession_defs
	)


func _revoke_orphan_racial_skills(party_state) -> bool:
	return RACIAL_SKILL_GRANT_SERVICE_SCRIPT.revoke_orphan_party(
		party_state,
		get_progression_content_bundle(),
		_skill_defs,
		_profession_defs
	)


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
	var existing_save_ids: Dictionary = {}
	for entry in _load_save_index_entries():
		existing_save_ids[String(entry.get("save_id", ""))] = true
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
		if not existing_save_ids.has(save_id) and not FileAccess.file_exists(_build_save_file_path(save_id)):
			return save_id
	return ""


func _load_generation_config(generation_config_path: String) -> WorldMapGenerationConfig:
	var generation_config = load(generation_config_path)
	if generation_config == null:
		_push_session_error("session.config.load_failed", "GameSession failed to load config from %s." % generation_config_path, {
			"generation_config_path": generation_config_path,
		})
		return null
	if generation_config.get_script() != WORLD_MAP_GENERATION_CONFIG_SCRIPT:
		_push_session_error("session.config.invalid_type", "GameSession generation config %s must use WorldMapGenerationConfig." % generation_config_path, {
			"generation_config_path": generation_config_path,
			"actual_script": str(generation_config.get_script()),
		})
		return null
	return generation_config as WorldMapGenerationConfig


func _read_save_payload(save_path: String, emit_errors: bool = true) -> Dictionary:
	var recovery_error := FILE_IO_COORDINATOR_SCRIPT.recover_replace_target(
		save_path,
		SAVE_FILE_COMPRESSION_MODE,
		"session.save.read",
		"save",
		Callable(self, "_push_session_error")
	)
	if recovery_error != OK and recovery_error != ERR_DOES_NOT_EXIST:
		return {"error": recovery_error}
	if not FileAccess.file_exists(save_path):
		if emit_errors:
			_push_session_error("session.save.read.missing_file", "GameSession could not find persisted save %s." % save_path, {
				"save_path": save_path,
			})
		return {"error": ERR_DOES_NOT_EXIST}

	var save_file := FileAccess.open_compressed(save_path, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
	if save_file == null:
		var open_error := FileAccess.get_open_error()
		if emit_errors:
			_push_session_error("session.save.read.open_failed", "Failed to open persisted save %s. Error: %s" % [save_path, open_error], {
				"save_path": save_path,
				"open_error": open_error,
			})
		return {"error": open_error}

	var save_size := int(save_file.get_length())
	# Compressed Variant payloads shorter than Godot's 8-byte wrapper/header are
	# always truncated; skip get_var() so corrupt files do not emit engine errors.
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
	if not _save_serializer.is_valid_save_id_token(save_id):
		return ""
	return "%s/%s.dat" % [SAVE_DIRECTORY, save_id]


func _write_compressed_variant_atomically(virtual_path: String, payload: Variant, error_event_prefix: String, label: String) -> int:
	return FILE_IO_COORDINATOR_SCRIPT.write_compressed_variant_atomically(
		virtual_path,
		payload,
		SAVE_FILE_COMPRESSION_MODE,
		error_event_prefix,
		label,
		Callable(self, "_push_session_error")
	)


func _write_save_payload_atomically(save_path: String, payload: Dictionary) -> int:
	return _write_compressed_variant_atomically(save_path, payload, "session.save.persist", "save")


func _replace_file_atomically(source_path: String, target_path: String, error_event_prefix: String, label: String) -> int:
	return FILE_IO_COORDINATOR_SCRIPT.replace_file_atomically(
		source_path,
		target_path,
		error_event_prefix,
		label,
		Callable(self, "_push_session_error")
	)


func _rename_file(from_virtual_path: String, to_virtual_path: String) -> int:
	return FILE_IO_COORDINATOR_SCRIPT.rename_file(from_virtual_path, to_virtual_path)


func _remove_file_if_exists(virtual_path: String) -> int:
	return FILE_IO_COORDINATOR_SCRIPT.remove_file_if_exists(virtual_path)


func _load_save_index_entries() -> Array[Dictionary]:
	if _is_save_index_cache_current():
		return _duplicate_save_index_entries(_save_index_entries_cache)

	var should_rewrite_index := false
	var raw_entries: Array = []
	var index_recovery_error := FILE_IO_COORDINATOR_SCRIPT.recover_replace_target(
		SAVE_INDEX_PATH,
		SAVE_FILE_COMPRESSION_MODE,
		"session.save.index",
		"save index",
		Callable(self, "_push_session_error")
	)
	if index_recovery_error != OK and index_recovery_error != ERR_DOES_NOT_EXIST:
		should_rewrite_index = true
	if not FileAccess.file_exists(SAVE_INDEX_PATH):
		should_rewrite_index = true
	else:
		var index_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
		if index_file == null:
			should_rewrite_index = true
		else:
			var raw_payload = _read_save_index_payload(index_file)
			index_file.close()
			if typeof(raw_payload) == TYPE_DICTIONARY:
				var raw_payload_dict: Dictionary = raw_payload
				var index_version_variant: Variant = raw_payload_dict.get("version", null)
				if not _is_save_index_integer_value(index_version_variant) \
					or int(index_version_variant) != SAVE_INDEX_VERSION \
					or typeof(raw_payload_dict.get("saves", null)) != TYPE_ARRAY:
					should_rewrite_index = true
				else:
					raw_entries = raw_payload_dict.get("saves", [])
			else:
				should_rewrite_index = true

	var entries := _normalize_save_index_entries(raw_entries)
	var rebuilt_entries := _rebuild_save_index_entries_from_save_files()
	var merged_entries := _merge_save_index_entries(entries, rebuilt_entries)
	if should_rewrite_index or not _save_index_entries_match(entries, merged_entries):
		_write_save_index(merged_entries)
	else:
		_set_save_index_cache(merged_entries)
	return _duplicate_save_index_entries(merged_entries)


func _peek_save_index_entries_read_only() -> Array[Dictionary]:
	if _is_save_index_cache_current():
		return _duplicate_save_index_entries(_save_index_entries_cache)
	if not FileAccess.file_exists(SAVE_INDEX_PATH):
		return []

	var index_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
	if index_file == null:
		return []
	var raw_payload = _read_save_index_payload(index_file)
	index_file.close()
	if typeof(raw_payload) != TYPE_DICTIONARY:
		return []

	var raw_payload_dict: Dictionary = raw_payload
	var index_version_variant: Variant = raw_payload_dict.get("version", null)
	if not _is_save_index_integer_value(index_version_variant) \
		or int(index_version_variant) != SAVE_INDEX_VERSION \
		or typeof(raw_payload_dict.get("saves", null)) != TYPE_ARRAY:
		return []

	var entries := _normalize_save_index_entries(raw_payload_dict.get("saves", []))
	_set_save_index_cache(entries)
	return _duplicate_save_index_entries(entries)


func _write_save_index(entries: Array[Dictionary]) -> int:
	var ensure_dir_error := _ensure_save_directory()
	if ensure_dir_error != OK:
		return ensure_dir_error

	var normalized_entries := _normalize_save_index_entries(entries)
	var write_error := _write_compressed_variant_atomically(
		SAVE_INDEX_PATH,
		_build_save_index_payload(normalized_entries),
		"session.save.index",
		"save index"
	)
	_set_save_index_cache(normalized_entries)
	if write_error != OK:
		# Save files remain authoritative; the index is a rebuildable cache.
		return OK
	return OK


func _read_save_index_payload(index_file: FileAccess) -> Variant:
	return _save_serializer.read_save_index_payload(index_file)


func _is_save_index_cache_current() -> bool:
	if not _save_index_cache_valid:
		return false
	var current_signature := _get_save_index_file_signature()
	return bool(_save_index_cache_signature.get("exists", false)) == bool(current_signature.get("exists", false)) \
		and int(_save_index_cache_signature.get("modified_time", -1)) == int(current_signature.get("modified_time", -1)) \
		and int(_save_index_cache_signature.get("size", -1)) == int(current_signature.get("size", -1))


func _set_save_index_cache(entries: Array[Dictionary]) -> void:
	_save_index_entries_cache = _duplicate_save_index_entries(entries)
	_save_index_cache_valid = true
	_save_index_cache_signature = _get_save_index_file_signature()


func _invalidate_save_index_cache() -> void:
	_save_index_entries_cache.clear()
	_save_index_cache_valid = false
	_save_index_cache_signature = {}


func _get_save_index_file_signature() -> Dictionary:
	if not FileAccess.file_exists(SAVE_INDEX_PATH):
		return {
			"exists": false,
			"modified_time": -1,
			"size": -1,
		}

	var size := -1
	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	if index_file != null:
		size = int(index_file.get_length())
		index_file.close()
	return {
		"exists": true,
		"modified_time": int(FileAccess.get_modified_time(SAVE_INDEX_PATH)),
		"size": size,
	}


func _duplicate_save_index_entries(entries: Array[Dictionary]) -> Array[Dictionary]:
	var duplicated_entries: Array[Dictionary] = []
	for entry in entries:
		duplicated_entries.append(entry.duplicate(true))
	return duplicated_entries


func _duplicate_content_bundle(bundle: Dictionary) -> Dictionary:
	var duplicated_bundle: Dictionary = {}
	for key in bundle.keys():
		var value: Variant = bundle.get(key)
		if value is Dictionary:
			duplicated_bundle[key] = (value as Dictionary).duplicate()
		elif value is Array:
			duplicated_bundle[key] = (value as Array).duplicate()
		else:
			duplicated_bundle[key] = value
	return duplicated_bundle


func _save_index_entries_match(left_entries: Array[Dictionary], right_entries: Array[Dictionary]) -> bool:
	if left_entries.size() != right_entries.size():
		return false
	for index in range(left_entries.size()):
		if not _save_index_entry_matches(left_entries[index], right_entries[index]):
			return false
	return true


func _save_index_entry_matches(left_entry: Dictionary, right_entry: Dictionary) -> bool:
	for key in [
		"save_id",
		"display_name",
		"world_preset_id",
		"world_preset_name",
		"generation_config_path",
		"world_size_cells",
		"created_at_unix_time",
		"updated_at_unix_time",
	]:
		if left_entry.get(key, null) != right_entry.get(key, null):
			return false
	return true


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


func _build_save_index_payload(entries: Array[Dictionary]) -> Dictionary:
	return _save_serializer.build_save_index_payload(entries)


func _deserialize_save_index_entry(raw_entry: Dictionary) -> Dictionary:
	return _save_serializer.deserialize_save_index_entry(raw_entry)


func _is_save_index_integer_value(value: Variant) -> bool:
	return _save_serializer.is_save_index_integer_value(value)


func _rebuild_save_index_entries_from_save_files() -> Array[Dictionary]:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(SAVE_DIRECTORY)):
		return []

	var save_dir := DirAccess.open(SAVE_DIRECTORY)
	if save_dir == null:
		return []

	var rebuilt_by_id: Dictionary = {}
	var list_error := save_dir.list_dir_begin()
	if list_error != OK:
		_push_session_error("session.save.index.rebuild_list_failed", "Failed to list save directory %s for index rebuild. Error: %s" % [SAVE_DIRECTORY, list_error], {
			"save_directory": SAVE_DIRECTORY,
			"list_error": list_error,
		})
		return []
	while true:
		var file_name := save_dir.get_next()
		if file_name.is_empty():
			break
		if file_name == "." or file_name == ".." or save_dir.current_is_dir():
			continue
		if not file_name.ends_with(".dat") or file_name == "index.dat":
			continue
		var candidate_save_id := file_name.get_basename()
		if not _save_serializer.is_valid_save_id_token(candidate_save_id):
			continue
		var save_path := "%s/%s" % [SAVE_DIRECTORY, file_name]
		var read_result := _read_save_payload(save_path, false)
		if int(read_result.get("error", ERR_INVALID_DATA)) != OK:
			continue
		var payload_variant = read_result.get("payload", {})
		if typeof(payload_variant) != TYPE_DICTIONARY:
			continue
		var payload: Dictionary = payload_variant
		var save_meta := _extract_save_meta_from_payload(payload)
		if save_meta.is_empty():
			continue
		var generation_config_path := String(save_meta.get("generation_config_path", ""))
		var generation_config = _load_generation_config(generation_config_path)
		if generation_config == null:
			continue
		var decode_result := _save_serializer.decode_payload(payload, generation_config_path, generation_config, save_meta)
		if int(decode_result.get("error", ERR_INVALID_DATA)) != OK:
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


func _extract_save_meta_from_payload(payload: Dictionary) -> Dictionary:
	return _save_serializer.extract_save_meta_from_payload(payload)


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
	return FILE_IO_COORDINATOR_SCRIPT.remove_directory_recursive(
		virtual_path,
		Callable(self, "_push_session_error")
	)


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

	if not CHARACTER_CREATION_SERVICE_SCRIPT.apply_character_creation_payload_to_member(
		member_state,
		payload,
		_progression_content_registry,
		{CHARACTER_CREATION_SERVICE_SCRIPT.CREATION_OPTION_BAKE_REROLL_LUCK: true}
	):
		push_warning("GameSession: failed to apply character creation payload for main character %s." % String(main_member_id))
		return

	_revoke_orphan_racial_skills(_party_state)
	_backfill_racial_granted_skills(_party_state)


func _apply_character_creation_identity_payload(member_state, payload: Dictionary) -> void:
	if member_state == null or payload == null:
		return
	member_state.race_id = _read_payload_string_name(payload, "race_id", member_state.race_id, false)
	member_state.subrace_id = _read_payload_string_name(payload, "subrace_id", member_state.subrace_id, false)
	member_state.age_years = _read_payload_nonnegative_int(payload, "age_years", member_state.age_years)
	member_state.birth_at_world_step = _read_payload_nonnegative_int(payload, "birth_at_world_step", member_state.birth_at_world_step)
	member_state.age_profile_id = _read_payload_string_name(payload, "age_profile_id", member_state.age_profile_id, false)
	member_state.natural_age_stage_id = _read_payload_string_name(payload, "natural_age_stage_id", member_state.natural_age_stage_id, false)
	member_state.effective_age_stage_id = _read_payload_string_name(payload, "effective_age_stage_id", member_state.effective_age_stage_id, false)
	member_state.effective_age_stage_source_type = _read_payload_string_name(payload, "effective_age_stage_source_type", member_state.effective_age_stage_source_type, true)
	member_state.effective_age_stage_source_id = _read_payload_string_name(payload, "effective_age_stage_source_id", member_state.effective_age_stage_source_id, true)
	member_state.body_size = maxi(_read_payload_nonnegative_int(payload, "body_size", member_state.body_size), 1)
	member_state.body_size_category = _read_payload_string_name(payload, "body_size_category", member_state.body_size_category, false)
	member_state.versatility_pick = _read_payload_string_name(payload, "versatility_pick", member_state.versatility_pick, true)
	if payload.has("active_stage_advancement_modifier_ids") and payload["active_stage_advancement_modifier_ids"] is Array:
		member_state.active_stage_advancement_modifier_ids = ProgressionDataUtils.to_string_name_array(payload["active_stage_advancement_modifier_ids"])
	member_state.bloodline_id = _read_payload_string_name(payload, "bloodline_id", member_state.bloodline_id, true)
	member_state.bloodline_stage_id = _read_payload_string_name(payload, "bloodline_stage_id", member_state.bloodline_stage_id, true)
	member_state.ascension_id = _read_payload_string_name(payload, "ascension_id", member_state.ascension_id, true)
	member_state.ascension_stage_id = _read_payload_string_name(payload, "ascension_stage_id", member_state.ascension_stage_id, true)
	if payload.has("ascension_started_at_world_step") and payload["ascension_started_at_world_step"] is int:
		member_state.ascension_started_at_world_step = maxi(int(payload["ascension_started_at_world_step"]), -1)
	member_state.original_race_id_before_ascension = _read_payload_string_name(payload, "original_race_id_before_ascension", member_state.original_race_id_before_ascension, true)
	member_state.biological_age_years = _read_payload_nonnegative_int(payload, "biological_age_years", member_state.biological_age_years)
	member_state.astral_memory_years = _read_payload_nonnegative_int(payload, "astral_memory_years", member_state.astral_memory_years)
	_refresh_member_body_size_from_identity(member_state)


func _apply_initial_hp_formula(member_state) -> void:
	if member_state == null or member_state.progression == null:
		return
	var attributes: UnitBaseAttributes = member_state.progression.unit_base_attributes
	if attributes == null:
		return
	var constitution := int(attributes.get_attribute_value(UnitBaseAttributes.CONSTITUTION))
	var initial_hp_max := CHARACTER_CREATION_SERVICE_SCRIPT.calculate_initial_hp_max(constitution)
	attributes.set_attribute_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, initial_hp_max)
	member_state.current_hp = initial_hp_max


func _refresh_member_body_size_from_identity(member_state) -> bool:
	var category := _resolve_body_size_category_for_member(member_state)
	if category == &"":
		return false
	var resolved_body_size := BodySizeRules.get_body_size_for_category(category)
	if member_state.body_size_category == category and int(member_state.body_size) == resolved_body_size:
		return false
	member_state.body_size_category = category
	member_state.body_size = resolved_body_size
	return true


func _resolve_body_size_category_for_member(member_state) -> StringName:
	if member_state == null or _progression_content_registry == null:
		return &""
	if member_state.ascension_stage_id != &"":
		var ascension_stage_def := _progression_content_registry.get_ascension_stage_defs().get(member_state.ascension_stage_id) as AscensionStageDef
		if ascension_stage_def != null \
			and ascension_stage_def.body_size_category_override != &"" \
			and BodySizeRules.is_valid_body_size_category(ascension_stage_def.body_size_category_override):
			return ascension_stage_def.body_size_category_override
	var subrace_def := _progression_content_registry.get_subrace_defs().get(member_state.subrace_id) as SubraceDef
	if subrace_def != null \
		and subrace_def.body_size_category_override != &"" \
		and BodySizeRules.is_valid_body_size_category(subrace_def.body_size_category_override):
		return subrace_def.body_size_category_override
	var race_def := _progression_content_registry.get_race_defs().get(member_state.race_id) as RaceDef
	if race_def != null and BodySizeRules.is_valid_body_size_category(race_def.body_size_category):
		return race_def.body_size_category
	return &""


func _read_payload_string_name(payload: Dictionary, field_name: String, fallback: StringName, allow_empty: bool) -> StringName:
	if not payload.has(field_name):
		return fallback
	var value: Variant = payload[field_name]
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return fallback
	var parsed := ProgressionDataUtils.to_string_name(value)
	if parsed == &"" and not allow_empty:
		return fallback
	return parsed


func _read_payload_nonnegative_int(payload: Dictionary, field_name: String, fallback: int) -> int:
	if not payload.has(field_name) or payload[field_name] is not int:
		return fallback
	return maxi(int(payload[field_name]), 0)


func _create_default_party_state():
	var party_state = PARTY_STATE_SCRIPT.new()
	party_state.gold = 180

	var sword_member = _build_default_member_state(
		&"player_sword_01",
		"剑士",
		&"warrior_heavy_strike",
		&"portrait_sword",
		0,
		4,
		2,
		3,
		1,
		1,
		1,
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
	current_mp: int,
	strength: int,
	agility: int,
	constitution: int,
	perception: int,
	intelligence: int,
	willpower: int,
	storage_space: int = 0
):
	var member_state = PARTY_MEMBER_STATE_SCRIPT.new()
	member_state.member_id = member_id
	member_state.display_name = display_name
	member_state.faction_id = &"player"
	member_state.portrait_id = portrait_id
	member_state.control_mode = &"manual"
	member_state.current_mp = current_mp
	member_state.body_size = 2
	member_state.race_id = &"human"
	member_state.subrace_id = &"common_human"
	member_state.age_years = 24
	member_state.birth_at_world_step = 0
	member_state.age_profile_id = &"human_age_profile"
	member_state.natural_age_stage_id = &"adult"
	member_state.effective_age_stage_id = &"adult"
	member_state.effective_age_stage_source_type = &""
	member_state.effective_age_stage_source_id = &""
	member_state.body_size_category = &"medium"
	member_state.versatility_pick = &""
	member_state.active_stage_advancement_modifier_ids = Array([], TYPE_STRING_NAME, &"", null)
	member_state.bloodline_id = &""
	member_state.bloodline_stage_id = &""
	member_state.ascension_id = &""
	member_state.ascension_stage_id = &""
	member_state.ascension_started_at_world_step = -1
	member_state.original_race_id_before_ascension = &""
	member_state.biological_age_years = 24
	member_state.astral_memory_years = 0

	var progression = UNIT_PROGRESS_SCRIPT.new()
	progression.unit_id = member_id
	progression.display_name = display_name
	progression.character_level = 0

	var unit_base_attributes: UnitBaseAttributes = UNIT_BASE_ATTRIBUTES_SCRIPT.new()
	unit_base_attributes.strength = strength
	unit_base_attributes.agility = agility
	unit_base_attributes.constitution = constitution
	unit_base_attributes.perception = perception
	unit_base_attributes.intelligence = intelligence
	unit_base_attributes.willpower = willpower
	var initial_hp_max := CHARACTER_CREATION_SERVICE_SCRIPT.calculate_initial_hp_max(constitution)
	unit_base_attributes.custom_stats[&"hp_max"] = initial_hp_max
	unit_base_attributes.custom_stats[&"mp_max"] = current_mp
	unit_base_attributes.custom_stats[&"storage_space"] = maxi(storage_space, 0)
	member_state.current_hp = initial_hp_max
	progression.unit_base_attributes = unit_base_attributes

	var starter_skill = UNIT_SKILL_PROGRESS_SCRIPT.new()
	starter_skill.skill_id = starting_skill_id
	starter_skill.is_learned = true
	starter_skill.is_core = true
	starter_skill.assigned_profession_id = &"warrior"
	starter_skill.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PROFESSION
	starter_skill.granted_source_id = &"warrior"
	progression.set_skill_progress(starter_skill)

	var warrior_progress = UNIT_PROFESSION_PROGRESS_SCRIPT.new()
	warrior_progress.profession_id = &"warrior"
	warrior_progress.rank = 0
	warrior_progress.is_active = false
	warrior_progress.add_core_skill(starting_skill_id)
	progression.set_profession_progress(warrior_progress)
	var random_starting_skill_def = _grant_random_starting_book_skill(progression)
	_refresh_progression_runtime_state(progression)

	member_state.progression = progression
	_equip_starting_weapon_for_skill(member_state, random_starting_skill_def)
	return member_state


func _grant_random_starting_book_skill(progression):
	if progression == null or _skill_defs.is_empty():
		return null

	var eligible_skill_ids: Array[StringName] = []
	for skill_key in ProgressionDataUtils.sorted_string_keys(_skill_defs):
		var skill_id := StringName(skill_key)
		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if not _is_random_start_book_skill_candidate(skill_def, progression):
			continue
		eligible_skill_ids.append(skill_id)

	if eligible_skill_ids.is_empty():
		return null

	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var selected_skill_id: StringName = eligible_skill_ids[rng.randi_range(0, eligible_skill_ids.size() - 1)]
	var selected_skill_def := _skill_defs.get(selected_skill_id) as SkillDef
	if selected_skill_def == null:
		return null

	var skill_progress = progression.get_skill_progress(selected_skill_id)
	if skill_progress == null:
		skill_progress = UNIT_SKILL_PROGRESS_SCRIPT.new()
		skill_progress.skill_id = selected_skill_id

	skill_progress.is_learned = true
	skill_progress.granted_source_type = UnitSkillProgress.GRANTED_SOURCE_PLAYER
	skill_progress.granted_source_id = &""
	skill_progress.skill_level = _resolve_random_start_skill_initial_level(selected_skill_def)
	skill_progress.current_mastery = 0
	skill_progress.total_mastery_earned = 0
	progression.set_skill_progress(skill_progress)
	return selected_skill_def


func _equip_starting_weapon_for_skill(member_state, skill_def) -> void:
	if member_state == null or member_state.equipment_state == null:
		return
	var item_id := _resolve_starting_weapon_item_id_for_skill(skill_def)
	if item_id == &"":
		return
	var item_def = _item_defs.get(item_id)
	if item_def == null or not item_def.is_weapon():
		return
	var instance_id := allocate_equipment_instance_id()
	if instance_id == &"":
		return
	var equipment_instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(item_id, instance_id)
	var occupied_slots: Array[StringName] = item_def.get_final_occupied_slot_ids(EQUIPMENT_RULES_SCRIPT.MAIN_HAND)
	member_state.equipment_state.set_equipped_entry(
		EQUIPMENT_RULES_SCRIPT.MAIN_HAND,
		item_id,
		occupied_slots,
		equipment_instance
	)


func _resolve_starting_weapon_item_id_for_skill(skill_def) -> StringName:
	var candidates: Array[StringName] = []
	if _skill_matches_starting_weapon_type(skill_def, [&"crossbow"], ["crossbow"]):
		candidates.append(STARTING_CROSSBOW_WEAPON_ITEM_ID)
	if _skill_matches_starting_weapon_type(skill_def, [&"archer", &"bow"], ["archer_"]):
		candidates.append(STARTING_ARCHER_WEAPON_ITEM_ID)
	if _skill_matches_starting_weapon_type(skill_def, [&"mage", &"magic", &"spell"], ["mage_"]):
		candidates.append(STARTING_MAGE_WEAPON_ITEM_ID)
	if _skill_matches_starting_weapon_type(skill_def, [&"priest", &"faith", &"heal"], ["priest_", "saint_"]):
		candidates.append(STARTING_PRIEST_WEAPON_ITEM_ID)
	if _skill_matches_starting_weapon_type(skill_def, [&"warrior", &"melee", &"shield"], ["warrior_"]):
		candidates.append(STARTING_MELEE_WEAPON_ITEM_ID)
	candidates.append(STARTING_MELEE_WEAPON_ITEM_ID)
	return _first_valid_starting_weapon_item_id(candidates)


func _skill_matches_starting_weapon_type(skill_def, tag_ids: Array[StringName], skill_id_prefixes: Array[String]) -> bool:
	if skill_def == null:
		return false
	for tag_id in tag_ids:
		if skill_def.tags.has(tag_id):
			return true
	var skill_id_text := String(skill_def.skill_id)
	for prefix in skill_id_prefixes:
		if skill_id_text.begins_with(prefix):
			return true
	return false


func _first_valid_starting_weapon_item_id(candidates: Array[StringName]) -> StringName:
	for item_id in candidates:
		if item_id == &"":
			continue
		var item_def = _item_defs.get(item_id)
		if item_def != null and item_def.is_weapon():
			return item_id
	return &""


func _refresh_progression_runtime_state(progression) -> void:
	if progression == null:
		return
	var progression_service = PROGRESSION_SERVICE_SCRIPT.new()
	progression_service.setup(progression, _skill_defs, _profession_defs)
	progression_service.refresh_runtime_state()


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


func _resolve_random_start_skill_initial_level(skill_def: SkillDef, progression: UnitProgress = null) -> int:
	if skill_def == null:
		return 0
	var mapped_level := int(RANDOM_START_SKILL_LEVEL_BY_TIER.get(_resolve_random_start_skill_tier(skill_def), 0))
	var max_initial_level := maxi(skill_def.max_level, 0) if skill_def.max_level >= 0 else 999
	if progression != null and skill_def.dynamic_max_level_stat_id != &"":
		var effective_max := SKILL_EFFECTIVE_MAX_LEVEL_RULES_SCRIPT.get_effective_max_level(skill_def, null, progression)
		if effective_max > 0:
			max_initial_level = mini(max_initial_level, effective_max)
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


func _capture_runtime_state() -> Dictionary:
	return {
		"active_save_id": _active_save_id,
		"active_save_path": _active_save_path,
		"active_save_meta": _active_save_meta.duplicate(true),
		"generation_config_path": _generation_config_path,
		"generation_config": _generation_config,
		"world_data": _world_data.duplicate(true),
		"player_coord": _player_coord,
		"player_faction_id": _player_faction_id,
		"party_state": _party_state,
		"has_active_world": _has_active_world,
		"battle_save_lock_enabled": _battle_save_lock_enabled,
		"battle_save_dirty": _battle_save_dirty,
		"runtime_save_dirty": _runtime_save_dirty,
		"runtime_save_dirty_scopes": _runtime_save_dirty_scopes.duplicate(),
		"last_save_error": _last_save_error,
		"last_save_error_reason": _last_save_error_reason,
		"post_decode_save_pending": _post_decode_save_pending,
		"post_decode_save_reasons": _post_decode_save_reasons.duplicate(),
	}


func _restore_runtime_state(state: Dictionary) -> void:
	_active_save_id = String(state.get("active_save_id", ""))
	_active_save_path = String(state.get("active_save_path", ""))
	_active_save_meta = (state.get("active_save_meta", {}) as Dictionary).duplicate(true)
	_generation_config_path = String(state.get("generation_config_path", ""))
	_generation_config = state.get("generation_config", null)
	_world_data = (state.get("world_data", {}) as Dictionary).duplicate(true)
	_player_coord = state.get("player_coord", Vector2i.ZERO)
	_player_faction_id = String(state.get("player_faction_id", "player"))
	_party_state = state.get("party_state", PARTY_STATE_SCRIPT.new())
	_has_active_world = bool(state.get("has_active_world", false))
	_battle_save_lock_enabled = bool(state.get("battle_save_lock_enabled", false))
	_battle_save_dirty = bool(state.get("battle_save_dirty", false))
	_runtime_save_dirty = bool(state.get("runtime_save_dirty", false))
	_runtime_save_dirty_scopes = ProgressionDataUtils.to_string_name_array(state.get("runtime_save_dirty_scopes", []))
	_last_save_error = int(state.get("last_save_error", OK))
	_last_save_error_reason = StringName(String(state.get("last_save_error_reason", &"")))
	_post_decode_save_pending = bool(state.get("post_decode_save_pending", false))
	_post_decode_save_reasons = ProgressionDataUtils.to_string_name_array(state.get("post_decode_save_reasons", []))


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
	_runtime_save_dirty = false
	_runtime_save_dirty_scopes.clear()
	_last_save_error = OK
	_last_save_error_reason = &""
	_post_decode_save_pending = false
	_post_decode_save_reasons.clear()


func _refresh_progression_content() -> void:
	if _progression_content_registry == null:
		return

	_skill_defs = _progression_content_registry.get_skill_defs()
	_profession_defs = _progression_content_registry.get_profession_defs()
	_achievement_defs = _progression_content_registry.get_achievement_defs()
	_quest_defs = _progression_content_registry.get_quest_defs()


func _refresh_battle_special_profiles() -> void:
	if _battle_special_profile_registry == null:
		return
	_battle_special_profile_registry.rebuild(_skill_defs)


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
	_refresh_battle_special_profiles()
	var domain_snapshots := {
		"progression": _build_content_validation_domain_snapshot(_progression_content_registry),
		"battle_special_profile": _build_content_validation_domain_snapshot(_battle_special_profile_registry),
		"item": _build_content_validation_domain_snapshot(_item_content_registry),
		"recipe": _build_content_validation_domain_snapshot(_recipe_content_registry),
		"enemy": _build_content_validation_domain_snapshot(_enemy_content_registry),
		"world": _build_world_content_validation_domain_snapshot(),
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


func _build_world_content_validation_domain_snapshot() -> Dictionary:
	var errors: Array[String] = []
	if _world_content_validator != null and _world_content_validator.has_method("validate_world_presets"):
		for validation_error in _world_content_validator.validate_world_presets(_enemy_templates, _wild_encounter_rosters):
			errors.append(String(validation_error))
	return {
		"ok": errors.is_empty(),
		"error_count": errors.size(),
		"errors": errors,
	}


func _require_content_validation_for_runtime(operation_id: StringName) -> int:
	_refresh_content_validation_snapshot()
	if is_content_validation_ok():
		return OK
	var error_count := int(_content_validation_snapshot.get("error_count", 0))
	_push_session_error(
		"session.content.validation_blocked",
		"GameSession blocked formal runtime entry because content validation failed.",
		{
			"operation_id": String(operation_id),
			"error_count": error_count,
		}
	)
	return ERR_INVALID_DATA


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
		"battle_special_profile":
			_push_session_error("session.content.battle_special_profile_validation_failed", "Battle special profile content error: %s" % validation_error)
		"item":
			_push_session_error("session.content.item_validation_failed", "Item content error: %s" % validation_error)
		"recipe":
			_push_session_error("session.content.recipe_validation_failed", "Recipe content error: %s" % validation_error)
		"enemy":
			_push_session_error("session.content.enemy_validation_failed", "Enemy content error: %s" % validation_error)
		"world":
			_push_session_error("session.content.world_validation_failed", "World content error: %s" % validation_error)


func _log_session_info(event_id: String, message: String, context: Dictionary = {}) -> void:
	log_event("info", "session", event_id, message, context)


func _push_session_error(event_id: String, message: String, context: Dictionary = {}) -> void:
	push_error(message)
	log_event("error", "session", event_id, message, context)
