## 文件说明：该脚本属于技能内容注册表相关的注册表脚本，集中维护技能定义集合、扫描目录和校验错误列表等顶层字段。
## 审查重点：重点核对技能主键、嵌套战斗资源结构、扫描失败提示以及资源迁移期间的兼容边界是否保持稳定。
## 备注：当前注册表只负责 skill resource 的扫描、校验和索引，不承担技能规则执行逻辑。

class_name SkillContentRegistry
extends RefCounted

const SKILL_CONFIG_DIRECTORY := "res://data/configs/skills"
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")

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
	return _skill_defs


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
	if skill_def.max_level <= 0:
		errors.append("Skill %s must have max_level >= 1." % String(skill_id))
	if skill_def.mastery_curve.size() != skill_def.max_level:
		errors.append(
			"Skill %s mastery_curve size must match max_level." % String(skill_id)
		)
	for mastery_threshold in skill_def.mastery_curve:
		if int(mastery_threshold) <= 0:
			errors.append("Skill %s has a non-positive mastery threshold." % String(skill_id))
			break

	if skill_def.skill_type == &"active" and skill_def.combat_profile == null:
		errors.append("Skill %s is active but missing combat_profile." % String(skill_id))

	if skill_def.combat_profile != null:
		_append_combat_profile_validation_errors(errors, skill_id, skill_def.combat_profile)


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
	if combat_profile.range_value < 0:
		errors.append("Skill %s combat_profile range_value must be >= 0." % String(skill_id))
	if combat_profile.area_value < 0:
		errors.append("Skill %s combat_profile area_value must be >= 0." % String(skill_id))
	if combat_profile.ap_cost < 0 or combat_profile.mp_cost < 0 or combat_profile.stamina_cost < 0 or combat_profile.aura_cost < 0:
		errors.append("Skill %s combat_profile costs must be >= 0." % String(skill_id))
	if combat_profile.cooldown_tu < 0:
		errors.append("Skill %s combat_profile cooldown_tu must be >= 0." % String(skill_id))
	if combat_profile.min_target_count <= 0:
		errors.append("Skill %s combat_profile min_target_count must be >= 1." % String(skill_id))
	if combat_profile.max_target_count < combat_profile.min_target_count:
		errors.append(
			"Skill %s combat_profile max_target_count must be >= min_target_count." % String(skill_id)
		)
	if combat_profile.effect_defs.is_empty() and combat_profile.cast_variants.is_empty():
		errors.append(
			"Skill %s combat_profile must declare effect_defs or cast_variants." % String(skill_id)
		)

	for effect_index in range(combat_profile.effect_defs.size()):
		_append_effect_validation_errors(
			errors,
			skill_id,
			combat_profile.effect_defs[effect_index] as CombatEffectDef,
			"combat_profile.effect_defs[%d]" % effect_index
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
		if cast_variant.effect_defs.is_empty():
			errors.append(
				"Skill %s cast variant %s must declare at least one effect." % [
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

	match effect_def.effect_type:
		&"status":
			if effect_def.status_id == &"":
				errors.append(
					"Skill %s status effect in %s is missing status_id." % [
						String(skill_id),
						context_label,
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
					"Skill %s terrain_effect in %s must have tick_interval_tu >= 1." % [
						String(skill_id),
						context_label,
					]
				)
		&"terrain_replace":
			if effect_def.terrain_replace_to == &"":
				errors.append(
					"Skill %s terrain_replace effect in %s is missing terrain_replace_to." % [
						String(skill_id),
						context_label,
					]
				)
		&"forced_move":
			var forced_move_mode := effect_def.forced_move_mode
			if forced_move_mode == &"":
				forced_move_mode = ProgressionDataUtils.to_string_name(effect_def.params.get("mode", ""))
			var forced_move_distance := int(effect_def.forced_move_distance)
			if forced_move_distance <= 0:
				forced_move_distance = int(effect_def.params.get("distance", 0))
			if forced_move_mode == &"":
				errors.append(
					"Skill %s forced_move effect in %s is missing forced_move_mode." % [
						String(skill_id),
						context_label,
					]
				)
			if forced_move_distance <= 0:
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
