extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_SAVE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_preview_damage_effect_uses_shared_damage_math_without_mutating_units()
	_test_preview_damage_effect_uses_save_probability_without_rolling()
	if _failures.is_empty():
		print("Battle damage resolver preview contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle damage resolver preview contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_preview_damage_effect_uses_shared_damage_math_without_mutating_units() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"preview_source", &"player")
	var target = _make_unit(&"preview_target", &"enemy")
	_set_status(source, &"attack_up", 2, {})
	_set_status(target, &"damage_reduction_up", 1, {})
	target.damage_resistances[&"fire"] = &"half"
	target.current_shield_hp = 5
	target.shield_max_hp = 5
	target.shield_duration = 100
	var effect = _make_damage_effect(&"fire", 10)
	effect.params["dice_count"] = 2
	effect.params["dice_sides"] = 6

	var expected_preview: Dictionary = resolver.preview_damage_effect(
		source,
		target,
		effect,
		{},
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_ROLL_MODE_AVERAGE,
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_SAVE_MODE_EXPECTED
	)
	var expected_outcome := expected_preview.get("damage_outcome", {}) as Dictionary
	_assert_eq(int(expected_outcome.get("rolled_damage", -1)), 20, "average preview 应复用 offense multiplier 后的 rolled_damage。")
	_assert_eq(String(expected_outcome.get("mitigation_tier", "")), "half", "average preview 应复用抗性分层。")
	_assert_eq(int(expected_outcome.get("fixed_mitigation_total", -1)), 2, "average preview 应复用固定减伤。")
	_assert_eq(int(expected_preview.get("post_save_damage", -1)), 8, "average preview post-save damage 应来自共享 damage outcome。")
	_assert_eq(int(expected_preview.get("shield_absorbed", -1)), 5, "average preview 应走共享护盾吸收路径。")
	_assert_eq(int(expected_preview.get("hp_damage", -1)), 3, "average preview hp_damage 应扣除护盾后得到。")

	var worst_preview: Dictionary = resolver.preview_damage_effect(
		source,
		target,
		effect,
		{},
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_ROLL_MODE_MAXIMUM,
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_SAVE_MODE_WORST
	)
	_assert_eq(int(worst_preview.get("post_save_damage", -1)), 11, "worst preview 应使用最大骰并保留同一减伤链。")
	_assert_eq(int(worst_preview.get("hp_damage", -1)), 6, "worst preview 应在克隆护盾上结算 hp_damage。")
	_assert_eq(int(target.current_hp), 30, "preview 不应改真实目标 HP。")
	_assert_eq(int(target.current_shield_hp), 5, "preview 不应改真实目标护盾。")
	_assert_true(target.has_status_effect(&"damage_reduction_up"), "preview 不应改真实目标状态。")
	_assert_true(source.has_status_effect(&"attack_up"), "preview 不应改真实来源状态。")


func _test_preview_damage_effect_uses_save_probability_without_rolling() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"save_preview_source", &"player")
	var target = _make_unit(&"save_preview_target", &"enemy")
	var effect = _make_damage_effect(&"fire", 20)
	effect.save_dc = 10
	effect.save_ability = &"agility"
	effect.save_tag = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_TAG_MAGIC
	effect.save_partial_on_success = true

	var preview: Dictionary = resolver.preview_damage_effect(
		source,
		target,
		effect,
		{"save_roll_override": 20},
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_ROLL_MODE_AVERAGE,
		BATTLE_DAMAGE_RESOLVER_SCRIPT.DAMAGE_PREVIEW_SAVE_MODE_EXPECTED
	)
	var save_estimate := preview.get("save_estimate", {}) as Dictionary
	_assert_true(bool(save_estimate.get("has_save", false)), "save preview 应输出 save_estimate。")
	_assert_eq(int(save_estimate.get("save_success_probability_basis_points", -1)), 10000, "save_roll_override=20 应变成 100% 成功概率。")
	_assert_eq(int(preview.get("post_save_damage", -1)), 10, "成功且 partial save 应把伤害减半。")
	_assert_eq(int(target.current_hp), 30, "save preview 不应通过真实豁免掷骰改目标。")


func _make_damage_effect(damage_tag: StringName, power: int):
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.damage_tag = damage_tag
	effect.power = power
	effect.params = {}
	return effect


func _make_unit(unit_id: StringName, faction_id: StringName):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_ap = 2
	unit.current_stamina = 20
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 30)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	unit.attribute_snapshot.set_value(&"agility", 10)
	unit.attribute_snapshot.set_value(&"agility_modifier", 0)
	return unit


func _set_status(unit, status_id: StringName, power: int, params: Dictionary) -> void:
	var status = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status.status_id = status_id
	status.source_unit_id = unit.unit_id
	status.power = power
	status.stacks = power
	status.duration = -1
	status.params = params.duplicate(true)
	unit.set_status_effect(status)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
