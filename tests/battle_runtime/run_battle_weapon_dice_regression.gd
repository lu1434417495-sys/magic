extends SceneTree

const BattleDamageResolver = preload("res://scripts/systems/battle/rules/battle_damage_resolver.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CharacterProgressionDelta = preload("res://scripts/systems/progression/character_progression_delta.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")


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


class MasteryGatewayStub:
	extends RefCounted

	var grants: Array[Dictionary] = []
	var skill_used_events := 0

	func record_achievement_event(
		_member_id: StringName,
		event_type: StringName,
		_amount: int = 1,
		_subject_id: StringName = &"",
		_meta: Dictionary = {}
	) -> Array[StringName]:
		if event_type == &"skill_used":
			skill_used_events += 1
		return []

	func grant_battle_mastery(member_id: StringName, skill_id: StringName, amount: int) -> CharacterProgressionDelta:
		grants.append({
			"member_id": member_id,
			"skill_id": skill_id,
			"amount": amount,
		})
		var delta := CharacterProgressionDelta.new()
		delta.member_id = member_id
		delta.mastery_changes.append({
			"skill_id": skill_id,
			"mastery_amount": amount,
		})
		return delta

	func get_member_state(_member_id: StringName):
		return null


var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_add_weapon_dice_explicit_formula()
	_test_physical_damage_does_not_add_weapon_dice_by_default()
	_test_critical_hit_rolls_extra_weapon_and_skill_dice_once()
	_test_each_damage_effect_reads_add_weapon_dice_independently()
	_test_current_two_handed_weapon_dice_is_used()
	_test_versatile_current_grip_selects_active_dice()
	_test_unarmed_and_natural_weapon_dice_feed_add_weapon_dice()
	_test_requires_weapon_gate_accepts_equipped_only()
	_test_natural_weapon_dice_do_not_trigger_skill_mastery()
	_test_dice_event_fields_split_by_dice_group()
	_test_dice_event_fields_stay_false_without_dice_groups()
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
	_assert_true(bool(event.get("damage_dice_high_total_roll", false)), "暴击且存在任意骰组时本段 high-total 事件应为 true。")
	_assert_eq(String(event.get("damage_dice_high_total_roll_reason", "")), "critical_hit", "暴击 high-total reason 应记录 critical_hit。")
	_assert_true(bool(event.get("skill_damage_dice_is_max", false)), "暴击且存在技能骰时本段技能骰事件应为 true。")
	_assert_eq(String(event.get("skill_damage_dice_is_max_reason", "")), "critical_hit", "暴击技能骰 reason 应记录 critical_hit。")
	_assert_true(bool(event.get("weapon_damage_dice_is_max", false)), "暴击且存在武器骰时本段武器骰事件应为 true。")
	_assert_eq(String(event.get("weapon_damage_dice_is_max_reason", "")), "critical_hit", "暴击武器骰 reason 应记录 critical_hit。")
	_assert_true(bool(result.get("damage_dice_high_total_roll", false)), "顶层 high-total 事件应 OR 汇总 damage_events。")
	_assert_true(bool(result.get("skill_damage_dice_is_max", false)), "顶层技能骰事件应 OR 汇总 damage_events。")
	_assert_true(bool(result.get("weapon_damage_dice_is_max", false)), "顶层武器骰事件应 OR 汇总 damage_events。")


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


func _test_versatile_current_grip_selects_active_dice() -> void:
	var resolver := FixedRollDamageResolver.new([5, 2, 4])
	var source := _build_unit(&"versatile_user")
	var target := _build_unit(&"versatile_target")
	var effect := _build_damage_effect(0, true)

	_apply_versatile_weapon(source, false)
	var one_handed_result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var one_handed_event := _first_damage_event(one_handed_result)
	_assert_eq(String(source.weapon_current_grip), "one_handed", "versatile 单手握法应保留当前 grip。")
	_assert_true(not source.weapon_uses_two_hands, "versatile 单手握法不应标记双手。")
	_assert_eq(int(one_handed_event.get("weapon_damage_dice_count", 0)), 1, "versatile 单手应读取 one_handed_dice 数量。")
	_assert_eq(int(one_handed_event.get("weapon_damage_dice_sides", 0)), 8, "versatile 单手应读取 one_handed_dice 骰面。")
	_assert_eq(int(one_handed_event.get("base_damage", 0)), 5, "versatile 单手应只掷 1D8。")

	_apply_versatile_weapon(source, true)
	var two_handed_result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var two_handed_event := _first_damage_event(two_handed_result)
	_assert_eq(String(source.weapon_current_grip), "two_handed", "versatile 双手握法应保留当前 grip。")
	_assert_true(source.weapon_uses_two_hands, "versatile 双手握法应标记双手。")
	_assert_eq(int(two_handed_event.get("weapon_damage_dice_count", 0)), 2, "versatile 双手应读取 two_handed_dice 数量。")
	_assert_eq(int(two_handed_event.get("weapon_damage_dice_sides", 0)), 6, "versatile 双手应读取 two_handed_dice 骰面。")
	_assert_eq(int(two_handed_event.get("base_damage", 0)), 6, "versatile 双手应按 2D6 结算。")


func _test_unarmed_and_natural_weapon_dice_feed_add_weapon_dice() -> void:
	var resolver := FixedRollDamageResolver.new([4, 6])
	var source := _build_unit(&"innate_weapon_user")
	var target := _build_unit(&"innate_weapon_target")
	var effect := _build_damage_effect(0, true)

	source.set_unarmed_weapon_projection()
	var unarmed_result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var unarmed_event := _first_damage_event(unarmed_result)
	_assert_eq(String(source.weapon_profile_kind), "unarmed", "空手攻击应通过 unarmed profile 表达。")
	_assert_eq(int(unarmed_event.get("weapon_damage_dice_count", 0)), 1, "空手 add_weapon_dice 应读取 1 颗武器骰。")
	_assert_eq(int(unarmed_event.get("weapon_damage_dice_sides", 0)), 4, "空手 add_weapon_dice 应读取 1D4。")
	_assert_eq(int(unarmed_event.get("base_damage", 0)), 4, "空手 add_weapon_dice 应使用空手骰。")

	source.set_natural_weapon_projection(&"natural_weapon", &"physical_pierce", 1, {
		"dice_count": 1,
		"dice_sides": 6,
		"flat_bonus": 0,
	})
	var natural_result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var natural_event := _first_damage_event(natural_result)
	_assert_eq(String(source.weapon_profile_kind), "natural", "天生武器应通过 natural profile 表达。")
	_assert_eq(int(natural_event.get("weapon_damage_dice_count", 0)), 1, "天生武器 add_weapon_dice 应读取 1 颗武器骰。")
	_assert_eq(int(natural_event.get("weapon_damage_dice_sides", 0)), 6, "天生武器 add_weapon_dice 应读取 natural weapon 骰面。")
	_assert_eq(int(natural_event.get("base_damage", 0)), 6, "天生武器 add_weapon_dice 应使用 natural weapon 骰。")


func _test_requires_weapon_gate_accepts_equipped_only() -> void:
	var skill := _build_runtime_damage_skill(&"requires_weapon_contract", 1, true, false)
	var runtime := BattleRuntimeModule.new()
	runtime.configure_damage_resolver_for_tests(FixedRollDamageResolver.new([1], [10]))
	runtime.setup(null, {skill.skill_id: skill}, {}, {})

	var fixture := _build_runtime_duel_fixture(runtime, skill.skill_id)
	var attacker := fixture.get("attacker") as BattleUnitState
	var target := fixture.get("target") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	if attacker == null or target == null or command == null:
		return

	attacker.set_unarmed_weapon_projection()
	var target_hp_before := target.current_hp
	var unarmed_batch := runtime.issue_command(command)
	_assert_true(
		not unarmed_batch.log_lines.is_empty() and String(unarmed_batch.log_lines[-1]).contains("需要装备"),
		"空手攻击不应满足 requires_weapon。 log=%s" % [str(unarmed_batch.log_lines)]
	)
	_assert_eq(attacker.current_ap, 2, "空手被 requires_weapon 阻断时不应扣除 AP。")
	_assert_eq(target.current_hp, target_hp_before, "空手被 requires_weapon 阻断时不应结算伤害。")

	attacker.set_natural_weapon_projection(&"natural_weapon", &"physical_slash", 1, {
		"dice_count": 1,
		"dice_sides": 6,
		"flat_bonus": 0,
	})
	var natural_batch := runtime.issue_command(command)
	_assert_true(
		not natural_batch.log_lines.is_empty() and String(natural_batch.log_lines[-1]).contains("需要装备"),
		"天生武器不应满足 requires_weapon。 log=%s" % [str(natural_batch.log_lines)]
	)
	_assert_eq(attacker.current_ap, 2, "天生武器被 requires_weapon 阻断时不应扣除 AP。")
	_assert_eq(target.current_hp, target_hp_before, "天生武器被 requires_weapon 阻断时不应结算伤害。")

	_apply_weapon(attacker, 1, 6, 0)
	var equipped_batch := runtime.issue_command(command)
	_assert_true(equipped_batch.changed_unit_ids.has(attacker.unit_id), "装备武器应满足 requires_weapon 并正常结算施法者。")
	_assert_eq(attacker.current_ap, 1, "装备武器满足 requires_weapon 后应正常扣除 AP。")
	_assert_true(target.current_hp < target_hp_before, "装备武器满足 requires_weapon 后应造成伤害。")


func _test_natural_weapon_dice_do_not_trigger_skill_mastery() -> void:
	var gateway := MasteryGatewayStub.new()
	var skill := _build_runtime_damage_skill(&"natural_weapon_only_mastery_contract", 0, false, true)
	var runtime := BattleRuntimeModule.new()
	runtime.configure_damage_resolver_for_tests(FixedRollDamageResolver.new([1], [10]))
	runtime.setup(gateway, {skill.skill_id: skill}, {}, {})

	var fixture := _build_runtime_duel_fixture(runtime, skill.skill_id)
	var attacker := fixture.get("attacker") as BattleUnitState
	var target := fixture.get("target") as BattleUnitState
	var command := fixture.get("command") as BattleCommand
	if attacker == null or target == null or command == null:
		return
	attacker.source_member_id = &"hero"
	attacker.set_natural_weapon_projection(&"natural_weapon", &"physical_slash", 1, {
		"dice_count": 1,
		"dice_sides": 1,
		"flat_bonus": 0,
	})

	var batch := runtime.issue_command(command)
	_assert_true(batch.changed_unit_ids.has(attacker.unit_id), "天生武器骰技能应正常完成一次主动技能结算。")
	_assert_eq(gateway.skill_used_events, 1, "天生武器骰技能成功后仍应记录技能使用事件。")
	_assert_true(gateway.grants.is_empty(), "天生武器骰满值不应触发主动技能熟练度 / 精通入账。")


func _test_dice_event_fields_split_by_dice_group() -> void:
	var resolver := FixedRollDamageResolver.new([6, 4])
	var source := _build_unit(&"dice_event_split_user")
	_apply_weapon(source, 1, 6, 0)
	var target := _build_unit(&"dice_event_split_target")
	var weapon_only_effect := _build_damage_effect(0, true)
	var skill_only_effect := _build_damage_effect(0, false, 1, 4)

	var result: Dictionary = resolver.resolve_effects(source, target, [weapon_only_effect, skill_only_effect])
	var events = result.get("damage_events", [])
	_assert_eq(events.size() if events is Array else 0, 2, "拆分骰子事件回归应产生两段 damage event。")
	_assert_true(bool(result.get("damage_dice_high_total_roll", false)), "顶层 high-total 应只表达任意一段满足。")
	_assert_true(bool(result.get("skill_damage_dice_is_max", false)), "顶层技能骰事件应只表达任意一段满足。")
	_assert_true(bool(result.get("weapon_damage_dice_is_max", false)), "顶层武器骰事件应只表达任意一段满足。")
	_assert_true(not result.has("damage_dice_high_total_roll_reason"), "顶层 high-total 不应携带单段 reason。")
	_assert_true(not result.has("skill_damage_dice_is_max_reason"), "顶层技能骰事件不应携带单段 reason。")
	_assert_true(not result.has("weapon_damage_dice_is_max_reason"), "顶层武器骰事件不应携带单段 reason。")
	if events is Array and events.size() >= 2:
		var weapon_event := events[0] as Dictionary
		var skill_event := events[1] as Dictionary
		_assert_true(bool(weapon_event.get("damage_dice_high_total_roll", false)), "武器满骰段应满足 high-total 阈值。")
		_assert_eq(String(weapon_event.get("damage_dice_high_total_roll_reason", "")), "dice_threshold", "非暴击 high-total reason 应记录 dice_threshold。")
		_assert_true(not bool(weapon_event.get("skill_damage_dice_is_max", true)), "没有技能骰的段不应触发技能骰事件。")
		_assert_eq(String(weapon_event.get("skill_damage_dice_is_max_reason", "")), "", "没有技能骰时技能骰 reason 应为空。")
		_assert_true(bool(weapon_event.get("weapon_damage_dice_is_max", false)), "武器满骰段应触发武器骰事件。")
		_assert_eq(String(weapon_event.get("weapon_damage_dice_is_max_reason", "")), "weapon_dice_max", "武器满骰 reason 应记录 weapon_dice_max。")
		_assert_true(bool(skill_event.get("damage_dice_high_total_roll", false)), "技能满骰段应满足 high-total 阈值。")
		_assert_eq(String(skill_event.get("damage_dice_high_total_roll_reason", "")), "dice_threshold", "技能满骰 high-total reason 应记录 dice_threshold。")
		_assert_true(bool(skill_event.get("skill_damage_dice_is_max", false)), "技能满骰段应触发技能骰事件。")
		_assert_eq(String(skill_event.get("skill_damage_dice_is_max_reason", "")), "skill_dice_max", "技能满骰 reason 应记录 skill_dice_max。")
		_assert_true(not bool(skill_event.get("weapon_damage_dice_is_max", true)), "没有武器骰的段不应触发武器骰事件。")
		_assert_eq(String(skill_event.get("weapon_damage_dice_is_max_reason", "")), "", "没有武器骰时武器骰 reason 应为空。")


func _test_dice_event_fields_stay_false_without_dice_groups() -> void:
	var resolver := FixedRollDamageResolver.new([6])
	var source := _build_unit(&"no_dice_event_user")
	var target := _build_unit(&"no_dice_event_target")
	var effect := _build_damage_effect(5, false)

	var result: Dictionary = resolver.resolve_effects(source, target, [effect])
	var event := _first_damage_event(result)
	_assert_true(not bool(event.get("damage_dice_high_total_roll", true)), "无骰组时 high-total 事件必须为 false。")
	_assert_true(not bool(event.get("skill_damage_dice_is_max", true)), "无技能骰组时技能骰事件必须为 false。")
	_assert_true(not bool(event.get("weapon_damage_dice_is_max", true)), "无武器骰组时武器骰事件必须为 false。")
	_assert_eq(String(event.get("damage_dice_high_total_roll_reason", "")), "", "无骰组时 high-total reason 应为空。")
	_assert_eq(String(event.get("skill_damage_dice_is_max_reason", "")), "", "无技能骰组时技能骰 reason 应为空。")
	_assert_eq(String(event.get("weapon_damage_dice_is_max_reason", "")), "", "无武器骰组时武器骰 reason 应为空。")
	_assert_true(not bool(result.get("damage_dice_high_total_roll", true)), "顶层 high-total 汇总不应因 0 == 0 触发。")
	_assert_true(not bool(result.get("skill_damage_dice_is_max", true)), "顶层技能骰汇总不应因 0 == 0 触发。")
	_assert_true(not bool(result.get("weapon_damage_dice_is_max", true)), "顶层武器骰汇总不应因 0 == 0 触发。")


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


func _build_runtime_damage_skill(
	skill_id: StringName,
	power: int,
	requires_weapon: bool,
	add_weapon_dice: bool,
	dice_count: int = 0,
	dice_sides: int = 0
) -> SkillDef:
	var damage_effect := _build_damage_effect(power, add_weapon_dice, dice_count, dice_sides)
	damage_effect.effect_target_team_filter = &"enemy"
	if requires_weapon:
		damage_effect.params["requires_weapon"] = true
		damage_effect.params["use_weapon_physical_damage_tag"] = true

	var combat_profile := CombatSkillDef.new()
	combat_profile.skill_id = skill_id
	combat_profile.target_mode = &"unit"
	combat_profile.target_team_filter = &"enemy"
	combat_profile.range_value = 1
	combat_profile.ap_cost = 1
	var effect_defs: Array[CombatEffectDef] = [damage_effect]
	combat_profile.effect_defs = effect_defs

	var skill := SkillDef.new()
	skill.skill_id = skill_id
	skill.display_name = String(skill_id)
	skill.tags = [&"warrior", &"melee"]
	skill.combat_profile = combat_profile
	return skill


func _build_runtime_duel_fixture(runtime: BattleRuntimeModule, skill_id: StringName) -> Dictionary:
	var state := _build_skill_test_state(Vector2i(2, 1))
	var attacker := _build_unit(&"weapon_contract_user", Vector2i(0, 0), 2)
	attacker.known_active_skill_ids = [skill_id]
	attacker.known_skill_level_map = {skill_id: 1}
	attacker.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 100)
	var target := _build_enemy_unit(&"weapon_contract_target", Vector2i(1, 0))
	target.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 1)
	state.units = {
		attacker.unit_id: attacker,
		target.unit_id: target,
	}
	state.ally_unit_ids = [attacker.unit_id]
	state.enemy_unit_ids = [target.unit_id]
	state.active_unit_id = attacker.unit_id
	_assert_true(runtime._grid_service.place_unit(state, attacker, attacker.coord, true), "武器骰 runtime 夹具攻击者应能放入战场。")
	_assert_true(runtime._grid_service.place_unit(state, target, target.coord, true), "武器骰 runtime 夹具目标应能放入战场。")
	runtime._state = state

	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = attacker.unit_id
	command.skill_id = skill_id
	command.target_unit_id = target.unit_id
	command.target_coord = target.coord
	return {
		"state": state,
		"attacker": attacker,
		"target": target,
		"command": command,
	}


