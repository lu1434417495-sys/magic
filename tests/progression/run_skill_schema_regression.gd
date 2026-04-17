extends SceneTree

const DesignSkillCatalog = preload("res://scripts/player/progression/design_skill_catalog.gd")
const ProgressionContentRegistry = preload("res://scripts/player/progression/progression_content_registry.gd")
const SkillContentRegistry = preload("res://scripts/player/progression/skill_content_registry.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const OFFICIAL_SKILL_RESOURCE_DIRECTORY := "res://data/configs/skills/"
const OFFICIAL_HEAVY_STRIKE_PATH := "res://data/configs/skills/warrior_heavy_strike.tres"
const OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS: Array[StringName] = [
	&"warrior_heavy_strike",
	&"warrior_sweeping_slash",
	&"warrior_piercing_thrust",
	&"warrior_guard_break",
	&"warrior_execution_cleave",
	&"warrior_jump_slash",
	&"warrior_backstep",
	&"warrior_guard",
	&"warrior_shield_wall",
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
	_test_design_mage_catalog_root_cast_variant_compatibility()
	_test_seed_skill_resources_scan_and_validate()
	_test_progression_registry_keeps_skill_resource_and_compat_bridge()
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
	var heavy_strike := skill_defs.get(&"warrior_heavy_strike") as SkillDef
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(registry.validate().is_empty(), "SkillContentRegistry 的正式技能资源当前不应报告校验错误。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS, &"warrior", "战士")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_ARCHER_RESOURCE_SKILL_IDS, &"archer", "弓箭手")
	_assert_resource_backed_skill_ids(skill_defs, _build_mage_catalog_skill_ids(), &"mage", "法师")
	_assert_true(skill_defs.has(&"warrior_heavy_strike"), "SkillContentRegistry 应扫描到已迁移的重击资源。")
	_assert_true(heavy_strike != null, "重击资源应成功转成 SkillDef。")
	if heavy_strike == null:
		return
	_assert_eq(String(heavy_strike.resource_path), OFFICIAL_HEAVY_STRIKE_PATH, "重击应来自正式 skill resource。")
	_assert_eq(heavy_strike.display_name, "重击", "重击资源应保留展示名。")
	_assert_eq(heavy_strike.icon_id, &"warrior_heavy_strike", "重击资源应保留稳定 icon_id。")
	_assert_eq(int(heavy_strike.max_level), 3, "重击资源应保留 3 级上限。")
	_assert_true(heavy_strike.tags.has(&"warrior"), "重击资源应保留 warrior 标签。")
	_assert_true(heavy_strike.can_use_in_combat(), "重击资源应保留 combat_profile。")
	if heavy_strike.combat_profile != null:
		_assert_eq(int(heavy_strike.combat_profile.effect_defs.size()), 2, "重击资源应保留两条 effect_defs。")
		_assert_eq(int(heavy_strike.combat_profile.ap_cost), 2, "重击资源应保留 2 点 AP 消耗。")
		_assert_eq(int(heavy_strike.combat_profile.stamina_cost), 1, "重击资源应保留 1 点体力消耗。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "SkillContentRegistry")


func _test_progression_registry_keeps_skill_resource_and_compat_bridge() -> void:
	var registry := ProgressionContentRegistry.new()
	var skill_defs := registry.get_skill_defs()
	var heavy_strike := skill_defs.get(&"warrior_heavy_strike") as SkillDef
	var charge := skill_defs.get(&"charge") as SkillDef
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(registry.validate().is_empty(), "ProgressionContentRegistry 接入 skill resource 后仍应通过静态校验。")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_WARRIOR_RESOURCE_SKILL_IDS, &"warrior", "战士")
	_assert_resource_backed_skill_ids(skill_defs, OFFICIAL_ARCHER_RESOURCE_SKILL_IDS, &"archer", "弓箭手")
	_assert_resource_backed_skill_ids(skill_defs, _build_mage_catalog_skill_ids(), &"mage", "法师")
	_assert_true(heavy_strike != null, "ProgressionContentRegistry 应暴露已迁移的重击资源。")
	_assert_true(charge != null, "seed 未全迁完前，兼容桥仍应保留 code seed 的冲锋。")
	if heavy_strike != null:
		_assert_eq(String(heavy_strike.resource_path), OFFICIAL_HEAVY_STRIKE_PATH, "已迁移技能在 ProgressionContentRegistry 中不应被 code seed 覆盖。")
	if charge != null:
		_assert_true(String(charge.resource_path).is_empty(), "冲锋当前仍应通过兼容桥从 code seed 提供。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "ProgressionContentRegistry")


func _test_design_mage_catalog_root_cast_variant_compatibility() -> void:
	var skill_defs := _build_mage_catalog_skill_defs()
	var fossil_to_mud := skill_defs.get(&"mage_fossil_to_mud") as SkillDef

	_assert_true(fossil_to_mud != null, "DesignSkillCatalog 应继续解析根级 cast_variant 技能。")
	_assert_cast_variant_compat_shape(fossil_to_mud, "DesignSkillCatalog")


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


func _build_mage_catalog_skill_ids() -> Array[StringName]:
	var skill_defs := _build_mage_catalog_skill_defs()
	var skill_ids: Array[StringName] = []
	for skill_key in skill_defs.keys():
		skill_ids.append(StringName(skill_key))
	return skill_ids


func _build_mage_catalog_skill_defs() -> Dictionary:
	var skill_defs: Dictionary = {}
	DesignSkillCatalog.new().register_mage_skills(func(skill_def: SkillDef) -> void:
		if skill_def == null or skill_def.skill_id == &"":
			return
		skill_defs[skill_def.skill_id] = skill_def
	)
	return skill_defs


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
