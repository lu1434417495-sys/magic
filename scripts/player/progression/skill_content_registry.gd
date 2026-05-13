## 文件说明：该脚本属于技能内容注册表相关的注册表脚本，集中维护技能定义集合、扫描目录和校验错误列表等顶层字段。
## 审查重点：重点核对技能主键、嵌套战斗资源结构、扫描失败提示以及资源迁移期间的兼容边界是否保持稳定。
## 备注：当前注册表只负责 skill resource 的扫描、校验和索引，不承担技能规则执行逻辑。

class_name SkillContentRegistry
extends RefCounted

const SKILL_CONFIG_DIRECTORY := "res://data/configs/skills"
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const ATTRIBUTE_GROWTH_CONTENT_RULES = preload("res://scripts/player/progression/attribute_growth_content_rules.gd")
const BATTLE_SAVE_CONTENT_RULES = preload("res://scripts/player/progression/battle_save_content_rules.gd")
const BODY_SIZE_CONTENT_RULES = preload("res://scripts/player/progression/body_size_content_rules.gd")
const EQUIPMENT_RULES_SCRIPT = preload("res://scripts/player/equipment/equipment_rules.gd")
const TU_GRANULARITY := 5
const VALID_MASTERY_TRIGGER_MODES := [
	&"skill_damage_dice_max",
	&"weapon_attack_quality",
	&"damage_dealt",
	&"status_applied",
	&"effect_applied",
	&"incoming_physical_hit",
	&"secondary_hit",
]
const VALID_MASTERY_AMOUNT_MODES := [
	&"per_target_rank",
	&"per_cast_hp_ratio",
]
const VALID_SPELL_FATE_MODES := [
	&"",
	&"control_roll",
]
const VALID_SPELL_CRITICAL_MODES := [
	&"",
	&"mp_refund",
]
const VALID_BACKLASH_MODES := [
	&"",
	&"ground_anchor_drift",
]
const VALID_SAVE_DC_MODES := [
	BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_STATIC,
	BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_CASTER_SPELL,
]
const VALID_EFFECT_TRIGGER_EVENTS := [
	&"",
	&"critical_hit",
	&"ordinary_hit",
	&"secondary_hit",
]
const VALID_TRIGGER_CONDITIONS := [
	&"",
	&"battle_start",
	&"on_fatal_damage",
]
const VALID_EFFECT_TYPES := [
	&"body_size_category_override",
	&"chain_damage",
	&"charge",
	&"cleanse_harmful",
	&"damage",
	&"dispel_magic",
	&"edge_clear",
	&"equipment_durability_damage",
	&"erase_status",
	&"forced_move",
	&"heal",
	&"heal_fatal",
	&"height",
	&"height_delta",
	&"layered_barrier",
	&"on_kill_gain_resources",
	&"path_step_aoe",
	&"repeat_attack_until_fail",
	&"shield",
	&"stamina_restore",
	&"status",
	&"apply_status",
	&"terrain",
	&"terrain_effect",
	&"terrain_replace",
	&"terrain_replace_to",
	&"execute",
]

## 字段说明：缓存技能定义集合字典，集中保存可按键查询的运行时数据。
var _skill_defs: Dictionary = {}
## 字段说明：收集配置校验阶段发现的错误信息，便于启动时统一报告和定位问题。
var _validation_errors: Array[String] = []


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_skill_defs.clear()
	_validation_errors.clear()
	_scan_directory(SKILL_CONFIG_DIRECTORY)
	_validation_errors.append_array(_collect_validation_errors())


func get_skill_defs() -> Dictionary:
	return _skill_defs.duplicate()


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("SkillContentRegistry could not find %s." % directory_path)
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("SkillContentRegistry could not open %s." % directory_path)
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue

		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_scan_directory(entry_path)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_skill_resource(entry_path)
	directory.list_dir_end()


func _register_skill_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load skill config %s." % resource_path)
		return
	if resource.get_script() != SKILL_DEF_SCRIPT:
		_validation_errors.append("Skill config %s is not a SkillDef." % resource_path)
		return

	var skill_def := resource as SkillDef
	if skill_def == null:
		_validation_errors.append("Skill config %s failed to cast to SkillDef." % resource_path)
		return

	_normalize_skill_def(skill_def)

	if skill_def.skill_id == &"":
		_validation_errors.append("Skill config %s is missing skill_id." % resource_path)
		return
	if _skill_defs.has(skill_def.skill_id):
		_validation_errors.append("Duplicate skill_id registered: %s" % String(skill_def.skill_id))
		return

	_skill_defs[skill_def.skill_id] = skill_def


func _normalize_skill_def(skill_def: SkillDef) -> void:
	if skill_def == null:
		return
	if skill_def.skill_id != &"" and skill_def.icon_id == &"":
		skill_def.icon_id = skill_def.skill_id
	if skill_def.combat_profile != null and skill_def.skill_id != &"" and skill_def.combat_profile.skill_id == &"":
		skill_def.combat_profile.skill_id = skill_def.skill_id


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []

	for skill_key in ProgressionDataUtils.sorted_string_keys(_skill_defs):
		var skill_id := StringName(skill_key)
		var skill_def := _skill_defs.get(skill_id) as SkillDef
		if skill_def == null:
			continue
		_append_skill_validation_errors(errors, skill_id, skill_def)

	return errors


