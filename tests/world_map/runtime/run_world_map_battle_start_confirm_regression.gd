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
	await _test_battle_start_confirm_stays_non_cancellable_on_world_map_scene()
	await _cleanup()

	if _failures.is_empty():
		print("World map battle start confirm regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map battle start confirm regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_battle_start_confirm_stays_non_cancellable_on_world_map_scene() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "加载 world_map 场景前应能成功创建测试世界。")
	if create_error != OK:
		return

	var world_map = WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	var runtime = world_map._runtime
	var prompt_window = world_map.get_node("SubmapEntryWindow")
	_assert_true(runtime != null, "world_map 场景应初始化 runtime。")
	_assert_true(prompt_window != null, "world_map 场景应包含 SubmapEntryWindow。")
	if runtime == null or prompt_window == null:
		world_map.queue_free()
		await process_frame
		return

	var encounter_anchor = _find_encounter_anchor_by_kind(
		_game_session.get_world_data(),
		EncounterAnchorData.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "battle-start confirm 场景回归需要至少一个单体野怪遭遇。")
	if encounter_anchor == null:
		world_map.queue_free()
		await process_frame
		return

	_game_session.set_battle_save_lock(true)
	runtime.start_battle(encounter_anchor)
	world_map._render_from_runtime(true)
	await process_frame

	var start_prompt: Dictionary = runtime.get_pending_battle_start_prompt()
	var battle_state = runtime.get_battle_state()
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "开战后应进入 battle_start_confirm modal。")
	_assert_true(bool(start_prompt.has("cancel_visible")), "battle-start confirm prompt 应显式包含 cancel_visible。")
	_assert_true(bool(start_prompt.has("dismiss_on_shade")), "battle-start confirm prompt 应显式包含 dismiss_on_shade。")
	_assert_eq(bool(start_prompt.get("cancel_visible", true)), false, "battle-start confirm prompt 应显式禁用取消按钮。")
	_assert_eq(bool(start_prompt.get("dismiss_on_shade", true)), false, "battle-start confirm prompt 应显式禁用遮罩关闭。")
	_assert_true(prompt_window.visible, "battle-start confirm 打开时 SubmapEntryWindow 应可见。")
	_assert_true(not prompt_window.cancel_button.visible, "battle-start confirm 模式下不应显示取消按钮。")
	_assert_true(prompt_window.cancel_button.disabled, "battle-start confirm 模式下取消按钮应保持禁用。")
	_assert_true(battle_state != null and String(battle_state.modal_state) == "start_confirm", "battle state 应保持在 start_confirm。")
	if battle_state != null and battle_state.timeline != null:
		_assert_true(battle_state.timeline.frozen, "battle-start confirm 打开时 timeline 应保持冻结。")

	prompt_window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_LEFT))
	_assert_true(prompt_window.visible, "battle-start confirm 模式下点击遮罩不应关闭窗口。")
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "点击遮罩后 runtime modal 不应变化。")
	if battle_state != null and battle_state.timeline != null:
		_assert_true(battle_state.timeline.frozen, "点击遮罩后 timeline 应继续冻结。")

	prompt_window._on_cancel_button_pressed()
	_assert_true(prompt_window.visible, "battle-start confirm 模式下触发取消按钮回调不应关闭窗口。")
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "触发取消按钮回调后 runtime modal 不应变化。")
	if battle_state != null and battle_state.timeline != null:
		_assert_true(battle_state.timeline.frozen, "触发取消按钮回调后 timeline 应继续冻结。")

	prompt_window.hide_window()
	prompt_window.cancelled.emit()
	await process_frame
	_assert_true(prompt_window.visible, "battle-start confirm 模式下 stray cancel 信号后场景应重新显示确认窗。")
	_assert_true(not prompt_window.cancel_button.visible, "stray cancel 信号后取消按钮仍应保持隐藏。")
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "stray cancel 信号不应改写 runtime modal。")
	_assert_eq(
		bool(runtime.get_pending_battle_start_prompt().get("cancel_visible", true)),
		false,
		"stray cancel 信号后 runtime prompt 仍应保持不可取消契约。"
	)
	if battle_state != null and battle_state.timeline != null:
		_assert_true(battle_state.timeline.frozen, "stray cancel 信号后 timeline 应继续冻结。")

	world_map.queue_free()
	await process_frame


func _find_encounter_anchor_by_kind(world_data: Dictionary, encounter_kind: StringName):
	for encounter_variant in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as EncounterAnchorData
		if encounter_anchor == null:
			continue
		if encounter_anchor.encounter_kind == encounter_kind:
			return encounter_anchor
	return null


func _make_mouse_button_event(button_index: MouseButton) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button_index
	event.pressed = true
	return event


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
