extends SceneTree

const BattleDamagePreviewRangeService = preload("res://scripts/systems/battle/rules/battle_damage_preview_range_service.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_empty_preview_contract()
	_test_power_only_damage_preview()
	_test_weapon_and_skill_dice_damage_range()
	_test_multiple_damage_effects_are_summed()
	_test_two_handed_weapon_dice_ignores_alias_skill_dice_fields()
	_test_dice_bonus_without_dice_is_ignored()
	if _failures.is_empty():
		print("Battle damage preview range contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle damage preview range contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_empty_preview_contract() -> void:
	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(null, [])
	_assert_true(not bool(preview.get("has_damage", true)), "无伤害效果时 has_damage 应为 false。")
	_assert_eq(int(preview.get("min_damage", -1)), 0, "无伤害效果时 min_damage 应为 0。")
	_assert_eq(int(preview.get("max_damage", -1)), 0, "无伤害效果时 max_damage 应为 0。")
	_assert_eq(String(preview.get("summary_text", "x")), "", "无伤害效果时 summary_text 应为空。")
	_assert_eq(preview.get("damage_ranges", []).size(), 0, "无伤害效果时 damage_ranges 应为空。")


func _test_power_only_damage_preview() -> void:
	var effect := _build_damage_effect(12)
	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(null, [effect])
	_assert_true(bool(preview.get("has_damage", false)), "power-only 伤害效果应产生伤害预览。")
	_assert_eq(int(preview.get("min_damage", 0)), 12, "power-only min_damage 应等于 power。")
	_assert_eq(int(preview.get("max_damage", 0)), 12, "power-only max_damage 应等于 power。")
	_assert_eq(String(preview.get("summary_text", "")), "伤害 12", "固定伤害摘要应省略范围横杠。")
	_assert_eq(BattleDamagePreviewRangeService.format_damage_range_text(preview), "伤害 12", "format_damage_range_text 应复用同一固定伤害文案。")


func _test_weapon_and_skill_dice_damage_range() -> void:
	var source := _build_unit(&"preview_weapon_user")
	_apply_weapon(source, 1, 6, 2)
	var effect := _build_damage_effect(5, true, 2, 4, 3)

	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(source, [effect])
	_assert_eq(int(preview.get("min_damage", 0)), 13, "最小值应为 power + 武器最小骰含 flat_bonus + 技能最小骰含 dice_bonus。")
	_assert_eq(int(preview.get("max_damage", 0)), 24, "最大值应为 power + 武器最大骰含 flat_bonus + 技能最大骰含 dice_bonus。")
	_assert_eq(String(preview.get("summary_text", "")), "伤害 13-24", "范围摘要应显示理论最小与最大值。")
	var ranges := preview.get("damage_ranges", []) as Array
	_assert_eq(ranges.size(), 1, "单段伤害应产生一条 damage_range。")
	if ranges.size() >= 1 and ranges[0] is Dictionary:
		var damage_range := ranges[0] as Dictionary
		_assert_true(bool(damage_range.get("add_weapon_dice", false)), "显式 add_weapon_dice 应进入 per-effect 预览。")
		_assert_eq(int(damage_range.get("weapon_damage_dice_min", 0)), 3, "武器骰最小值应包含 flat_bonus。")
		_assert_eq(int(damage_range.get("weapon_damage_dice_max", 0)), 8, "武器骰最大值应包含 flat_bonus。")
		_assert_eq(int(damage_range.get("damage_dice_min", 0)), 5, "技能骰最小值应包含 dice_bonus。")
		_assert_eq(int(damage_range.get("damage_dice_max", 0)), 11, "技能骰最大值应包含 dice_bonus。")


func _test_multiple_damage_effects_are_summed() -> void:
	var source := _build_unit(&"multi_preview_user")
	_apply_weapon(source, 2, 6, 0)
	var weapon_effect := _build_damage_effect(0, true)
	var status_effect := CombatEffectDef.new()
	status_effect.effect_type = &"status"
	status_effect.power = 99
	var skill_effect := _build_damage_effect(10, false, 1, 8, 1)

	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(source, [weapon_effect, status_effect, skill_effect])
	_assert_eq(int(preview.get("min_damage", 0)), 14, "多个 damage effect 的 min_damage 应求和并跳过非伤害效果。")
	_assert_eq(int(preview.get("max_damage", 0)), 31, "多个 damage effect 的 max_damage 应求和并跳过非伤害效果。")
	var ranges := preview.get("damage_ranges", []) as Array
	_assert_eq(ranges.size(), 2, "damage_ranges 应只包含 damage effect。")
	if ranges.size() >= 2 and ranges[0] is Dictionary and ranges[1] is Dictionary:
		_assert_eq(int((ranges[0] as Dictionary).get("effect_index", -1)), 0, "第一段应保留原 effect index。")
		_assert_eq(int((ranges[1] as Dictionary).get("effect_index", -1)), 2, "第二段应保留原 effect index。")


func _test_two_handed_weapon_dice_ignores_alias_skill_dice_fields() -> void:
	var source := _build_unit(&"two_handed_preview_user")
	source.apply_weapon_projection({
		"weapon_profile_kind": "equipped",
		"weapon_item_id": "two_handed_preview_weapon",
		"weapon_profile_type_id": "greatsword",
		"weapon_current_grip": "two_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 4, "flat_bonus": 1},
		"weapon_two_handed_dice": {"dice_count": 2, "dice_sides": 6, "flat_bonus": 4},
		"weapon_uses_two_hands": true,
		"weapon_physical_damage_tag": "physical_slash",
	})
	var effect := _build_damage_effect(1, true)
	effect.params["damage_dice_count"] = 3
	effect.params["damage_dice_sides"] = 3
	effect.params["damage_dice_bonus"] = 2

	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(source, [effect])
	_assert_eq(int(preview.get("min_damage", 0)), 7, "旧技能骰 alias 不应加入预览最小伤害。")
	_assert_eq(int(preview.get("max_damage", 0)), 17, "旧技能骰 alias 不应加入预览最大伤害。")
	var ranges := preview.get("damage_ranges", []) as Array
	if ranges.size() >= 1 and ranges[0] is Dictionary:
		var damage_range := ranges[0] as Dictionary
		_assert_eq(int(damage_range.get("weapon_damage_dice_count", 0)), 2, "双手武器骰数量应来自 two_handed_dice。")
		_assert_eq(int(damage_range.get("weapon_damage_dice_sides", 0)), 6, "双手武器骰面应来自 two_handed_dice。")
		_assert_eq(int(damage_range.get("damage_dice_count", 0)), 0, "旧 damage_dice_count alias 不应再被读取。")
		_assert_eq(int(damage_range.get("damage_dice_sides", 0)), 0, "旧 damage_dice_sides alias 不应再被读取。")
		_assert_eq(int(damage_range.get("damage_dice_bonus", 0)), 0, "旧 damage_dice_bonus alias 不应再被读取。")


func _test_dice_bonus_without_dice_is_ignored() -> void:
	var effect := _build_damage_effect(4)
	effect.params["dice_bonus"] = 99
	var preview := BattleDamagePreviewRangeService.build_skill_damage_preview(null, [effect])
	_assert_eq(int(preview.get("min_damage", 0)), 4, "缺少有效技能骰时 dice_bonus 不应单独加入最小伤害。")
	_assert_eq(int(preview.get("max_damage", 0)), 4, "缺少有效技能骰时 dice_bonus 不应单独加入最大伤害。")
	var ranges := preview.get("damage_ranges", []) as Array
	if ranges.size() >= 1 and ranges[0] is Dictionary:
		_assert_eq(int((ranges[0] as Dictionary).get("damage_dice_bonus", -1)), 0, "无有效技能骰时 damage_dice_bonus 应保持 0。")


func _build_damage_effect(
	power: int,
	add_weapon_dice: bool = false,
	dice_count: int = 0,
	dice_sides: int = 0,
	dice_bonus: int = 0
) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"damage"
	effect.power = power
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
		"weapon_item_id": "preview_range_weapon",
		"weapon_profile_type_id": "test_weapon",
		"weapon_current_grip": "one_handed",
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": dice_count, "dice_sides": dice_sides, "flat_bonus": flat_bonus},
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_slash",
	})


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
