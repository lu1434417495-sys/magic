extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")

const ASHEN_WORLD_CONFIG := "res://data/configs/world_map/ashen_intersection_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_submap_entry_return_and_reload()

	if _failures.is_empty():
		print("World submap regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World submap regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_submap_entry_return_and_reload() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(ASHEN_WORLD_CONFIG))
	_assert_true(create_error == OK, "灰烬交界预设应能创建新存档。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	_assert_true(not facade.is_submap_active(), "初始应停留在主世界。")
	_assert_true(not facade.get_nearby_world_event_entries().is_empty(), "主世界应能看到已发现的灰烬入口事件。")

	var move_result := facade.command_world_move(Vector2i.RIGHT, 3)
	_assert_true(bool(move_result.get("ok", false)), "走向灰烬入口应成功。")
	_assert_eq(facade.get_active_modal_id(), "submap_confirm", "踩入入口事件后应弹出进入确认窗。")
	_assert_eq(
		String(facade.get_pending_submap_prompt().get("target_display_name", "")),
		"灰烬地图",
		"待确认入口应指向灰烬地图。"
	)

	var confirm_result := facade.command_confirm_submap_entry()
	_assert_true(bool(confirm_result.get("ok", false)), "确认后应进入灰烬地图。")
	_assert_true(facade.is_submap_active(), "确认后当前地图应切换到子地图。")
	_assert_eq(facade.get_active_map_id(), "ashen_ashlands", "子地图 ID 应写入运行时。")
	_assert_eq(facade.get_player_coord(), Vector2i(15, 15), "首次进入灰烬地图应落在子地图起点。")

	var submap_move_result := facade.command_world_move(Vector2i.LEFT, 1)
	_assert_true(bool(submap_move_result.get("ok", false)), "子地图内移动应成功。")
	_assert_eq(facade.get_player_coord(), Vector2i(14, 15), "子地图内移动后坐标应更新。")

	var active_save_id := game_session.get_active_save_id()
	facade.dispose()

	var reload_error := int(game_session.load_save(active_save_id))
	_assert_true(reload_error == OK, "子地图状态应能从存档中重新载入。")
	if reload_error != OK:
		_cleanup(game_session)
		return

	var reloaded_facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	reloaded_facade.setup(game_session)
	_assert_true(reloaded_facade.is_submap_active(), "重新载入后应仍停留在灰烬地图。")
	_assert_eq(reloaded_facade.get_player_coord(), Vector2i(14, 15), "重新载入后应恢复子地图内坐标。")

	var return_result := reloaded_facade.command_return_from_submap()
	_assert_true(bool(return_result.get("ok", false)), "从子地图返回主世界应成功。")
	_assert_true(not reloaded_facade.is_submap_active(), "返回后应回到主世界。")
	_assert_eq(reloaded_facade.get_player_coord(), Vector2i(52, 49), "返回后应恢复到进入前的原坐标。")

	reloaded_facade.dispose()
	_cleanup(game_session)


func _cleanup(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