func _append_skill_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	if skill_def.display_name.strip_edges().is_empty():
		errors.append("Skill %s is missing display_name." % String(skill_id))
	if skill_def.icon_id == &"":
		errors.append("Skill %s is missing icon_id." % String(skill_id))
	if skill_def.max_level < 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append("Skill %s must have max_level >= 0." % String(skill_id))
	if skill_def.non_core_max_level < 0:
		errors.append("Skill %s non_core_max_level must be >= 0." % String(skill_id))
	if skill_def.non_core_max_level > skill_def.max_level and skill_def.max_level >= 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append("Skill %s non_core_max_level must be <= max_level." % String(skill_id))
	if skill_def.mastery_curve.size() != skill_def.max_level and skill_def.max_level >= 0 and skill_def.dynamic_max_level_stat_id == &"":
		errors.append(
			"Skill %s mastery_curve size must match max_level." % String(skill_id)
		)
	_append_dynamic_max_level_validation_errors(errors, skill_id, skill_def)
	for mastery_threshold in skill_def.mastery_curve:
		if int(mastery_threshold) <= 0:
			errors.append("Skill %s has a non-positive mastery threshold." % String(skill_id))
			break

	if skill_def.skill_type == &"active" and skill_def.combat_profile == null:
		errors.append("Skill %s is active but missing combat_profile." % String(skill_id))
	_append_attribute_growth_validation_errors(errors, skill_id, skill_def)

	if skill_def.combat_profile != null:
		_append_combat_profile_validation_errors(errors, skill_id, skill_def.combat_profile)


func _append_dynamic_max_level_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	var has_dynamic_stat := skill_def.dynamic_max_level_stat_id != &""
	if not has_dynamic_stat:
		if skill_def.dynamic_max_level_base != 0:
			errors.append("Skill %s dynamic_max_level_base requires dynamic_max_level_stat_id." % String(skill_id))
		if skill_def.dynamic_max_level_per_stat != 0:
			errors.append("Skill %s dynamic_max_level_per_stat requires dynamic_max_level_stat_id." % String(skill_id))
		return

	if skill_def.dynamic_max_level_base <= 0:
		errors.append("Skill %s dynamic_max_level_base must be >= 1." % String(skill_id))
	if skill_def.dynamic_max_level_per_stat == 0:
		errors.append("Skill %s dynamic_max_level_per_stat must not be 0 when dynamic_max_level_stat_id is set." % String(skill_id))


func _append_attribute_growth_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	skill_def: SkillDef
) -> void:
	if skill_def.attribute_growth_progress.is_empty() and skill_def.growth_tier == &"":
		return
	if not ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_growth_tier(skill_def.growth_tier):
		errors.append("Skill %s uses unsupported growth_tier %s." % [String(skill_id), String(skill_def.growth_tier)])
		return

	var progress_total := 0
	for attribute_key in skill_def.attribute_growth_progress.keys():
		if typeof(attribute_key) != TYPE_STRING or String(attribute_key).strip_edges().is_empty():
			errors.append("Skill %s attribute_growth_progress key %s must be a non-empty String." % [String(skill_id), str(attribute_key)])
			continue
		var attribute_id := ProgressionDataUtils.to_string_name(attribute_key)
		var amount_variant: Variant = skill_def.attribute_growth_progress.get(attribute_key, null)
		if typeof(amount_variant) != TYPE_INT:
			errors.append("Skill %s attribute_growth_progress for %s must be a positive int." % [String(skill_id), String(attribute_id)])
			continue
		var amount := int(amount_variant)
		if not ATTRIBUTE_GROWTH_CONTENT_RULES.is_valid_attribute_id(attribute_id):
			errors.append("Skill %s attribute_growth_progress references invalid attribute %s." % [String(skill_id), String(attribute_id)])
		if amount <= 0:
			errors.append("Skill %s attribute_growth_progress for %s must be a positive int." % [String(skill_id), String(attribute_id)])
		progress_total += amount

	var expected_total := ATTRIBUTE_GROWTH_CONTENT_RULES.get_tier_budget(skill_def.growth_tier)
	if progress_total != expected_total:
		errors.append(
			"Skill %s attribute_growth_progress total must equal %d for growth_tier %s." % [
				String(skill_id),
				expected_total,
				String(skill_def.growth_tier),
			]
		)


