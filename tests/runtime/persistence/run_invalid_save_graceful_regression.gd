extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const INVALID_GENERATION_CONFIG_PATH := "user://invalid_generation_config_resource.tres"
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_create_new_save_rejects_invalid_generation_config_without_quit()
	_test_create_new_save_failure_preserves_previous_active_world()
	_test_load_save_rejects_bad_world_data_without_quit()
	_test_fresh_session_load_rejects_bad_world_data_without_quit()
	_test_load_save_rejects_bad_equipment_instance_without_quit()
	_test_load_save_repairs_memory_without_implicit_disk_write()
	_test_load_save_rejects_foreign_payload_save_id()
	_test_decode_payload_rejects_request_meta_mismatch()
	_test_decode_payload_rejects_extra_root_world_state_and_meta_fields()
	_test_save_id_filename_token_validator()
	_test_save_meta_rejects_extra_fields()
	_test_world_data_requires_exact_int_fields()
	_test_world_data_rejects_extra_top_level_fields()
	_test_world_data_nested_current_schema_rejects_extra_fields()
	_test_generated_submap_bad_world_data_rejects_root_world()
	_test_mounted_submap_rejects_extra_fields()
	_test_save_index_version_requires_exact_int()

	if _failures.is_empty():
		print("Invalid save graceful regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Invalid save graceful regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_create_new_save_rejects_invalid_generation_config_without_quit() -> void:
	_remove_user_file_if_exists(INVALID_GENERATION_CONFIG_PATH)
	var save_resource_error := ResourceSaver.save(Resource.new(), INVALID_GENERATION_CONFIG_PATH)
	_assert_eq(save_resource_error, OK, "坏 generation config 回归前置：应能写入可加载但类型错误的资源。")
	if save_resource_error != OK:
		return

	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(INVALID_GENERATION_CONFIG_PATH))
	_assert_eq(create_error, ERR_CANT_OPEN, "类型错误的 generation config 应通过 create_new_save() 返回错误，不应中止进程。")
	_assert_eq(game_session.has_active_world(), false, "类型错误的 generation config 不应留下 active world。")
	_cleanup_test_session(game_session)
	_remove_user_file_if_exists(INVALID_GENERATION_CONFIG_PATH)


func _test_create_new_save_failure_preserves_previous_active_world() -> void:
	_remove_user_file_if_exists(INVALID_GENERATION_CONFIG_PATH)
	var save_resource_error := ResourceSaver.save(Resource.new(), INVALID_GENERATION_CONFIG_PATH)
	_assert_eq(save_resource_error, OK, "建档失败回滚回归前置：应能写入类型错误的 generation config。")
	if save_resource_error != OK:
		return

	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "建档失败回滚回归前置：应能先创建有效存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		_remove_user_file_if_exists(INVALID_GENERATION_CONFIG_PATH)
		return

	var previous_save_id := String(game_session.get_active_save_id())
	var previous_save_path := String(game_session.get_active_save_path())
	var failed_create_error := int(game_session.create_new_save(INVALID_GENERATION_CONFIG_PATH))
	_assert_eq(failed_create_error, ERR_CANT_OPEN, "类型错误的 generation config 应返回错误。")
	_assert_true(game_session.has_active_world(), "创建新存档失败不应卸载原 active world。")
	_assert_eq(String(game_session.get_active_save_id()), previous_save_id, "创建新存档失败后应保留原 active save id。")
	_assert_eq(String(game_session.get_active_save_path()), previous_save_path, "创建新存档失败后应保留原 active save path。")

	_cleanup_test_session(game_session)
	_remove_user_file_if_exists(INVALID_GENERATION_CONFIG_PATH)


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
			"rarity": 0,
			"current_durability": 0,
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


