extends SceneTree

const BATTLE_SAVE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const SKILL_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/skill_content_registry.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_skill_schema_accepts_valid_save_fields()
	_test_skill_schema_rejects_invalid_save_fields()
	if _failures.is_empty():
		print("Battle save skill schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle save skill schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_skill_schema_accepts_valid_save_fields() -> void:
	var registry = SKILL_CONTENT_REGISTRY_SCRIPT.new()
	var damage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	damage_effect.effect_type = &"damage"
	damage_effect.power = 8
	damage_effect.save_dc = 12
	damage_effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION
	damage_effect.save_tag = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_TAG_DRAGON_BREATH
	damage_effect.save_partial_on_success = true
	var damage_errors: Array[String] = []
	registry._append_effect_validation_errors(damage_errors, &"valid_save_damage", damage_effect, "test_effect")
	_assert_true(damage_errors.is_empty(), "valid damage save fields should pass SkillContentRegistry validation.")

	var status_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	status_effect.effect_type = &"status"
	status_effect.status_id = &"poisoned"
	status_effect.save_failure_status_id = &"poisoned"
	status_effect.save_dc = 11
	status_effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION
	status_effect.save_tag = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_TAG_POISON
	var status_errors: Array[String] = []
	registry._append_effect_validation_errors(status_errors, &"valid_save_status", status_effect, "test_effect")
	_assert_true(status_errors.is_empty(), "valid status save fields should pass SkillContentRegistry validation.")


func _test_skill_schema_rejects_invalid_save_fields() -> void:
	var registry = SKILL_CONTENT_REGISTRY_SCRIPT.new()
	var invalid_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	invalid_effect.effect_type = &"status"
	invalid_effect.status_id = &"bad_status"
	invalid_effect.save_dc = 10
	invalid_effect.save_ability = &"fortune"
	invalid_effect.save_tag = &"cold"
	invalid_effect.save_partial_on_success = true
	var invalid_errors: Array[String] = []
	registry._append_effect_validation_errors(invalid_errors, &"invalid_save_status", invalid_effect, "test_effect")
	_assert_true(_has_error_containing(invalid_errors, "unsupported save_ability"), "invalid save_ability should be rejected.")
	_assert_true(_has_error_containing(invalid_errors, "unsupported save_tag"), "invalid save_tag should be rejected.")
	_assert_true(_has_error_containing(invalid_errors, "save_partial_on_success is only supported on damage effects"), "status save_partial_on_success should be rejected.")

	var noop_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	noop_effect.effect_type = &"damage"
	noop_effect.power = 4
	noop_effect.save_tag = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_TAG_POISON
	var noop_errors: Array[String] = []
	registry._append_effect_validation_errors(noop_errors, &"noop_save", noop_effect, "test_effect")
	_assert_true(_has_error_containing(noop_errors, "save_tag requires save_dc"), "save_tag without save_dc should be rejected.")


func _has_error_containing(errors: Array[String], needle: String) -> bool:
	for error in errors:
		if error.contains(needle):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