func _append_combat_profile_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	combat_profile: CombatSkillDef
) -> void:
	if combat_profile.skill_id != skill_id:
		errors.append(
			"Skill %s combat_profile.skill_id must match skill_id." % String(skill_id)
		)
	if combat_profile.target_mode == &"":
		errors.append("Skill %s combat_profile is missing target_mode." % String(skill_id))
	if combat_profile.target_team_filter == &"":
		errors.append("Skill %s combat_profile is missing target_team_filter." % String(skill_id))
	if combat_profile.target_selection_mode == &"":
		errors.append("Skill %s combat_profile is missing target_selection_mode." % String(skill_id))
	if combat_profile.selection_order_mode == &"":
		errors.append("Skill %s combat_profile is missing selection_order_mode." % String(skill_id))
	if not VALID_MASTERY_TRIGGER_MODES.has(combat_profile.mastery_trigger_mode):
		errors.append("Skill %s combat_profile uses unsupported mastery_trigger_mode %s." % [String(skill_id), String(combat_profile.mastery_trigger_mode)])
	if not VALID_MASTERY_AMOUNT_MODES.has(combat_profile.mastery_amount_mode):
		errors.append("Skill %s combat_profile uses unsupported mastery_amount_mode %s." % [String(skill_id), String(combat_profile.mastery_amount_mode)])
	if combat_profile.range_value < 0:
		errors.append("Skill %s combat_profile range_value must be >= 0." % String(skill_id))
	if combat_profile.area_value < 0:
		errors.append("Skill %s combat_profile area_value must be >= 0." % String(skill_id))
	if combat_profile.ap_cost < 0 or combat_profile.mp_cost < 0 or combat_profile.stamina_cost < 0 or combat_profile.aura_cost < 0:
		errors.append("Skill %s combat_profile costs must be >= 0." % String(skill_id))
	if not _is_valid_tu_value(int(combat_profile.cooldown_tu)):
		errors.append("Skill %s combat_profile cooldown_tu must be 0 or a multiple of %d." % [String(skill_id), TU_GRANULARITY])
	_append_spell_fate_validation_errors(errors, skill_id, combat_profile)
	_append_string_name_array_validation_errors(errors, skill_id, "combat_profile.required_weapon_families", combat_profile.required_weapon_families)
	_append_string_name_array_validation_errors(errors, skill_id, "combat_profile.excluded_weapon_families", combat_profile.excluded_weapon_families)
	_append_string_name_array_validation_errors(errors, skill_id, "combat_profile.excluded_weapon_type_ids", combat_profile.excluded_weapon_type_ids)
	for override_level_key in combat_profile.level_overrides.keys():
		if typeof(override_level_key) != TYPE_INT:
			errors.append("Skill %s combat_profile level override key %s must be an int." % [String(skill_id), str(override_level_key)])
			continue
		var override_data = combat_profile.level_overrides.get(override_level_key)
		if override_data is not Dictionary:
			errors.append("Skill %s combat_profile level override %s must be a Dictionary." % [String(skill_id), String(override_level_key)])
			continue
		var override_level := int(override_level_key)
		if override_level < 0:
			errors.append("Skill %s combat_profile level override %s must use a non-negative level." % [String(skill_id), String(override_level_key)])
		var override_dict := override_data as Dictionary
		for cost_key in ["ap_cost", "mp_cost", "stamina_cost", "aura_cost"]:
			if override_dict.has(cost_key) and int(override_dict.get(cost_key, 0)) < 0:
				errors.append("Skill %s combat_profile level override %s.%s must be >= 0." % [String(skill_id), String(override_level_key), String(cost_key)])
		if override_dict.has("cooldown_tu") and not _is_valid_tu_value(int(override_dict.get("cooldown_tu", 0))):
			errors.append("Skill %s combat_profile level override %s.cooldown_tu must be 0 or a multiple of %d." % [String(skill_id), String(override_level_key), TU_GRANULARITY])
		if override_dict.has("area_value") and int(override_dict.get("area_value", 0)) < 0:
			errors.append("Skill %s combat_profile level override %s.area_value must be >= 0." % [String(skill_id), String(override_level_key)])
		if override_dict.has("max_target_count") and int(override_dict.get("max_target_count", 0)) < 1:
			errors.append("Skill %s combat_profile level override %s.max_target_count must be >= 1." % [String(skill_id), String(override_level_key)])
	if combat_profile.min_target_count <= 0:
		errors.append("Skill %s combat_profile min_target_count must be >= 1." % String(skill_id))
	if combat_profile.max_target_count < combat_profile.min_target_count:
		errors.append(
			"Skill %s combat_profile max_target_count must be >= min_target_count." % String(skill_id)
		)
	# Mage design seeds intentionally contain placeholder active skills and selection-only variants.
	# Resource validation keeps structural checks, but it must not reject those zero-effect carriers.
	for effect_index in range(combat_profile.effect_defs.size()):
		_append_effect_validation_errors(
			errors,
			skill_id,
			combat_profile.effect_defs[effect_index] as CombatEffectDef,
			"combat_profile.effect_defs[%d]" % effect_index
		)

	if combat_profile.passive_effect_defs != null and combat_profile.passive_effect_defs.size() > 0:
		for passive_index in range(combat_profile.passive_effect_defs.size()):
			var passive_effect := combat_profile.passive_effect_defs[passive_index] as CombatEffectDef
			if passive_effect != null and passive_effect.effect_type == &"execute":
				errors.append(
					"Skill %s passive_effect_defs[%d] uses effect_type 'execute', which is not allowed in passive effects." % [
						String(skill_id),
						passive_index,
					]
				)
				continue
			_append_effect_validation_errors(
				errors,
				skill_id,
				passive_effect,
				"combat_profile.passive_effect_defs[%d]" % passive_index
			)

	var seen_variant_ids: Dictionary = {}
	for variant_index in range(combat_profile.cast_variants.size()):
		var cast_variant := combat_profile.cast_variants[variant_index] as CombatCastVariantDef
		if cast_variant == null:
			errors.append(
				"Skill %s combat_profile.cast_variants[%d] failed to cast to CombatCastVariantDef." % [
					String(skill_id),
					variant_index,
				]
			)
			continue
		if cast_variant.variant_id == &"":
			errors.append(
				"Skill %s has a cast variant without variant_id." % String(skill_id)
			)
		elif seen_variant_ids.has(cast_variant.variant_id):
			errors.append(
				"Skill %s declares duplicate cast variant %s." % [
					String(skill_id),
					String(cast_variant.variant_id),
				]
			)
		else:
			seen_variant_ids[cast_variant.variant_id] = true
		if cast_variant.target_mode == &"":
			errors.append(
				"Skill %s cast variant %s is missing target_mode." % [
					String(skill_id),
					String(cast_variant.variant_id),
				]
			)
		if cast_variant.required_coord_count <= 0:
			errors.append(
				"Skill %s cast variant %s must have required_coord_count >= 1." % [
					String(skill_id),
					String(cast_variant.variant_id),
				]
			)
		for effect_index in range(cast_variant.effect_defs.size()):
			_append_effect_validation_errors(
				errors,
				skill_id,
				cast_variant.effect_defs[effect_index] as CombatEffectDef,
				"combat_profile.cast_variants[%d].effect_defs[%d]" % [variant_index, effect_index]
			)