func _test_load_save_repairs_memory_without_implicit_disk_write() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "读档只读修复回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var payload := _build_payload_for_session(game_session)
	var party_state: Dictionary = (payload.get("party_state", {}) as Dictionary).duplicate(true)
	var member_states: Dictionary = (party_state.get("member_states", {}) as Dictionary).duplicate(true)
	_assert_true(not member_states.is_empty(), "读档只读修复回归前置：测试队伍应至少有一个成员。")
	if member_states.is_empty():
		_cleanup_test_session(game_session)
		return

	var member_key = member_states.keys()[0]
	var member_payload: Dictionary = (member_states.get(member_key, {}) as Dictionary).duplicate(true)
	member_payload["body_size"] = 1
	member_payload["body_size_category"] = "small"
	member_states[member_key] = member_payload
	party_state["member_states"] = member_states
	payload["party_state"] = party_state

	var write_error := _overwrite_active_save_payload(game_session, payload)
	_assert_eq(write_error, OK, "读档只读修复回归前置：应能写入带派生体型旧值的 payload。")
	if write_error == OK:
		var load_error := int(game_session.load_save(game_session.get_active_save_id()))
		_assert_eq(load_error, OK, "带派生体型旧值的 payload 应能加载并在内存中修复。")
		var loaded_party = game_session.get_party_state()
		var loaded_member = loaded_party.get_member_state(StringName(member_key)) if loaded_party != null else null
		_assert_true(loaded_member != null, "读档只读修复后应能取得原成员。")
		if loaded_member != null:
			_assert_eq(loaded_member.body_size_category, &"medium", "读档内存态应从身份规则修复 body_size_category。")
			_assert_eq(int(loaded_member.body_size), 2, "读档内存态应从身份规则修复 body_size。")

		var disk_payload := _read_active_save_payload(game_session)
		var disk_party: Dictionary = (disk_payload.get("party_state", {}) as Dictionary)
		var disk_members: Dictionary = (disk_party.get("member_states", {}) as Dictionary)
		var disk_member: Dictionary = (disk_members.get(member_key, {}) as Dictionary)
		_assert_eq(String(disk_member.get("body_size_category", "")), "small", "load_save() 不应把 post-decode 修复隐式写回磁盘。")
		_assert_eq(int(disk_member.get("body_size", 0)), 1, "load_save() 不应把派生 body_size 修复隐式写回磁盘。")

	_cleanup_test_session(game_session)


func _test_load_save_rejects_foreign_payload_save_id() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "foreign save_id 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var original_save_id := String(game_session.get_active_save_id())
	var foreign_save_id := "%s_foreign" % original_save_id
	var payload := _build_payload_for_session(game_session)
	payload["save_id"] = foreign_save_id
	var slot_meta: Dictionary = (payload.get("save_slot_meta", {}) as Dictionary).duplicate(true)
	slot_meta["save_id"] = foreign_save_id
	payload["save_slot_meta"] = slot_meta

	var write_error := _overwrite_active_save_payload(game_session, payload)
	_assert_eq(write_error, OK, "foreign save_id 回归前置：应能写入损坏 payload。")
	if write_error == OK:
		var load_error := int(game_session.load_save(original_save_id))
		_assert_eq(load_error, ERR_INVALID_DATA, "请求 A 但 payload/meta 指向 B 时应拒绝加载。")
		_assert_eq(String(game_session.get_active_save_id()), original_save_id, "foreign payload 被拒后不应把 active save id 改成 B。")

	_cleanup_test_session(game_session)


func _test_decode_payload_rejects_request_meta_mismatch() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "request meta mismatch 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var payload := _build_payload_for_session(game_session)
	var generation_config = game_session.get_generation_config()
	var mismatched_meta := game_session.get_active_save_meta()
	mismatched_meta["save_id"] = "%s_other" % String(mismatched_meta.get("save_id", ""))
	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		generation_config,
		mismatched_meta
	)
	_assert_eq(int(decode_result.get("error", OK)), ERR_INVALID_DATA, "decode_payload 必须校验调用方传入的 save_meta.save_id。")

	var generation_mismatch_meta := game_session.get_active_save_meta()
	generation_mismatch_meta["generation_config_path"] = "res://data/configs/world_map/other_world_config.tres"
	var generation_decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		generation_config,
		generation_mismatch_meta
	)
	_assert_eq(int(generation_decode_result.get("error", OK)), ERR_INVALID_DATA, "decode_payload 必须校验调用方 save_meta.generation_config_path。")

	_cleanup_test_session(game_session)


