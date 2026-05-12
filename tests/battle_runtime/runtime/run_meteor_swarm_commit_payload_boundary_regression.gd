extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const MeteorSwarmCommitResult = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd")
const MeteorSwarmTargetPlan = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd")

const ALLOWED_COMMON_OUTCOME_PAYLOAD_FILES := {
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd": true,
	"res://scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd": true,
}
const SPECIAL_RUNTIME_FILES := [
	"res://scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd",
	"res://scripts/systems/battle/runtime/battle_special_profile_gate.gd",
	"res://scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd",
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd",
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd",
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd",
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_outcome.gd",
	"res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_common_outcome_payload_boundary_is_auditable()
	_test_special_runtime_does_not_read_legacy_effect_defs()
	_test_commit_payload_is_deep_copy_boundary()
	if _failures.is_empty():
		print("Meteor swarm commit payload boundary regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Meteor swarm commit payload boundary regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_common_outcome_payload_boundary_is_auditable() -> void:
	for file_path in _collect_gd_files("res://scripts"):
		var text := _read_text(file_path)
		if not text.contains("to_common_outcome_payload("):
			continue
		_assert_true(ALLOWED_COMMON_OUTCOME_PAYLOAD_FILES.has(file_path), "to_common_outcome_payload 只能出现在 typed result 和 commit adapter 边界：%s" % file_path)


func _test_special_runtime_does_not_read_legacy_effect_defs() -> void:
	for file_path in SPECIAL_RUNTIME_FILES:
		var text := _read_text(file_path)
		_assert_true(not text.contains(".effect_defs"), "Meteor special runtime/core 不得读取 legacy executable effect_defs：%s" % file_path)


func _test_commit_payload_is_deep_copy_boundary() -> void:
	var plan := MeteorSwarmTargetPlan.new()
	plan.skill_id = &"mage_meteor_swarm"
	plan.source_unit_id = &"meteor_commit_payload_caster"
	plan.final_anchor_coord = Vector2i(4, 4)
	plan.nominal_plan_signature = "nominal"
	plan.final_plan_signature = "final"
	var result := MeteorSwarmCommitResult.new()
	result.plan = plan
	result.total_damage = 42
	result.changed_unit_ids.append(&"target_a")
	result.terrain_effects.append({"coord": Vector2i(4, 4), "terrain_effect_id": "meteor_swarm_crater_core"})
	var commit_payload := result.to_common_outcome_payload()
	_assert_eq(String(commit_payload.get("commit_schema_id", "")), "meteor_swarm_ground_commit", "commit payload schema id 应稳定。")
	_assert_eq(String(commit_payload.get("boundary_kind", "")), "common_outcome_payload", "commit payload 应标记边界类型。")
	var payload_terrain := commit_payload.get("terrain_effects", []) as Array
	(payload_terrain[0] as Dictionary)["terrain_effect_id"] = "mutated"
	_assert_eq(String(result.terrain_effects[0].get("terrain_effect_id", "")), "meteor_swarm_crater_core", "commit payload 必须是 deep copy，不能回写 typed result。")


func _collect_gd_files(root_path: String) -> Array[String]:
	var files: Array[String] = []
	var dir := DirAccess.open(root_path)
	if dir == null:
		return files
	dir.list_dir_begin()
	while true:
		var entry := dir.get_next()
		if entry.is_empty():
			break
		if entry.begins_with("."):
			continue
		var path := "%s/%s" % [root_path, entry]
		if dir.current_is_dir():
			files.append_array(_collect_gd_files(path))
		elif path.ends_with(".gd"):
			files.append(path)
	dir.list_dir_end()
	return files


func _read_text(file_path: String) -> String:
	var file := FileAccess.open(file_path, FileAccess.READ)
	if file == null:
		return ""
	return file.get_as_text()


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
