extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_DIRECTORY := "user://saves"
const SAVE_INDEX_PATH := "%s/index.dat" % SAVE_DIRECTORY

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_corrupt_save_index_recovers_cleanly()
	await _test_save_index_schema_rejects_old_entry_shapes()
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
	missing_display_entry.erase("display_name_b64")
	_assert_true(
		serializer.deserialize_save_index_entry(missing_display_entry).is_empty(),
		"缺失 display_name_b64 的旧 save index entry 应直接拒绝。"
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

	var non_string_b64_entry := valid_entry.duplicate(true)
	non_string_b64_entry["display_name_b64"] = 123
	_assert_true(
		serializer.deserialize_save_index_entry(non_string_b64_entry).is_empty(),
		"save index entry 不应把非字符串 base64 字段转成正式字段。"
	)

	var bad_entry_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	_assert_true(bad_entry_file != null, "应能写入坏 save index entry 夹具。")
	if bad_entry_file != null:
		bad_entry_file.store_string(JSON.stringify({
			"version": 1,
			"saves": [string_timestamp_entry],
		}))
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

	var string_version_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.WRITE)
	_assert_true(string_version_file != null, "应能写入字符串 version 的 save index 夹具。")
	if string_version_file != null:
		string_version_file.store_string(JSON.stringify({
			"version": "1",
			"saves": serialized_entries,
		}))
		string_version_file.close()

	var string_version_slots := game_session.list_save_slots()
	_assert_true(not string_version_slots.is_empty(), "字符串 version index 被拒绝后，应能从正式 save payload 重建列表。")
	_assert_save_index_file_uses_current_schema("字符串 version index 被拒绝后")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "save index schema 回归结束后应能清理 save 目录。")
	game_session.queue_free()
	await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])


func _assert_save_index_file_uses_current_schema(context: String) -> void:
	var index_file := FileAccess.open(SAVE_INDEX_PATH, FileAccess.READ)
	_assert_true(index_file != null, "%s应能读取 save index。" % context)
	if index_file == null:
		return
	var raw_text := index_file.get_as_text()
	index_file.close()
	var json := JSON.new()
	_assert_eq(json.parse(raw_text), OK, "%s重建后的 save index 应是 JSON。" % context)
	if json.data is not Dictionary:
		_failures.append("%s重建后的 save index 不应继续保持旧 top-level Array shape。" % context)
		return
	var index_payload := json.data as Dictionary
	var version_variant: Variant = index_payload.get("version", null)
	_assert_true(version_variant is not String, "%s重建后的 save index version 不应保留字符串数字。" % context)
	_assert_eq(int(version_variant), 1, "%s重建后的 save index 应写入当前 version。" % context)
	_assert_true(index_payload.get("saves", null) is Array, "%s重建后的 save index 应使用当前 saves 数组字段。" % context)
	for raw_entry in index_payload.get("saves", []):
		if raw_entry is not Dictionary:
			_failures.append("%s重建后的 save index saves 只能包含 Dictionary entry。" % context)
			continue
		var entry := raw_entry as Dictionary
		_assert_true(entry.has("display_name_b64"), "%s重建后的 save index 不应保留缺字段 entry。" % context)
		_assert_true(entry.get("created_at_unix_time") is not String, "%s重建后的 save index 不应保留字符串 created_at。" % context)
		_assert_true(entry.get("updated_at_unix_time") is not String, "%s重建后的 save index 不应保留字符串 updated_at。" % context)
