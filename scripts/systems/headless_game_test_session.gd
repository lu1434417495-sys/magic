class_name HeadlessGameTestSession
extends RefCounted

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")

var _game_session = null
var _runtime = null


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
		"world": {},
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


func _ensure_game_session() -> void:
	if _game_session != null and is_instance_valid(_game_session):
		return
	var scene_tree := _get_scene_tree()
	if scene_tree == null:
		return
	_game_session = scene_tree.root.get_node_or_null("GameSession")
	if _game_session != null:
		return
	_game_session = GAME_SESSION_SCRIPT.new()
	_game_session.name = "GameSession"
	scene_tree.root.add_child(_game_session)
	await settle_frames(1)


func _unload_world_scene() -> void:
	if not has_world_loaded():
		_runtime = null
		return
	_runtime = null
	await settle_frames()


func _get_scene_tree() -> SceneTree:
	var main_loop := Engine.get_main_loop()
	return main_loop as SceneTree if main_loop is SceneTree else null