func _append_spell_fate_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	combat_profile: CombatSkillDef
) -> void:
	if combat_profile == null:
		return
	if not VALID_SPELL_FATE_MODES.has(combat_profile.spell_fate_mode):
		errors.append("Skill %s combat_profile uses unsupported spell_fate_mode %s." % [String(skill_id), String(combat_profile.spell_fate_mode)])
	if not VALID_SPELL_CRITICAL_MODES.has(combat_profile.spell_critical_mode):
		errors.append("Skill %s combat_profile uses unsupported spell_critical_mode %s." % [String(skill_id), String(combat_profile.spell_critical_mode)])
	if not VALID_BACKLASH_MODES.has(combat_profile.backlash_mode):
		errors.append("Skill %s combat_profile uses unsupported backlash_mode %s." % [String(skill_id), String(combat_profile.backlash_mode)])
	if combat_profile.spell_critical_mode != &"" and combat_profile.spell_fate_mode == &"":
		errors.append("Skill %s combat_profile spell_critical_mode requires spell_fate_mode." % String(skill_id))
	if combat_profile.backlash_mode != &"" and combat_profile.spell_fate_mode == &"":
		errors.append("Skill %s combat_profile backlash_mode requires spell_fate_mode." % String(skill_id))
	if int(combat_profile.spell_critical_mp_refund_percent) < 0 or int(combat_profile.spell_critical_mp_refund_percent) > 100:
		errors.append("Skill %s combat_profile spell_critical_mp_refund_percent must be between 0 and 100." % String(skill_id))
	if int(combat_profile.fumble_protection_extra_mp_percent) < 0:
		errors.append("Skill %s combat_profile fumble_protection_extra_mp_percent must be >= 0." % String(skill_id))
	for protection_value in combat_profile.fumble_protection_curve:
		if int(protection_value) < 0:
			errors.append("Skill %s combat_profile fumble_protection_curve values must be >= 0." % String(skill_id))
			break
	if combat_profile.backlash_offset_radius < 0:
		errors.append("Skill %s combat_profile backlash_offset_radius must be >= 0." % String(skill_id))
	if combat_profile.backlash_mode == &"ground_anchor_drift":
		if combat_profile.target_mode != &"ground":
			errors.append("Skill %s combat_profile ground_anchor_drift requires target_mode ground." % String(skill_id))
		if combat_profile.backlash_offset_radius <= 0:
			errors.append("Skill %s combat_profile ground_anchor_drift requires backlash_offset_radius >= 1." % String(skill_id))


