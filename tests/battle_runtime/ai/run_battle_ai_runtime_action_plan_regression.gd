extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_runtime_action_plan.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_service.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_rule_def.gd")
const ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_condition_def.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_plan_fingerprint_ignores_resources_but_tracks_skills_and_brain_shape()
	_test_service_requires_runtime_plan_by_default()
	_test_service_uses_explicit_test_fallback_only_when_enabled()
	_test_service_reports_empty_runtime_state()
	_test.finish(self, "Battle AI runtime action plan regression")


func _test_plan_fingerprint_ignores_resources_but_tracks_skills_and_brain_shape() -> void:
	var brain = _build_brain()
	var unit = _build_unit(&"actor", &"plan_brain", &"engage")
	unit.known_active_skill_ids.append(&"bolt")
	unit.known_skill_level_map = {&"bolt": 1}
	unit.current_ap = 1
	var plan = BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT.new()
	plan.set_source(unit, brain, {})
	_test.assert_false(plan.is_stale_for(unit, brain, {}), "同一单位/brain/技能签名不应 stale。")
	unit.current_ap = 0
	_test.assert_false(plan.is_stale_for(unit, brain, {}), "AP/MP 等每回合资源不应影响 plan stale。")
	unit.known_skill_level_map[&"bolt"] = 2
	_test.assert_true(plan.is_stale_for(unit, brain, {}), "技能等级变化应让 plan stale。")
	unit.known_skill_level_map[&"bolt"] = 1
	var extra_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	extra_state.state_id = &"support"
	extra_state.actions = [_wait(&"support_wait")]
	brain.states.append(extra_state)
	_test.assert_true(plan.is_stale_for(unit, brain, {}), "brain state/slot/action shape 变化应让 plan stale。")
	var transition_plan = BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT.new()
	transition_plan.set_source(unit, brain, {})
	brain.transition_rules = [_rule(&"support_when_low", 10, &"support", [_condition(&"self_hp_at_or_below_basis_points", {"basis_points": 5000})])]
	_test.assert_true(transition_plan.is_stale_for(unit, brain, {}), "brain transition rule shape 变化应让 plan stale。")


func _test_service_requires_runtime_plan_by_default() -> void:
	var fixture := _build_service_fixture(false, null)
	var decision = fixture.service.choose_command(fixture.context)
	_test.assert_true(decision != null, "缺少 runtime plan 时仍应返回 wait decision。")
	_test.assert_eq(decision.action_id, &"wait_missing_runtime_plan", "默认路径不应回退到 authored actions。")


func _test_service_uses_explicit_test_fallback_only_when_enabled() -> void:
	var fixture := _build_service_fixture(true, null)
	var decision = fixture.service.choose_command(fixture.context)
	_test.assert_true(decision != null, "显式测试 fallback 应返回 authored decision。")
	_test.assert_eq(decision.action_id, &"authored_wait", "只有 allow_authored_action_fallback_for_tests=true 才能读取 authored actions。")


func _test_service_reports_empty_runtime_state() -> void:
	var plan = BATTLE_AI_RUNTIME_ACTION_PLAN_SCRIPT.new()
	var fixture := _build_service_fixture(false, plan)
	plan.set_source(fixture.actor, fixture.brain, {})
	plan.add_state_actions(&"engage", [])
	var decision = fixture.service.choose_command(fixture.context)
	_test.assert_true(decision != null, "空 runtime state 应返回 wait decision。")
	_test.assert_eq(decision.action_id, &"wait_empty_runtime_state", "空 runtime state 应使用专门 wait reason。")


func _build_service_fixture(enable_test_fallback: bool, plan) -> Dictionary:
	var state = _build_state()
	var grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()
	var actor = _build_unit(&"actor", &"plan_brain", &"engage")
	var hero = _build_unit(&"hero", &"", &"")
	hero.control_mode = &"manual"
	actor.faction_id = &"hostile"
	hero.faction_id = &"player"
	actor.set_anchor_coord(Vector2i(1, 1))
	hero.set_anchor_coord(Vector2i(3, 1))
	_add_unit(grid_service, state, actor, true)
	_add_unit(grid_service, state, hero, false)
	state.phase = &"unit_acting"
	state.active_unit_id = actor.unit_id
	var brain = _build_brain()
	var service = BATTLE_AI_SERVICE_SCRIPT.new()
	service.enable_mutation_guard = false
	service.setup({brain.brain_id: brain})
	var context = BATTLE_AI_CONTEXT_SCRIPT.new()
	context.state = state
	context.unit_state = actor
	context.grid_service = grid_service
	context.skill_defs = {}
	context.runtime_action_plan = plan
	context.allow_authored_action_fallback_for_tests = enable_test_fallback
	return {
		"state": state,
		"grid_service": grid_service,
		"actor": actor,
		"brain": brain,
		"service": service,
		"context": context,
	}


func _build_brain():
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"plan_brain"
	brain.default_state_id = &"engage"
	var state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state.state_id = &"engage"
	state.actions = [_wait(&"authored_wait")]
	brain.states = [state]
	return brain


func _build_state():
	var state = BATTLE_STATE_SCRIPT.new()
	state.map_size = Vector2i(6, 4)
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			state.cells[cell.coord] = cell
	return state


func _build_unit(unit_id: StringName, brain_id: StringName, state_id: StringName):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.ai_brain_id = brain_id
	unit.ai_state_id = state_id
	unit.control_mode = &"ai"
	unit.current_hp = 20
	unit.current_ap = 2
	unit.current_mp = 2
	unit.current_stamina = 2
	return unit


func _add_unit(grid_service, state, unit, is_enemy: bool) -> void:
	grid_service.place_unit(state, unit, unit.coord)
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)


func _wait(action_id: StringName):
	var action = WAIT_ACTION_SCRIPT.new()
	action.action_id = action_id
	return action


func _rule(rule_id: StringName, order: int, target_state_id: StringName, conditions: Array):
	var rule = ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT.new()
	rule.rule_id = rule_id
	rule.order = order
	rule.target_state_id = target_state_id
	rule.conditions = conditions
	return rule


func _condition(predicate: StringName, args: Dictionary = {}):
	var condition = ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.new()
	condition.predicate = predicate
	condition.basis_points = int(args.get("basis_points", -1))
	condition.max_distance = int(args.get("max_distance", -1))
	return condition
