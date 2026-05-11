extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleFateAttackRules = preload("res://scripts/systems/battle/fate/battle_fate_attack_rules.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_lock_crit_requires_string_param_key()
	_test_lock_dodge_bonus_requires_string_param_key()
	_test_damage_bool_helper_requires_string_param_key()
	_test_mitigation_tier_requires_string_param_key()
	_test_outgoing_damage_multiplier_requires_string_param_key()

	if _failures.is_empty():
		print("Battle rule status param schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle rule status param schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_lock_crit_requires_string_param_key() -> void:
	var rules := BattleFateAttackRules.new()

	var legacy_unit := _build_unit(&"legacy_lock_crit")
	_set_status_params(legacy_unit, &"legacy_lock_crit", {
		&"lock_crit": true,
	})
	_assert_false(
		rules.is_attack_crit_locked(legacy_unit),
		"StringName-only lock_crit params must not lock crit."
	)

	var formal_unit := _build_unit(&"formal_lock_crit")
	_set_status_params(formal_unit, &"formal_lock_crit", {
		"lock_crit": true,
	})
	_assert_true(
		rules.is_attack_crit_locked(formal_unit),
		"String key lock_crit params must still lock crit."
	)


func _test_lock_dodge_bonus_requires_string_param_key() -> void:
	var resolver := BattleHitResolver.new()
	var attacker := _build_unit(&"hit_attacker")
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)

	var legacy_target := _build_unit(&"legacy_lock_dodge_bonus")
	_set_ac_profile(legacy_target, 15, 4)
	_set_status_params(legacy_target, &"legacy_lock_dodge_bonus", {
		&"lock_dodge_bonus": true,
	})
	var legacy_check := resolver.build_skill_attack_check(attacker, legacy_target, null)
	_assert_eq(
		int(legacy_check.get("target_armor_class", -1)),
		15,
		"StringName-only lock_dodge_bonus params must not change target AC."
	)

	var formal_target := _build_unit(&"formal_lock_dodge_bonus")
	_set_ac_profile(formal_target, 15, 4)
	_set_status_params(formal_target, &"formal_lock_dodge_bonus", {
		"lock_dodge_bonus": true,
	})
	var formal_check := resolver.build_skill_attack_check(attacker, formal_target, null)
	_assert_eq(
		int(formal_check.get("target_armor_class", -1)),
		11,
		"String key lock_dodge_bonus params must still remove the dodge AC component."
	)


func _test_damage_bool_helper_requires_string_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_unit := _build_unit(&"legacy_damage_bool_param")
	_set_status_params(legacy_unit, &"legacy_damage_bool_param", {
		&"lock_crit": true,
	})
	_assert_false(
		resolver._unit_has_status_bool_param(legacy_unit, &"lock_crit"),
		"BattleDamageResolver bool helper must reject StringName-only params."
	)

	var formal_unit := _build_unit(&"formal_damage_bool_param")
	_set_status_params(formal_unit, &"formal_damage_bool_param", {
		"lock_crit": true,
	})
	_assert_true(
		resolver._unit_has_status_bool_param(formal_unit, &"lock_crit"),
		"BattleDamageResolver bool helper must still accept String-key params."
	)


func _test_mitigation_tier_requires_string_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_source := _build_unit(&"legacy_mitigation_source")
	var legacy_target := _build_unit(&"legacy_mitigation_target")
	_set_status_params(legacy_target, &"legacy_half_mitigation", {
		&"mitigation_tier": "half",
	})
	var legacy_result := resolver.resolve_effects(legacy_source, legacy_target, [_build_damage_effect(20)])
	_assert_eq(
		int(legacy_result.get("damage", -1)),
		20,
		"StringName-only mitigation_tier params must not reduce damage."
	)

	var formal_source := _build_unit(&"formal_mitigation_source")
	var formal_target := _build_unit(&"formal_mitigation_target")
	_set_status_params(formal_target, &"formal_half_mitigation", {
		"mitigation_tier": "half",
	})
	var formal_result := resolver.resolve_effects(formal_source, formal_target, [_build_damage_effect(20)])
	_assert_eq(
		int(formal_result.get("damage", -1)),
		10,
		"String key mitigation_tier params must still reduce damage."
	)
	var formal_event := _first_damage_event(formal_result)
	_assert_eq(
		formal_event.get("mitigation_tier", &""),
		&"half",
		"String key mitigation_tier params must still be reported on the damage event."
	)


func _test_outgoing_damage_multiplier_requires_string_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_source := _build_unit(&"legacy_outgoing_multiplier_source")
	var legacy_target := _build_unit(&"legacy_outgoing_multiplier_target")
	_set_status_params(legacy_source, &"legacy_outgoing_multiplier", {
		&"outgoing_damage_multiplier": 0.5,
	})
	var legacy_result := resolver.resolve_effects(legacy_source, legacy_target, [_build_damage_effect(20)])
	_assert_eq(
		int(legacy_result.get("damage", -1)),
		20,
		"StringName-only outgoing_damage_multiplier params must not scale damage."
	)

	var formal_source := _build_unit(&"formal_outgoing_multiplier_source")
	var formal_target := _build_unit(&"formal_outgoing_multiplier_target")
	_set_status_params(formal_source, &"formal_outgoing_multiplier", {
		"outgoing_damage_multiplier": 0.5,
	})
	var formal_result := resolver.resolve_effects(formal_source, formal_target, [_build_damage_effect(20)])
	_assert_eq(
		int(formal_result.get("damage", -1)),
		10,
		"String key outgoing_damage_multiplier params must still scale damage."
	)
	var formal_event := _first_damage_event(formal_result)
	_assert_eq(
		float(formal_event.get("offense_multiplier", 0.0)),
		0.5,
		"String key outgoing_damage_multiplier params must still be reported in offense_multiplier."
	)


func _set_status_params(unit: BattleUnitState, status_id: StringName, params: Dictionary) -> void:
	var status_effect := BattleStatusEffectState.new()
	status_effect.status_id = status_id
	status_effect.power = 1
	status_effect.stacks = 1
	status_effect.params = params.duplicate(true)
	unit.set_status_effect(status_effect)


func _set_ac_profile(unit: BattleUnitState, armor_class: int, dodge_bonus: int) -> void:
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, armor_class)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, dodge_bonus)


func _build_damage_effect(power: int) -> CombatEffectDef:
	var effect_def := CombatEffectDef.new()
	effect_def.effect_type = &"damage"
	effect_def.power = power
	effect_def.damage_tag = &"physical_slash"
	effect_def.params = {}
	return effect_def


func _first_damage_event(result: Dictionary) -> Dictionary:
	var damage_events = result.get("damage_events", [])
	if damage_events is Array and not (damage_events as Array).is_empty() and damage_events[0] is Dictionary:
		return damage_events[0] as Dictionary
	return {}


func _build_unit(unit_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = 2
	unit.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
	unit.current_hp = 100
	unit.current_mp = 4
	unit.current_stamina = 4
	unit.current_aura = 0
	unit.is_alive = true
	unit.set_anchor_coord(Vector2i.ZERO)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 100)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 4)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 4)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.DODGE_BONUS, 0)
	return unit


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_false(condition: bool, message: String) -> void:
	_assert_true(not condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
