extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BodySizeRules = preload("res://scripts/systems/progression/body_size_rules.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")

const TITAN_COLOSSUS_FORM: StringName = &"titan_colossus_form"
const TITAN_GIANT_FORM_STATUS: StringName = &"titan_giant_form"
const TITAN_COLOSSUS_CHARGE_KEY: StringName = &"racial_skill_titan_colossus_form"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_titan_colossus_form_changes_and_restores_body_size()
	_test_body_size_restore_waits_when_previous_footprint_is_blocked()

	if _failures.is_empty():
		print("Titan colossus form regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Titan colossus form regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_titan_colossus_form_changes_and_restores_body_size() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 5))
	var titan := _build_unit(&"titan_user", Vector2i(1, 1))
	_assert_true(titan.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_LARGE), "测试前置：泰坦升华单位应为 large。")
	titan.known_active_skill_ids = [TITAN_COLOSSUS_FORM]
	titan.known_skill_level_map = {TITAN_COLOSSUS_FORM: 1}
	titan.per_battle_charges[TITAN_COLOSSUS_CHARGE_KEY] = 1
	_add_unit(runtime, state, titan)
	state.ally_unit_ids = [titan.unit_id]
	state.active_unit_id = titan.unit_id
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = titan.unit_id
	command.skill_id = TITAN_COLOSSUS_FORM
	command.target_unit_id = titan.unit_id

	var preview := runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "Titan Colossus Form 应允许自施放。")

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(titan.unit_id), "Titan Colossus Form 应记录施法者变更。")
	_assert_eq(titan.body_size_category, BodySizeRules.BODY_SIZE_CATEGORY_HUGE, "Titan Colossus Form 应临时改为 huge category。")
	_assert_eq(titan.body_size, BodySizeRules.BODY_SIZE_HUGE, "Titan Colossus Form 应同步 huge 的 int body_size。")
	_assert_true(titan.has_status_effect(TITAN_GIANT_FORM_STATUS), "Titan Colossus Form 应挂 battle-local status。")
	_assert_eq(int(titan.per_battle_charges.get(TITAN_COLOSSUS_CHARGE_KEY, -1)), 0, "Titan Colossus Form 应消耗身份技能次数。")

	var status_entry = titan.get_status_effect(TITAN_GIANT_FORM_STATUS)
	_assert_true(status_entry != null, "Titan giant form status 应可读取。")
	if status_entry != null:
		_assert_eq(String(status_entry.params.get("previous_body_size_category", "")), "large", "巨神化 status 应记录恢复体型。")
		_assert_eq(String(status_entry.params.get("body_size_category_override", "")), "huge", "巨神化 status 应记录覆盖体型。")

	_assert_true(runtime._advance_unit_status_durations(titan, 80), "巨神化持续时间耗尽时应产生状态变化。")
	_assert_true(not titan.has_status_effect(TITAN_GIANT_FORM_STATUS), "巨神化过期后 status 应移除。")
	_assert_eq(titan.body_size_category, BodySizeRules.BODY_SIZE_CATEGORY_LARGE, "巨神化过期后应恢复 large category。")
	_assert_eq(titan.body_size, BodySizeRules.BODY_SIZE_LARGE, "巨神化过期后应恢复 large int body_size。")


func _test_body_size_restore_waits_when_previous_footprint_is_blocked() -> void:
	var runtime := _build_runtime()
	var state := _build_state(Vector2i(5, 5))
	var shrunken := _build_unit(&"blocked_restore_user", Vector2i(1, 1))
	_assert_true(shrunken.set_body_size_category(BodySizeRules.BODY_SIZE_CATEGORY_MEDIUM), "测试前置：单位当前为 medium。")
	var blocker := _build_unit(&"blocked_restore_occupant", Vector2i(2, 1))
	_add_unit(runtime, state, shrunken)
	_add_unit(runtime, state, blocker)
	state.ally_unit_ids = [shrunken.unit_id, blocker.unit_id]
	runtime._state = state
	var status := BattleStatusEffectState.new()
	status.status_id = &"blocked_body_restore"
	status.duration = 1
	status.params = {
		"body_size_category_override": String(BodySizeRules.BODY_SIZE_CATEGORY_MEDIUM),
		"previous_body_size_category": String(BodySizeRules.BODY_SIZE_CATEGORY_LARGE),
	}
	shrunken.set_status_effect(status)

	runtime._advance_unit_status_durations(shrunken, 5)

	_assert_true(shrunken.has_status_effect(&"blocked_body_restore"), "恢复 footprint 被占用时，体型覆盖 status 应保留以便后续重试。")
	_assert_eq(shrunken.body_size_category, BodySizeRules.BODY_SIZE_CATEGORY_MEDIUM, "恢复失败时不应切换到会覆盖占位者的 large category。")
	var occupant = runtime._grid_service.get_unit_at_coord(state, blocker.coord)
	_assert_true(occupant == blocker, "恢复失败时不应覆盖目标 footprint 上的其他单位。")


func _build_runtime() -> BattleRuntimeModule:
	var registry := ProgressionContentRegistry.new()
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, registry.get_skill_defs(), {}, {})
	return runtime


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"titan_colossus_form"
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


func _build_unit(unit_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = 2
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_stamina = 30
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	return unit


func _add_unit(runtime: BattleRuntimeModule, state: BattleState, unit: BattleUnitState) -> void:
	state.units[unit.unit_id] = unit
	runtime._grid_service.place_unit(state, unit, unit.coord, true)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
