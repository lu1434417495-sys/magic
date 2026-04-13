## 文件说明：该脚本属于战斗运行时冒烟测试相关的回归脚本，集中覆盖 timed terrain tick 等核心推进路径。
## 审查重点：重点核对最小状态夹具、关键推进调用以及类型收敛场景是否持续稳定。
## 备注：后续若 battle runtime 的最小启动前置发生变化，需要同步更新该脚本夹具。

extends SceneTree

const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_timed_terrain_processing_accepts_dictionary_keys()
	_test_evaluate_move_rules_survive_stacked_columns()
	_test_move_command_executes_normally_on_stacked_columns()
	_test_height_delta_rebuilds_cell_columns()
	_test_archer_multishot_accepts_unordered_targets()
	_test_skill_costs_and_cooldowns_apply_in_runtime()
	if _failures.is_empty():
		print("Battle runtime smoke: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle runtime smoke: FAIL (%d)" % _failures.size())
	quit(1)


func _test_timed_terrain_processing_accepts_dictionary_keys() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"runtime_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(2, 1)
	state.timeline = BattleTimelineState.new()
	state.timeline.units_per_second = 10
	state.timeline.current_tu = 5

	var lead_cell := _build_cell(Vector2i(1, 0))
	var trailing_cell := _build_cell(Vector2i(0, 0))
	var timed_effect := BattleTerrainEffectState.new()
	timed_effect.field_instance_id = &"smoke_field"
	timed_effect.effect_id = &"smoke_tick"
	timed_effect.tick_interval_tu = 5
	timed_effect.remaining_tu = 10
	timed_effect.next_tick_at_tu = 5
	trailing_cell.timed_terrain_effects.append(timed_effect)

	state.cells = {
		lead_cell.coord: lead_cell,
		trailing_cell.coord: trailing_cell,
	}

	runtime._state = state
	var batch = runtime.advance(0.1)
	_assert_true(batch != null, "advance() 应返回有效 batch。")
	_assert_true(
		trailing_cell.timed_terrain_effects.size() == 1,
		"timed terrain effect 在无占位单位时应稳定保留，且不应因坐标排序报错。"
	)
	_assert_true(
		trailing_cell.timed_terrain_effects[0].remaining_tu == 5,
		"timed terrain effect 应完成一次稳定 tick。"
	)


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _test_evaluate_move_rules_survive_stacked_columns() -> void:
	var grid_service := BattleGridService.new()
	var state := BattleState.new()
	state.battle_id = &"move_rules_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(3, 1)
	state.timeline = BattleTimelineState.new()
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
		Vector2i(2, 0): _build_cell(Vector2i(2, 0)),
	}
	state.cells[Vector2i(2, 0)].base_height = 6
	state.cells[Vector2i(2, 0)].recalculate_runtime_values()
	state.cells[Vector2i(1, 0)].set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"move_smoke_unit", Vector2i(0, 0), 3)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(grid_service.place_unit(state, unit, Vector2i(0, 0), true), "移动规则测试单位应成功放入起点。")

	var flat_move := grid_service.evaluate_move(state, Vector2i(0, 0), Vector2i(1, 0), unit)
	_assert_true(bool(flat_move.get("allowed", false)), "真堆叠列改造后，平地相邻移动仍应允许。")
	_assert_true(grid_service.move_unit_force(state, unit, Vector2i(1, 0)), "移动规则测试单位应能被重定位到中间格继续验证后续规则。")

	var blocked_by_wall := grid_service.evaluate_move(state, Vector2i(1, 0), Vector2i(2, 0), unit)
	_assert_true(not bool(blocked_by_wall.get("allowed", false)), "真堆叠列改造后，墙阻挡规则仍应生效。")

	state.cells[Vector2i(1, 0)].clear_edge_feature(Vector2i.RIGHT)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	var blocked_by_height := grid_service.evaluate_move(state, Vector2i(1, 0), Vector2i(2, 0), unit)
	_assert_true(not bool(blocked_by_height.get("allowed", false)), "真堆叠列改造后，高差超过 1 的移动仍应被禁止。")