func _append_effect_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def == null:
		errors.append("Skill %s has a null effect in %s." % [String(skill_id), context_label])
		return
	if effect_def.effect_type == &"":
		errors.append("Skill %s has an effect without effect_type in %s." % [String(skill_id), context_label])
		return
	if not VALID_EFFECT_TYPES.has(effect_def.effect_type):
		errors.append(
			"Skill %s effect %s uses unsupported effect_type %s." % [
				String(skill_id),
				context_label,
				String(effect_def.effect_type),
			]
		)
	if effect_def.min_skill_level < 0:
		errors.append("Skill %s effect %s min_skill_level must be >= 0." % [String(skill_id), context_label])
	if effect_def.max_skill_level >= 0 and effect_def.max_skill_level < effect_def.min_skill_level:
		errors.append("Skill %s effect %s max_skill_level must be >= min_skill_level or -1." % [String(skill_id), context_label])
	if not VALID_EFFECT_TRIGGER_EVENTS.has(effect_def.trigger_event):
		errors.append(
			"Skill %s effect %s uses unsupported trigger_event %s." % [
				String(skill_id),
				context_label,
				String(effect_def.trigger_event),
			]
		)
	if not VALID_TRIGGER_CONDITIONS.has(effect_def.trigger_condition):
		errors.append(
			"Skill %s effect %s uses unsupported trigger_condition %s." % [
				String(skill_id),
				context_label,
				String(effect_def.trigger_condition),
			]
		)
	if not _is_valid_tu_value(int(effect_def.duration_tu)):
		errors.append(
			"Skill %s effect %s duration_tu must be 0 or a multiple of %d." % [
				String(skill_id),
				context_label,
				TU_GRANULARITY,
			]
		)
	if not _is_valid_tu_value(int(effect_def.tick_interval_tu)):
		errors.append(
			"Skill %s effect %s tick_interval_tu must be 0 or a multiple of %d." % [
				String(skill_id),
				context_label,
				TU_GRANULARITY,
			]
		)
	_append_save_validation_errors(errors, skill_id, effect_def, context_label)
	if effect_def.params != null:
		var unsupported_param_aliases := {
			"damage_dice_count": "dice_count",
			"damage_dice_sides": "dice_sides",
			"damage_dice_bonus": "dice_bonus",
			"tag": "damage_tag",
			"bypass_tag": "dr_bypass_tag",
			"low_hp_ratio": "hp_ratio_threshold_percent",
		}
		for legacy_param in unsupported_param_aliases.keys():
			if effect_def.params.has(legacy_param):
				errors.append(
					"Skill %s effect %s params.%s is unsupported; use %s." % [
						String(skill_id),
						context_label,
						String(legacy_param),
						String(unsupported_param_aliases.get(legacy_param, "")),
					]
				)
		if effect_def.params.has("duration"):
			errors.append(
				"Skill %s effect %s params.duration is unsupported; use duration_tu." % [
					String(skill_id),
					context_label,
				]
			)
		if effect_def.params.has("duration_tu"):
			var params_duration_tu := int(effect_def.params.get("duration_tu", 0))
			if not _is_valid_tu_value(params_duration_tu):
				errors.append(
					"Skill %s effect %s params.duration_tu must be 0 or a multiple of %d." % [
						String(skill_id),
						context_label,
						TU_GRANULARITY,
					]
				)
		_append_weapon_param_validation_errors(errors, skill_id, effect_def, context_label)

	match effect_def.effect_type:
		&"damage":
			_append_damage_effect_validation_errors(errors, skill_id, effect_def, context_label)
		&"status", &"apply_status":
			if effect_def.status_id == &"":
				errors.append(
					"Skill %s status effect in %s is missing status_id." % [
						String(skill_id),
						context_label,
					]
				)
		&"shield":
			var has_dice_keys := effect_def.params != null \
				and (effect_def.params.has("dice_count") or effect_def.params.has("dice_sides"))
			var has_valid_dice_config := _has_valid_shield_dice_config(effect_def)
			if effect_def.power <= 0 and not has_valid_dice_config:
				errors.append(
					"Skill %s shield effect in %s must have power >= 1 or a valid dice_count/dice_sides config." % [
						String(skill_id),
						context_label,
					]
				)
			if has_dice_keys and not has_valid_dice_config:
				errors.append(
					"Skill %s shield effect in %s must set dice_count and dice_sides >= 1 together." % [
						String(skill_id),
						context_label,
					]
				)
			if effect_def.duration_tu <= 0 and int(effect_def.params.get("duration_tu", 0)) <= 0:
				errors.append(
					"Skill %s shield effect in %s must have positive duration_tu in %d TU steps." % [
						String(skill_id),
						context_label,
						TU_GRANULARITY,
					]
				)
		&"terrain_effect":
			if effect_def.terrain_effect_id == &"":
				errors.append(
					"Skill %s terrain_effect in %s is missing terrain_effect_id." % [
						String(skill_id),
						context_label,
					]
				)
			if effect_def.duration_tu > 0 and effect_def.tick_interval_tu <= 0:
				errors.append(
					"Skill %s terrain_effect in %s must have positive tick_interval_tu in %d TU steps." % [
						String(skill_id),
						context_label,
						TU_GRANULARITY,
					]
				)
		&"terrain", &"terrain_replace", &"terrain_replace_to":
			if effect_def.terrain_replace_to == &"":
				errors.append(
					"Skill %s terrain_replace effect in %s is missing terrain_replace_to." % [
						String(skill_id),
						context_label,
					]
				)
		&"height", &"height_delta":
			if int(effect_def.height_delta) == 0:
				errors.append(
					"Skill %s height effect in %s must have non-zero height_delta." % [
						String(skill_id),
						context_label,
					]
				)
		&"body_size_category_override":
			if effect_def.status_id == &"":
				errors.append(
					"Skill %s body_size_category_override effect in %s is missing status_id." % [
						String(skill_id),
						context_label,
					]
				)
			if effect_def.body_size_category == &"":
				errors.append(
					"Skill %s body_size_category_override effect in %s is missing body_size_category." % [
						String(skill_id),
						context_label,
					]
				)
			elif not BODY_SIZE_CONTENT_RULES.is_valid_body_size_category(effect_def.body_size_category):
				errors.append(
					"Skill %s body_size_category_override effect in %s uses unsupported body_size_category %s." % [
						String(skill_id),
						context_label,
						String(effect_def.body_size_category),
					]
				)
			if effect_def.duration_tu <= 0:
				errors.append(
					"Skill %s body_size_category_override effect in %s must have positive duration_tu." % [
						String(skill_id),
						context_label,
					]
				)
		&"forced_move":
			if effect_def.params != null:
				if effect_def.params.has("mode"):
					errors.append(
						"Skill %s forced_move effect in %s params.mode is unsupported; use forced_move_mode." % [
							String(skill_id),
							context_label,
						]
					)
				if effect_def.params.has("distance"):
					errors.append(
						"Skill %s forced_move effect in %s params.distance is unsupported; use forced_move_distance." % [
							String(skill_id),
							context_label,
						]
					)
			if effect_def.forced_move_mode == &"":
				errors.append(
					"Skill %s forced_move effect in %s is missing forced_move_mode." % [
						String(skill_id),
						context_label,
					]
				)
			if effect_def.forced_move_mode == &"jump":
				_append_jump_effect_validation_errors(errors, skill_id, effect_def, context_label)
			elif int(effect_def.forced_move_distance) <= 0:
				errors.append(
					"Skill %s forced_move effect in %s must have forced_move_distance >= 1." % [
						String(skill_id),
						context_label,
					]
				)
		&"charge":
			if ProgressionDataUtils.to_string_name(effect_def.params.get("skill_id", "")) == &"":
				errors.append(
					"Skill %s charge effect in %s is missing params.skill_id." % [
						String(skill_id),
						context_label,
						]
				)
		&"path_step_aoe":
			_append_path_step_aoe_validation_errors(errors, skill_id, effect_def, context_label)
		&"equipment_durability_damage":
			_append_equipment_durability_damage_validation_errors(errors, skill_id, effect_def, context_label)


