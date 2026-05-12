extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const BattleSpecialProfileRegistry = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
const BattleSpecialProfileManifest = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd")
const BattleSpecialProfileManifestValidator = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd")
const BattleSpecialProfileGate = preload("res://scripts/systems/battle/runtime/battle_special_profile_gate.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var progression_registry := ProgressionContentRegistry.new()
	var skill_defs := progression_registry.get_skill_defs()
	var meteor_skill = skill_defs.get(&"mage_meteor_swarm")
	_assert_true(meteor_skill != null, "陨星雨技能应存在。")
	_assert_true(meteor_skill.combat_profile != null, "陨星雨应声明 combat_profile。")
	_assert_eq(meteor_skill.combat_profile.special_resolution_profile_id, &"meteor_swarm", "陨星雨应切到 meteor_swarm special profile。")
	_assert_true(meteor_skill.combat_profile.effect_defs.is_empty(), "陨星雨不应保留 executable effect_defs。")
	_assert_eq(meteor_skill.combat_profile.area_pattern, &"radius", "陨星雨 shell 应保留 square-radius area metadata。")
	_assert_eq(int(meteor_skill.combat_profile.area_value), 3, "陨星雨 shell 的最外层应为 7x7。")

	var registry := BattleSpecialProfileRegistry.new()
	registry.rebuild(skill_defs)
	var errors := registry.validate()
	_assert_true(errors.is_empty(), "正式 battle special profile manifest 应通过校验：%s" % str(errors))
	var snapshot := registry.get_snapshot()
	_assert_true(bool(snapshot.get("ok", false)), "battle special profile snapshot 应为 ok。")
	var profile_id_by_skill_id := snapshot.get("profile_id_by_skill_id", {}) as Dictionary
	_assert_eq(String(profile_id_by_skill_id.get("mage_meteor_swarm", "")), "meteor_swarm", "snapshot 应映射 mage_meteor_swarm -> meteor_swarm。")
	var profiles := snapshot.get("profiles", {}) as Dictionary
	_assert_true(profiles.has("meteor_swarm"), "snapshot 应包含 meteor_swarm profile。")
	var meteor_profile_snapshot := profiles.get("meteor_swarm", {}) as Dictionary
	_assert_eq(String(meteor_profile_snapshot.get("runtime_resolver_id", "")), "meteor_swarm", "runtime resolver id 必须走 hardcoded meteor_swarm。")
	var profile_resource = meteor_profile_snapshot.get("profile_resource", null)
	_assert_true(profile_resource != null, "snapshot 应携带已加载 profile_resource。")
	_assert_true(profile_resource.get("impact_components").size() >= 4, "meteor_swarm profile 应声明 typed impact components。")
	_assert_true(profile_resource.get("terrain_profiles").size() >= 5, "meteor_swarm profile 应声明 typed terrain profiles。")
	_test_manifest_validator_rejects_non_default_required_tests(skill_defs, profile_resource)
	_test_manifest_validator_rejects_unknown_save_profile(profile_resource)
	_test_manifest_validator_rejects_duplicate_component_id(profile_resource)
	_test_manifest_validator_rejects_component_ring_outside_radius(profile_resource)
	_test_manifest_validator_rejects_terrain_ring_outside_radius(profile_resource)

	var gate := BattleSpecialProfileGate.new()
	gate.setup(snapshot)
	var allowed_result = gate.preview_skill(meteor_skill, BattleCommand.new(), BattleUnitState.new(), BattleState.new())
	_assert_true(allowed_result.allowed, "manifest gate 通过时应允许进入 meteor resolver。")

	var invalid_gate := BattleSpecialProfileGate.new()
	invalid_gate.setup({
		"ok": false,
		"errors": ["fixture error"],
		"profiles": {},
		"profile_id_by_skill_id": {},
	})
	var blocked_result = invalid_gate.preview_skill(meteor_skill, BattleCommand.new(), BattleUnitState.new(), BattleState.new())
	_assert_true(not blocked_result.allowed, "manifest gate 失败时应 fail closed。")
	_assert_eq(blocked_result.player_message, "该禁咒配置未通过校验，暂时无法施放。", "manifest gate fail closed 文案应稳定。")

	if _failures.is_empty():
		print("Meteor swarm manifest gate regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm manifest gate regression: FAIL (%d)" % _failures.size())
	quit(1)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _test_manifest_validator_rejects_non_default_required_tests(skill_defs: Dictionary, profile_resource: Resource) -> void:
	var manifest := BattleSpecialProfileManifest.new()
	manifest.profile_id = &"meteor_swarm"
	manifest.schema_version = 1
	manifest.owning_skill_ids = [&"mage_meteor_swarm"]
	manifest.runtime_resolver_id = &"meteor_swarm"
	manifest.runtime_read_policy = &"forbidden"
	manifest.profile_resource = profile_resource
	manifest.required_regression_tests = [
		"tests/battle_runtime/simulation/run_battle_simulation_regression.gd",
		"docs/discussions/meteor_swarm_impact_analysis.md",
	]
	var validator := BattleSpecialProfileManifestValidator.new()
	var errors := validator.validate_manifest(manifest, skill_defs)
	_assert_true(
		errors.any(func(error): return String(error).contains("default regression suite member")),
		"manifest validator 应拒绝 simulation/docs 等非默认回归入口：%s" % str(errors)
	)


func _test_manifest_validator_rejects_unknown_save_profile(profile_resource: Resource) -> void:
	var profile := (profile_resource as MeteorSwarmProfile).duplicate(true) as MeteorSwarmProfile
	_assert_true(profile != null, "save profile 负例前置：profile 应能 duplicate。")
	if profile == null or profile.impact_components.is_empty():
		return
	profile.impact_components[0].save_profile_id = &"legacy_dex_save"
	var validator := BattleSpecialProfileManifestValidator.new()
	var errors := validator.validate_meteor_swarm_profile(profile, true)
	_assert_true(
		errors.any(func(error): return String(error).contains("save_profile_id is unsupported")),
		"manifest validator 应拒绝未知 save_profile_id，避免运行时 fallback：%s" % str(errors)
	)


func _test_manifest_validator_rejects_duplicate_component_id(profile_resource: Resource) -> void:
	var profile := (profile_resource as MeteorSwarmProfile).duplicate(true) as MeteorSwarmProfile
	_assert_true(profile != null, "duplicate component_id 负例前置：profile 应能 duplicate。")
	if profile == null or profile.impact_components.size() < 2:
		return
	profile.impact_components[1].component_id = profile.impact_components[0].component_id
	var validator := BattleSpecialProfileManifestValidator.new()
	var errors := validator.validate_meteor_swarm_profile(profile, true)
	_assert_true(
		errors.any(func(error): return String(error).contains("component_id is duplicated")),
		"manifest validator 应拒绝重复 impact component_id：%s" % str(errors)
	)


func _test_manifest_validator_rejects_component_ring_outside_radius(profile_resource: Resource) -> void:
	var profile := (profile_resource as MeteorSwarmProfile).duplicate(true) as MeteorSwarmProfile
	_assert_true(profile != null, "component ring 负例前置：profile 应能 duplicate。")
	if profile == null or profile.impact_components.is_empty():
		return
	profile.impact_components[0].ring_max = 4
	var validator := BattleSpecialProfileManifestValidator.new()
	var errors := validator.validate_meteor_swarm_profile(profile, true)
	_assert_true(
		errors.any(func(error): return String(error).contains("ring range is invalid or outside radius")),
		"manifest validator 应拒绝越过 7x7 半径的 impact component ring：%s" % str(errors)
	)


func _test_manifest_validator_rejects_terrain_ring_outside_radius(profile_resource: Resource) -> void:
	var profile := (profile_resource as MeteorSwarmProfile).duplicate(true) as MeteorSwarmProfile
	_assert_true(profile != null, "terrain ring 负例前置：profile 应能 duplicate。")
	if profile == null or profile.terrain_profiles.is_empty():
		return
	var terrain_profile := (profile.terrain_profiles[0] as Dictionary).duplicate(true)
	terrain_profile["ring_max"] = 4
	profile.terrain_profiles[0] = terrain_profile
	var validator := BattleSpecialProfileManifestValidator.new()
	var errors := validator.validate_meteor_swarm_profile(profile, true)
	_assert_true(
		errors.any(func(error): return String(error).contains("ring range is invalid or outside radius")),
		"manifest validator 应拒绝越过 7x7 半径的 terrain profile ring：%s" % str(errors)
	)
