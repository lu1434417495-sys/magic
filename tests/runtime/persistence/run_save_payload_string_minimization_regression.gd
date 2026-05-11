extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const SAVE_DIRECTORY := "user://saves"
const SAVE_FILE_COMPRESSION_MODE := FileAccess.COMPRESSION_ZSTD

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_save_payload_minimizes_identity_strings()

	if _failures.is_empty():
		print("Save payload string minimization regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Save payload string minimization regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_save_payload_minimizes_identity_strings() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "字符串最小化回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "字符串最小化回归需要已初始化的 SaveSerializer。")
	if serializer == null:
		_cleanup_test_session(game_session)
		return

	var payload: Dictionary = serializer.build_save_payload(
		game_session.get_active_save_id(),
		game_session.get_generation_config_path(),
		game_session.get_active_save_meta(),
		game_session.get_world_data(),
		game_session.get_player_coord(),
		game_session.get_player_faction_id(),
		game_session.get_party_state(),
		int(Time.get_unix_time_from_system())
	)
	_assert_no_string_variants(payload, "payload", "正式 save payload 不应保留 TYPE_STRING key 或 value。")
	_assert_binary_dictionary_file(
		"%s/%s.dat" % [SAVE_DIRECTORY, game_session.get_active_save_id()],
		"正式 slot save 文件"
	)

	_assert_type(payload.get(&"save_id", null), TYPE_STRING_NAME, "save_id 在正式 payload 中应保存为 StringName。")
	_assert_type(payload.get(&"generation_config_path", null), TYPE_STRING_NAME, "generation_config_path 在正式 payload 中应保存为 StringName。")
	var save_meta: Dictionary = payload.get(&"save_slot_meta", {})
	_assert_type(save_meta.get(&"display_name", null), TYPE_STRING_NAME, "save_slot_meta.display_name 在正式 payload 中应保存为 StringName。")

	var world_state: Dictionary = payload.get(&"world_state", {})
	_assert_type(world_state.get(&"player_faction_id", null), TYPE_STRING_NAME, "world_state.player_faction_id 应保存为 StringName。")
	var world_data: Dictionary = world_state.get(&"world_data", {})
	_assert_type(world_data.get(&"active_submap_id", null), TYPE_STRING_NAME, "world_data.active_submap_id 应保存为 StringName。")

	var settlements: Array = world_data.get(&"settlements", [])
	_assert_true(not settlements.is_empty(), "测试世界应至少生成一个据点用于检查 world_data 字符串最小化。")
	if not settlements.is_empty():
		var settlement: Dictionary = settlements[0]
		_assert_type(settlement.get(&"settlement_id", null), TYPE_STRING_NAME, "settlement_id 应保存为 StringName。")
		_assert_type(settlement.get(&"faction_id", null), TYPE_STRING_NAME, "settlement faction_id 应保存为 StringName。")
		_assert_type(settlement.get(&"display_name", null), TYPE_STRING_NAME, "settlement display_name 在正式 payload 中应保存为 StringName。")
		var facilities: Array = settlement.get(&"facilities", [])
		if not facilities.is_empty():
			var facility: Dictionary = facilities[0]
			_assert_type(facility.get(&"facility_id", null), TYPE_STRING_NAME, "facility_id 应保存为 StringName。")
			_assert_type(facility.get(&"slot_tag", null), TYPE_STRING_NAME, "facility slot_tag 应保存为 StringName。")
			_assert_type(facility.get(&"display_name", null), TYPE_STRING_NAME, "facility display_name 在正式 payload 中应保存为 StringName。")
		var services: Array = settlement.get(&"available_services", [])
		if not services.is_empty():
			var service: Dictionary = services[0]
			_assert_type(service.get(&"action_id", null), TYPE_STRING_NAME, "service action_id 应保存为 StringName。")
			_assert_type(service.get(&"interaction_script_id", null), TYPE_STRING_NAME, "service interaction_script_id 应保存为 StringName。")
			_assert_type(service.get(&"service_type", null), TYPE_STRING_NAME, "service_type 在正式 payload 中应保存为 StringName。")

	var encounters: Array = world_data.get(&"encounter_anchors", [])
	_assert_true(not encounters.is_empty(), "测试世界应至少生成一个 encounter_anchor 用于检查 ID 字段。")
	if not encounters.is_empty():
		var encounter: Dictionary = encounters[0]
		_assert_type(encounter.get(&"entity_id", null), TYPE_STRING_NAME, "encounter entity_id 应保存为 StringName。")
		_assert_type(encounter.get(&"encounter_kind", null), TYPE_STRING_NAME, "encounter_kind 应保存为 StringName。")
		_assert_type(encounter.get(&"display_name", null), TYPE_STRING_NAME, "encounter display_name 在正式 payload 中应保存为 StringName。")

	var party_payload: Dictionary = payload.get(&"party_state", {})
	_assert_type(party_payload.get(&"leader_member_id", null), TYPE_STRING_NAME, "party leader_member_id 应保存为 StringName。")
	_assert_array_item_type(party_payload.get(&"active_member_ids", []), TYPE_STRING_NAME, "active_member_ids 元素应保存为 StringName。")
	var member_states: Dictionary = party_payload.get(&"member_states", {})
	_assert_dictionary_keys_type(member_states, TYPE_STRING_NAME, "member_states 的成员 ID key 应保存为 StringName。")
	var main_member_id: StringName = game_session.get_party_state().main_character_member_id
	var member_payload: Dictionary = member_states.get(main_member_id, {})
	_assert_true(not member_payload.is_empty(), "应能用 StringName 成员 ID 读取成员 payload。")
	if not member_payload.is_empty():
		_assert_type(member_payload.get(&"member_id", null), TYPE_STRING_NAME, "member_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"display_name", null), TYPE_STRING_NAME, "member display_name 在正式 payload 中应保存为 StringName。")
		_assert_type(member_payload.get(&"race_id", null), TYPE_STRING_NAME, "member race_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"subrace_id", null), TYPE_STRING_NAME, "member subrace_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"age_profile_id", null), TYPE_STRING_NAME, "member age_profile_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"natural_age_stage_id", null), TYPE_STRING_NAME, "member natural_age_stage_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"effective_age_stage_id", null), TYPE_STRING_NAME, "member effective_age_stage_id 应保存为 StringName。")
		_assert_type(member_payload.get(&"body_size_category", null), TYPE_STRING_NAME, "member body_size_category 应保存为 StringName。")
		var progression: Dictionary = member_payload.get(&"progression", {})
		_assert_type(progression.get(&"unit_id", null), TYPE_STRING_NAME, "progression unit_id 应保存为 StringName。")
		_assert_array_item_type(progression.get(&"active_core_skill_ids", []), TYPE_STRING_NAME, "active_core_skill_ids 元素应保存为 StringName。")
		_assert_dictionary_keys_type(progression.get(&"skills", {}), TYPE_STRING_NAME, "skills 的 skill_id key 应保存为 StringName。")
		var skill_payloads: Dictionary = progression.get(&"skills", {})
		for skill_payload_variant in skill_payloads.values():
			if skill_payload_variant is not Dictionary:
				continue
			var skill_payload: Dictionary = skill_payload_variant
			_assert_type(skill_payload.get(&"granted_source_type", null), TYPE_STRING_NAME, "skill granted_source_type 应保存为 StringName。")
			_assert_type(skill_payload.get(&"granted_source_id", null), TYPE_STRING_NAME, "skill granted_source_id 应保存为 StringName。")
			break

	var decode_result: Dictionary = serializer.decode_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(int(decode_result.get("error", ERR_INVALID_DATA)), OK, "StringName 化后的 save payload 应继续能被 SaveSerializer 解码。")
	_assert_type(decode_result.get("active_save_id", null), TYPE_STRING, "解码后 active_save_id 应恢复为运行时 String。")
	var decoded_meta: Dictionary = decode_result.get("active_save_meta", {})
	_assert_type(decoded_meta.get("display_name", null), TYPE_STRING, "解码后 save meta display_name 应恢复为运行时 String。")

	_cleanup_test_session(game_session)


