extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_corrupt_save_index_recovers_cleanly()
	if _failures.is_empty():
		print("Save index resilience regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Save index resilience regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_corrupt_save_index_recovers_cleanly() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	root.add_child(game_session)
	await process_frame

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "测试前应能清理旧存档目录。")
	var ensure_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SAVE_DIRECTORY))
	_assert_eq(ensure_error, OK, "测试前应能创建 save 目录。")

	var corrupt_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	_assert_true(corrupt_file != null, "应能写入损坏的 save index 夹具。")
	if corrupt_file == null:
		return
	corrupt_file.store_buffer(PackedByteArray([0xCC, 0x80, 0x01, 0x02]))
	corrupt_file.close()

	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "损坏的 save index 不应阻止创建新存档。")

	var save_slots := game_session.list_save_slots()
	_assert_true(not save_slots.is_empty(), "损坏索引恢复后，应能重新列出至少一个存档槽。")
	if not save_slots.is_empty():
		_assert_eq(
			String(save_slots[0].get("save_id", "")),
			game_session.get_active_save_id(),
			"恢复后的索引应包含当前新创建的存档。"
		)

	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	_assert_true(index_file != null, "恢复后应能重新打开 save index。")
	if index_file != null:
		var raw_bytes := index_file.get_buffer(index_file.get_length())
		index_file.close()
		_assert_true(not raw_bytes.is_empty(), "恢复后的 save index 不应为空文件。")
		for byte_value in raw_bytes:
			_assert_true(int(byte_value) >= 0 and int(byte_value) <= 127, "恢复后的 save index 应写成 ASCII-only JSON。")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "测试结束后应能清理 save 目录。")
	game_session.queue_free()
	await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
