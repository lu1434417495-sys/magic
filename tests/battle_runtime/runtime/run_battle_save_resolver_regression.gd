extends SceneTree

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_SAVE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_save_resolver_handles_immunity_before_roll()
	_test_save_resolver_handles_advantage_and_disadvantage()
	_test_save_resolver_forces_natural_one_and_twenty()
	_test_caster_spell_save_dc_uses_source_ability_and_spell_proficiency()
	_test_damage_save_success_halves_partial_damage()
	_test_status_save_success_blocks_and_failure_applies_status()
	if _failures.is_empty():
		print("Battle save resolver regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle save resolver regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_save_resolver_handles_immunity_before_roll() -> void:
	var target = _make_unit(&"poison_immune_target", &"player")
	target.save_advantage_tags.append(&"poison_immunity")
	var result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(
		null,
		target,
		_make_save_damage_effect(&"poison", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 12, false),
		{"save_roll_override": 1}
	)
	_assert_true(bool(result.get("immune", false)), "poison_immunity tag should make the save immune before rolling.")
	_assert_true(bool(result.get("success", false)), "immune save should count as success.")
	_assert_eq(int(result.get("natural_roll", -1)), 0, "immune save should not roll.")


func _test_save_resolver_handles_advantage_and_disadvantage() -> void:
	var advantage_target = _make_unit(&"advantage_target", &"player")
	advantage_target.save_advantage_tags.append(&"poison")
	var advantage_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(
		null,
		advantage_target,
		_make_save_damage_effect(&"poison", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 15, false),
		{"save_roll_overrides": [2, 18]}
	)
	_assert_eq(int(advantage_result.get("natural_roll", -1)), 18, "direct save tag should grant advantage.")
	_assert_true(bool(advantage_result.get("success", false)), "advantage should use the higher override roll.")

	var disadvantage_target = _make_unit(&"disadvantage_target", &"player")
	disadvantage_target.save_advantage_tags.append(&"poison_disadvantage")
	var disadvantage_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(
		null,
		disadvantage_target,
		_make_save_damage_effect(&"poison", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 15, false),
		{"save_roll_overrides": [18, 2]}
	)
	_assert_eq(int(disadvantage_result.get("natural_roll", -1)), 2, "save_tag_disadvantage should use the lower roll.")
	_assert_true(not bool(disadvantage_result.get("success", true)), "disadvantage should fail with the lower override roll.")


func _test_save_resolver_forces_natural_one_and_twenty() -> void:
	var target = _make_unit(&"natural_save_target", &"player")
	target.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 30)
	var natural_one_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(
		null,
		target,
		_make_save_damage_effect(&"poison", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 5, false),
		{"save_roll_override": 1}
	)
	_assert_true(not bool(natural_one_result.get("success", true)), "natural 1 should force save failure.")

	target.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 1)
	var natural_twenty_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(
		null,
		target,
		_make_save_damage_effect(&"poison", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 40, false),
		{"save_roll_override": 20}
	)
	_assert_true(bool(natural_twenty_result.get("success", false)), "natural 20 should force save success.")


func _test_caster_spell_save_dc_uses_source_ability_and_spell_proficiency() -> void:
	var source = _make_unit(&"spell_dc_source", &"enemy")
	source.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 18)
	source.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS, 3)
	var target = _make_unit(&"spell_dc_target", &"player")
	target.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY, 10)
	var effect = _make_caster_spell_save_damage_effect()

	var failed_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(source, target, effect, {"save_roll_override": 14})
	_assert_eq(int(failed_result.get("dc", -1)), 15, "caster_spell DC 应为 8 + INT 调整值 4 + 法术熟练 3。")
	_assert_eq(int(failed_result.get("roll_total", -1)), 14, "敏捷 10 目标的豁免总值应只等于 d20。")
	_assert_true(not bool(failed_result.get("success", true)), "低于动态 DC 的敏捷豁免应失败。")

	var success_result: Dictionary = BATTLE_SAVE_RESOLVER_SCRIPT.resolve_save(source, target, effect, {"save_roll_override": 15})
	_assert_true(bool(success_result.get("success", false)), "达到动态 DC 的敏捷豁免应成功。")


func _test_damage_save_success_halves_partial_damage() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"breath_source", &"enemy")
	var target = _make_unit(&"breath_target", &"player")
	var effect = _make_save_damage_effect(&"dragon_breath", UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION, 12, true)
	effect.power = 10
	var result: Dictionary = resolver.resolve_effects(source, target, [effect], {"save_roll_override": 20})

	_assert_eq(int(result.get("damage", -1)), 5, "successful partial damage save should halve resolved damage.")
	var event := _first_damage_event(result)
	_assert_true(bool(event.get("save_success", false)), "damage event should record save success.")
	_assert_true(bool(event.get("save_partial_applied", false)), "damage event should record partial save application.")
	_assert_eq(int(event.get("pre_save_damage", -1)), 10, "damage event should preserve pre-save damage.")
	_assert_eq(int(event.get("save_adjusted_damage", -1)), 5, "damage event should record adjusted save damage.")


func _test_status_save_success_blocks_and_failure_applies_status() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source = _make_unit(&"status_source", &"enemy")
	var success_target = _make_unit(&"status_success_target", &"player")
	var effect = _make_save_status_effect(&"sleep", &"asleep", &"deep_sleep")
	var success_result: Dictionary = resolver.resolve_effects(source, success_target, [effect], {"save_roll_override": 20})
	_assert_true(not success_target.has_status_effect(&"asleep"), "successful save should block default status.")
	_assert_true(not success_target.has_status_effect(&"deep_sleep"), "successful save should block failure status.")
	_assert_true(not bool(success_result.get("applied", true)), "blocked status save should not mark the effect as applied.")

	var failure_target = _make_unit(&"status_failure_target", &"player")
	var failure_result: Dictionary = resolver.resolve_effects(source, failure_target, [effect], {"save_roll_override": 1})
	_assert_true(failure_target.has_status_effect(&"deep_sleep"), "failed save should apply save_failure_status_id when set.")
	_assert_true(not failure_target.has_status_effect(&"asleep"), "save_failure_status_id should replace default status on failure.")
	_assert_true(bool(failure_result.get("applied", false)), "failed status save should mark effect as applied.")
	_assert_true((failure_result.get("status_effect_ids", []) as Array).has(&"deep_sleep"), "result should report applied failure status id.")


func _make_save_damage_effect(save_tag: StringName, save_ability: StringName, save_dc: int, partial: bool):
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.damage_tag = &"fire"
	effect.power = 10
	effect.save_dc = save_dc
	effect.save_ability = save_ability
	effect.save_tag = save_tag
	effect.save_partial_on_success = partial
	return effect


func _make_save_status_effect(save_tag: StringName, status_id: StringName, failure_status_id: StringName):
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"status"
	effect.status_id = status_id
	effect.save_failure_status_id = failure_status_id
	effect.save_dc = 12
	effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER
	effect.save_tag = save_tag
	return effect


func _make_caster_spell_save_damage_effect():
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.damage_tag = &"fire"
	effect.power = 10
	effect.save_dc_mode = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_DC_MODE_CASTER_SPELL
	effect.save_dc_source_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE
	effect.save_ability = UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY
	effect.save_tag = BATTLE_SAVE_RESOLVER_SCRIPT.SAVE_TAG_FIREBALL
	effect.save_partial_on_success = true
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
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
