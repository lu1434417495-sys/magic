extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")

const ASHEN_WORLD_CONFIG := "res://data/configs/world_map/ashen_intersection_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_submap_return_blocks_while_battle_active()
	_test_submap_return_blocks_while_modal_open()
	_test_submap_entry_return_and_reload()

	if _failures.is_empty():
		print("World submap regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World submap regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_submap_return_blocks_while_battle_active() -> void:
	var runtime_context := _create_ashen_runtime_context()
	if runtime_context.is_empty():
		return

	var game_session = runtime_context.get("game_session")
	var facade = runtime_context.get("facade")
	if not _enter_ashen_submap(facade):
		facade.dispose()
		_cleanup(game_session)
		return

	var expected_coord: Vector2i = facade.get_player_coord()
	var expected_map_id: String = facade.get_active_map_id()
	var expected_active_submap_id := String(game_session.get_world_data().get("active_submap_id", ""))
	var battle_state := BATTLE_STATE_SCRIPT.new()
	battle_state.battle_id = &"submap_guard_battle"
	facade.set_runtime_battle_state(battle_state)

	var return_result: Dictionary = facade.command_return_from_submap()
	_assert_true(not bool(return_result.get("ok", true)), "子地图 battle active 时返回应被阻断。")
	_assert_eq(
		String(return_result.get("message", "")),
		"当前处于战斗中，不能从子地图返回。",
		"battle active 阻断应返回明确错误。"
	)
	_assert_true(facade.is_submap_active(), "battle active 阻断后应仍停留在子地图。")
	_assert_true(facade.is_battle_active(), "battle active 阻断后不应清空 battle 状态。")
	_assert_eq(facade.get_active_map_id(), expected_map_id, "battle active 阻断后 active_map_id 应保持不变。")
	_assert_eq(
		String(game_session.get_world_data().get("active_submap_id", "")),
		expected_active_submap_id,
		"battle active 阻断后 active_submap_id 应保持不变。"
	)
	_assert_eq(facade.get_player_coord(), expected_coord, "battle active 阻断后运行时玩家坐标应保持不变。")
	_assert_eq(game_session.get_player_coord(), expected_coord, "battle active 阻断后存档侧玩家坐标应保持不变。")

	facade.dispose()
	_cleanup(game_session)


func _test_submap_return_blocks_while_modal_open() -> void:
	var runtime_context := _create_ashen_runtime_context()
	if runtime_context.is_empty():
		return

	var game_session = runtime_context.get("game_session")
	var facade = runtime_context.get("facade")
	if not _enter_ashen_submap(facade):
		facade.dispose()
		_cleanup(game_session)
		return

	var expected_coord: Vector2i = facade.get_player_coord()
	var expected_map_id: String = facade.get_active_map_id()
	var expected_active_submap_id := String(game_session.get_world_data().get("active_submap_id", ""))
	facade.set_runtime_active_modal_id("settlement")

	var return_result: Dictionary = facade.command_return_from_submap()
	_assert_true(not bool(return_result.get("ok", true)), "子地图 modal 打开时返回应被阻断。")
	_assert_eq(
		String(return_result.get("message", "")),
		"当前有窗口打开，不能从子地图返回。",
		"modal-open 阻断应返回明确错误。"
	)
	_assert_true(facade.is_submap_active(), "modal-open 阻断后应仍停留在子地图。")
	_assert_eq(facade.get_active_map_id(), expected_map_id, "modal-open 阻断后 active_map_id 应保持不变。")
	_assert_eq(
		String(game_session.get_world_data().get("active_submap_id", "")),
		expected_active_submap_id,
		"modal-open 阻断后 active_submap_id 应保持不变。"
	)
	_assert_eq(facade.get_active_modal_id(), "settlement", "modal-open 阻断后当前 modal 应保持不变。")
	_assert_eq(facade.get_player_coord(), expected_coord, "modal-open 阻断后运行时玩家坐标应保持不变。")
	_assert_eq(game_session.get_player_coord(), expected_coord, "modal-open 阻断后存档侧玩家坐标应保持不变。")

	facade.dispose()
	_cleanup(game_session)


func _test_submap_entry_return_and_reload() -> void:
	var runtime_context := _create_ashen_runtime_context()
	if runtime_context.is_empty():
		return

	var game_session = runtime_context.get("game_session")
	var facade = runtime_context.get("facade")
	if not _enter_ashen_submap(facade):
		facade.dispose()
		_cleanup(game_session)
		return

	var submap_move_result: Dictionary = facade.command_world_move(Vector2i.LEFT, 1)
	_assert_true(bool(submap_move_result.get("ok", false)), "子地图内移动应成功。")
	_assert_eq(facade.get_player_coord(), Vector2i(14, 15), "子地图内移动后坐标应更新。")

	var active_save_id: String = game_session.get_active_save_id()
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
	_assert_eq(String(game_session.get_world_data().get("active_submap_id", "")), "", "成功返回后应清空 active_submap_id。")
	_assert_eq(game_session.get_player_coord(), Vector2i(52, 49), "成功返回后存档侧玩家坐标应同步回主世界原坐标。")

	reloaded_facade.dispose()
	_cleanup(game_session)


func _create_ashen_runtime_context() -> Dictionary:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(ASHEN_WORLD_CONFIG))
	_assert_true(create_error == OK, "灰烬交界预设应能创建新存档。")
	if create_error != OK:
		_cleanup(game_session)
		return {}

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	return {
		"game_session": game_session,
		"facade": facade,
	}


func _enter_ashen_submap(facade) -> bool:
	_assert_true(not facade.is_submap_active(), "初始应停留在主世界。")
	_assert_true(not facade.get_nearby_world_event_entries().is_empty(), "主世界应能看到已发现的灰烬入口事件。")

	var move_result: Dictionary = facade.command_world_move(Vector2i.RIGHT, 3)
	_assert_true(bool(move_result.get("ok", false)), "走向灰烬入口应成功。")
	_assert_eq(facade.get_active_modal_id(), "submap_confirm", "踩入入口事件后应弹出进入确认窗。")
	_assert_eq(
		String(facade.get_pending_submap_prompt().get("target_display_name", "")),
		"灰烬地图",
		"待确认入口应指向灰烬地图。"
	)

	var confirm_result: Dictionary = facade.command_confirm_submap_entry()
	_assert_true(bool(confirm_result.get("ok", false)), "确认后应进入灰烬地图。")
	_assert_true(facade.is_submap_active(), "确认后当前地图应切换到子地图。")
	_assert_eq(facade.get_active_map_id(), "ashen_ashlands", "子地图 ID 应写入运行时。")
	_assert_eq(facade.get_player_coord(), Vector2i(15, 15), "首次进入灰烬地图应落在子地图起点。")
	return bool(confirm_result.get("ok", false))


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
