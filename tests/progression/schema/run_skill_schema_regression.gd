extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const SkillContentRegistry = preload("res://scripts/player/progression/skill_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const OFFICIAL_SKILL_RESOURCE_DIRECTORY := "res://data/configs/skills/"
const OFFICIAL_CHARGE_PATH := "res://data/configs/skills/charge.tres"
const OFFICIAL_HEAVY_STRIKE_PATH := "res://data/configs/skills/warrior_heavy_strike.tres"
const OFFICIAL_COMMON_MELEE_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"charge",
]
const OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"warrior_toughness",
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
	&"archer_shooting_specialization",
	&"archer_multishot",
	&"archer_arrow_rain",
	&"archer_fan_volley",
	&"archer_suppressive_fire",
	&"archer_breach_barrage",
	&"archer_blast_arrow",
	&"archer_hunting_grid",
	&"archer_killing_field",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_seed_skill_resources_scan_and_validate()
	_test_mage_damage_tags_do_not_use_generic_magic()
	_test_progression_registry_uses_skill_resources_only()
	_test_attribute_growth_progress_schema_validation()
	_test_dynamic_max_level_schema_validation()
	_test_level_override_key_schema_validation()
	_test_required_weapon_family_schema_validation()
	_test_requires_weapon_param_schema_validation()
	_test_duration_param_schema_validation()
	_test_damage_dice_alias_param_schema_validation()
	_test_damage_resolver_alias_param_schema_validation()
	_test_path_step_repeat_status_schema_validation()
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
	var warrior_toughness := skill_defs.get(&"warrior_toughness") as SkillDef
	var sweeping_slash := skill_defs.get(&"warrior_sweeping_slash") as SkillDef
	var aura_slash := skill_defs.get(&"warrior_aura_slash") as SkillDef
	var archer_multishot := skill_defs.get(&"archer_multishot") as SkillDef
	var archer_arrow_rain := skill_defs.get(&"archer_arrow_rain") as SkillDef
	var archer_shooting_specialization := skill_defs.get(&"archer_shooting_specialization") as SkillDef
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(registry.validate().is_empty(), "SkillContentRegistry 的正式技能资源当前不应报告校验错误。")
	_assert_true(basic_attack != null, "SkillContentRegistry 应扫描到内建基础攻击资源。")
	if basic_attack != null:
		_assert_eq(basic_attack.display_name, "攻击", "基础攻击应保留展示名。")
		_assert_true(basic_attack.tags.has(&"basic"), "基础攻击应带 basic 标签。")
		_assert_true(basic_attack.can_use_in_combat(), "基础攻击应可在战斗中使用。")
		_assert_true(basic_attack.combat_profile != null, "基础攻击应保留 combat_profile。")
		if basic_attack.combat_profile != null:
			_assert_eq(int(basic_attack.combat_profile.stamina_cost), 8, "基础攻击体力消耗应为 8。")
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
			_assert_eq(int(charge.combat_profile.stamina_cost), 50, "冲锋 0 级体力消耗应为 50。")
			_assert_eq(int(charge.combat_profile.cooldown_tu), 50, "冲锋基础冷却应保留 50 TU。")
			_assert_eq(int(charge.combat_profile.get_effective_resource_costs(1).get("stamina_cost", 0)), 50, "冲锋 1 级体力消耗应跟随基础值涨为 50。")
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
	_assert_true(warrior_toughness != null, "强健资源应成功转成 SkillDef。")
	if warrior_toughness != null:
		_assert_eq(warrior_toughness.display_name, "强健", "强健应保留展示名。")
		_assert_eq(warrior_toughness.skill_type, &"passive", "强健应是被动技能。")
		_assert_eq(warrior_toughness.learn_source, &"profession", "强健只能由职业授予。")
		_assert_eq(int(warrior_toughness.max_level), 1, "强健上限应为 1。")
		_assert_eq(Array(warrior_toughness.mastery_curve), [1], "强健应保留 1 档占位熟练度曲线。")
		_assert_eq(int(warrior_toughness.attribute_modifiers.size()), 2, "强健应配置人物生命和体力恢复两条修正。")
		if int(warrior_toughness.attribute_modifiers.size()) >= 2:
			var toughness_hp_modifier := warrior_toughness.attribute_modifiers[0]
			_assert_eq(toughness_hp_modifier.attribute_id, AttributeService.CHARACTER_HP_MAX_PERCENT_BONUS, "强健应写入人物生命百分比通道。")
			_assert_eq(toughness_hp_modifier.mode, &"flat", "人物生命百分比通道应使用 flat 数值表达百分比点。")
			_assert_eq(int(toughness_hp_modifier.value), 20, "强健应提供 20% 人物生命加成。")
			var toughness_stamina_modifier := warrior_toughness.attribute_modifiers[1]
			_assert_eq(toughness_stamina_modifier.attribute_id, AttributeService.STAMINA_RECOVERY_PERCENT_BONUS, "强健应写入体力恢复百分比通道。")
			_assert_eq(toughness_stamina_modifier.mode, &"flat", "体力恢复百分比通道应使用 flat 数值表达百分比点。")
			_assert_eq(int(toughness_stamina_modifier.value), 50, "强健应提供 50% 体力恢复加成。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_PRIEST_RESOURCE_SKILL_IDS, &"priest", "神术")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_ARCHER_RESOURCE_SKILL_IDS, &"archer", "弓箭手")
	_assert_true(archer_shooting_specialization != null, "射击专精资源应成功转成 SkillDef。")
	if archer_shooting_specialization != null:
		_assert_eq(archer_shooting_specialization.display_name, "射击专精", "射击专精应保留展示名。")
		_assert_eq(archer_shooting_specialization.skill_type, &"passive", "射击专精应是被动技能。")
		_assert_eq(archer_shooting_specialization.learn_source, &"profession", "射击专精只能由职业授予。")
		_assert_eq(int(archer_shooting_specialization.max_level), 1, "射击专精上限应为 1。")
		_assert_eq(Array(archer_shooting_specialization.mastery_curve), [1], "射击专精应保留 1 档占位熟练度曲线。")
		_assert_true(archer_shooting_specialization.combat_profile != null, "射击专精应保留被动 combat_profile。")
		if archer_shooting_specialization.combat_profile != null:
			_assert_eq(int(archer_shooting_specialization.combat_profile.passive_effect_defs.size()), 1, "射击专精应配置一个 battle_start 被动效果。")
			var specialization_effect = archer_shooting_specialization.combat_profile.passive_effect_defs[0]
			_assert_eq(specialization_effect.status_id, &"archer_shooting_specialization", "射击专精应投影专用射程状态。")
			_assert_eq(int(specialization_effect.params.get("range_bonus", 0)), 1, "射击专精应提供 +1 射程。")
	_assert_true(archer_multishot != null, "连珠箭资源应成功转成 SkillDef。")
	if archer_multishot != null and archer_multishot.combat_profile != null:
		_assert_eq(int(archer_multishot.max_level), 7, "连珠箭应使用 7 级上限。")
		_assert_eq(int(archer_multishot.non_core_max_level), 5, "连珠箭非核心上限应显式覆盖 5 级。")
		_assert_eq(Array(archer_multishot.mastery_curve), [300, 750, 1650, 3000, 4800, 7050, 9750], "连珠箭熟练度曲线应覆盖 7 级。")
		_assert_eq(archer_multishot.growth_tier, &"intermediate", "连珠箭应使用 intermediate 成长档。")
		_assert_eq(int(archer_multishot.attribute_growth_progress.get("agility", 0)), 80, "连珠箭满级应提供 80 点敏捷成长进度。")
		_assert_eq(int(archer_multishot.attribute_growth_progress.get("strength", 0)), 40, "连珠箭满级应提供 40 点力量成长进度。")
		_assert_eq(int(archer_multishot.attribute_growth_progress.get("perception", 0)), 0, "连珠箭不应再提供感知成长进度。")
		_assert_eq(archer_multishot.combat_profile.mastery_trigger_mode, &"weapon_attack_quality", "连珠箭应按武器满骰或暴击触发熟练度。")
		_assert_eq(archer_multishot.combat_profile.mastery_amount_mode, &"per_target_rank", "连珠箭应按每个目标阶级计算熟练度。")
		_assert_eq(Array(archer_multishot.combat_profile.required_weapon_families), [&"bow"], "连珠箭应要求当前装备弓类武器。")
		_assert_eq(int(archer_multishot.combat_profile.ap_cost), 1, "连珠箭应消耗 1 AP。")
		_assert_eq(int(archer_multishot.combat_profile.stamina_cost), 45, "连珠箭应消耗 45 体力。")
		_assert_eq(int(archer_multishot.combat_profile.cooldown_tu), 0, "连珠箭不应配置冷却。")
		_assert_eq(int(archer_multishot.combat_profile.min_target_count), 1, "连珠箭应允许单目标施放。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_max_target_count(0)), 2, "连珠箭 0 级最多 2 个目标。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_max_target_count(1)), 3, "连珠箭 1 级最多 3 个目标。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_max_target_count(3)), 4, "连珠箭 3 级最多 4 个目标。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_max_target_count(5)), 5, "连珠箭 5 级最多 5 个目标。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_attack_roll_bonus(0)), -1, "连珠箭 0 级应保留多目标攻击检定惩罚。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_attack_roll_bonus(2)), 0, "连珠箭 2 级应移除攻击检定惩罚。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_attack_roll_bonus(4)), 1, "连珠箭 4 级应获得攻击检定加值。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_attack_roll_bonus(6)), 2, "连珠箭 6 级应获得 +2 攻击检定加值。")
		_assert_eq(int(archer_multishot.combat_profile.get_effective_resource_costs(7).get("stamina_cost", 0)), 40, "连珠箭 7 级体力消耗应降为 40。")
		var multishot_variant = archer_multishot.combat_profile.get_cast_variant(&"multishot_volley")
		_assert_true(multishot_variant != null, "连珠箭应保留 multishot_volley 施放变体。")
		if multishot_variant != null:
			_assert_eq(int(multishot_variant.effect_defs.size()), 1, "连珠箭应只保留一条武器攻击伤害效果。")
			if multishot_variant.effect_defs.size() >= 1:
				var multishot_damage = multishot_variant.effect_defs[0]
				_assert_eq(int(multishot_damage.power), 0, "连珠箭不应配置固定技能伤害。")
				_assert_true(bool(multishot_damage.params.get("add_weapon_dice", false)), "连珠箭应使用当前弓的武器骰。")
				_assert_true(bool(multishot_damage.params.get("use_weapon_physical_damage_tag", false)), "连珠箭应使用当前弓的物理伤害类型。")
				_assert_true(bool(multishot_damage.params.get("resolve_as_weapon_attack", false)), "连珠箭每个目标应按武器攻击命中结算。")
				_assert_true(not bool(multishot_damage.params.get("requires_weapon", false)), "连珠箭应由 required_weapon_families 表达弓类门槛。")
				_assert_true(not multishot_damage.params.has("dice_count"), "连珠箭不应配置额外技能伤害骰。")
				_assert_true(not multishot_damage.params.has("dice_sides"), "连珠箭不应配置额外技能伤害骰。")
	_assert_true(archer_arrow_rain != null, "箭雨资源应成功转成 SkillDef。")
	if archer_arrow_rain != null and archer_arrow_rain.combat_profile != null:
		_assert_eq(int(archer_arrow_rain.max_level), 7, "箭雨应使用 7 级上限。")
		_assert_eq(Array(archer_arrow_rain.combat_profile.required_weapon_families), [&"bow"], "箭雨应要求当前装备弓类武器。")
		_assert_eq(int(archer_arrow_rain.combat_profile.ap_cost), 1, "箭雨应消耗 1 AP。")
		_assert_eq(int(archer_arrow_rain.combat_profile.stamina_cost), 50, "箭雨应消耗 50 体力。")
		_assert_eq(int(archer_arrow_rain.combat_profile.cooldown_tu), 90, "箭雨应保留 90 TU 冷却。")
		_assert_eq(int(archer_arrow_rain.combat_profile.attack_roll_bonus), -1, "箭雨攻击检定惩罚应降为 -1。")
		_assert_eq(int(archer_arrow_rain.combat_profile.effect_defs.size()), 5, "箭雨应由一条武器伤害和四档压制地形效果组成。")
		var arrow_rain_damage = archer_arrow_rain.combat_profile.effect_defs[0]
		_assert_eq(arrow_rain_damage.effect_type, &"damage", "箭雨首段效果应是武器伤害。")
		_assert_true(bool(arrow_rain_damage.params.get("add_weapon_dice", false)), "箭雨应使用当前弓的武器骰。")
		_assert_true(bool(arrow_rain_damage.params.get("use_weapon_physical_damage_tag", false)), "箭雨应使用当前弓的物理伤害类型。")
		_assert_true(bool(arrow_rain_damage.params.get("resolve_as_weapon_attack", false)), "箭雨范围内每个目标应按武器攻击命中结算。")
		_assert_true(not arrow_rain_damage.params.has("requires_weapon"), "箭雨应由 required_weapon_families 表达弓类门槛。")
		var suppression_durations := {}
		var suppression_count := 0
		var has_unit_slow := false
		for effect_def in archer_arrow_rain.combat_profile.effect_defs:
			if effect_def.effect_type == &"status" and effect_def.status_id == &"slow":
				has_unit_slow = true
			if effect_def.effect_type != &"terrain_effect":
				continue
			suppression_count += 1
			_assert_eq(effect_def.terrain_effect_id, &"arrow_rain_suppression", "箭雨地形效果应使用稳定压制 effect id。")
			_assert_eq(effect_def.tick_effect_type, &"movement_cost", "箭雨压制地形只应修改移动成本，不应额外 tick 伤害或状态。")
			_assert_eq(int(effect_def.tick_interval_tu), 5, "箭雨压制地形应按 5 TU 粒度过期。")
			_assert_eq(int(effect_def.params.get("move_cost_delta", 0)), 1, "箭雨压制地形应只增加 1 点移动成本。")
			_assert_eq(String(effect_def.params.get("does_not_stack_with_status_id", "")), "slow", "箭雨压制地形不应和单位 slow 叠加。")
			suppression_durations[int(effect_def.min_skill_level)] = int(effect_def.duration_tu)
		_assert_true(not has_unit_slow, "路线B下箭雨不应再配置单位 slow 状态效果。")
		_assert_eq(suppression_count, 4, "箭雨压制地形应按 0/2/4/6 四档配置。")
		_assert_eq(int(suppression_durations.get(0, 0)), 30, "箭雨 0-1 级压制地形应持续 30 TU。")
		_assert_eq(int(suppression_durations.get(2, 0)), 35, "箭雨 2-3 级压制地形应持续 35 TU。")
		_assert_eq(int(suppression_durations.get(4, 0)), 40, "箭雨 4-5 级压制地形应持续 40 TU。")
		_assert_eq(int(suppression_durations.get(6, 0)), 45, "箭雨 6-7 级压制地形应持续 45 TU。")
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
		_assert_eq(int(heavy_strike.combat_profile.effect_defs.size()), 7, "重击资源应保留七条分级 effect_defs。")
		_assert_eq(int(heavy_strike.combat_profile.ap_cost), 1, "重击资源应保留 1 点 AP 消耗。")
		_assert_eq(int(heavy_strike.combat_profile.stamina_cost), 30, "重击资源应保留 30 点体力消耗。")
		_assert_eq(int(heavy_strike.combat_profile.get_effective_resource_costs(2).get("stamina_cost", 0)), 20, "重击 2 级起体力消耗应降为 20。")
		_assert_eq(int(heavy_strike.combat_profile.cooldown_tu), 0, "重击资源不应配置冷却。")
		_assert_eq(int(heavy_strike.combat_profile.attack_roll_bonus), 1, "重击 0 级攻击检定修正应为 +1。")
		_assert_eq(int(heavy_strike.combat_profile.get_effective_attack_roll_bonus(3)), 1, "重击 3 级前应保持 +1 攻击检定修正。")
		_assert_eq(int(heavy_strike.combat_profile.get_effective_attack_roll_bonus(4)), 2, "重击 4 级攻击检定修正应为 +2。")
		_assert_eq(int(heavy_strike.combat_profile.get_effective_attack_roll_bonus(5)), 3, "重击 5 级攻击检定修正应为 +3。")
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
		_assert_eq(level_five_staggered_effect.trigger_event, &"secondary_hit", "重击 5 级 staggered 应在二次命中成功时触发。")
		var level_five_damage = heavy_strike.combat_profile.effect_defs[6]
		_assert_eq(int(level_five_damage.params.get("dice_count", 0)), 2, "重击 5 级伤害骰应为 2 颗。")
		_assert_eq(int(level_five_damage.params.get("dice_sides", 0)), 5, "重击 5 级伤害骰应为 2D5。")
		_assert_eq(int(level_five_damage.min_skill_level), 5, "重击 2D5 伤害效果应从 5 级生效。")
		_assert_true(bool(level_five_damage.params.get("requires_weapon", false)), "重击 5 级武器伤害标签效果必须显式要求装备武器。")
		_assert_true(bool(level_five_damage.params.get("use_weapon_physical_damage_tag", false)), "重击 5 级应使用当前武器物理伤害标签。")
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
	_assert_true(aura_slash != null, "斗气斩资源应成功转成 SkillDef。")
	if aura_slash != null and aura_slash.combat_profile != null:
		_assert_eq(int(aura_slash.max_level), 7, "斗气斩资源默认核心上限应为 7。")
		_assert_eq(int(aura_slash.non_core_max_level), 5, "斗气斩非核心上限应为 5。")
		_assert_eq(aura_slash.dynamic_max_level_stat_id, &"aura_transformation_count", "斗气斩动态上限应从斗气质变次数读取。")
		_assert_eq(int(aura_slash.dynamic_max_level_base), 7, "斗气斩动态上限基础值应为 7。")
		_assert_eq(int(aura_slash.dynamic_max_level_per_stat), 2, "斗气斩每次斗气质变应提高 2 级上限。")
		_assert_eq(Array(aura_slash.mastery_curve), [240, 600, 1320, 2400, 3840, 5640, 7800], "斗气斩默认熟练度曲线应覆盖 7 级。")
		_assert_eq(int(aura_slash.combat_profile.aura_cost), 1, "斗气斩应消耗 1 点斗气。")
		_assert_eq(int(aura_slash.combat_profile.get_effective_resource_costs(5).get("ap_cost", 0)), 1, "斗气斩 5 级起 AP 消耗应降为 1。")
		_assert_eq(int(aura_slash.combat_profile.effect_defs.size()), 3, "斗气斩应保留三段伤害效果。")
		var aura_level_five_damage = aura_slash.combat_profile.effect_defs[2]
		_assert_eq(int(aura_level_five_damage.min_skill_level), 5, "斗气斩最终伤害档应从 5 级起生效。")
		_assert_eq(int(aura_level_five_damage.params.get("dice_sides", 0)), 10, "斗气斩 5 级起应使用 1d10 技能骰。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "SkillContentRegistry")


func _test_mage_damage_tags_do_not_use_generic_magic() -> void:
	var registry := SkillContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	for skill_id in _collect_mage_skill_ids(skill_defs):
		var skill_def := skill_defs.get(skill_id) as SkillDef
		for effect_def in _collect_skill_effect_defs(skill_def):
			if effect_def == null or effect_def.effect_type != &"damage":
				continue
			_assert_true(
				effect_def.damage_tag != &"magic",
				"法师技能 %s 的伤害效果不应使用 generic magic damage_tag。" % String(skill_id)
			)


func _test_dynamic_max_level_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_skill := _make_minimal_schema_skill(&"valid_dynamic_max_level_skill")
	valid_skill.dynamic_max_level_stat_id = &"aura_transformation_count"
	valid_skill.dynamic_max_level_base = 7
	valid_skill.dynamic_max_level_per_stat = 2
	var valid_errors: Array[String] = []
	registry._append_skill_validation_errors(valid_errors, valid_skill.skill_id, valid_skill)
	_assert_true(valid_errors.is_empty(), "合法动态等级上限配置应通过 SkillContentRegistry 校验。")

	var valid_divisor_skill := _make_minimal_schema_skill(&"valid_dynamic_max_level_divisor_skill")
	valid_divisor_skill.dynamic_max_level_stat_id = &"profession_rank:mage"
	valid_divisor_skill.dynamic_max_level_base = 5
	valid_divisor_skill.dynamic_max_level_per_stat = -2
	var valid_divisor_errors: Array[String] = []
	registry._append_skill_validation_errors(valid_divisor_errors, valid_divisor_skill.skill_id, valid_divisor_skill)
	_assert_true(valid_divisor_errors.is_empty(), "合法动态等级上限整除配置应通过 SkillContentRegistry 校验。")

	var missing_stat_skill := _make_minimal_schema_skill(&"missing_dynamic_max_level_stat_skill")
	missing_stat_skill.dynamic_max_level_base = 7
	missing_stat_skill.dynamic_max_level_per_stat = 2
	var missing_stat_errors: Array[String] = []
	registry._append_skill_validation_errors(missing_stat_errors, missing_stat_skill.skill_id, missing_stat_skill)
	_assert_true(
		_has_error_containing(missing_stat_errors, "dynamic_max_level_base requires dynamic_max_level_stat_id"),
		"动态上限基础值缺少 stat id 时应被静态拒绝。"
	)
	_assert_true(
		_has_error_containing(missing_stat_errors, "dynamic_max_level_per_stat requires dynamic_max_level_stat_id"),
		"动态上限增量缺少 stat id 时应被静态拒绝。"
	)

	var invalid_base_skill := _make_minimal_schema_skill(&"invalid_dynamic_max_level_base_skill")
	invalid_base_skill.dynamic_max_level_stat_id = &"aura_transformation_count"
	invalid_base_skill.dynamic_max_level_base = 0
	invalid_base_skill.dynamic_max_level_per_stat = 2
	var invalid_base_errors: Array[String] = []
	registry._append_skill_validation_errors(invalid_base_errors, invalid_base_skill.skill_id, invalid_base_skill)
	_assert_true(
		_has_error_containing(invalid_base_errors, "dynamic_max_level_base must be >= 1"),
		"动态上限基础值必须为正数。"
	)

	var invalid_per_stat_skill := _make_minimal_schema_skill(&"invalid_dynamic_max_level_per_stat_skill")
	invalid_per_stat_skill.dynamic_max_level_stat_id = &"aura_transformation_count"
	invalid_per_stat_skill.dynamic_max_level_base = 7
	invalid_per_stat_skill.dynamic_max_level_per_stat = 0
	var invalid_per_stat_errors: Array[String] = []
	registry._append_skill_validation_errors(invalid_per_stat_errors, invalid_per_stat_skill.skill_id, invalid_per_stat_skill)
	_assert_true(
		_has_error_containing(invalid_per_stat_errors, "dynamic_max_level_per_stat must not be 0"),
		"动态上限每点增量不能为 0。"
	)


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


func _test_required_weapon_family_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_profile := CombatSkillDef.new()
	valid_profile.skill_id = &"valid_required_weapon_family_skill"
	valid_profile.required_weapon_families = [&"bow"]
	var valid_errors: Array[String] = []
	registry._append_combat_profile_validation_errors(valid_errors, valid_profile.skill_id, valid_profile)
	_assert_true(valid_errors.is_empty(), "required_weapon_families 应允许非空 StringName 武器家族。")

	var empty_family_profile := CombatSkillDef.new()
	empty_family_profile.skill_id = &"empty_required_weapon_family_skill"
	empty_family_profile.required_weapon_families = [&""]
	var empty_family_errors: Array[String] = []
	registry._append_combat_profile_validation_errors(empty_family_errors, empty_family_profile.skill_id, empty_family_profile)
	_assert_true(
		_has_error_containing(empty_family_errors, "combat_profile.required_weapon_families[0] must be non-empty"),
		"required_weapon_families 应拒绝空武器家族。"
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
	invalid_trigger_effect.trigger_event = &"nonexistent_trigger"
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
		"hp_ratio_threshold_percent": 60,
		"bonus_damage_dice_count": 1,
		"bonus_damage_dice_sides": 6,
	}
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_damage_resolver_params_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "正式 damage_tag / dr_bypass_tag / hp_ratio_threshold_percent / bonus_damage_dice params 应通过 schema。")

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
		_has_error_containing(legacy_errors, "params.low_hp_ratio is unsupported; use hp_ratio_threshold_percent"),
		"params.low_hp_ratio 旧 schema 应被 SkillContentRegistry 静态拒绝。"
	)

	var invalid_bonus_effect := CombatEffectDef.new()
	invalid_bonus_effect.effect_type = &"damage"
	invalid_bonus_effect.bonus_condition = &"target_low_hp"
	invalid_bonus_effect.params = {
		"hp_ratio_threshold_percent": 60.0,
		"bonus_damage_dice_count": 1.5,
		"bonus_damage_dice_sides": 0,
	}
	var invalid_bonus_errors: Array[String] = []
	registry._append_effect_validation_errors(invalid_bonus_errors, &"invalid_bonus_damage_dice_skill", invalid_bonus_effect, "test_effect")
	_assert_true(
		_has_error_containing(invalid_bonus_errors, "params.hp_ratio_threshold_percent must be an int from 1 to 100"),
		"hp_ratio_threshold_percent 应要求整数百分比。"
	)
	_assert_true(
		_has_error_containing(invalid_bonus_errors, "params.bonus_damage_dice_count must be a positive int"),
		"bonus_damage_dice_count 应要求正整数。"
	)
	_assert_true(
		_has_error_containing(invalid_bonus_errors, "params.bonus_damage_dice_sides must be a positive int"),
		"bonus_damage_dice_sides 应要求正整数。"
	)


