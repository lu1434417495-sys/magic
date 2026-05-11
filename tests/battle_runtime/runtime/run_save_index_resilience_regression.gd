extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY
const SAVE_INDEX_VERSION := 3
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_corrupt_save_index_recovers_cleanly()
	await _test_save_index_schema_rejects_old_entry_shapes()
	await _test_bak_only_restore_for_save_payload()
	await _test_index_rebuild_filters_payloads_that_fail_full_decode()
	await _test_ensure_world_ready_skips_newest_bad_save()
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
		_assert_true(not _looks_like_json_text(raw_bytes), "恢复后的 save index 不应写成 JSON 文本。")
		var restored_index_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
		if restored_index_file != null:
			var raw_index_payload = restored_index_file.get_var(false)
			restored_index_file.close()
			_assert_true(raw_index_payload is Dictionary, "恢复后的 save index 应写成 Godot Variant 二进制 Dictionary。")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "测试结束后应能清理 save 目录。")
	game_session.queue_free()
	await process_frame


func _test_save_index_schema_rejects_old_entry_shapes() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	root.add_child(game_session)
	await process_frame

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "save index schema 回归前应能清理旧存档目录。")
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "save index schema 回归应能创建测试存档。")
	if create_error != OK:
		game_session.queue_free()
		await process_frame
		return

	var serializer = game_session._save_serializer
	var meta_entries: Array[Dictionary] = [game_session.get_active_save_meta()]
	var serialized_entries: Array = serializer.serialize_save_index_entries(meta_entries)
	_assert_eq(serialized_entries.size(), 1, "当前 save meta 应能序列化为一个 index entry。")
	if serialized_entries.is_empty():
		game_session.clear_persisted_game()
		game_session.queue_free()
		await process_frame
		return

	var valid_entry: Dictionary = serialized_entries[0]
	_assert_true(not serializer.deserialize_save_index_entry(valid_entry).is_empty(), "完整 save index entry 应能反序列化。")

	var valid_meta := game_session.get_active_save_meta()
	_assert_true(not serializer.normalize_save_meta(valid_meta).is_empty(), "完整 save meta 应能通过严格 schema 校验。")

	var string_timestamp_meta := valid_meta.duplicate(true)
	string_timestamp_meta["created_at_unix_time"] = str(valid_meta.get("created_at_unix_time"))
	_assert_true(
		serializer.normalize_save_meta(string_timestamp_meta).is_empty(),
		"save meta 不应接受字符串时间戳。"
	)

	var bool_timestamp_meta := valid_meta.duplicate(true)
	bool_timestamp_meta["updated_at_unix_time"] = true
	_assert_true(
		serializer.normalize_save_meta(bool_timestamp_meta).is_empty(),
		"save meta 不应把布尔值转换为时间戳。"
	)

	var world_size_cells: Vector2i = valid_meta.get("world_size_cells", Vector2i.ZERO)
	var string_world_size_meta := valid_meta.duplicate(true)
	string_world_size_meta["world_size_cells"] = {
		"x": str(world_size_cells.x),
		"y": world_size_cells.y,
	}
	_assert_true(
		serializer.normalize_save_meta(string_world_size_meta).is_empty(),
		"save meta 不应接受字符串 world_size_cells 轴值。"
	)

	var non_string_name_meta := valid_meta.duplicate(true)
	non_string_name_meta["display_name"] = 123
	_assert_true(
		serializer.normalize_save_meta(non_string_name_meta).is_empty(),
		"save meta 不应把非字符串 display_name 转成正式字段。"
	)

	var missing_display_entry := valid_entry.duplicate(true)
	missing_display_entry.erase("display_name")
	_assert_true(
		serializer.deserialize_save_index_entry(missing_display_entry).is_empty(),
		"缺失 display_name 的 save index entry 应直接拒绝。"
	)

	var missing_world_size_axis_entry := valid_entry.duplicate(true)
	missing_world_size_axis_entry["world_size_cells"] = {"x": 32}
	_assert_true(
		serializer.deserialize_save_index_entry(missing_world_size_axis_entry).is_empty(),
		"缺失 world_size_cells.y 的旧 save index entry 应直接拒绝。"
	)

	var string_timestamp_entry := valid_entry.duplicate(true)
	string_timestamp_entry["updated_at_unix_time"] = str(valid_entry.get("updated_at_unix_time"))
	_assert_true(
		serializer.deserialize_save_index_entry(string_timestamp_entry).is_empty(),
		"save index entry 不应接受字符串时间戳。"
	)

	var bool_timestamp_entry := valid_entry.duplicate(true)
	bool_timestamp_entry["created_at_unix_time"] = true
	_assert_true(
		serializer.deserialize_save_index_entry(bool_timestamp_entry).is_empty(),
		"save index entry 不应把布尔值转换为时间戳。"
	)

	var string_world_size_entry := valid_entry.duplicate(true)
	string_world_size_entry["world_size_cells"] = {
		"x": str(world_size_cells.x),
		"y": world_size_cells.y,
	}
	_assert_true(
		serializer.deserialize_save_index_entry(string_world_size_entry).is_empty(),
		"save index entry 不应接受字符串 world_size_cells 轴值。"
	)

	var non_string_display_entry := valid_entry.duplicate(true)
	non_string_display_entry["display_name"] = 123
	_assert_true(
		serializer.deserialize_save_index_entry(non_string_display_entry).is_empty(),
		"save index entry 不应把非字符串 display_name 字段转成正式字段。"
	)

	var bad_entry_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.WRITE, SAVE_FILE_COMPRESSION_MODE)
	_assert_true(bad_entry_file != null, "应能写入坏 save index entry 夹具。")
	if bad_entry_file != null:
		bad_entry_file.store_var(serializer.minimize_save_payload_strings({
			"version": SAVE_INDEX_VERSION,
			"saves": [string_timestamp_entry],
		}), false)
		bad_entry_file.close()

	var rebuilt_from_bad_entry_slots := game_session.list_save_slots()
	_assert_true(not rebuilt_from_bad_entry_slots.is_empty(), "坏 save index entry 被拒绝后，应能从正式 save payload 重建列表。")
	_assert_save_index_file_uses_current_schema("坏 save index entry 被拒绝后")

	var old_shape_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	_assert_true(old_shape_file != null, "应能写入旧 top-level Array save index 夹具。")
	if old_shape_file != null:
		old_shape_file.store_string(JSON.stringify(serialized_entries))
		old_shape_file.close()

	var save_slots := game_session.list_save_slots()
	_assert_true(not save_slots.is_empty(), "旧 top-level Array index 被拒绝后，应能从正式 save payload 重建列表。")
	_assert_save_index_file_uses_current_schema("旧 top-level Array index 被拒绝后")

	var string_version_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.WRITE, SAVE_FILE_COMPRESSION_MODE)
	_assert_true(string_version_file != null, "应能写入字符串 version 的 save index 夹具。")
	if string_version_file != null:
		string_version_file.store_var(serializer.minimize_save_payload_strings({
			"version": str(SAVE_INDEX_VERSION),
			"saves": serialized_entries,
		}), false)
		string_version_file.close()

	var string_version_slots := game_session.list_save_slots()
	_assert_true(not string_version_slots.is_empty(), "字符串 version index 被拒绝后，应能从正式 save payload 重建列表。")
	_assert_save_index_file_uses_current_schema("字符串 version index 被拒绝后")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "save index schema 回归结束后应能清理 save 目录。")
	game_session.queue_free()
	await process_frame


