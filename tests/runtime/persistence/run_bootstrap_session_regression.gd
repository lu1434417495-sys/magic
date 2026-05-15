extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const LOGIN_SCREEN_SCENE = preload("res://scenes/main/login_screen.tscn")
const DISPLAY_SETTINGS_SERVICE_SCRIPT = preload("res://scripts/utils/display_settings_service.gd")
const WORLD_MAP_CONTENT_VALIDATOR_SCRIPT = preload("res://scripts/utils/world_map_content_validator.gd")
const UnitSkillProgress = preload("res://scripts/player/progression/unit_skill_progress.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"
const TEST_PRESET_ID := &"test"
const TEMP_SETTINGS_PATH := "user://bootstrap_display_settings_test.cfg"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class InvalidWorldContentValidator:
	extends RefCounted

	func validate_world_presets(_enemy_templates: Dictionary = {}, _wild_encounter_rosters: Dictionary = {}) -> Array[String]:
		return ["World content hard gate fixture error."]


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_display_settings_round_trip()
	_test_decode_payload_rejects_empty_world_data()
	_test_character_creation_body_size_uses_identity_rules()
	_test_character_creation_applies_identity_granted_skills()
	_test_starting_equipment_matches_random_skill()
	_test_game_session_rotates_log_boundary_on_create_load_unload()
	_test_game_session_content_getters_are_read_only_copies()
	_test_content_validation_failure_blocks_formal_runtime_entries()
	await _test_login_screen_blocks_character_creation_when_content_validation_fails()
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


func _test_decode_payload_rejects_empty_world_data() -> void:
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

	var decode_result: Dictionary = serializer.decode_payload(
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


func _test_character_creation_body_size_uses_identity_rules() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var payload := {
		"display_name": "Body Rule Hero",
		"reroll_count": 0,
		"strength": 10,
		"agility": 10,
		"constitution": 10,
		"perception": 10,
		"intelligence": 10,
		"willpower": 10,
		"race_id": &"human",
		"subrace_id": &"common_human",
		"age_years": 24,
		"birth_at_world_step": 0,
		"age_profile_id": &"human_age_profile",
		"natural_age_stage_id": &"adult",
		"effective_age_stage_id": &"adult",
		"effective_age_stage_source_type": &"",
		"effective_age_stage_source_id": &"",
		"body_size": 99,
		"body_size_category": &"large",
		"versatility_pick": &"",
		"active_stage_advancement_modifier_ids": [],
		"bloodline_id": &"",
		"bloodline_stage_id": &"",
		"ascension_id": &"",
		"ascension_stage_id": &"",
		"ascension_started_at_world_step": -1,
		"original_race_id_before_ascension": &"",
		"biological_age_years": 24,
		"astral_memory_years": 0,
	}
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG, &"", "", payload))
	_assert_eq(create_error, OK, "建卡体型规则回归前置：应能创建测试存档。")
	if create_error == OK:
		var party_state = game_session.get_party_state()
		var member = party_state.get_member_state(party_state.get_resolved_main_character_member_id()) if party_state != null else null
		_assert_true(member != null, "建卡体型规则回归前置：应能取得主角。")
		if member != null:
			_assert_eq(member.body_size_category, &"medium", "建卡落地应从 race/subrace 规则解析最终 body_size_category，不信任 payload int/category。")
			_assert_eq(member.body_size, 2, "建卡落地应通过 BodySizeRules 从 category 派生 body_size。")
			_assert_eq(int(member.progression.character_level), 0, "建卡创建的主角应从 0 级开始。")
			_assert_eq(int(member.progression.unit_base_attributes.get_attribute_value(&"hp_max")), 14, "建卡创建的 0 级主角应按 14 + 体质调整值*2 写入初始生命上限。")
			_assert_eq(int(member.current_hp), 14, "建卡创建的 0 级主角当前生命应等于初始生命上限。")
	_cleanup_test_session(game_session)


