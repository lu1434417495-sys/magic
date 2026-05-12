extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ALLOWED_ATTACK_RESOLVER_FILES := {
	"res://scripts/systems/battle/rules/battle_attack_check_policy_service.gd": true,
	"res://scripts/systems/battle/rules/battle_hit_resolver.gd": true,
}
const REQUIRED_POLICY_CALLEE_FRAGMENTS := {
	"unit_execute_context": {
		"file": "res://scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd",
		"fragment": "build_attack_context(",
	},
	"unit_execute_check": {
		"file": "res://scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd",
		"fragment": "build_attack_check(",
	},
	"unit_preview": {
		"file": "res://scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd",
		"fragment": "build_attack_preview(",
	},
	"ground_execute": {
		"file": "res://scripts/systems/battle/runtime/battle_ground_effect_service.gd",
		"fragment": "build_attack_check(",
	},
	"repeat_execute_context": {
		"file": "res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd",
		"fragment": "attack_policy.build_repeat_attack_stage_context",
	},
	"repeat_execute_check": {
		"file": "res://scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd",
		"fragment": "attack_policy.build_fate_aware_repeat_attack_stage_hit_check",
	},
	"charge_path_execute": {
		"file": "res://scripts/systems/battle/runtime/battle_charge_resolver.gd",
		"fragment": "build_attack_check(",
	},
	"hud_preview": {
		"file": "res://scripts/systems/battle/presentation/battle_hud_adapter.gd",
		"fragment": "_attack_check_policy_service.build_attack_preview",
	},
	"ai_score_preview": {
		"file": "res://scripts/systems/battle/ai/battle_ai_score_service.gd",
		"fragment": "_populate_special_profile_metrics(score_input, context)",
	},
}

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_no_production_direct_hit_resolver_attack_calls()
	_test_required_call_sites_route_through_policy()
	_test_policy_context_public_contract()
	_test_repeat_policy_api_uses_typed_stage_specs()
	if _failures.is_empty():
		print("Attack policy call-site audit: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Attack policy call-site audit: FAIL (%d)" % _failures.size())
	quit(1)


func _test_no_production_direct_hit_resolver_attack_calls() -> void:
	for file_path in _collect_gd_files("res://scripts"):
		if ALLOWED_ATTACK_RESOLVER_FILES.has(file_path):
			continue
		var lines := _read_text(file_path).split("\n")
		for index in range(lines.size()):
			var line := String(lines[index])
			if _is_direct_hit_resolver_attack_call(line):
				_test.fail("生产调用点不得绕过 BattleAttackCheckPolicyService：%s:%d %s" % [
					file_path,
					index + 1,
					line.strip_edges(),
				])


func _test_required_call_sites_route_through_policy() -> void:
	for key in REQUIRED_POLICY_CALLEE_FRAGMENTS.keys():
		var spec := REQUIRED_POLICY_CALLEE_FRAGMENTS[key] as Dictionary
		var file_path := String(spec.get("file", ""))
		var fragment := String(spec.get("fragment", ""))
		_assert_true(_read_text(file_path).contains(fragment), "attack policy audit 缺少必需调用面 %s：%s" % [String(key), fragment])


func _test_repeat_policy_api_uses_typed_stage_specs() -> void:
	var source := _read_text("res://scripts/systems/battle/rules/battle_attack_check_policy_service.gd")
	_assert_true(
		not source.contains("func build_skill_attack_check(") and not source.contains("func build_skill_attack_preview("),
		"BattleAttackCheckPolicyService 公共命中 API 应收敛为 context-first 的 build_attack_check / build_attack_preview。"
	)
	_assert_true(
		not source.contains("CombatEffectDef") and not source.contains("repeat_attack_effect"),
		"BattleAttackCheckPolicyService repeat API 不应接收 CombatEffectDef / repeat_attack_effect，repeat resolver 应先翻译为 BattleRepeatAttackStageSpec。"
	)
	_assert_true(
		source.contains("BattleAttackCheckPolicyContext") and source.contains("BattleRepeatAttackStageSpec"),
		"BattleAttackCheckPolicyService repeat API 应显式使用 policy context 与 BattleRepeatAttackStageSpec。"
	)


func _test_policy_context_public_contract() -> void:
	var source := _read_text("res://scripts/systems/battle/core/battle_attack_check_policy_context.gd")
	_assert_true(
		source.contains("var target: BattleUnitState"),
		"BattleAttackCheckPolicyContext 应暴露文档约定的 target 字段。"
	)
	_assert_true(
		not source.contains("var target_unit: BattleUnitState"),
		"BattleAttackCheckPolicyContext 不应继续暴露 target_unit，避免新 API 又带回旧调用形状。"
	)


func _is_direct_hit_resolver_attack_call(line: String) -> bool:
	if line.contains("get_attack_check_policy_service()") or line.contains("_attack_check_policy_service.") or line.contains("attack_policy."):
		return false
	if not line.contains("hit_resolver"):
		return false
	return line.contains(".build_skill_attack_check(") \
		or line.contains(".build_skill_attack_preview(") \
		or line.contains(".build_attack_check(") \
		or line.contains(".build_attack_preview(") \
		or line.contains(".build_repeat_attack_stage_hit_check(") \
		or line.contains(".build_fate_aware_repeat_attack_stage_hit_check(") \
		or line.contains(".build_repeat_attack_preview(")


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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
