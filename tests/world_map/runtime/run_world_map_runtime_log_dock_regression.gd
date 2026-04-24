extends SceneTree

const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")
const EncounterAnchorData = preload("res://scripts/systems/encounter_anchor_data.gd")
const RuntimeLogDock = preload("res://scripts/ui/runtime_log_dock.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []
var _game_session = null


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _reset_session()
	await _test_runtime_log_dock_reuses_same_window_for_world_and_battle()
	await _cleanup()

	if _failures.is_empty():
		print("World map runtime log dock regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map runtime log dock regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_runtime_log_dock_reuses_same_window_for_world_and_battle() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "runtime log dock 回归前置：应能创建测试世界。")
	if create_error != OK:
		return

	var world_map := WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	var runtime_log_dock := world_map.get_node("%RuntimeLogDock") as RuntimeLogDock
	var map_viewport := world_map.get_node("MapViewport") as Control
	_assert_true(runtime_log_dock != null, "world_map.tscn 应提供共享 RuntimeLogDock。")
	if runtime_log_dock == null:
		world_map.queue_free()
		await process_frame
		return

	_game_session.log_event("info", "world", "world.runtime_log_dock.test", "世界日志窗口回归。")
	world_map._render_from_runtime(false)
	await process_frame

	_assert_eq(runtime_log_dock.title_label.text, "运行日志", "世界态应使用共享日志窗口显示运行日志。")
	_assert_true(
		runtime_log_dock.log_output.get_parsed_text().contains("世界日志窗口回归。"),
		"世界态共享日志窗口应显示最近运行日志。"
	)
	_assert_true(
		runtime_log_dock.meta_label.text.contains("最近"),
		"世界态共享日志窗口元信息应显示运行日志缓冲摘要。"
	)
	if map_viewport != null:
		var viewport_rect := map_viewport.get_global_rect()
		var dock_rect := runtime_log_dock.get_global_rect()
		_assert_true(
			viewport_rect.position.x + viewport_rect.size.x <= dock_rect.position.x,
			"世界态共享日志窗口应位于地图区右侧，而不是覆盖地图。"
		)

	var runtime = world_map._runtime
	_assert_true(runtime != null, "runtime log dock 回归前置：world_map 场景应初始化 runtime。")
	if runtime == null:
		world_map.queue_free()
		await process_frame
		return

	var encounter_anchor = _find_encounter_anchor_by_kind(
		_game_session.get_world_data(),
		EncounterAnchorData.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "runtime log dock 回归需要至少一个单体野怪遭遇。")
	if encounter_anchor == null:
		world_map.queue_free()
		await process_frame
		return

	_game_session.set_battle_save_lock(true)
	runtime.start_battle(encounter_anchor)
	world_map._render_from_runtime(true)
	await process_frame
	await process_frame

	_assert_eq(runtime_log_dock.title_label.text, "战斗日志", "进入战斗后应复用同一个日志窗口切到战斗日志。")
	_assert_true(
		runtime_log_dock.log_output.get_parsed_text().contains("战斗开始："),
		"进入战斗后共享日志窗口应切到 battle start 之后的战斗日志。"
	)
	_assert_true(
		runtime_log_dock.meta_label.text.contains("上限"),
		"进入战斗后共享日志窗口元信息应切到 battle log 容量摘要。"
	)

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