func _append_damage_effect_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def == null or effect_def.params == null:
		return
	if effect_def.params.has("hp_ratio_threshold_percent"):
		var threshold_value = effect_def.params.get("hp_ratio_threshold_percent")
		if typeof(threshold_value) != TYPE_INT or int(threshold_value) < 1 or int(threshold_value) > 100:
			errors.append(
				"Skill %s damage effect in %s params.hp_ratio_threshold_percent must be an int from 1 to 100." % [
					String(skill_id),
					context_label,
				]
			)
	var has_bonus_dice_key := effect_def.params.has("bonus_damage_dice_count") \
		or effect_def.params.has("bonus_damage_dice_sides") \
		or effect_def.params.has("bonus_damage_dice_bonus")
	if not has_bonus_dice_key:
		return
	if effect_def.bonus_condition == &"":
		errors.append(
			"Skill %s damage effect in %s bonus_damage_dice requires bonus_condition." % [
				String(skill_id),
				context_label,
			]
		)
	var count_value = effect_def.params.get("bonus_damage_dice_count", 0)
	var sides_value = effect_def.params.get("bonus_damage_dice_sides", 0)
	if typeof(count_value) != TYPE_INT or int(count_value) < 1:
		errors.append(
			"Skill %s damage effect in %s params.bonus_damage_dice_count must be a positive int." % [
				String(skill_id),
				context_label,
			]
		)
	if typeof(sides_value) != TYPE_INT or int(sides_value) < 1:
		errors.append(
			"Skill %s damage effect in %s params.bonus_damage_dice_sides must be a positive int." % [
				String(skill_id),
				context_label,
			]
		)
	if effect_def.params.has("bonus_damage_dice_bonus") and typeof(effect_def.params.get("bonus_damage_dice_bonus")) != TYPE_INT:
		errors.append(
			"Skill %s damage effect in %s params.bonus_damage_dice_bonus must be an int." % [
				String(skill_id),
				context_label,
			]
		)


func _append_equipment_durability_damage_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def == null:
		return
	if effect_def.power <= 0:
		errors.append(
			"Skill %s equipment_durability_damage effect in %s must have power >= 1." % [
				String(skill_id),
				context_label,
			]
		)
	var params: Dictionary = effect_def.params if effect_def.params != null else {}
	var has_dynamic_save := ProgressionDataUtils.to_string_name(effect_def.save_dc_mode) == BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_CASTER_SPELL
	if int(effect_def.save_dc) <= 0 and not has_dynamic_save:
		errors.append(
			"Skill %s equipment_durability_damage effect in %s must configure a save DC." % [
				String(skill_id),
				context_label,
			]
		)
	var max_damaged_items := int(params.get("max_damaged_items", 1))
	if max_damaged_items != 1:
		errors.append(
			"Skill %s equipment_durability_damage effect in %s currently supports max_damaged_items = 1 only." % [
				String(skill_id),
				context_label,
			]
		)
	if not bool(params.get("require_damage_applied", false)):
		errors.append(
			"Skill %s equipment_durability_damage effect in %s must set params.require_damage_applied = true." % [
				String(skill_id),
				context_label,
			]
		)
	var target_slots: Variant = params.get("target_slots", [])
	if target_slots is Array and target_slots.is_empty():
		errors.append(
			"Skill %s equipment_durability_damage effect in %s params.target_slots must include at least one slot." % [
				String(skill_id),
				context_label,
			]
		)
	_append_equipment_slot_array_validation_errors(
		errors,
		skill_id,
		context_label,
		target_slots,
		"target_slots"
	)
	_append_equipment_slot_weight_validation_errors(
		errors,
		skill_id,
		context_label,
		params.get("slot_weight_map", {}),
		"slot_weight_map"
	)


func _append_equipment_slot_array_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	context_label: String,
	value: Variant,
	param_name: String
) -> void:
	if value is not Array:
		errors.append(
			"Skill %s equipment_durability_damage effect in %s params.%s must be an Array." % [
				String(skill_id),
				context_label,
				param_name,
			]
		)
		return
	var seen_slots: Dictionary = {}
	for raw_slot_id in value:
		var slot_id := ProgressionDataUtils.to_string_name(raw_slot_id)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			errors.append(
				"Skill %s equipment_durability_damage effect in %s params.%s uses unsupported slot %s." % [
					String(skill_id),
					context_label,
					param_name,
					String(slot_id),
				]
			)
			continue
		if seen_slots.has(slot_id):
			errors.append(
				"Skill %s equipment_durability_damage effect in %s params.%s repeats slot %s." % [
					String(skill_id),
					context_label,
					param_name,
					String(slot_id),
				]
			)
		seen_slots[slot_id] = true


