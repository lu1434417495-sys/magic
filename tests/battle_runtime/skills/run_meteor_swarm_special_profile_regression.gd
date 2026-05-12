extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const BattleSpecialProfileRegistry = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_target_plan_uses_square_7x7_and_edge_clipping()
	_test_preview_and_execute_use_typed_profile_not_legacy_area()
	_test_meteor_attempt_metrics_start_after_runtime_validation()
	if _failures.is_empty():
		print("Meteor swarm special profile regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm special profile regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_target_plan_uses_square_7x7_and_edge_clipping() -> void:
	var setup := _build_runtime_fixture(Vector2i(9, 9), [])
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	var skill_defs: Dictionary = setup["skill_defs"]
	var skill_def = skill_defs.get(&"mage_meteor_swarm")
	var resolver = runtime._meteor_swarm_resolver
	var center_plan = resolver.build_target_plan(resolver.build_cast_context(caster, _build_command(caster, Vector2i(4, 4)), skill_def, null, Vector2i(4, 4), Vector2i(4, 4)))
	_assert_eq(center_plan.affected_coords.size(), 49, "开放棋盘中心陨星雨应覆盖 7x7 共 49 格。")
	_assert_eq(center_plan.get_ring_for_coord(Vector2i(1, 1)), 3, "最外层 d==3 应使用 Chebyshev ring。")
	var edge_plan = resolver.build_target_plan(resolver.build_cast_context(caster, _build_command(caster, Vector2i(0, 4)), skill_def, null, Vector2i(0, 4), Vector2i(0, 4)))
	_assert_eq(edge_plan.affected_coords.size(), 28, "贴边中心应裁剪为 4x7 共 28 格。")
	var corner_plan = resolver.build_target_plan(resolver.build_cast_context(caster, _build_command(caster, Vector2i(0, 0)), skill_def, null, Vector2i(0, 0), Vector2i(0, 0)))
	_assert_eq(corner_plan.affected_coords.size(), 16, "角落中心应裁剪为 4x4 共 16 格。")


func _test_preview_and_execute_use_typed_profile_not_legacy_area() -> void:
	var enemy_center := _build_unit(&"enemy_center", "中心敌人", &"enemy", Vector2i(4, 4), 160)
	var enemy_outer := _build_unit(&"enemy_outer", "外圈敌人", &"enemy", Vector2i(7, 7), 160)
	var ally_inner := _build_unit(&"ally_inner", "内圈友军", &"player", Vector2i(5, 4), 160)
	var setup := _build_runtime_fixture(Vector2i(9, 9), [enemy_center, enemy_outer, ally_inner])
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	var skill_defs: Dictionary = setup["skill_defs"]
	var skill_def = skill_defs.get(&"mage_meteor_swarm")
	skill_def.combat_profile.area_pattern = &"diamond"
	skill_def.combat_profile.area_value = 1
	var command := _build_command(caster, Vector2i(4, 4))
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "陨星雨 typed preview 应可用。")
	_assert_true(preview.special_profile_preview_facts != null, "preview 应暴露 special_profile_preview_facts。")
	_assert_eq(preview.target_coords.size(), 49, "poisoned legacy area_value 不应改变 typed 7x7 target plan。")
	_assert_true(preview.target_unit_ids.has(enemy_center.unit_id), "preview 应包含中心敌人。")
	_assert_true(preview.target_unit_ids.has(enemy_outer.unit_id), "preview 应包含最外层敌人。")
	_assert_true(preview.target_unit_ids.has(ally_inner.unit_id), "preview 友伤应走同一份全量 target plan。")
	_assert_true(preview.special_profile_preview_facts.get_friendly_fire_numeric_summary().size() == 1, "友军波及时应输出 numeric friendly fire summary。")
	var target_summaries := (preview.special_profile_preview_facts.to_dict().get("target_numeric_summary", []) as Array)
	var center_summary := _find_target_summary(target_summaries, enemy_center.unit_id)
	_assert_true(not center_summary.is_empty(), "中心敌人的 numeric summary 应存在。")
	var fire_component := _find_component_summary(center_summary.get("component_breakdown", []), "area_blast_fire")
	_assert_true(not fire_component.is_empty(), "area_blast_fire component summary 应存在。")
	var save_estimate := fire_component.get("save_estimate", {}) as Dictionary
	_assert_true(bool(save_estimate.get("has_save", false)), "meteor_dex_half component preview 应计算豁免概率。")
	_assert_eq(String(save_estimate.get("ability", "")), "agility", "meteor_dex_half 应使用敏捷豁免。")
	_assert_true(bool(save_estimate.get("save_partial_on_success", false)), "meteor_dex_half 成功豁免应保留半伤。")

	var batch = runtime.issue_command(command)
	_assert_true(batch != null and batch.report_entries.size() >= 1, "execute 应写入陨星雨聚合战报。")
	_assert_true(enemy_center.current_hp < 160, "中心敌人应受到 typed component 伤害。")
	_assert_true(enemy_outer.current_hp < 160, "最外层敌人也应受到灾害波及伤害。")
	_assert_true(ally_inner.current_hp < 160, "友军应走全量数值结算，不应免友伤。")
	_assert_true(ally_inner.has_status_effect(&"meteor_concussed"), "内环友军同样应按全量结算获得震眩。")
	var center_cell := runtime.get_state().cells.get(Vector2i(4, 4)) as BattleCellState
	var outer_cell := runtime.get_state().cells.get(Vector2i(7, 7)) as BattleCellState
	_assert_true(center_cell != null and center_cell.timed_terrain_effects.size() >= 3, "中心格应留下陨坑/碎石/尘土地形效果。")
	_assert_true(outer_cell != null and outer_cell.timed_terrain_effects.size() >= 1, "最外层应留下碎石地形效果。")
	var summary_entry: Dictionary = batch.report_entries[0]
	_assert_eq(String(summary_entry.get("entry_type", "")), "meteor_swarm_impact_summary", "战报应使用 meteor_swarm_impact_summary。")
	_assert_eq(String(summary_entry.get("nominal_plan_signature", "")), String(summary_entry.get("final_plan_signature", "")), "无漂移时 final plan signature 应等于 nominal。")


