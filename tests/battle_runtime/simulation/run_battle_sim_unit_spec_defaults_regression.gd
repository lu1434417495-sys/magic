extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_default_attack_bonus_and_ac_are_initialized()
	_test_base_attributes_use_formal_attribute_service()
	_test_attribute_overrides_can_replace_attack_bonus_and_ac()
	_test_base_attribute_overrides_use_formal_action_threshold()
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
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS) == 10,
		"BattleSimUnitSpec 默认应初始化 10 AC，保持 simulation 与常规战斗的基础面板口径一致。"
	)


func _test_base_attributes_use_formal_attribute_service() -> void:
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = &"sim_formal_base"
	unit_spec.display_name = "正式属性模拟单位"
	unit_spec.current_hp = 99
	unit_spec.current_stamina = 110
	unit_spec.current_ap = 2
	unit_spec.action_threshold = 120
	unit_spec.base_attributes = {
		"strength": 10,
		"agility": 16,
		"constitution": 12,
		"perception": 14,
		"intelligence": 8,
		"willpower": 10,
	}
	var unit_state = unit_spec.to_battle_unit_state(&"player", &"ai")
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX) == 16,
		"BattleSimUnitSpec 有 base_attributes 时应使用正式 0 级初始 HP 公式。"
	)
	_assert_true(
		unit_state.current_hp == 16,
		"BattleSimUnitSpec 当前 HP 应按正式 HP 上限 clamp。"
	)
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX) == 110,
		"BattleSimUnitSpec 有 base_attributes 时应通过 AttributeService 派生体力。"
	)
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS) == 2,
		"BattleSimUnitSpec 有 base_attributes 时应通过 AttributeService 派生 AP。"
	)
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS) == 11,
		"BattleSimUnitSpec 有 base_attributes 时 AC 应来自正式 AttributeService。"
	)
	_assert_true(
		unit_state.action_threshold == ATTRIBUTE_SERVICE_SCRIPT.DEFAULT_CHARACTER_ACTION_THRESHOLD,
		"BattleSimUnitSpec 有 base_attributes 时 action_threshold 应来自正式属性快照。"
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


func _test_base_attribute_overrides_use_formal_action_threshold() -> void:
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = &"sim_threshold_override"
	unit_spec.display_name = "行动阈值覆盖单位"
	unit_spec.current_hp = 30
	unit_spec.current_stamina = 120
	unit_spec.current_ap = 2
	unit_spec.action_threshold = 120
	unit_spec.base_attributes = {
		"strength": 14,
		"agility": 12,
		"constitution": 14,
		"perception": 10,
		"intelligence": 8,
		"willpower": 10,
	}
	unit_spec.attribute_overrides = {
		"hp_max": 36,
		"action_threshold": 47,
	}
	var unit_state = unit_spec.to_battle_unit_state(&"hostile", &"ai")
	_assert_true(
		unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX) == 36,
		"base attribute 模拟单位的 hp_max 覆盖应通过正式属性快照生效。"
	)
	_assert_true(
		unit_state.action_threshold == 45,
		"base attribute 模拟单位的 action_threshold 覆盖应经过 AttributeService 的 5 TU 归一。"
	)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
