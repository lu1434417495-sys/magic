extends SceneTree

const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_arrow_rain_applies_pinned_and_damage()
	_test_skirmish_step_repositions_and_grants_pre_aim()
	_test_evasive_roll_repositions_and_grants_evasion()
	if _failures.is_empty():
		print("Archer skill semantics regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Archer skill semantics regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_arrow_rain_applies_pinned_and_damage() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(6, 4))
	var archer := _build_unit(&"archer_arrow_rain_user", Vector2i(1, 1), 6)
	archer.current_mp = 6
	archer.current_stamina = 6
	archer.current_aura = 6
	archer.known_active_skill_ids = [&"archer_arrow_rain"]
	archer.known_skill_level_map = {&"archer_arrow_rain": 1}
	var enemy := _build_unit(&"archer_arrow_rain_target", Vector2i(3, 1), 1)
	enemy.faction_id = &"enemy"
	enemy.current_hp = 30

	_add_unit(runtime, state, archer)
	_add_unit(runtime, state, enemy)
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_arrow_rain"
	command.target_coord = enemy.coord

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "箭雨应允许在合法落点上施放。")
	_assert_true(preview != null and preview.target_unit_ids.has(enemy.unit_id), "箭雨预览应识别命中的敌人。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(enemy.unit_id), "箭雨应记录目标单位变更。")
	_assert_true(enemy.current_hp < 30, "箭雨应对目标造成实际伤害。")
	_assert_true(enemy.status_effects.has(&"pinned"), "箭雨应对目标施加压制状态。")


func _test_skirmish_step_repositions_and_grants_pre_aim() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(6, 4))
	var archer := _build_unit(&"archer_skirmish_step_user", Vector2i(3, 1), 6)
	archer.current_mp = 6
	archer.current_stamina = 6
	archer.current_aura = 6
	archer.known_active_skill_ids = [&"archer_skirmish_step"]
	archer.known_skill_level_map = {&"archer_skirmish_step": 1}
	var enemy := _build_unit(&"archer_skirmish_step_target", Vector2i(5, 1), 1)
	enemy.faction_id = &"enemy"
	var blocker_up := _build_unit(&"archer_skirmish_step_block_up", Vector2i(3, 0), 1)
	var blocker_down := _build_unit(&"archer_skirmish_step_block_down", Vector2i(3, 2), 1)

	_add_unit(runtime, state, archer)
	_add_unit(runtime, state, enemy)
	_add_unit(runtime, state, blocker_up)
	_add_unit(runtime, state, blocker_down)
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_skirmish_step"
	command.target_unit_id = archer.unit_id

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "游击步应允许自施放。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "游击步应记录施法者变更。")
	_assert_true(
		archer.coord == Vector2i(2, 0),
		"游击步应真实位移 2 格并优先远离威胁。actual=%s log=%s" % [str(archer.coord), str(batch.log_lines)]
	)
	_assert_true(archer.status_effects.has(&"archer_pre_aim"), "游击步应为施法者挂上预瞄状态。")


func _test_evasive_roll_repositions_and_grants_evasion() -> void:
	var runtime := _build_runtime()
	var state := _build_skill_test_state(Vector2i(6, 4))
	var archer := _build_unit(&"archer_evasive_roll_user", Vector2i(3, 2), 6)
	archer.current_mp = 6
	archer.current_stamina = 6
	archer.current_aura = 6
	archer.known_active_skill_ids = [&"archer_evasive_roll"]
	archer.known_skill_level_map = {&"archer_evasive_roll": 1}
	var enemy := _build_unit(&"archer_evasive_roll_target", Vector2i(5, 2), 1)
	enemy.faction_id = &"enemy"
	var blocker_up := _build_unit(&"archer_evasive_roll_block_up", Vector2i(3, 1), 1)
	var blocker_down := _build_unit(&"archer_evasive_roll_block_down", Vector2i(3, 3), 1)

	_add_unit(runtime, state, archer)
	_add_unit(runtime, state, enemy)
	_add_unit(runtime, state, blocker_up)
	_add_unit(runtime, state, blocker_down)
	state.ally_unit_ids = [archer.unit_id]
	state.enemy_unit_ids = [enemy.unit_id]
	state.active_unit_id = archer.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = archer.unit_id
	command.skill_id = &"archer_evasive_roll"
	command.target_unit_id = archer.unit_id

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "翻滚卸力应允许自施放。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(archer.unit_id), "翻滚卸力应记录施法者变更。")
	_assert_true(
		archer.coord == Vector2i(2, 1),
		"翻滚卸力应真实位移 2 格并优先远离威胁。actual=%s log=%s" % [str(archer.coord), str(batch.log_lines)]
	)
	_assert_true(archer.status_effects.has(&"dodge_bonus_up"), "翻滚卸力应为施法者挂上闪避状态。")


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	return runtime


func _build_skill_test_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"archer_skill_semantics"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(unit_id: StringName, coord: Vector2i, current_ap: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_stamina = 0
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