func _test_decode_payload_rejects_extra_root_world_state_and_meta_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "payload strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var generation_config = game_session.get_generation_config()
	for field_case in [
		{"scope": "root", "field": "legacy_payload_field"},
		{"scope": "world_state", "field": "legacy_world_state_field"},
		{"scope": "meta", "field": "legacy_meta_field"},
	]:
		var payload := _build_payload_for_session(game_session)
		match String(field_case.get("scope", "")):
			"root":
				payload[String(field_case.get("field", ""))] = true
			"world_state":
				var world_state: Dictionary = (payload.get("world_state", {}) as Dictionary).duplicate(true)
				world_state[String(field_case.get("field", ""))] = true
				payload["world_state"] = world_state
			"meta":
				var payload_meta: Dictionary = (payload.get("meta", {}) as Dictionary).duplicate(true)
				payload_meta[String(field_case.get("field", ""))] = true
				payload["meta"] = payload_meta
		var decode_result: Dictionary = serializer.decode_payload(
			payload,
			game_session.get_generation_config_path(),
			generation_config,
			game_session.get_active_save_meta()
		)
		_assert_eq(
			int(decode_result.get("error", OK)),
			ERR_INVALID_DATA,
			"payload.%s 额外字段应按当前 schema 直接拒绝。" % String(field_case.get("scope", ""))
		)

	_cleanup_test_session(game_session)


func _test_save_id_filename_token_validator() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "save_id validator 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var valid_meta := game_session.get_active_save_meta()
	for bad_save_id in ["", " trim_me", "trim_me ", "nested/save", "nested\\save", "..", "save..escape"]:
		var bad_meta := valid_meta.duplicate(true)
		bad_meta["save_id"] = bad_save_id
		_assert_true(
			serializer.normalize_save_meta(bad_meta).is_empty(),
			"save_id 文件名 token 应拒绝非法片段：%s" % bad_save_id
		)

	_cleanup_test_session(game_session)


func _test_save_meta_rejects_extra_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "save meta strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var meta := game_session.get_active_save_meta()
	meta["legacy_alias"] = "old"
	_assert_true(
		serializer.normalize_save_meta(meta).is_empty(),
		"save meta 当前 schema 应拒绝额外字段，不做静默丢弃。"
	)

	_cleanup_test_session(game_session)


func _test_world_data_requires_exact_int_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "world_data strict int 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var base_world_data := game_session.get_world_data().duplicate(true)
	for field_name in ["map_seed", "next_equipment_instance_serial"]:
		for bad_value in ["123", 123.0, true]:
			var bad_world_data := base_world_data.duplicate(true)
			bad_world_data[field_name] = bad_value
			_assert_true(
				serializer.normalize_world_data(bad_world_data).is_empty(),
				"world_data.%s 必须是精确 int，不应接受 %s。" % [field_name, var_to_str(bad_value)]
			)

	_cleanup_test_session(game_session)


func _test_world_data_rejects_extra_top_level_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "world_data 顶层 strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var world_data := game_session.get_world_data().duplicate(true)
	world_data["legacy_world_cache"] = true
	_assert_true(
		serializer.normalize_world_data(world_data).is_empty(),
		"world_data 顶层额外字段应直接拒绝，不应静默保留或丢弃。"
	)

	_cleanup_test_session(game_session)


