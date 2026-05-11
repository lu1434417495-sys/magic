extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_formal_terrain_context_omits_manual_layout_keys()
	_test_formal_canyon_generation_builds_non_flat_board()
	if _failures.is_empty():
		print("Battle sim formal terrain regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle sim formal terrain regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_formal_terrain_context_omits_manual_layout_keys() -> void:
	var scenario = _build_canyon_scenario()
	var context: Dictionary = scenario.build_start_context()
	_assert_true(not context.has("cells"), "formal terrain 模拟场景不应再把平地 cells 直接塞进 context。")
	_assert_true(not context.has("map_size"), "formal terrain 模拟场景不应再走手工 map_size 回退路径。")
	_assert_true(not context.has("ally_spawns"), "formal terrain 模拟场景应复用正式出生点生成，而不是写死 ally_spawns。")
	_assert_true(not context.has("enemy_spawns"), "formal terrain 模拟场景应复用正式出生点生成，而不是写死 enemy_spawns。")
	_assert_true(
		context.get("battle_map_size", Vector2i.ZERO) == Vector2i(19, 11),
		"formal terrain 模拟场景应把正式 canyon 尺寸透传给 runtime。"
	)
	_assert_true(
		String(context.get("battle_terrain_profile", "")) == "canyon",
		"formal terrain 模拟场景应显式声明 canyon profile。"
	)


func _test_formal_canyon_generation_builds_non_flat_board() -> void:
	var scenario = _build_canyon_scenario()
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		game_session.get_enemy_templates(),
		game_session.get_enemy_ai_brains(),
		null
	)
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = scenario.scenario_id
	encounter_anchor.display_name = scenario.display_name
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.region_tag = &"simulation"
	var state = runtime.start_battle(encounter_anchor, 4101, scenario.build_start_context())
	_assert_true(state != null, "formal canyon 模拟回归应能正常开战。")
	if state == null:
		runtime.dispose()
		game_session.free()
		return
	_assert_true(state.map_size == Vector2i(19, 11), "formal canyon 模拟回归应生成 19x11 的正式峡谷地图。")
	_assert_true(String(state.terrain_profile_id) == "canyon", "formal canyon 模拟回归应保留 canyon terrain_profile_id。")
	_assert_true(_count_non_land_cells(state.cells) > 0, "formal canyon 模拟回归不应退化回纯 land 平地。")
	_assert_true(_has_multi_layer_column(state.cell_columns), "formal canyon 模拟回归应保留正式峡谷的真实多层列数据。")
	runtime.dispose()
	game_session.free()


func _build_canyon_scenario():
	var scenario = BATTLE_SIM_SCENARIO_DEF_SCRIPT.new()
	scenario.scenario_id = &"battle_sim_formal_canyon_regression"
	scenario.display_name = "Battle Sim Formal Canyon Regression"
	scenario.map_size = Vector2i(19, 11)
	scenario.terrain_profile_id = &"canyon"
	scenario.use_formal_terrain_generation = true
	scenario.ally_units = [_build_unit(&"ally_probe", "玩家探针", &"player", &"manual")]
	scenario.enemy_units = [_build_unit(&"enemy_probe", "敌方探针", &"hostile", &"ai")]
	return scenario


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName, control_mode: StringName):
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = unit_id
	unit_spec.display_name = display_name
	unit_spec.faction_id = faction_id
	unit_spec.control_mode = control_mode
	unit_spec.current_hp = 20
	unit_spec.current_ap = 2
	unit_spec.attribute_overrides = {
		"hp_max": 20,
		"action_points": 2,
	}
	return unit_spec


func _count_non_land_cells(cells: Dictionary) -> int:
	var count := 0
	for cell_variant in cells.values():
		if cell_variant == null:
			continue
		if String(cell_variant.base_terrain) != "land":
			count += 1
	return count


func _has_multi_layer_column(cell_columns: Dictionary) -> bool:
	for column_variant in cell_columns.values():
		if column_variant is Array and (column_variant as Array).size() > 1:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
