extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const AscensionDef = preload("res://scripts/player/progression/ascension_def.gd")
const AscensionStageDef = preload("res://scripts/player/progression/ascension_stage_def.gd")
const BloodlineDef = preload("res://scripts/player/progression/bloodline_def.gd")
const BloodlineStageDef = preload("res://scripts/player/progression/bloodline_stage_def.gd")
const IdentityPayloadValidator = preload("res://scripts/systems/progression/identity_payload_validator.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const RaceDef = preload("res://scripts/player/progression/race_def.gd")
const SubraceDef = preload("res://scripts/player/progression/subrace_def.gd")

var _runner := TestRunner.new()


func _initialize() -> void:
	auto_accept_quit = false
	call_deferred("_run")


func _run() -> void:
	_test_valid_identity_passes()
	_test_rejects_missing_race()
	_test_rejects_missing_subrace()
	_test_rejects_subrace_parent_mismatch()
	_test_rejects_race_that_does_not_list_subrace()
	_test_rejects_half_set_bloodline_pair()
	_test_rejects_bloodline_stage_that_does_not_belong()
	_test_rejects_half_set_ascension_pair()
	_test_rejects_ascension_stage_that_does_not_belong()
	_test_rejects_ascension_disallowed_race()
	_test_rejects_ascension_disallowed_subrace()
	_test_rejects_ascension_disallowed_bloodline()
	_test_body_size_cache_mismatch_is_not_identity_error()
	_runner.finish(self, "Identity payload validator regression")


func _test_valid_identity_passes() -> void:
	var member := _make_member()
	member.bloodline_id = &"titan"
	member.bloodline_stage_id = &"titan_awakened"
	member.ascension_id = &"dragon_ascension"
	member.ascension_stage_id = &"dragon_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_runner.assert_true(errors.is_empty(), "valid identity payload should pass validation")


func _test_rejects_missing_race() -> void:
	var member := _make_member()
	member.race_id = &"missing_race"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "references missing race missing_race", "missing race should be rejected")


func _test_rejects_missing_subrace() -> void:
	var member := _make_member()
	member.subrace_id = &"missing_subrace"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "references missing subrace missing_subrace", "missing subrace should be rejected")


func _test_rejects_subrace_parent_mismatch() -> void:
	var member := _make_member()
	var bundle := _make_identity_bundle()
	bundle["subrace_defs"][&"high_human"].parent_race_id = &"elf"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, bundle)
	_assert_has_error(errors, "subrace high_human parent_race_id must be human, got elf", "subrace parent mismatch should be rejected")


func _test_rejects_race_that_does_not_list_subrace() -> void:
	var member := _make_member()
	var bundle := _make_identity_bundle()
	bundle["race_defs"][&"human"].subrace_ids = _typed_string_names([])

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, bundle)
	_assert_has_error(errors, "race human must list subrace high_human in subrace_ids", "race missing selected subrace should be rejected")


func _test_rejects_half_set_bloodline_pair() -> void:
	var member := _make_member()
	member.bloodline_id = &"titan"
	member.bloodline_stage_id = &""

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "bloodline_id and bloodline_stage_id must both be empty or both be set", "half-set bloodline pair should be rejected")


func _test_rejects_bloodline_stage_that_does_not_belong() -> void:
	var member := _make_member()
	member.bloodline_id = &"titan"
	member.bloodline_stage_id = &"dragon_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "bloodline_stage_id dragon_awakened does not belong to bloodline titan", "bloodline stage from another bloodline should be rejected")


func _test_rejects_half_set_ascension_pair() -> void:
	var member := _make_member()
	member.ascension_id = &"dragon_ascension"
	member.ascension_stage_id = &""

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "ascension_id and ascension_stage_id must both be empty or both be set", "half-set ascension pair should be rejected")


func _test_rejects_ascension_stage_that_does_not_belong() -> void:
	var member := _make_member()
	member.ascension_id = &"dragon_ascension"
	member.ascension_stage_id = &"elf_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "ascension_stage_id elf_awakened does not belong to ascension dragon_ascension", "ascension stage from another ascension should be rejected")


func _test_rejects_ascension_disallowed_race() -> void:
	var member := _make_member()
	member.race_id = &"elf"
	member.subrace_id = &"moon_elf"
	member.ascension_id = &"dragon_ascension"
	member.ascension_stage_id = &"dragon_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "ascension dragon_ascension does not allow race elf", "ascension allowed race gate should be enforced")


func _test_rejects_ascension_disallowed_subrace() -> void:
	var member := _make_member()
	member.subrace_id = &"low_human"
	member.ascension_id = &"dragon_ascension"
	member.ascension_stage_id = &"dragon_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "ascension dragon_ascension does not allow subrace low_human", "ascension allowed subrace gate should be enforced")


func _test_rejects_ascension_disallowed_bloodline() -> void:
	var member := _make_member()
	member.ascension_id = &"bloodline_locked_ascension"
	member.ascension_stage_id = &"bloodline_locked_awakened"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_assert_has_error(errors, "ascension bloodline_locked_ascension does not allow bloodline", "ascension allowed bloodline gate should be enforced")


