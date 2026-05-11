extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const BATTLE_DAMAGE_RESOLVER_SCRIPT = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const EQUIPMENT_DURABILITY_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_durability_rules.gd")
const EQUIPMENT_INSTANCE_STATE_SCRIPT = preload("res://scripts/player/warehouse/equipment_instance_state.gd")
const EQUIPMENT_STATE_SCRIPT = preload("res://scripts/player/equipment/equipment_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_disjunction_failure_destroys_common_equipment_after_two_hits()
	_test_disjunction_reversed_effect_order_uses_attack_success_requirement()
	_test_disjunction_success_leaves_durability_unchanged()
	_test_disjunction_rarity_bonus_can_pass_save()
	if _failures.is_empty():
		print("Spell disjunction equipment durability regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Spell disjunction equipment durability regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_disjunction_failure_destroys_common_equipment_after_two_hits() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var caster = _build_unit(&"caster", &"player")
	var target = _build_unit(&"target", &"enemy")
	_equip_instance(
		target,
		&"main_hand",
		&"bronze_sword",
		&"eq_common_sword",
		EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON,
		EQUIPMENT_DURABILITY_RULES_SCRIPT.get_default_current_durability(EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON)
	)

	var first_result: Dictionary = resolver.resolve_effects(
		caster,
		target,
		[_fixed_damage_effect(1), _disjunction_effect(28)],
		{"save_roll_override": 1, "equipment_slot_override": &"main_hand"}
	)
	var first_instance = target.get_equipment_view().get_equipped_instance(&"main_hand")
	_assert_eq(int(first_result.get("damage", 0)), 1, "第一次裂解测试应使用固定伤害。")
	_assert_true(first_instance != null, "第一次失败后普通装备应仍在装备栏。")
	if first_instance != null:
		_assert_eq(int(first_instance.current_durability), 28, "第一次失败应扣除 28 点耐久。")

	var second_result: Dictionary = resolver.resolve_effects(
		caster,
		target,
		[_fixed_damage_effect(1), _disjunction_effect(28)],
		{"save_roll_override": 1, "equipment_slot_override": &"main_hand"}
	)
	var events: Array = second_result.get("equipment_durability_events", [])
	_assert_eq(target.get_equipment_view().get_equipped_item_id(&"main_hand"), &"", "第二次失败后 0 耐久装备应直接从装备栏消失。")
	_assert_true(not events.is_empty(), "第二次失败应记录装备耐久事件。")
	if not events.is_empty():
		_assert_true(bool((events[0] as Dictionary).get("destroyed", false)), "第二次失败的装备耐久事件应标记 destroyed。")


func _test_disjunction_reversed_effect_order_uses_attack_success_requirement() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var caster = _build_unit(&"caster_reversed", &"player")
	var target = _build_unit(&"target_reversed", &"enemy")
	_equip_instance(
		target,
		&"main_hand",
		&"bronze_sword",
		&"eq_reversed_sword",
		EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON,
		EQUIPMENT_DURABILITY_RULES_SCRIPT.get_default_current_durability(EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON)
	)

	var result: Dictionary = resolver.resolve_effects(
		caster,
		target,
		[_disjunction_effect(28), _fixed_damage_effect(1)],
		{"attack_success": true, "save_roll_override": 1, "equipment_slot_override": &"main_hand"}
	)
	var equipped_instance = target.get_equipment_view().get_equipped_instance(&"main_hand")
	var events: Array = result.get("equipment_durability_events", [])
	_assert_eq(int(result.get("damage", 0)), 1, "反向效果顺序仍应结算固定伤害。")
	_assert_true(not events.is_empty(), "命中元数据存在时，裂解效果排在伤害前也应记录装备耐久事件。")
	_assert_true(equipped_instance != null, "反向效果顺序失败后普通装备应仍在装备栏。")
	if equipped_instance != null:
		_assert_eq(int(equipped_instance.current_durability), 28, "反向效果顺序失败应扣除 28 点耐久。")


func _test_disjunction_success_leaves_durability_unchanged() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var caster = _build_unit(&"caster_success", &"player")
	var target = _build_unit(&"target_success", &"enemy")
	_equip_instance(
		target,
		&"main_hand",
		&"bronze_sword",
		&"eq_saved_sword",
		EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.COMMON,
		56
	)

	var result: Dictionary = resolver.resolve_effects(
		caster,
		target,
		[_fixed_damage_effect(1), _disjunction_effect(28)],
		{"save_roll_override": 20, "equipment_slot_override": &"main_hand"}
	)
	var equipped_instance = target.get_equipment_view().get_equipped_instance(&"main_hand")
	_assert_true(equipped_instance != null, "豁免成功后装备应保留。")
	if equipped_instance != null:
		_assert_eq(int(equipped_instance.current_durability), 56, "豁免成功不应扣除耐久。")
	var events: Array = result.get("equipment_durability_events", [])
	_assert_true(not events.is_empty(), "豁免成功也应记录裂解判定事件。")
	if not events.is_empty():
		var save_result: Dictionary = (events[0] as Dictionary).get("save_result", {})
		_assert_true(bool(save_result.get("success", false)), "自然 20 的装备裂解豁免应成功。")


func _test_disjunction_rarity_bonus_can_pass_save() -> void:
	var resolver = BATTLE_DAMAGE_RESOLVER_SCRIPT.new()
	var caster = _build_unit(&"caster_rare", &"player")
	var target = _build_unit(&"target_rare", &"enemy")
	_equip_instance(
		target,
		&"main_hand",
		&"bronze_sword",
		&"eq_rare_sword",
		EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.RARE,
		EQUIPMENT_DURABILITY_RULES_SCRIPT.get_default_current_durability(EQUIPMENT_INSTANCE_STATE_SCRIPT.RarityTier.RARE)
	)

	var result: Dictionary = resolver.resolve_effects(
		caster,
		target,
		[_fixed_damage_effect(1), _disjunction_effect(28)],
		{"save_roll_override": 11, "equipment_slot_override": &"main_hand"}
	)
	var events: Array = result.get("equipment_durability_events", [])
	_assert_true(not events.is_empty(), "稀有度加值豁免应记录裂解事件。")
	if events.is_empty():
		return
	var event := events[0] as Dictionary
	var save_result: Dictionary = event.get("save_result", {})
	_assert_eq(int(save_result.get("equipment_rarity_bonus", 0)), 4, "rare 装备应提供 +4 裂解豁免加值。")
	_assert_eq(int(save_result.get("roll_total", 0)), 15, "rare +4 应把 11 点 d20 结果推到 DC 15。")
	_assert_true(bool(save_result.get("success", false)), "稀有度加值达到 DC 时应完全免除耐久损失。")
	var equipped_instance = target.get_equipment_view().get_equipped_instance(&"main_hand")
	_assert_true(equipped_instance != null, "稀有度加值成功后装备应保留。")
	if equipped_instance != null:
		_assert_eq(int(equipped_instance.current_durability), 120, "稀有度加值成功后耐久应保持满值。")


func _fixed_damage_effect(power: int) -> Resource:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"damage"
	effect.power = maxi(power, 0)
	effect.damage_tag = &"magic"
	return effect


func _disjunction_effect(power: int) -> Resource:
	var effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect.effect_type = &"equipment_durability_damage"
	effect.power = maxi(power, 1)
	effect.effect_target_team_filter = &"enemy"
	effect.save_dc_mode = &"caster_spell"
	effect.save_ability = &"willpower"
	effect.save_dc_source_ability = &"intelligence"
	effect.save_tag = &"equipment_disjunction"
	effect.params = {
		"max_damaged_items": 1,
		"require_damage_applied": true,
		"slot_weight_map": {"main_hand": 1},
		"target_slots": [&"main_hand"],
	}
	return effect


func _equip_instance(
	unit_state,
	slot_id: StringName,
	item_id: StringName,
	instance_id: StringName,
	rarity: int,
	current_durability: int
) -> void:
	var equipment_state = EQUIPMENT_STATE_SCRIPT.new()
	var instance = EQUIPMENT_INSTANCE_STATE_SCRIPT.create(item_id, instance_id)
	instance.rarity = rarity
	instance.current_durability = current_durability
	var equipped := equipment_state.set_equipped_entry(slot_id, item_id, [slot_id], instance)
	_assert_true(equipped, "测试装备实例应能写入装备栏。")
	unit_state.set_equipment_view(equipment_state)


func _build_unit(unit_id: StringName, faction_id: StringName):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = faction_id
	unit.is_alive = true
	unit.current_hp = 30
	unit.current_mp = 0
	unit.current_stamina = 0
	unit.current_aura = 0
	unit.current_ap = 1
	unit.attribute_snapshot = ATTRIBUTE_SNAPSHOT_SCRIPT.new()
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX, 30)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.MP_MAX, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_MAX, 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.AURA_MAX, 0)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE, 18)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SNAPSHOT_SCRIPT.get_base_attribute_modifier_id(UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE), 4)
	unit.attribute_snapshot.set_value(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SNAPSHOT_SCRIPT.get_base_attribute_modifier_id(UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER), 0)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.SPELL_PROFICIENCY_BONUS, 3)
	return unit


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
