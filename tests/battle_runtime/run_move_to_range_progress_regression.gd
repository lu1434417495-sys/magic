extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_move_to_range_prefers_progress_over_wait_when_far_from_band()
	_test_move_to_range_uses_path_detour_when_direct_progress_is_blocked()
	if _failures.is_empty():
		print("Move-to-range progress regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Move-to-range progress regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_move_to_range_prefers_progress_over_wait_when_far_from_band() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"far_gap_mover_brain"
	brain.default_state_id = &"engage"
	brain.pressure_distance = 0
	var engage_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	engage_state.state_id = &"engage"
	var move_action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	move_action.action_id = &"far_gap_close_in"
	move_action.target_selector = &"nearest_enemy"
	move_action.desired_min_distance = 4
	move_action.desired_max_distance = 5
	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"far_gap_wait"
	engage_state.actions = [move_action, wait_action]
	brain.states = [engage_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains)

	var state = _build_flat_state(Vector2i(31, 3))
	runtime._state = state
	var mover = _build_ai_unit(
		&"far_gap_enemy",
		"远距接敌者",
		&"hostile",
		Vector2i(1, 1),
		brain.brain_id,
		&"engage"
	)
	var player = _build_manual_unit(
		&"far_gap_player",
		"远距目标",
		&"player",
		Vector2i(28, 1)
	)
	_add_unit_to_state(runtime, state, mover, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, mover)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "远距离 move_to_range 回归应产出合法指令。")
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
		"当单位距离目标距离带过远时，AI 不应继续待机。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(3, 1),
		"远距离 move_to_range 回归应优先选择本回合可达的最远逼近落点。"
	)


func _test_move_to_range_uses_path_detour_when_direct_progress_is_blocked() -> void:
	var runtime = _build_runtime_with_enemy_content()
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"detour_mover_brain"
	brain.default_state_id = &"engage"
	brain.pressure_distance = 0
	var engage_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	engage_state.state_id = &"engage"
	var move_action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	move_action.action_id = &"detour_close_in"
	move_action.target_selector = &"nearest_enemy"
	move_action.desired_min_distance = 1
	move_action.desired_max_distance = 1
	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"detour_wait"
	engage_state.actions = [move_action, wait_action]
	brain.states = [engage_state]
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains)

	var state = _build_flat_state(Vector2i(7, 3))
	runtime._state = state
	var mover = _build_ai_unit(
		&"detour_enemy",
		"绕路接敌者",
		&"hostile",
		Vector2i(1, 1),
		brain.brain_id,
		&"engage"
	)
	var blocker = _build_ai_unit(
		&"detour_blocker",
		"阻挡者",
		&"hostile",
		Vector2i(2, 1),
		brain.brain_id,
		&"engage"
	)
	var player = _build_manual_unit(
		&"detour_player",
		"绕路目标",
		&"player",
		Vector2i(5, 1)
	)
	_add_unit_to_state(runtime, state, mover, true)
	_add_unit_to_state(runtime, state, blocker, true)
	_add_unit_to_state(runtime, state, player, false)
	var ai_context = _build_ai_context(runtime, mover)
	var decision = runtime._ai_service.choose_command(ai_context)
	_assert_true(decision != null and decision.command != null, "绕路 move_to_range 回归应产出合法指令。")
	_assert_eq(
		decision.command.command_type if decision != null and decision.command != null else &"",
		BATTLE_COMMAND_SCRIPT.TYPE_MOVE,
		"直接逼近被阻挡但存在绕路路径时，AI 不应待机。"
	)
	_assert_eq(
		decision.command.target_coord if decision != null and decision.command != null else Vector2i(-1, -1),
		Vector2i(2, 0),
		"绕路 move_to_range 回归应沿正式路径走到本回合可达的绕路落点。"
	)


func _build_runtime_with_enemy_content():
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		game_session.get_enemy_templates(),
		game_session.get_enemy_ai_brains(),
		null
	)
	game_session.free()
	return runtime


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"move_to_range_progress_regression"
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


func _build_ai_context(runtime, unit_state):
	var ai_context = BATTLE_AI_CONTEXT_SCRIPT.new()
	ai_context.state = runtime._state
	ai_context.unit_state = unit_state
	ai_context.grid_service = runtime._grid_service
	ai_context.skill_defs = runtime._skill_defs
	ai_context.preview_callback = Callable(runtime, "preview_command")
	ai_context.skill_score_input_callback = Callable(runtime._ai_service, "build_skill_score_input")
	ai_context.action_score_input_callback = Callable(runtime._ai_service, "build_action_score_input")
	return ai_context


func _build_ai_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	brain_id: StringName,
	state_id: StringName
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"ai"
	unit.ai_brain_id = brain_id
	unit.ai_state_id = state_id
	unit.current_hp = 26
	unit.current_mp = 0
	unit.current_stamina = 8
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 26)
	unit.attribute_snapshot.set_value(&"mp_max", 0)
	unit.attribute_snapshot.set_value(&"stamina_max", 8)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 4)
	return unit


func _build_manual_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 6)
	return unit


func _add_unit_to_state(runtime, state, unit, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	_assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
