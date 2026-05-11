extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_turn_control_contract_surface()
	_test_petrified_self_save_failure_skips_turn()
	_test_petrified_self_save_success_removes_status_and_allows_action()
	_test_madness_self_save_failure_returns_ai_override_policy()
	_test_madness_self_save_success_removes_status_and_allows_action()
	_test.finish(self, "Control status contract regression")


func _test_turn_control_contract_surface() -> void:
	var resolver = _build_turn_resolver().get("resolver")
	if resolver == null:
		return
	_assert_has_method(resolver, "resolve_turn_control_status", "BattleRuntimeSkillTurnResolver must own resolve_turn_control_status(unit_state, batch).")


func _test_petrified_self_save_failure_skips_turn() -> void:
	var fixture := _build_turn_resolver()
	var resolver = fixture.get("resolver")
	var target: BattleUnitState = fixture.get("target")
	if resolver == null or not resolver.has_method("resolve_turn_control_status"):
		return
	_add_control_status(target, &"petrified", {
		"self_save_ability": "constitution",
		"self_save_dc": 15,
		"self_save_roll_override": 1,
		"self_save_tag": "constitution",
	})
	var result: Dictionary = resolver.resolve_turn_control_status(target, BattleEventBatch.new())
	_assert_true(bool(result.get("skip_turn", false)), "Petrified self-save failure must skip the current turn.")
	_assert_true(not bool(result.get("ai_controlled", false)), "Petrified self-save failure must not request AI control.")
	_assert_eq(int(target.current_ap), 0, "Petrified self-save failure must clear AP.")
	_assert_eq(int(target.current_move_points), 0, "Petrified self-save failure must clear movement.")
	_assert_true(target.has_status_effect(&"petrified"), "Petrified self-save failure must keep the status.")


func _test_petrified_self_save_success_removes_status_and_allows_action() -> void:
	var fixture := _build_turn_resolver()
	var resolver = fixture.get("resolver")
	var target: BattleUnitState = fixture.get("target")
	if resolver == null or not resolver.has_method("resolve_turn_control_status"):
		return
	_add_control_status(target, &"petrified", {
		"self_save_ability": "constitution",
		"self_save_dc": 15,
		"self_save_roll_override": 20,
		"self_save_tag": "constitution",
	})
	var result: Dictionary = resolver.resolve_turn_control_status(target, BattleEventBatch.new())
	_assert_true(not bool(result.get("skip_turn", true)), "Petrified self-save success must allow the unit to act immediately.")
	_assert_true(bool(result.get("status_removed", false)), "Petrified self-save success must report status_removed.")
	_assert_true(not target.has_status_effect(&"petrified"), "Petrified self-save success must remove the status.")


func _test_madness_self_save_failure_returns_ai_override_policy() -> void:
	var fixture := _build_turn_resolver()
	var resolver = fixture.get("resolver")
	var target: BattleUnitState = fixture.get("target")
	if resolver == null or not resolver.has_method("resolve_turn_control_status"):
		return
	_add_control_status(target, &"madness", {
		"self_save_ability": "willpower",
		"self_save_dc": 15,
		"self_save_roll_override": 1,
		"self_save_tag": "willpower",
	})
	var result: Dictionary = resolver.resolve_turn_control_status(target, BattleEventBatch.new())
	_assert_true(not bool(result.get("skip_turn", true)), "Madness self-save failure must not skip the turn.")
	_assert_true(bool(result.get("ai_controlled", false)), "Madness self-save failure must request AI control.")
	_assert_eq(String(result.get("ai_target_policy", "")), "any_unit", "Madness AI override must be allowed to target any unit.")
	_assert_true(bool(result.get("cleanup_on_turn_end", false)), "Madness AI override must request turn-end cleanup.")
	_assert_true(target.has_status_effect(&"madness"), "Madness self-save failure must keep the status.")


func _test_madness_self_save_success_removes_status_and_allows_action() -> void:
	var fixture := _build_turn_resolver()
	var resolver = fixture.get("resolver")
	var target: BattleUnitState = fixture.get("target")
	if resolver == null or not resolver.has_method("resolve_turn_control_status"):
		return
	_add_control_status(target, &"madness", {
		"self_save_ability": "willpower",
		"self_save_dc": 15,
		"self_save_roll_override": 20,
		"self_save_tag": "willpower",
	})
	var result: Dictionary = resolver.resolve_turn_control_status(target, BattleEventBatch.new())
	_assert_true(not bool(result.get("skip_turn", true)), "Madness self-save success must allow the unit to act immediately.")
	_assert_true(not bool(result.get("ai_controlled", true)), "Madness self-save success must not request AI control.")
	_assert_true(bool(result.get("status_removed", false)), "Madness self-save success must report status_removed.")
	_assert_true(not target.has_status_effect(&"madness"), "Madness self-save success must remove the status.")


func _build_turn_resolver() -> Dictionary:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state := _build_state(Vector2i(4, 4))
	runtime._state = state
	var target := _build_unit(&"target", "Target", &"enemy", Vector2i(1, 1))
	state.units[target.unit_id] = target
	state.enemy_unit_ids.append(target.unit_id)
	runtime._grid_service.place_unit(state, target, target.coord, true)
	var resolver = runtime._skill_turn_resolver
	if resolver == null:
		_failures.append("BattleRuntimeModule must expose a skill turn resolver for control status resolution.")
		return {"runtime": runtime, "target": target}
	return {
		"runtime": runtime,
		"resolver": resolver,
		"target": target,
	}


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"control_status_contract"
	state.phase = &"unit_acting"
	state.map_size = map_size
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell := BattleCellState.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BattleCellState.TERRAIN_LAND
			cell.base_height = 4
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 100
	unit.current_mp = 20
	unit.current_stamina = 20
	unit.current_ap = 2
	unit.current_move_points = 4
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 100)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, 10)
	unit.attribute_snapshot.set_value(&"constitution_modifier", 0)
	unit.attribute_snapshot.set_value(&"willpower_modifier", 0)
	return unit


func _add_control_status(unit_state: BattleUnitState, status_id: StringName, params: Dictionary) -> void:
	var status := BattleStatusEffectState.new()
	status.status_id = status_id
	status.source_unit_id = &"source"
	status.power = 1
	status.stacks = 1
	status.duration = -1
	status.params = params.duplicate(true)
	unit_state.set_status_effect(status)


func _assert_has_method(object, method_name: String, message: String) -> bool:
	if object == null or not object.has_method(method_name):
		_failures.append(message)
		return false
	return true


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	_test.assert_eq(actual, expected, message)
