extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_dust_attack_modifier_uses_schema_not_source_id()
	_test_dust_distance_gate_and_endpoint_stacking()
	_test_dust_expires_while_battle_lifetime_terrain_stays_active()
	if _failures.is_empty():
		print("Meteor swarm terrain modifier regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm terrain modifier regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_dust_attack_modifier_uses_schema_not_source_id() -> void:
	var setup := _build_runtime_with_units(Vector2i(5, 3), Vector2i(0, 1), Vector2i(3, 1))
	var runtime: BattleRuntimeModule = setup["runtime"]
	var attacker: BattleUnitState = setup["attacker"]
	var target: BattleUnitState = setup["target"]
	var odd_named_dust := _build_dust_effect(&"schema_driven_not_meteor_named")
	_assert_true(
		runtime._terrain_effect_system.upsert_timed_terrain_effect(target.coord, attacker, null, odd_named_dust, &"dust_target"),
		"dust schema 测试应能写入 target footprint。"
	)
	var attack_check: Dictionary = _build_policy_attack_check(runtime, attacker, target)
	_assert_eq(int(attack_check.get("situational_attack_penalty", -1)), 2, "尘土命中 -2 应通过 accuracy_modifier_spec 生效，而不是靠 source id。")
	var breakdown: Array = attack_check.get("attack_roll_modifier_breakdown", [])
	_assert_eq(breakdown.size(), 1, "尘土命中修饰应输出一条 post-stack breakdown。")
	_assert_eq(int((breakdown[0] as Dictionary).get("effective_modifier_delta", 0)) if breakdown.size() > 0 else 0, -2, "breakdown 应保留有效 -2 修饰。")


func _test_dust_distance_gate_and_endpoint_stacking() -> void:
	var adjacent_setup := _build_runtime_with_units(Vector2i(4, 2), Vector2i(0, 0), Vector2i(1, 0))
	var adjacent_runtime: BattleRuntimeModule = adjacent_setup["runtime"]
	var adjacent_attacker: BattleUnitState = adjacent_setup["attacker"]
	var adjacent_target: BattleUnitState = adjacent_setup["target"]
	_assert_true(
		adjacent_runtime._terrain_effect_system.upsert_timed_terrain_effect(adjacent_target.coord, adjacent_attacker, null, _build_dust_effect(&"adjacent_dust"), &"adjacent_dust"),
		"相邻尘土 fixture 应能写入。"
	)
	var adjacent_check: Dictionary = _build_policy_attack_check(adjacent_runtime, adjacent_attacker, adjacent_target)
	_assert_true(not adjacent_check.has("attack_roll_modifier_breakdown"), "distance_min_exclusive=1 时相邻攻击不应吃尘土命中惩罚。")

	var double_setup := _build_runtime_with_units(Vector2i(5, 2), Vector2i(0, 0), Vector2i(3, 0))
	var double_runtime: BattleRuntimeModule = double_setup["runtime"]
	var double_attacker: BattleUnitState = double_setup["attacker"]
	var double_target: BattleUnitState = double_setup["target"]
	_assert_true(double_runtime._terrain_effect_system.upsert_timed_terrain_effect(double_attacker.coord, double_attacker, null, _build_dust_effect(&"attacker_dust"), &"attacker_dust"), "attacker footprint 尘土应能写入。")
	_assert_true(double_runtime._terrain_effect_system.upsert_timed_terrain_effect(double_target.coord, double_attacker, null, _build_dust_effect(&"target_dust"), &"target_dust"), "target footprint 尘土应能写入。")
	var double_check: Dictionary = _build_policy_attack_check(double_runtime, double_attacker, double_target)
	_assert_eq(int(double_check.get("situational_attack_penalty", -1)), 2, "attacker/target 同时处于 dust 时同 stack_key 不应叠成 -4。")
	_assert_eq((double_check.get("attack_roll_modifier_breakdown", []) as Array).size(), 1, "同 stack_key dust 只应保留一条 post-stack breakdown。")


func _test_dust_expires_while_battle_lifetime_terrain_stays_active() -> void:
	var setup := _build_runtime_with_units(Vector2i(5, 2), Vector2i(0, 0), Vector2i(3, 0))
	var runtime: BattleRuntimeModule = setup["runtime"]
	var state: BattleState = setup["state"]
	var attacker: BattleUnitState = setup["attacker"]
	var target: BattleUnitState = setup["target"]
	_assert_true(runtime._terrain_effect_system.upsert_timed_terrain_effect(target.coord, attacker, null, _build_dust_effect(&"meteor_swarm_dust"), &"timed_dust"), "timed dust 应能写入。")
	_assert_true(runtime._terrain_effect_system.upsert_timed_terrain_effect(target.coord, attacker, null, _build_battle_terrain_effect(&"meteor_swarm_rubble", 2), &"rubble"), "battle lifetime rubble 应能写入。")
	state.timeline.current_tu = 55
	runtime._terrain_effect_system.process_timed_terrain_effects(BattleEventBatch.new())
	var attack_check: Dictionary = _build_policy_attack_check(runtime, attacker, target)
	_assert_true(not attack_check.has("attack_roll_modifier_breakdown"), "timed dust 到期后不应继续提供命中惩罚。")
	_assert_eq(runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(target, target.coord), 2, "battle lifetime rubble 推进后仍应保留移动成本。")


func _build_runtime_with_units(map_size: Vector2i, attacker_coord: Vector2i, target_coord: Vector2i) -> Dictionary:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state := _build_state(map_size)
	var attacker := _build_unit(&"attacker", attacker_coord, &"player")
	var target := _build_unit(&"target", target_coord, &"enemy")
	state.units = {
		attacker.unit_id: attacker,
		target.unit_id: target,
	}
	state.ally_unit_ids = [attacker.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	state.active_unit_id = attacker.unit_id
	_assert_true(runtime._grid_service.place_unit(state, attacker, attacker.coord, true), "attacker 应能放入 terrain modifier fixture。")
	_assert_true(runtime._grid_service.place_unit(state, target, target.coord, true), "target 应能放入 terrain modifier fixture。")
	runtime._state = state
	return {
		"runtime": runtime,
		"state": state,
		"attacker": attacker,
		"target": target,
	}


func _build_policy_attack_check(runtime: BattleRuntimeModule, attacker: BattleUnitState, target: BattleUnitState) -> Dictionary:
	var attack_policy = runtime.get_attack_check_policy_service()
	var context = attack_policy.build_attack_context(
		runtime.get_state(),
		attacker,
		target,
		null
	)
	return attack_policy.build_attack_check(context)


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.map_size = map_size
	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell := BattleCellState.new()
			cell.coord = coord
			cell.passable = true
			state.cells[coord] = cell
	return state


func _build_unit(unit_id: StringName, coord: Vector2i, faction_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.coord = coord
	unit.is_alive = true
	unit.refresh_footprint()
	return unit


func _build_dust_effect(effect_id: StringName) -> CombatEffectDef:
	var effect := _build_timed_terrain_effect(effect_id, 0, &"timed", 50, 5)
	effect.params["accuracy_modifier_spec"] = {
		"source_domain": &"terrain",
		"label": "尘土",
		"modifier_delta": -2,
		"stack_key": &"dust_attack_roll_penalty",
		"stack_mode": &"max",
		"roll_kind_filter": &"spell_attack",
		"endpoint_mode": &"either",
		"distance_min_exclusive": 1,
		"distance_max_inclusive": -1,
		"target_team_filter": &"any",
		"footprint_mode": &"any_cell",
		"applies_to": &"attack_roll",
	}
	return effect


func _build_battle_terrain_effect(effect_id: StringName, move_cost_delta: int) -> CombatEffectDef:
	return _build_timed_terrain_effect(effect_id, move_cost_delta, &"battle", 0, 0)


func _build_timed_terrain_effect(
	effect_id: StringName,
	move_cost_delta: int,
	lifetime_policy: StringName,
	duration_tu: int,
	tick_interval_tu: int
) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"terrain_effect"
	effect.tick_effect_type = &"none"
	effect.terrain_effect_id = effect_id
	effect.duration_tu = duration_tu
	effect.tick_interval_tu = tick_interval_tu
	effect.effect_target_team_filter = &"any"
	effect.params = {
		"lifetime_policy": lifetime_policy,
		"move_cost_delta": move_cost_delta,
		"display_name": String(effect_id),
		"render_overlay_id": String(effect_id),
	}
	return effect


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