func _build_skill_test_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"weapon_dice_runtime_contract"
	state.phase = &"unit_acting"
	state.map_size = map_size
	state.timeline = BattleTimelineState.new()
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.base_height = 4
	cell.height_offset = 0
	cell.recalculate_runtime_values()
	return cell


func _build_unit(unit_id: StringName, coord: Vector2i = Vector2i.ZERO, current_ap: int = 1) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.current_ap = current_ap
	unit.current_hp = 100
	unit.is_alive = true
	unit.attribute_snapshot.set_value(&"hp_max", 100)
	unit.set_anchor_coord(coord)
	return unit


func _build_enemy_unit(unit_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := _build_unit(unit_id, coord, 1)
	unit.faction_id = &"enemy"
	unit.current_hp = 30
	unit.attribute_snapshot.set_value(&"hp_max", 30)
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


func _apply_versatile_weapon(unit: BattleUnitState, uses_two_hands: bool) -> void:
	unit.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "versatile_test_longsword",
		"weapon_profile_type_id": "longsword",
		"weapon_current_grip": "two_handed" if uses_two_hands else "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 8, "flat_bonus": 0},
		"weapon_two_handed_dice": {"dice_count": 2, "dice_sides": 6, "flat_bonus": 0},
		"weapon_is_versatile": true,
		"weapon_uses_two_hands": uses_two_hands,
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