func _test_character_creation_applies_identity_granted_skills() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var payload := {
		"display_name": "Dragonborn Hero",
		"reroll_count": 0,
		"strength": 10,
		"agility": 10,
		"constitution": 10,
		"perception": 10,
		"intelligence": 10,
		"willpower": 10,
		"race_id": &"dragonborn",
		"subrace_id": &"red_dragonborn",
		"age_years": 24,
		"birth_at_world_step": 0,
		"age_profile_id": &"dragonborn_age_profile",
		"natural_age_stage_id": &"adult",
		"effective_age_stage_id": &"adult",
		"effective_age_stage_source_type": &"",
		"effective_age_stage_source_id": &"",
		"body_size": 99,
		"body_size_category": &"large",
		"versatility_pick": &"",
		"active_stage_advancement_modifier_ids": [],
		"bloodline_id": &"",
		"bloodline_stage_id": &"",
		"ascension_id": &"",
		"ascension_stage_id": &"",
		"ascension_started_at_world_step": -1,
		"original_race_id_before_ascension": &"",
		"biological_age_years": 24,
		"astral_memory_years": 0,
	}
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG, &"", "", payload))
	_assert_eq(create_error, OK, "建卡身份授予技能回归前置：应能创建测试存档。")
	if create_error == OK:
		var party_state = game_session.get_party_state()
		var member = party_state.get_member_state(party_state.get_resolved_main_character_member_id()) if party_state != null else null
		_assert_true(member != null, "建卡身份授予技能回归前置：应能取得主角。")
		if member != null:
			_assert_eq(member.race_id, &"dragonborn", "建卡落地应保留 payload 指定的 race_id。")
			_assert_eq(member.subrace_id, &"red_dragonborn", "建卡落地应保留 payload 指定的 subrace_id。")
			var skill_progress = member.progression.get_skill_progress(&"dragon_breath_fire_cone")
			_assert_true(skill_progress != null, "建卡落地后应立即补授亚种技能，不需要等下一次读档。")
			if skill_progress != null:
				_assert_true(skill_progress.is_learned, "Red Dragonborn 火焰吐息应为已学会状态。")
				_assert_eq(skill_progress.granted_source_type, UnitSkillProgress.GRANTED_SOURCE_SUBRACE, "Red Dragonborn 火焰吐息来源类型应为 subrace。")
				_assert_eq(skill_progress.granted_source_id, &"red_dragonborn", "Red Dragonborn 火焰吐息来源 id 应为 red_dragonborn。")
	_cleanup_test_session(game_session)


func _test_starting_equipment_matches_random_skill() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "随机起始装备回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var party_state = game_session.get_party_state()
	var member = party_state.get_member_state(party_state.get_resolved_main_character_member_id()) if party_state != null else null
	_assert_true(member != null, "随机起始装备回归前置：应能取得新建主角。")
	if member != null:
		var random_skill_def = _find_random_starting_skill_def(game_session, member)
		_assert_true(random_skill_def != null, "新建主角应记录一条 player 来源的随机起始技能。")
		var expected_item_id := _expected_starting_weapon_for_skill(game_session, random_skill_def)
		var equipped_item_id: StringName = member.equipment_state.get_equipped_item_id(&"main_hand")
		_assert_eq(
			String(equipped_item_id),
			String(expected_item_id),
			"随机起始技能类型应匹配主手基础装备。 skill_id=%s" % String(random_skill_def.skill_id if random_skill_def != null else &"")
		)
		_assert_true(
			member.equipment_state.get_equipped_instance_id(&"main_hand") != &"",
			"随机起始装备应写入持久装备实例 ID。"
		)
		if equipped_item_id == &"ash_shortbow" or equipped_item_id == &"militia_light_crossbow":
			_assert_eq(
				String(member.equipment_state.get_equipped_item_id(&"off_hand")),
				String(equipped_item_id),
				"双手远程起始武器应同步占用副手。"
			)
	_cleanup_test_session(game_session)


