extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const BattleSpecialProfileRegistry = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleAiActionAssembler = preload("res://scripts/systems/battle/ai/battle_ai_action_assembler.gd")
const BattleAiContext = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BattleAiScoreService = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const UseGroundSkillAction = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const SharedHitResolvers = preload("res://tests/shared/stub_hit_resolvers.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_meteor_swarm_ai_uses_special_score_fields()
	_test_meteor_swarm_use_cases_and_high_priority_trace()
	_test_meteor_swarm_friendly_fire_soft_and_protected_paths()
	if _failures.is_empty():
		print("Meteor swarm AI regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm AI regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_meteor_swarm_ai_uses_special_score_fields() -> void:
	var enemy_center := _build_unit(&"meteor_ai_enemy_center", "中心敌人", &"enemy", Vector2i(4, 4), 120)
	var enemy_outer := _build_unit(&"meteor_ai_enemy_outer", "外圈敌人", &"enemy", Vector2i(7, 7), 160)
	var ally_inner := _build_unit(&"meteor_ai_ally_inner", "内圈友军", &"player", Vector2i(5, 4), 160)
	var setup := _build_runtime_fixture(Vector2i(9, 9), [enemy_center, enemy_outer, ally_inner])
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	var skill_defs: Dictionary = setup["skill_defs"]
	var skill_def = skill_defs.get(&"mage_meteor_swarm")

	var assembler := BattleAiActionAssembler.new()
	_assert_true(assembler._is_offensive_or_enemy_skill(skill_def), "AI action assembler 应把 effectless meteor special profile 识别为进攻技能。")

	var command := _build_command(caster, Vector2i(4, 4))
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "AI regression 前置：陨星雨 preview 应可用。")
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
	_assert_true(score_input != null, "AI 应能构造 meteor special score input。")
	if score_input == null:
		return
	_assert_eq(score_input.enemy_target_count, 2, "AI 应识别两个敌方目标。")
	_assert_eq(score_input.estimated_friendly_fire_target_count, 1, "AI 应从 numeric summary 识别一个友伤目标。")
	_assert_true(score_input.estimated_enemy_damage > 0, "AI 应估算敌方伤害。")
	_assert_true(score_input.estimated_terrain_effect_count >= 49, "AI 应估算陨星雨地形收益。")
	_assert_true(score_input.attack_roll_modifier_breakdown.size() >= 1, "AI trace 应携带尘土命中修正 breakdown。")
	_assert_true(not score_input.friendly_fire_reject_reason.is_empty(), "AI 应标记友伤 hard reject reason。")
	_assert_eq(score_input.meteor_use_case, &"unsafe_friendly_fire", "友伤 hard reject 时 meteor_use_case 应进入 unsafe。")

	var action := UseGroundSkillAction.new()
	action.maximum_friendly_fire_target_count = 99
	action.allow_friendly_lethal = true
	_assert_true(not action._passes_friendly_fire_limits(score_input), "UseGroundSkillAction 应优先遵守 meteor hard reject，而不是粗略友伤数量。")


func _test_meteor_swarm_use_cases_and_high_priority_trace() -> void:
	var cluster_a := _build_unit(&"meteor_cluster_a", "集群敌A", &"enemy", Vector2i(5, 4), 300)
	var cluster_b := _build_unit(&"meteor_cluster_b", "集群敌B", &"enemy", Vector2i(6, 4), 300)
	var cluster_c := _build_unit(&"meteor_cluster_c", "集群敌C", &"enemy", Vector2i(7, 4), 300)
	var cluster_setup := _build_runtime_fixture(Vector2i(10, 10), [cluster_a, cluster_b, cluster_c])
	var cluster_score = _build_meteor_score_input(cluster_setup, Vector2i(4, 4))
	_assert_true(cluster_score != null, "cluster 用例应能构造 score input。")
	if cluster_score != null:
		_assert_eq(cluster_score.meteor_use_case, &"cluster", "3 个有效敌方目标应进入 cluster use-case。")

	var elite_center := _build_unit(&"meteor_decap_elite", "中心精英", &"enemy", Vector2i(4, 4), 1000)
	elite_center.attribute_snapshot.set_value(&"fortune_mark_target", 1)
	var decap_setup := _build_runtime_fixture(Vector2i(9, 9), [elite_center])
	var decap_score = _build_meteor_score_input(decap_setup, Vector2i(4, 4))
	_assert_true(decap_score != null, "decapitation 用例应能构造 score input。")
	if decap_score != null:
		_assert_eq(decap_score.meteor_use_case, &"decapitation", "中心直击 high-priority target 应进入 decapitation use-case。")
		_assert_true(decap_score.high_priority_target_ids.has(elite_center.unit_id), "AI trace 应输出 high_priority_target_ids。")
		var reasons: Variant = decap_score.high_priority_reasons.get(String(elite_center.unit_id), [])
		_assert_true(reasons is Array and (reasons as Array).has("elite_or_boss"), "high priority trace 应记录 elite/boss reason。")
		var trace: Dictionary = decap_score.to_dict()
		_assert_true((trace.get("high_priority_target_ids", []) as Array).has(elite_center.unit_id), "to_dict trace 应序列化 high_priority_target_ids。")
		_assert_true(trace.has("high_priority_reasons"), "to_dict trace 应序列化 high_priority_reasons。")
		_assert_true(trace.has("low_value_penalty_reason"), "to_dict trace 应序列化 low_value_penalty_reason。")

	var zone_enemy := _build_unit(&"meteor_zone_enemy", "压制敌人", &"enemy", Vector2i(6, 4), 1000)
	var zone_setup := _build_runtime_fixture(Vector2i(9, 9), [zone_enemy])
	var zone_score = _build_meteor_score_input(zone_setup, Vector2i(4, 4))
	_assert_true(zone_score != null, "zone_denial 用例应能构造 score input。")
	if zone_score != null:
		_assert_eq(zone_score.meteor_use_case, &"zone_denial", "无 cluster/decapitation 但地形压住敌人时应进入 zone_denial。")


func _test_meteor_swarm_friendly_fire_soft_and_protected_paths() -> void:
	var enemy := _build_unit(&"meteor_soft_enemy", "软友伤敌人", &"enemy", Vector2i(4, 4), 1000)
	var sturdy_ally := _build_unit(&"meteor_soft_ally", "高血友军", &"player", Vector2i(7, 7), 3000)
	var soft_setup := _build_runtime_fixture(Vector2i(10, 10), [enemy, sturdy_ally])
	var soft_score = _build_meteor_score_input(soft_setup, Vector2i(4, 4))
	_assert_true(soft_score != null, "soft 友伤用例应能构造 score input。")
	if soft_score != null:
		_assert_eq(soft_score.estimated_friendly_fire_target_count, 1, "soft 友伤前置：应识别一个友军波及目标。")
		_assert_eq(soft_score.friendly_fire_reject_reason, "", "低比例友伤应进入 soft penalty 而非 hard reject。")
		var default_action := UseGroundSkillAction.new()
		_assert_true(default_action._passes_friendly_fire_limits(soft_score), "Meteor soft 友伤不应被默认 friendly_fire_target_count=0 的通用上限挡掉。")

	var protected_ally := _build_unit(&"meteor_protected_ally", "受保护友军", &"player", Vector2i(7, 7), 3000)
	protected_ally.ai_blackboard["protected_ally"] = true
	var protected_setup := _build_runtime_fixture(Vector2i(10, 10), [enemy, protected_ally])
	var protected_score = _build_meteor_score_input(protected_setup, Vector2i(4, 4))
	_assert_true(protected_score != null, "protected ally 用例应能构造 score input。")
	if protected_score != null:
		_assert_true(
			String(protected_score.friendly_fire_reject_reason).begins_with("meteor_swarm_protected_ally"),
			"protected ally 任意非零后果应 hard reject。 actual=%s" % protected_score.friendly_fire_reject_reason
		)
		_assert_eq(protected_score.meteor_use_case, &"unsafe_friendly_fire", "protected ally hard reject 应进入 unsafe use-case。")


func _build_runtime_fixture(map_size: Vector2i, extra_units: Array) -> Dictionary:
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var special_registry := BattleSpecialProfileRegistry.new()
	special_registry.rebuild(skill_defs)
	_assert_true(special_registry.validate().is_empty(), "正式 special profile registry 应可用于 meteor AI fixture。")
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, skill_defs, {}, {}, null, null, {}, null, Callable(), special_registry.get_snapshot())
	runtime.configure_hit_resolver_for_tests(SharedHitResolvers.FixedHitResolver.new(10))
	var state := _build_state(map_size)
	var caster := _build_unit(&"meteor_ai_caster", "陨星术者", &"player", Vector2i(4, 0), 180)
	caster.known_active_skill_ids.append(&"mage_meteor_swarm")
	caster.known_skill_level_map[&"mage_meteor_swarm"] = 9
	caster.current_ap = 4
	caster.current_mp = 200
	caster.current_aura = 3
	caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP)
	caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA)
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
		_assert_true(runtime._grid_service.place_unit(state, unit_state, unit_state.coord, true), "单位应能放入 meteor AI 棋盘：%s" % String(unit_state.unit_id))
	runtime._state = state
	return {
		"runtime": runtime,
		"caster": caster,
		"skill_defs": skill_defs,
	}


func _build_meteor_score_input(setup: Dictionary, anchor_coord: Vector2i):
	var runtime: BattleRuntimeModule = setup["runtime"]
	var caster: BattleUnitState = setup["caster"]
	var skill_defs: Dictionary = setup["skill_defs"]
	var skill_def = skill_defs.get(&"mage_meteor_swarm")
	var command := _build_command(caster, anchor_coord)
	var preview = runtime.preview_command(command)
	_assert_true(preview != null and preview.allowed, "meteor score input helper 前置：preview 应可用。")
	if preview == null or not bool(preview.allowed):
		return null
	var ai_context := BattleAiContext.new()
	ai_context.state = runtime.get_state()
	ai_context.unit_state = caster
	ai_context.grid_service = runtime.get_grid_service()
	ai_context.skill_defs = skill_defs
	var score_service := BattleAiScoreService.new()
	return score_service.build_skill_score_input(ai_context, skill_def, command, preview, [], {
		"action_kind": &"ground_skill",
		"action_label": "陨星雨",
	})


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"meteor_swarm_ai_regression"
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
	BattleRuntimeTestHelpers.seed_base_attributes_and_derive_ac(unit)
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


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
