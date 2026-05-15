extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_rule_def.gd")
const ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_condition_def.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_accepts_declared_transition_rules_for_custom_state_names()
	_test_rejects_ambiguous_rule_order_and_ids()
	_test_rejects_empty_conditions_and_unknown_predicates()
	_test_condition_trace_shape_is_stable()
	_test.finish(self, "Enemy AI transition schema regression")


func _test_accepts_declared_transition_rules_for_custom_state_names() -> void:
	var brain = _build_brain()
	var low_hp_rule = _rule(&"recover_when_low", 10, &"recover", [
		_condition(&"self_hp_at_or_below_basis_points", {"basis_points": 3000}),
	])
	var close_range_rule = _rule(&"close_range_when_near", 20, &"close_range", [
		_condition(&"nearest_enemy_distance_at_or_below", {"max_distance": 2}),
	])
	var hold_rule = _rule(&"hold_default", 30, &"hold", [
		_condition(&"always"),
	])
	brain.transition_rules = [low_hp_rule, close_range_rule, hold_rule]
	var errors: Array[String] = brain.validate_schema({})
	_test.assert_true(errors.is_empty(), "custom state transition schema 应合法: %s" % str(errors))


func _test_rejects_ambiguous_rule_order_and_ids() -> void:
	var brain = _build_brain()
	brain.transition_rules = [
		_rule(&"duplicate", 10, &"recover", [_condition(&"always")]),
		_rule(&"duplicate", 10, &"hold", [_condition(&"always")]),
	]
	var errors: Array[String] = brain.validate_schema({})
	_test.assert_true(_errors_contain(errors, "duplicate transition rule_id duplicate"), "应拒绝重复 rule_id: %s" % str(errors))
	_test.assert_true(_errors_contain(errors, "duplicate transition order 10"), "应拒绝重复 order: %s" % str(errors))


func _test_rejects_empty_conditions_and_unknown_predicates() -> void:
	var brain = _build_brain()
	brain.transition_rules = [
		_rule(&"empty_conditions", 10, &"recover", []),
		_rule(&"unknown_condition", 20, &"hold", [_condition(&"scripted_expression")]),
		_rule(&"bad_target", 30, &"missing_state", [_condition(&"always")]),
		_rule(&"bad_from", 40, &"hold", [_condition(&"always")], [&"missing_from_state"]),
	]
	var errors: Array[String] = brain.validate_schema({})
	_test.assert_true(_errors_contain(errors, "must declare at least one condition"), "应拒绝空 conditions: %s" % str(errors))
	_test.assert_true(_errors_contain(errors, "uses unsupported predicate scripted_expression"), "应拒绝未知 predicate: %s" % str(errors))
	_test.assert_true(_errors_contain(errors, "target_state_id missing_state is not declared"), "应拒绝不存在的 target state: %s" % str(errors))
	_test.assert_true(_errors_contain(errors, "from_state_id missing_from_state is not declared"), "应拒绝不存在的 from state: %s" % str(errors))


func _test_condition_trace_shape_is_stable() -> void:
	var condition = _condition(&"has_skill_affordance", {"affordances": [&"ally_heal", &"self_or_ally_buff"]})
	var trace: Dictionary = condition.to_trace_dict()
	_test.assert_eq(trace.keys(), ["predicate", "basis_points", "max_distance", "state_ids", "affordances"], "condition trace key 顺序应稳定。")
	_test.assert_eq(trace["predicate"], "has_skill_affordance", "trace 应输出 predicate。")
	_test.assert_eq(trace["basis_points"], -1, "未使用的 basis_points 应固定为 -1。")
	_test.assert_eq(trace["max_distance"], -1, "未使用的 max_distance 应固定为 -1。")
	_test.assert_eq(trace["state_ids"], [], "未使用的 state_ids 应固定为空数组。")
	_test.assert_eq(trace["affordances"], ["ally_heal", "self_or_ally_buff"], "affordance trace 应输出字符串数组。")


func _build_brain():
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"custom_transition_brain"
	brain.default_state_id = &"hold"
	brain.states = [
		_state(&"hold"),
		_state(&"recover"),
		_state(&"close_range"),
	]
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
	rule.from_state_ids = from_state_ids
	rule.target_state_id = target_state_id
	rule.conditions = conditions
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


func _errors_contain(errors: Array[String], fragment: String) -> bool:
	for error in errors:
		if String(error).contains(fragment):
			return true
	return false
