extends SceneTree

const FateAttackFormula = preload("res://scripts/systems/battle/fate/fate_attack_formula.gd")


class StubRng:
	extends RefCounted

	var _rolls: Array[int] = []
	var call_count := 0


	func _init(rolls: Array[int] = []) -> void:
		_rolls = rolls.duplicate()


	func randi_range(min_value: int, max_value: int) -> int:
		if call_count >= _rolls.size():
			call_count += 1
			return min_value
		var roll := clampi(int(_rolls[call_count]), min_value, max_value)
		call_count += 1
		return roll


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_crit_gate_die_size_cases()
	_test_fumble_low_end_cases()
	_test_combat_luck_score_and_crit_threshold_cases()
	_test_roll_die_uses_injected_rng_without_disadvantage()
	_test_roll_die_uses_injected_rng_with_disadvantage()

	if _failures.is_empty():
		print("Fate attack formula regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Fate attack formula regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_crit_gate_die_size_cases() -> void:
	var cases := [
		{"label": "effective_luck >= -3 normal", "effective_luck": 0, "is_disadvantage": false, "expected": 20},
		{"label": "effective_luck >= -3 disadvantage", "effective_luck": -3, "is_disadvantage": true, "expected": 20},
		{"label": "effective_luck -4 normal", "effective_luck": -4, "is_disadvantage": false, "expected": 40},
		{"label": "effective_luck -4 disadvantage", "effective_luck": -4, "is_disadvantage": true, "expected": 40},
		{"label": "effective_luck -5 normal", "effective_luck": -5, "is_disadvantage": false, "expected": 80},
		{"label": "effective_luck -5 disadvantage mercy", "effective_luck": -5, "is_disadvantage": true, "expected": 40},
		{"label": "effective_luck -6 normal", "effective_luck": -6, "is_disadvantage": false, "expected": 160},
		{"label": "effective_luck -6 disadvantage mercy", "effective_luck": -6, "is_disadvantage": true, "expected": 80},
	]
	for case_data in cases:
		var actual := FateAttackFormula.calc_crit_gate_die_size(
			int(case_data.get("effective_luck", 0)),
			bool(case_data.get("is_disadvantage", false))
		)
		_assert_eq(actual, int(case_data.get("expected", 0)), "%s gate die mismatch" % String(case_data.get("label", "")))


func _test_fumble_low_end_cases() -> void:
	var cases := [
		{"label": "effective_luck >= -4", "effective_luck": 2, "expected": 1},
		{"label": "effective_luck -4", "effective_luck": -4, "expected": 1},
		{"label": "effective_luck -5", "effective_luck": -5, "expected": 2},
		{"label": "effective_luck -6", "effective_luck": -6, "expected": 3},
	]
	for case_data in cases:
		var actual := FateAttackFormula.calc_fumble_low_end(int(case_data.get("effective_luck", 0)))
		_assert_eq(actual, int(case_data.get("expected", 0)), "%s fumble range mismatch" % String(case_data.get("label", "")))


func _test_combat_luck_score_and_crit_threshold_cases() -> void:
	var cases := [
		{"label": "default values", "hidden_luck": 0, "faith_luck": 0, "expected_score": 0, "expected_threshold": 20},
		{"label": "odd faith rounds down", "hidden_luck": 0, "faith_luck": 1, "expected_score": 0, "expected_threshold": 20},
		{"label": "high luck soft cap", "hidden_luck": 2, "faith_luck": 5, "expected_score": 4, "expected_threshold": 16},
		{"label": "negative faith ignored for score", "hidden_luck": 2, "faith_luck": -3, "expected_score": 2, "expected_threshold": 18},
		{"label": "combat luck score cap", "hidden_luck": 4, "faith_luck": 4, "expected_score": 4, "expected_threshold": 16},
	]
	for case_data in cases:
		var hidden_luck := int(case_data.get("hidden_luck", 0))
		var faith_luck := int(case_data.get("faith_luck", 0))
		var score := FateAttackFormula.calc_combat_luck_score(hidden_luck, faith_luck)
		var threshold := FateAttackFormula.calc_crit_threshold(hidden_luck, faith_luck)
		_assert_eq(score, int(case_data.get("expected_score", 0)), "%s combat luck score mismatch" % String(case_data.get("label", "")))
		_assert_eq(threshold, int(case_data.get("expected_threshold", 0)), "%s crit threshold mismatch" % String(case_data.get("label", "")))


func _test_roll_die_uses_injected_rng_without_disadvantage() -> void:
	var rng := StubRng.new([17, 4])
	var actual := FateAttackFormula.roll_die_with_disadvantage_rule(20, false, rng)
	_assert_eq(actual, 17, "normal roll should return the first injected result")
	_assert_eq(rng.call_count, 1, "normal roll should consume exactly one injected RNG call")


func _test_roll_die_uses_injected_rng_with_disadvantage() -> void:
	var rng := StubRng.new([17, 4])
	var actual := FateAttackFormula.roll_die_with_disadvantage_rule(20, true, rng)
	_assert_eq(actual, 4, "disadvantage roll should choose the lower injected result")
	_assert_eq(rng.call_count, 2, "disadvantage roll should consume exactly two injected RNG calls")


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
