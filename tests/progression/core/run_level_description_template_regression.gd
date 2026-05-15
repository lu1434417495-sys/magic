extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const SkillLevelDescriptionFormatter = preload("res://scripts/systems/progression/skill_level_description_formatter.gd")
const BATTLE_RECOVERY_SKILL_PATH := "res://data/configs/skills/warrior_battle_recovery.tres"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_basic_substitution()
	_test_conditional_present()
	_test_conditional_absent()
	_test_conditional_numeric_zero_absent()
	_test_guard_full_template()
	_test_whirlwind_template()
	_test_taunt_template()
	_test_empty_config()
	_test_level_description_requires_template_config()
	_test_level_description_hides_zero_profile_defaults_in_optional_blocks()
	_test_battle_recovery_description_derives_display_dice()
	_test_level_description_derives_typed_effect_fields()
	_test_level_description_ignores_locked_cast_variant_effects()

	if _failures.is_empty():
		print("Level description template regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Level description template regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_basic_substitution() -> void:
	var result := SkillLevelDescriptionFormatter.render_template("距离{dist}，伤害{dmg}", {"dist": "3", "dmg": "5"})
	_assert_eq(result, "距离3，伤害5", "基本变量替换")


func _test_conditional_present() -> void:
	var result := SkillLevelDescriptionFormatter.render_template("A{{?x}}，B{x}{{/x}}C", {"x": "1"})
	_assert_eq(result, "A，B1C", "条件块存在时应保留内容")


func _test_conditional_absent() -> void:
	var result := SkillLevelDescriptionFormatter.render_template("A{{?x}}，B{x}{{/x}}C", {})
	_assert_eq(result, "AC", "条件块不存在时应整段删除")


func _test_conditional_numeric_zero_absent() -> void:
	_assert_eq(
		SkillLevelDescriptionFormatter.render_template("A{{?x}}，B{x}{{/x}}C", {"x": 0}),
		"AC",
		"条件块数值为 0 时应整段删除"
	)
	_assert_eq(
		SkillLevelDescriptionFormatter.render_template("A{{?x}}，B{x}{{/x}}C", {"x": 0.0}),
		"AC",
		"条件块数值为 0.0 时应整段删除"
	)


func _test_guard_full_template() -> void:
	var template := "guarding 物理伤害减{guard_power}{{?slow_power}}，移动力-{slow_power}{{/slow_power}}，持续{duration}TU，体力消耗{stamina}，冷却{cooldown}TU"

	var level0 := SkillLevelDescriptionFormatter.render_template(template, {
		"guard_power": "1", "slow_power": "1", "duration": "40", "stamina": "50", "cooldown": "120"
	})
	_assert_eq(
		level0,
		"guarding 物理伤害减1，移动力-1，持续40TU，体力消耗50，冷却120TU",
		"格挡 0 级完整描述"
	)

	var level4 := SkillLevelDescriptionFormatter.render_template(template, {
		"guard_power": "2", "duration": "60", "stamina": "35", "cooldown": "100"
	})
	_assert_eq(
		level4,
		"guarding 物理伤害减2，持续60TU，体力消耗35，冷却100TU",
		"格挡 4 级（无 slow）完整描述"
	)


func _test_whirlwind_template() -> void:
	var template := "冲锋距离{distance}，攻击检定{attack}，冷却{cooldown}TU。每进一步对周边敌人触发武器攻击{{?stagger}}；被连续命中超过3次的敌人陷入踉跄{{/stagger}}"

	var level0 := SkillLevelDescriptionFormatter.render_template(template, {
		"distance": "3", "attack": "-4", "cooldown": "120"
	})
	_assert_eq(
		level0,
		"冲锋距离3，攻击检定-4，冷却120TU。每进一步对周边敌人触发武器攻击",
		"旋风斩 0 级（无 stagger）"
	)

	var level9 := SkillLevelDescriptionFormatter.render_template(template, {
		"distance": "6", "attack": "+1", "cooldown": "90", "stagger": "true"
	})
	_assert_eq(
		level9,
		"冲锋距离6，攻击检定+1，冷却90TU。每进一步对周边敌人触发武器攻击；被连续命中超过3次的敌人陷入踉跄",
		"旋风斩 9 级（有 stagger）"
	)


func _test_taunt_template() -> void:
	var template := "{{?range_desc}}{range_desc}{{/range_desc}}，使其攻击非来源单位时处于劣势，持续{duration}TU"

	var level0 := SkillLevelDescriptionFormatter.render_template(template, {
		"range_desc": "对身边相邻一格的敌人嘲讽", "duration": "30"
	})
	_assert_eq(
		level0,
		"对身边相邻一格的敌人嘲讽，使其攻击非来源单位时处于劣势，持续30TU",
		"挑衅 0 级"
	)

	var level5 := SkillLevelDescriptionFormatter.render_template(template, {
		"range_desc": "对身边相邻一格横向3格内的敌人嘲讽", "duration": "90"
	})
	_assert_eq(
		level5,
		"对身边相邻一格横向3格内的敌人嘲讽，使其攻击非来源单位时处于劣势，持续90TU",
		"挑衅 5 级"
	)


func _test_empty_config() -> void:
	var result := SkillLevelDescriptionFormatter.render_template("A{{?x}}B{{/x}}C", {})
	_assert_eq(result, "AC", "空配置应删除所有条件块")


func _test_level_description_requires_template_config() -> void:
	var skill_def := SkillDef.new()
	skill_def.level_description_template = "模板{val}"
	skill_def.level_description_configs = {"0": {"val": "新"}, "1": {"val": "新"}}

	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(skill_def, 0), "模板新", "正式模板配置应渲染等级描述")
	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(skill_def, 1), "模板新", "正式模板配置应渲染对应等级描述")
	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(skill_def, 2), "", "缺少当前等级正式配置时应返回空")

	var missing_template := SkillDef.new()
	missing_template.level_description_configs = {"0": {"val": "新"}}
	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(missing_template, 0), "", "缺少正式模板时应返回空")

	var missing_config := SkillDef.new()
	missing_config.level_description_template = "模板{val}"
	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(missing_config, 0), "", "缺少正式等级配置时应返回空")

	var wrong_config_type := SkillDef.new()
	wrong_config_type.level_description_template = "模板{val}"
	wrong_config_type.level_description_configs = {"0": "旧格式描述"}
	_assert_eq(SkillLevelDescriptionFormatter.build_level_description(wrong_config_type, 0), "", "等级配置不是字典时应返回空")