func _test_bak_only_restore_for_save_payload() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	root.add_child(game_session)
	await process_frame

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "bak restore 回归前应能清理旧存档目录。")
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "bak restore 回归应能创建测试存档。")
	if create_error != OK:
		game_session.queue_free()
		await process_frame
		return

	var save_id := String(game_session.get_active_save_id())
	var save_path := String(game_session.get_active_save_path())
	var bak_path := "%s.bak" % save_path
	var rename_error := DirAccess.rename_absolute(ProjectSettings.globalize_path(save_path), ProjectSettings.globalize_path(bak_path))
	_assert_eq(rename_error, OK, "bak restore 回归前置：应能把正式 save 移成 .bak。")
	if rename_error == OK:
		_assert_true(not FileAccess.file_exists(save_path), "bak restore 前置：target 应暂时缺失。")
		_assert_true(FileAccess.file_exists(bak_path), "bak restore 前置：.bak 应存在。")
		var load_error := int(game_session.load_save(save_id))
		_assert_eq(load_error, OK, "target 缺失但有效 .bak 存在时，load_save() 应恢复并加载。")
		_assert_true(FileAccess.file_exists(save_path), "从 .bak 恢复后 target save 应重新存在。")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "bak restore 回归结束后应能清理 save 目录。")
	game_session.queue_free()
	await process_frame


