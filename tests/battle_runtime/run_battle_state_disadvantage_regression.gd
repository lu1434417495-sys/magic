extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_attack_disadvantage_triggers_on_two_adjacent_enemies()
	_test_attack_disadvantage_triggers_on_low_hp()
	_test_attack_disadvantage_triggers_on_strong_attack_debuff()
	_test_attack_disadvantage_triggers_on_explicit_scene_tag()
	_test_attack_disadvantage_does_not_trigger_on_single_adjacent_enemy()
	_test_attack_disadvantage_does_not_trigger_on_wrong_element_target()
	_test_attack_disadvantage_does_not_trigger_on_bad_tactical_choice()
	_test_attack_disadvantage_does_not_trigger_on_economic_delay()
	_test_attack_disadvantage_does_not_trigger_on_soft_debuff()

	if _failures.is_empty():
		print("Battle state disadvantage regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle state disadvantage regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_attack_disadvantage_triggers_on_two_adjacent_enemies() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"flanked_attacker", &"player", Vector2i(2, 2))
	var defender := _build_unit(&"flanked_defender", &"enemy", Vector2i(3, 2))
	var side_enemy := _build_unit(&"flanked_side_enemy", &"enemy", Vector2i(2, 1))

	_add_units(state, [attacker, defender, side_enemy])
	_assert_true(
		state.is_attack_disadvantage(attacker, defender),
		"被 2 个相邻敌人包夹时应判定为 attack disadvantage。"
	)


func _test_attack_disadvantage_triggers_on_low_hp() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"low_hp_attacker", &"player", Vector2i(1, 1), 9, 30)
	var defender := _build_unit(&"low_hp_defender", &"enemy", Vector2i(3, 1))

	_add_units(state, [attacker, defender])
	_assert_true(
		state.is_attack_disadvantage(attacker, defender),
		"当前 HP <= 30% 时应判定为 attack disadvantage。"
	)


func _test_attack_disadvantage_triggers_on_strong_attack_debuff() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"debuffed_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"debuffed_defender", &"enemy", Vector2i(3, 1))
	attacker.status_effects[&"frozen"] = {
		"status_id": &"frozen",
		"duration": 15,
	}

	_add_units(state, [attacker, defender])
	_assert_true(
		state.is_attack_disadvantage(attacker, defender),
		"强攻击型 debuff 应触发 attack disadvantage。"
	)


func _test_attack_disadvantage_triggers_on_explicit_scene_tag() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"tagged_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"tagged_defender", &"enemy", Vector2i(3, 1))
	state.attack_disadvantage_tags = [&"darkness"]

	_add_units(state, [attacker, defender])
	_assert_true(
		state.is_attack_disadvantage(attacker, defender),
		"场景显式 hardship 标签应触发 attack disadvantage。"
	)


func _test_attack_disadvantage_does_not_trigger_on_single_adjacent_enemy() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"single_adjacent_attacker", &"player", Vector2i(2, 2))
	var defender := _build_unit(&"single_adjacent_defender", &"enemy", Vector2i(3, 2))

	_add_units(state, [attacker, defender])
	_assert_false(
		state.is_attack_disadvantage(attacker, defender),
		"只有 1 个相邻敌人时不应误判为被包夹。"
	)


func _test_attack_disadvantage_does_not_trigger_on_wrong_element_target() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"wrong_element_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"wrong_element_defender", &"enemy", Vector2i(3, 1))
	defender.status_effects[&"prismatic_barrier"] = {
		"status_id": &"prismatic_barrier",
		"duration": 15,
	}

	_add_units(state, [attacker, defender])
	_assert_false(
		state.is_attack_disadvantage(attacker, defender),
		"主动打错元素或命中抗性目标不应触发 attack disadvantage。"
	)


func _test_attack_disadvantage_does_not_trigger_on_bad_tactical_choice() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"bad_choice_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"bad_choice_defender", &"enemy", Vector2i(3, 1))
	defender.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 100)
	defender.status_effects[&"dodge_bonus_up"] = {
		"status_id": &"dodge_bonus_up",
		"duration": 15,
	}

	_add_units(state, [attacker, defender])
	_assert_false(
		state.is_attack_disadvantage(attacker, defender),
		"故意挑高闪避目标这种坏选择不应触发 attack disadvantage。"
	)


func _test_attack_disadvantage_does_not_trigger_on_economic_delay() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"economic_delay_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"economic_delay_defender", &"enemy", Vector2i(3, 1))
	attacker.current_ap = 0
	attacker.current_aura = 0
	attacker.current_stamina = 0

	_add_units(state, [attacker, defender])
	_assert_false(
		state.is_attack_disadvantage(attacker, defender),
		"纯经济拖延或资源打空不应触发 attack disadvantage。"
	)


func _test_attack_disadvantage_does_not_trigger_on_soft_debuff() -> void:
	var state := BattleState.new()
	var attacker := _build_unit(&"soft_debuff_attacker", &"player", Vector2i(1, 1))
	var defender := _build_unit(&"soft_debuff_defender", &"enemy", Vector2i(3, 1))
	attacker.status_effects[&"slow"] = {
		"status_id": &"slow",
		"duration": 15,
	}

	_add_units(state, [attacker, defender])
	_assert_false(
		state.is_attack_disadvantage(attacker, defender),
		"非强攻击型 debuff 不应触发 attack disadvantage。"
	)


func _build_unit(
	unit_id: StringName,
	faction_id: StringName,
	coord: Vector2i,
	current_hp: int = 30,
	max_hp: int = 30
) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.current_hp = current_hp
	unit.current_ap = 2
	unit.current_mp = 4
	unit.current_stamina = 4
	unit.current_aura = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, max_hp)
	return unit


func _add_units(state: BattleState, units: Array[BattleUnitState]) -> void:
	for unit in units:
		state.units[unit.unit_id] = unit
		if unit.faction_id == &"enemy":
			state.enemy_unit_ids.append(unit.unit_id)
		else:
			state.ally_unit_ids.append(unit.unit_id)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_false(condition: bool, message: String) -> void:
	_assert_true(not condition, message)