func _append_equipment_slot_weight_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	context_label: String,
	value: Variant,
	param_name: String
) -> void:
	if value is not Dictionary:
		errors.append(
			"Skill %s equipment_durability_damage effect in %s params.%s must be a Dictionary." % [
				String(skill_id),
				context_label,
				param_name,
			]
		)
		return
	var weight_map := value as Dictionary
	for key in weight_map.keys():
		var slot_id := ProgressionDataUtils.to_string_name(key)
		if not EQUIPMENT_RULES_SCRIPT.is_valid_slot(slot_id):
			errors.append(
				"Skill %s equipment_durability_damage effect in %s params.%s uses unsupported slot %s." % [
					String(skill_id),
					context_label,
					param_name,
					String(slot_id),
				]
			)
		var weight: Variant = weight_map.get(key)
		if weight is not int or int(weight) <= 0:
			errors.append(
				"Skill %s equipment_durability_damage effect in %s params.%s.%s must be a positive int." % [
					String(skill_id),
					context_label,
					param_name,
					String(slot_id),
				]
			)


func _append_save_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def == null:
		return
	var save_dc := int(effect_def.save_dc)
	var save_dc_mode := ProgressionDataUtils.to_string_name(effect_def.save_dc_mode)
	var dynamic_save_dc := save_dc_mode == BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_CASTER_SPELL
	var has_save_dc := save_dc > 0 or dynamic_save_dc
	var save_ability := ProgressionDataUtils.to_string_name(effect_def.save_ability)
	var save_dc_source_ability := ProgressionDataUtils.to_string_name(effect_def.save_dc_source_ability)
	var save_tag := ProgressionDataUtils.to_string_name(effect_def.save_tag)
	if not VALID_SAVE_DC_MODES.has(save_dc_mode):
		errors.append(
			"Skill %s effect %s uses unsupported save_dc_mode %s." % [
				String(skill_id),
				context_label,
				String(save_dc_mode),
			]
		)
	if save_dc < 0:
		errors.append("Skill %s effect %s save_dc must be >= 0." % [String(skill_id), context_label])
	if dynamic_save_dc and save_dc > 0:
		errors.append("Skill %s effect %s caster_spell save_dc_mode must leave static save_dc at 0." % [String(skill_id), context_label])
	if not dynamic_save_dc and save_dc_source_ability != &"":
		errors.append("Skill %s effect %s save_dc_source_ability requires caster_spell save_dc_mode." % [String(skill_id), context_label])
	if dynamic_save_dc and not BATTLE_SAVE_CONTENT_RULES.VALID_SAVE_ABILITIES.has(save_dc_source_ability):
		errors.append(
			"Skill %s effect %s uses unsupported save_dc_source_ability %s." % [
				String(skill_id),
				context_label,
				String(save_dc_source_ability),
			]
		)
	if not has_save_dc:
		if save_ability != &"":
			errors.append("Skill %s effect %s save_ability requires save_dc >= 1 or caster_spell save_dc_mode." % [String(skill_id), context_label])
		if save_tag != &"":
			errors.append("Skill %s effect %s save_tag requires save_dc >= 1 or caster_spell save_dc_mode." % [String(skill_id), context_label])
		if effect_def.save_failure_status_id != &"":
			errors.append("Skill %s effect %s save_failure_status_id requires save_dc >= 1 or caster_spell save_dc_mode." % [String(skill_id), context_label])
		if bool(effect_def.save_partial_on_success):
			errors.append("Skill %s effect %s save_partial_on_success requires save_dc >= 1 or caster_spell save_dc_mode." % [String(skill_id), context_label])
		return
	if not BATTLE_SAVE_CONTENT_RULES.VALID_SAVE_ABILITIES.has(save_ability):
		errors.append(
			"Skill %s effect %s uses unsupported save_ability %s." % [
				String(skill_id),
				context_label,
				String(save_ability),
			]
		)
	if not BATTLE_SAVE_CONTENT_RULES.VALID_SAVE_TAGS.has(save_tag):
		errors.append(
			"Skill %s effect %s uses unsupported save_tag %s." % [
				String(skill_id),
				context_label,
				String(save_tag),
			]
		)
	if bool(effect_def.save_partial_on_success) and effect_def.effect_type != &"damage":
		errors.append("Skill %s effect %s save_partial_on_success is only supported on damage effects." % [String(skill_id), context_label])
	if effect_def.save_failure_status_id != &"" \
			and effect_def.effect_type != &"status" \
			and effect_def.effect_type != &"apply_status":
		errors.append("Skill %s effect %s save_failure_status_id is only supported on status effects." % [String(skill_id), context_label])