func _test_level_description_hides_zero_profile_defaults_in_optional_blocks() -> void:
	var skill_def := SkillDef.new()
	skill_def.level_description_template = "基础{{?attack_roll_bonus}}，攻击检定{attack_roll_bonus}{{/attack_roll_bonus}}{{?aura_cost}}，消耗{aura_cost}斗气{{/aura_cost}}"
	skill_def.level_description_configs = {
		"0": {"marker": "configured"},
		"1": {"marker": "configured"},
	}
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.attack_roll_bonus = 0
	skill_def.combat_profile.aura_cost = 0
	skill_def.combat_profile.level_overrides = {
		1: {
			"attack_roll_bonus": 2,
			"aura_cost": 1,
		},
	}

	_assert_eq(
		SkillLevelDescriptionFormatter.build_level_description(skill_def, 0),
		"基础",
		"formatter 不应让 profile 默认 0 撑开 optional 条件块。"
	)
	_assert_eq(
		SkillLevelDescriptionFormatter.build_level_description(skill_def, 1),
		"基础，攻击检定2，消耗1斗气",
		"formatter 仍应显示非 0 profile override。"
	)


func _test_battle_recovery_description_derives_display_dice() -> void:
	var skill_def := load(BATTLE_RECOVERY_SKILL_PATH) as SkillDef
	if skill_def == null:
		_test.fail("战斗回复技能资源应能加载。")
		return

	var low_stat_description := SkillLevelDescriptionFormatter.build_level_description(skill_def, 5, {
		"con_mod": -3,
		"will_mod": -3,
	})
	_assert_eq(
		low_stat_description,
		"恢复体力10D4，并恢复生命2D4。冷却120TU。",
		"战斗回复描述 formatter 应正确渲染低属性展示骰面和 Lv5 治疗骰。"
	)


func _test_level_description_derives_typed_effect_fields() -> void:
	var skill_def := SkillDef.new()
	skill_def.level_description_template = "造成{dmg}伤害（{damage_save_text}），{shocked_save_text}（{shocked_duration_tu}TU，强度{shocked_power}）。"
	skill_def.level_description_configs = {"0": {"dmg": "4D6"}}
	skill_def.combat_profile = CombatSkillDef.new()

	var damage_effect := CombatEffectDef.new()
	damage_effect.effect_type = &"damage"
	damage_effect.save_ability = &"agility"
	damage_effect.save_dc_mode = &"caster_spell"
	damage_effect.save_partial_on_success = true
	skill_def.combat_profile.effect_defs.append(damage_effect)

	var status_effect := CombatEffectDef.new()
	status_effect.effect_type = &"status"
	status_effect.status_id = &"shocked"
	status_effect.power = 1
	status_effect.duration_tu = 60
	status_effect.save_ability = &"constitution"
	status_effect.save_dc_mode = &"caster_spell"
	skill_def.combat_profile.effect_defs.append(status_effect)

	_assert_eq(
		SkillLevelDescriptionFormatter.build_level_description(skill_def, 0),
		"造成4D6伤害（敏捷豁免成功时伤害减半），体质豁免失败时附加感电（60TU，强度1）。",
		"等级描述 formatter 应从 typed effect fields 派生豁免、状态名、持续时间和强度。"
	)


func _test_level_description_ignores_locked_cast_variant_effects() -> void:
	var skill_def := SkillDef.new()
	skill_def.level_description_template = "基础{base}{{?locked_param}}，高阶{locked_param}{{/locked_param}}"
	skill_def.level_description_configs = {
		"0": {"base": "可用"},
		"3": {"base": "可用"},
	}
	skill_def.combat_profile = CombatSkillDef.new()

	var variant := CombatCastVariantDef.new()
	variant.variant_id = &"advanced"
	variant.min_skill_level = 3
	var variant_effect := CombatEffectDef.new()
	variant_effect.effect_type = &"damage"
	variant_effect.params = {"locked_param": "未锁"}
	variant.effect_defs.append(variant_effect)
	skill_def.combat_profile.cast_variants.append(variant)

	_assert_eq(
		SkillLevelDescriptionFormatter.build_level_description(skill_def, 0),
		"基础可用",
		"低等级描述不应合并未解锁施法形态的 effect params。"
	)
	_assert_eq(
		SkillLevelDescriptionFormatter.build_level_description(skill_def, 3),
		"基础可用，高阶未锁",
		"达到施法形态等级后应合并该形态的 effect params。"
	)


func _assert_eq(actual: String, expected: String, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, actual, expected])
