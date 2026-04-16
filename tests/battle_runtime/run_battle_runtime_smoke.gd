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
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const EncounterAnchorData = preload("res://scripts/systems/encounter_anchor_data.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_timed_terrain_processing_accepts_dictionary_keys()
	_test_start_battle_accepts_explicit_narrow_assault_profile()
	_test_evaluate_move_rules_survive_stacked_columns()
	_test_move_command_executes_normally_on_stacked_columns()
	_test_runtime_reports_multistep_reachable_move_coords()
	_test_spawn_anchor_prefers_better_local_mobility_over_corner_slot()
	_test_movement_tags_override_water_traversal_rules()
	_test_height_delta_rebuilds_cell_columns()
	_test_height_delta_reclassifies_adjacent_water_component()
	_test_charge_preview_allows_impassable_first_step_and_resolves_as_stop()
	_test_charge_preview_allows_larger_first_step_blocker_and_resolves_as_stop()
	_test_charge_stops_at_larger_midpath_blocker_without_rollback()
	_test_large_unit_charge_respects_full_frontier_wall_blocking()
	_test_large_unit_charge_still_resolves_frontier_blockers()
	_test_large_unit_charge_stops_on_partial_frontier_terrain_in_all_directions()
	_test_large_unit_charge_stops_on_partial_frontier_height_in_all_directions()
	_test_large_unit_charge_stops_at_large_blockers_in_all_directions()
	_test_large_unit_charge_can_side_push_blocker()
	_test_large_unit_charge_prefers_lower_side_push_and_applies_fall_damage()
	_test_large_unit_charge_collision_kills_blocker()
	_test_large_unit_charge_force_pushes_surviving_blocker_across_height_step()
	_test_large_unit_charge_stops_when_collision_cannot_displace_blocker()
	_test_large_unit_charge_trap_stops_after_first_step()
	_test_ground_line_and_cone_skills_follow_caster_facing()
	_test_archer_multishot_uses_target_unit_ids_in_manual_order()
	_test_multi_unit_skill_uses_stable_target_order()
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


func _test_start_battle_accepts_explicit_narrow_assault_profile() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var encounter_anchor := EncounterAnchorData.new()
	encounter_anchor.entity_id = &"narrow_assault_smoke"
	encounter_anchor.display_name = "狭道突击测试"
	encounter_anchor.world_coord = Vector2i(8, 4)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"default"

	var ally_a := _build_unit(&"narrow_assault_ally_a", Vector2i.ZERO, 3)
	var ally_b := _build_unit(&"narrow_assault_ally_b", Vector2i.ZERO, 3)
	var state := runtime.start_battle(
		encounter_anchor,
		20260417,
		{
			"battle_terrain_profile": "narrow_assault",
			"battle_map_size": Vector2i(19, 11),
			"battle_party": [ally_a.to_dict(), ally_b.to_dict()],
			"enemy_unit_count": 2,
		}
	)
	_assert_true(state != null and not state.is_empty(), "BattleRuntimeModule.start_battle() 应能显式启动 narrow_assault 地形。")
	if state == null or state.is_empty():
		return

	_assert_eq(String(state.terrain_profile_id), "narrow_assault", "显式 battle_terrain_profile 应进入正式 narrow_assault battle state。")
	_assert_eq(state.map_size, Vector2i(19, 11), "显式 narrow_assault 入口应保留请求的 battle_map_size。")
	_assert_eq(state.ally_unit_ids.size(), 2, "显式 narrow_assault 入口应保留传入的 ally battle party。")
	_assert_eq(state.enemy_unit_ids.size(), 2, "显式 narrow_assault 入口应构建请求数量的敌方单位。")

	var center_x := int(state.map_size.x / 2)
	for ally_unit_id in state.ally_unit_ids:
		var ally_unit := state.units.get(ally_unit_id) as BattleUnitState
		_assert_true(ally_unit != null, "narrow_assault 入口构建后，友军单位应可从 state.units 读取。")
		if ally_unit == null:
			continue
		_assert_true(ally_unit.coord.x < center_x, "narrow_assault 入口应把友军部署在突破线左侧 staging 区。")
	for enemy_unit_id in state.enemy_unit_ids:
		var enemy_unit := state.units.get(enemy_unit_id) as BattleUnitState
		_assert_true(enemy_unit != null, "narrow_assault 入口构建后，敌军单位应可从 state.units 读取。")
		if enemy_unit == null:
			continue
		_assert_true(enemy_unit.coord.x >= center_x, "narrow_assault 入口应把敌军部署在突破线右侧 staging 区。")

	var explicit_prop_counts := _count_explicit_props(state)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER, 0)),
		1,
		"narrow_assault 入口生成的 battle state 应保留唯一 objective marker。"
	)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TENT, 0)),
		2,
		"narrow_assault 入口生成的 battle state 应保留双方 tent。"
	)
	_assert_eq(
		int(explicit_prop_counts.get(BattleBoardPropCatalog.PROP_TORCH, 0)),
		2,
		"narrow_assault 入口生成的 battle state 应保留左右 torch。"
	)
	_assert_true(
		_count_terrain_cells(state, BattleCellState.TERRAIN_SPIKE) >= 1,
		"narrow_assault 入口生成的 battle state 应保留突破口后的 spike kill-zone。"
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


func _test_runtime_reports_multistep_reachable_move_coords() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"move_reachable_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(4, 2)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	var mud_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	if mud_cell != null:
		mud_cell.base_terrain = BattleCellState.TERRAIN_MUD
		mud_cell.recalculate_runtime_values()
	var blocked_cell := state.cells.get(Vector2i(3, 1)) as BattleCellState
	if blocked_cell != null:
		blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_cell.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var unit := _build_unit(&"move_reachable_unit", Vector2i(0, 0), 2)
	state.units[unit.unit_id] = unit
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "移动范围测试单位应成功放入起点。")
	runtime._state = state

	var reachable_coords := runtime.get_unit_reachable_move_coords(unit)
	_assert_true(reachable_coords.has(Vector2i(0, 1)), "可达集应包含一步可达的 land 地格。")
	_assert_true(reachable_coords.has(Vector2i(1, 1)), "可达集应包含两步可达地格，而不只是相邻地格。")
	_assert_true(reachable_coords.has(Vector2i(1, 0)), "可达集应包含花费 2 AP 的泥地。")
	_assert_true(not reachable_coords.has(Vector2i(2, 0)), "穿过泥地后超出 AP 预算的地格不应进入可达集。")
	_assert_true(not reachable_coords.has(Vector2i(3, 1)), "不可通行的水域不应进入可达集。")

	var move_command := BattleCommand.new()
	move_command.command_type = BattleCommand.TYPE_MOVE
	move_command.unit_id = unit.unit_id
	move_command.target_coord = Vector2i(1, 1)
	var preview := runtime.preview_command(move_command)
	_assert_true(preview.allowed, "两步内蓝色可达格应允许普通移动预览。")

	var batch := runtime.issue_command(move_command)
	_assert_true(unit.coord == Vector2i(1, 1), "issue_command(move) 应允许直接移动到两步内可达终点。")
	_assert_true(unit.current_ap == 0, "多步移动后应累计扣除整条路径消耗。")
	_assert_true(batch.changed_unit_ids.has(unit.unit_id), "多步移动批次应记录变更单位。")
	_assert_true(state.cells[Vector2i(1, 1)].occupant_unit_id == unit.unit_id, "多步移动后目标地格占位应同步更新。")


