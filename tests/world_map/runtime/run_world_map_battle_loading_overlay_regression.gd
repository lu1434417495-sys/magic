extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const EncounterAnchorData = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const BattleTerrainGenerator = preload("res://scripts/systems/battle/terrain/battle_terrain_generator.gd")
const GameSessionScript = preload("res://scripts/systems/persistence/game_session.gd")
const WorldMapScene = preload("res://scenes/main/world_map.tscn")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures
var _game_session = null


class PendingTerrainGenerator:
	extends BattleTerrainGenerator

	const PENDING_FAIL_CALLS := 8

	var _generate_call_count := 0

	func generate(_encounter_anchor_or_context, _seed: int = 0, _context: Dictionary = {}) -> Dictionary:
		_generate_call_count += 1
		if _generate_call_count <= PENDING_FAIL_CALLS:
			return {}
		var map_size := Vector2i(3, 2)
		var cells: Dictionary = {}
		for y in range(map_size.y):
			for x in range(map_size.x):
				var cell = BATTLE_CELL_STATE_SCRIPT.new()
				cell.coord = Vector2i(x, y)
				cell.base_terrain = &"land"
				cell.base_height = 4
				cell.height_offset = 0
				cell.recalculate_runtime_values()
				cells[cell.coord] = cell
		return {
			"map_size": map_size,
			"cells": cells,
			"cell_columns": BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(cells),
			"ally_spawns": [Vector2i(0, 0)],
			"enemy_spawns": [Vector2i(2, 1)],
			"terrain_profile_id": &"default",
		}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _ensure_game_session()
	await _reset_session()
	await _test_world_map_loading_overlay_tracks_battle_panel_state()
	await _reset_session()
	await _test_world_map_loading_overlay_stays_visible_during_pending_battle_generation()
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


func _test_world_map_loading_overlay_stays_visible_during_pending_battle_generation() -> void:
	var create_error := int(_game_session.start_new_game(TEST_CONFIG_PATH))
	_assert_eq(create_error, OK, "pending terrain loading 回归前应能成功创建测试世界。")
	if create_error != OK:
		return

	var world_map := WorldMapScene.instantiate()
	root.add_child(world_map)
	await process_frame
	await process_frame

	var runtime = world_map._runtime
	var overlay := world_map.get_node("BattleLoadingOverlay") as Control
	var loading_progress_bar := world_map.get_node("%BattleLoadingProgressBar") as ProgressBar
	var loading_percent_label := world_map.get_node("%BattleLoadingPercentLabel") as Label
	var battle_map_panel = world_map.get_node("MapViewport/BattleMapPanel")
	_assert_true(runtime != null, "world_map 场景应初始化 runtime。")
	_assert_true(overlay != null, "pending terrain loading 回归需要根层 battle loading overlay。")
	_assert_true(loading_progress_bar != null, "pending terrain loading 回归需要 battle loading progress bar。")
	_assert_true(loading_percent_label != null, "pending terrain loading 回归需要 battle loading 百分比标签。")
	_assert_true(battle_map_panel != null, "pending terrain loading 回归需要 BattleMapPanel。")
	if runtime == null or overlay == null or loading_progress_bar == null or loading_percent_label == null or battle_map_panel == null:
		world_map.queue_free()
		await process_frame
		return

	var encounter_anchor = _find_encounter_anchor_by_kind(
		_game_session.get_world_data(),
		EncounterAnchorData.ENCOUNTER_KIND_SINGLE
	)
	_assert_true(encounter_anchor != null, "pending terrain loading 回归需要至少一个单体野怪遭遇。")
	if encounter_anchor == null:
		world_map.queue_free()
		await process_frame
		return

	runtime.get_battle_runtime()._terrain_generator = PendingTerrainGenerator.new()
	runtime.start_battle(encounter_anchor)
	world_map._render_from_runtime(true)

	_assert_eq(runtime.get_active_modal_id(), "battle_loading", "terrain 首次生成失败时应进入 battle_loading modal。")
	_assert_true(overlay != null and overlay.visible, "battle_loading modal 下根层 overlay 应保持可见。")
	_assert_eq(int(round(loading_progress_bar.value)), 0, "battle_loading modal 初始应显示 0 进度。")
	_assert_eq(loading_percent_label.text, "0%", "battle_loading modal 初始百分比应显示 0%。")

	var reached_battle_start_confirm := false
	for _frame in range(30):
		await process_frame
		if runtime.get_active_modal_id() == "battle_start_confirm":
			reached_battle_start_confirm = true
			break
	_assert_true(reached_battle_start_confirm, "terrain 重试成功后应进入 battle_start_confirm modal。")
	if overlay.visible:
		for _frame in range(30):
			await process_frame
			if not overlay.visible:
				break
	_assert_eq(runtime.get_active_modal_id(), "battle_start_confirm", "terrain 重试成功后应进入 battle_start_confirm modal。")
	_assert_eq(
		overlay.visible,
		battle_map_panel.is_loading_battle(),
		"进入 battle_start_confirm 后根层 overlay 应重新由 BattleMapPanel 的 loading 状态驱动。"
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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
