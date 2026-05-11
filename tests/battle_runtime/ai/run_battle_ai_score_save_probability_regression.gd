extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_AI_CONTEXT_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_context.gd")
const BATTLE_AI_SCORE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_score_service.gd")
const BATTLE_PREVIEW_SCRIPT = preload("res://scripts/systems/battle/core/battle_preview.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_ai_damage_estimate_weights_partial_save_probability()
	if _failures.is_empty():
		print("Battle AI score save probability regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle AI score save probability regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_ai_damage_estimate_weights_partial_save_probability() -> void:
	var source = _make_unit(&"caster", &"player", 30)
	var target = _make_unit(&"target", &"hostile", 35)
	var state = BATTLE_STATE_SCRIPT.new()
	state.units[source.unit_id] = source
	state.units[target.unit_id] = target

	var context = BATTLE_AI_CONTEXT_SCRIPT.new()
	context.state = state
	context.unit_state = source

	var skill = SKILL_DEF_SCRIPT.new()
	skill.skill_id = &"save_weighted_fire"
	skill.display_name = "Save Weighted Fire"

	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.damage_tag = &"fire"
	effect.power = 40
	effect.save_dc = 11
	effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION
	effect.save_tag = &"fireball"
	effect.save_partial_on_success = true

	var preview = BATTLE_PREVIEW_SCRIPT.new()
	preview.allowed = true
	preview.target_unit_ids.append(target.unit_id)

	var score_service = BATTLE_AI_SCORE_SERVICE_SCRIPT.new()
	var score_input = score_service.build_skill_score_input(context, skill, null, preview, [effect])

	_assert_eq(int(score_input.estimated_damage), 30, "40 点伤害、50% 半伤豁免时，AI 期望伤害应为 30。")
	_assert_eq(int(score_input.estimated_lethal_target_count), 0, "目标 35 HP 时，豁免加权后不应再被估成稳定击杀。")
	var save_estimates: Dictionary = score_input.save_estimates_by_target_id
	_assert_true(save_estimates.has(String(target.unit_id)), "score_input 应暴露目标豁免概率估算。")
	if save_estimates.has(String(target.unit_id)):
		var target_estimates: Array = save_estimates[String(target.unit_id)]
		_assert_true(not target_estimates.is_empty(), "目标豁免估算列表不应为空。")
		if not target_estimates.is_empty():
			var estimate: Dictionary = target_estimates[0]
			_assert_eq(int(estimate.get("save_success_rate_percent", -1)), 50, "DC11/CON0 的豁免成功率应为 50%。")
			_assert_eq(int(estimate.get("damage_after_save_estimate", -1)), 30, "trace 中也应保留豁免加权后的期望伤害。")


func _make_unit(unit_id: StringName, faction_id: StringName, hp: int):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_hp = hp
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, hp)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
	return unit


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	_test.assert_eq(actual, expected, message)