func _test_spawn_anchor_prefers_better_local_mobility_over_corner_slot() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"spawn_anchor_mobility_smoke"
	state.phase = &"timeline_running"
	state.map_size = Vector2i(4, 4)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	var blocked_corner_exit := state.cells.get(Vector2i(0, 2)) as BattleCellState
	if blocked_corner_exit != null:
		blocked_corner_exit.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_corner_exit.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var unit := _build_unit(&"spawn_anchor_unit", Vector2i.ZERO, 7)
	var preferred_coords: Array[Vector2i] = [
		Vector2i(0, 3),
		Vector2i(1, 3),
		Vector2i(0, 2),
	]
	var chosen_coord := runtime._find_spawn_anchor(unit, preferred_coords)
	_assert_eq(
		chosen_coord,
		Vector2i(1, 3),
		"spawn ring 含角落死角时，运行时应优先选择局部机动空间更大的出生格。"
	)


func _test_movement_tags_override_water_traversal_rules() -> void:
	var grid_service := BattleGridService.new()
	var lane_state := BattleState.new()
	lane_state.battle_id = &"water_tags_smoke"
	lane_state.phase = &"unit_acting"
	lane_state.map_size = Vector2i(4, 1)
	lane_state.timeline = BattleTimelineState.new()
	lane_state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
		Vector2i(2, 0): _build_cell(Vector2i(2, 0)),
		Vector2i(3, 0): _build_cell(Vector2i(3, 0)),
	}
	(lane_state.cells.get(Vector2i(1, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_SHALLOW_WATER
	(lane_state.cells.get(Vector2i(2, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_FLOWING_WATER
	(lane_state.cells.get(Vector2i(3, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	for cell_variant in lane_state.cells.values():
		var lane_cell := cell_variant as BattleCellState
		if lane_cell != null:
			lane_cell.recalculate_runtime_values()
	lane_state.cell_columns = BattleCellState.build_columns_from_surface_cells(lane_state.cells)

	var default_unit := _build_unit(&"default_water_unit", Vector2i.ZERO, 3)
	default_unit.movement_tags = []
	lane_state.units[default_unit.unit_id] = default_unit
	lane_state.ally_unit_ids = [default_unit.unit_id]
	lane_state.active_unit_id = default_unit.unit_id
	_assert_true(grid_service.place_unit(lane_state, default_unit, default_unit.coord, true), "默认地面单位应成功放入起点。")
	_assert_true(
		grid_service.evaluate_move(lane_state, Vector2i.ZERO, Vector2i(1, 0), default_unit).get("allowed", false),
		"默认地面单位应能进入浅水。"
	)
	_assert_true(
		not grid_service.can_unit_enter_coord(lane_state, Vector2i(3, 0), default_unit),
		"默认地面单位不应进入深水。"
	)

	var wade_unit := _build_unit(&"wade_water_unit", Vector2i.ZERO, 3)
	wade_unit.movement_tags = [BattleTerrainRules.TAG_WADE]
	_assert_eq(grid_service.get_unit_move_cost(lane_state, wade_unit, Vector2i(1, 0)), 1, "涉水单位进入浅水应只消耗 1 AP。")
	_assert_eq(grid_service.get_unit_move_cost(lane_state, wade_unit, Vector2i(2, 0)), 2, "涉水单位进入流水应消耗 2 AP。")

	var amphibious_state := BattleState.new()
	amphibious_state.battle_id = &"amphibious_water_unit"
	amphibious_state.phase = &"unit_acting"
	amphibious_state.map_size = Vector2i(2, 1)
	amphibious_state.timeline = BattleTimelineState.new()
	amphibious_state.cells = {
		Vector2i(0, 0): _build_cell(Vector2i(0, 0)),
		Vector2i(1, 0): _build_cell(Vector2i(1, 0)),
	}
	(amphibious_state.cells.get(Vector2i(1, 0)) as BattleCellState).base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	for cell_variant in amphibious_state.cells.values():
		var amphibious_cell := cell_variant as BattleCellState
		if amphibious_cell != null:
			amphibious_cell.recalculate_runtime_values()
	amphibious_state.cell_columns = BattleCellState.build_columns_from_surface_cells(amphibious_state.cells)
	var amphibious_unit := _build_unit(&"amphibious_unit", Vector2i.ZERO, 2)
	amphibious_unit.movement_tags = [BattleTerrainRules.TAG_AMPHIBIOUS]
	amphibious_state.units[amphibious_unit.unit_id] = amphibious_unit
	amphibious_state.ally_unit_ids = [amphibious_unit.unit_id]
	amphibious_state.active_unit_id = amphibious_unit.unit_id
	_assert_true(grid_service.place_unit(amphibious_state, amphibious_unit, amphibious_unit.coord, true), "两栖单位应成功放入起点。")
	_assert_true(
		bool(grid_service.evaluate_move(amphibious_state, Vector2i.ZERO, Vector2i(1, 0), amphibious_unit).get("allowed", false)),
		"两栖单位应能进入深水。"
	)


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


func _test_height_delta_reclassifies_adjacent_water_component() -> void:
	var runtime := BattleRuntimeModule.new()
	var state := BattleState.new()
	state.battle_id = &"water_reclassify_smoke"
	state.phase = &"unit_acting"
	state.map_size = Vector2i(3, 3)
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var cell := _build_cell(Vector2i(x, y))
			cell.base_height = 5
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	var center_water := state.cells.get(Vector2i(1, 1)) as BattleCellState
	center_water.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
	center_water.base_height = 4
	center_water.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"height_delta"
	effect_def.height_delta = -1
	var batch := BattleEventBatch.new()
	var applied := runtime._apply_ground_terrain_effects(null, null, [effect_def], [Vector2i(1, 0)], batch)
	_assert_true(bool(applied.get("applied", false)), "降低堤岸后应触发邻近水域重分类。")
	_assert_eq(
		center_water.base_terrain,
		BattleCellState.TERRAIN_FLOWING_WATER,
		"当相邻地格降低到水面时，封闭水域应重分类为流水。"
	)
	_assert_eq(center_water.flow_direction, Vector2i.UP, "被击穿的流水应记录通向出口的流向。")


func _test_charge_preview_allows_impassable_first_step_and_resolves_as_stop() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 1))
	var blocked_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	if blocked_cell != null:
		blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
		blocked_cell.recalculate_runtime_values()
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var charger := _build_unit(&"charge_blocked_by_terrain", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "冲锋测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(3, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "首步被不可通行地形阻挡时，冲锋预览仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "首步被地形阻挡时，冲锋应原地停下。")
	_assert_eq(charger.current_ap, 0, "首步被地形阻挡时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"首步被地形阻挡时，日志应明确记录这是一次起步即停止的冲锋。 log=%s" % [str(batch.log_lines)]
	)


func _test_charge_preview_allows_larger_first_step_blocker_and_resolves_as_stop() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 3))
	var charger := _build_unit(&"charge_blocked_by_unit", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_large_blocker", Vector2i(1, 0))
	blocker.body_size = 3
	blocker.refresh_footprint()

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "冲锋测试单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "大型阻挡单位应能成功放入测试战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "首步被更大体型单位阻挡时，冲锋预览仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "首步被更大体型单位阻挡时，冲锋应原地停下。")
	_assert_eq(charger.current_ap, 0, "首步被更大体型单位阻挡时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"首步被更大体型单位阻挡时，日志应明确记录这是一次起步即停止的冲锋。 log=%s" % [str(batch.log_lines)]
	)


func _test_charge_stops_at_larger_midpath_blocker_without_rollback() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 3))
	var charger := _build_unit(&"charge_midpath_blocked", Vector2i.ZERO, 1)
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_midpath_large_blocker", Vector2i(2, 0))
	blocker.body_size = 3
	blocker.refresh_footprint()

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "中途阻挡测试中的冲锋单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "中途阻挡测试中的大型单位应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "中途才遇到更大体型单位时，冲锋预览应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i(1, 0), "中途被更大体型单位拦住时，应保留已完成的前进一步而不是回退。")
	_assert_eq(charger.current_ap, 0, "中途被更大体型单位拦住时，冲锋仍应消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("更大体型")),
		"中途被更大体型单位拦住时，日志应给出明确原因。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_respects_full_frontier_wall_blocking() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(5, 3))
	(state.cells.get(Vector2i(1, 0)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	(state.cells.get(Vector2i(1, 1)) as BattleCellState).set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)

	var charger := _build_unit(&"charge_large_unit", Vector2i.ZERO, 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "2x2 单位的冲锋预览在首步被整条前沿墙阻挡时仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i.ZERO, "2x2 单位冲锋时应检查整条前沿边，不能穿过只挡前沿列的墙。")
	_assert_eq(charger.current_ap, 0, "2x2 单位首步被墙挡住时，冲锋仍应按 stop 流程消耗 AP。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"2x2 单位首步被墙挡住时，日志应记录起步即停止。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_still_resolves_frontier_blockers() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(Vector2i(6, 3))
	var charger := _build_unit(&"charge_large_unit_vs_blocker", Vector2i.ZERO, 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	var blocker := _build_enemy_unit(&"charge_frontier_blocker", Vector2i(2, 0))

	state.units = {
		charger.unit_id: charger,
		blocker.unit_id: blocker,
	}
	state.ally_unit_ids = [charger.unit_id]
	state.enemy_unit_ids = [blocker.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋单位应能成功放入起点。")
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 冲锋前沿的阻挡单位应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = Vector2i(4, 0)

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "2x2 单位前沿有阻挡单位时，冲锋预览仍应允许尝试。")

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, Vector2i(3, 0), "2x2 单位遇到前沿 1x1 阻挡时，仍应进入推挤分支并继续完成冲锋。")
	_assert_eq(blocker.coord, Vector2i(5, 0), "2x2 单位的前沿阻挡应被持续向前顶开，而不是被误判为地形阻挡。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("向前顶开")),
		"2x2 单位冲锋遇到前沿阻挡时，日志应记录推挤而不是地形停步。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_stops_on_partial_frontier_terrain_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		var blocked_coord: Vector2i = case_data.get("partial_frontier_coord", Vector2i.ZERO)
		var blocked_cell := state.cells.get(blocked_coord) as BattleCellState
		if blocked_cell != null:
			blocked_cell.base_terrain = BattleCellState.TERRAIN_DEEP_WATER
			blocked_cell.recalculate_runtime_values()
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步有单格不可通行地形时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步只有半个前沿不可通行时，也应整段停下。" % case_data.get("label", "未知"))
		_assert_eq(charger.current_ap, 0, "2x2 单位在%s方向首步被单格不可通行地形拦住时，冲锋仍应消耗 AP。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
			"2x2 单位在%s方向首步被单格不可通行地形拦住时，应记录起步即停止。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_stops_on_partial_frontier_height_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		_set_cell_height(state, case_data.get("partial_frontier_coord", Vector2i.ZERO), 7)
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步有单格高差过大时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步只有半个前沿高差过大时，也应整段停下。" % case_data.get("label", "未知"))
		_assert_eq(charger.current_ap, 0, "2x2 单位在%s方向首步被单格高差拦住时，冲锋仍应消耗 AP。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
			"2x2 单位在%s方向首步被单格高差拦住时，应记录起步即停止。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_stops_at_large_blockers_in_all_directions() -> void:
	for case_data in _get_large_charge_direction_cases():
		var fixture := _build_large_charge_fixture(case_data, true)
		var runtime := fixture.get("runtime") as BattleRuntimeModule
		var state := fixture.get("state") as BattleState
		var charger := fixture.get("charger") as BattleUnitState
		var command := fixture.get("command") as BattleCommand
		var blocker_id := StringName("charge_large_blocker_%s" % String(case_data.get("label", "dir")))
		var blocker := _build_enemy_unit(blocker_id, case_data.get("large_blocker_anchor", Vector2i.ZERO))
		blocker.body_size = 3
		blocker.refresh_footprint()
		state.units[blocker.unit_id] = blocker
		state.enemy_unit_ids.append(blocker.unit_id)
		_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 冲锋在%s方向的大体型阻挡单位应能成功放入战场。" % case_data.get("label", "未知"))
		runtime._state = state

		var preview := runtime.preview_command(command)
		_assert_true(preview != null and preview.allowed, "2x2 单位在%s方向首步遇到 2x2 阻挡时，冲锋预览仍应允许尝试。" % case_data.get("label", "未知"))

		var batch := runtime.issue_command(command)
		_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位在%s方向首步遇到另一名 2x2 单位时，应停在原地。" % case_data.get("label", "未知"))
		_assert_true(
			batch.log_lines.any(func(line): return String(line).contains("无法继续冲锋")),
			"2x2 单位在%s方向首步遇到另一名 2x2 单位时，应记录大体型阻挡原因。 log=%s" % [case_data.get("label", "未知"), str(batch.log_lines)]
		)


func _test_large_unit_charge_can_side_push_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_side_push_blocker", case_data.get("side_push_blocker_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.enemy_unit_ids.append(blocker.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 侧推分支的阻挡单位应能成功放入战场。")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位在首步发生侧推后，应完成本次前进一步。")
	_assert_eq(blocker.coord, case_data.get("side_push_coord", Vector2i.ZERO), "2x2 单位在首步遇到偏置前沿阻挡时，应把阻挡单位顶向侧面。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("顶向侧面")),
		"2x2 单位触发侧推时，应记录侧推日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_prefers_lower_side_push_and_applies_fall_damage() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_side_push_fall_blocker", case_data.get("side_push_blocker_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.enemy_unit_ids.append(blocker.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 侧推跌落分支的阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("side_push_coord", Vector2i.ZERO), 2)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(blocker.coord, case_data.get("side_push_coord", Vector2i.ZERO), "2x2 单位在首步侧推时，应优先把阻挡单位顶向更低的侧向地格。")
	_assert_eq(blocker.current_hp, 26, "2x2 单位把阻挡单位侧推下两层时，应结算坠落伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("坠落伤害")),
		"2x2 单位触发侧推跌落时，应记录坠落伤害日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_collision_kills_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_collision_kill_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_collision_side_guard", Vector2i(3, 1))
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 碰撞击倒分支的阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 碰撞击倒分支的侧向阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("forward_coord", Vector2i.ZERO), 7)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位撞倒阻挡后，应完成本次前进一步。")
	_assert_true(not blocker.is_alive and blocker.current_hp == 0, "2x2 单位发生碰撞击倒时，应清除阻挡单位的存活状态。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("撞上")) and batch.log_lines.any(func(line): return String(line).contains("被击倒")),
		"2x2 单位撞倒阻挡时，应同时记录碰撞与击倒日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_force_pushes_surviving_blocker_across_height_step() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_force_push_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_force_push_side_guard", Vector2i(3, 1))
	blocker.current_hp = 40
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 强制撞退分支的阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 强制撞退分支的侧向阻挡单位应能成功放入战场。")
	_set_cell_height(state, case_data.get("forward_coord", Vector2i.ZERO), 7)
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位把阻挡单位强行撞退后，应完成本次前进一步。")
	_assert_eq(blocker.coord, case_data.get("forward_coord", Vector2i.ZERO), "2x2 单位在普通前推失败但强制撞退可行时，应把阻挡单位撞退到前方高差地格。")
	_assert_eq(blocker.current_hp, 10, "2x2 单位强制撞退存活阻挡时，应先结算碰撞伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("强行撞退一格")),
		"2x2 单位触发强制撞退时，应记录强制位移日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_stops_when_collision_cannot_displace_blocker() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, true)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var blocker := _build_enemy_unit(&"charge_collision_stop_blocker", case_data.get("forward_blocker_coord", Vector2i.ZERO))
	var side_guard := _build_enemy_unit(&"charge_collision_stop_side_guard", Vector2i(3, 1))
	blocker.current_hp = 40
	var blocking_wall := _build_enemy_unit(&"charge_collision_stop_wall", case_data.get("forward_coord", Vector2i.ZERO))
	state.units[blocker.unit_id] = blocker
	state.units[side_guard.unit_id] = side_guard
	state.units[blocking_wall.unit_id] = blocking_wall
	state.enemy_unit_ids.append(blocker.unit_id)
	state.enemy_unit_ids.append(side_guard.unit_id)
	state.enemy_unit_ids.append(blocking_wall.unit_id)
	_assert_true(runtime._grid_service.place_unit(state, blocker, blocker.coord, true), "2x2 碰撞停步分支的首个阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, side_guard, side_guard.coord, true), "2x2 碰撞停步分支的侧向阻挡单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, blocking_wall, blocking_wall.coord, true), "2x2 碰撞停步分支的第二个阻挡单位应能成功放入战场。")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("start_coord", Vector2i.ZERO), "2x2 单位碰撞后仍无法挪开阻挡时，应在原地 stop。")
	_assert_eq(blocker.coord, case_data.get("forward_blocker_coord", Vector2i.ZERO), "2x2 单位碰撞停步时，首个阻挡单位不应被错误位移。")
	_assert_eq(blocker.current_hp, 10, "2x2 单位碰撞停步时，仍应先结算碰撞伤害。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("撞上")) and batch.log_lines.any(func(line): return String(line).contains("起步时被拦下")),
		"2x2 单位碰撞后仍无法挪开阻挡时，应同时记录碰撞与 stop 日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_large_unit_charge_trap_stops_after_first_step() -> void:
	var case_data: Dictionary = _get_large_charge_direction_cases()[0]
	var fixture := _build_large_charge_fixture(case_data, false)
	var runtime := fixture.get("runtime") as BattleRuntimeModule
	var state := fixture.get("state") as BattleState
	var charger := fixture.get("charger") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	var trap_cell := state.cells.get(case_data.get("trap_coord", Vector2i.ZERO)) as BattleCellState
	if trap_cell != null:
		trap_cell.terrain_effect_ids.append(&"trap_large_unit_smoke")
	runtime._state = state

	var batch := runtime.issue_command(command)
	_assert_eq(charger.coord, case_data.get("first_anchor", Vector2i.ZERO), "2x2 单位首步踩中 trap 时，应保留首步位移并停止后续冲锋。")
	_assert_true(trap_cell != null and trap_cell.terrain_effect_ids.is_empty(), "2x2 单位触发 trap 后，应移除对应地格上的 trap 标记。")
	_assert_true(
		batch.log_lines.any(func(line): return String(line).contains("触发陷阱")),
		"2x2 单位踩中 trap 时，应记录 trap 中断日志。 log=%s" % [str(batch.log_lines)]
	)


func _test_ground_line_and_cone_skills_follow_caster_facing() -> void:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var line_state := _build_skill_test_state(Vector2i(5, 5))
	var line_user := _build_unit(&"line_skill_user", Vector2i(2, 2), 3)
	line_user.current_mp = 3
	line_user.known_active_skill_ids = [&"mage_flame_spear"]
	line_user.known_skill_level_map = {&"mage_flame_spear": 1}
	var line_enemy_front := _build_enemy_unit(&"line_enemy_front", Vector2i(2, 0))
	var line_enemy_side := _build_enemy_unit(&"line_enemy_side", Vector2i(3, 1))
	line_state.units = {
		line_user.unit_id: line_user,
		line_enemy_front.unit_id: line_enemy_front,
		line_enemy_side.unit_id: line_enemy_side,
	}
	line_state.ally_unit_ids = [line_user.unit_id]
	line_state.enemy_unit_ids = [line_enemy_front.unit_id, line_enemy_side.unit_id]
	line_state.active_unit_id = line_user.unit_id
	_assert_true(runtime._grid_service.place_unit(line_state, line_user, line_user.coord, true), "直线技能测试施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(line_state, line_enemy_front, line_enemy_front.coord, true), "直线技能前方敌人应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(line_state, line_enemy_side, line_enemy_side.coord, true), "直线技能侧向敌人应能成功放入战场。")
	runtime._state = line_state

	var line_command := BattleCommand.new()
	line_command.command_type = BattleCommand.TYPE_SKILL
	line_command.unit_id = line_user.unit_id
	line_command.skill_id = &"mage_flame_spear"
	line_command.target_coord = Vector2i(2, 1)
	var line_preview := runtime.preview_command(line_command)
	_assert_true(line_preview.allowed, "炎枪术应允许指向施法者正前方的地格。")
	_assert_true(line_preview.target_coords.has(Vector2i(2, 0)), "炎枪术应沿施法者面向继续向前扩展。")
	_assert_true(not line_preview.target_coords.has(Vector2i(3, 1)), "炎枪术不应在正前方施放时横向偏转。")
	runtime.issue_command(line_command)
	_assert_true(line_enemy_front.current_hp < 30, "炎枪术应命中正前方敌人。")
	_assert_eq(line_enemy_side.current_hp, 30, "炎枪术不应误伤侧向敌人。")

	var cone_state := _build_skill_test_state(Vector2i(5, 5))
	var cone_user := _build_unit(&"cone_skill_user", Vector2i(2, 2), 3)
	cone_user.current_stamina = 12
	cone_user.known_active_skill_ids = [&"warrior_sweeping_slash"]
	cone_user.known_skill_level_map = {&"warrior_sweeping_slash": 1}
	var cone_enemy_front := _build_enemy_unit(&"cone_enemy_front", Vector2i(2, 0))
	var cone_enemy_side := _build_enemy_unit(&"cone_enemy_side", Vector2i(3, 1))
	cone_state.units = {
		cone_user.unit_id: cone_user,
		cone_enemy_front.unit_id: cone_enemy_front,
		cone_enemy_side.unit_id: cone_enemy_side,
	}
	cone_state.ally_unit_ids = [cone_user.unit_id]
	cone_state.enemy_unit_ids = [cone_enemy_front.unit_id, cone_enemy_side.unit_id]
	cone_state.active_unit_id = cone_user.unit_id
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_user, cone_user.coord, true), "锥形技能测试施法者应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_enemy_front, cone_enemy_front.coord, true), "锥形技能前方敌人应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(cone_state, cone_enemy_side, cone_enemy_side.coord, true), "锥形技能侧向敌人应能成功放入战场。")
	runtime._state = cone_state

	var cone_command := BattleCommand.new()
	cone_command.command_type = BattleCommand.TYPE_SKILL
	cone_command.unit_id = cone_user.unit_id
	cone_command.skill_id = &"warrior_sweeping_slash"
	cone_command.target_coord = Vector2i(2, 1)
	var cone_preview := runtime.preview_command(cone_command)
	_assert_true(cone_preview.allowed, "横扫应允许指向施法者正前方的地格。")
	_assert_true(cone_preview.target_coords.has(Vector2i(2, 0)), "横扫应沿施法者面向向前展开扇形。")
	_assert_true(not cone_preview.target_coords.has(Vector2i(3, 1)), "横扫不应在正前方施放时改为向右扇出。")
	runtime.issue_command(cone_command)
	_assert_true(cone_enemy_front.current_hp < 30, "横扫应命中正前方敌人。")
	_assert_eq(cone_enemy_side.current_hp, 30, "横扫不应误伤右侧敌人。")


func _test_archer_multishot_uses_target_unit_ids_in_manual_order() -> void:
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
	command.target_unit_ids = [enemy_c.unit_id, enemy_a.unit_id, enemy_b.unit_id]
	var preview := runtime.preview_command(command)
	_assert_true(preview.allowed, "连珠箭应允许一次锁定三个离散敌方单位。")
	_assert_eq(preview.target_unit_ids.size(), 3, "连珠箭预览应识别三个目标单位。")
	_assert_eq(preview.target_unit_ids, [enemy_c.unit_id, enemy_a.unit_id, enemy_b.unit_id], "连珠箭预览应保持玩家选择顺序。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "连珠箭应记录施法者变更。")
	_assert_eq(archer.current_stamina, 18, "连珠箭应只按一次施放消耗体力。")
	_assert_eq(
		batch.log_lines[0].find(String(enemy_c.display_name)) >= 0 and batch.log_lines[1].find(String(enemy_a.display_name)) >= 0 and batch.log_lines[2].find(String(enemy_b.display_name)) >= 0,
		true,
		"连珠箭日志应按玩家选择顺序依次结算。"
	)
	_assert_true(enemy_a.current_hp < 30, "连珠箭应对敌人 A 造成伤害。")
	_assert_true(enemy_b.current_hp < 30, "连珠箭应对敌人 B 造成伤害。")
	_assert_true(enemy_c.current_hp < 30, "连珠箭应对敌人 C 造成伤害。")


func _test_multi_unit_skill_uses_stable_target_order() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var arcane_missile := skill_defs.get(&"mage_arcane_missile") as SkillDef
	if arcane_missile != null and arcane_missile.combat_profile != null:
		arcane_missile.combat_profile.selection_order_mode = &"stable"

	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {})

	var state := _build_skill_test_state(Vector2i(4, 2))
	var mage := _build_unit(&"mage_arcane_missile_user", Vector2i(0, 1), 3)
	mage.current_mp = 3
	mage.known_active_skill_ids = [&"mage_arcane_missile"]
	mage.known_skill_level_map = {&"mage_arcane_missile": 1}
	var enemy_a := _build_enemy_unit(&"enemy_a", Vector2i(2, 0))
	var enemy_b := _build_enemy_unit(&"enemy_b", Vector2i(0, 0))
	var enemy_c := _build_enemy_unit(&"enemy_c", Vector2i(1, 0))

	state.units = {
		mage.unit_id: mage,
		enemy_a.unit_id: enemy_a,
		enemy_b.unit_id: enemy_b,
		enemy_c.unit_id: enemy_c,
	}
	state.ally_unit_ids = [mage.unit_id]
	state.enemy_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	state.active_unit_id = mage.unit_id
	_assert_true(runtime._grid_service.place_unit(state, mage, mage.coord, true), "奥术飞弹测试单位应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_a, enemy_a.coord, true), "敌人 A 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_b, enemy_b.coord, true), "敌人 B 应能成功放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, enemy_c, enemy_c.coord, true), "敌人 C 应能成功放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = mage.unit_id
	command.skill_id = &"mage_arcane_missile"
	command.target_unit_ids = [enemy_a.unit_id, enemy_b.unit_id, enemy_c.unit_id]
	var preview := runtime.preview_command(command)
	_assert_true(preview.allowed, "奥术飞弹应允许一次锁定三个离散敌方单位。")
	_assert_eq(preview.target_unit_ids, [enemy_b.unit_id, enemy_c.unit_id, enemy_a.unit_id], "稳定排序应按战场坐标归一化目标顺序。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(mage.unit_id), "奥术飞弹应记录施法者变更。")
	_assert_eq(mage.current_mp, 2, "奥术飞弹应只按一次施放消耗法力。")
	_assert_true(
		batch.log_lines.size() >= 3
			and batch.log_lines[0].find(String(enemy_b.display_name)) >= 0
			and batch.log_lines[1].find(String(enemy_c.display_name)) >= 0
			and batch.log_lines[2].find(String(enemy_a.display_name)) >= 0,
		"奥术飞弹日志应按稳定排序后的命中顺序依次结算。"
	)


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


func _count_explicit_props(state: BattleState) -> Dictionary:
	var counts := {
		BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER: 0,
		BattleBoardPropCatalog.PROP_TENT: 0,
		BattleBoardPropCatalog.PROP_TORCH: 0,
	}
	if state == null:
		return counts
	for cell_variant in state.cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		for prop_id in cell.prop_ids:
			if counts.has(prop_id):
				counts[prop_id] = int(counts.get(prop_id, 0)) + 1
	return counts


func _count_terrain_cells(state: BattleState, terrain_id: StringName) -> int:
	if state == null:
		return 0
	var count := 0
	for cell_variant in state.cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		if cell.base_terrain == terrain_id:
			count += 1
	return count


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


func _get_large_charge_direction_cases() -> Array[Dictionary]:
	return [
		{
			"label": "向右",
			"direction": Vector2i.RIGHT,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(1, 2),
			"short_target_coord": Vector2i(3, 2),
			"target_coord": Vector2i(5, 2),
			"first_anchor": Vector2i(2, 2),
			"partial_frontier_coord": Vector2i(3, 2),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(3, 4),
			"forward_blocker_coord": Vector2i(3, 2),
			"forward_coord": Vector2i(4, 2),
			"large_blocker_anchor": Vector2i(3, 2),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向左",
			"direction": Vector2i.LEFT,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(4, 2),
			"short_target_coord": Vector2i(3, 2),
			"target_coord": Vector2i(1, 2),
			"first_anchor": Vector2i(3, 2),
			"partial_frontier_coord": Vector2i(3, 2),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(3, 4),
			"forward_blocker_coord": Vector2i(3, 2),
			"forward_coord": Vector2i(2, 2),
			"large_blocker_anchor": Vector2i(2, 2),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向下",
			"direction": Vector2i.DOWN,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 1),
			"short_target_coord": Vector2i(2, 3),
			"target_coord": Vector2i(2, 5),
			"first_anchor": Vector2i(2, 2),
			"partial_frontier_coord": Vector2i(2, 3),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(4, 3),
			"forward_blocker_coord": Vector2i(2, 3),
			"forward_coord": Vector2i(2, 4),
			"large_blocker_anchor": Vector2i(2, 3),
			"trap_coord": Vector2i(3, 3),
		},
		{
			"label": "向上",
			"direction": Vector2i.UP,
			"map_size": Vector2i(7, 7),
			"start_coord": Vector2i(2, 4),
			"short_target_coord": Vector2i(2, 3),
			"target_coord": Vector2i(2, 1),
			"first_anchor": Vector2i(2, 3),
			"partial_frontier_coord": Vector2i(2, 3),
			"side_push_blocker_coord": Vector2i(3, 3),
			"side_push_coord": Vector2i(4, 3),
			"forward_blocker_coord": Vector2i(2, 3),
			"forward_coord": Vector2i(2, 2),
			"large_blocker_anchor": Vector2i(2, 2),
			"trap_coord": Vector2i(3, 3),
		},
	]


func _build_large_charge_fixture(case_data: Dictionary, use_short_target: bool) -> Dictionary:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})

	var state := _build_skill_test_state(case_data.get("map_size", Vector2i(7, 7)))
	var charger_id := StringName("large_charge_tester_%s" % String(case_data.get("label", "dir")))
	var charger := _build_unit(charger_id, case_data.get("start_coord", Vector2i.ZERO), 1)
	charger.body_size = 3
	charger.refresh_footprint()
	charger.known_active_skill_ids = [&"charge"]
	charger.known_skill_level_map = {&"charge": 1}
	state.units = {charger.unit_id: charger}
	state.ally_unit_ids = [charger.unit_id]
	state.active_unit_id = charger.unit_id
	_assert_true(runtime._grid_service.place_unit(state, charger, charger.coord, true), "2x2 冲锋夹具中的测试单位应能成功放入起点。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = charger.unit_id
	command.skill_id = &"charge"
	command.target_coord = case_data.get("short_target_coord" if use_short_target else "target_coord", Vector2i.ZERO)
	return {
		"runtime": runtime,
		"state": state,
		"charger": charger,
		"command": command,
	}


func _set_cell_height(state: BattleState, coord: Vector2i, height: int) -> void:
	var cell := state.cells.get(coord) as BattleCellState
	if cell == null:
		return
	cell.base_height = height
	cell.recalculate_runtime_values()
	state.mark_runtime_edges_dirty()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
