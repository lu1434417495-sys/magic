extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_attack_check_reads_attacker_base_attack_bonus()
	_test_attack_check_falls_back_to_zero_when_attribute_absent()
	_test_attack_check_rejects_missing_target_armor_class()
	_test_attack_check_adds_bab_on_top_of_existing_attack_bonus()
	_test_locked_skill_hit_bonus_reduces_required_roll()
	_test_locked_skill_hit_bonus_applies_to_spell_control_roll()

	if _failures.is_empty():
		print("Battle hit resolver BAB regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle hit resolver BAB regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_attack_check_reads_attacker_base_attack_bonus() -> void:
	# 攻击者只配 BAB（其他攻击加值为 0），验证 BAB 直接进入 required_roll。
	var attacker := _make_unit_with_attack_bonuses(0, 5)
	var target := _make_unit_with_armor_class(15)
	var resolver := BattleHitResolver.new()

	var attack_check := resolver.build_skill_attack_check(attacker, target, null)
	_assert_eq(int(attack_check.get("attacker_base_attack_bonus", -1)), 5, "attack_check 应暴露 attacker 的 base_attack_bonus。")
	_assert_eq(int(attack_check.get("attacker_attack_bonus", -1)), 0, "attack_check 中的 attacker_attack_bonus 与 BAB 应保持独立字段。")
	# required_roll = AC - BAB - 其他 = 15 - 5 - 0 = 10
	_assert_eq(int(attack_check.get("required_roll", -1)), 10, "BAB +5 应把 required_roll 从 15 拉到 10。")


func _test_attack_check_falls_back_to_zero_when_attribute_absent() -> void:
	# snapshot 完全不含 BASE_ATTACK_BONUS 时（例如旧存档或 NPC），应回退到 0，行为与改动前等价。
	var attacker := BattleUnitState.new()
	# 不写入任何 attack 相关属性。
	var target := _make_unit_with_armor_class(12)
	var resolver := BattleHitResolver.new()

	var attack_check := resolver.build_skill_attack_check(attacker, target, null)
	_assert_eq(int(attack_check.get("attacker_base_attack_bonus", -1)), 0, "缺失 BASE_ATTACK_BONUS 时应回退为 0。")
	_assert_eq(int(attack_check.get("required_roll", -1)), 12, "缺失 BAB 时 required_roll 应等于裸 AC。")


func _test_attack_check_rejects_missing_target_armor_class() -> void:
	var attacker := _make_unit_with_attack_bonuses(0, 0)
	var target := BattleUnitState.new()
	var resolver := BattleHitResolver.new()

	var attack_check := resolver.build_skill_attack_check(attacker, target, null)
	_assert_true(bool(attack_check.get("invalid", false)), "缺失目标 armor_class 时不应回退到隐藏 AC。")
	_assert_eq(
		String(attack_check.get("error_id", "")),
		"missing_target_armor_class",
		"缺失目标 armor_class 应返回明确错误码。"
	)
	_assert_eq(int(attack_check.get("success_rate_percent", -1)), 0, "无效命中检定不应被 AI/HUD 当作 100% 命中。")


func _test_attack_check_adds_bab_on_top_of_existing_attack_bonus() -> void:
	# 同时配 BAB 与既有 ATTACK_BONUS，确认两者叠加（不替换）。
	var attacker := _make_unit_with_attack_bonuses(7, 3)
	var target := _make_unit_with_armor_class(20)
	var resolver := BattleHitResolver.new()

	var attack_check := resolver.build_skill_attack_check(attacker, target, null)
	_assert_eq(int(attack_check.get("attacker_base_attack_bonus", -1)), 3, "BAB 字段应取 BASE_ATTACK_BONUS。")
	_assert_eq(int(attack_check.get("attacker_attack_bonus", -1)), 7, "ATTACK_BONUS 字段应保留原值，不被 BAB 覆盖。")
	# required_roll = 20 - 3 (BAB) - 7 (ATTACK_BONUS) = 10
	_assert_eq(int(attack_check.get("required_roll", -1)), 10, "BAB 与 ATTACK_BONUS 应叠加进 required_roll，而非择一。")


func _test_locked_skill_hit_bonus_reduces_required_roll() -> void:
	var attacker := _make_unit_with_attack_bonuses(0, 0)
	attacker.known_skill_lock_hit_bonus_map[&"locked_slash"] = 2
	var target := _make_unit_with_armor_class(15)
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"locked_slash"
	var resolver := BattleHitResolver.new()

	var attack_check := resolver.build_skill_attack_check(attacker, target, skill_def)
	_assert_eq(int(attack_check.get("locked_skill_hit_bonus", -1)), 2, "attack_check 应读取锁定技能的命中加值。")
	_assert_eq(int(attack_check.get("required_roll", -1)), 13, "锁定命中 +2 应把 required_roll 从 15 降到 13。")


func _test_locked_skill_hit_bonus_applies_to_spell_control_roll() -> void:
	var source := _make_unit_with_attack_bonuses(0, 0)
	source.known_skill_lock_hit_bonus_map[&"locked_spell"] = 2
	var resolver := BattleHitResolver.new()

	var metadata := resolver.resolve_spell_control_metadata(source, {
		"skill_id": &"locked_spell",
		"attack_roll_override": 4,
	})
	_assert_eq(int(metadata.get("locked_skill_hit_bonus", -1)), 2, "法术检定元数据应记录锁定技能加值。")
	_assert_eq(int(metadata.get("hit_roll", -1)), 4, "法术检定应保留原始 d20。")
	_assert_eq(int(metadata.get("effective_hit_roll", -1)), 6, "法术检定应使用 d20 + 锁定加值作为有效检定值。")


func _make_unit_with_attack_bonuses(attack_bonus: int, base_attack_bonus: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, attack_bonus)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.BASE_ATTACK_BONUS, base_attack_bonus)
	return unit


func _make_unit_with_armor_class(armor_class: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, armor_class)
	return unit


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
