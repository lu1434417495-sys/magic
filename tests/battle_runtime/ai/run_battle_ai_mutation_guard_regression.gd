extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiContext.cs")
const BATTLE_AI_DECISION_COMMITTER_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiDecisionCommitter.cs")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/BattleAiService.cs")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleState.cs")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleTimelineState.cs")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleCellState.cs")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/BattleUnitState.cs")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/BattleGridService.cs")
const BATTLE_AI_MUTATION_GUARD_TEST_ACTION_SCRIPT = preload("res://tests/battle_runtime/ai/BattleAiMutationGuardTestAction.cs")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/EnemyAiBrainDef.cs")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/EnemyAiStateDef.cs")


var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_benign_ai_bookkeeping_is_allowed()
	_test_active_unit_hp_mutation_is_blocked_and_restored()
	_test_other_unit_coord_mutation_is_blocked_and_restored()
	_test_illegal_blackboard_key_mutation_is_blocked_and_restored()
	_test_cell_occupant_mutation_is_blocked_and_restored()
	_test_cell_height_mutation_is_blocked_and_restored()
	_test_missing_brain_wait_path_is_allowed()
	_test_missing_state_wait_path_is_allowed()
	_test.finish(self, "Battle AI mutation guard regression")


func _test_benign_ai_bookkeeping_is_allowed() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"none"))
	var decision = fixture.service.choose_command(fixture.context)
	_assert_no_guard_violation(fixture.context, "普通 wait 决策不应触发 mutation guard。")
	_test.assert_true(decision != null and decision.action_id == &"test_mutation_none", "普通 action 应正常返回原 decision。")
	BATTLE_AI_DECISION_COMMITTER_SCRIPT.new().commit(fixture.actor, decision)
	_test.assert_eq(fixture.actor.ai_blackboard.get("last_action_id", ""), "test_mutation_none", "合法 decision bookkeeping 应保留。")


func _test_active_unit_hp_mutation_is_blocked_and_restored() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"active_hp"))
	var before_hp: int = int(fixture.actor.current_hp)
	var decision = fixture.service.choose_command(fixture.context)
	_assert_guard_blocked(fixture.context, decision, "active unit HP mutation 应触发 guard。")
	_test.assert_eq(fixture.actor.current_hp, before_hp, "active unit HP mutation 应被恢复。")


func _test_other_unit_coord_mutation_is_blocked_and_restored() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"other_coord"))
	var before_coord: Vector2i = fixture.hero.coord
	var before_occupied: Array = fixture.hero.occupied_coords.duplicate()
	var decision = fixture.service.choose_command(fixture.context)
	_assert_guard_blocked(fixture.context, decision, "其他单位坐标 mutation 应触发 guard。")
	_test.assert_eq(fixture.hero.coord, before_coord, "其他单位坐标 mutation 应被恢复。")
	_test.assert_eq(fixture.hero.occupied_coords, before_occupied, "其他单位 footprint cache mutation 应被恢复。")


func _test_illegal_blackboard_key_mutation_is_blocked_and_restored() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"blackboard"))
	var decision = fixture.service.choose_command(fixture.context)
	_assert_guard_blocked(fixture.context, decision, "非法 blackboard key mutation 应触发 guard。")
	_test.assert_false(fixture.actor.ai_blackboard.has("rogue_key"), "非法 blackboard key 应被移除。")


func _test_cell_occupant_mutation_is_blocked_and_restored() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"cell_occupant"))
	var cell = fixture.grid_service.get_cell(fixture.state, Vector2i(3, 1))
	var before_occupant: StringName = cell.occupant_unit_id
	var decision = fixture.service.choose_command(fixture.context)
	_assert_guard_blocked(fixture.context, decision, "cell occupant mutation 应触发 guard。")
	cell = fixture.grid_service.get_cell(fixture.state, Vector2i(3, 1))
	_test.assert_eq(cell.occupant_unit_id, before_occupant, "cell occupant mutation 应被恢复。")


func _test_cell_height_mutation_is_blocked_and_restored() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"cell_height"))
	var cell = fixture.grid_service.get_cell(fixture.state, Vector2i(0, 0))
	var before_height := int(cell.current_height)
	var before_offset := int(cell.height_offset)
	var decision = fixture.service.choose_command(fixture.context)
	_assert_guard_blocked(fixture.context, decision, "cell height mutation 应触发 guard。")
	cell = fixture.grid_service.get_cell(fixture.state, Vector2i(0, 0))
	_test.assert_eq(int(cell.current_height), before_height, "cell current_height mutation 应被恢复。")
	_test.assert_eq(int(cell.height_offset), before_offset, "cell height_offset mutation 应被恢复。")


