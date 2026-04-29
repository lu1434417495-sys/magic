extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const SPAWN_REACHABILITY_SERVICE_PATH := "res://scripts/systems/battle/runtime/battle_spawn_reachability_service.gd"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var service_script = load(SPAWN_REACHABILITY_SERVICE_PATH)
	_assert_true(
		service_script != null,
		"BattleSpawnReachabilityService 脚本应存在于 %s。" % SPAWN_REACHABILITY_SERVICE_PATH
	)
	if service_script != null:
		_test_deep_water_split_marks_enemy_spawn_invalid(service_script)
		_test_flat_field_marks_enemy_spawn_valid(service_script)

	if _failures.is_empty():
		print("Battle spawn reachability regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle spawn reachability regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_deep_water_split_marks_enemy_spawn_invalid(service_script) -> void:
	var fixture := _build_service_fixture()
	var skill_id: StringName = fixture.get("skill_id", &"")
	if skill_id == &"":
		return

	var skill_def = fixture.get("skill_def")
	var skill_range := _get_skill_range(skill_def)
	var barrier_start := 3
	var barrier_width := skill_range + 2
	var map_size := Vector2i(barrier_start + barrier_width + 4, 5)
	var state = _build_flat_state(map_size)
	for x in range(barrier_start, barrier_start + barrier_width):
		for y in range(map_size.y):
			_set_cell_terrain(state, Vector2i(x, y), BATTLE_CELL_STATE_SCRIPT.TERRAIN_DEEP_WATER)

	var enemy = _build_unit(&"split_enemy", &"enemy", Vector2i(1, 2), skill_id)
	var player = _build_unit(&"split_player", &"player", Vector2i(barrier_start + barrier_width + 1, 2), &"")
	_add_unit_to_state(fixture.grid_service, state, enemy, true)
	_add_unit_to_state(fixture.grid_service, state, player, false)

	var result: Dictionary = fixture.service.validate_state(
		state,
		fixture.grid_service,
		fixture.skill_defs,
		{}
	)
	_assert_true(not bool(result.get("valid", true)), "深水完全隔断敌人与玩家时，出生可达性应判定为 invalid。")
	_assert_true(
		_string_name_array_has(result.get("invalid_enemy_unit_ids", []), enemy.unit_id),
		"深水隔断回归应在 invalid_enemy_unit_ids 中包含敌方单位。"
	)
	_assert_true(
		not _collect_details_for_unit(result.get("details", []), enemy.unit_id).is_empty(),
		"深水隔断回归应为无效敌方单位返回 details，便于定位出生点问题。"
	)


func _test_flat_field_marks_enemy_spawn_valid(service_script) -> void:
	var fixture := _build_service_fixture()
	var skill_id: StringName = fixture.get("skill_id", &"")
	if skill_id == &"":
		return

	var skill_def = fixture.get("skill_def")
	var skill_range := _get_skill_range(skill_def)
	var state = _build_flat_state(Vector2i(skill_range + 6, 3))
	var enemy = _build_unit(&"flat_enemy", &"enemy", Vector2i(1, 1), skill_id)
	var player = _build_unit(&"flat_player", &"player", Vector2i(skill_range + 4, 1), &"")
	_add_unit_to_state(fixture.grid_service, state, enemy, true)
	_add_unit_to_state(fixture.grid_service, state, player, false)

	var result: Dictionary = fixture.service.validate_state(
		state,
		fixture.grid_service,
		fixture.skill_defs,
		{}
	)
	_assert_true(bool(result.get("valid", false)), "平地直连时，敌方出生点应能抵达可攻击玩家的位置。")
	_assert_true(
		not _string_name_array_has(result.get("invalid_enemy_unit_ids", []), enemy.unit_id),
		"平地直连回归不应把敌方单位列入 invalid_enemy_unit_ids。"
	)


func _build_service_fixture() -> Dictionary:
	var game_session = GAME_SESSION_SCRIPT.new()
	var skill_defs: Dictionary = game_session.get_skill_defs()
	var skill_data := _find_enemy_unit_attack_skill(skill_defs)
	game_session.free()
	if skill_data.is_empty():
		_failures.append("正式 GameSession 技能表中应存在 target_mode == unit 且敌方可攻击玩家的 range >= 1 技能。")
		return {}

	var service_script = load(SPAWN_REACHABILITY_SERVICE_PATH)
	var service = service_script.new()
	return {
		"service": service,
		"grid_service": BATTLE_GRID_SERVICE_SCRIPT.new(),
		"skill_defs": skill_defs,
		"skill_id": skill_data.get("skill_id", &""),
		"skill_def": skill_data.get("skill_def"),
	}


func _find_enemy_unit_attack_skill(skill_defs: Dictionary) -> Dictionary:
	var best_skill_id: StringName = &""
	var best_skill_def = null
	var best_range := 2147483647
	for skill_key in skill_defs.keys():
		var skill_def = skill_defs.get(skill_key)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		var combat_profile = skill_def.combat_profile
		if combat_profile.target_mode != &"unit":
			continue
		if not _target_filter_can_attack_player(combat_profile.target_team_filter):
			continue
		var skill_range := int(combat_profile.range_value)
		if skill_range < 1:
			continue
		if skill_range < best_range:
			best_range = skill_range
			best_skill_id = skill_def.skill_id if skill_def.skill_id != &"" else StringName(String(skill_key))
			best_skill_def = skill_def
	if best_skill_id == &"":
		return {}
	return {
		"skill_id": best_skill_id,
		"skill_def": best_skill_def,
	}


func _target_filter_can_attack_player(target_team_filter: StringName) -> bool:
	match target_team_filter:
		&"enemy", &"any", &"":
			return true
		_:
			return false


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_spawn_reachability_regression"
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


func _set_cell_terrain(state, coord: Vector2i, terrain: StringName) -> void:
	var cell = state.cells.get(coord)
	if cell == null:
		return
	cell.base_terrain = terrain
	cell.recalculate_runtime_values()
	state.cell_columns[coord] = BATTLE_CELL_STATE_SCRIPT.build_stacked_cells_from_surface_cell(cell)


func _build_unit(
	unit_id: StringName,
	faction_id: StringName,
	coord: Vector2i,
	skill_id: StringName
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.control_mode = &"ai" if faction_id == &"enemy" else &"manual"
	unit.current_hp = 30
	unit.current_mp = 10
	unit.current_stamina = 10
	unit.current_aura = 10
	unit.current_ap = 2
	unit.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"mp_max", 10)
	unit.attribute_snapshot.set_value(&"stamina_max", 10)
	unit.attribute_snapshot.set_value(&"aura_max", 10)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	if skill_id != &"":
		unit.known_active_skill_ids.clear()
		unit.known_active_skill_ids.append(skill_id)
		unit.known_skill_level_map = {skill_id: 1}
	return unit


func _add_unit_to_state(grid_service, state, unit, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed = grid_service.place_unit(state, unit, unit.coord, true)
	_assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _get_skill_range(skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 1
	return maxi(int(skill_def.combat_profile.range_value), 1)


func _string_name_array_has(values, expected: StringName) -> bool:
	for value in values:
		if StringName(String(value)) == expected:
			return true
	return false


func _collect_details_for_unit(details, unit_id: StringName) -> Array[Dictionary]:
	var matches: Array[Dictionary] = []
	for detail_variant in details:
		if detail_variant is not Dictionary:
			continue
		var detail: Dictionary = detail_variant
		if StringName(String(detail.get("enemy_unit_id", detail.get("unit_id", &"")))) == unit_id:
			matches.append(detail)
	return matches


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
