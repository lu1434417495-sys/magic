extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ENEMY_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/enemies/enemy_content_registry.gd")

const BRAIN_PATHS := [
	"res://data/configs/enemies/brains/frontline_bulwark.tres",
	"res://data/configs/enemies/brains/healer_controller.tres",
	"res://data/configs/enemies/brains/mage_controller.tres",
	"res://data/configs/enemies/brains/melee_aggressor.tres",
	"res://data/configs/enemies/brains/ranged_archer.tres",
	"res://data/configs/enemies/brains/ranged_controller.tres",
	"res://data/configs/enemies/brains/ranged_suppressor.tres",
]

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_enemy_content_registry_accepts_generation_slots()
	_test_formal_brains_declare_generation_slots()
	_test_formal_brains_declare_transition_rules()
	_test.finish(self, "Enemy AI generation slots content regression")


func _test_formal_brains_declare_generation_slots() -> void:
	for brain_path in BRAIN_PATHS:
		var brain = ResourceLoader.load(brain_path)
		_test.assert_true(brain != null, "%s 应能加载。" % brain_path)
		if brain == null:
			continue
		for state_def in brain.get_states():
			if state_def == null:
				continue
			var state_errors: Array[String] = state_def.validate_schema(brain.brain_id, _collect_declared_skill_defs(state_def))
			_test.assert_true(state_errors.is_empty(), "%s state %s full schema 应合法: %s" % [
				brain_path,
				String(state_def.state_id),
				str(state_errors),
			])
			_test.assert_true(
				state_def.get("generation_slots") is Array and not state_def.get("generation_slots").is_empty(),
				"%s state %s 应声明 generation_slots。" % [brain_path, String(state_def.state_id)]
			)
			for slot in state_def.get("generation_slots"):
				_test.assert_true(slot != null, "%s state %s 不应包含空 generation slot。" % [brain_path, String(state_def.state_id)])
				if slot == null:
					continue
				var errors: Array[String] = slot.validate_schema(
					"%s state %s" % [brain_path, String(state_def.state_id)],
					state_def.get_actions()
				)
				_test.assert_true(errors.is_empty(), "%s state %s slot %s schema 应合法: %s" % [
					brain_path,
					String(state_def.state_id),
					String(slot.slot_id),
					str(errors),
				])


func _collect_declared_skill_defs(state_def) -> Dictionary:
	var skill_defs: Dictionary = {}
	if state_def == null:
		return skill_defs
	for action in state_def.get_actions():
		if action == null or not action.has_method("get_declared_skill_ids"):
			continue
		for skill_id in action.get_declared_skill_ids():
			skill_defs[ProgressionDataUtils.to_string_name(skill_id)] = true
	return skill_defs


func _collect_declared_skill_defs_for_brain(brain) -> Dictionary:
	var skill_defs: Dictionary = {}
	if brain == null:
		return skill_defs
	for state_def in brain.get_states():
		if state_def == null:
			continue
		for skill_id in _collect_declared_skill_defs(state_def).keys():
			skill_defs[skill_id] = true
	return skill_defs


func _test_formal_brains_declare_transition_rules() -> void:
	for brain_path in BRAIN_PATHS:
		var brain = ResourceLoader.load(brain_path)
		_test.assert_true(brain != null, "%s 应能加载。" % brain_path)
		if brain == null:
			continue
		_test.assert_true(
			brain.get("transition_rules") is Array and not brain.get("transition_rules").is_empty(),
			"%s 应声明 transition_rules。" % brain_path
		)
		var brain_errors: Array[String] = brain.validate_schema(_collect_declared_skill_defs_for_brain(brain))
		_test.assert_true(brain_errors.is_empty(), "%s transition/full schema 应合法: %s" % [brain_path, str(brain_errors)])
		var raw_text := FileAccess.get_file_as_string(brain_path)
		for old_field in ["retreat_hp_basis_points", "support_hp_basis_points", "pressure_distance"]:
			_test.assert_false(raw_text.contains(old_field), "%s 不应继续声明旧 transition 字段 %s。" % [brain_path, old_field])


func _test_enemy_content_registry_accepts_generation_slots() -> void:
	var registry = ENEMY_CONTENT_REGISTRY_SCRIPT.new()
	var errors: Array[String] = registry.validate()
	_test.assert_true(errors.is_empty(), "EnemyContentRegistry 应接受正式 generation slots: %s" % str(errors))
