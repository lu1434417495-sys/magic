extends SceneTree

const GameSessionScript = preload("res://scripts/systems/persistence/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")
const EncounterAnchorData = preload("res://scripts/systems/world/encounter_anchor_data.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _reset_session()
	await _test_world_confirm_key_split()
	await _reset_session()
	await _test_battle_reset_key_split_and_modal_block()
	await _reset_session()
	await _test_game_over_confirmation_ui_accept_and_scene_return()
	await _cleanup()

	if _failures.is_empty():
		print("World map input routing regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map input routing regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_world_confirm_key_split() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "world input routing 回归前置：应能创建测试世界。")
	if create_error != OK:
		return

	var world_map = await _instantiate_world_map()
	if world_map == null:
		return

	var runtime = world_map._runtime
	_assert_true(runtime != null, "world_map 场景应初始化 runtime。")
	if runtime == null:
		world_map.queue_free()
		await process_frame
		return

	var settlement_coord := _find_visible_settlement_coord(runtime)
	_assert_true(settlement_coord != Vector2i(-1, -1), "world input routing 回归需要一个可见据点。")
	if settlement_coord == Vector2i(-1, -1):
		world_map.queue_free()
		await process_frame
		return

	runtime.set_selected_coord(settlement_coord)
	world_map._render_from_runtime(false)
	await process_frame

	_send_key(world_map, KEY_SPACE)
	await process_frame
	_assert_eq(runtime.get_active_modal_id(), "", "世界态不应再由 Space 打开据点。")

	_send_key(world_map, KEY_ENTER)
	await process_frame
	_assert_eq(runtime.get_active_modal_id(), "settlement", "世界态应继续由 Enter 打开据点。")

	var status_before_block: String = runtime.get_status_text()
	_send_key(world_map, KEY_SPACE)
	await process_frame
	_send_key(world_map, KEY_ENTER)
	await process_frame
	_assert_eq(runtime.get_active_modal_id(), "settlement", "据点 modal 打开时应继续停留在 settlement。")
	_assert_eq(runtime.get_status_text(), status_before_block, "据点 modal 打开时确认键应被阻断，不应改写状态文本。")

	world_map.queue_free()
	await process_frame


func _test_battle_reset_key_split_and_modal_block() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "battle input routing 回归前置：应能创建测试世界。")
	if create_error != OK:
		return

	var world_map = await _instantiate_world_map()
	if world_map == null:
		return

	var runtime = world_map._runtime
	_assert_true(runtime != null, "world_map 场景应初始化 runtime。")
	if runtime == null:
		world_map.queue_free()
		await process_frame
		return

	var encounter_anchor = _find_encounter_anchor_by_kind(
		_game_session.get_world_data(),
		EncounterAnchorData.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "battle input routing 回归需要至少一个单体野怪遭遇。")
	if encounter_anchor == null:
		world_map.queue_free()
		await process_frame
		return

	_game_session.set_battle_save_lock(true)
	runtime.start_battle(encounter_anchor)
	world_map._render_from_runtime(true)
	await process_frame

	var pending_battle_state = runtime.get_battle_state()
	var modal_status_before: String = runtime.get_status_text()
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "开战后应进入 battle_start_confirm modal。")
	_assert_true(
		pending_battle_state != null
			and pending_battle_state.timeline != null
			and pending_battle_state.timeline.frozen,
		"battle_start_confirm 打开时 timeline 应保持冻结。"
	)

	_send_key(world_map, KEY_SPACE)
	await process_frame
	_send_key(world_map, KEY_ENTER)
	await process_frame
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "battle_start_confirm 打开时确认键应被阻断。")
	_assert_eq(runtime.get_status_text(), modal_status_before, "battle_start_confirm 打开时确认键不应改写状态文本。")
	_assert_true(
		pending_battle_state != null
			and pending_battle_state.timeline != null
			and pending_battle_state.timeline.frozen,
		"battle_start_confirm 打开时确认键不应解冻 timeline。"
	)

	var confirm_result: Dictionary = runtime.command_confirm_battle_start()
	_assert_true(bool(confirm_result.get("ok", false)), "battle_start_confirm 应能成功确认。")
	world_map._render_from_runtime(true)
	await process_frame
	await _await_battle_input_ready(world_map)
	var manual_turn_ready := await _advance_to_manual_battle_turn(runtime, world_map)
	_assert_true(manual_turn_ready, "确认开战后应能推进到可输入的手动行动阶段。")

	var battle_state = runtime.get_battle_state()
	_assert_true(battle_state != null, "确认开战后应存在 battle state。")
	if battle_state == null:
		world_map.queue_free()
		await process_frame
		return
	_assert_true(runtime.get_manual_battle_unit() != null, "推进 battle tick 后应出现手动行动单位。")

	var manual_unit = runtime.get_manual_battle_unit()
	_assert_true(manual_unit != null, "Space 复位回归需要一个手动行动单位。")
	if manual_unit != null:
		var log_count_before_space: int = battle_state.log_entries.size()
		var offset_coord: Vector2i = manual_unit.coord + Vector2i(1, 0)
		runtime.set_runtime_battle_selected_coord(offset_coord)
		_assert_eq(
			runtime.get_battle_selected_coord(),
			offset_coord,
			"Space 复位回归前置：应先把 battle selected coord 设置到非当前行动单位坐标。"
		)
		_send_key(world_map, KEY_SPACE)
		await process_frame
		_assert_eq(
			runtime.get_battle_selected_coord(),
			manual_unit.coord,
			"正式战斗中 Space 应复位到当前行动单位。"
		)
		_assert_eq(
			battle_state.log_entries.size(),
			log_count_before_space,
			"正式战斗中 Space 不应再触发等待/继续。"
		)

	var log_count_before_enter: int = battle_state.log_entries.size()
	_send_key(world_map, KEY_ENTER)
	await process_frame
	_assert_eq(runtime.get_active_modal_id(), "", "正式战斗中不应因 Enter 打开 modal。")
	_assert_true(
		battle_state.log_entries.size() > log_count_before_enter,
		"正式战斗中 Enter 应触发等待/继续。"
	)

	world_map.queue_free()
	await process_frame


