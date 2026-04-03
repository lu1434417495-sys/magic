class_name ProgressionTestCase
extends RefCounted

var _check_count := 0


func get_case_name() -> String:
	return "Unnamed Progression Test"


func run_case() -> Dictionary:
	_check_count = 0
	var failures: Array[String] = []
	run(failures)
	return {
		"name": get_case_name(),
		"checks": _check_count,
		"failures": failures,
	}


func run(_failures: Array[String]) -> void:
	push_error("run() must be overridden in %s." % get_case_name())


func assert_true(failures: Array[String], condition: bool, message: String) -> void:
	_check_count += 1
	if not condition:
		failures.append(message)


func assert_false(failures: Array[String], condition: bool, message: String) -> void:
	assert_true(failures, not condition, message)


func assert_equal(failures: Array[String], actual: Variant, expected: Variant, message: String) -> void:
	_check_count += 1
	if actual != expected:
		failures.append("%s | expected=%s actual=%s" % [message, expected, actual])


func assert_has(failures: Array[String], values: Array, expected_value: Variant, message: String) -> void:
	_check_count += 1
	if not values.has(expected_value):
		failures.append("%s | missing=%s" % [message, expected_value])