func _append_path_step_aoe_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def == null or effect_def.params == null:
		return
	var params: Dictionary = effect_def.params
	if params.has("path_step_log_label") and String(params.get("path_step_log_label", "")).strip_edges().is_empty():
		errors.append(
			"Skill %s path_step_aoe effect in %s params.path_step_log_label must be non-empty when set." % [
				String(skill_id),
				context_label,
			]
		)
	if not _has_repeat_hit_status_config(params):
		return
	var status_id := ProgressionDataUtils.to_string_name(params.get("repeat_hit_status_id", ""))
	if status_id == &"":
		errors.append(
			"Skill %s path_step_aoe effect in %s repeat-hit status config requires params.repeat_hit_status_id." % [
				String(skill_id),
				context_label,
			]
		)
	if int(params.get("repeat_hit_status_threshold", 0)) < 1:
		errors.append(
			"Skill %s path_step_aoe effect in %s params.repeat_hit_status_threshold must be >= 1." % [
				String(skill_id),
				context_label,
			]
		)
	if int(params.get("repeat_hit_status_min_skill_level", 0)) < 0:
		errors.append(
			"Skill %s path_step_aoe effect in %s params.repeat_hit_status_min_skill_level must be >= 0." % [
				String(skill_id),
				context_label,
			]
		)
	if int(params.get("repeat_hit_status_power", 1)) < 1:
		errors.append(
			"Skill %s path_step_aoe effect in %s params.repeat_hit_status_power must be >= 1." % [
				String(skill_id),
				context_label,
			]
		)
	if not params.has("repeat_hit_status_duration_tu"):
		errors.append(
			"Skill %s path_step_aoe effect in %s repeat-hit status config requires params.repeat_hit_status_duration_tu." % [
				String(skill_id),
				context_label,
			]
		)
	else:
		var repeat_hit_status_duration_tu := int(params.get("repeat_hit_status_duration_tu", 0))
		if repeat_hit_status_duration_tu <= 0 or not _is_valid_tu_value(repeat_hit_status_duration_tu):
			errors.append(
				"Skill %s path_step_aoe effect in %s params.repeat_hit_status_duration_tu must be a positive multiple of %d." % [
					String(skill_id),
					context_label,
					TU_GRANULARITY,
				]
			)
	if params.has("repeat_hit_status_params") and params.get("repeat_hit_status_params") is not Dictionary:
		errors.append(
			"Skill %s path_step_aoe effect in %s params.repeat_hit_status_params must be a Dictionary." % [
				String(skill_id),
				context_label,
			]
		)


func _has_repeat_hit_status_config(params: Dictionary) -> bool:
	for key in [
		"repeat_hit_status_id",
		"repeat_hit_status_threshold",
		"repeat_hit_status_min_skill_level",
		"repeat_hit_status_power",
		"repeat_hit_status_duration_tu",
		"repeat_hit_status_params",
		"repeat_hit_status_log_template",
	]:
		if params.has(key):
			return true
	return false


func _append_jump_effect_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if int(effect_def.forced_move_distance) < 0:
		errors.append(
			"Skill %s jump effect in %s must have forced_move_distance >= 0 (0 = no max_range cap)." % [
				String(skill_id),
				context_label,
			]
		)
	if float(effect_def.jump_arc_ratio) < CombatEffectDef.MIN_JUMP_ARC_RATIO:
		errors.append(
			"Skill %s jump effect in %s requires jump_arc_ratio >= %.2f; jump must lift the unit." % [
				String(skill_id),
				context_label,
				CombatEffectDef.MIN_JUMP_ARC_RATIO,
			]
		)
	if float(effect_def.jump_arc_ratio) > 1.0:
		errors.append(
			"Skill %s jump effect in %s requires jump_arc_ratio <= 1.0." % [
				String(skill_id),
				context_label,
			]
		)
	if int(effect_def.jump_base_budget) < 0:
		errors.append(
			"Skill %s jump effect in %s must have jump_base_budget >= 0." % [
				String(skill_id),
				context_label,
			]
		)
	if float(effect_def.jump_str_scale) < 0.0:
		errors.append(
			"Skill %s jump effect in %s must have jump_str_scale >= 0." % [
				String(skill_id),
				context_label,
			]
		)
	if int(effect_def.jump_range_multiplier) < 1:
		errors.append(
			"Skill %s jump effect in %s must have jump_range_multiplier >= 1." % [
				String(skill_id),
				context_label,
			]
		)


func _append_weapon_param_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	effect_def: CombatEffectDef,
	context_label: String
) -> void:
	if effect_def.params.has("requires_weapon"):
		if typeof(effect_def.params.get("requires_weapon")) != TYPE_BOOL:
			errors.append(
				"Skill %s effect %s params.requires_weapon must be a bool." % [
					String(skill_id),
					context_label,
				]
			)
	if effect_def.params.has("use_weapon_physical_damage_tag"):
		if typeof(effect_def.params.get("use_weapon_physical_damage_tag")) != TYPE_BOOL:
			errors.append(
				"Skill %s effect %s params.use_weapon_physical_damage_tag must be a bool." % [
					String(skill_id),
					context_label,
				]
			)
	if effect_def.params.has("resolve_as_weapon_attack"):
		if typeof(effect_def.params.get("resolve_as_weapon_attack")) != TYPE_BOOL:
			errors.append(
				"Skill %s effect %s params.resolve_as_weapon_attack must be a bool." % [
					String(skill_id),
					context_label,
				]
			)


func _append_string_name_array_validation_errors(
	errors: Array[String],
	skill_id: StringName,
	field_label: String,
	values: Array
) -> void:
	for index in range(values.size()):
		var value = values[index]
		if typeof(value) != TYPE_STRING_NAME:
			errors.append("Skill %s %s[%d] must be a StringName." % [String(skill_id), field_label, index])
			continue
		if ProgressionDataUtils.to_string_name(value) == &"":
			errors.append("Skill %s %s[%d] must be non-empty." % [String(skill_id), field_label, index])


func _is_valid_tu_value(value: int) -> bool:
	if value < 0:
		return false
	if value == 0:
		return true
	return value % TU_GRANULARITY == 0


func _has_valid_shield_dice_config(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and int(effect_def.params.get("dice_count", 0)) > 0 \
		and int(effect_def.params.get("dice_sides", 0)) > 0
