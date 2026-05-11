extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_plain_world_move_stages_without_disk_write()

	if _failures.is_empty():
		print("World map save transaction regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map save transaction regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_plain_world_move_stages_without_disk_write() -> void:
	var directions := [Vector2i.RIGHT, Vector2i.LEFT, Vector2i.DOWN, Vector2i.UP]
	for direction in directions:
		var context := _create_runtime_context()
		if context.is_empty():
			return
		var game_session = context.get("game_session")
		var facade = context.get("facade")
		var original_payload := _read_active_save_payload(game_session)
		var original_coord := _payload_player_coord(original_payload)
		var result: Dictionary = facade.command_world_move(direction, 1)
		var moved_without_boundary: bool = bool(result.get("ok", false)) \
			and facade.get_player_coord() != original_coord \
			and facade.get_active_modal_id().is_empty() \
			and not facade.is_battle_active()
		if not moved_without_boundary:
			facade.dispose()
			_cleanup(game_session)
			continue

		_assert_true(game_session.has_pending_save(), "普通大地图移动后应只标记 pending save。")
		var disk_payload := _read_active_save_payload(game_session)
		_assert_eq(
			_payload_player_coord(disk_payload),
			original_coord,
			"普通大地图移动不应逐步写入磁盘坐标。"
		)
		facade.dispose()
		_cleanup(game_session)
		return

	_test.fail("测试地图应至少存在一个不会打开窗口或战斗的相邻可移动格。")


func _create_runtime_context() -> Dictionary:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "大地图保存事务回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup(game_session)
		return {}
	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	return {
		"game_session": game_session,
		"facade": facade,
	}


func _read_active_save_payload(game_session) -> Dictionary:
	var save_path: String = game_session.get_active_save_path()
	if save_path.is_empty():
		return {}
	var save_file := FileAccess.open_compressed(save_path, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
	if save_file == null:
		return {}
	var payload = save_file.get_var(false)
	save_file.close()
	return payload if payload is Dictionary else {}


func _payload_player_coord(payload: Dictionary) -> Vector2i:
	var world_state_variant = _dictionary_get(payload, "world_state", {})
	var world_state: Dictionary = world_state_variant if world_state_variant is Dictionary else {}
	return _dictionary_get(world_state, "player_coord", Vector2i.ZERO)


func _dictionary_get(values: Dictionary, key: String, default_value: Variant = null) -> Variant:
	if values.has(key):
		return values[key]
	var string_name_key := StringName(key)
	if values.has(string_name_key):
		return values[string_name_key]
	return default_value


func _cleanup(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