func _test_game_session_rotates_log_boundary_on_create_load_unload() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var initial_log_path := game_session.get_active_log_file_path()
	_assert_eq(initial_log_path, "", "GameSession 默认不应持有日志文件路径。")
	_assert_eq(
		bool(game_session.get_log_snapshot().get("file_output_enabled", true)),
		false,
		"GameSession 默认应关闭运行日志文件输出。"
	)

	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "日志边界回归前置：应能创建测试存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return

	var create_log_path := game_session.get_active_log_file_path()
	_assert_eq(create_log_path, "", "创建新存档后默认不应写 session jsonl 日志文件。")
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
		_assert_eq(load_log_path, "", "加载存档后默认不应写 session jsonl 日志文件。")
		var load_logs := game_session.get_recent_logs(4)
		_assert_eq(int(load_logs.size()), 1, "加载存档后的日志缓冲应重新开始计数。")
		if load_logs.size() == 1:
			_assert_eq(String(load_logs[0].get("event_id", "")), "session.save.load.ok", "加载存档后的首条日志应记录 load.ok。")
			_assert_eq(int(load_logs[0].get("seq", 0)), 1, "加载存档后的日志序号应重新从 1 开始。")

		game_session.unload_active_world()
		_assert_true(not game_session.has_active_world(), "卸载世界后不应继续保留 active world。")
		var unload_log_path := game_session.get_active_log_file_path()
		_assert_eq(unload_log_path, "", "卸载世界后默认不应写 session jsonl 日志文件。")
		var unload_logs := game_session.get_recent_logs(4)
		_assert_eq(int(unload_logs.size()), 1, "卸载世界后的日志缓冲应重新开始计数。")
		if unload_logs.size() == 1:
			_assert_eq(String(unload_logs[0].get("event_id", "")), "session.runtime.unload.ok", "卸载世界后的首条日志应记录 unload.ok。")
			_assert_eq(int(unload_logs[0].get("seq", 0)), 1, "卸载世界后的日志序号应重新从 1 开始。")

	var cleanup_error := int(game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "日志边界回归结束后应能清理存档目录。")
	game_session.free()


func _test_game_session_content_getters_are_read_only_copies() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()

	var skill_defs := game_session.get_skill_defs()
	skill_defs[&"_test_mutated_skill"] = Resource.new()
	_assert_true(
		not game_session.get_skill_defs().has(&"_test_mutated_skill"),
		"GameSession.get_skill_defs() 应返回只读副本，外部写入不应污染 registry。"
	)

	var item_defs := game_session.get_item_defs()
	item_defs[&"_test_mutated_item"] = Resource.new()
	_assert_true(
		not game_session.get_item_defs().has(&"_test_mutated_item"),
		"GameSession.get_item_defs() 应返回只读副本，外部写入不应污染 registry。"
	)

	var bundle := game_session.get_progression_content_bundle()
	var bundled_skills: Dictionary = bundle.get("skill_defs", {}) if bundle.get("skill_defs", {}) is Dictionary else {}
	bundled_skills[&"_test_mutated_bundle_skill"] = Resource.new()
	var fresh_bundle := game_session.get_progression_content_bundle()
	var fresh_bundled_skills: Dictionary = fresh_bundle.get("skill_defs", {}) if fresh_bundle.get("skill_defs", {}) is Dictionary else {}
	_assert_true(
		not fresh_bundled_skills.has(&"_test_mutated_bundle_skill"),
		"Progression content bundle 应返回只读副本，外部写入不应污染 registry。"
	)

	var fixture_skill := Resource.new()
	var fixture_error := int(game_session.install_test_content_def(&"skill", &"_fixture_skill", fixture_skill))
	_assert_eq(fixture_error, OK, "测试应通过显式 fixture API 注入临时内容，而不是改 getter 返回值。")
	_assert_true(
		game_session.get_skill_defs().get(&"_fixture_skill") == fixture_skill,
		"显式 fixture API 应能把临时技能内容注入 GameSession 测试上下文。"
	)

	game_session.free()


