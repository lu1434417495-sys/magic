extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_load_save_rejects_bad_world_data_without_quit()
	_test_fresh_session_load_rejects_bad_world_data_without_quit()
	_test_load_save_rejects_bad_equipment_instance_without_quit()

	if _failures.is_empty():
		print("Invalid save graceful regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Invalid save graceful regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_load_save_rejects_bad_world_data_without_quit() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "坏 world_data 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var payload := _build_payload_for_session(game_session)
	var world_state: Dictionary = (payload.get("world_state", {}) as Dictionary).duplicate(true)
	var world_data: Dictionary = (world_state.get("world_data", {}) as Dictionary).duplicate(true)
	world_data.erase("next_equipment_instance_serial")
	world_state["world_data"] = world_data
	payload["world_state"] = world_state

	var write_error := _overwrite_active_save_payload(game_session, payload)
	_assert_eq(write_error, OK, "坏 world_data 回归前置：应能写入损坏存档 payload。")
	if write_error == OK:
		var load_error := int(game_session.load_save(game_session.get_active_save_id()))
		_assert_eq(load_error, ERR_INVALID_DATA, "坏 world_data 应通过 load_save() 返回 ERR_INVALID_DATA，不应中止进程。")

	_cleanup_test_session(game_session)


func _test_fresh_session_load_rejects_bad_world_data_without_quit() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "fresh load 坏档回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var save_id := String(game_session.get_active_save_id())
	var payload := _build_payload_for_session(game_session)
	var world_state: Dictionary = (payload.get("world_state", {}) as Dictionary).duplicate(true)
	var world_data: Dictionary = (world_state.get("world_data", {}) as Dictionary).duplicate(true)
	world_data.erase("next_equipment_instance_serial")
	world_state["world_data"] = world_data
	payload["world_state"] = world_state

	var write_error := _overwrite_active_save_payload(game_session, payload)
	_assert_eq(write_error, OK, "fresh load 坏档回归前置：应能写入损坏存档 payload。")
	game_session.free()
	if write_error != OK:
		var cleanup_session = GAME_SESSION_SCRIPT.new()
		_cleanup_test_session(cleanup_session)
		return

	var fresh_session = GAME_SESSION_SCRIPT.new()
	var load_error := int(fresh_session.load_save(save_id))
	_assert_eq(load_error, ERR_INVALID_DATA, "fresh GameSession 通过存档列表加载坏 world_data 时应返回 ERR_INVALID_DATA，不应中止进程。")
	_assert_eq(fresh_session.has_active_world(), false, "fresh GameSession 加载坏档失败后不应留下 active world。")

	_cleanup_test_session(fresh_session)


func _test_load_save_rejects_bad_equipment_instance_without_quit() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "坏装备实例回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var payload := _build_payload_for_session(game_session)
	var party_state: Dictionary = (payload.get("party_state", {}) as Dictionary).duplicate(true)
	var warehouse_state: Dictionary = (party_state.get("warehouse_state", {}) as Dictionary).duplicate(true)
	warehouse_state["equipment_instances"] = [
		{
			"instance_id": "eq_999999",
			"item_id": "bronze_sword",
			"current_durability": -1,
			"armor_wear_progress": 0.0,
			"weapon_wear_progress": 0.0,
		},
	]
	party_state["warehouse_state"] = warehouse_state
	payload["party_state"] = party_state

	var write_error := _overwrite_active_save_payload(game_session, payload)
	_assert_eq(write_error, OK, "坏装备实例回归前置：应能写入损坏存档 payload。")
	if write_error == OK:
		var load_error := int(game_session.load_save(game_session.get_active_save_id()))
		_assert_eq(load_error, ERR_INVALID_DATA, "坏 equipment instance payload 应通过 load_save() 返回 ERR_INVALID_DATA，不应中止进程。")

	_cleanup_test_session(game_session)


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


func _overwrite_active_save_payload(game_session, payload: Dictionary) -> int:
	var save_path: String = game_session.get_active_save_path()
	if save_path.is_empty():
		return ERR_INVALID_PARAMETER
	var save_file := FileAccess.open(save_path, FileAccess.WRITE)
	if save_file == null:
		return FileAccess.get_open_error()
	save_file.store_var(payload, false)
	save_file.close()
	return OK


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
