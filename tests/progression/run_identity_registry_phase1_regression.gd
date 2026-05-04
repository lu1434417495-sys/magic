extends SceneTree

const AgeContentRegistry = preload("res://scripts/player/progression/age_content_registry.gd")
const AgeProfileDef = preload("res://scripts/player/progression/age_profile_def.gd")
const AgeStageRule = preload("res://scripts/player/progression/age_stage_rule.gd")
const AscensionContentRegistry = preload("res://scripts/player/progression/ascension_content_registry.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const BloodlineContentRegistry = preload("res://scripts/player/progression/bloodline_content_registry.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const RaceContentRegistry = preload("res://scripts/player/progression/race_content_registry.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const RaceTraitContentRegistry = preload("res://scripts/player/progression/race_trait_content_registry.gd")
const RaceTraitDef = preload("res://scripts/player/progression/race_trait_def.gd")
const RacialGrantedSkill = preload("res://scripts/player/progression/racial_granted_skill.gd")
const StageAdvancementContentRegistry = preload("res://scripts/player/progression/stage_advancement_content_registry.gd")
const StageAdvancementModifier = preload("res://scripts/player/progression/stage_advancement_modifier.gd")
const SubraceContentRegistry = preload("res://scripts/player/progression/subrace_content_registry.gd")

const TEMP_ROOT := "user://identity_registry_phase1"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_cleanup_temp_root()
	_test_identity_registry_directory_constants()
	_test_official_identity_directories_scan_without_errors()
	_test_progression_registry_exposes_identity_bundle()
	_test_race_registry_reports_missing_and_duplicate_ids()
	_test_racial_granted_skill_limited_charges_must_be_positive()
	_test_age_registry_loads_profile_stage_rules()
	_test_race_trait_registry_validates_phase1_whitelists()
	_test_bloodline_and_ascension_registries_load_stage_defs()
	_test_stage_advancement_registry_validates_target_axis_and_source_shape()
	_cleanup_temp_root()

	if _failures.is_empty():
		print("Identity registry Phase 1 regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Identity registry Phase 1 regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_identity_registry_directory_constants() -> void:
	_assert_eq(RaceContentRegistry.RACE_CONFIG_DIRECTORY, "res://data/configs/races", "RaceContentRegistry 应暴露正式 races 目录常量。")
	_assert_eq(SubraceContentRegistry.SUBRACE_CONFIG_DIRECTORY, "res://data/configs/subraces", "SubraceContentRegistry 应暴露正式 subraces 目录常量。")
	_assert_eq(RaceTraitContentRegistry.RACE_TRAIT_CONFIG_DIRECTORY, "res://data/configs/race_traits", "RaceTraitContentRegistry 应暴露正式 race_traits 目录常量。")
	_assert_eq(AgeContentRegistry.AGE_PROFILE_CONFIG_DIRECTORY, "res://data/configs/age_profiles", "AgeContentRegistry 应暴露正式 age_profiles 目录常量。")
	_assert_eq(BloodlineContentRegistry.BLOODLINE_CONFIG_DIRECTORY, "res://data/configs/bloodlines", "BloodlineContentRegistry 应暴露正式 bloodlines 目录常量。")
	_assert_eq(AscensionContentRegistry.ASCENSION_CONFIG_DIRECTORY, "res://data/configs/ascensions", "AscensionContentRegistry 应暴露正式 ascensions 目录常量。")
	_assert_eq(StageAdvancementContentRegistry.STAGE_ADVANCEMENT_CONFIG_DIRECTORY, "res://data/configs/stage_advancements", "StageAdvancementContentRegistry 应暴露正式 stage_advancements 目录常量。")


func _test_official_identity_directories_scan_without_errors() -> void:
	var registries := [
		RaceContentRegistry.new(),
		SubraceContentRegistry.new(),
		RaceTraitContentRegistry.new(),
		AgeContentRegistry.new(),
		BloodlineContentRegistry.new(),
		AscensionContentRegistry.new(),
		StageAdvancementContentRegistry.new(),
	]
	for registry in registries:
		_assert_true(
			registry.validate().is_empty(),
			"%s 的正式空目录扫描不应报告错误。" % String(registry.get_script().resource_path)
		)


func _test_progression_registry_exposes_identity_bundle() -> void:
	var registry := ProgressionContentRegistry.new()
	var bundle := registry.get_bundle()
	_assert_true(registry.validate().is_empty(), "ProgressionContentRegistry 接入身份子 registry 后仍应通过正式内容校验。")
	for key in [
		"race_defs",
		"subrace_defs",
		"race_trait_defs",
		"age_profile_defs",
		"bloodline_defs",
		"bloodline_stage_defs",
		"ascension_defs",
		"ascension_stage_defs",
		"stage_advancement_defs",
	]:
		_assert_true(bundle.has(key), "ProgressionContentRegistry bundle 应暴露 %s。" % key)


func _test_race_registry_reports_missing_and_duplicate_ids() -> void:
	var directory_path := _make_temp_directory("race_registry")
	_save_resource(_make_race(&"duplicate_race"), directory_path, "duplicate_a")
	_save_resource(_make_race(&"duplicate_race"), directory_path, "duplicate_b")

	var missing_id := _make_race(&"")
	missing_id.display_name = "Missing Race"
	_save_resource(missing_id, directory_path, "missing_id")

	var registry := RaceContentRegistry.new()
	registry.load_from_directory(directory_path)
	var errors := registry.validate()
	_assert_true(_has_error_containing(errors, "is missing race_id"), "RaceContentRegistry 应报告缺失 race_id。")
	_assert_true(_has_error_containing(errors, "Duplicate race_id registered: duplicate_race"), "RaceContentRegistry 应报告重复 race_id。")


func _test_racial_granted_skill_limited_charges_must_be_positive() -> void:
	var directory_path := _make_temp_directory("racial_grant_charges")
	var race := _make_race(&"charged_race")
	var per_battle_grant := _make_racial_granted_skill(&"per_battle_skill", RacialGrantedSkill.CHARGE_KIND_PER_BATTLE, 0)
	var per_turn_grant := _make_racial_granted_skill(&"per_turn_skill", RacialGrantedSkill.CHARGE_KIND_PER_TURN, -1)
	race.racial_granted_skills = [per_battle_grant, per_turn_grant]
	_save_resource(race, directory_path, "charged_race")

	var registry := RaceContentRegistry.new()
	registry.load_from_directory(directory_path)
	var errors := registry.validate()
	_assert_true(
		_has_error_containing(errors, "racial_granted_skills[0].charges must be > 0 for charge_kind per_battle"),
		"per_battle racial_granted_skills 应要求正数 charges。"
	)
	_assert_true(
		_has_error_containing(errors, "racial_granted_skills[1].charges must be > 0 for charge_kind per_turn"),
		"per_turn racial_granted_skills 应要求正数 charges。"
	)


func _test_age_registry_loads_profile_stage_rules() -> void:
	var directory_path := _make_temp_directory("age_registry")
	var adult_rule := AgeStageRule.new()
	adult_rule.stage_id = &"adult"
	adult_rule.display_name = "Adult"
	adult_rule.description = "Fixture adult stage."

	var profile := AgeProfileDef.new()
	profile.profile_id = &"human_age_profile"
	profile.race_id = &"human"
	profile.stage_rules = [adult_rule]
	profile.creation_stage_ids = [&"adult"]
	profile.default_age_by_stage = {"adult": 18}
	_save_resource(profile, directory_path, "human_age_profile")

	var registry := AgeContentRegistry.new()
	registry.load_from_directory(directory_path)
	_assert_true(registry.validate().is_empty(), "AgeContentRegistry 应接受本表内合法的 age profile 与 stage rule 形状。")
	_assert_true(registry.get_age_profile_defs().has(&"human_age_profile"), "AgeContentRegistry 应扫描到 age profile。")


func _test_race_trait_registry_validates_phase1_whitelists() -> void:
	var directory_path := _make_temp_directory("race_trait_registry")
	var trait_def := RaceTraitDef.new()
	trait_def.trait_id = &"invalid_trait"
	trait_def.display_name = "Invalid Trait"
	trait_def.description = "Fixture invalid trait."
	trait_def.trigger_type = &"on_unknown_trigger"
	trait_def.effect_type = &"unknown_effect"
	_save_resource(trait_def, directory_path, "invalid_trait")

	var registry := RaceTraitContentRegistry.new()
	registry.load_from_directory(directory_path)
	var errors := registry.validate()
	_assert_true(_has_error_containing(errors, "unsupported trigger_type on_unknown_trigger"), "RaceTraitContentRegistry 应报告非法 trigger_type。")
	_assert_true(_has_error_containing(errors, "unsupported effect_type unknown_effect"), "RaceTraitContentRegistry 应报告非法 effect_type。")


func _test_bloodline_and_ascension_registries_load_stage_defs() -> void:
	var bloodline_directory := _make_temp_directory("bloodline_registry")
	var bloodline := BloodlineDef.new()
	bloodline.bloodline_id = &"titan"
	bloodline.display_name = "Titan"
	bloodline.description = "Fixture bloodline."
	bloodline.stage_ids = [&"titan_awakened"]
	_save_resource(bloodline, bloodline_directory, "titan")

	var bloodline_stage := BloodlineStageDef.new()
	bloodline_stage.stage_id = &"titan_awakened"
	bloodline_stage.bloodline_id = &"titan"
	bloodline_stage.display_name = "Titan Awakened"
	bloodline_stage.description = "Fixture bloodline stage."
	_save_resource(bloodline_stage, bloodline_directory, "titan_awakened")

	var bloodline_registry := BloodlineContentRegistry.new()
	bloodline_registry.load_from_directory(bloodline_directory)
	_assert_true(bloodline_registry.validate().is_empty(), "BloodlineContentRegistry 应接受 bloodline 与 stage 的本表内基础形状。")
	_assert_true(bloodline_registry.get_bloodline_defs().has(&"titan"), "BloodlineContentRegistry 应扫描 bloodline def。")
	_assert_true(bloodline_registry.get_bloodline_stage_defs().has(&"titan_awakened"), "BloodlineContentRegistry 应扫描 bloodline stage def。")

	var ascension_directory := _make_temp_directory("ascension_registry")
	var ascension := AscensionDef.new()
	ascension.ascension_id = &"dragon_ascension"
	ascension.display_name = "Dragon Ascension"
	ascension.description = "Fixture ascension."
	ascension.stage_ids = [&"dragon_awakened"]
	_save_resource(ascension, ascension_directory, "dragon_ascension")

	var ascension_stage := AscensionStageDef.new()
	ascension_stage.stage_id = &"dragon_awakened"
	ascension_stage.ascension_id = &"dragon_ascension"
	ascension_stage.display_name = "Dragon Awakened"
	ascension_stage.description = "Fixture ascension stage."
	_save_resource(ascension_stage, ascension_directory, "dragon_awakened")

	var ascension_registry := AscensionContentRegistry.new()
	ascension_registry.load_from_directory(ascension_directory)
	_assert_true(ascension_registry.validate().is_empty(), "AscensionContentRegistry 应接受 ascension 与 stage 的本表内基础形状。")
	_assert_true(ascension_registry.get_ascension_defs().has(&"dragon_ascension"), "AscensionContentRegistry 应扫描 ascension def。")
	_assert_true(ascension_registry.get_ascension_stage_defs().has(&"dragon_awakened"), "AscensionContentRegistry 应扫描 ascension stage def。")


func _test_stage_advancement_registry_validates_target_axis_and_source_shape() -> void:
	var directory_path := _make_temp_directory("stage_advancement_registry")
	var modifier := StageAdvancementModifier.new()
	modifier.modifier_id = &"invalid_stage_shift"
	modifier.display_name = "Invalid Stage Shift"
	modifier.target_axis = &"temporary_battle"
	modifier.applies_to_race_ids = [&""]
	_save_resource(modifier, directory_path, "invalid_stage_shift")

	var registry := StageAdvancementContentRegistry.new()
	registry.load_from_directory(directory_path)
	var errors := registry.validate()
	_assert_true(_has_error_containing(errors, "unsupported target_axis temporary_battle"), "StageAdvancementContentRegistry 应报告非法 target_axis。")
	_assert_true(_has_error_containing(errors, "applies_to_race_ids[0] must be a non-empty StringName"), "StageAdvancementContentRegistry 应校验长期来源数组形状。")


func _make_race(race_id: StringName) -> RaceDef:
	var race := RaceDef.new()
	race.race_id = race_id
	race.display_name = String(race_id)
	race.description = "Fixture race."
	race.age_profile_id = &"fixture_age_profile"
	race.default_subrace_id = &"fixture_subrace"
	race.subrace_ids = [&"fixture_subrace"]
	race.body_size = 2
	race.base_speed = 6
	return race


func _make_racial_granted_skill(skill_id: StringName, charge_kind: StringName, charges: int) -> RacialGrantedSkill:
	var granted_skill := RacialGrantedSkill.new()
	granted_skill.skill_id = skill_id
	granted_skill.minimum_skill_level = 1
	granted_skill.grant_level = 1
	granted_skill.charge_kind = charge_kind
	granted_skill.charges = charges
	return granted_skill


func _make_temp_directory(label: String) -> String:
	var directory_path := "%s/%s_%d" % [TEMP_ROOT, label, Time.get_ticks_usec()]
	var error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(directory_path))
	_assert_eq(error, OK, "测试目录应能创建：%s" % directory_path)
	return directory_path


func _save_resource(resource: Resource, directory_path: String, file_stem: String) -> void:
	var error := ResourceSaver.save(resource, "%s/%s.tres" % [directory_path, file_stem])
	_assert_eq(error, OK, "测试资源应能保存：%s/%s.tres" % [directory_path, file_stem])


func _cleanup_temp_root() -> void:
	var cleanup_error := _remove_path_recursive(TEMP_ROOT)
	if cleanup_error != OK:
		_failures.append("测试临时目录应能清理：%s error=%s" % [TEMP_ROOT, cleanup_error])


func _remove_path_recursive(virtual_path: String) -> int:
	var absolute_path := ProjectSettings.globalize_path(virtual_path)
	if FileAccess.file_exists(virtual_path):
		return DirAccess.remove_absolute(absolute_path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		return OK

	var directory := DirAccess.open(virtual_path)
	if directory == null:
		return DirAccess.get_open_error()
	directory.list_dir_begin()
	while true:
		var child_name := directory.get_next()
		if child_name.is_empty():
			break
		if child_name == "." or child_name == "..":
			continue
		var child_virtual_path := "%s/%s" % [virtual_path, child_name]
		var child_error := OK
		if directory.current_is_dir():
			child_error = _remove_path_recursive(child_virtual_path)
		else:
			child_error = DirAccess.remove_absolute(ProjectSettings.globalize_path(child_virtual_path))
		if child_error != OK:
			directory.list_dir_end()
			return child_error
	directory.list_dir_end()
	return DirAccess.remove_absolute(absolute_path)


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