func _test_index_rebuild_filters_payloads_that_fail_full_decode() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	root.add_child(game_session)
	await process_frame

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "bad payload index rebuild 回归前应能清理旧存档目录。")
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "bad payload index rebuild 回归应能创建测试存档。")
	if create_error != OK:
		game_session.queue_free()
		await process_frame
		return

	var payload := _build_payload_for_session(game_session)
	var world_state: Dictionary = (payload.get("world_state", {}) as Dictionary).duplicate(true)
	var world_data: Dictionary = (world_state.get("world_data", {}) as Dictionary).duplicate(true)
	world_data.erase("next_equipment_instance_serial")
	world_state["world_data"] = world_data
	payload["world_state"] = world_state
	var write_error := _overwrite_payload_at_path(game_session.get_active_save_path(), payload)
	_assert_eq(write_error, OK, "bad payload index rebuild 回归前置：应能写入坏 payload。")

	var corrupt_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	_assert_true(corrupt_file != null, "bad payload index rebuild 回归前置：应能写入坏 index 以触发 rebuild。")
	if corrupt_file != null:
		corrupt_file.store_buffer(PackedByteArray([0xCC, 0x80, 0x01, 0x02]))
		corrupt_file.close()
	game_session.queue_free()
	await process_frame

	var fresh_session = GAME_SESSION_SCRIPT.new()
	root.add_child(fresh_session)
	await process_frame
	var slots := fresh_session.list_save_slots()
	_assert_eq(slots.size(), 0, "index rebuild 不应收录无法完整 current-schema decode 的坏 payload。")

	var cleanup_error := int(fresh_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "bad payload index rebuild 回归结束后应能清理 save 目录。")
	fresh_session.queue_free()
	await process_frame


func _test_ensure_world_ready_skips_newest_bad_save() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	root.add_child(game_session)
	await process_frame

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "ensure fallback 回归前应能清理旧存档目录。")
	var old_create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(old_create_error, OK, "ensure fallback 回归应能创建旧测试存档。")
	if old_create_error != OK:
		game_session.queue_free()
		await process_frame
		return
	var old_save_id := String(game_session.get_active_save_id())
	var old_save_path := String(game_session.get_active_save_path())
	var old_payload := _build_payload_for_session(game_session)
	var old_meta := game_session.get_active_save_meta()
	old_meta["created_at_unix_time"] = 100
	old_meta["updated_at_unix_time"] = 100
	old_payload["save_slot_meta"] = old_meta.duplicate(true)
	_assert_eq(_overwrite_payload_at_path(old_save_path, old_payload), OK, "ensure fallback 前置：应能重写旧好档 meta。")

	var new_create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(new_create_error, OK, "ensure fallback 回归应能创建新测试存档。")
	if new_create_error != OK:
		game_session.clear_persisted_game()
		game_session.queue_free()
		await process_frame
		return
	var new_payload := _build_payload_for_session(game_session)
	var new_meta := game_session.get_active_save_meta()
	new_meta["created_at_unix_time"] = 200
	new_meta["updated_at_unix_time"] = 200
	new_payload["save_slot_meta"] = new_meta.duplicate(true)
	var new_world_state: Dictionary = (new_payload.get("world_state", {}) as Dictionary).duplicate(true)
	var new_world_data: Dictionary = (new_world_state.get("world_data", {}) as Dictionary).duplicate(true)
	new_world_data.erase("next_equipment_instance_serial")
	new_world_state["world_data"] = new_world_data
	new_payload["world_state"] = new_world_state
	_assert_eq(_overwrite_payload_at_path(game_session.get_active_save_path(), new_payload), OK, "ensure fallback 前置：应能写入新坏档。")

	var serializer = game_session._save_serializer
	var index_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.WRITE, SAVE_FILE_COMPRESSION_MODE)
	_assert_true(index_file != null, "ensure fallback 前置：应能写入双存档 index。")
	if index_file != null:
		var index_entries: Array[Dictionary] = [old_meta, new_meta]
		index_file.store_var(serializer.build_save_index_payload(index_entries), false)
		index_file.close()
	game_session.queue_free()
	await process_frame

	var fresh_session = GAME_SESSION_SCRIPT.new()
	root.add_child(fresh_session)
	await process_frame
	var ensure_error := int(fresh_session.ensure_world_ready(TEST_WORLD_CONFIG))
	_assert_eq(ensure_error, OK, "ensure_world_ready() 应在最新坏档失败后继续尝试旧好档。")
	_assert_eq(String(fresh_session.get_active_save_id()), old_save_id, "ensure_world_ready() 应加载同 config 下较旧的好档，而不是新建世界。")

	var cleanup_error := int(fresh_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "ensure fallback 回归结束后应能清理 save 目录。")
	fresh_session.queue_free()
	await process_frame


