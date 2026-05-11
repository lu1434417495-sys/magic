extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class FailingPayloadWriteSession extends GAME_SESSION_SCRIPT:
	var fail_payload_write := false

	func _write_save_payload_atomically(save_path: String, payload: Dictionary) -> int:
		if fail_payload_write:
			return ERR_CANT_CREATE
		return super._write_save_payload_atomically(save_path, payload)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_setters_stage_runtime_without_disk_write()
	_test_commit_runtime_state_persists_complete_snapshot()
	_test_commit_failure_keeps_dirty_and_last_error()
	_test_unload_commits_pending_runtime_state()

	if _failures.is_empty():
		print("GameSession transaction regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("GameSession transaction regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_setters_stage_runtime_without_disk_write() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "事务 setter 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var original_payload := _read_active_save_payload(game_session)
	var original_coord := _payload_player_coord(original_payload)
	var staged_coord := original_coord + Vector2i.RIGHT
	var set_error := int(game_session.set_player_coord(staged_coord))
	_assert_eq(set_error, OK, "set_player_coord 应只更新运行时并标记 dirty。")
	_assert_eq(game_session.get_player_coord(), staged_coord, "set_player_coord 后内存坐标应立即更新。")
	_assert_true(game_session.has_pending_save(), "setter 更新后应存在 pending save。")

	var disk_payload := _read_active_save_payload(game_session)
	_assert_eq(
		_payload_player_coord(disk_payload),
		original_coord,
		"未 commit 前 setter 不应把玩家坐标写入磁盘。"
	)

	_cleanup_test_session(game_session)


func _test_commit_runtime_state_persists_complete_snapshot() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "事务 commit 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return
	if not _require_method(game_session, "commit_runtime_state", "GameSession 应提供统一运行时提交入口。"):
		_cleanup_test_session(game_session)
		return

	var original_payload := _read_active_save_payload(game_session)
	var original_coord := _payload_player_coord(original_payload)
	var original_world_step := _payload_world_step(original_payload)
	var staged_coord := original_coord + Vector2i.RIGHT
	var staged_world_data := game_session.get_world_data().duplicate(true)
	staged_world_data["world_step"] = original_world_step + 7

	_assert_eq(int(game_session.set_player_coord(staged_coord)), OK, "事务 commit 回归前置：坐标 staging 应成功。")
	_assert_eq(int(game_session.set_world_data(staged_world_data)), OK, "事务 commit 回归前置：world_data staging 应成功。")
	var commit_error := int(game_session.commit_runtime_state(&"test.full_snapshot"))
	_assert_eq(commit_error, OK, "commit_runtime_state 应一次性持久化完整运行时快照。")
	_assert_true(not game_session.has_pending_save(), "commit 成功后 pending save 应被清空。")

	var committed_payload := _read_active_save_payload(game_session)
	_assert_eq(_payload_player_coord(committed_payload), staged_coord, "commit 后磁盘应保存 staged 玩家坐标。")
	_assert_eq(_payload_world_step(committed_payload), original_world_step + 7, "commit 后磁盘应保存 staged world_data。")

	_cleanup_test_session(game_session)


func _test_commit_failure_keeps_dirty_and_last_error() -> void:
	var game_session = FailingPayloadWriteSession.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "事务失败回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return
	if not _require_method(game_session, "commit_runtime_state", "GameSession 应提供统一运行时提交入口。"):
		_cleanup_test_session(game_session)
		return
	if not _require_method(game_session, "get_save_status", "GameSession 应暴露 dirty 与最近保存错误状态。"):
		_cleanup_test_session(game_session)
		return

	var original_payload := _read_active_save_payload(game_session)
	var original_coord := _payload_player_coord(original_payload)
	var staged_coord := original_coord + Vector2i.RIGHT
	_assert_eq(int(game_session.set_player_coord(staged_coord)), OK, "事务失败回归前置：坐标 staging 应成功。")

	game_session.fail_payload_write = true
	var commit_error := int(game_session.commit_runtime_state(&"test.fail_payload_write"))
	_assert_eq(commit_error, ERR_CANT_CREATE, "payload 写入失败时 commit_runtime_state 应返回底层错误。")
	_assert_true(game_session.has_pending_save(), "commit 失败后 pending save 不能被清空。")
	var status: Dictionary = game_session.get_save_status()
	_assert_eq(int(status.get("last_error", OK)), ERR_CANT_CREATE, "commit 失败后 save_status 应记录最近错误。")
	_assert_true(
		_array_has_string_name(status.get("dirty_scopes", []), &"player_coord"),
		"commit 失败后 dirty_scopes 应保留玩家坐标变更。"
	)

	var disk_payload := _read_active_save_payload(game_session)
	_assert_eq(_payload_player_coord(disk_payload), original_coord, "commit 失败后磁盘坐标应保持旧快照。")

	_cleanup_test_session(game_session)


func _test_unload_commits_pending_runtime_state() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "卸载提交回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var save_id := String(game_session.get_active_save_id())
	var original_payload := _read_active_save_payload(game_session)
	var staged_coord := _payload_player_coord(original_payload) + Vector2i.RIGHT
	_assert_eq(int(game_session.set_player_coord(staged_coord)), OK, "卸载提交回归前置：坐标 staging 应成功。")
	_assert_true(game_session.has_pending_save(), "卸载前应存在 pending save。")

	game_session.unload_active_world()
	_assert_true(not game_session.has_active_world(), "卸载 active world 应清理当前内存态。")
	var load_error := int(game_session.load_save(save_id))
	_assert_eq(load_error, OK, "卸载前的 pending save 应先提交，之后仍能重新载入。")
	if load_error == OK:
		_assert_eq(game_session.get_player_coord(), staged_coord, "卸载触发的提交应保存 staged 玩家坐标。")

	_cleanup_test_session(game_session)


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


func _payload_world_step(payload: Dictionary) -> int:
	var world_state_variant = _dictionary_get(payload, "world_state", {})
	var world_state: Dictionary = world_state_variant if world_state_variant is Dictionary else {}
	var world_data_variant = _dictionary_get(world_state, "world_data", {})
	var world_data: Dictionary = world_data_variant if world_data_variant is Dictionary else {}
	return int(_dictionary_get(world_data, "world_step", 0))


func _dictionary_get(values: Dictionary, key: String, default_value: Variant = null) -> Variant:
	if values.has(key):
		return values[key]
	var string_name_key := StringName(key)
	if values.has(string_name_key):
		return values[string_name_key]
	return default_value


func _array_has_string_name(values: Variant, expected: StringName) -> bool:
	if values is not Array:
		return false
	for value in values:
		if StringName(String(value)) == expected:
			return true
	return false


func _require_method(target, method_name: String, message: String) -> bool:
	if target != null and target.has_method(method_name):
		return true
	_test.fail(message)
	return false


func _cleanup_test_session(game_session) -> void:
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
