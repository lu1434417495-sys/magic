extends SceneTree

const LEARNING_CASE = preload("res://tests/progression/cases/test_progression_learning_case.gd")
const PROFESSION_PROMOTION_CASE = preload("res://tests/progression/cases/test_profession_promotion_case.gd")
const CORE_BACKFILL_CASE = preload("res://tests/progression/cases/test_core_backfill_case.gd")
const SKILL_MERGE_CASE = preload("res://tests/progression/cases/test_skill_merge_case.gd")


func _initialize() -> void:
	var cases: Array = [
		LEARNING_CASE.new(),
		PROFESSION_PROMOTION_CASE.new(),
		CORE_BACKFILL_CASE.new(),
		SKILL_MERGE_CASE.new(),
	]

	var total_cases := 0
	var failed_cases := 0
	var total_checks := 0

	print("Running progression unit tests...")
	for test_case in cases:
		total_cases += 1
		var result: Dictionary = test_case.run_case()
		var case_name := str(result.get("name", "Unnamed Progression Test"))
		var failures: Array = result.get("failures", [])
		var checks := int(result.get("checks", 0))
		total_checks += checks

		if failures.is_empty():
			print("[PASS] %s (%d checks)" % [case_name, checks])
			continue

		failed_cases += 1
		print("[FAIL] %s (%d checks, %d failures)" % [case_name, checks, failures.size()])
		for failure in failures:
			print("  - %s" % failure)

	print("Progression unit tests finished: %d case(s), %d check(s), %d failure case(s)." % [
		total_cases,
		total_checks,
		failed_cases,
	])
	quit(0 if failed_cases == 0 else 1)

