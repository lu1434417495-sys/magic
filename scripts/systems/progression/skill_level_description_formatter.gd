## 文件说明：该脚本属于技能展示描述格式化服务，负责把 SkillDef 的等级描述模板渲染成 UI 文本。
## 审查重点：该服务只做展示文本派生，不拥有技能配置、不修改资源，也不参与战斗结算。

class_name SkillLevelDescriptionFormatter
extends RefCounted

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


static func build_level_description(skill_def: SkillDef, level: int, runtime_context: Dictionary = {}) -> String:
	if skill_def == null:
		return ""
	if skill_def.level_description_template.is_empty():
		return ""

	var key := str(level)
	var raw_config: Variant = skill_def.level_description_configs.get(key, null)
	var config := {}
	if raw_config is Dictionary:
		config = (raw_config as Dictionary).duplicate()
	_merge_matching_effect_params(config, skill_def, level)
	_merge_level_overrides(config, skill_def, level)
	_resolve_charge_distance(config, level)
	for ctx_key in runtime_context.keys():
		config[ctx_key] = runtime_context[ctx_key]
	_apply_description_derived_fields(config)

	if config.is_empty():
		return ""
	return render_template(skill_def.level_description_template, config)


static func render_template(template: String, config: Dictionary) -> String:
	var result := template

	var cond_regex := RegEx.new()
	cond_regex.compile("\\{\\{\\?([^}]+)\\}\\}(.*?)\\{\\{/\\1\\}\\}")
	while true:
		var match_result := cond_regex.search(result)
		if match_result == null:
			break
		var key := match_result.get_string(1).strip_edges()
		var inner := match_result.get_string(2)
		var start := match_result.get_start()
		var end := match_result.get_end()

		if config.has(key) and not str(config[key]).strip_edges().is_empty():
			result = result.substr(0, start) + inner + result.substr(end)
		else:
			result = result.substr(0, start) + result.substr(end)

	var expr_regex := RegEx.new()
	expr_regex.compile("\\{=([^}]+)\\}")
	while true:
		var match_result := expr_regex.search(result)
		if match_result == null:
			break
		var expr_str := match_result.get_string(1).strip_edges()
		var replacement := _eval_expression(expr_str, config)
		result = result.substr(0, match_result.get_start()) + replacement + result.substr(match_result.get_end())

	var var_regex := RegEx.new()
	var_regex.compile("\\{([^}]+)\\}")
	while true:
		var match_result := var_regex.search(result)
		if match_result == null:
			break
		var key := match_result.get_string(1).strip_edges()
		var value := str(config.get(key, ""))
		var start := match_result.get_start()
		var end := match_result.get_end()
		result = result.substr(0, start) + value + result.substr(end)

	return result


static func _merge_matching_effect_params(config: Dictionary, skill_def: SkillDef, level: int) -> void:
	if skill_def == null or skill_def.combat_profile == null:
		return
	var all_effect_defs: Array = []
	all_effect_defs.append_array(skill_def.combat_profile.effect_defs)
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant != null and cast_variant.effect_defs != null:
			all_effect_defs.append_array(cast_variant.effect_defs)
	for effect_def in all_effect_defs:
		if effect_def == null or effect_def.params == null:
			continue
		var min_level := maxi(int(effect_def.min_skill_level), 0)
		var max_level := int(effect_def.max_skill_level)
		if level < min_level:
			continue
		if max_level >= 0 and level > max_level:
			continue
		for param_key in effect_def.params.keys():
			if not config.has(param_key):
				config[param_key] = effect_def.params[param_key]


static func _merge_level_overrides(config: Dictionary, skill_def: SkillDef, level: int) -> void:
	if skill_def == null or skill_def.combat_profile == null:
		return
	var profile = skill_def.combat_profile
	var override = profile.get_level_override(level)

	var fields = {
		"ap_cost": profile.ap_cost,
		"mp_cost": profile.mp_cost,
		"stamina_cost": profile.stamina_cost,
		"cooldown_tu": profile.cooldown_tu,
		"attack_roll_bonus": profile.attack_roll_bonus,
		"aura_cost": profile.aura_cost,
		"range_value": profile.range_value,
		"area_value": profile.area_value,
	}

	for field in fields.keys():
		if config.has(field):
			continue
		var value = override.get(field, fields[field])
		config[field] = value


static func _resolve_charge_distance(config: Dictionary, level: int) -> void:
	if config.has("distance"):
		return
	if not config.has("base_distance") and not config.has("distance_by_level"):
		return
	var base_distance = config.get("base_distance", 0)
	var distance_by_level = config.get("distance_by_level", {})
	if distance_by_level is not Dictionary:
		config["distance"] = base_distance
		return
	var distance = base_distance
	var keys = distance_by_level.keys()
	keys.sort()
	for key in keys:
		if int(key) > level:
			break
		distance = distance_by_level[key]
	config["distance"] = distance


static func _apply_description_derived_fields(config: Dictionary) -> void:
	if config.has("base_sides") and config.has("con_mod_sides") and config.has("will_mod_sides"):
		var base_sides := int(config.get("base_sides", 4))
		var con_mod := int(config.get("con_mod", 0))
		var will_mod := int(config.get("will_mod", 0))
		var con_mod_sides := int(config.get("con_mod_sides", 2))
		var will_mod_sides := int(config.get("will_mod_sides", 1))
		config["dice_sides"] = maxi(base_sides + con_mod * con_mod_sides + will_mod * will_mod_sides, 4)

	# NOTE: 如需为 attack_roll_bonus 等字段添加正负号显示，请在具体技能的
	# level_description_configs 中手动覆盖（如 "attack_roll_bonus": "+1"），
	# 或让模板直接引用原始数值变量。


static func _eval_expression(expr_str: String, variables: Dictionary) -> String:
	var expression := Expression.new()
	var input_names: Array[String] = []
	var input_values: Array = []
	for key in variables.keys():
		input_names.append(str(key))
		var value = variables[key]
		if value is String:
			if value.is_valid_int():
				input_values.append(int(value))
			elif value.is_valid_float():
				input_values.append(float(value))
			else:
				input_values.append(value)
		else:
			input_values.append(value)

	var err := expression.parse(expr_str, input_names)
	if err != OK:
		return "{=" + expr_str + "}"

	var expr_result = expression.execute(input_values)
	if expression.has_execute_failed():
		return "{=" + expr_str + "}"

	if expr_result is float and expr_result == floor(expr_result):
		return str(int(expr_result))
	return str(expr_result)
