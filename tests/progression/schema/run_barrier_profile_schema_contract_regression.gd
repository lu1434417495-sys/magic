extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_barrier_profile_scripts_exist()
	_test_prismatic_sphere_profile_is_data_owned()
	_test_prismatic_sphere_profile_declares_2e_contract()
	_test.finish(self, "Barrier profile schema contract regression")


func _test_barrier_profile_scripts_exist() -> void:
	_assert_resource_script("res://scripts/player/progression/barrier_profile_def.gd")
	_assert_resource_script("res://scripts/player/progression/barrier_layer_def.gd")
	_assert_resource_script("res://scripts/player/progression/barrier_outcome_def.gd")
	_assert_resource_script("res://scripts/player/progression/barrier_content_registry.gd")


func _test_prismatic_sphere_profile_is_data_owned() -> void:
	var profile_path := "res://data/configs/barriers/prismatic_sphere.tres"
	if not FileAccess.file_exists(profile_path):
		_failures.append("Prismatic sphere barrier profile must live at %s." % profile_path)
		return
	var profile = load(profile_path)
	if profile == null:
		_failures.append("Prismatic sphere barrier profile must load as a Resource.")
		return
	_assert_has_property(profile, "profile_id", "BarrierProfileDef must expose profile_id.")
	_assert_has_property(profile, "layers", "BarrierProfileDef must expose ordered layers.")
	_assert_has_property(profile, "anchor_mode", "BarrierProfileDef must expose anchor_mode.")
	_assert_has_property(profile, "area_pattern", "BarrierProfileDef must expose area_pattern.")
	_assert_has_property(profile, "radius_cells", "BarrierProfileDef must expose radius_cells.")
	_assert_has_property(profile, "catch_all_projected_effects", "BarrierProfileDef must explicitly declare catch-all projected blocking policy.")
	_assert_eq(profile.get("profile_id"), &"prismatic_sphere", "Prismatic sphere profile id must be stable.")
	_assert_true(bool(profile.get("catch_all_projected_effects")), "Prismatic sphere must explicitly declare catch-all projected effect blocking.")


func _test_prismatic_sphere_profile_declares_2e_contract() -> void:
	var profile_path := "res://data/configs/barriers/prismatic_sphere.tres"
	if not FileAccess.file_exists(profile_path):
		return
	var profile = load(profile_path)
	if profile == null or not _has_property(profile, "layers"):
		return
	var layers: Array = profile.get("layers")
	_assert_eq(layers.size(), 7, "Prismatic sphere profile must declare exactly seven layers.")
	var expected_layer_ids: Array[StringName] = [&"red", &"orange", &"yellow", &"green", &"blue", &"indigo", &"violet"]
	var expected_breakers: Array[StringName] = [
		&"mage_cone_of_cold",
		&"mage_gust_of_wind",
		&"mage_spell_disjunction",
		&"mage_passwall",
		&"mage_arcane_missile",
		&"mage_continual_light",
		&"mage_dispel_magic",
	]
	var expected_outcomes: Array[StringName] = [&"damage", &"damage", &"damage", &"poison_death", &"status", &"status", &"banish"]
	var expected_statuses: Dictionary = {
		&"blue": &"petrified",
		&"indigo": &"madness",
	}
	for index in range(expected_layer_ids.size()):
		if index >= layers.size():
			return
		var layer = layers[index]
		if layer == null:
			_failures.append("Prismatic sphere layer %d must be a BarrierLayerDef resource." % index)
			continue
		_assert_has_property(layer, "layer_id", "BarrierLayerDef must expose layer_id.")
		_assert_has_property(layer, "order", "BarrierLayerDef must expose order.")
		_assert_has_property(layer, "blocked_categories", "BarrierLayerDef must expose blocked_categories.")
		_assert_has_property(layer, "breaker_skill_ids", "BarrierLayerDef must expose breaker_skill_ids.")
		_assert_has_property(layer, "passage_outcomes", "BarrierLayerDef must expose passage_outcomes.")
		var layer_id := _to_string_name(layer.get("layer_id"))
		_assert_eq(layer_id, expected_layer_ids[index], "Prismatic sphere layer order must match 2E color order.")
		_assert_eq(int(layer.get("order")), index + 1, "Prismatic sphere layer order field must be one-based and stable.")
		var breaker_ids: Array = layer.get("breaker_skill_ids")
		_assert_true(breaker_ids.has(expected_breakers[index]), "Layer %s must declare its breaker skill in data." % String(layer_id))
		var outcomes: Array = layer.get("passage_outcomes")
		_assert_true(not outcomes.is_empty(), "Layer %s must declare at least one passage outcome." % String(layer_id))
		if outcomes.is_empty():
			continue
		var outcome = outcomes[0]
		_assert_has_property(outcome, "outcome_type", "BarrierOutcomeDef must expose outcome_type.")
		_assert_has_property(outcome, "save_ability", "BarrierOutcomeDef must expose save_ability.")
		_assert_has_property(outcome, "save_tag", "BarrierOutcomeDef must expose save_tag.")
		_assert_eq(_to_string_name(outcome.get("outcome_type")), expected_outcomes[index], "Layer %s must declare the expected passage outcome type." % String(layer_id))
		if expected_statuses.has(layer_id):
			_assert_has_property(outcome, "status_id", "Status outcomes must expose status_id.")
			_assert_eq(_to_string_name(outcome.get("status_id")), expected_statuses[layer_id], "Layer %s must declare its status effect in data." % String(layer_id))


func _assert_resource_script(path: String) -> void:
	if not FileAccess.file_exists(path):
		_failures.append("Required barrier content script is missing: %s." % path)
		return
	var script = load(path)
	if script == null:
		_failures.append("Required barrier content script must load: %s." % path)


func _assert_has_property(object, property_name: String, message: String) -> bool:
	if not _has_property(object, property_name):
		_failures.append(message)
		return false
	return true


func _has_property(object, property_name: String) -> bool:
	if object == null:
		return false
	for property in object.get_property_list():
		if String(property.get("name", "")) == property_name:
			return true
	return false


func _to_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	return StringName(String(value))


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	_test.assert_eq(actual, expected, message)
