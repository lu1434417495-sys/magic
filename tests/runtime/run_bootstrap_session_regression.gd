extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const LOGIN_SCREEN_SCENE = preload("res://scenes/main/login_screen.tscn")
const DISPLAY_SETTINGS_SERVICE_SCRIPT = preload("res://scripts/utils/display_settings_service.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const TEST_PRESET_ID := &"test"
const TEMP_SETTINGS_PATH := "user://bootstrap_display_settings_test.cfg"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_display_settings_round_trip()
	_test_decode_v5_payload_rejects_empty_world_data()
	_test_game_session_rotates_log_boundary_on_create_load_unload()
	await _test_login_screen_test_entry_creates_generated_world()

	if _failures.is_empty():
		print("Bootstrap session regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Bootstrap session regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_display_settings_round_trip() -> void:
	_cleanup_file(TEMP_SETTINGS_PATH)
	var service = DISPLAY_SETTINGS_SERVICE_SCRIPT.new(TEMP_SETTINGS_PATH)
	var expected_settings := {
		"resolution": Vector2i(1920, 1080),
		"fullscreen": true,
	}
	var save_error := int(service.save_settings(expected_settings))
	_assert_eq(save_error, OK, "显示设置服务应能写入临时配置文件。")
	var loaded_settings: Dictionary = service.load_settings()
	_assert_eq(loaded_settings.get("resolution", Vector2i.ZERO), Vector2i(1920, 1080), "显示设置 round-trip 后应保留分辨率。")
	_assert_eq(bool(loaded_settings.get("fullscreen", false)), true, "显示设置 round-trip 后应保留全屏开关。")
	_cleanup_file(TEMP_SETTINGS_PATH)


func _test_decode_v5_payload_rejects_empty_world_data() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "空 world_data 回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var serializer = game_session._save_serializer
	_assert_true(serializer != null, "空 world_data 回归前置：GameSession 应暴露 SaveSerializer。")
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
	var world_state: Dictionary = (payload.get("world_state", {}) as Dictionary).duplicate(true)
	world_state["world_data"] = {}
	payload["world_state"] = world_state

	var decode_result: Dictionary = serializer.decode_v5_payload(
		payload,
		game_session.get_generation_config_path(),
		game_session.get_generation_config(),
		game_session.get_active_save_meta()
	)
	_assert_eq(
		int(decode_result.get("error", OK)),
		ERR_INVALID_DATA,
		"空 world_state.world_data 不应再被 normalize_world_data() 隐式放行。"
	)
	_cleanup_test_session(game_session)


func _test_game_session_rotates_log_boundary_on_create_load_unload() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var initial_log_path := game_session.get_active_log_file_path()
	_assert_true(not initial_log_path.is_empty(), "GameSession 初始化时应持有日志路径。")

	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "日志边界回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var create_log_path := game_session.get_active_log_file_path()
	_assert_true(create_log_path != initial_log_path, "创建新存档后应轮转到新的日志文件。")
	var create_logs := game_session.get_recent_logs(4)
	_assert_eq(int(create_logs.size()), 1, "新存档日志会话应从空缓冲开始。")
	if create_logs.size() == 1:
		_assert_eq(String(create_logs[0].get("event_id", "")), "session.save.create.ok", "新存档日志会话首条应记录 create.ok。")
		_assert_eq(int(create_logs[0].get("seq", 0)), 1, "新存档日志会话应从 seq=1 开始。")

	var save_id := game_session.get_active_save_id()
	var load_error := int(game_session.load_save(save_id))
	_assert_eq(load_error, OK, "日志边界回归前置：应能重新加载同一存档。")
	if load_error == OK:
		var load_log_path := game_session.get_active_log_file_path()
		_assert_true(load_log_path != create_log_path, "加载存档后应轮转到新的日志文件。")
		var load_logs := game_session.get_recent_logs(4)
		_assert_eq(int(load_logs.size()), 1, "加载存档后的日志缓冲应重新开始计数。")
		if load_logs.size() == 1:
			_assert_eq(String(load_logs[0].get("event_id", "")), "session.save.load.ok", "加载存档后的首条日志应记录 load.ok。")
			_assert_eq(int(load_logs[0].get("seq", 0)), 1, "加载存档后的日志序号应重新从 1 开始。")

		var pre_unload_log_path := load_log_path
		game_session.unload_active_world()
		_assert_true(not game_session.has_active_world(), "卸载世界后不应继续保留 active world。")
		var unload_log_path := game_session.get_active_log_file_path()
		_assert_true(unload_log_path != pre_unload_log_path, "卸载世界后应轮转到新的日志文件。")
		var unload_logs := game_session.get_recent_logs(4)
		_assert_eq(int(unload_logs.size()), 1, "卸载世界后的日志缓冲应重新开始计数。")
		if unload_logs.size() == 1:
			_assert_eq(String(unload_logs[0].get("event_id", "")), "session.runtime.unload.ok", "卸载世界后的首条日志应记录 unload.ok。")
			_assert_eq(int(unload_logs[0].get("seq", 0)), 1, "卸载世界后的日志序号应重新从 1 开始。")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "日志边界回归结束后应能清理存档目录。")
	game_session.free()


func _test_login_screen_test_entry_creates_generated_world() -> void:
	var shared_game_session = _get_shared_game_session()
	_assert_true(shared_game_session != null, "登录壳测试入口回归前置：SceneTree 应提供共享 GameSession。")
	if shared_game_session == null:
		return
	var clear_error := int(shared_game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "登录壳测试入口回归前置：应能清理旧存档目录。")

	var login_screen = LOGIN_SCREEN_SCENE.instantiate()
	root.add_child(login_screen)
	await process_frame

	_assert_true(
		String(login_screen.status_label.text).contains("创建测试世界"),
		"登录壳空闲提示应明确说明测试地图会创建测试世界。"
	)
	login_screen._on_test_button_pressed()
	_assert_eq(login_screen._pending_start_type, login_screen.PENDING_START_TYPE_PRESET, "测试地图按钮应进入正式预设建卡流程。")
	_assert_eq(login_screen._pending_preset_id, TEST_PRESET_ID, "测试地图按钮应把 test 预设交给正式建卡流程。")

	var create_error := int(login_screen._create_save_for_preset(TEST_PRESET_ID))
	_assert_eq(create_error, OK, "登录壳测试地图入口应通过 create_new_save 生成测试世界。")
	if create_error == OK:
		var active_meta: Dictionary = shared_game_session.get_active_save_meta()
		_assert_eq(String(active_meta.get("world_preset_id", "")), "test", "登录壳生成后的 save meta 应标记 test preset。")
		_assert_eq(String(active_meta.get("world_preset_name", "")), "测试", "登录壳生成后的 save meta 应保留 test preset 名称。")
		_assert_eq(shared_game_session.get_generation_config_path(), TEST_WORLD_CONFIG, "登录壳生成后的测试存档应使用 test world generation config。")
		_assert_eq(shared_game_session.list_save_slots().size(), 1, "登录壳测试地图入口应只生成一个新存档槽位。")
		var world_data: Dictionary = shared_game_session.get_world_data()
		_assert_true(int(world_data.get("map_seed", 0)) != 0, "测试地图应通过正式生成链分配运行时 map_seed。")
		_assert_true((world_data.get("settlements", []) as Array).size() > 0, "测试地图应通过正式生成链生成据点。")

	login_screen.queue_free()
	await process_frame
	var cleanup_error := int(shared_game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "登录壳测试入口回归结束后应能清理旧存档目录。")


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _cleanup_file(virtual_path: String) -> void:
	if virtual_path.is_empty():
		return
	var absolute_path := ProjectSettings.globalize_path(virtual_path)
	if FileAccess.file_exists(absolute_path):
		DirAccess.remove_absolute(absolute_path)


func _get_shared_game_session():
	if root == null:
		return null
	var existing = root.get_node_or_null("GameSession")
	if existing != null:
		return existing
	var game_session = GAME_SESSION_SCRIPT.new()
	game_session.name = "GameSession"
	root.add_child(game_session)
	return game_session


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
