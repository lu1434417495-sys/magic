extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillLevelDescriptionFormatter = preload("res://scripts/systems/progression/skill_level_description_formatter.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_chain_lightning_description_matches_save_enabled_effects()

	if _failures.is_empty():
		print("Skill description consistency regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Skill description consistency regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_chain_lightning_description_matches_save_enabled_effects() -> void:
	var registry := ProgressionContentRegistry.new()
	var chain_lightning = registry.get_skill_defs().get(&"mage_chain_lightning")
	_assert_true(chain_lightning != null, "链式闪击技能应存在。")
	if chain_lightning == null:
		return

	var level0_description := SkillLevelDescriptionFormatter.build_level_description(chain_lightning, 0)
	_assert_eq(
		level0_description,
		"射程5，造成4D6雷电伤害（敏捷豁免成功时伤害减半），并使目标进行体质豁免；失败则附加感电（60TU，强度1）。向范围内全部目标弹射全额伤害，不分敌我。湿地扩大弹射范围。消耗1AP/120法力，冷却60TU",
		"链式闪击 0 级描述应同时覆盖伤害敏捷豁免与感电体质豁免。"
	)

	var level7_description := SkillLevelDescriptionFormatter.build_level_description(chain_lightning, 7)
	_assert_eq(
		level7_description,
		"射程5，造成8D6雷电伤害（敏捷豁免成功时伤害减半），并使目标进行体质豁免；失败则附加感电（60TU，强度1）。向范围内全部目标弹射全额伤害，不分敌我。湿地扩大弹射范围。消耗1AP/120法力，冷却60TU",
		"链式闪击 7 级描述应沿用 typed effect 字段并只替换等级伤害。"
	)
	_assert_true(not level0_description.contains("shocked"), "链式闪击正式描述不应暴露英文状态 id。")
	_assert_true(not level7_description.contains("shocked"), "链式闪击高等级正式描述不应暴露英文状态 id。")


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual: String, expected: String, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, actual, expected])