func _test_missing_brain_wait_path_is_allowed() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"none"), false)
	var decision = fixture.service.choose_command(fixture.context)
	_assert_no_guard_violation(fixture.context, "missing brain fallback 不应触发 mutation guard。")
	_test.assert_true(decision != null and decision.action_id == &"wait_missing_brain", "missing brain 应正常回落到 wait。")


func _test_missing_state_wait_path_is_allowed() -> void:
	var fixture := _build_fixture(_make_mutation_action(&"none"), true, false)
	var decision = fixture.service.choose_command(fixture.context)
	_assert_no_guard_violation(fixture.context, "missing state fallback 不应触发 mutation guard。")
	_test.assert_true(decision != null and decision.action_id == &"wait_missing_state", "missing state 应正常回落到 wait。")


func _make_mutation_action(kind: StringName):
	var action = BATTLE_AI_MUTATION_GUARD_TEST_ACTION_SCRIPT.new()
	action.setup(kind)
	return action


func _build_fixture(action, include_brain := true, include_state := true) -> Dictionary:
	var state = _build_flat_state(Vector2i(6, 4))
	var grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()
	var actor = _build_unit(&"guard_actor", "守卫", &"hostile", Vector2i(1, 1), &"guard_brain", &"engage", 20, 2)
	var hero = _build_unit(&"hero", "玩家", &"player", Vector2i(3, 1), &"", &"", 30, 2)
	_add_unit_to_state(grid_service, state, actor, true)
	_add_unit_to_state(grid_service, state, hero, false)
	state.phase = &"unit_acting"
	state.active_unit_id = actor.unit_id

	var brain_map := {}
	if include_brain:
		var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
		brain.brain_id = &"guard_brain"
		brain.default_state_id = &"engage"
		if include_state:
			var state_def = ENEMY_AI_STATE_DEF_SCRIPT.new()
			state_def.state_id = &"engage"
			state_def.actions = [action]
			brain.states = [state_def]
		else:
			brain.states = []
		brain_map[brain.brain_id] = brain

	var service = BATTLE_AI_SERVICE_SCRIPT.new()
	service.setup(brain_map, null)
	var context = BATTLE_AI_CONTEXT_SCRIPT.new()
	context.state = state
	context.unit_state = actor
	context.grid_service = grid_service
	context.skill_defs = {}
	context.allow_authored_action_fallback_for_tests = true
	return {
		"state": state,
		"grid_service": grid_service,
		"actor": actor,
		"hero": hero,
		"service": service,
		"context": context,
	}


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"ai_mutation_guard_regression"
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND()
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _build_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	coord: Vector2i,
	brain_id: StringName,
	state_id: StringName,
	current_hp: int,
	current_ap: int
):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"ai" if brain_id != &"" else &"manual"
	unit.ai_brain_id = brain_id
	unit.ai_state_id = state_id
	unit.current_hp = current_hp
	unit.current_mp = 20
	unit.current_stamina = 10
	unit.current_ap = current_ap
	unit.current_move_points = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", maxi(current_hp, 1))
	unit.attribute_snapshot.set_value(&"mp_max", 20)
	unit.attribute_snapshot.set_value(&"stamina_max", 10)
	unit.attribute_snapshot.set_value(&"action_points", maxi(current_ap, 1))
	return unit


func _add_unit_to_state(grid_service, state, unit, is_enemy: bool) -> void:
	BattleRuntimeTestHelpers.register_unit_in_state(state, unit, is_enemy)
	var placed = grid_service.place_unit(state, unit, unit.coord, true)
	_test.assert_true(placed, "测试单位 %s 应能放入测试战场。" % String(unit.unit_id))


func _assert_guard_blocked(context, decision, message: String) -> void:
	var violations := _get_guard_violations(context)
	_test.assert_true(not violations.is_empty(), "%s violations=%s" % [message, str(violations)])
	_test.assert_true(
		decision == null,
		"%s guard 应 fail-loud 并阻断原 decision。" % message
	)


func _assert_no_guard_violation(context, message: String) -> void:
	var violations := _get_guard_violations(context)
	_test.assert_true(violations.is_empty(), "%s violations=%s" % [message, str(violations)])


func _get_guard_violations(context) -> Array:
	if context == null:
		return []
	var value = context.get("mutation_guard_violations")
	if value is Array:
		return value
	return []
