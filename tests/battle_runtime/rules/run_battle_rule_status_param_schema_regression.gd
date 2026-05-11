extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleFateAttackRules = preload("res://scripts/systems/battle/fate/battle_fate_attack_rules.gd")
const BattleHitResolver = preload("res://scripts/systems/battle/rules/battle_hit_resolver.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_lock_crit_accepts_string_name_param_key()
	_test_lock_dodge_bonus_accepts_string_name_param_key()
	_test_blind_attack_penalty_uses_status_semantic_and_param_override()
	_test_damage_bool_helper_accepts_string_name_param_key()
	_test_mitigation_tier_accepts_string_name_param_key()
	_test_outgoing_damage_multiplier_accepts_string_name_param_key()

	if _failures.is_empty():
		print("Battle rule status param schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle rule status param schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_lock_crit_accepts_string_name_param_key() -> void:
	var rules := BattleFateAttackRules.new()

	var legacy_unit := _build_unit(&"legacy_lock_crit")
	_set_status_params(legacy_unit, &"legacy_lock_crit", {
		&"lock_crit": true,
	})
	_assert_true(
		rules.is_attack_crit_locked(legacy_unit),
		"StringName-only lock_crit params should lock crit under current status param handling."
	)

	var formal_unit := _build_unit(&"formal_lock_crit")
	_set_status_params(formal_unit, &"formal_lock_crit", {
		"lock_crit": true,
	})
	_assert_true(
		rules.is_attack_crit_locked(formal_unit),
		"String key lock_crit params must still lock crit."
	)


func _test_lock_dodge_bonus_accepts_string_name_param_key() -> void:
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
		11,
		"StringName-only lock_dodge_bonus params should remove the dodge AC component."
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


func _test_blind_attack_penalty_uses_status_semantic_and_param_override() -> void:
	var resolver := BattleHitResolver.new()
	var target := _build_unit(&"blind_penalty_target")
	_set_ac_profile(target, 15, 0)

	var clear_attacker := _build_unit(&"clear_blind_penalty_attacker")
	var clear_check := resolver.build_skill_attack_check(clear_attacker, target, null)

	var default_blind_attacker := _build_unit(&"default_blind_penalty_attacker")
	_set_status_params(default_blind_attacker, &"blind", {})
	var default_check := resolver.build_skill_attack_check(default_blind_attacker, target, null)
	_assert_eq(
		int(default_check.get("situational_attack_penalty", -1)),
		4,
		"blind 默认应让攻击检定承受 -4 等价惩罚。"
	)
	_assert_eq(
		int(default_check.get("required_roll", -1)),
		int(clear_check.get("required_roll", 0)) + 4,
		"blind 攻击惩罚应提高命中所需 d20 点数。"
	)

	var severe_blind_attacker := _build_unit(&"severe_blind_penalty_attacker")
	_set_status_params(severe_blind_attacker, &"blind", {
		"attack_roll_penalty": 6,
	})
	var severe_check := resolver.build_skill_attack_check(severe_blind_attacker, target, null)
	_assert_eq(
		int(severe_check.get("situational_attack_penalty", -1)),
		6,
		"blind 的 attack_roll_penalty 参数应能覆盖默认攻击惩罚。"
	)


func _test_damage_bool_helper_accepts_string_name_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_unit := _build_unit(&"legacy_damage_bool_param")
	_set_status_params(legacy_unit, &"legacy_damage_bool_param", {
		&"lock_crit": true,
	})
	_assert_true(
		resolver._unit_has_status_bool_param(legacy_unit, &"lock_crit"),
		"BattleDamageResolver bool helper should accept StringName-only params."
	)

	var formal_unit := _build_unit(&"formal_damage_bool_param")
	_set_status_params(formal_unit, &"formal_damage_bool_param", {
		"lock_crit": true,
	})
	_assert_true(
		resolver._unit_has_status_bool_param(formal_unit, &"lock_crit"),
		"BattleDamageResolver bool helper must still accept String-key params."
	)


func _test_mitigation_tier_accepts_string_name_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_source := _build_unit(&"legacy_mitigation_source")
	var legacy_target := _build_unit(&"legacy_mitigation_target")
	_set_status_params(legacy_target, &"legacy_half_mitigation", {
		&"mitigation_tier": "half",
	})
	var legacy_result := resolver.resolve_effects(legacy_source, legacy_target, [_build_damage_effect(20)])
	_assert_eq(
		int(legacy_result.get("damage", -1)),
		10,
		"StringName-only mitigation_tier params should reduce damage."
	)
	var legacy_event := _first_damage_event(legacy_result)
	_assert_eq(
		legacy_event.get("mitigation_tier", &""),
		&"half",
		"StringName-only mitigation_tier params should be reported on the damage event."
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


func _test_outgoing_damage_multiplier_accepts_string_name_param_key() -> void:
	var resolver := BattleDamageResolver.new()

	var legacy_source := _build_unit(&"legacy_outgoing_multiplier_source")
	var legacy_target := _build_unit(&"legacy_outgoing_multiplier_target")
	_set_status_params(legacy_source, &"legacy_outgoing_multiplier", {
		&"outgoing_damage_multiplier": 0.5,
	})
	var legacy_result := resolver.resolve_effects(legacy_source, legacy_target, [_build_damage_effect(20)])
	_assert_eq(
		int(legacy_result.get("damage", -1)),
		10,
		"StringName-only outgoing_damage_multiplier params should scale damage."
	)
	var legacy_event := _first_damage_event(legacy_result)
	_assert_eq(
		float(legacy_event.get("offense_multiplier", 0.0)),
		0.5,
		"StringName-only outgoing_damage_multiplier params should be reported in offense_multiplier."
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
		_test.fail(message)


func _assert_false(condition: bool, message: String) -> void:
	_assert_true(not condition, message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
