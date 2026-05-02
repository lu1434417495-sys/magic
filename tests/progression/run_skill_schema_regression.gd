extends SceneTree

const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const SkillContentRegistry = preload("res://scripts/player/progression/skill_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const OFFICIAL_SKILL_RESOURCE_DIRECTORY := "res://data/configs/skills/"
const OFFICIAL_CHARGE_PATH := "res://data/configs/skills/charge.tres"
const OFFICIAL_HEAVY_STRIKE_PATH := "res://data/configs/skills/warrior_heavy_strike.tres"
const OFFICIAL_COMMON_MELEE_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"charge",
]
const OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"warrior_heavy_strike",
	&"warrior_sweeping_slash",
	&"warrior_piercing_thrust",
	&"warrior_guard_break",
	&"warrior_execution_cleave",
	&"warrior_jump_slash",
	&"warrior_backstep",
	&"warrior_guard",
	&"warrior_battle_recovery",
	&"warrior_shield_bash",
	&"warrior_taunt",
	&"warrior_war_cry",
	&"warrior_true_dragon_slash",
	&"warrior_combo_strike",
	&"warrior_aura_slash",
	&"warrior_whirlwind_slash",
	&"saint_blade_combo",
]
const OFFICIAL_PRIEST_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"priest_aid",
]
const OFFICIAL_ARCHER_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"archer_aimed_shot",
	&"archer_armor_piercer",
	&"archer_heartseeker",
	&"archer_long_draw",
	&"archer_split_bolt",
	&"archer_execution_arrow",
	&"archer_double_nock",
	&"archer_far_horizon",
	&"archer_skirmish_step",
	&"archer_backstep_shot",
	&"archer_sidewind_slide",
	&"archer_running_shot",
	&"archer_grapple_redeploy",
	&"archer_evasive_roll",
	&"archer_highground_claim",
	&"archer_hunter_feint",
	&"archer_pinning_shot",
	&"archer_tendon_splitter",
	&"archer_disrupting_arrow",
	&"archer_flash_whistle",
	&"archer_tripwire_arrow",
	&"archer_shield_breaker",
	&"archer_fearsignal_shot",
	&"archer_harrier_mark",
	&"archer_multishot",
	&"archer_arrow_rain",
	&"archer_fan_volley",
	&"archer_suppressive_fire",
	&"archer_breach_barrage",
	&"archer_blast_arrow",
	&"archer_hunting_grid",
	&"archer_killing_field",
]

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_skill_resources_scan_and_validate()
	_test_progression_registry_uses_skill_resources_only()
	_test_attribute_growth_progress_schema_validation()
	_test_level_override_key_schema_validation()
	_test_requires_weapon_param_schema_validation()
	_test_duration_param_schema_validation()
	_test_damage_dice_alias_param_schema_validation()
	_test_damage_resolver_alias_param_schema_validation()
	_test_forced_move_param_schema_validation()
	_test_supported_effect_type_schema_validation()
	_test_unknown_effect_type_schema_validation()
	_test_skill_registry_reports_missing_id_duplicate_schema_and_illegal_refs()

	if _failures.is_empty():
		print("Skill schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Skill schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_seed_skill_resources_scan_and_validate() -> void:
	var registry := SkillContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var basic_attack := skill_defs.get(&"basic_attack") as SkillDef
	var charge := skill_defs.get(&"charge") as SkillDef
	var heavy_strike := skill_defs.get(&"warrior_heavy_strike") as SkillDef
	var sweeping_slash := skill_defs.get(&"warrior_sweeping_slash") as SkillDef
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(registry.validate().is_empty(), "SkillContentRegistry 的正式技能资源当前不应报告校验错误。")
	_assert_true(basic_attack != null, "SkillContentRegistry 应扫描到内建基础攻击资源。")
	if basic_attack != null:
		_assert_eq(basic_attack.display_name, "攻击", "基础攻击应保留展示名。")
		_assert_true(basic_attack.tags.has(&"basic"), "基础攻击应带 basic 标签。")
		_assert_true(basic_attack.can_use_in_combat(), "基础攻击应可在战斗中使用。")
		if basic_attack.combat_profile != null and not basic_attack.combat_profile.effect_defs.is_empty():
			var basic_damage = basic_attack.combat_profile.effect_defs[0]
			_assert_true(bool(basic_damage.params.get("add_weapon_dice", false)), "基础攻击应使用当前武器/空手/天生武器骰。")
			_assert_true(bool(basic_damage.params.get("use_weapon_physical_damage_tag", false)), "基础攻击应使用当前武器/空手/天生武器伤害类型。")
			_assert_true(not bool(basic_damage.params.get("requires_weapon", false)), "基础攻击不应要求装备武器。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_COMMON_MELEE_RESOURCE_SKILL_IDS, &"melee", "通用近战")
	_assert_true(charge != null, "SkillContentRegistry 应扫描到正式冲锋资源。")
	if charge != null:
		_assert_eq(String(charge.resource_path), OFFICIAL_CHARGE_PATH, "冲锋应来自正式 skill resource。")
		_assert_eq(int(charge.max_level), 7, "冲锋应保留 7 级绝对上限。")
		_assert_eq(int(charge.non_core_max_level), 5, "冲锋非核心上限应保留 5。")
		_assert_eq(Array(charge.mastery_curve), [100, 250, 550, 1000, 1600, 2350, 3250], "冲锋熟练度曲线应匹配当前设计。")
		_assert_eq(charge.growth_tier, &"intermediate", "冲锋应保留 intermediate 成长档。")
		_assert_eq(int(charge.attribute_requirements.get("agility", 0)), 14, "冲锋敏捷前置应来自正式资源。")
		_assert_eq(int(charge.attribute_requirements.get("strength", 0)), 12, "冲锋力量前置应来自正式资源。")
		_assert_eq(int(charge.attribute_growth_progress.get("agility", 0)), 100, "冲锋敏捷成长进度应来自正式资源。")
		_assert_eq(int(charge.attribute_growth_progress.get("strength", 0)), 20, "冲锋力量成长进度应来自正式资源。")
		_assert_true(charge.combat_profile != null, "冲锋资源应保留 combat_profile。")
		if charge.combat_profile != null:
			_assert_eq(int(charge.combat_profile.stamina_cost), 40, "冲锋基础体力消耗应保留 40。")
			_assert_eq(int(charge.combat_profile.cooldown_tu), 50, "冲锋基础冷却应保留 50 TU。")
			_assert_eq(int(charge.combat_profile.get_effective_resource_costs(2).get("stamina_cost", 0)), 35, "冲锋 2 级起体力消耗应降为 35。")
			_assert_eq(int(charge.combat_profile.get_effective_resource_costs(4).get("stamina_cost", 0)), 30, "冲锋 4 级起体力消耗应降为 30。")
			_assert_eq(int(charge.combat_profile.get_effective_resource_costs(6).get("stamina_cost", 0)), 25, "冲锋 6 级起体力消耗应降为 25。")
			_assert_eq(int(charge.combat_profile.cast_variants.size()), 1, "冲锋应保留一个正式 cast variant。")
			var charge_variant = charge.combat_profile.get_cast_variant(&"charge_line")
			_assert_true(charge_variant != null, "冲锋应保留 charge_line 变体。")
			if charge_variant != null and not charge_variant.effect_defs.is_empty():
				var charge_effect = charge_variant.effect_defs[0]
				_assert_eq(charge_effect.effect_type, &"charge", "冲锋变体应保留 charge effect。")
				_assert_eq(int(charge_effect.params.get("base_distance", 0)), 3, "冲锋基础距离应保留 3。")
				_assert_eq(int((charge_effect.params.get("distance_by_level", {}) as Dictionary).get("7", 0)), 7, "冲锋 7 级距离应保留 7。")
				_assert_eq(int(charge_effect.params.get("trap_immunity_level", 0)), 7, "冲锋陷阱免疫等级应保留 7。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS, &"warrior", "战士")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_PRIEST_RESOURCE_SKILL_IDS, &"priest", "神术")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_ARCHER_RESOURCE_SKILL_IDS, &"archer", "弓箭手")
	_assert_resource_backed_skill_ids(skill_defs, _collect_mage_skill_ids(skill_defs), &"mage", "法师")
	_assert_true(skill_defs.has(&"warrior_heavy_strike"), "SkillContentRegistry 应扫描到已迁移的重击资源。")
	_assert_true(heavy_strike != null, "重击资源应成功转成 SkillDef。")
	if heavy_strike == null:
		return
	_assert_eq(String(heavy_strike.resource_path), OFFICIAL_HEAVY_STRIKE_PATH, "重击应来自正式 skill resource。")
	_assert_eq(heavy_strike.display_name, "重击", "重击资源应保留展示名。")
	_assert_eq(heavy_strike.icon_id, &"warrior_heavy_strike", "重击资源应保留稳定 icon_id。")
	_assert_eq(int(heavy_strike.max_level), 5, "重击资源应保留 5 级绝对上限。")
	_assert_eq(int(heavy_strike.non_core_max_level), 3, "重击未锁定核心时应最多提升到 3 级。")
	_assert_eq(Array(heavy_strike.mastery_curve), [100, 250, 550, 1000, 1600], "重击熟练度曲线应匹配当前设计。")
	_assert_true(heavy_strike.tags.has(&"warrior"), "重击资源应保留 warrior 标签。")
	_assert_true(heavy_strike.can_use_in_combat(), "重击资源应保留 combat_profile。")
	if heavy_strike.combat_profile != null:
		_assert_eq(int(heavy_strike.combat_profile.effect_defs.size()), 6, "重击资源应保留六条分级 effect_defs。")
		_assert_eq(int(heavy_strike.combat_profile.ap_cost), 1, "重击资源应保留 1 点 AP 消耗。")
		_assert_eq(int(heavy_strike.combat_profile.stamina_cost), 30, "重击资源应保留 30 点体力消耗。")
		_assert_eq(int(heavy_strike.combat_profile.get_effective_resource_costs(2).get("stamina_cost", 0)), 20, "重击 2 级起体力消耗应降为 20。")
		_assert_eq(int(heavy_strike.combat_profile.cooldown_tu), 0, "重击资源不应配置冷却。")
		_assert_eq(int(heavy_strike.combat_profile.attack_roll_bonus), -1, "重击所有等级都应保留 -1 攻击检定修正。")
		var level_zero_damage = heavy_strike.combat_profile.effect_defs[0]
		var level_one_damage = heavy_strike.combat_profile.effect_defs[1]
		var level_three_damage = heavy_strike.combat_profile.effect_defs[2]
		var armor_break_effect = heavy_strike.combat_profile.effect_defs[3]
		var level_four_armor_break_effect = heavy_strike.combat_profile.effect_defs[4]
		var level_five_staggered_effect = heavy_strike.combat_profile.effect_defs[5]
		_assert_eq(int(level_zero_damage.params.get("dice_sides", 0)), 4, "重击 0 级伤害骰应为 1d4。")
		_assert_true(bool(level_zero_damage.params.get("requires_weapon", false)), "重击 0 级武器伤害标签效果必须显式要求装备武器。")
		_assert_true(bool(level_zero_damage.params.get("use_weapon_physical_damage_tag", false)), "重击 0 级应使用当前武器物理伤害标签。")
		_assert_eq(int(level_zero_damage.max_skill_level), 0, "重击 0 级伤害效果应只在 0 级生效。")
		_assert_eq(int(level_one_damage.params.get("dice_sides", 0)), 6, "重击 1-2 级伤害骰应为 1d6。")
		_assert_true(bool(level_one_damage.params.get("requires_weapon", false)), "重击 1-2 级武器伤害标签效果必须显式要求装备武器。")
		_assert_true(bool(level_one_damage.params.get("use_weapon_physical_damage_tag", false)), "重击 1-2 级应使用当前武器物理伤害标签。")
		_assert_eq(int(level_one_damage.min_skill_level), 1, "重击 1d6 伤害效果应从 1 级生效。")
		_assert_eq(int(level_one_damage.max_skill_level), 2, "重击 1d6 伤害效果应在 2 级后停止生效。")
		_assert_eq(int(level_three_damage.params.get("dice_sides", 0)), 8, "重击 3 级伤害骰应为 1d8。")
		_assert_true(bool(level_three_damage.params.get("requires_weapon", false)), "重击 3 级武器伤害标签效果必须显式要求装备武器。")
		_assert_true(bool(level_three_damage.params.get("use_weapon_physical_damage_tag", false)), "重击 3 级应使用当前武器物理伤害标签。")
		_assert_eq(int(level_three_damage.min_skill_level), 3, "重击 1d8 伤害效果应从 3 级生效。")
		_assert_eq(armor_break_effect.status_id, &"armor_break", "重击满级效果应是 armor_break。")
		_assert_eq(int(armor_break_effect.min_skill_level), 3, "重击 armor_break 应从 3 级生效。")
		_assert_eq(int(armor_break_effect.max_skill_level), 3, "重击 3 级 armor_break 应只覆盖 3 级。")
		_assert_eq(int(armor_break_effect.duration_tu), 40, "重击 3 级 armor_break 应持续 40 TU。")
		_assert_eq(level_four_armor_break_effect.status_id, &"armor_break", "重击 4 级效果应继续是 armor_break。")
		_assert_eq(int(level_four_armor_break_effect.min_skill_level), 4, "重击 4 级 armor_break 应从 4 级生效。")
		_assert_eq(int(level_four_armor_break_effect.duration_tu), 70, "重击 4 级 armor_break 应延长到 70 TU。")
		_assert_eq(level_five_staggered_effect.status_id, &"staggered", "重击 5 级应追加 staggered。")
		_assert_eq(int(level_five_staggered_effect.min_skill_level), 5, "重击 staggered 应从 5 级生效。")
		_assert_eq(int(level_five_staggered_effect.duration_tu), 60, "重击 5 级 staggered 应持续 60 TU。")
		_assert_eq(level_five_staggered_effect.trigger_event, &"critical_hit", "重击 5 级 staggered 应只在大成功时触发。")
	_assert_true(sweeping_slash != null, "横扫资源应成功转成 SkillDef。")
	if sweeping_slash != null and sweeping_slash.combat_profile != null:
		_assert_eq(int(sweeping_slash.max_level), 5, "横扫应使用 5 级上限。")
		_assert_eq(int(sweeping_slash.non_core_max_level), 3, "横扫非核心上限应为 3。")
		_assert_eq(Array(sweeping_slash.mastery_curve), [160, 400, 900, 1600, 2600], "横扫熟练度曲线应匹配 intermediate 设计。")
		_assert_eq(sweeping_slash.combat_profile.area_pattern, &"front_arc", "横扫应使用 front_arc 相邻前弧范围。")
		_assert_eq(int(sweeping_slash.combat_profile.area_value), 1, "横扫 front_arc 半径应为 1。")
		_assert_eq(int(sweeping_slash.combat_profile.stamina_cost), 30, "横扫体力消耗应进入当前资源尺度。")
		_assert_eq(int(sweeping_slash.combat_profile.get_effective_resource_costs(2).get("stamina_cost", 0)), 25, "横扫 2 级起体力消耗应降为 25。")
		_assert_eq(int(sweeping_slash.combat_profile.get_effective_resource_costs(4).get("cooldown_tu", 0)), 0, "横扫 4 级起应移除冷却。")
		_assert_eq(sweeping_slash.combat_profile.mastery_trigger_mode, &"weapon_attack_quality", "横扫应使用武器攻击质量熟练度触发模式。")
		_assert_eq(sweeping_slash.combat_profile.mastery_amount_mode, &"per_target_rank", "横扫应按每个目标阶级计算熟练度。")
		_assert_eq(int(sweeping_slash.combat_profile.effect_defs.size()), 3, "横扫应按等级切分三条武器攻击效果。")
		var sweeping_damage = sweeping_slash.combat_profile.effect_defs[0]
		var sweeping_level_three_damage = sweeping_slash.combat_profile.effect_defs[1]
		var sweeping_level_five_damage = sweeping_slash.combat_profile.effect_defs[2]
		_assert_eq(int(sweeping_damage.power), 0, "横扫不应再配置固定技能伤害。")
		_assert_true(bool(sweeping_damage.params.get("add_weapon_dice", false)), "横扫应使用当前武器骰。")
		_assert_true(bool(sweeping_damage.params.get("requires_weapon", false)), "横扫应显式要求装备武器。")
		_assert_true(bool(sweeping_damage.params.get("use_weapon_physical_damage_tag", false)), "横扫应使用当前武器物理伤害类型。")
		_assert_true(bool(sweeping_damage.params.get("resolve_as_weapon_attack", false)), "横扫每个目标应按武器攻击命中结算。")
		_assert_eq(int(sweeping_level_three_damage.params.get("dice_sides", 0)), 4, "横扫 3-4 级应追加 1d4 技能骰。")
		_assert_eq(int(sweeping_level_five_damage.params.get("dice_sides", 0)), 6, "横扫 5 级应追加 1d6 技能骰。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "SkillContentRegistry")


func _test_progression_registry_uses_skill_resources_only() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var heavy_strike := skill_defs.get(&"warrior_heavy_strike") as SkillDef
	var charge := skill_defs.get(&"charge") as SkillDef
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(registry.validate().is_empty(), "ProgressionContentRegistry 接入 skill resource 后仍应通过静态校验。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_COMMON_MELEE_RESOURCE_SKILL_IDS, &"melee", "通用近战")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS, &"warrior", "战士")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_PRIEST_RESOURCE_SKILL_IDS, &"priest", "神术")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_ARCHER_RESOURCE_SKILL_IDS, &"archer", "弓箭手")
	_assert_resource_backed_skill_ids(skill_defs, _collect_mage_skill_ids(skill_defs), &"mage", "法师")
	_assert_true(heavy_strike != null, "ProgressionContentRegistry 应暴露已迁移的重击资源。")
	_assert_true(charge != null, "ProgressionContentRegistry 应暴露正式冲锋资源。")
	if heavy_strike != null:
		_assert_eq(String(heavy_strike.resource_path), OFFICIAL_HEAVY_STRIKE_PATH, "已迁移技能在 ProgressionContentRegistry 中不应被 code seed 覆盖。")
	if charge != null:
		_assert_eq(String(charge.resource_path), OFFICIAL_CHARGE_PATH, "冲锋不应再通过 code seed 兼容桥提供。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "ProgressionContentRegistry")


func _test_attribute_growth_progress_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_skill := _make_growth_schema_skill(&"valid_growth_schema_skill", &"intermediate", {
		"agility": 90,
		"perception": 30,
	})
	var valid_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(valid_errors, valid_skill.skill_id, valid_skill)
	_assert_true(valid_errors.is_empty(), "合法属性进度配置应通过 SkillContentRegistry 校验。")

	var invalid_total_skill := _make_growth_schema_skill(&"invalid_total_growth_schema_skill", &"advanced", {
		"agility": 120,
	})
	var invalid_total_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(invalid_total_errors, invalid_total_skill.skill_id, invalid_total_skill)
	_assert_true(
		_has_error_containing(invalid_total_errors, "attribute_growth_progress total must equal 180"),
		"advanced 技能属性进度总和必须等于 180。"
	)

	var invalid_attribute_skill := _make_growth_schema_skill(&"invalid_attribute_growth_schema_skill", &"basic", {
		"hp_max": 60,
	})
	var invalid_attribute_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(invalid_attribute_errors, invalid_attribute_skill.skill_id, invalid_attribute_skill)
	_assert_true(
		_has_error_containing(invalid_attribute_errors, "references invalid attribute hp_max"),
		"属性进度配置只能引用六项基础属性。"
	)

	var string_name_key_skill := _make_growth_schema_skill(&"string_name_key_growth_schema_skill", &"basic", {
		UnitBaseAttributes.AGILITY: 60,
	})
	var string_name_key_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(string_name_key_errors, string_name_key_skill.skill_id, string_name_key_skill)
	_assert_true(
		_has_error_containing(string_name_key_errors, "attribute_growth_progress key agility must be a non-empty String"),
		"attribute_growth_progress 旧 StringName key 应被 SkillContentRegistry 静态拒绝。"
	)

	var non_string_key_skill := _make_growth_schema_skill(&"non_string_key_growth_schema_skill", &"basic", {
		123: 60,
	})
	var non_string_key_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(non_string_key_errors, non_string_key_skill.skill_id, non_string_key_skill)
	_assert_true(
		_has_error_containing(non_string_key_errors, "attribute_growth_progress key 123 must be a non-empty String"),
		"attribute_growth_progress 非 String key 应被 SkillContentRegistry 静态拒绝。"
	)

	var empty_string_key_skill := _make_growth_schema_skill(&"empty_string_key_growth_schema_skill", &"basic", {
		"": 60,
	})
	var empty_string_key_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(empty_string_key_errors, empty_string_key_skill.skill_id, empty_string_key_skill)
	_assert_true(
		_has_error_containing(empty_string_key_errors, "attribute_growth_progress key  must be a non-empty String"),
		"attribute_growth_progress 空字符串 key 应被 SkillContentRegistry 静态拒绝。"
	)

	var non_int_amount_skill := _make_growth_schema_skill(&"non_int_growth_schema_skill", &"basic", {
		"agility": "60",
	})
	var non_int_amount_errors: Array[String] = []
	registry._append_attribute_growth_validation_errors(non_int_amount_errors, non_int_amount_skill.skill_id, non_int_amount_skill)
	_assert_true(
		_has_error_containing(non_int_amount_errors, "attribute_growth_progress for agility must be a positive int"),
		"attribute_growth_progress value 应拒绝字符串数字。"
	)


func _test_level_override_key_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_profile := CombatSkillDef.new()
	valid_profile.skill_id = &"valid_level_override_key_skill"
	valid_profile.level_overrides = {
		2: {"stamina_cost": 20},
	}
	var valid_errors: Array[String] = []
	registry._append_combat_profile_validation_errors(valid_errors, valid_profile.skill_id, valid_profile)
	_assert_true(valid_errors.is_empty(), "combat_profile.level_overrides 正式 int key 应通过 SkillContentRegistry 校验。")

	var string_key_profile := CombatSkillDef.new()
	string_key_profile.skill_id = &"string_level_override_key_skill"
	string_key_profile.level_overrides = {
		"2": {"stamina_cost": 20},
	}
	var string_key_errors: Array[String] = []
	registry._append_combat_profile_validation_errors(string_key_errors, string_key_profile.skill_id, string_key_profile)
	_assert_true(
		_has_error_containing(string_key_errors, "level override key 2 must be an int"),
		"combat_profile.level_overrides 字符串数字 key 应被 SkillContentRegistry 静态拒绝。"
	)


func _test_requires_weapon_param_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_effect := CombatEffectDef.new()
	valid_effect.effect_type = &"damage"
	valid_effect.params = {
		"requires_weapon": true,
		"resolve_as_weapon_attack": true,
		"use_weapon_physical_damage_tag": true,
	}
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_requires_weapon_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "requires_weapon=true 时允许 use_weapon_physical_damage_tag=true 与 resolve_as_weapon_attack=true。")

	var damage_tag_only_effect := CombatEffectDef.new()
	damage_tag_only_effect.effect_type = &"damage"
	damage_tag_only_effect.params = {
		"use_weapon_physical_damage_tag": true,
	}
	var damage_tag_only_errors: Array[String] = []
	registry._append_effect_validation_errors(damage_tag_only_errors, &"damage_tag_only_skill", damage_tag_only_effect, "test_effect")
	_assert_true(
		damage_tag_only_errors.is_empty(),
		"use_weapon_physical_damage_tag=true 本身只表达伤害标签覆盖，不应被 schema 当成施展条件。"
	)

	var non_bool_effect := CombatEffectDef.new()
	non_bool_effect.effect_type = &"damage"
	non_bool_effect.params = {
		"requires_weapon": "true",
		"use_weapon_physical_damage_tag": true,
	}
	var non_bool_errors: Array[String] = []
	registry._append_effect_validation_errors(non_bool_errors, &"invalid_requires_weapon_type_skill", non_bool_effect, "test_effect")
	_assert_true(
		_has_error_containing(non_bool_errors, "params.requires_weapon must be a bool"),
		"requires_weapon schema 应拒绝非 bool 值。"
	)

	var non_bool_damage_tag_effect := CombatEffectDef.new()
	non_bool_damage_tag_effect.effect_type = &"damage"
	non_bool_damage_tag_effect.params = {
		"use_weapon_physical_damage_tag": "true",
	}
	var non_bool_damage_tag_errors: Array[String] = []
	registry._append_effect_validation_errors(non_bool_damage_tag_errors, &"invalid_weapon_damage_tag_type_skill", non_bool_damage_tag_effect, "test_effect")
	_assert_true(
		_has_error_containing(non_bool_damage_tag_errors, "params.use_weapon_physical_damage_tag must be a bool"),
		"use_weapon_physical_damage_tag schema 应拒绝非 bool 值。"
	)

	var non_bool_weapon_attack_effect := CombatEffectDef.new()
	non_bool_weapon_attack_effect.effect_type = &"damage"
	non_bool_weapon_attack_effect.params = {
		"resolve_as_weapon_attack": "true",
	}
	var non_bool_weapon_attack_errors: Array[String] = []
	registry._append_effect_validation_errors(non_bool_weapon_attack_errors, &"invalid_weapon_attack_type_skill", non_bool_weapon_attack_effect, "test_effect")
	_assert_true(
		_has_error_containing(non_bool_weapon_attack_errors, "params.resolve_as_weapon_attack must be a bool"),
		"resolve_as_weapon_attack schema 应拒绝非 bool 值。"
	)

	var invalid_trigger_effect := CombatEffectDef.new()
	invalid_trigger_effect.effect_type = &"status"
	invalid_trigger_effect.status_id = &"staggered"
	invalid_trigger_effect.trigger_event = &"ordinary_hit"
	var invalid_trigger_errors: Array[String] = []
	registry._append_effect_validation_errors(invalid_trigger_errors, &"invalid_trigger_skill", invalid_trigger_effect, "test_effect")
	_assert_true(
		_has_error_containing(invalid_trigger_errors, "unsupported trigger_event"),
		"trigger_event schema 应拒绝未支持的触发事件。"
	)


func _test_duration_param_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_effect := CombatEffectDef.new()
	valid_effect.effect_type = &"status"
	valid_effect.status_id = &"pinned"
	valid_effect.params = {
		"duration_tu": 15,
	}
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_duration_tu_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "params.duration_tu 仍是当前允许的正式 TU 时长字段。")

	var legacy_duration_effect := CombatEffectDef.new()
	legacy_duration_effect.effect_type = &"status"
	legacy_duration_effect.status_id = &"pinned"
	legacy_duration_effect.params = {
		"duration": 15,
	}
	var legacy_duration_errors: Array[String] = []
	registry._append_effect_validation_errors(legacy_duration_errors, &"legacy_duration_skill", legacy_duration_effect, "test_effect")
	_assert_true(
		_has_error_containing(legacy_duration_errors, "params.duration is unsupported; use duration_tu"),
		"params.duration 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)


func _test_damage_dice_alias_param_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var legacy_dice_effect := CombatEffectDef.new()
	legacy_dice_effect.effect_type = &"damage"
	legacy_dice_effect.params = {
		"damage_dice_count": 1,
		"damage_dice_sides": 6,
		"damage_dice_bonus": 2,
	}
	var legacy_dice_errors: Array[String] = []
	registry._append_effect_validation_errors(legacy_dice_errors, &"legacy_damage_dice_skill", legacy_dice_effect, "test_effect")
	_assert_true(
		_has_error_containing(legacy_dice_errors, "params.damage_dice_count is unsupported; use dice_count"),
		"damage_dice_count 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_dice_errors, "params.damage_dice_sides is unsupported; use dice_sides"),
		"damage_dice_sides 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_dice_errors, "params.damage_dice_bonus is unsupported; use dice_bonus"),
		"damage_dice_bonus 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)


func _test_damage_resolver_alias_param_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_effect := CombatEffectDef.new()
	valid_effect.effect_type = &"damage"
	valid_effect.bonus_condition = &"target_low_hp"
	valid_effect.params = {
		"damage_tag": "fire",
		"dr_bypass_tag": "armor_pierce",
		"hp_ratio_threshold": 0.6,
	}
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_damage_resolver_params_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "正式 damage_tag / dr_bypass_tag / hp_ratio_threshold params 应通过 schema。")

	var legacy_effect := CombatEffectDef.new()
	legacy_effect.effect_type = &"damage"
	legacy_effect.params = {
		"tag": "fire",
		"bypass_tag": "armor_pierce",
		"low_hp_ratio": 0.6,
	}
	var legacy_errors: Array[String] = []
	registry._append_effect_validation_errors(legacy_errors, &"legacy_damage_resolver_params_skill", legacy_effect, "test_effect")
	_assert_true(
		_has_error_containing(legacy_errors, "params.tag is unsupported; use damage_tag"),
		"params.tag 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_errors, "params.bypass_tag is unsupported; use dr_bypass_tag"),
		"params.bypass_tag 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_errors, "params.low_hp_ratio is unsupported; use hp_ratio_threshold"),
		"params.low_hp_ratio 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)


func _test_forced_move_param_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_effect := CombatEffectDef.new()
	valid_effect.effect_type = &"forced_move"
	valid_effect.forced_move_mode = &"retreat"
	valid_effect.forced_move_distance = 2
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_forced_move_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "正式 forced_move_mode / forced_move_distance 应通过 schema。")

	var legacy_effect := CombatEffectDef.new()
	legacy_effect.effect_type = &"forced_move"
	legacy_effect.params = {
		"mode": "retreat",
		"distance": 2,
	}
	var legacy_errors: Array[String] = []
	registry._append_effect_validation_errors(legacy_errors, &"legacy_forced_move_skill", legacy_effect, "test_effect")
	_assert_true(
		_has_error_containing(legacy_errors, "params.mode is unsupported; use forced_move_mode"),
		"forced_move params.mode 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_errors, "params.distance is unsupported; use forced_move_distance"),
		"forced_move params.distance 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)
	_assert_true(
		_has_error_containing(legacy_errors, "is missing forced_move_mode"),
		"只提供旧 params.mode 时仍应报告缺少正式 forced_move_mode。"
	)
	_assert_true(
		_has_error_containing(legacy_errors, "must have forced_move_distance >= 1"),
		"只提供旧 params.distance 时仍应报告缺少正式 forced_move_distance。"
	)


func _test_unknown_effect_type_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var unknown_effect := CombatEffectDef.new()
	unknown_effect.effect_type = &"unknown_effect_contract"

	var errors: Array[String] = []
	registry._append_effect_validation_errors(errors, &"unknown_effect_skill", unknown_effect, "test_effect")
	_assert_true(
		_has_error_containing(errors, "uses unsupported effect_type unknown_effect_contract"),
		"SkillContentRegistry 应拒绝未知 effect_type，而不是让未知效果静默通过。"
	)


func _test_supported_effect_type_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var apply_status_effect := CombatEffectDef.new()
	apply_status_effect.effect_type = &"apply_status"
	apply_status_effect.status_id = &"pinned"
	var apply_status_errors: Array[String] = []
	registry._append_effect_validation_errors(apply_status_errors, &"apply_status_skill", apply_status_effect, "test_effect")
	_assert_true(apply_status_errors.is_empty(), "运行时支持的 apply_status effect_type 应通过 schema。")

	var terrain_effect := CombatEffectDef.new()
	terrain_effect.effect_type = &"terrain_replace_to"
	terrain_effect.terrain_replace_to = &"mud"
	var terrain_errors: Array[String] = []
	registry._append_effect_validation_errors(terrain_errors, &"terrain_replace_to_skill", terrain_effect, "test_effect")
	_assert_true(terrain_errors.is_empty(), "运行时支持的 terrain_replace_to effect_type 应通过 schema。")

	var height_effect := CombatEffectDef.new()
	height_effect.effect_type = &"height"
	height_effect.height_delta = -1
	var height_errors: Array[String] = []
	registry._append_effect_validation_errors(height_errors, &"height_skill", height_effect, "test_effect")
	_assert_true(height_errors.is_empty(), "运行时支持的 height effect_type 应通过 schema。")

	var invalid_height_effect := CombatEffectDef.new()
	invalid_height_effect.effect_type = &"height_delta"
	var invalid_height_errors: Array[String] = []
	registry._append_effect_validation_errors(invalid_height_errors, &"invalid_height_skill", invalid_height_effect, "test_effect")
	_assert_true(
		_has_error_containing(invalid_height_errors, "must have non-zero height_delta"),
		"height / height_delta schema 应拒绝零变化效果。"
	)


func _test_skill_registry_reports_missing_id_duplicate_schema_and_illegal_refs() -> void:
	var skill_registry := SkillContentRegistry.new()
	skill_registry._skill_defs.clear()
	skill_registry._validation_errors.clear()
	skill_registry._scan_directory("res://tests/progression/fixtures/skill_registry_invalid")
	skill_registry._validation_errors.append_array(skill_registry._collect_validation_errors())

	var validation_errors := skill_registry.validate()
	var progression_registry := ProgressionContentRegistry.new()
	progression_registry._skill_defs = skill_registry.get_skill_defs().duplicate()
	progression_registry._achievement_defs.clear()
	progression_registry._quest_defs.clear()
	validation_errors.append_array(progression_registry._collect_validation_errors())

	_assert_true(
		_has_error_containing(validation_errors, "is missing skill_id"),
		"技能注册表应显式报告缺失 skill_id。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "Duplicate skill_id registered: duplicate_skill"),
		"技能注册表应显式报告重复 skill_id。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "effect without effect_type"),
		"技能注册表应显式报告嵌套 effect 缺失 effect_type。"
	)
	_assert_true(
		_has_error_containing(validation_errors, "references missing skill missing_skill"),
		"技能注册表应显式报告非法技能引用。"
	)


func _assert_resource_backed_skill_ids(
	skill_defs: Dictionary,
	skill_ids: Array[StringName],
	expected_tag: StringName,
	label: String
) -> void:
	for skill_id in skill_ids:
		var skill_def := skill_defs.get(skill_id) as SkillDef
		_assert_true(skill_def != null, "%s技能 %s 应已迁到正式 SkillDef resource。" % [label, String(skill_id)])
		if skill_def == null:
			continue
		_assert_true(
			String(skill_def.resource_path).begins_with(OFFICIAL_SKILL_RESOURCE_DIRECTORY),
			"%s技能 %s 应从 data/configs/skills 正式资源加载。" % [label, String(skill_id)]
		)
		_assert_true(
			skill_def.tags.has(expected_tag),
			"%s技能 %s 应保留 %s 标签。" % [label, String(skill_id), String(expected_tag)]
		)


func _collect_mage_skill_ids(skill_defs: Dictionary) -> Array[StringName]:
	var skill_ids: Array[StringName] = []
	for skill_key in skill_defs.keys():
		var skill_def := skill_defs.get(skill_key) as SkillDef
		if skill_def == null:
			continue
		if not skill_def.tags.has(&"mage"):
			continue
		skill_ids.append(StringName(skill_key))
	return skill_ids


func _assert_cast_variant_compat_shape(skill_def: SkillDef, source_label: String) -> void:
	_assert_true(skill_def != null, "%s 应提供 mage_fossil_to_mud。" % source_label)
	if skill_def == null:
		return
	_assert_true(skill_def.combat_profile != null, "%s 的 mage_fossil_to_mud 应保留 combat_profile。" % source_label)
	if skill_def.combat_profile == null:
		return
	_assert_eq(skill_def.combat_profile.target_mode, &"ground", "%s 的 mage_fossil_to_mud 应保持 ground 目标模式。" % source_label)
	_assert_eq(int(skill_def.combat_profile.effect_defs.size()), 0, "%s 的根级 cast_variant 兼容归一后不应残留根级 effect_defs。" % source_label)
	_assert_eq(int(skill_def.combat_profile.cast_variants.size()), 4, "%s 的 mage_fossil_to_mud 应保留 4 个 cast_variant。" % source_label)
	_assert_cast_variant_compat_entry(skill_def, 0, &"mud_single", &"single", 1, source_label)
	_assert_cast_variant_compat_entry(skill_def, 1, &"lower_single_1", &"single", 1, source_label)
	_assert_cast_variant_compat_entry(skill_def, 2, &"lower_line2_1", &"line2", 2, source_label)
	_assert_cast_variant_compat_entry(skill_def, 3, &"mud_square2", &"square2", 4, source_label)


func _make_growth_schema_skill(
	skill_id: StringName,
	growth_tier: StringName,
	attribute_growth_progress: Dictionary
) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.skill_type = &"passive"
	skill_def.max_level = 1
	skill_def.mastery_curve = PackedInt32Array([1])
	skill_def.growth_tier = growth_tier
	skill_def.attribute_growth_progress = attribute_growth_progress.duplicate(true)
	return skill_def


func _assert_cast_variant_compat_entry(
	skill_def: SkillDef,
	index: int,
	expected_variant_id: StringName,
	expected_footprint_pattern: StringName,
	expected_coord_count: int,
	source_label: String
) -> void:
	if skill_def == null or skill_def.combat_profile == null:
		return
	if index >= skill_def.combat_profile.cast_variants.size():
		_failures.append("%s 的 mage_fossil_to_mud 缺少 cast_variant[%d]。" % [source_label, index])
		return
	var cast_variant = skill_def.combat_profile.cast_variants[index]
	_assert_true(cast_variant != null, "%s 的 mage_fossil_to_mud cast_variant[%d] 不应为空。" % [source_label, index])
	if cast_variant == null:
		return
	_assert_eq(cast_variant.variant_id, expected_variant_id, "%s 的 mage_fossil_to_mud cast_variant[%d] 应保留稳定 variant_id。" % [source_label, index])
	_assert_eq(cast_variant.target_mode, &"ground", "%s 的 mage_fossil_to_mud cast_variant[%d] 应归一为 ground。" % [source_label, index])
	_assert_eq(cast_variant.footprint_pattern, expected_footprint_pattern, "%s 的 mage_fossil_to_mud cast_variant[%d] 应保留 footprint_pattern。" % [source_label, index])
	_assert_eq(int(cast_variant.required_coord_count), expected_coord_count, "%s 的 mage_fossil_to_mud cast_variant[%d] 应保留 required_coord_count。" % [source_label, index])
	_assert_true(not cast_variant.effect_defs.is_empty(), "%s 的 mage_fossil_to_mud cast_variant[%d] 应保留 effect_defs。" % [source_label, index])


func _has_error_containing(errors: Array[String], expected_fragment: String) -> bool:
	for validation_error in errors:
		if validation_error.contains(expected_fragment):
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
