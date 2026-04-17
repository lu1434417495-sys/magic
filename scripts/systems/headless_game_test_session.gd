# Development-only headless bridge for automation and debugging.
# This is not a player-facing startup path or UI layer.
class_name HeadlessGameTestSession
extends RefCounted

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")
const HEADLESS_SETTLEMENT_LOOT_PROFILE_ID: StringName = &"wolf_den"
const HEADLESS_SETTLEMENT_LOOT_ENCOUNTER_ID: StringName = &"headless_settlement_wolf_den"
const HEADLESS_SETTLEMENT_LOOT_DISPLAY_NAME := "荒狼巢穴"

var _game_session = null
var _runtime = null
var _owns_game_session := false


func initialize() -> void:
	await _ensure_game_session()


func get_game_session():
	return _game_session


func get_runtime_facade():
	return _runtime


func has_world_loaded() -> bool:
	return _runtime != null


func list_presets() -> Array[Dictionary]:
	return WORLD_PRESET_REGISTRY_SCRIPT.list_presets()


func list_save_slots() -> Array[Dictionary]:
	await _ensure_game_session()
	return _game_session.list_save_slots()


func create_new_game(preset_id: StringName) -> Dictionary:
	await _ensure_game_session()
	var preset: Dictionary = WORLD_PRESET_REGISTRY_SCRIPT.get_preset(preset_id)
	if preset.is_empty():
		return {
			"ok": false,
			"message": "未找到世界预设 %s。" % String(preset_id),
		}
	await _unload_world_scene()
	var create_error := int(_game_session.create_new_save(
		String(preset.get("generation_config_path", "")),
		preset_id,
		String(preset.get("display_name", "世界"))
	))
	if create_error != OK:
		return {
			"ok": false,
			"message": "创建世界失败，错误码 %d。" % create_error,
		}
	return await ensure_world_loaded()


func load_game(save_id: String) -> Dictionary:
	await _ensure_game_session()
	if save_id.is_empty():
		return {
			"ok": false,
			"message": "存档 ID 不能为空。",
		}
	await _unload_world_scene()
	var load_error := int(_game_session.load_save(save_id))
	if load_error != OK:
		return {
			"ok": false,
			"message": "加载存档失败，错误码 %d。" % load_error,
		}
	return await ensure_world_loaded()


func ensure_world_loaded() -> Dictionary:
	await _ensure_game_session()
	if not _game_session.has_active_world():
		return {
			"ok": false,
			"message": "当前没有已加载的世界。",
		}
	if has_world_loaded():
		await settle_frames()
		return {
			"ok": true,
			"message": "世界地图已可用。",
		}
	_runtime = GAME_RUNTIME_FACADE_SCRIPT.new()
	_runtime.setup(_game_session)
	await settle_frames()
	return {
		"ok": true,
		"message": "世界地图已载入。",
	}


func settle_frames(frame_count: int = 2) -> void:
	var scene_tree := _get_scene_tree()
	if scene_tree == null:
		return
	for _index in range(maxi(frame_count, 1)):
		await scene_tree.process_frame


func set_party_storage_capacity(capacity: int) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	var party_state = _runtime.get_party_state()
	if party_state == null:
		return {
			"ok": false,
			"message": "当前不存在队伍数据。",
		}
	var resolved_capacity := maxi(capacity, 0)
	var first_member_assigned := false
	for member_variant in party_state.member_states.values():
		var member_state = member_variant
		if member_state == null or member_state.progression == null:
			continue
		var unit_base_attributes = member_state.progression.unit_base_attributes
		if unit_base_attributes == null:
			continue
		unit_base_attributes.custom_stats[&"storage_space"] = resolved_capacity if not first_member_assigned else 0
		first_member_assigned = true
	await settle_frames(1)
	if not first_member_assigned:
		return {
			"ok": false,
			"message": "当前队伍没有可调整仓库容量的成员。",
		}
	return {
		"ok": true,
		"message": "已将共享仓库总容量调整为 %d。" % resolved_capacity,
	}


func start_battle_by_kind(encounter_kind: StringName) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	if _runtime.is_battle_active():
		return {
			"ok": false,
			"message": "当前已有进行中的战斗。",
		}
	var encounter_anchor: EncounterAnchorData = _find_nearest_encounter_anchor(encounter_kind)
	if encounter_anchor == null:
		encounter_anchor = _build_headless_encounter_anchor(encounter_kind)
	if encounter_anchor == null:
		return {
			"ok": false,
			"message": "未找到 encounter_kind=%s 的遭遇。" % String(encounter_kind),
		}
	_game_session.set_battle_save_lock(true)
	_runtime._start_battle(encounter_anchor)
	await settle_frames(1)
	if not _runtime.is_battle_active():
		_game_session.set_battle_save_lock(false)
		return {
			"ok": false,
			"message": "遭遇 %s 未能开始战斗。" % String(encounter_anchor.display_name),
		}
	return {
		"ok": true,
		"message": "已进入遭遇 %s 的战斗准备。" % String(encounter_anchor.display_name),
	}


