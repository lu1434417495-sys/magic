extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_damage_resistance_halves_matching_damage_tag()
	_test_damage_resistance_cancels_with_status_vulnerability()
	_test_damage_resistance_immune_keeps_highest_priority()
	if _failures.is_empty():
		print("Damage resistance regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Damage resistance regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_damage_resistance_halves_matching_damage_tag() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"resistance_source", &"enemy")
	var target = _make_unit(&"fire_resistant_target", &"player")
	target.damage_resistances[&"fire"] = &"half"
	var result: Dictionary = resolver.resolve_effects(source, target, [_make_damage_effect(&"fire", 10)])

	_assert_eq(int(result.get("damage", -1)), 5, "damage_resistances fire=half should halve fire damage.")
	var event := _first_damage_event(result)
	_assert_eq(String(event.get("mitigation_tier", "")), "half", "damage event should record half mitigation tier.")
	_assert_true(
		_sources_include(event.get("mitigation_sources", []), "damage_resistance_fire", "damage_resistance", "half"),
		"damage event should record damage_resistance source."
	)


func _test_damage_resistance_cancels_with_status_vulnerability() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"cancel_source", &"enemy")
	var target = _make_unit(&"cancel_target", &"player")
	target.damage_resistances[&"fire"] = &"half"
	_set_status(target, &"fire_vulnerability", {
		"damage_tag": &"fire",
		"mitigation_tier": &"double",
	})

	var result: Dictionary = resolver.resolve_effects(source, target, [_make_damage_effect(&"fire", 10)])
	_assert_eq(int(result.get("damage", -1)), 10, "damage_resistance half should cancel matching double status.")
	var event := _first_damage_event(result)
	_assert_eq(String(event.get("mitigation_tier", "")), "normal", "canceled half/double should record normal tier.")
	_assert_true(
		_sources_include(event.get("mitigation_sources", []), "damage_resistance_fire", "damage_resistance", "half"),
		"canceled sources should still include damage_resistance source."
	)
	_assert_true(
		_sources_include(event.get("mitigation_sources", []), "fire_vulnerability", "mitigation_tier", "double"),
		"canceled sources should still include status vulnerability source."
	)


func _test_damage_resistance_immune_keeps_highest_priority() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"immune_source", &"enemy")
	var target = _make_unit(&"immune_target", &"player")
	target.damage_resistances[&"negative_energy"] = &"immune"
	_set_status(target, &"negative_vulnerability", {
		"damage_tag": &"negative_energy",
		"mitigation_tier": &"double",
	})

	var result: Dictionary = resolver.resolve_effects(source, target, [_make_damage_effect(&"negative_energy", 10)])
	_assert_eq(int(result.get("damage", -1)), 0, "immune damage_resistance should override matching double status.")
	var event := _first_damage_event(result)
	_assert_eq(String(event.get("mitigation_tier", "")), "immune", "immune resistance should record immune tier.")
	_assert_true(
		_sources_include(event.get("mitigation_sources", []), "damage_resistance_negative_energy", "damage_resistance", "immune"),
		"immune source should come from damage_resistances."
	)


func _make_damage_effect(damage_tag: StringName, power: int):
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.damage_tag = damage_tag
	effect.power = power
	return effect


func _make_unit(unit_id: StringName, faction_id: StringName):
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
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 0)
	return unit


func _set_status(unit, status_id: StringName, params: Dictionary) -> void:
	var status = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status.status_id = status_id
	status.source_unit_id = unit.unit_id
	status.power = 1
	status.stacks = 1
	status.duration = -1
	status.params = params.duplicate(true)
	unit.set_status_effect(status)


func _first_damage_event(result: Dictionary) -> Dictionary:
	var events = result.get("damage_events", [])
	if events is Array and not (events as Array).is_empty() and (events as Array)[0] is Dictionary:
		return (events as Array)[0]
	return {}


func _sources_include(sources, status_id: String, source_type: String, tier: String) -> bool:
	if sources is not Array:
		return false
	for source_variant in sources:
		if source_variant is not Dictionary:
			continue
		var source := source_variant as Dictionary
		if String(source.get("status_id", "")) == status_id \
				and String(source.get("type", "")) == source_type \
				and String(source.get("tier", "")) == tier:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
