extends SceneTree

const BattleDamageResolver = preload("res://scripts/systems/battle_damage_resolver.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")


class FixedRollDamageResolver extends BattleDamageResolver:
	var damage_rolls: Array = []
	var attack_rolls: Array = []

	func _init(p_damage_rolls: Array = [], p_attack_rolls: Array = []) -> void:
		damage_rolls = p_damage_rolls.duplicate()
		attack_rolls = p_attack_rolls.duplicate()

	func _roll_damage_die(dice_sides: int) -> int:
		var normalized_sides := maxi(dice_sides, 1)
		if damage_rolls.is_empty():
			return normalized_sides
		return clampi(int(damage_rolls.pop_front()), 1, normalized_sides)

	func _roll_true_random_attack_range(min_value: int, max_value: int, battle_state) -> int:
		var lower := mini(min_value, max_value)
		var upper := maxi(min_value, max_value)
		if battle_state != null:
			battle_state.attack_roll_nonce = maxi(int(battle_state.attack_roll_nonce), 0) + 1
		if attack_rolls.is_empty():
			return upper
		return clampi(int(attack_rolls.pop_front()), lower, upper)


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_add_weapon_dice_explicit_formula()
	_test_physical_damage_does_not_add_weapon_dice_by_default()
	_test_critical_hit_rolls_extra_weapon_and_skill_dice_once()
	_test_each_damage_effect_reads_add_weapon_dice_independently()
	_test_current_two_handed_weapon_dice_is_used()
	_test_warrior_heavy_strike_uses_weapon_plus_skill_dice_template()
	if _failures.is_empty():
		print("Battle weapon dice regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle weapon dice regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_add_weapon_dice_explicit_formula() -> void:
	var resolver := FixedRollDamageResolver.new([2, 3, 6])
	var source := _build_unit(&"weapon_formula_user")
	_apply_weapon(source, 1, 6, 2)
	var target := _build_unit(&"weapon_formula_target")
	var effect := _build_damage_effect(5, true, 2, 4, 3)

	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var event := _first_damage_event(result)
	_assert_eq(int(event.get("base_damage", 0)), 21, "add_weapon_dice 公式应为 weapon dice + skill dice + skill bonus + power。")
	_assert_eq(int(event.get("weapon_damage_dice_total", 0)), 6, "武器骰应加入当前 damage event。")
	_assert_eq(int(event.get("weapon_damage_dice_bonus", 0)), 2, "武器骰 flat_bonus 应只作为普通武器骰静态项加入。")
	_assert_eq(int(event.get("damage_dice_total", 0)), 5, "技能骰应保留为 damage_dice_total。")
	_assert_eq(int(event.get("damage_dice_bonus", 0)), 3, "技能骰 bonus 应加入基础伤害。")
	_assert_eq(int(result.get("damage", 0)), 21, "无减伤时总 HP 伤害应等于 base_damage。")


func _test_physical_damage_does_not_add_weapon_dice_by_default() -> void:
	var resolver := FixedRollDamageResolver.new([4, 6])
	var source := _build_unit(&"physical_default_user")
	_apply_weapon(source, 1, 6, 2)
	var target := _build_unit(&"physical_default_target")
	var effect := _build_damage_effect(5, false, 1, 4, 1, &"physical_slash")

	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var event := _first_damage_event(result)
	_assert_eq(int(event.get("base_damage", 0)), 10, "物理伤害默认不应自动加入武器骰。")
	_assert_eq(int(event.get("weapon_damage_dice_count", 0)), 0, "未显式 add_weapon_dice 时不应掷武器骰。")
	_assert_true(not bool(event.get("add_weapon_dice", false)), "未显式配置时 add_weapon_dice 应为 false。")


func _test_critical_hit_rolls_extra_weapon_and_skill_dice_once() -> void:
	var resolver := FixedRollDamageResolver.new([2, 5, 3, 4], [20])
	var source := _build_unit(&"critical_weapon_user")
	_apply_weapon(source, 1, 6, 2)
	var target := _build_unit(&"critical_weapon_target")
	var effect := _build_damage_effect(7, true, 1, 4, 3)

	var result: Dictionary = resolver.resolve_attack_effects(
		source,
		target,
		[effect],
		{"required_roll": 1, "display_required_roll": 1},
		{}
	)
	var event := _first_damage_event(result)
	_assert_true(bool(result.get("critical_hit", false)), "测试前置：攻击应被判定为暴击。")
	_assert_true(bool(event.get("critical_hit", false)), "damage event 应记录本段来自暴击。")
	_assert_eq(int(event.get("base_damage", 0)), 26, "暴击应额外掷一组 weapon dice 与 skill dice，但不翻倍 power 或 bonus。")
	_assert_eq(int(event.get("critical_extra_damage_dice_total", 0)), 3, "暴击应额外掷一组技能骰。")
	_assert_eq(int(event.get("critical_extra_weapon_damage_dice_total", 0)), 4, "暴击应额外掷一组武器骰。")
	_assert_eq(int(event.get("damage_dice_bonus", 0)), 3, "技能 dice_bonus 不应因暴击重复加入。")
	_assert_eq(int(event.get("weapon_damage_dice_bonus", 0)), 2, "武器 flat_bonus 不应因暴击重复加入。")


func _test_each_damage_effect_reads_add_weapon_dice_independently() -> void:
	var resolver := FixedRollDamageResolver.new([4, 5])
	var source := _build_unit(&"multi_weapon_user")
	_apply_weapon(source, 1, 6, 0)
	var target := _build_unit(&"multi_weapon_target")
	var first_effect := _build_damage_effect(0, true)
	var second_effect := _build_damage_effect(0, true)

	var result: Dictionary = resolver.resolve_effects(source, target, [first_effect, second_effect])
	var events = result.get("damage_events", [])
	_assert_eq(events.size() if events is Array else 0, 2, "多段 damage effect 应各自产生 damage event。")
	if events is Array and events.size() >= 2:
		_assert_eq(int((events[0] as Dictionary).get("weapon_damage_dice_total", 0)), 4, "第一段应独立掷当前武器骰。")
		_assert_eq(int((events[1] as Dictionary).get("weapon_damage_dice_total", 0)), 5, "第二段应再次独立掷当前武器骰。")
		_assert_eq(int((events[0] as Dictionary).get("base_damage", 0)), 4, "第一段 base_damage 应只包含本段武器骰。")
		_assert_eq(int((events[1] as Dictionary).get("base_damage", 0)), 5, "第二段 base_damage 应只包含本段武器骰。")
	_assert_eq(int(result.get("damage", 0)), 9, "多段 add_weapon_dice 应允许重复加入当前武器骰。")


func _test_current_two_handed_weapon_dice_is_used() -> void:
	var resolver := FixedRollDamageResolver.new([3, 4])
	var source := _build_unit(&"two_handed_user")
	source.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "two_handed_test_weapon",
		"weapon_profile_type_id": "greatsword",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 4, "flat_bonus": 0},
		"weapon_two_handed_dice": {"dice_count": 2, "dice_sides": 6, "flat_bonus": 0},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_slash",
	})
	var target := _build_unit(&"two_handed_target")
	var effect := _build_damage_effect(0, true)

	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var event := _first_damage_event(result)
	_assert_eq(int(event.get("weapon_damage_dice_count", 0)), 2, "双手握法应读取 two_handed_dice 的骰子数量。")
	_assert_eq(int(event.get("weapon_damage_dice_sides", 0)), 6, "双手握法应读取 two_handed_dice 的骰面。")
	_assert_eq(int(event.get("base_damage", 0)), 7, "双手武器应按当前握法掷 2D6。")