func _build_payload_for_session(game_session) -> Dictionary:
	var serializer = game_session._save_serializer
	return serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)


func _overwrite_payload_at_path(save_path: String, payload: Dictionary) -> int:
	var save_file := FileAccess.open_compressed(save_path, FileAccess.WRITE, SAVE_FILE_COMPRESSION_MODE)
	if save_file == null:
		return FileAccess.get_open_error()
	save_file.store_var(payload, false)
	save_file.close()
	return OK


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])


func _assert_save_index_file_uses_current_schema(context: String) -> void:
	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	_assert_true(index_file != null, "%s应能读取 save index。" % context)
	if index_file == null:
		return
	var raw_bytes := index_file.get_buffer(index_file.get_length())
	_assert_true(not raw_bytes.is_empty(), "%s重建后的 save index 不应为空。" % context)
	_assert_true(not _looks_like_json_text(raw_bytes), "%s重建后的 save index 不应是 JSON 文本。" % context)
	index_file.seek(0)
	var compressed_index_file := FileAccess.open_compressed(SAVE_INDEX_PATH, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
	_assert_true(compressed_index_file != null, "%s重建后的 save index 应能以压缩格式打开。" % context)
	if compressed_index_file == null:
		index_file.close()
		return
	var index_payload_variant = compressed_index_file.get_var(false)
	compressed_index_file.close()
	index_file.close()
	if index_payload_variant is not Dictionary:
		_test.fail("%s重建后的 save index 应是 Godot Variant 二进制 Dictionary。" % context)
		return
	var index_payload := index_payload_variant as Dictionary
	var version_variant: Variant = index_payload.get(&"version", null)
	_assert_true(version_variant is not String, "%s重建后的 save index version 不应保留字符串数字。" % context)
	_assert_eq(int(version_variant), SAVE_INDEX_VERSION, "%s重建后的 save index 应写入当前 version。" % context)
	_assert_true(index_payload.get(&"saves", null) is Array, "%s重建后的 save index 应使用当前 saves 数组字段。" % context)
	for raw_entry in index_payload.get(&"saves", []):
		if raw_entry is not Dictionary:
			_test.fail("%s重建后的 save index saves 只能包含 Dictionary entry。" % context)
			continue
		var entry := raw_entry as Dictionary
		_assert_true(entry.has(&"display_name"), "%s重建后的 save index 不应保留缺字段 entry。" % context)
		_assert_true(not entry.has(&"display_name_b64"), "%s重建后的 save index 不应继续使用 base64 JSON 字段。" % context)
		_assert_true(entry.get(&"created_at_unix_time") is not String, "%s重建后的 save index 不应保留字符串 created_at。" % context)
		_assert_true(entry.get(&"updated_at_unix_time") is not String, "%s重建后的 save index 不应保留字符串 updated_at。" % context)


func _looks_like_json_text(raw_bytes: PackedByteArray) -> bool:
	for byte_value in raw_bytes:
		var byte_int := int(byte_value)
		if byte_int == 9 or byte_int == 10 or byte_int == 13 or byte_int == 32:
			continue
		return byte_int == 123 or byte_int == 91
	return false
