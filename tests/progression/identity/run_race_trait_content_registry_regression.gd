extends SceneTree

const ContentValidationRunner = preload("res://tests/runtime/content_validation_runner.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const RaceTraitContentRegistry = preload("res://scripts/player/progression/race_trait_content_registry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_official_race_trait_registry_validate_without_errors()
	_test_official_identity_content_domain_validate_without_errors()

	if _failures.is_empty():
		print("Race trait content registry regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Race trait content registry regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_official_race_trait_registry_validate_without_errors() -> void:
	var registry := RaceTraitContentRegistry.new()
	_assert_empty(
		registry.validate(),
		"Official race trait content should validate without errors."
	)


func _test_official_identity_content_domain_validate_without_errors() -> void:
	var progression_registry := ProgressionContentRegistry.new()
	var validation_runner := ContentValidationRunner.new()
	var identity_result := validation_runner.validate_identity_content(
		"official_identity",
		progression_registry.get_skill_defs()
	)
	_assert_empty(
		_get_errors(identity_result),
		"Official identity content should validate without errors."
	)


func _get_errors(domain_result: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	for error_variant in domain_result.get("errors", []):
		errors.append(String(error_variant))
	return errors


func _assert_empty(errors: Array[String], message: String) -> void:
	if not errors.is_empty():
		_failures.append("%s errors=%s" % [message, str(errors)])