func _assert_type(value: Variant, expected_type: int, message: String) -> void:
	if typeof(value) != expected_type:
		_test.fail("%s | actual_type=%s expected_type=%s value=%s" % [
			message,
			type_string(typeof(value)),
			type_string(expected_type),
			var_to_str(value),
		])


func _assert_array_item_type(values: Variant, expected_type: int, message: String) -> void:
	if values is not Array:
		_test.fail("%s | actual container type=%s" % [message, type_string(typeof(values))])
		return
	for item in values:
		if typeof(item) != expected_type:
			_test.fail("%s | bad item type=%s value=%s" % [message, type_string(typeof(item)), var_to_str(item)])
			return


func _assert_dictionary_keys_type(values: Variant, expected_type: int, message: String) -> void:
	if values is not Dictionary:
		_test.fail("%s | actual container type=%s" % [message, type_string(typeof(values))])
		return
	for key in (values as Dictionary).keys():
		if typeof(key) != expected_type:
			_test.fail("%s | bad key type=%s key=%s" % [message, type_string(typeof(key)), var_to_str(key)])
			return


func _assert_no_string_variants(value: Variant, root_path: String, message: String) -> void:
	var string_paths: Array[String] = []
	_collect_string_variant_paths(value, root_path, string_paths)
	if string_paths.is_empty():
		return
	var preview := PackedStringArray()
	for index in range(mini(string_paths.size(), 8)):
		preview.append(string_paths[index])
	_test.fail("%s | count=%d examples=%s" % [
		message,
		string_paths.size(),
		", ".join(preview),
	])