func _test_path_step_repeat_status_schema_validation() -> void:
	var registry := SkillContentRegistry.new()
	var valid_effect := CombatEffectDef.new()
	valid_effect.effect_type = &"path_step_aoe"
	valid_effect.params = {
		"repeat_hit_status_id": "staggered",
		"repeat_hit_status_threshold": 2,
		"repeat_hit_status_min_skill_level": 0,
		"repeat_hit_status_power": 1,
		"repeat_hit_status_duration_tu": 60,
	}
	var valid_errors: Array[String] = []
	registry._append_effect_validation_errors(valid_errors, &"valid_repeat_status_skill", valid_effect, "test_effect")
	_assert_true(valid_errors.is_empty(), "path_step_aoe repeat-hit 状态配置必须带正数 TU 时长并通过 schema。")

	var missing_duration_effect := CombatEffectDef.new()
	missing_duration_effect.effect_type = &"path_step_aoe"
	missing_duration_effect.params = valid_effect.params.duplicate(true)
	missing_duration_effect.params.erase("repeat_hit_status_duration_tu")
	var missing_duration_errors: Array[String] = []
	registry._append_effect_validation_errors(missing_duration_errors, &"missing_repeat_status_duration_skill", missing_duration_effect, "test_effect")
	_assert_true(
		_has_error_containing(missing_duration_errors, "requires params.repeat_hit_status_duration_tu"),
		"repeat-hit 状态配置缺少 duration_tu 时应被 SkillContentRegistry 静态拒绝。"
	)

	var zero_duration_effect := CombatEffectDef.new()
	zero_duration_effect.effect_type = &"path_step_aoe"
	zero_duration_effect.params = valid_effect.params.duplicate(true)
	zero_duration_effect.params["repeat_hit_status_duration_tu"] = 0
	var zero_duration_errors: Array[String] = []
	registry._append_effect_validation_errors(zero_duration_errors, &"zero_repeat_status_duration_skill", zero_duration_effect, "test_effect")
	_assert_true(
		_has_error_containing(zero_duration_errors, "must be a positive multiple"),
		"repeat-hit 状态配置的 duration_tu=0 会形成永久状态，应被静态拒绝。"
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

	var edge_clear_effect := CombatEffectDef.new()
	edge_clear_effect.effect_type = &"edge_clear"
	var edge_clear_errors: Array[String] = []
	registry._append_effect_validation_errors(edge_clear_errors, &"edge_clear_skill", edge_clear_effect, "test_effect")
	_assert_true(edge_clear_errors.is_empty(), "运行时支持的 edge_clear effect_type 应通过 schema。")

	var dispel_magic_effect := CombatEffectDef.new()
	dispel_magic_effect.effect_type = &"dispel_magic"
	var dispel_magic_errors: Array[String] = []
	registry._append_effect_validation_errors(dispel_magic_errors, &"dispel_magic_skill", dispel_magic_effect, "test_effect")
	_assert_true(dispel_magic_errors.is_empty(), "运行时支持的 dispel_magic effect_type 应通过 schema。")

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


func _collect_skill_effect_defs(skill_def: SkillDef) -> Array:
	var effects: Array = []
	if skill_def == null or skill_def.combat_profile == null:
		return effects
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null:
			effects.append(effect_def)
	for effect_def in skill_def.combat_profile.passive_effect_defs:
		if effect_def != null:
			effects.append(effect_def)
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effects.append(effect_def)
	return effects


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


func _make_minimal_schema_skill(skill_id: StringName) -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = skill_id
	skill_def.display_name = String(skill_id)
	skill_def.icon_id = skill_id
	skill_def.skill_type = &"passive"
	skill_def.max_level = 1
	skill_def.mastery_curve = PackedInt32Array([1])
	return skill_def


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
		_test.fail("%s 的 mage_fossil_to_mud 缺少 cast_variant[%d]。" % [source_label, index])
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
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
