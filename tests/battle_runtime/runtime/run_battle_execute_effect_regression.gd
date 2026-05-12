extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_execute_finishes_low_hp_target()
	_test_execute_non_lethal_on_high_hp_target()
	_test_execute_non_lethal_on_boss_target()
	_test_execute_shield_efficiency()
	_test_execute_min_hp_never_heals()
	if _failures.is_empty():
		print("Battle execute effect regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle execute effect regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_execute_finishes_low_hp_target() -> void:
	var source = _make_unit(&"mage_source", &"player")
	var target = _make_unit(&"weak_target", &"hostile")
	target.current_hp = 1
	var effect = _make_execute_effect()
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result = resolver.resolve_effects(source, target, [effect], {"save_roll_override": 1})
	_assert_true(not target.is_alive, "execute on HP=1 target with failed save should kill.")
	_assert_true(int(result.get("damage", 0)) > 0, "execute should register damage.")


func _test_execute_non_lethal_on_high_hp_target() -> void:
	var source = _make_unit(&"mage_source", &"player")
	var target = _make_unit(&"healthy_target", &"hostile")
	target.current_hp = 30
	var effect = _make_execute_effect()
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result = resolver.resolve_effects(source, target, [effect], {})
	_assert_true(target.is_alive, "execute on high-HP target should leave target alive.")
	# Threshold ~6, non-lethal = max(6*30/100,1)=1, so HP goes 30->29
	_assert_eq(target.current_hp, 29, "non-lethal should deal 1 damage leaving 29 HP.")
	_assert_eq(int(result.get("damage", 0)), 1, "non-lethal should register 1 damage.")


func _test_execute_non_lethal_on_boss_target() -> void:
	var source = _make_unit(&"mage_source", &"player")
	var target = _make_unit(&"boss_target", &"hostile")
	target.attribute_snapshot.set_value(&"boss_target", 1)
	target.current_hp = 5
	var effect = _make_execute_effect()
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result = resolver.resolve_effects(source, target, [effect], {})
	_assert_true(target.is_alive, "execute on boss target should never be lethal.")
	# Boss non-lethal = max(30*12/100,25)=25, clamped to min_hp=1 => HP becomes 1
	_assert_eq(target.current_hp, 1, "boss should be clamped to 1 HP.")


func _test_execute_shield_efficiency() -> void:
	var source = _make_unit(&"mage_source", &"player")
	var target = _make_unit(&"shielded_target", &"hostile")
	# HP=5 makes threshold=6, so target is vulnerable (enters burst path)
	target.current_hp = 5
	target.current_shield_hp = 20
	target.shield_max_hp = 20
	target.shield_duration = 10
	var effect = _make_execute_effect()
	effect.params["shield_absorption_percent"] = 50.0
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result = resolver.resolve_effects(source, target, [effect], {"save_roll_override": 20})
	var first_event = _first_damage_event(result)
	var shield_absorbed = int(first_event.get("shield_absorbed", 0))
	# 50%% efficiency on 20 shield -> capacity = 10, so at most 10 absorbed
	_assert_eq(shield_absorbed, 10, "50%% shield efficiency should absorb at most ceil(20*0.5)=10.")
	# Shield drained = ceil(10/0.5) = 20
	_assert_eq(target.current_shield_hp, 0, "50%% efficiency should drain all 20 shield HP.")
	# After burst 9999, HP should be clamped to 1 (save succeeded, no finisher)
	_assert_eq(target.current_hp, 1, "burst should clamp target to 1 HP after shield.")


func _test_execute_min_hp_never_heals() -> void:
	var source = _make_unit(&"mage_source", &"player")
	var target = _make_unit(&"wounded_target", &"hostile")
	target.current_hp = 1
	var outcome = {
		"resolved_damage": 0,
		"min_hp_after_damage": 1,
	}
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var result = resolver.apply_direct_damage_to_target(target, outcome, source)
	_assert_eq(target.current_hp, 1, "min_hp_after_damage=1 with 0 damage should not heal.")
	_assert_eq(int(result.get("damage", -1)), 0, "0 resolved damage should yield 0 hp_damage.")


func _make_execute_effect() -> CombatEffectDef:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"execute"
	effect.save_dc_mode = &"fixed"
	effect.save_dc = 10
	effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER
	effect.save_tag = &"magic"
	effect.params = {
		"skill_id": "mage_power_word_kill",
		"staged_execution": true,
		"burst_damage": 9999,
		"finisher_damage": 1,
		"shield_absorption_percent": 50.0,
		"min_hp_after_damage": 1,
		"boss_non_lethal_damage_max_hp_ratio_percent": 12,
		"boss_non_lethal_damage_floor": 25,
	}
	return effect


func _make_unit(unit_id: StringName, faction_id: StringName) -> BattleUnitState:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.control_mode = &"manual"
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_ap = 2
	unit.current_stamina = 20
	unit.is_alive = true
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 30)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS, 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 10)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, 10)
	return unit


func _first_damage_event(result: Dictionary) -> Dictionary:
	var events = result.get("damage_events", [])
	if events is Array and not (events as Array).is_empty() and (events as Array)[0] is Dictionary:
		return (events as Array)[0]
	return {}


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	_test.assert_eq(actual, expected, message)