func _test_content_validation_failure_blocks_formal_runtime_entries() -> void:
	var source_session = GAME_SESSION_SCRIPT.new()
	var clear_error := int(source_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "内容硬门禁回归前置：应能清理旧存档目录。")
	var create_error := int(source_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(create_error, OK, "内容硬门禁回归前置：正式内容应能创建测试存档。")
	var save_id := String(source_session.get_active_save_id())
	source_session.free()
	if create_error != OK or save_id.is_empty():
		return

	var blocked_session = GAME_SESSION_SCRIPT.new()
	blocked_session._world_content_validator = InvalidWorldContentValidator.new()
	var validation_snapshot: Dictionary = blocked_session.refresh_content_validation_snapshot()
	_assert_true(not bool(validation_snapshot.get("ok", true)), "坏内容夹具应让 GameSession validation snapshot 失败。")

	var blocked_create_error := int(blocked_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_eq(blocked_create_error, ERR_INVALID_DATA, "内容校验失败时 create_new_save() 应硬阻断正式运行链。")
	_assert_true(not blocked_session.has_active_world(), "内容校验失败阻断新建后不应激活世界。")

	var blocked_load_error := int(blocked_session.load_save(save_id))
	_assert_eq(blocked_load_error, ERR_INVALID_DATA, "内容校验失败时 load_save() 应硬阻断正式运行链。")
	_assert_true(not blocked_session.has_active_world(), "内容校验失败阻断读档后不应激活世界。")

	var blocked_ensure_error := int(blocked_session.ensure_world_ready(TEST_WORLD_CONFIG))
	_assert_eq(blocked_ensure_error, ERR_INVALID_DATA, "内容校验失败时 ensure_world_ready() 应硬阻断正式运行链。")
	_assert_true(not blocked_session.has_active_world(), "内容校验失败阻断自动准备世界后不应激活世界。")

	var cleanup_error := int(blocked_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "内容硬门禁回归结束后应能清理 save 目录。")
	blocked_session.free()


func _test_login_screen_blocks_character_creation_when_content_validation_fails() -> void:
	var shared_game_session = _get_shared_game_session()
	_assert_true(shared_game_session != null, "登录壳内容门禁回归前置：SceneTree 应提供共享 GameSession。")
	if shared_game_session == null:
		return
	shared_game_session._world_content_validator = InvalidWorldContentValidator.new()

	var login_screen = LOGIN_SCREEN_SCENE.instantiate()
	root.add_child(login_screen)
	await process_frame

	login_screen._on_test_button_pressed()
	_assert_true(
		not login_screen.character_creation_window.visible,
		"内容校验失败时测试地图入口不应打开建卡窗口。"
	)
	_assert_eq(login_screen._pending_start_type, &"", "内容校验失败时不应设置 pending start type。")
	_assert_eq(login_screen._pending_preset_id, &"", "内容校验失败时不应设置 pending preset id。")
	_assert_true(
		String(login_screen.status_label.text).contains("内容校验失败，无法开始建卡"),
		"内容校验失败时登录页应显示无法开始建卡的明确提示。"
	)

	login_screen.character_creation_window.hide_window()
	login_screen._pending_start_type = &""
	login_screen._pending_preset_id = &""
	login_screen._on_world_preset_confirmed(login_screen.DEFAULT_START_PRESET_ID)
	_assert_true(
		not login_screen.character_creation_window.visible,
		"内容校验失败时正式世界预设确认也不应打开建卡窗口。"
	)
	_assert_eq(login_screen._pending_start_type, &"", "正式预设路径被内容校验阻断后不应设置 pending start type。")
	_assert_eq(login_screen._pending_preset_id, &"", "正式预设路径被内容校验阻断后不应设置 pending preset id。")

	shared_game_session._world_content_validator = WORLD_MAP_CONTENT_VALIDATOR_SCRIPT.new()
	login_screen._on_test_button_pressed()
	_assert_true(login_screen.character_creation_window.visible, "内容恢复合法后测试地图入口应能正常打开建卡窗口。")
	_assert_eq(login_screen._pending_start_type, login_screen.PENDING_START_TYPE_PRESET, "内容恢复合法后应设置 pending start type。")
	_assert_eq(login_screen._pending_preset_id, TEST_PRESET_ID, "内容恢复合法后应设置 pending preset id。")

	login_screen.queue_free()
	await process_frame
	shared_game_session._world_content_validator = WORLD_MAP_CONTENT_VALIDATOR_SCRIPT.new()


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
		var party_state = shared_game_session.get_party_state()
		var member = party_state.get_member_state(party_state.get_resolved_main_character_member_id()) if party_state != null else null
		_assert_true(member != null, "登录壳测试地图入口应能取得新建主角。")
		if member != null:
			_assert_eq(int(member.progression.character_level), 0, "登录壳测试地图入口创建的主角应从 0 级开始。")

	login_screen.queue_free()
	await process_frame
	var cleanup_error := int(shared_game_session.clear_persisted_game())
	_assert_eq(cleanup_error, OK, "登录壳测试入口回归结束后应能清理旧存档目录。")


func _find_random_starting_skill_def(game_session, member):
	if game_session == null or member == null or member.progression == null:
		return null
	var skill_defs: Dictionary = game_session.get_skill_defs()
	for skill_key in member.progression.skills.keys():
		var skill_progress = member.progression.get_skill_progress(StringName(skill_key))
		if skill_progress == null or not skill_progress.is_learned:
			continue
		if skill_progress.granted_source_type != UnitSkillProgress.GRANTED_SOURCE_PLAYER:
			continue
		if skill_progress.granted_source_id != &"":
			continue
		return skill_defs.get(skill_progress.skill_id)
	return null


func _expected_starting_weapon_for_skill(game_session, skill_def) -> StringName:
	var candidates: Array[StringName] = []
	if _skill_matches(skill_def, [&"crossbow"], ["crossbow"]):
		candidates.append(&"militia_light_crossbow")
	if _skill_matches(skill_def, [&"archer", &"bow"], ["archer_"]):
		candidates.append(&"ash_shortbow")
	if _skill_matches(skill_def, [&"mage", &"magic", &"spell"], ["mage_"]):
		candidates.append(&"oak_quarterstaff")
	if _skill_matches(skill_def, [&"priest", &"faith", &"heal"], ["priest_", "saint_"]):
		candidates.append(&"watchman_mace")
	if _skill_matches(skill_def, [&"warrior", &"melee", &"shield"], ["warrior_"]):
		candidates.append(&"steel_longsword")
	candidates.append(&"steel_longsword")
	return _first_valid_weapon_item_id(game_session, candidates)


func _skill_matches(skill_def, tag_ids: Array[StringName], skill_id_prefixes: Array[String]) -> bool:
	if skill_def == null:
		return false
	for tag_id in tag_ids:
		if skill_def.tags.has(tag_id):
			return true
	var skill_id_text := String(skill_def.skill_id)
	for prefix in skill_id_prefixes:
		if skill_id_text.begins_with(prefix):
			return true
	return false


func _first_valid_weapon_item_id(game_session, candidates: Array[StringName]) -> StringName:
	var item_defs: Dictionary = game_session.get_item_defs() if game_session != null else {}
	for item_id in candidates:
		var item_def = item_defs.get(item_id)
		if item_def != null and item_def.is_weapon():
			return item_id
	return &""


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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, var_to_str(actual), var_to_str(expected)])
