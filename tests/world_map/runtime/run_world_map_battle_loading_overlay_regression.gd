extends SceneTree

const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _reset_session()
	await _test_world_map_loading_overlay_tracks_battle_panel_state()
	await _cleanup()

	if _failures.is_empty():
		print("World map battle loading overlay regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map battle loading overlay regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_world_map_loading_overlay_tracks_battle_panel_state() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "加载世界地图场景前应能成功创建测试世界。")
	if create_error != OK:
		return

	var world_map := WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	var overlay := world_map.get_node("BattleLoadingOverlay") as Control
	var background := world_map.get_node("Background") as Control
	var loading_label := world_map.get_node("%BattleLoadingLabel") as Label
	var loading_progress_bar := world_map.get_node("%BattleLoadingProgressBar") as ProgressBar
	var loading_percent_label := world_map.get_node("%BattleLoadingPercentLabel") as Label
	var battle_map_panel = world_map.get_node("MapViewport/BattleMapPanel")

	_assert_true(overlay != null, "world_map.tscn 应提供根层 battle loading overlay。")
	_assert_true(overlay != null and not overlay.visible, "初始进入世界地图时 battle loading overlay 应保持隐藏。")
	_assert_eq(overlay.get_global_rect(), background.get_global_rect(), "battle loading overlay 应与 Background 保持同尺寸。")
	_assert_true(loading_progress_bar != null, "根层 overlay 应提供 battle loading progress bar。")
	_assert_true(loading_percent_label != null, "根层 overlay 应提供 battle loading 百分比标签。")

	if battle_map_panel != null:
		battle_map_panel.emit_signal("battle_loading_state_changed", true, 48.0)
		await process_frame

	_assert_true(overlay != null and overlay.visible, "BattleMapPanel 进入 loading 状态时应显示根层黑屏 overlay。")
	_assert_eq(loading_label.text, "LOADING...", "根层 overlay 只应显示 LOADING... 文本。")
	_assert_eq(int(round(loading_progress_bar.value)), 48, "loading 时 progress bar 应同步 battle panel 的进度值。")
	_assert_eq(loading_percent_label.text, "48%", "loading 时百分比标签应同步 battle panel 的进度值。")

	if battle_map_panel != null:
		battle_map_panel.emit_signal("battle_loading_state_changed", false, 100.0)
		await process_frame

	_assert_true(overlay != null and not overlay.visible, "BattleMapPanel 结束 loading 状态时应隐藏根层黑屏 overlay。")
	_assert_eq(int(round(loading_progress_bar.value)), 100, "loading 结束后 progress bar 应保留最终完成进度。")
	_assert_eq(loading_percent_label.text, "100%", "loading 结束后百分比标签应显示 100%。")

	world_map.queue_free()
	await process_frame


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