func _test_move_command_executes_normally_on_stacked_columns() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"move_command_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(2, 1)
	state.timeline = BattleTimelineState.new()
	state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
	}
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"runtime_move_unit", Vector2i(0, 0), 3)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(runtime._grid_service.place_unit(state, unit, Vector2i(0, 0), true), "runtime move 测试单位应成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_MOVE
	command.unit_id = unit.unit_id
	command.target_coord = Vector2i(1, 0)
	var batch := runtime.issue_command(command)
	_assert_true(unit.coord == Vector2i(1, 0), "issue_command(move) 在真堆叠列地图上仍应更新单位坐标。")
	_assert_true(unit.current_ap == 2, "issue_command(move) 在真堆叠列地图上仍应按移动消耗扣除行动点。")
	_assert_true(batch.changed_unit_ids.has(unit.unit_id), "移动批次仍应记录变更单位。")
	_assert_true(state.cells[Vector2i(1, 0)].occupant_unit_id == unit.unit_id, "目标地格占位应在移动后同步更新。")


func _test_height_delta_rebuilds_cell_columns() -> void:
	var grid_service := BattleGridService.new()
	var state := BattleState.new()
	state.battle_id = &"height_delta_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(1, 1)
	state.timeline = BattleTimelineState.new()
	var cell := _build_cell(Vector2i.ZERO)
	state.cells = {Vector2i.ZERO: cell}
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var before_column := state.cell_columns.get(Vector2i.ZERO, []) as Array
	_assert_true(before_column.size() == 5, "初始高度 4 的地格应展开成 5 层真实堆叠 cell。")
	var result := grid_service.apply_height_delta_result(state, Vector2i.ZERO, 1)
	_assert_true(bool(result.get("changed", false)), "高度变化在真堆叠列地图上应仍可生效。")
	var after_column := state.cell_columns.get(Vector2i.ZERO, []) as Array
	_assert_true(after_column.size() == 6, "高度增加 1 后，真实堆叠 cell 列数量应同步增加。")
	_assert_true(int(state.cells[Vector2i.ZERO].current_height) == 5, "surface cache 顶层高度应与真实堆叠列同步。")


func _test_archer_multishot_accepts_unordered_targets() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(4, 1))
	var archer := _build_unit(&"archer_multishot_user", Vector2i(0, 0), 3)
	archer.current_stamina = 20
	archer.known_active_skill_ids = [&"archer_multishot"]
	archer.known_skill_level_map = {&"archer_multishot": 1}
	var enemy_a := _build_enemy_unit(&"enemy_a", Vector2i(1, 0))
	var enemy_b := _build_enemy_unit(&"enemy_b", Vector2i(2, 0))
	var enemy_c := _build_enemy_unit(&"enemy_c", Vector2i(3, 0))

	state.units = {
		archer.unit_id: archer,
		enemy_a.unit_id: enemy_a,
		enemy_b.unit_id: enemy_b,
		enemy_c.unit_id: enemy_c,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "弓箭手测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_a, enemy_a.coord, true), "敌人 A 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_b, enemy_b.coord, true), "敌人 B 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_c, enemy_c.coord, true), "敌人 C 应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_multishot"
	command.skill_variant_id = &"multishot_volley"
	command.target_coords = [enemy_c.coord, enemy_a.coord, enemy_b.coord]
	var preview := runtime.preview_command(command)
	_assert_true(preview.allowed, "连珠箭应允许一次锁定三个离散敌方地格。")
	_assert_eq(preview.target_unit_ids.size(), 3, "连珠箭预览应识别三个目标单位。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "连珠箭应记录施法者变更。")
	_assert_true(enemy_a.current_hp < 30, "连珠箭应对敌人 A 造成伤害。")
	_assert_true(enemy_b.current_hp < 30, "连珠箭应对敌人 B 造成伤害。")
	_assert_true(enemy_c.current_hp < 30, "连珠箭应对敌人 C 造成伤害。")


func _test_skill_costs_and_cooldowns_apply_in_runtime() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(2, 1))
	var archer := _build_unit(&"archer_long_draw_user", Vector2i(0, 0), 3)
	archer.current_stamina = 12
	archer.current_mp = 0
	archer.known_active_skill_ids = [&"archer_long_draw"]
	archer.known_skill_level_map = {&"archer_long_draw": 1}
	var enemy := _build_enemy_unit(&"enemy_target", Vector2i(1, 0))

	state.units = {
		archer.unit_id: archer,
		enemy.unit_id: enemy,
	}
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	_assert_true(runtime._grid_service.place_unit(state, archer, archer.coord, true), "长弓测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy, enemy.coord, true), "长弓测试目标应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_long_draw"
	command.target_unit_id = enemy.unit_id
	command.target_coord = enemy.coord
	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "施放满弦狙击后应记录施法者变更。")
	_assert_eq(archer.current_stamina, 10, "满弦狙击应按文档配置扣除 2 点体力。")
	_assert_eq(int(archer.cooldowns.get(&"archer_long_draw", 0)), 3, "满弦狙击应按文档配置写入 3 TU 冷却。")

	var second_batch := runtime.issue_command(command)
	_assert_true(
		not second_batch.log_lines.is_empty() and String(second_batch.log_lines[-1]).contains("冷却"),
		"技能仍在冷却时，再次施放应给出明确提示。"
	)


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_hp = 10
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	return unit


func _build_enemy_unit(unit_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := _build_unit(unit_id, coord, 1)
	unit.faction_id = &"enemy"
	unit.current_hp = 30
	return unit


func _build_skill_test_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"skill_runtime_smoke"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
