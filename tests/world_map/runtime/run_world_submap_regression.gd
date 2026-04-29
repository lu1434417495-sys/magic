extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const SAVE_SERIALIZER_SCRIPT = preload("res://scripts/systems/persistence/save_serializer.gd")

const ASHEN_WORLD_CONFIG := "res://data/configs/world_map/ashen_intersection_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_mounted_submap_serializer_contract()
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


func _test_mounted_submap_serializer_contract() -> void:
	var serializer = SAVE_SERIALIZER_SCRIPT.new()
	var root_world_data := _build_minimal_world_data(101)
	root_world_data["mounted_submaps"] = {
		"ashen_ashlands": _build_mounted_submap_entry(false, {}),
	}

	var normalized_world_data := serializer.normalize_world_data(root_world_data)
	var normalized_entry := _get_mounted_submap_entry(normalized_world_data, "ashen_ashlands")
	_assert_true(not normalized_entry.is_empty(), "未生成子地图占位应能穿过 normalize_world_data。")
	_assert_true(not bool(normalized_entry.get("is_generated", true)), "未生成子地图 normalize 后应保持 is_generated=false。")
	_assert_true((normalized_entry.get("world_data", {}) as Dictionary).is_empty(), "未生成子地图 normalize 后应保持空 world_data。")

	var serialized_world_data := serializer.serialize_world_data(normalized_world_data)
	var serialized_entry := _get_mounted_submap_entry(serialized_world_data, "ashen_ashlands")
	_assert_true(not serialized_entry.is_empty(), "未生成子地图占位应能穿过 serialize_world_data。")
	_assert_true((serialized_entry.get("world_data", {}) as Dictionary).is_empty(), "未生成子地图 serialize 后应保持空 world_data。")

	var generated_submap_world_data := _build_minimal_world_data(202)
	var generated_root_world_data := _build_minimal_world_data(101)
	generated_root_world_data["mounted_submaps"] = {
		"ashen_ashlands": _build_mounted_submap_entry(true, generated_submap_world_data),
	}
	var serialized_generated_world_data := serializer.serialize_world_data(generated_root_world_data)
	var serialized_generated_entry := _get_mounted_submap_entry(serialized_generated_world_data, "ashen_ashlands")
	var serialized_generated_submap_world_data: Dictionary = serialized_generated_entry.get("world_data", {})
	_assert_eq(
		int(serialized_generated_submap_world_data.get("next_equipment_instance_serial", 0)),
		1,
		"已生成子地图应继续按完整 world_data 序列化。"
	)

	var generated_missing_serial := _build_minimal_world_data(303)
	generated_missing_serial.erase("next_equipment_instance_serial")
	var missing_serial_error := serializer.get_mounted_submap_world_data_validation_error(
		"ashen_ashlands",
		true,
		generated_missing_serial
	)
	_assert_true(
		missing_serial_error.contains("missing required field 'next_equipment_instance_serial'"),
		"已生成子地图缺少装备实例序列仍应判为坏档。 error=%s" % missing_serial_error
	)

	var ungenerated_with_world_data_error := serializer.get_mounted_submap_world_data_validation_error(
		"ashen_ashlands",
		false,
		_build_minimal_world_data(404)
	)
	_assert_true(
		ungenerated_with_world_data_error.contains("ungenerated submap requires empty world_data"),
		"未生成子地图不应携带非空 world_data，避免把不一致状态静默降级。 error=%s" % ungenerated_with_world_data_error
	)


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


func _build_minimal_world_data(map_seed: int) -> Dictionary:
	return {
		"map_seed": map_seed,
		"world_step": 0,
		"next_equipment_instance_serial": 1,
		"active_submap_id": "",
		"submap_return_stack": [],
		"settlements": [],
		"world_events": [],
		"encounter_anchors": [],
		"mounted_submaps": {},
	}


func _build_mounted_submap_entry(is_generated: bool, world_data: Dictionary) -> Dictionary:
	return {
		"submap_id": "ashen_ashlands",
		"display_name": "灰烬地图",
		"generation_config_path": ASHEN_WORLD_CONFIG,
		"return_hint_text": "点击任意地点返回原位置。",
		"is_generated": is_generated,
		"player_coord": Vector2i(-1, -1),
		"world_data": world_data,
	}


func _get_mounted_submap_entry(world_data: Dictionary, submap_id: String) -> Dictionary:
	var mounted_submaps_variant = world_data.get("mounted_submaps", {})
	if mounted_submaps_variant is not Dictionary:
		return {}
	var mounted_submaps: Dictionary = mounted_submaps_variant
	var entry_variant = mounted_submaps.get(submap_id, {})
	return entry_variant if entry_variant is Dictionary else {}


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