func _test_world_data_nested_current_schema_rejects_extra_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "world_data 嵌套 strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	for field_case in [
		{"scope": "settlements", "field": "legacy_settlement_field"},
		{"scope": "world_events", "field": "legacy_event_field"},
		{"scope": "submap_return_stack", "field": "legacy_return_field"},
	]:
		var world_data := game_session.get_world_data().duplicate(true)
		match String(field_case.get("scope", "")):
			"settlements":
				var settlements: Array = (world_data.get("settlements", []) as Array).duplicate(true)
				_assert_true(not settlements.is_empty(), "world_data 嵌套 strict 前置：测试世界应生成 settlement。")
				if settlements.is_empty():
					continue
				var settlement: Dictionary = (settlements[0] as Dictionary).duplicate(true)
				settlement[String(field_case.get("field", ""))] = true
				settlements[0] = settlement
				world_data["settlements"] = settlements
			"world_events":
				world_data["world_events"] = [_build_valid_world_event_payload_with_extra(String(field_case.get("field", "")))]
			"submap_return_stack":
				world_data["submap_return_stack"] = [{
					"map_id": "root",
					"coord": Vector2i.ZERO,
					String(field_case.get("field", "")): true,
				}]
		_assert_true(
			serializer.normalize_world_data(world_data).is_empty(),
			"world_data.%s 内额外字段应按当前 schema 直接拒绝。" % String(field_case.get("scope", ""))
		)

	_cleanup_test_session(game_session)


func _test_generated_submap_bad_world_data_rejects_root_world() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "generated submap strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var bad_submap_world := game_session.get_world_data().duplicate(true)
	bad_submap_world.erase("next_equipment_instance_serial")
	var root_world := game_session.get_world_data().duplicate(true)
	root_world["mounted_submaps"] = {
		"submap_bad": {
			"submap_id": "submap_bad",
			"display_name": "Bad Submap",
			"generation_config_path": TEST_WORLD_CONFIG,
			"return_hint_text": "",
			"is_generated": true,
			"player_coord": Vector2i.ZERO,
			"world_data": bad_submap_world,
		},
	}
	_assert_true(
		serializer.normalize_world_data(root_world).is_empty(),
		"已生成 submap 的坏 world_data 必须拒绝整份 root world_data。"
	)

	_cleanup_test_session(game_session)


func _test_mounted_submap_rejects_extra_fields() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "mounted_submap strict 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	var root_world := game_session.get_world_data().duplicate(true)
	root_world["mounted_submaps"] = {
		"submap_extra": {
			"submap_id": "submap_extra",
			"display_name": "Extra Submap",
			"generation_config_path": TEST_WORLD_CONFIG,
			"return_hint_text": "",
			"is_generated": false,
			"player_coord": Vector2i.ZERO,
			"world_data": {},
			"legacy_extra": true,
		},
	}
	_assert_true(
		serializer.normalize_world_data(root_world).is_empty(),
		"mounted_submaps entry 当前 schema 应拒绝额外字段。"
	)

	_cleanup_test_session(game_session)


func _test_save_index_version_requires_exact_int() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var serializer = game_session._save_serializer
	_assert_true(serializer.is_save_index_integer_value(3), "save index version 应接受 int。")
	_assert_true(not serializer.is_save_index_integer_value(3.0), "save index version 不应接受 float。")
	_assert_true(not serializer.is_save_index_integer_value("3"), "save index version 不应接受 string。")
	_assert_true(not serializer.is_save_index_integer_value(true), "save index version 不应接受 bool。")
	game_session.free()


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
	var save_file := FileAccess.open_compressed(save_path, FileAccess.WRITE, SAVE_FILE_COMPRESSION_MODE)
	if save_file == null:
		return FileAccess.get_open_error()
	save_file.store_var(payload, false)
	save_file.close()
	return OK


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


func _build_valid_world_event_payload_with_extra(extra_field_name: String) -> Dictionary:
	var event_payload := {
		"event_id": "event_schema_probe",
		"display_name": "Schema Probe",
		"world_coord": Vector2i.ZERO,
		"event_type": "enter_submap",
		"target_submap_id": "submap_schema_probe",
		"discovery_condition_id": "",
		"prompt_title": "Probe",
		"prompt_text": "Probe",
		"is_discovered": true,
	}
	event_payload[extra_field_name] = true
	return event_payload


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _remove_user_file_if_exists(path: String) -> void:
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(path))


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
