extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle_unit_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_facade_clicking_active_unit_casts_self_skill()
	_test_facade_multi_unit_selection_tracks_target_unit_ids()
	if _failures.is_empty():
		print("Battle skill protocol regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle skill protocol regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_clicking_active_unit_casts_self_skill() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(4, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"self_cast_user",
		"自施法者",
		&"player",
		Vector2i(1, 0),
		[&"mage_arcane_orbit"],
		2,
		6
	)
	var enemy: BattleUnitState = _build_manual_unit(
		&"self_cast_enemy",
		"敌人",
		&"enemy",
		Vector2i(3, 0),
		[],
		2,
		0
	)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var before_hp: int = int(caster.current_hp)
	var before_mp: int = int(caster.current_mp)
	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择自施法技能应返回成功结果。")
	var cast_result: Dictionary = facade.command_battle_move_to(caster.coord)
	_assert_true(bool(cast_result.get("ok", false)), "点击自身坐标施法应返回成功结果。")
	var battle_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})

	_assert_eq(caster.current_mp, before_mp - 2, "点击自身后应真正施放自施法技能并扣除法力。")
	_assert_true(caster.current_hp < before_hp, "当前自施法回归夹具应能观测到技能已真实结算。")
	_assert_eq(
		_extract_coord_pairs(battle_snapshot.get("selected_target_coords", [])),
		[],
		"自施法结算后不应残留已选目标坐标。"
	)

	_cleanup_test_session(game_session)


func _test_facade_multi_unit_selection_tracks_target_unit_ids() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return

	var skill_def = game_session.get_skill_defs().get(&"mage_arcane_missile")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "多目标协议回归前置：mage_arcane_missile 定义应存在。")
	if skill_def == null or skill_def.combat_profile == null:
		_cleanup_test_session(game_session)
		return
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 2

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var state: BattleState = _build_flat_state(Vector2i(5, 2))
	var caster: BattleUnitState = _build_manual_unit(
		&"multi_unit_user",
		"多目标施法者",
		&"player",
		Vector2i(0, 0),
		[&"mage_arcane_missile"],
		2,
		6
	)
	var enemy_a: BattleUnitState = _build_manual_unit(&"enemy_a", "敌人A", &"enemy", Vector2i(2, 0), [], 2, 0)
	var enemy_b: BattleUnitState = _build_manual_unit(&"enemy_b", "敌人B", &"enemy", Vector2i(3, 0), [], 2, 0)
	var enemy_c: BattleUnitState = _build_manual_unit(&"enemy_c", "敌人C", &"enemy", Vector2i(4, 0), [], 2, 0)
	_add_unit_to_state(facade, state, caster, false)
	_add_unit_to_state(facade, state, enemy_a, true)
	_add_unit_to_state(facade, state, enemy_b, true)
	_add_unit_to_state(facade, state, enemy_c, true)
	state.phase = &"unit_acting"
	state.active_unit_id = caster.unit_id
	_apply_battle_state(facade, state)

	var select_result: Dictionary = facade.command_battle_select_skill(0)
	_assert_true(bool(select_result.get("ok", false)), "选择多目标技能应返回成功结果。")
	facade.command_battle_move_to(enemy_b.coord)
	var queued_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_eq(
		_extract_string_array(queued_snapshot.get("selected_target_unit_ids", [])),
		["enemy_b"],
		"首个单位目标应按点击顺序写入 battle snapshot。"
	)
	_assert_eq(
		_extract_coord_pairs(queued_snapshot.get("selected_target_coords", [])),
		[[enemy_b.coord.x, enemy_b.coord.y]],
		"单位多选阶段应把已选单位坐标同步暴露给界面快照。"
	)

	facade.command_battle_move_to(enemy_a.coord)
	var after_cast_snapshot: Dictionary = facade.build_headless_snapshot().get("battle", {})
	_assert_true(enemy_b.current_hp < 30, "多目标技能结算后应命中第一个已选单位。")
	_assert_true(enemy_a.current_hp < 30, "多目标技能结算后应命中第二个已选单位。")
	_assert_eq(enemy_c.current_hp, 30, "未被选中的单位不应受到多目标技能影响。")
	_assert_eq(
		_extract_string_array(after_cast_snapshot.get("selected_target_unit_ids", [])),
		[],
		"多目标技能结算后，battle snapshot 不应残留已选单位目标。"
	)

	_cleanup_test_session(game_session)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _build_flat_state(map_size: Vector2i) -> BattleState:
	var state: BattleState = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_skill_protocol"
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_manual_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	skill_ids: Array[StringName],
	current_ap: int,
	current_mp: int
) -> BattleUnitState:
	var unit: BattleUnitState = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_mp = current_mp
	unit.current_ap = current_ap
	unit.current_stamina = 20
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", maxi(current_mp, 6))
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 2))
	unit.attribute_snapshot.set_value(&"physical_attack", 10)
	unit.attribute_snapshot.set_value(&"magic_attack", 12)
	unit.attribute_snapshot.set_value(&"physical_defense", 4)
	unit.attribute_snapshot.set_value(&"magic_defense", 4)
	unit.attribute_snapshot.set_value(&"speed", 10)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(facade, state: BattleState, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = bool(facade._battle_runtime._grid_service.place_unit(state, unit, unit.coord, true))
	_assert_true(placed, "测试单位 %s 应能成功放入战场。" % String(unit.unit_id))


func _apply_battle_state(facade, state: BattleState) -> void:
	facade._battle_runtime._state = state
	facade._battle_state = state
	facade._battle_selected_coord = Vector2i(-1, -1)
	facade._refresh_battle_runtime_state()


func _extract_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _extract_coord_pairs(coord_dicts: Array) -> Array:
	var pairs: Array = []
	for coord_variant in coord_dicts:
		if coord_variant is not Dictionary:
			continue
		var coord: Dictionary = coord_variant
		pairs.append([int(coord.get("x", 0)), int(coord.get("y", 0))])
	return pairs


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
