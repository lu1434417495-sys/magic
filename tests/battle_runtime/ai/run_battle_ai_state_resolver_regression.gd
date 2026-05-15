extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_runtime_action_plan.gd")
const BATTLE_AI_STATE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_state_resolver.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_rule_def.gd")
const ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_condition_def.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_resolves_custom_state_names_without_mutating_unit_state()
	_test_ally_low_hp_excludes_self()
	_test_nearest_enemy_distance_and_sticky_rule_are_data_driven()
	_test_skill_affordance_uses_plan_cache_and_context_lazy_cache()
	_test.finish(self, "Battle AI state resolver regression")


func _test_resolves_custom_state_names_without_mutating_unit_state() -> void:
	var fixture := _build_fixture()
	var brain = _build_brain(&"hold", [
		_rule(&"recover_low_hp", 10, &"recover", [_condition(&"self_hp_at_or_below_basis_points", {"basis_points": 3000})]),
		_rule(&"hold_default", 20, &"hold", [_condition(&"always")]),
	])
	fixture.actor.ai_state_id = &"hold"
	fixture.actor.current_hp = 5
	fixture.actor.attribute_snapshot.set_value(&"hp_max", 20)
	var result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(result["previous_state_id"], &"hold", "resolver 应记录原 state。")
	_test.assert_eq(result["state_id"], &"recover", "低血量应按 rule 切到自定义 recover。")
	_test.assert_eq(result["rule_id"], &"recover_low_hp", "trace 应记录命中的 rule。")
	_test.assert_eq(fixture.actor.ai_state_id, &"hold", "resolver 本身不得写 unit_state.ai_state_id。")


func _test_ally_low_hp_excludes_self() -> void:
	var fixture := _build_fixture()
	var brain = _build_brain(&"hold", [
		_rule(&"aid_low_ally", 10, &"aid_ally", [_condition(&"ally_hp_at_or_below_basis_points", {"basis_points": 5000})]),
		_rule(&"hold_default", 20, &"hold", [_condition(&"always")]),
	])
	fixture.actor.current_hp = 1
	fixture.actor.attribute_snapshot.set_value(&"hp_max", 20)
	var self_only_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(self_only_result["state_id"], &"hold", "ally_hp_at_or_below 不应把自己算成低血友军。")
	fixture.ally.current_hp = 5
	fixture.ally.attribute_snapshot.set_value(&"hp_max", 20)
	var ally_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(ally_result["state_id"], &"aid_ally", "其他同阵营单位低血时才应进入 aid_ally。")


func _test_nearest_enemy_distance_and_sticky_rule_are_data_driven() -> void:
	var fixture := _build_fixture()
	var brain = _build_brain(&"hold", [
		_rule(&"stay_close_range", 5, &"close_range", [
			_condition(&"current_state_is", {"state_ids": [&"close_range"]}),
			_condition(&"nearest_enemy_distance_at_or_below", {"max_distance": 3}),
		], [&"close_range"]),
		_rule(&"enter_close_range", 10, &"close_range", [_condition(&"nearest_enemy_distance_at_or_below", {"max_distance": 2})]),
		_rule(&"hold_default", 20, &"hold", [_condition(&"always")]),
	])
	fixture.actor.ai_state_id = &"hold"
	fixture.enemy.set_anchor_coord(Vector2i(3, 1))
	fixture.grid_service.place_unit(fixture.state, fixture.enemy, fixture.enemy.coord, true)
	var enter_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(enter_result["state_id"], &"close_range", "距离 2 时应进入 close_range。")
	fixture.actor.ai_state_id = &"close_range"
	fixture.enemy.set_anchor_coord(Vector2i(4, 1))
	fixture.grid_service.place_unit(fixture.state, fixture.enemy, fixture.enemy.coord, true)
	var sticky_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(sticky_result["rule_id"], &"stay_close_range", "sticky 行为应由 current_state_is + 距离 rule 表达。")
	_test.assert_eq(sticky_result["state_id"], &"close_range", "距离 3 时应保持 close_range。")


