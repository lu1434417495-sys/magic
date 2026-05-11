extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_required_runtime_barrier_files_exist()
	_test_runtime_does_not_load_barrier_content_directly()
	_test_runtime_has_no_prismatic_specific_rule_literals()
	_test_runtime_has_no_skill_id_text_category_guessing()
	_test_control_status_no_longer_depends_on_barrier_service()
	_test.finish(self, "Barrier architecture contract regression")


func _test_required_runtime_barrier_files_exist() -> void:
	_assert_file_exists("res://scripts/systems/battle/runtime/battle_barrier_service.gd", "BattleBarrierService must own barrier instances and interaction coordination.")
	_assert_file_exists("res://scripts/systems/battle/runtime/battle_barrier_geometry_service.gd", "BattleBarrierGeometryService must own footprint/line/area barrier geometry.")
	_assert_file_exists("res://scripts/systems/battle/runtime/battle_barrier_outcome_resolver.gd", "BattleBarrierOutcomeResolver must own whitelist outcome translation.")
	_assert_file_exists("res://scripts/systems/battle/core/battle_barrier_instance_state.gd", "Typed barrier instance state must replace anonymous barrier dictionaries.")
	_assert_file_exists("res://scripts/systems/battle/core/battle_barrier_layer_state.gd", "Typed barrier layer state must replace anonymous layer dictionaries.")


func _test_runtime_does_not_load_barrier_content_directly() -> void:
	for source_path in _collect_gd_files("res://scripts/systems/battle/runtime"):
		var text := _read_text(source_path)
		if text.contains("data/configs/barriers"):
			_failures.append("%s must not load barrier profile resources directly; profiles must come from the content registry." % source_path)


func _test_runtime_has_no_prismatic_specific_rule_literals() -> void:
	var forbidden_tokens := [
		"PROFILE_PRISMATIC_SPHERE",
		"GREEN_INSTANT_DEATH_DAMAGE",
		"_build_prismatic_sphere_layers",
		"mage_cone_of_cold",
		"mage_gust_of_wind",
		"mage_spell_disjunction",
		"mage_passwall",
		"mage_arcane_missile",
		"mage_continual_light",
		"mage_dispel_magic",
	]
	for source_path in _collect_gd_files("res://scripts/systems/battle/runtime"):
		var text := _read_text(source_path)
		for token in forbidden_tokens:
			if text.contains(token):
				_failures.append("%s must not contain prismatic-specific runtime rule literal '%s'." % [source_path, token])


func _test_runtime_has_no_skill_id_text_category_guessing() -> void:
	for source_path in _collect_gd_files("res://scripts/systems/battle/runtime") + _collect_gd_files("res://scripts/systems/battle/rules"):
		var text := _read_text(source_path)
		if text.contains("skill_id_text.contains") or text.contains(".contains(\"detect\")") or text.contains(".contains(\"breath\")"):
			_failures.append("%s must not infer barrier/effect categories from skill id text." % source_path)
		if text.contains("params.get(\"barrier_categories\"") or text.contains("params.get(&\"barrier_categories\""):
			_failures.append("%s must not read legacy params.barrier_categories." % source_path)


func _test_control_status_no_longer_depends_on_barrier_service() -> void:
	var timeline_text := _read_text("res://scripts/systems/battle/runtime/battle_timeline_driver.gd")
	var runtime_text := _read_text("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
	for forbidden in ["resolve_control_status_turn_start", "is_unit_ai_controlled_for_turn", "clear_turn_ai_control"]:
		if timeline_text.contains(forbidden):
			_failures.append("BattleTimelineDriver must not call barrier service control-status method '%s'." % forbidden)
		if runtime_text.contains(forbidden):
			_failures.append("BattleRuntimeModule must not call barrier service control-status method '%s'." % forbidden)


func _assert_file_exists(path: String, message: String) -> void:
	if not FileAccess.file_exists(path):
		_failures.append(message)


func _collect_gd_files(root_path: String) -> Array[String]:
	var results: Array[String] = []
	_collect_gd_files_recursive(root_path, results)
	results.sort()
	return results


func _collect_gd_files_recursive(root_path: String, results: Array[String]) -> void:
	var dir := DirAccess.open(root_path)
	if dir == null:
		return
	dir.list_dir_begin()
	while true:
		var entry := dir.get_next()
		if entry.is_empty():
			break
		if entry == "." or entry == "..":
			continue
		var path := "%s/%s" % [root_path, entry]
		if dir.current_is_dir():
			_collect_gd_files_recursive(path, results)
		elif entry.ends_with(".gd"):
			results.append(path)
	dir.list_dir_end()


func _read_text(path: String) -> String:
	if not FileAccess.file_exists(path):
		return ""
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return ""
	return file.get_as_text()