func _test_game_over_confirmation_ui_accept_and_scene_return() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "game over adapter 回归前置：应能创建测试世界。")
	if create_error != OK:
		return

	var world_map = await _instantiate_world_map()
	if world_map == null:
		return

	var runtime = world_map._runtime
	_assert_true(runtime != null, "game over adapter 回归前置：world_map 场景应初始化 runtime。")
	if runtime == null:
		world_map.queue_free()
		await process_frame
		return

	runtime._activate_game_over({
		"title": "Game Over",
		"description": "主角已阵亡，本次旅程结束。",
		"confirm_text": "返回标题",
		"main_character_member_id": "player_sword_01",
		"main_character_name": "剑士",
		"main_character_dead": true,
	})
	world_map._render_from_runtime(false)
	await process_frame

	_assert_true(world_map.submap_entry_window.visible, "game over modal 激活后场景层应显示共享确认窗。")
	_assert_true(
		world_map.submap_entry_window.get_viewport().gui_get_focus_owner() == world_map.submap_entry_window.confirm_button,
		"game over 共享确认窗打开后应把焦点交给确认按钮。"
	)
	_assert_true(not world_map.submap_entry_window.cancel_button.visible, "game over 共享确认窗应隐藏取消按钮。")

	var accept_event := InputEventAction.new()
	accept_event.action = "ui_accept"
	accept_event.pressed = true
	world_map.submap_entry_window._unhandled_input(accept_event)
	await process_frame
	await process_frame

	var configured_main_scene := String(ProjectSettings.get_setting("application/run/main_scene", ""))
	_assert_true(not _game_session.has_active_world(), "game over 共享确认窗返回标题后应卸载当前 active world。")
	_assert_true(current_scene != null, "game over 共享确认窗返回标题后 SceneTree 应切到配置的启动场景。")
	_assert_eq(
		String(current_scene.scene_file_path) if current_scene != null else "",
		configured_main_scene,
		"共享确认窗 confirmed -> WorldMapSystem 应切到 project.godot 配置的启动场景。"
	)

	if current_scene != null:
		current_scene.queue_free()
		await process_frame


func _instantiate_world_map():
	var world_map = WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame
	return world_map


func _find_visible_settlement_coord(runtime) -> Vector2i:
	if runtime == null:
		return Vector2i(-1, -1)
	var fog_system = runtime.get_fog_system()
	var faction_id := String(runtime.get_player_faction_id())
	for settlement_variant in runtime.get_world_data().get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for offset_y in range(footprint_size.y):
			for offset_x in range(footprint_size.x):
				var coord := origin + Vector2i(offset_x, offset_y)
				if fog_system != null and fog_system.is_visible(coord, faction_id):
					return coord
	return Vector2i(-1, -1)


func _find_encounter_anchor_by_kind(world_data: Dictionary, encounter_kind: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as EncounterAnchorData
		if encounter_anchor == null:
			continue
		if encounter_anchor.encounter_kind == encounter_kind:
			return encounter_anchor
	return null


func _await_battle_input_ready(world_map) -> void:
	var deadline_msec := Time.get_ticks_msec() + 1500
	while world_map != null and world_map.battle_map_panel != null and world_map.battle_map_panel.is_loading_battle():
		if Time.get_ticks_msec() >= deadline_msec:
			_failures.append("等待 battle loading 结束超时，无法验证战斗键位。")
			return
		await _wait_seconds(0.05)
		await process_frame


func _advance_to_manual_battle_turn(runtime, world_map) -> bool:
	if runtime == null:
		return false
	for _attempt in range(20):
		if runtime.get_manual_battle_unit() != null:
			return true
		var tick_result: Dictionary = runtime.command_battle_tick(1.0)
		if not bool(tick_result.get("ok", false)):
			_failures.append("推进 battle tick 时失败，无法验证战斗键位。")
			return false
		if world_map != null:
			world_map._render_from_runtime(true)
		await process_frame
	return runtime.get_manual_battle_unit() != null


func _wait_seconds(duration_seconds: float) -> void:
	var target_time_msec := Time.get_ticks_msec() + int(round(duration_seconds * 1000.0))
	while Time.get_ticks_msec() < target_time_msec:
		await process_frame


func _send_key(world_map, keycode: Key) -> void:
	if world_map == null:
		return
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	event.echo = false
	world_map._unhandled_input(event)


func _ensure_game_session() -> void:
	_game_session = root.get_node_or_null("GameSession")
	if _game_session != null:
		return

	_game_session = GameSessionScript.new()
	_game_session.name = "GameSession"
	root.add_child(_game_session)
	await process_frame


func _reset_session() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	await process_frame


func _cleanup() -> void:
	if _game_session == null:
		return
	_game_session.clear_persisted_game()
	await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