func _test_skill_affordance_uses_plan_cache_and_context_lazy_cache() -> void:
	var fixture := _build_fixture()
	var support_skill = _build_support_skill(&"aid_spell")
	fixture.context.skill_defs = {support_skill.skill_id: support_skill}
	var known_skill_ids: Array[StringName] = [support_skill.skill_id]
	fixture.actor.known_active_skill_ids = known_skill_ids
	fixture.actor.known_skill_level_map[support_skill.skill_id] = 1
	var brain = _build_brain(&"hold", [
		_rule(&"aid_skill_available", 10, &"aid_ally", [_condition(&"has_skill_affordance", {"affordances": [&"ally_heal"]})]),
		_rule(&"hold_default", 20, &"hold", [_condition(&"always")]),
	])

	var plan = BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT.new()
	plan.set_source(fixture.actor, brain, fixture.context.skill_defs)
	plan.set_skill_affordance_record(support_skill.skill_id, {
		"skill_id": support_skill.skill_id,
		"affordances": [&"ally_heal"],
		"action_families": [&"use_unit_skill"],
	})
	fixture.context.runtime_action_plan = plan
	var plan_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(plan_result["state_id"], &"aid_ally", "production path 应优先读取 runtime plan 的 affordance cache。")

	fixture.context.runtime_action_plan = null
	fixture.actor.ai_state_id = &"hold"
	var lazy_result: Dictionary = fixture.resolver.resolve(fixture.context, brain)
	_test.assert_eq(lazy_result["state_id"], &"aid_ally", "无 plan 的测试路径应按 skill_defs lazy classify。")


func _build_fixture() -> Dictionary:
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"resolver_regression"
	state.map_size = Vector2i(6, 4)
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			state.cells[cell.coord] = cell
	var grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()
	var actor = _unit(&"actor", &"hostile", Vector2i(1, 1))
	var ally = _unit(&"ally", &"hostile", Vector2i(1, 2))
	var enemy = _unit(&"enemy", &"player", Vector2i(5, 1))
	_add_unit(grid_service, state, actor, true)
	_add_unit(grid_service, state, ally, true)
	_add_unit(grid_service, state, enemy, false)
	var context = BATTLE_AI_CONTEXT_SCRIPT.new()
	context.state = state
	context.unit_state = actor
	context.grid_service = grid_service
	context.skill_defs = {}
	var resolver = BATTLE_AI_STATE_RESOLVER_SCRIPT.new()
	return {
		"state": state,
		"grid_service": grid_service,
		"actor": actor,
		"ally": ally,
		"enemy": enemy,
		"context": context,
		"resolver": resolver,
	}


func _build_brain(default_state_id: StringName, rules: Array):
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"resolver_brain"
	brain.default_state_id = default_state_id
	brain.states = [_state(&"hold"), _state(&"recover"), _state(&"aid_ally"), _state(&"close_range")]
	brain.transition_rules = rules
	return brain


func _state(state_id: StringName):
	var state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state.state_id = state_id
	state.actions = [_wait(StringName("%s_wait" % String(state_id)))]
	return state


func _wait(action_id: StringName):
	var action = WAIT_ACTION_SCRIPT.new()
	action.action_id = action_id
	return action


func _rule(
	rule_id: StringName,
	order: int,
	target_state_id: StringName,
	conditions: Array,
	from_state_ids: Array[StringName] = []
):
	var rule = ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT.new()
	rule.rule_id = rule_id
	rule.order = order
	rule.target_state_id = target_state_id
	rule.conditions = conditions
	rule.from_state_ids = from_state_ids
	return rule


func _condition(predicate: StringName, args: Dictionary = {}):
	var condition = ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.new()
	condition.predicate = predicate
	condition.basis_points = int(args.get("basis_points", -1))
	condition.max_distance = int(args.get("max_distance", -1))
	var state_ids: Array[StringName] = []
	for state_id in args.get("state_ids", []):
		state_ids.append(ProgressionDataUtils.to_string_name(state_id))
	var affordances: Array[StringName] = []
	for affordance in args.get("affordances", []):
		affordances.append(ProgressionDataUtils.to_string_name(affordance))
	condition.state_ids = state_ids
	condition.affordances = affordances
	return condition


func _unit(unit_id: StringName, faction_id: StringName, coord: Vector2i):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.control_mode = &"ai"
	unit.current_hp = 20
	unit.current_ap = 2
	unit.current_mp = 2
	unit.current_stamina = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 20)
	return unit


func _add_unit(grid_service, state, unit, is_enemy: bool) -> void:
	grid_service.place_unit(state, unit, unit.coord, true)
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)


func _build_support_skill(skill_id: StringName):
	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = "测试支援"
	skill_def.skill_type = &"active"
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_id
	skill_def.combat_profile.target_mode = &"unit"
	skill_def.combat_profile.target_team_filter = &"ally"
	skill_def.combat_profile.target_selection_mode = &"single_unit"
	var heal_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	heal_effect.effect_type = &"heal"
	heal_effect.effect_target_team_filter = &"ally"
	heal_effect.power = 8
	var effect_defs: Array[CombatEffectDef] = [heal_effect]
	skill_def.combat_profile.effect_defs = effect_defs
	return skill_def
