extends SceneTree

const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_roundtrip_preserves_current_schema()
	_test_empty_optional_roster_and_profile_fields_are_accepted_when_present()
	_test_non_dictionary_and_extra_fields_are_rejected()
	_test_missing_required_field_is_rejected()
	_test_wrong_field_type_is_rejected()
	_test_empty_required_identity_fields_are_rejected()
	_test_invalid_encounter_kind_is_rejected()

	if _failures.is_empty():
		print("Encounter anchor schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Encounter anchor schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_roundtrip_preserves_current_schema() -> void:
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = &"wild_den_roundtrip"
	encounter_anchor.display_name = "Wild Den"
	encounter_anchor.world_coord = Vector2i(12, 7)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.enemy_roster_template_id = &"wolf_pack"
	encounter_anchor.region_tag = &"north_wilds"
	encounter_anchor.vision_range = 3
	encounter_anchor.is_cleared = true
	encounter_anchor.encounter_kind = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	encounter_anchor.encounter_profile_id = &"wolf_den"
	encounter_anchor.growth_stage = 2
	encounter_anchor.suppressed_until_step = 11

	var restored_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(encounter_anchor.to_dict())
	_assert_true(restored_anchor != null, "valid to_dict payload should deserialize.")
	if restored_anchor == null:
		return
	_assert_eq(restored_anchor.to_dict(), encounter_anchor.to_dict(), "valid roundtrip should preserve all serialized fields.")


func _test_empty_optional_roster_and_profile_fields_are_accepted_when_present() -> void:
	for encounter_kind in [
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE,
		ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT,
	]:
		var payload := _build_valid_payload()
		payload["encounter_kind"] = encounter_kind
		payload["enemy_roster_template_id"] = &""
		payload["encounter_profile_id"] = ""

		var restored_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload)
		_assert_true(
			restored_anchor != null,
			"%s payload should accept present empty roster/profile string fields." % String(encounter_kind)
		)


func _test_missing_required_field_is_rejected() -> void:
	for field_name in [
		"entity_id",
		"display_name",
		"world_coord",
		"faction_id",
		"enemy_roster_template_id",
		"region_tag",
		"vision_range",
		"is_cleared",
		"encounter_kind",
		"encounter_profile_id",
		"growth_stage",
		"suppressed_until_step",
	]:
		var payload := _build_valid_payload()
		payload.erase(field_name)
		_assert_true(
			ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload) == null,
			"missing required field %s should be rejected." % field_name
		)


func _test_non_dictionary_and_extra_fields_are_rejected() -> void:
	_assert_true(
		ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict("not_dictionary") == null,
		"non-Dictionary payload should be rejected."
	)

	var payload := _build_valid_payload()
	payload["legacy_encounter_type"] = "hostile"
	_assert_true(
		ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload) == null,
		"payload with extra legacy fields should be rejected."
	)


func _test_wrong_field_type_is_rejected() -> void:
	var cases := [
		{"field": "entity_id", "value": 12},
		{"field": "display_name", "value": &"Wild Den"},
		{"field": "world_coord", "value": Vector2(1.0, 2.0)},
		{"field": "faction_id", "value": 1},
		{"field": "enemy_roster_template_id", "value": 1},
		{"field": "region_tag", "value": 1},
		{"field": "vision_range", "value": "2"},
		{"field": "is_cleared", "value": 0},
		{"field": "encounter_kind", "value": 1},
		{"field": "encounter_profile_id", "value": 1},
		{"field": "growth_stage", "value": "0"},
		{"field": "suppressed_until_step", "value": "0"},
	]
	for case_data in cases:
		var payload := _build_valid_payload()
		payload[String(case_data["field"])] = case_data["value"]
		_assert_true(
			ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload) == null,
			"wrong type for %s should be rejected." % String(case_data["field"])
		)


func _test_empty_required_identity_fields_are_rejected() -> void:
	for field_name in [
		"entity_id",
		"display_name",
		"faction_id",
		"encounter_kind",
	]:
		var payload := _build_valid_payload()
		payload[field_name] = ""
		_assert_true(
			ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload) == null,
			"empty required identity field %s should be rejected." % field_name
		)


func _test_invalid_encounter_kind_is_rejected() -> void:
	var payload := _build_valid_payload()
	payload["encounter_kind"] = "legacy_default_hostile"
	_assert_true(
		ENCOUNTER_ANCHOR_DATA_SCRIPT.from_dict(payload) == null,
		"invalid encounter_kind should be rejected."
	)


func _build_valid_payload() -> Dictionary:
	return {
		"entity_id": "wild_anchor",
		"display_name": "Wild Anchor",
		"world_coord": Vector2i(4, 5),
		"faction_id": "hostile",
		"enemy_roster_template_id": "wolf_pack",
		"region_tag": "north_wilds",
		"vision_range": 2,
		"is_cleared": false,
		"encounter_kind": "single",
		"encounter_profile_id": "wolf_den",
		"growth_stage": 0,
		"suppressed_until_step": 0,
	}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
