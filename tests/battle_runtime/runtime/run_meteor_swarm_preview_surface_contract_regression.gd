extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const BattleSpecialProfileRegistry = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleAiContext = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BattleAiScoreService = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BattleHudAdapter = preload("res://scripts/systems/battle/presentation/battle_hud_adapter.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures
var _skill_defs_provider_payload: Dictionary = {}


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_meteor_preview_uses_damage_resolver_preview_contract()
	_test_preview_hud_and_ai_share_typed_facts()
	if _failures.is_empty():
		print("Meteor swarm preview surface contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm preview surface contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_preview_hud_and_ai_share_typed_facts() -> void:
	var enemy_center := _build_unit(&"meteor_surface_enemy_center", "中心敌人", &"enemy", Vector2i(4, 4), 160)
	var ally_inner := _build_unit(&"meteor_surface_ally_inner", "内圈友军", &"player", Vector2i(5, 4), 160)
	var setup := _build_runtime_fixture(Vector2i(9, 9), [enemy_center, ally_inner])
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	var skill_defs: Dictionary = setup["skill_defs"]
	var skill_def = skill_defs.get(&"mage_meteor_swarm")
	var command := _build_command(caster, Vector2i(4, 4))
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "陨星雨 preview surface 合同前置应可用。")
	_assert_true(preview.special_profile_preview_facts != null, "preview 必须暴露 special_profile_preview_facts。")
	if preview == null or preview.special_profile_preview_facts == null:
		return
	var facts_payload: Dictionary = preview.special_profile_preview_facts.to_dict()
	var preview_fact_id := String(facts_payload.get("preview_fact_id", ""))
	_assert_true(not preview_fact_id.is_empty(), "preview facts 必须带稳定 preview_fact_id。")
	_assert_eq(String(preview.hit_preview.get("source", "")), "special_profile_preview_facts", "preview.hit_preview 应标记 special facts 来源。")
	_assert_eq(String(preview.hit_preview.get("source", "")), String(preview.hit_preview.get("source", "")), "preview source 应稳定。")
	_assert_eq(preview.target_coords.size(), 49, "preview surface 必须暴露同一份 7x7 target coords。")
	_assert_true((facts_payload.get("target_numeric_summary", []) as Array).size() >= 2, "preview facts 应携带全目标数值摘要。")
	_assert_true(preview.special_profile_preview_facts.get_friendly_fire_numeric_summary().size() == 1, "preview facts 应携带全量友伤数值摘要。")

	var hud := BattleHudAdapter.new()
	_skill_defs_provider_payload = skill_defs
	hud.set_content_def_providers(Callable(self, "_get_skill_defs"), Callable(self, "_get_empty_defs"))
	var snapshot := hud.build_snapshot(
		runtime.get_state(),
		Vector2i(4, 4),
		&"mage_meteor_swarm",
		"陨星雨",
		"",
		[Vector2i(4, 4)],
		1,
		[],
		&"",
		Callable(),
		"",
		preview
	)
	var hud_hit_payload := snapshot.get("selected_skill_hit_preview_payload", {}) as Dictionary
	var hud_facts := hud_hit_payload.get("special_profile_preview_facts", {}) as Dictionary
	_assert_eq(String(hud_hit_payload.get("source", "")), "special_profile_preview_facts", "HUD hit payload 应消费 special facts。")
	_assert_eq(String(hud_facts.get("preview_fact_id", "")), preview_fact_id, "HUD 必须和 runtime preview 共用同一 preview_fact_id。")
	_assert_eq(int(hud_hit_payload.get("impact_count", 0)), 49, "HUD payload 应显示影响格数。")
	_assert_true((hud_hit_payload.get("friendly_fire_numeric_summary", []) as Array).size() == 1, "HUD payload 应携带友伤数值摘要。")

	var ai_context := BattleAiContext.new()
	ai_context.state = runtime.get_state()
	ai_context.unit_state = caster
	ai_context.grid_service = runtime.get_grid_service()
	ai_context.skill_defs = skill_defs
	var score_service := BattleAiScoreService.new()
	var score_input = score_service.build_skill_score_input(ai_context, skill_def, command, preview, [], {
		"action_kind": &"ground_skill",
		"action_label": "陨星雨",
	})
	_assert_true(score_input != null, "AI score input 应能消费 special preview facts。")
	if score_input == null:
		return
	_assert_eq(String(score_input.special_profile_preview_facts.get("preview_fact_id", "")), preview_fact_id, "AI 必须和 runtime preview 共用同一 preview_fact_id。")
	_assert_eq(score_input.target_coords.size(), 49, "AI target coords 必须来自同一份 7x7 preview plan。")
	_assert_true(score_input.enemy_target_count >= 1, "AI 应识别陨星雨敌方目标。")
	_assert_true(score_input.estimated_enemy_damage > 0, "AI 应从 typed numeric summary 估算敌方伤害。")
	_assert_true(score_input.estimated_friendly_fire_target_count == 1, "AI 应从 friendly_fire_numeric_summary 识别友伤目标。")
	_assert_true(not score_input.friendly_fire_reject_reason.is_empty(), "AI 应把 hard friendly fire 写入 reject reason。")
	_assert_true(score_input.attack_roll_modifier_breakdown.size() >= 1, "AI trace payload 应暴露未来尘土命中修正 breakdown。")


func _build_runtime_fixture(map_size: Vector2i, extra_units: Array) -> Dictionary:
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var special_registry := BattleSpecialProfileRegistry.new()
	special_registry.rebuild(skill_defs)
	_assert_true(special_registry.validate().is_empty(), "正式 special profile registry 应可用于 preview surface fixture。")
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {}, null, null, {}, null, Callable(), special_registry.get_snapshot())
	runtime.configure_hit_resolver_for_tests(SharedHitResolvers.FixedHitResolver.new(10))
	var state := _build_state(map_size)
	var caster := _build_unit(&"meteor_surface_caster", "陨星术者", &"player", Vector2i(4, 0), 180)
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
		_assert_true(runtime._grid_service.place_unit(state, unit_state, unit_state.coord, true), "单位应能放入 preview surface 棋盘：%s" % String(unit_state.unit_id))
	runtime._state = state
	return {
		"runtime": runtime,
		"caster": caster,
		"skill_defs": skill_defs,
	}


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"meteor_swarm_preview_surface_regression"
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


func _get_skill_defs() -> Dictionary:
	return _skill_defs_provider_payload


func _get_empty_defs() -> Dictionary:
	return {}


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _test_meteor_preview_uses_damage_resolver_preview_contract() -> void:
	var source := _read_text("res://scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd")
	_assert_true(source.contains("preview_damage_effect("), "Meteor 友伤数值预览必须调用 BattleDamageResolver.preview_damage_effect。")
	_assert_true(
		not source.contains("_resolve_preview_mitigation_tier")
			and not source.contains("_apply_preview_mitigation")
			and not source.contains("_estimate_guard_block"),
		"Meteor resolver 不应保留手写抗性 / 固定减伤 / guard 预览 helper，避免和 BattleDamageResolver 漂移。"
	)


func _read_text(file_path: String) -> String:
	var file := FileAccess.open(file_path, FileAccess.READ)
	if file == null:
		return ""
	return file.get_as_text()