func _test_body_size_cache_mismatch_is_not_identity_error() -> void:
	var member := _make_member()
	member.body_size = 99
	member.body_size_category = &"boss"

	var errors: Array[String] = IdentityPayloadValidator.validate_member_identity(member, _make_identity_bundle())
	_runner.assert_true(errors.is_empty(), "stale body size cache should be repairable data, not identity-invalid data")


func _make_member() -> PartyMemberState:
	var member := PartyMemberState.new()
	member.member_id = &"hero"
	member.display_name = "Hero"
	member.race_id = &"human"
	member.subrace_id = &"high_human"
	member.body_size = 2
	member.body_size_category = &"medium"
	return member


func _make_identity_bundle() -> Dictionary:
	return {
		"race_defs": {
			&"human": _make_race(&"human", [&"high_human", &"low_human"], &"medium"),
			&"elf": _make_race(&"elf", [&"moon_elf"], &"medium"),
		},
		"subrace_defs": {
			&"high_human": _make_subrace(&"high_human", &"human", &""),
			&"low_human": _make_subrace(&"low_human", &"human", &""),
			&"moon_elf": _make_subrace(&"moon_elf", &"elf", &""),
		},
		"bloodline_defs": {
			&"titan": _make_bloodline(&"titan", [&"titan_awakened"]),
			&"dragon": _make_bloodline(&"dragon", [&"dragon_awakened"]),
		},
		"bloodline_stage_defs": {
			&"titan_awakened": _make_bloodline_stage(&"titan_awakened", &"titan"),
			&"dragon_awakened": _make_bloodline_stage(&"dragon_awakened", &"dragon"),
		},
		"ascension_defs": {
			&"dragon_ascension": _make_ascension(&"dragon_ascension", [&"dragon_awakened"], [&"human"], [&"high_human"], []),
			&"elf_ascension": _make_ascension(&"elf_ascension", [&"elf_awakened"], [&"elf"], [&"moon_elf"], []),
			&"bloodline_locked_ascension": _make_ascension(&"bloodline_locked_ascension", [&"bloodline_locked_awakened"], [], [], [&"titan"]),
		},
		"ascension_stage_defs": {
			&"dragon_awakened": _make_ascension_stage(&"dragon_awakened", &"dragon_ascension", &"large"),
			&"elf_awakened": _make_ascension_stage(&"elf_awakened", &"elf_ascension", &""),
			&"bloodline_locked_awakened": _make_ascension_stage(&"bloodline_locked_awakened", &"bloodline_locked_ascension", &""),
		},
	}


func _make_race(id: StringName, subrace_ids: Array, body_size_category: StringName) -> RaceDef:
	var race := RaceDef.new()
	race.race_id = id
	race.subrace_ids = _typed_string_names(subrace_ids)
	race.body_size_category = body_size_category
	return race


func _make_subrace(id: StringName, parent_race_id: StringName, body_size_category: StringName) -> SubraceDef:
	var subrace := SubraceDef.new()
	subrace.subrace_id = id
	subrace.parent_race_id = parent_race_id
	subrace.body_size_category_override = body_size_category
	return subrace


func _make_bloodline(id: StringName, stage_ids: Array) -> BloodlineDef:
	var bloodline := BloodlineDef.new()
	bloodline.bloodline_id = id
	bloodline.stage_ids = _typed_string_names(stage_ids)
	return bloodline


func _make_bloodline_stage(id: StringName, bloodline_id: StringName) -> BloodlineStageDef:
	var stage := BloodlineStageDef.new()
	stage.stage_id = id
	stage.bloodline_id = bloodline_id
	return stage


func _make_ascension(
	id: StringName,
	stage_ids: Array,
	allowed_race_ids: Array,
	allowed_subrace_ids: Array,
	allowed_bloodline_ids: Array
) -> AscensionDef:
	var ascension := AscensionDef.new()
	ascension.ascension_id = id
	ascension.stage_ids = _typed_string_names(stage_ids)
	ascension.allowed_race_ids = _typed_string_names(allowed_race_ids)
	ascension.allowed_subrace_ids = _typed_string_names(allowed_subrace_ids)
	ascension.allowed_bloodline_ids = _typed_string_names(allowed_bloodline_ids)
	return ascension


func _make_ascension_stage(id: StringName, ascension_id: StringName, body_size_category: StringName) -> AscensionStageDef:
	var stage := AscensionStageDef.new()
	stage.stage_id = id
	stage.ascension_id = ascension_id
	stage.body_size_category_override = body_size_category
	return stage


func _assert_has_error(errors: Array[String], fragment: String, message: String) -> void:
	for error in errors:
		if error.find(fragment) >= 0:
			_runner.assert_true(true, message)
			return
	_runner.assert_true(false, "%s; got errors: %s" % [message, errors])


func _typed_string_names(values: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	for value in values:
		result.append(StringName(String(value)))
	return result
