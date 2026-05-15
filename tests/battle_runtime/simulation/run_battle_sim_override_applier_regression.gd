extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_override_applier.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_brain_transition_patch_uses_deep_path()
	_test_formal_transition_profiles_apply_without_errors()
	_test_unknown_patch_path_reports_error()
	_test.finish(self, "Battle sim override applier regression")


func _test_brain_transition_patch_uses_deep_path() -> void:
	var brain = ResourceLoader.load("res://data/configs/enemies/brains/ranged_suppressor.tres")
	var profile = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	profile.profile_id = &"transition_patch_probe"
	profile.override_patches = [{
		"target_type": "brain",
		"target_id": "ranged_suppressor",
		"path": "transition_rules.0.conditions.0.basis_points",
		"value": 6000,
	}]
	var applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
	var result: Dictionary = applier.apply_profile({}, {brain.brain_id: brain}, profile)
	_test.assert_true(result.get("errors", []).is_empty(), "合法 transition 深路径 patch 不应产生错误: %s" % str(result.get("errors", [])))
	var patched_brain = result["enemy_ai_brains"].get(&"ranged_suppressor")
	var patched_rule = patched_brain.get_transition_rules()[0]
	var patched_condition = patched_rule.get_conditions()[0]
	_test.assert_eq(patched_condition.basis_points, 6000, "transition_rules.0.conditions.0.basis_points 应被 patch。")
	var original_rule = brain.get_transition_rules()[0]
	var original_condition = original_rule.get_conditions()[0]
	_test.assert_eq(original_condition.basis_points, 3000, "override applier 应深拷贝 brain，不应改写原资源。")


func _test_unknown_patch_path_reports_error() -> void:
	var brain = ResourceLoader.load("res://data/configs/enemies/brains/ranged_suppressor.tres")
	var profile = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	profile.profile_id = &"bad_path_probe"
	profile.override_patches = [{
		"target_type": "brain",
		"target_id": "ranged_suppressor",
		"path": "transition_rules.0.conditions.99.basis_points",
		"value": 6000,
	}]
	var applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
	var result: Dictionary = applier.apply_profile({}, {brain.brain_id: brain}, profile)
	var errors: Array = result.get("errors", [])
	_test.assert_false(errors.is_empty(), "未知 transition 深路径必须报告 errors。")
	if errors.is_empty():
		return
	_test.assert_true(String(errors[0]).contains("transition_rules.0.conditions.99.basis_points"), "error 应包含失败 path: %s" % str(errors))


func _test_formal_transition_profiles_apply_without_errors() -> void:
	var brains := {
		&"ranged_controller": ResourceLoader.load("res://data/configs/enemies/brains/ranged_controller.tres"),
		&"ranged_suppressor": ResourceLoader.load("res://data/configs/enemies/brains/ranged_suppressor.tres"),
	}
	var applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
	for profile_path in [
		"res://data/configs/battle_sim/profiles/mist_controller_aggressive.tres",
		"res://data/configs/battle_sim/profiles/ranged_suppressor_cautious.tres",
	]:
		var profile = ResourceLoader.load(profile_path)
		var result: Dictionary = applier.apply_profile({}, brains, profile)
		_test.assert_true(result.get("errors", []).is_empty(), "%s override patches 应全部可应用: %s" % [profile_path, str(result.get("errors", []))])