func _collect_string_variant_paths(value: Variant, path: String, string_paths: Array[String]) -> void:
	var value_type := typeof(value)
	if value_type == TYPE_STRING or value_type == TYPE_PACKED_STRING_ARRAY:
		string_paths.append(path)
		return
	if value is Dictionary:
		var values: Dictionary = value
		for raw_key in values.keys():
			var key_label := String(raw_key)
			if typeof(raw_key) == TYPE_STRING:
				string_paths.append("%s.<key:%s>" % [path, key_label])
			_collect_string_variant_paths(values[raw_key], "%s.%s" % [path, key_label], string_paths)
		return
	if value is Array:
		var values_array: Array = value
		for index in range(values_array.size()):
			_collect_string_variant_paths(values_array[index], "%s[%d]" % [path, index], string_paths)


func _assert_binary_dictionary_file(path: String, context: String) -> void:
	var file := FileAccess.open(path, FileAccess.READ)
	_assert_true(file != null, "%s 应能打开：%s" % [context, path])
	if file == null:
		return
	var raw_bytes := file.get_buffer(file.get_length())
	_assert_true(not raw_bytes.is_empty(), "%s 不应为空。" % context)
	_assert_true(not _looks_like_json_text(raw_bytes), "%s 不应是 JSON 文本。" % context)
	file.close()
	var compressed_file := FileAccess.open_compressed(path, FileAccess.READ, SAVE_FILE_COMPRESSION_MODE)
	_assert_true(compressed_file != null, "%s 应能以 ZSTD 压缩格式打开。" % context)
	if compressed_file == null:
		return
	var payload_variant = compressed_file.get_var(false)
	compressed_file.close()
	_assert_true(payload_variant is Dictionary, "%s 应能以压缩 Godot Variant Dictionary 读回。" % context)


func _looks_like_json_text(raw_bytes: PackedByteArray) -> bool:
	for byte_value in raw_bytes:
		var byte_int := int(byte_value)
		if byte_int == 9 or byte_int == 10 or byte_int == 13 or byte_int == 32:
			continue
		return byte_int == 123 or byte_int == 91
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
