extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleAttackCheckPolicyContext = preload("res://scripts/systems/battle/core/battle_attack_check_policy_context.gd")
const BattleAttackCheckPolicyService = preload("res://scripts/systems/battle/rules/battle_attack_check_policy_service.gd")
const BattleAttackRollModifierSpec = preload("res://scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_positive_add_stack()
	_test_penalty_max_and_min_stack()
	_test_mixed_sign_stack_hard_fails_to_empty()
	_test_exact_schema_round_trip()
	if _failures.is_empty():
		print("Attack roll modifier bundle regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Attack roll modifier bundle regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_positive_add_stack() -> void:
	var service := BattleAttackCheckPolicyService.new()
	var specs: Array[BattleAttackRollModifierSpec] = [
		_build_spec(&"height", 1, &"height_bonus", &"add"),
		_build_spec(&"height", 2, &"height_bonus", &"add"),
	]
	var resolved := service._resolve_stacked_specs(specs)
	_assert_eq(resolved.size(), 1, "add stack 应合并为一条 post-stack spec。")
	_assert_eq(int(resolved[0].modifier_delta) if not resolved.is_empty() else 0, 3, "add stack 应同号求和。")


func _test_penalty_max_and_min_stack() -> void:
	var service := BattleAttackCheckPolicyService.new()
	var max_specs: Array[BattleAttackRollModifierSpec] = [
		_build_spec(&"dust_a", -1, &"dust_penalty", &"max"),
		_build_spec(&"dust_b", -2, &"dust_penalty", &"max"),
	]
	var max_resolved := service._resolve_stacked_specs(max_specs)
	_assert_eq(int(max_resolved[0].modifier_delta) if not max_resolved.is_empty() else 0, -2, "penalty max 应取绝对值最大的惩罚。")

	var min_specs: Array[BattleAttackRollModifierSpec] = [
		_build_spec(&"dust_a", -1, &"dust_penalty", &"min"),
		_build_spec(&"dust_b", -2, &"dust_penalty", &"min"),
	]
	var min_resolved := service._resolve_stacked_specs(min_specs)
	_assert_eq(int(min_resolved[0].modifier_delta) if not min_resolved.is_empty() else 0, -1, "penalty min 应取最接近 0 的惩罚。")


func _test_mixed_sign_stack_hard_fails_to_empty() -> void:
	var service := BattleAttackCheckPolicyService.new()
	var specs: Array[BattleAttackRollModifierSpec] = [
		_build_spec(&"bonus", 1, &"mixed", &"max"),
		_build_spec(&"penalty", -1, &"mixed", &"max"),
	]
	var resolved := service._resolve_stacked_specs(specs)
	_assert_eq(resolved.size(), 0, "同一 stack_key 混合 bonus/penalty 应 hard fail，不产生 post-stack breakdown。")


func _test_exact_schema_round_trip() -> void:
	var payload := _build_spec(&"dust", -2, &"dust_attack_roll_penalty", &"max").to_dict()
	payload.erase("effective_modifier_delta")
	var restored := BattleAttackRollModifierSpec.from_dict(payload) as BattleAttackRollModifierSpec
	_assert_true(restored != null, "exact schema payload 应恢复为 typed modifier spec。")
	_assert_eq(restored.modifier_delta if restored != null else 0, -2, "typed modifier spec roundtrip 应保留 modifier_delta。")
	payload["unexpected"] = true
	_assert_true(BattleAttackRollModifierSpec.from_dict(payload) == null, "exact schema 应拒绝额外字段。")


func _build_spec(source_id: StringName, delta: int, stack_key: StringName, stack_mode: StringName) -> BattleAttackRollModifierSpec:
	var spec := BattleAttackRollModifierSpec.new()
	spec.source_domain = &"terrain"
	spec.source_id = source_id
	spec.source_instance_id = String(source_id)
	spec.label = String(source_id)
	spec.modifier_delta = delta
	spec.stack_key = stack_key
	spec.stack_mode = stack_mode
	spec.roll_kind_filter = &"spell_attack"
	spec.endpoint_mode = &"either"
	spec.target_team_filter = &"any"
	spec.footprint_mode = &"any_cell"
	spec.applies_to = &"attack_roll"
	return spec


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