func _test_warrior_heavy_strike_uses_weapon_plus_skill_dice_template() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_def = registry.get_skill_defs().get(&"warrior_heavy_strike")
	_assert_true(skill_def != null and skill_def.combat_profile != null, "重击技能配置应可加载。")
	if skill_def == null or skill_def.combat_profile == null:
		return
	var expected_sides := [4, 6, 8]
	var damage_effect_index := 0
	for effect_variant in skill_def.combat_profile.effect_defs:
		var effect := effect_variant as CombatEffectDef
		if effect == null or effect.effect_type != &"damage":
			continue
		_assert_true(bool(effect.params.get("add_weapon_dice", false)), "重击每段伤害样板都应显式 add_weapon_dice。")
		_assert_true(bool(effect.params.get("requires_weapon", false)), "重击仍应要求装备武器。")
		_assert_eq(int(effect.params.get("dice_count", 0)), 1, "重击技能骰应保持 1 颗。")
		if damage_effect_index < expected_sides.size():
			_assert_eq(int(effect.params.get("dice_sides", 0)), int(expected_sides[damage_effect_index]), "重击技能骰骰面应按等级样板递进。")
		damage_effect_index += 1
	_assert_eq(damage_effect_index, 3, "重击应保留 0/1/3 级三段技能骰样板。")


func _build_damage_effect(
	power: int,
	add_weapon_dice: bool,
	dice_count: int = 0,
	dice_sides: int = 0,
	dice_bonus: int = 0,
	damage_tag: StringName = &"physical_blunt"
) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = power
	effect.damage_tag = damage_tag
	effect.params = {}
	if add_weapon_dice:
		effect.params["add_weapon_dice"] = true
	if dice_count > 0 and dice_sides > 0:
		effect.params["dice_count"] = dice_count
		effect.params["dice_sides"] = dice_sides
		effect.params["dice_bonus"] = dice_bonus
	return effect


func _build_unit(unit_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_hp = 100
	unit.is_alive = true
	unit.attribute_snapshot.set_value(&"hp_max", 100)
	return unit


func _apply_weapon(unit: BattleUnitState, dice_count: int, dice_sides: int, flat_bonus: int) -> void:
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "weapon_dice_test_weapon",
		"weapon_profile_type_id": "test_weapon",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": dice_count, "dice_sides": dice_sides, "flat_bonus": flat_bonus},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _first_damage_event(result: Dictionary) -> Dictionary:
	var events = result.get("damage_events", [])
	if events is Array and not events.is_empty() and events[0] is Dictionary:
		return events[0] as Dictionary
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