func _test_meteor_attempt_metrics_start_after_runtime_validation() -> void:
	var setup := _build_runtime_fixture(Vector2i(9, 9), [])
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	runtime._initialize_battle_metrics()
	var invalid_command := _build_command(caster, Vector2i(-1, -1))
	var batch := BattleEventBatch.new()
	runtime._skill_orchestrator._handle_skill_command(caster, invalid_command, batch)
	var caster_metrics := (runtime.get_battle_metrics().get("units", {}) as Dictionary).get(String(caster.unit_id), {}) as Dictionary
	var attempt_counts := caster_metrics.get("skill_attempt_counts", {}) as Dictionary
	_assert_eq(int(attempt_counts.get("mage_meteor_swarm", 0)), 0, "陨星雨运行期校验失败不应记录 skill attempt。")

	var valid_command := _build_command(caster, Vector2i(4, 4))
	runtime._skill_orchestrator._handle_skill_command(caster, valid_command, BattleEventBatch.new())
	_assert_eq(int(attempt_counts.get("mage_meteor_swarm", 0)), 1, "陨星雨通过校验并完成扣费后才记录 skill attempt。")


func _build_runtime_fixture(map_size: Vector2i, extra_units: Array) -> Dictionary:
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var special_registry := BattleSpecialProfileRegistry.new()
	special_registry.rebuild(skill_defs)
	_assert_true(special_registry.validate().is_empty(), "正式 special profile registry 应可用于 runtime fixture。")
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {}, null, null, {}, null, Callable(), special_registry.get_snapshot())
	runtime.configure_hit_resolver_for_tests(SharedHitResolvers.FixedHitResolver.new(10))
	var state := _build_state(map_size)
	var caster := _build_unit(&"meteor_caster", "陨星术者", &"player", Vector2i(4, 0), 180)
	caster.known_active_skill_ids.append(&"mage_meteor_swarm")
	caster.known_skill_level_map[&"mage_meteor_swarm"] = 9
	caster.current_ap = 4
	caster.current_mp = 200
	caster.current_aura = 3
	state.units[caster.unit_id] = caster
	state.ally_unit_ids.append(caster.unit_id)
	for unit in extra_units:
		if unit == null:
			continue
		state.units[unit.unit_id] = unit
		if unit.faction_id == caster.faction_id:
			state.ally_unit_ids.append(unit.unit_id)
		else:
			state.enemy_unit_ids.append(unit.unit_id)
	state.active_unit_id = caster.unit_id
	for unit_variant in state.units.values():
		var unit_state := unit_variant as BattleUnitState
		_assert_true(runtime._grid_service.place_unit(state, unit_state, unit_state.coord, true), "单位应能放入陨星雨测试棋盘：%s" % String(unit_state.unit_id))
	runtime._state = state
	return {
		"runtime": runtime,
		"caster": caster,
		"skill_defs": skill_defs,
	}


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"meteor_swarm_regression"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell := BattleCellState.new()
			cell.coord = coord
			cell.passable = true
			state.cells[coord] = cell
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName, coord: Vector2i, hp: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.coord = coord
	unit.is_alive = true
	unit.current_hp = hp
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, hp)
	unit.refresh_footprint()
	return unit


func _build_command(caster: BattleUnitState, anchor_coord: Vector2i) -> BattleCommand:
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = caster.unit_id
	command.skill_id = &"mage_meteor_swarm"
	command.target_coord = anchor_coord
	command.target_coords = [anchor_coord]
	return command


func _find_target_summary(summaries: Array, target_unit_id: StringName) -> Dictionary:
	for summary_variant in summaries:
		if summary_variant is Dictionary and String((summary_variant as Dictionary).get("target_unit_id", "")) == String(target_unit_id):
			return (summary_variant as Dictionary)
	return {}


func _find_component_summary(components: Variant, component_id: String) -> Dictionary:
	if components is not Array:
		return {}
	for component_variant in components:
		if component_variant is Dictionary and String((component_variant as Dictionary).get("component_id", "")) == component_id:
			return (component_variant as Dictionary)
	return {}


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
