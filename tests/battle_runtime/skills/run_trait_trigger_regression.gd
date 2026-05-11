extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_STATUS_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const TRAIT_TRIGGER_HOOKS_SCRIPT = preload("res://scripts/systems/battle/runtime/trait_trigger_hooks.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_halfling_luck_rerolls_natural_one_attack()
	_test_savage_attacks_adds_one_weapon_die_on_melee_crit()
	_test_relentless_endurance_precedes_death_ward()
	_test_turn_start_refreshes_halfling_luck()

	if _failures.is_empty():
		print("Trait trigger regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Trait trigger regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_halfling_luck_rerolls_natural_one_attack() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := _build_unit(&"halfling_attacker", &"player", 20)
	source.race_trait_ids = [TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_HALFLING_LUCK]
	var target := _build_unit(&"halfling_target", &"enemy", 20)
	var effect := _make_damage_effect(5, false)
	var result: Dictionary = resolver.resolve_attack_effects(
		source,
		target,
		[effect],
		{
			"required_roll": 99,
			"display_required_roll": 20,
			"hit_rate_percent": 5,
		},
		{"attack_roll_overrides": [1, 20]}
	)
	_assert_true(bool(result.get("attack_success", false)), "halfling_luck reroll should turn a natural 1 into the overridden natural 20 success.")
	_assert_eq(int(result.get("hit_roll", 0)), 20, "halfling_luck should expose the rerolled hit_roll.")
	_assert_eq(int(source.per_turn_charges.get(&"trait_halfling_luck", -1)), 0, "halfling_luck should consume its per-turn charge.")
	_assert_has_trait_result(result, TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_HALFLING_LUCK, "attack result should record halfling_luck.")


func _test_savage_attacks_adds_one_weapon_die_on_melee_crit() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := _build_unit(&"savage_attacker", &"player", 20)
	source.race_trait_ids = [TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_SAVAGE_ATTACKS]
	source.set_unarmed_weapon_projection(&"physical_slash", {"dice_count": 1, "dice_sides": 6, "flat_bonus": 0}, 1)
	var target := _build_unit(&"savage_target", &"enemy", 100)
	var effect := _make_damage_effect(0, true)
	var result: Dictionary = resolver.resolve_effects(source, target, [effect], {"critical_hit": true})
	var event: Dictionary = _first_damage_event(result)
	_assert_eq(int(event.get("trait_extra_weapon_damage_dice_count", 0)), 1, "savage_attacks should add exactly one extra weapon die on melee crit.")
	_assert_eq(int(event.get("trait_extra_weapon_damage_dice_sides", 0)), 6, "savage_attacks should reuse the current melee weapon die size.")
	_assert_has_trait_result(event, TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_SAVAGE_ATTACKS, "damage event should record savage_attacks.")


func _test_relentless_endurance_precedes_death_ward() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var source := _build_unit(&"fatal_source", &"enemy", 20)
	var target := _build_unit(&"relentless_target", &"player", 8)
	target.race_trait_ids = [TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_RELENTLESS_ENDURANCE]
	_set_status(target, &"death_ward")
	var effect := _make_damage_effect(99, false)
	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	_assert_eq(target.current_hp, 1, "relentless_endurance should clamp fatal damage to 1 HP.")
	_assert_true(target.is_alive, "relentless_endurance should keep the target alive.")
	_assert_true(target.has_status_effect(&"death_ward"), "relentless_endurance should trigger before death_ward consumption.")
	_assert_has_trait_result(_first_damage_event(result), TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_RELENTLESS_ENDURANCE, "fatal damage event should record relentless_endurance.")

	var second_result: Dictionary = resolver.resolve_effects(source, target, [effect])
	_assert_eq(target.current_hp, 0, "relentless_endurance should not trigger a second time in the same battle.")
	_assert_true(not target.is_alive, "relentless_endurance spent charge should allow the next fatal damage to kill.")
	_assert_true(_first_damage_event(second_result).get("trait_trigger_results", []).is_empty(), "second fatal damage should not record a spent relentless_endurance.")


func _test_turn_start_refreshes_halfling_luck() -> void:
	var hooks = TRAIT_TRIGGER_HOOKS_SCRIPT.new()
	var unit := _build_unit(&"turn_halfling", &"player", 20)
	unit.race_trait_ids = [TRAIT_TRIGGER_HOOKS_SCRIPT.TRAIT_HALFLING_LUCK]
	hooks.on_battle_start(unit)
	var first_result: Dictionary = hooks.on_natural_one(unit, {"roll": 1, "die_size": 20})
	_assert_true(bool(first_result.get("triggered", false)), "halfling_luck should trigger after battle start initialization.")
	_assert_eq(int(unit.per_turn_charges.get(&"trait_halfling_luck", -1)), 0, "halfling_luck charge should be spent after use.")
	unit.reset_per_turn_charges()
	hooks.on_turn_start(unit)
	_assert_eq(int(unit.per_turn_charges.get(&"trait_halfling_luck", -1)), 1, "turn start should refresh halfling_luck.")


func _build_unit(unit_id: StringName, faction_id: StringName, hp: int) -> BATTLE_UNIT_STATE_SCRIPT:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.coord = Vector2i.ZERO
	unit.current_hp = hp
	unit.current_ap = 1
	unit.body_size = 1
	unit.is_alive = hp > 0
	unit.refresh_footprint()
	return unit


func _make_damage_effect(power: int, add_weapon_dice: bool) -> COMBAT_EFFECT_DEF_SCRIPT:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.power = power
	effect.damage_tag = &"physical_slash"
	if add_weapon_dice:
		effect.params = {"add_weapon_dice": true}
	return effect


func _set_status(unit: BATTLE_UNIT_STATE_SCRIPT, status_id: StringName) -> void:
	var status = BATTLE_STATUS_EFFECT_STATE_SCRIPT.new()
	status.status_id = status_id
	status.source_unit_id = &""
	status.params = {}
	unit.set_status_effect(status)


func _first_damage_event(result: Dictionary) -> Dictionary:
	var events: Array = result.get("damage_events", [])
	if events.is_empty() or not (events[0] is Dictionary):
		return {}
	var event: Dictionary = events[0]
	return event


func _assert_has_trait_result(result: Dictionary, trait_id: StringName, message: String) -> void:
	var trigger_results: Array = result.get("trait_trigger_results", [])
	for trigger_result_variant in trigger_results:
		if not (trigger_result_variant is Dictionary):
			continue
		var trigger_result: Dictionary = trigger_result_variant
		if StringName(trigger_result.get("trait_id", &"")) == trait_id:
			return
	_test.fail(message)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s expected=%s actual=%s" % [message, str(expected), str(actual)])
