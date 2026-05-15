extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const AgeProfileDef = preload("res://scripts/player/progression/age_profile_def.gd")
const AgeStageRule = preload("res://scripts/player/progression/age_stage_rule.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const RaceTraitDef = preload("res://scripts/player/progression/race_trait_def.gd")
const RacialGrantedSkill = preload("res://scripts/player/progression/racial_granted_skill.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const StageAdvancementModifier = preload("res://scripts/player/progression/stage_advancement_modifier.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_learn_sources_include_identity_grants()
	_test_progression_registry_bundle_exposes_identity_phase2_keys()
	_test_valid_identity_graph_passes_phase2()
	_test_invalid_identity_graph_reports_phase2_errors()
	_test_racial_granted_skill_minimum_level_cannot_exceed_skill_max_level()

	if _failures.is_empty():
		print("Identity registry Phase 2 regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Identity registry Phase 2 regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_learn_sources_include_identity_grants() -> void:
	for learn_source in [
		&"player",
		&"profession",
		&"race",
		&"subrace",
		&"ascension",
		&"bloodline",
	]:
		_assert_true(
			ProgressionContentRegistry.VALID_LEARN_SOURCES.has(learn_source),
			"VALID_LEARN_SOURCES 应包含 %s。" % String(learn_source)
		)


func _test_progression_registry_bundle_exposes_identity_phase2_keys() -> void:
	var registry := ProgressionContentRegistry.new()
	var bundle := registry.get_bundle()
	for key in [
		"race",
		"subrace",
		"race_trait",
		"age_profile",
		"bloodline",
		"bloodline_stage",
		"ascension",
		"ascension_stage",
		"stage_advancement",
	]:
		_assert_true(bundle.has(key), "ProgressionContentRegistry.get_bundle() 应暴露 %s 字典。" % key)
		_assert_true(bundle.get(key) is Dictionary, "ProgressionContentRegistry.get_bundle().%s 应为 Dictionary。" % key)


func _test_valid_identity_graph_passes_phase2() -> void:
	var registry := _make_empty_progression_registry()
	registry._skill_defs = {
		&"player_skill": _make_skill(&"player_skill", &"player"),
		&"race_skill": _make_skill(&"race_skill", &"race"),
		&"subrace_skill": _make_skill(&"subrace_skill", &"subrace"),
		&"bloodline_skill": _make_skill(&"bloodline_skill", &"bloodline"),
		&"ascension_skill": _make_skill(&"ascension_skill", &"ascension"),
	}
	registry._race_trait_defs = {
		&"keen_senses": _make_trait(&"keen_senses"),
	}
	registry._age_profile_defs = {
		&"human_age": _make_age_profile(&"human_age", &"human", [&"adult"]),
	}
	var valid_race := _make_race(&"human", &"human_age", &"high_human", [&"high_human"], [&"keen_senses"], &"race_skill")
	valid_race.body_size_category = &"gargantuan"
	registry._race_defs = {
		&"human": valid_race,
	}
	var valid_subrace := _make_subrace(&"high_human", &"human", [&"keen_senses"], &"subrace_skill")
	valid_subrace.body_size_category_override = &"boss"
	registry._subrace_defs = {
		&"high_human": valid_subrace,
	}
	registry._bloodline_defs = {
		&"titan": _make_bloodline(&"titan", [&"titan_awakened"], [&"keen_senses"], &"bloodline_skill"),
	}
	registry._bloodline_stage_defs = {
		&"titan_awakened": _make_bloodline_stage(&"titan_awakened", &"titan", [&"keen_senses"], &"bloodline_skill"),
	}
	registry._ascension_defs = {
		&"dragon_ascension": _make_ascension(&"dragon_ascension", [&"dragon_awakened"], [&"titan"], [&"keen_senses"], &"ascension_skill"),
	}
	var valid_ascension_stage := _make_ascension_stage(&"dragon_awakened", &"dragon_ascension", [&"keen_senses"], &"ascension_skill")
	valid_ascension_stage.body_size_category_override = &"tiny"
	registry._ascension_stage_defs = {
		&"dragon_awakened": valid_ascension_stage,
	}
	registry._stage_advancement_defs = {
		&"titan_shift": _make_stage_advancement(&"titan_shift", StageAdvancementModifier.TARGET_AXIS_BLOODLINE, &"titan_awakened"),
	}

	_assert_current_official_progression_validation_errors(
		registry.validate(),
		"完整合法 identity 图不应额外带有正式内容校验错误。"
	)


func _test_invalid_identity_graph_reports_phase2_errors() -> void:
	var registry := _make_empty_progression_registry()
	registry._skill_defs = {
		&"race_skill": _make_skill(&"race_skill", &"profession"),
		&"subrace_skill": _make_skill(&"subrace_skill", &"subrace"),
		&"bloodline_skill": _make_skill(&"bloodline_skill", &"bloodline"),
		&"ascension_skill": _make_skill(&"ascension_skill", &"ascension"),
	}
	registry._race_trait_defs = {
		&"known_trait": _make_trait(&"known_trait"),
		&"undispatched_trait": _make_trait(&"undispatched_trait"),
	}
	registry._race_trait_defs[&"undispatched_trait"].trigger_type = &"on_crit"

	var invalid_race := _make_race(&"broken_race", &"missing_age", &"broken_subrace", [&"broken_subrace"], [&"missing_trait"], &"race_skill")
	invalid_race.body_size_category = &"colossal"
	invalid_race.damage_resistances = {
		&"cold": &"half",
		&"fire": &"resist",
		123: &"half",
		&"poison": true,
	}
	registry._race_defs = {
		invalid_race.race_id: invalid_race,
	}

	var invalid_subrace := _make_subrace(&"broken_subrace", &"missing_parent", [&"missing_trait"], &"subrace_skill")
	invalid_subrace.body_size_category_override = &"colossal"
	registry._subrace_defs = {
		invalid_subrace.subrace_id: invalid_subrace,
	}

	var invalid_age := _make_age_profile(&"broken_age", &"missing_race", [])
	var invalid_creation_stage_ids: Array[StringName] = [&"adult"]
	invalid_age.creation_stage_ids = invalid_creation_stage_ids
	invalid_age.default_age_by_stage = {"adult": 18}
	registry._age_profile_defs = {
		invalid_age.profile_id: invalid_age,
	}

	registry._bloodline_defs = {
		&"broken_bloodline": _make_bloodline(&"broken_bloodline", [&"missing_bloodline_stage", &"shared_stage"], [&"missing_trait"], &"bloodline_skill"),
	}
	registry._bloodline_stage_defs = {
		&"shared_stage": _make_bloodline_stage(&"shared_stage", &"wrong_bloodline", [&"missing_trait"], &"bloodline_skill"),
	}
	registry._ascension_defs = {
		&"broken_ascension": _make_ascension(&"broken_ascension", [&"missing_ascension_stage", &"shared_stage"], [&"missing_bloodline"], [&"missing_trait"], &"ascension_skill"),
	}
	var invalid_ascension_stage := _make_ascension_stage(&"shared_stage", &"wrong_ascension", [&"missing_trait"], &"ascension_skill")
	invalid_ascension_stage.body_size_category_override = &"colossal"
	registry._ascension_stage_defs = {
		invalid_ascension_stage.stage_id: invalid_ascension_stage,
	}

	var invalid_modifier := _make_stage_advancement(&"broken_advancement", &"temporary_status", &"missing_stage")
	invalid_modifier.applies_to_race_ids = [&"missing_race"]
	invalid_modifier.applies_to_subrace_ids = [&"missing_subrace"]
	invalid_modifier.applies_to_bloodline_ids = [&"missing_bloodline"]
	invalid_modifier.applies_to_ascension_ids = [&"missing_ascension"]
	registry._stage_advancement_defs = {
		invalid_modifier.modifier_id: invalid_modifier,
	}

	var errors := registry.validate()
	_assert_error_contains(errors, "references missing age_profile missing_age", "Race -> age_profile 引用应进入 Phase 2 校验。")
	_assert_error_contains(errors, "subrace broken_subrace parent_race_id must be broken_race", "Race -> subrace 反向 parent_race_id 应进入 Phase 2 校验。")
	_assert_error_contains(errors, "references missing parent_race missing_parent", "Subrace.parent_race_id 应进入 Phase 2 校验。")
	_assert_error_contains(errors, "trait_ids references missing trait missing_trait", "trait_ids 引用应进入 Phase 2 校验。")
	_assert_error_contains(errors, "skill race_skill learn_source must be race, got profession", "racial_granted_skills.skill_id 的 learn_source 应匹配来源。")
	_assert_error_contains(errors, "RaceTrait undispatched_trait trigger_type on_crit has no TraitTriggerHooks dispatch", "非 passive trait 必须进入 TraitTriggerHooks dispatch。")
	_assert_error_contains(errors, "unsupported damage tag cold", "damage_resistances key 应校验 damage tag。")
	_assert_error_contains(errors, "uses unsupported mitigation tier resist", "damage_resistances value 应校验 mitigation tier。")
	_assert_error_contains(errors, "damage_resistances key 123 must be a non-empty String or StringName", "damage_resistances key 类型应可定位。")
	_assert_error_contains(errors, "damage_resistances[poison] must be a non-empty String or StringName", "damage_resistances value 类型应可定位。")
	_assert_error_contains(errors, "Race broken_race body_size_category uses unsupported body_size_category colossal", "Race.body_size_category 应校验合法枚举。")
	_assert_error_contains(errors, "Subrace broken_subrace body_size_category_override uses unsupported body_size_category colossal", "Subrace.body_size_category_override 应校验合法枚举。")
	_assert_error_contains(errors, "must declare at least one stage_rules entry", "AgeProfile.stage_rules 应要求非空。")
	_assert_error_contains(errors, "creation_stage_ids references missing stage adult", "AgeProfile.creation_stage_ids 应指向本 profile stage。")
	_assert_error_contains(errors, "references missing bloodline_stage missing_bloodline_stage", "Bloodline.stage_ids 应校验 stage 引用。")
	_assert_error_contains(errors, "references missing ascension_stage missing_ascension_stage", "Ascension.stage_ids 应校验 stage 引用。")
	_assert_error_contains(errors, "Stage id shared_stage must be globally unique", "bloodline/ascension stage_id 应全局唯一。")
	_assert_error_contains(errors, "allowed_bloodline_ids references missing bloodline missing_bloodline", "Ascension.allowed_bloodline_ids 应校验引用。")
	_assert_error_contains(errors, "uses unsupported target_axis temporary_status", "StageAdvancement.target_axis 应校验合法枚举。")
	_assert_error_contains(errors, "applies_to_race_ids references missing race missing_race", "StageAdvancement.applies_to_race_ids 应校验引用。")
	_assert_error_contains(errors, "applies_to_subrace_ids references missing subrace missing_subrace", "StageAdvancement.applies_to_subrace_ids 应校验引用。")
	_assert_error_contains(errors, "applies_to_bloodline_ids references missing bloodline missing_bloodline", "StageAdvancement.applies_to_bloodline_ids 应校验引用。")
	_assert_error_contains(errors, "applies_to_ascension_ids references missing ascension missing_ascension", "StageAdvancement.applies_to_ascension_ids 应校验引用。")
	_assert_error_contains(errors, "max_stage_id references missing stage missing_stage", "StageAdvancement.max_stage_id 应校验目标 stage 引用。")
	_assert_error_contains(errors, "AscensionStage shared_stage body_size_category_override uses unsupported body_size_category colossal", "AscensionStage.body_size_category_override 应校验合法枚举。")


func _test_racial_granted_skill_minimum_level_cannot_exceed_skill_max_level() -> void:
	var registry := _make_empty_progression_registry()
	var race_skill := _make_skill(&"race_level_cap_skill", &"race")
	race_skill.max_level = 1
	race_skill.mastery_curve = PackedInt32Array([10])
	registry._skill_defs = {
		race_skill.skill_id: race_skill,
	}
	registry._race_defs = {
		&"level_cap_race": _make_race(
			&"level_cap_race",
			&"missing_age",
			&"missing_subrace",
			[&"missing_subrace"],
			[],
			race_skill.skill_id
		),
	}
	var race := registry._race_defs[&"level_cap_race"] as RaceDef
	race.racial_granted_skills[0].minimum_skill_level = 2

	var errors := registry.validate()
	_assert_error_contains(
		errors,
		"Race level_cap_race racial_granted_skills[0] skill race_level_cap_skill minimum_skill_level must be <= max_level 1",
		"racial_granted_skills.minimum_skill_level 不应超过目标 SkillDef.max_level。"
	)


func _make_empty_progression_registry() -> ProgressionContentRegistry:
	var registry := ProgressionContentRegistry.new()
	registry._validation_errors.clear()
	registry._skill_defs.clear()
	registry._profession_defs.clear()
	registry._achievement_defs.clear()
	registry._quest_defs.clear()
	registry._race_defs.clear()
	registry._subrace_defs.clear()
	registry._race_trait_defs.clear()
	registry._age_profile_defs.clear()
	registry._bloodline_defs.clear()
	registry._bloodline_stage_defs.clear()
	registry._ascension_defs.clear()
	registry._ascension_stage_defs.clear()
	registry._stage_advancement_defs.clear()
	return registry


func _make_skill(skill_id: StringName, learn_source: StringName) -> SkillDef:
	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.icon_id = skill_id
	skill.description = "Fixture skill."
	skill.learn_source = learn_source
	skill.skill_type = &"passive"
	skill.max_level = 1
	skill.mastery_curve = PackedInt32Array([10])
	return skill


func _make_trait(trait_id: StringName) -> RaceTraitDef:
	var trait_def := RaceTraitDef.new()
	trait_def.trait_id = trait_id
	trait_def.display_name = String(trait_id)
	trait_def.description = "Fixture trait."
	trait_def.trigger_type = &"passive"
	trait_def.effect_type = RaceTraitDef.EFFECT_DARKVISION
	return trait_def


func _make_age_profile(profile_id: StringName, race_id: StringName, stage_ids: Array) -> AgeProfileDef:
	var profile := AgeProfileDef.new()
	profile.profile_id = profile_id
	profile.race_id = race_id
	var rules: Array[AgeStageRule] = []
	for stage_id_variant in stage_ids:
		var stage_id := StringName(stage_id_variant)
		var rule := AgeStageRule.new()
		rule.stage_id = stage_id
		rule.display_name = String(stage_id)
		rule.description = "Fixture age stage."
		rules.append(rule)
	profile.stage_rules = rules
	var creation_stage_ids: Array[StringName] = []
	var default_age_by_stage: Dictionary = {}
	if not stage_ids.is_empty():
		var first_stage_id := StringName(stage_ids[0])
		creation_stage_ids.append(first_stage_id)
		default_age_by_stage[String(first_stage_id)] = 18
	profile.creation_stage_ids = creation_stage_ids
	profile.default_age_by_stage = default_age_by_stage
	return profile


func _make_race(
	race_id: StringName,
	age_profile_id: StringName,
	default_subrace_id: StringName,
	subrace_ids: Array[StringName],
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> RaceDef:
	var race := RaceDef.new()
	race.race_id = race_id
	race.display_name = String(race_id)
	race.description = "Fixture race."
	race.age_profile_id = age_profile_id
	race.default_subrace_id = default_subrace_id
	race.subrace_ids = subrace_ids
	race.body_size_category = &"medium"
	race.base_speed = 6
	race.trait_ids = trait_ids
	race.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	race.damage_resistances = {&"fire": &"half"}
	return race


func _make_subrace(
	subrace_id: StringName,
	parent_race_id: StringName,
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> SubraceDef:
	var subrace := SubraceDef.new()
	subrace.subrace_id = subrace_id
	subrace.parent_race_id = parent_race_id
	subrace.display_name = String(subrace_id)
	subrace.description = "Fixture subrace."
	subrace.trait_ids = trait_ids
	subrace.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	subrace.damage_resistances = {&"freeze": &"immune"}
	return subrace


func _make_bloodline(
	bloodline_id: StringName,
	stage_ids: Array[StringName],
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> BloodlineDef:
	var bloodline := BloodlineDef.new()
	bloodline.bloodline_id = bloodline_id
	bloodline.display_name = String(bloodline_id)
	bloodline.description = "Fixture bloodline."
	bloodline.stage_ids = stage_ids
	bloodline.trait_ids = trait_ids
	bloodline.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	return bloodline


func _make_bloodline_stage(
	stage_id: StringName,
	bloodline_id: StringName,
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> BloodlineStageDef:
	var stage := BloodlineStageDef.new()
	stage.stage_id = stage_id
	stage.bloodline_id = bloodline_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture bloodline stage."
	stage.trait_ids = trait_ids
	stage.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	return stage


func _make_ascension(
	ascension_id: StringName,
	stage_ids: Array[StringName],
	allowed_bloodline_ids: Array[StringName],
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> AscensionDef:
	var ascension := AscensionDef.new()
	ascension.ascension_id = ascension_id
	ascension.display_name = String(ascension_id)
	ascension.description = "Fixture ascension."
	ascension.stage_ids = stage_ids
	ascension.trait_ids = trait_ids
	ascension.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	ascension.allowed_race_ids = [&"human"]
	ascension.allowed_subrace_ids = [&"high_human"]
	ascension.allowed_bloodline_ids = allowed_bloodline_ids
	return ascension


func _make_ascension_stage(
	stage_id: StringName,
	ascension_id: StringName,
	trait_ids: Array[StringName],
	granted_skill_id: StringName
) -> AscensionStageDef:
	var stage := AscensionStageDef.new()
	stage.stage_id = stage_id
	stage.ascension_id = ascension_id
	stage.display_name = String(stage_id)
	stage.description = "Fixture ascension stage."
	stage.trait_ids = trait_ids
	stage.racial_granted_skills = [_make_granted_skill(granted_skill_id)]
	stage.body_size_category_override = &"large"
	return stage


func _make_stage_advancement(
	modifier_id: StringName,
	target_axis: StringName,
	max_stage_id: StringName
) -> StageAdvancementModifier:
	var modifier := StageAdvancementModifier.new()
	modifier.modifier_id = modifier_id
	modifier.display_name = String(modifier_id)
	modifier.target_axis = target_axis
	modifier.max_stage_id = max_stage_id
	modifier.applies_to_bloodline_ids = [&"titan"]
	return modifier


func _make_granted_skill(skill_id: StringName) -> RacialGrantedSkill:
	var granted_skill := RacialGrantedSkill.new()
	granted_skill.skill_id = skill_id
	granted_skill.minimum_skill_level = 1
	granted_skill.charge_kind = RacialGrantedSkill.CHARGE_KIND_PER_BATTLE
	granted_skill.charges = 1
	return granted_skill


func _assert_error_contains(errors: Array[String], expected_fragment: String, message: String) -> void:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return
	_test.fail("%s | missing fragment=%s errors=%s" % [message, expected_fragment, str(errors)])


func _assert_current_official_progression_validation_errors(errors: Array[String], message: String) -> void:
	_assert_true(errors.is_empty(), "%s | errors=%s" % [message, str(errors)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