func finish_active_battle(winner_faction_id: StringName) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	if not _runtime.is_battle_active():
		return {
			"ok": false,
			"message": "当前没有进行中的战斗。",
		}
	if winner_faction_id != &"player" and winner_faction_id != &"hostile":
		return {
			"ok": false,
			"message": "胜利方只能是 player 或 hostile。",
		}
	var battle_state = _runtime.get_battle_state()
	if battle_state == null or battle_state.is_empty():
		return {
			"ok": false,
			"message": "当前战斗状态不可用。",
		}
	battle_state.phase = &"battle_ended"
	battle_state.winner_faction_id = winner_faction_id
	battle_state.active_unit_id = &""
	battle_state.timeline.ready_unit_ids.clear()
	battle_state.timeline.frozen = true
	_runtime._refresh_battle_runtime_state()
	var result: Dictionary = _runtime.command_battle_wait_or_resolve()
	await settle_frames(1)
	return result


func build_snapshot() -> Dictionary:
	var snapshot := {
		"session": {
			"active_save_id": _game_session.get_active_save_id() if _game_session != null else "",
			"generation_config_path":
				_game_session.get_generation_config_path() if _game_session != null else "",
			"world_loaded": has_world_loaded(),
			"presets": WORLD_PRESET_REGISTRY_SCRIPT.list_presets(),
			"save_slots": _game_session.list_save_slots() if _game_session != null else [],
		},
		"status": {
			"view": "none",
			"text": "",
		},
		"modal": {
			"id": "",
		},
		"logs": _game_session.get_log_snapshot() if _game_session != null else {},
		"world": {},
		"submap": {},
		"party": {},
		"settlement": {},
		"character_info": {},
		"warehouse": {},
		"battle": {},
		"reward": {},
		"promotion": {},
	}
	if has_world_loaded():
		var world_snapshot: Dictionary = _runtime.build_headless_snapshot()
		for key in world_snapshot.keys():
			snapshot[key] = world_snapshot[key]
	return snapshot


func build_text_snapshot() -> String:
	return GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot(build_snapshot())


func dispose(clear_persisted_game: bool = false) -> void:
	await _unload_world_scene()

	if _game_session != null and is_instance_valid(_game_session):
		if clear_persisted_game and _game_session.has_method("clear_persisted_game"):
			_game_session.clear_persisted_game()
		if _owns_game_session:
			_game_session.queue_free()
			await settle_frames(2)
	_game_session = null
	_owns_game_session = false


func _ensure_game_session() -> void:
	if _game_session != null and is_instance_valid(_game_session):
		return
	var scene_tree := _get_scene_tree()
	if scene_tree == null:
		return
	_game_session = scene_tree.root.get_node_or_null("GameSession")
	if _game_session != null:
		_owns_game_session = false
		return
	_game_session = GAME_SESSION_SCRIPT.new()
	_game_session.name = "GameSession"
	scene_tree.root.add_child(_game_session)
	_owns_game_session = true
	await settle_frames(1)


func _unload_world_scene() -> void:
	if not has_world_loaded():
		_runtime = null
		return
	if _runtime != null and _runtime.has_method("dispose"):
		_runtime.dispose()
	_runtime = null
	await settle_frames()


func _get_scene_tree() -> SceneTree:
	var main_loop := Engine.get_main_loop()
	return main_loop as SceneTree if main_loop is SceneTree else null


func _find_nearest_encounter_anchor(encounter_kind: StringName) -> EncounterAnchorData:
	if _runtime == null:
		return null
	var player_coord: Vector2i = _runtime.get_player_coord()
	var nearest_encounter: EncounterAnchorData = null
	var nearest_distance := 2147483647
	for encounter_variant in _runtime.get_world_data().get("encounter_anchors", []):
		var encounter_anchor: EncounterAnchorData = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null or encounter_anchor.is_cleared:
			continue
		if encounter_kind != &"" and encounter_anchor.encounter_kind != encounter_kind:
			continue
		if encounter_kind == ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT and not _encounter_has_formal_loot(encounter_anchor):
			continue
		var delta: Vector2i = encounter_anchor.world_coord - player_coord
		var distance := absi(delta.x) + absi(delta.y)
		if distance > nearest_distance:
			continue
		if distance == nearest_distance and nearest_encounter != null and String(encounter_anchor.entity_id) >= String(nearest_encounter.entity_id):
			continue
		nearest_distance = distance
		nearest_encounter = encounter_anchor
	return nearest_encounter


func _encounter_has_formal_loot(encounter_anchor: EncounterAnchorData) -> bool:
	if encounter_anchor == null or _game_session == null:
		return false
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(_game_session.get_wild_encounter_rosters())
	return not builder.build_loot_entries(encounter_anchor, {}).is_empty()


func _build_headless_encounter_anchor(encounter_kind: StringName) -> EncounterAnchorData:
	if encounter_kind != ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT or _runtime == null:
		return null
	if not _game_session.get_wild_encounter_rosters().has(HEADLESS_SETTLEMENT_LOOT_PROFILE_ID):
		return null
	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = HEADLESS_SETTLEMENT_LOOT_ENCOUNTER_ID
	encounter_anchor.display_name = HEADLESS_SETTLEMENT_LOOT_DISPLAY_NAME
	encounter_anchor.world_coord = _runtime.get_player_coord()
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.encounter_kind = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	encounter_anchor.encounter_profile_id = HEADLESS_SETTLEMENT_LOOT_PROFILE_ID
	if not _encounter_has_formal_loot(encounter_anchor):
		return null
	return encounter_anchor
