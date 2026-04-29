extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_default_attack_bonus_and_ac_are_initialized()
	_test_attribute_overrides_can_replace_attack_bonus_and_ac()
	if _failures.is_empty():
		print("Battle sim unit spec defaults regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle sim unit spec defaults regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_default_attack_bonus_and_ac_are_initialized() -> void:
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = &"sim_default"
	unit_spec.display_name = "默认模拟单位"
	unit_spec.current_hp = 30
	unit_spec.current_ap = 1
	var unit_state = unit_spec.to_battle_unit_state(&"player", &"manual")
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS) == 4,
		"BattleSimUnitSpec 默认应初始化 +4 攻击加值，避免 simulation 中退化到仅天然 20 命中。"
	)
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS) == 12,
		"BattleSimUnitSpec 默认应初始化 12 AC，保持 simulation 与常规战斗的基础面板口径一致。"
	)


func _test_attribute_overrides_can_replace_attack_bonus_and_ac() -> void:
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = &"sim_override"
	unit_spec.display_name = "覆盖模拟单位"
	unit_spec.current_hp = 30
	unit_spec.current_ap = 1
	unit_spec.attribute_overrides = {
		"attack_bonus": 6,
		"armor_class": 17,
	}
	var unit_state = unit_spec.to_battle_unit_state(&"hostile", &"ai")
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS) == 6,
		"BattleSimUnitSpec 应允许 attribute_overrides 覆盖默认攻击加值。"
	)
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS) == 17,
		"BattleSimUnitSpec 应允许 attribute_overrides 覆盖默认 AC。"
	)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
