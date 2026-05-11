extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AgeContentRegistry = preload("res://scripts/player/progression/age_content_registry.gd")
const AscensionContentRegistry = preload("res://scripts/player/progression/ascension_content_registry.gd")
const BloodlineContentRegistry = preload("res://scripts/player/progression/bloodline_content_registry.gd")
const RaceContentRegistry = preload("res://scripts/player/progression/race_content_registry.gd")
const RaceTraitContentRegistry = preload("res://scripts/player/progression/race_trait_content_registry.gd")
const StageAdvancementContentRegistry = preload("res://scripts/player/progression/stage_advancement_content_registry.gd")
const SubraceContentRegistry = preload("res://scripts/player/progression/subrace_content_registry.gd")

const MISSING_RACE_FIXTURE_PATH := "user://identity_registry_missing_race.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_cleanup_temp_fixture()
	_test_registry_directory_constants()
	_test_race_registry_phase_one_is_table_local()
	_test_race_trait_registry_phase_one_enums_and_shape()
	_test_age_registry_phase_one_nested_stage_shape()
	_test_bloodline_and_ascension_registries_stage_tables()
	_test_stage_advancement_registry_phase_one_shape()
	_cleanup_temp_fixture()

	if _failures.is_empty():
		print("Identity sub registry schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Identity sub registry schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_registry_directory_constants() -> void:
	_assert_eq(RaceContentRegistry.RACE_CONFIG_DIRECTORY, "res://data/configs/races", "RaceContentRegistry 应暴露正式目录常量。")
	_assert_eq(SubraceContentRegistry.SUBRACE_CONFIG_DIRECTORY, "res://data/configs/subraces", "SubraceContentRegistry 应暴露正式目录常量。")
	_assert_eq(RaceTraitContentRegistry.RACE_TRAIT_CONFIG_DIRECTORY, "res://data/configs/race_traits", "RaceTraitContentRegistry 应暴露正式目录常量。")
	_assert_eq(AgeContentRegistry.AGE_PROFILE_CONFIG_DIRECTORY, "res://data/configs/age_profiles", "AgeContentRegistry 应暴露正式目录常量。")
	_assert_eq(BloodlineContentRegistry.BLOODLINE_CONFIG_DIRECTORY, "res://data/configs/bloodlines", "BloodlineContentRegistry 应暴露正式目录常量。")
	_assert_eq(AscensionContentRegistry.ASCENSION_CONFIG_DIRECTORY, "res://data/configs/ascensions", "AscensionContentRegistry 应暴露正式目录常量。")
	_assert_eq(StageAdvancementContentRegistry.STAGE_ADVANCEMENT_CONFIG_DIRECTORY, "res://data/configs/stage_advancements", "StageAdvancementContentRegistry 应暴露正式目录常量。")


func _test_race_registry_phase_one_is_table_local() -> void:
	var registry := RaceContentRegistry.new()
	registry._race_defs.clear()
	registry._validation_errors.clear()

	var race_def := RaceDef.new()
	race_def.race_id = &"table_local_race"
	race_def.age_profile_id = &"missing_age_profile"
	race_def.default_subrace_id = &"missing_subrace"
	race_def.subrace_ids = [&"missing_subrace"]
	race_def.trait_ids = [&"missing_trait"]
	registry._race_defs[race_def.race_id] = race_def

	var errors := registry._collect_validation_errors()
	_assert_true(errors.is_empty(), "RaceContentRegistry Phase 1 不应查询 age/subrace/trait sibling registry。")

	var missing_id := RaceDef.new()
	var missing_id_path := MISSING_RACE_FIXTURE_PATH
	var save_result := ResourceSaver.save(missing_id, missing_id_path)
	_assert_eq(save_result, OK, "测试应能保存临时 RaceDef fixture。")
	if save_result == OK:
		registry._register_resource(missing_id_path)
	_assert_true(
		_has_error_containing(registry.validate(), "is missing race_id"),
		"RaceContentRegistry 应报告缺失 race_id。"
	)


func _test_race_trait_registry_phase_one_enums_and_shape() -> void:
	var registry := RaceTraitContentRegistry.new()
	registry._race_trait_defs.clear()
	registry._validation_errors.clear()

	var trait_def := RaceTraitDef.new()
	trait_def.trait_id = &"invalid_trait_shape"
	trait_def.trigger_type = &"not_a_trigger"
	trait_def.effect_type = &"not_an_effect"
	trait_def.params = {
		123: "invalid_key",
		"": "empty_key",
	}
	registry._race_trait_defs[trait_def.trait_id] = trait_def
	var errors := registry._collect_validation_errors()

	_assert_true(_has_error_containing(errors, "unsupported trigger_type not_a_trigger"), "RaceTraitContentRegistry 应用本表白名单校验 trigger_type。")
	_assert_true(_has_error_containing(errors, "unsupported effect_type not_an_effect"), "RaceTraitContentRegistry 应用本表/资源白名单校验 effect_type。")
	_assert_true(_has_error_containing(errors, "params key 123 must be a String or StringName"), "RaceTraitContentRegistry 应校验 params key 形状。")
	_assert_true(_has_error_containing(errors, "params has an empty key"), "RaceTraitContentRegistry 应拒绝空 params key。")


func _test_age_registry_phase_one_nested_stage_shape() -> void:
	var registry := AgeContentRegistry.new()
	registry._age_profile_defs.clear()
	registry._validation_errors.clear()

	var age_profile := AgeProfileDef.new()
	age_profile.profile_id = &"human_age"
	var stage_a := AgeStageRule.new()
	stage_a.stage_id = &"adult"
	var stage_b := AgeStageRule.new()
	stage_b.stage_id = &"adult"
	age_profile.stage_rules = [stage_a, stage_b]
	registry._age_profile_defs[age_profile.profile_id] = age_profile

	var errors := registry._collect_validation_errors()
	_assert_true(_has_error_containing(errors, "duplicate stage_id adult"), "AgeContentRegistry 应校验本资源内重复 stage_id。")


func _test_bloodline_and_ascension_registries_stage_tables() -> void:
	var bloodline_registry := BloodlineContentRegistry.new()
	bloodline_registry._bloodline_defs.clear()
	bloodline_registry._bloodline_stage_defs.clear()
	bloodline_registry._validation_errors.clear()

	var bloodline_def := BloodlineDef.new()
	bloodline_def.bloodline_id = &"dragon_blood"
	bloodline_def.stage_ids = [&"missing_stage"]
	bloodline_registry._bloodline_defs[bloodline_def.bloodline_id] = bloodline_def
	var bloodline_stage := BloodlineStageDef.new()
	bloodline_stage.stage_id = &"dragon_awakened"
	bloodline_stage.bloodline_id = &"missing_bloodline"
	bloodline_registry._bloodline_stage_defs[bloodline_stage.stage_id] = bloodline_stage
	_assert_true(bloodline_registry._collect_validation_errors().is_empty(), "BloodlineContentRegistry Phase 1 不应校验 bloodline/stage 互引。")

	var ascension_registry := AscensionContentRegistry.new()
	ascension_registry._ascension_defs.clear()
	ascension_registry._ascension_stage_defs.clear()
	ascension_registry._validation_errors.clear()
	var ascension_def := AscensionDef.new()
	ascension_def.ascension_id = &"divine_mark"
	ascension_def.stage_ids = [&"missing_stage"]
	ascension_def.allowed_race_ids = [&"missing_race"]
	ascension_registry._ascension_defs[ascension_def.ascension_id] = ascension_def
	var ascension_stage := AscensionStageDef.new()
	ascension_stage.stage_id = &"divine_awakened"
	ascension_stage.ascension_id = &"missing_ascension"
	ascension_registry._ascension_stage_defs[ascension_stage.stage_id] = ascension_stage
	_assert_true(ascension_registry._collect_validation_errors().is_empty(), "AscensionContentRegistry Phase 1 不应校验 ascension/stage 或 allowed_* 引用。")


func _test_stage_advancement_registry_phase_one_shape() -> void:
	var registry := StageAdvancementContentRegistry.new()
	registry._stage_advancement_defs.clear()
	registry._validation_errors.clear()

	var modifier := StageAdvancementModifier.new()
	modifier.modifier_id = &"invalid_axis"
	modifier.target_axis = &"temporary_status_axis"
	modifier.applies_to_race_ids = [&"missing_race"]
	registry._stage_advancement_defs[modifier.modifier_id] = modifier

	var errors := registry._collect_validation_errors()
	_assert_true(_has_error_containing(errors, "unsupported target_axis temporary_status_axis"), "StageAdvancementContentRegistry 应校验 VALID_TARGET_AXES。")
	_assert_true(not _has_error_containing(errors, "missing_race"), "StageAdvancementContentRegistry Phase 1 不应校验长期来源引用是否存在。")


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return true
	return false


func _cleanup_temp_fixture() -> void:
	if not FileAccess.file_exists(MISSING_RACE_FIXTURE_PATH):
		return
	var cleanup_error := DirAccess.remove_absolute(ProjectSettings.globalize_path(MISSING_RACE_FIXTURE_PATH))
	if cleanup_error != OK:
		_test.fail("测试临时资源应能清理：%s error=%s" % [MISSING_RACE_FIXTURE_PATH, cleanup_error])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
