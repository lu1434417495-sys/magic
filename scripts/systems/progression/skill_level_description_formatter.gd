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
	_merge_matching_effect_typed_fields(config, skill_def, level)
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

		if config.has(key) and _is_optional_value_visible(config[key]):
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


static func _is_optional_value_visible(value: Variant) -> bool:
	match typeof(value):
		TYPE_NIL:
			return false
		TYPE_BOOL:
			return bool(value)
		TYPE_INT:
			return int(value) != 0
		TYPE_FLOAT:
			var float_value := float(value)
			if float_value != float_value:
				return false
			return not is_equal_approx(float_value, 0.0)
		TYPE_STRING, TYPE_STRING_NAME:
			return not str(value).strip_edges().is_empty()
		_:
			return not str(value).strip_edges().is_empty()


static func _merge_matching_effect_params(config: Dictionary, skill_def: SkillDef, level: int) -> void:
	for effect_def in _collect_level_effect_defs(skill_def, level):
		if effect_def == null or effect_def.params == null:
			continue
		for param_key in effect_def.params.keys():
			if not config.has(param_key):
				config[param_key] = effect_def.params[param_key]


static func _merge_matching_effect_typed_fields(config: Dictionary, skill_def: SkillDef, level: int) -> void:
	for effect_def in _collect_level_effect_defs(skill_def, level):
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"damage":
				_merge_damage_effect_typed_fields(config, effect_def)
			&"status", &"apply_status":
				_merge_status_effect_typed_fields(config, effect_def)
			&"forced_move":
				if effect_def.forced_move_mode != &"":
					_set_if_missing(config, "forced_move_mode", String(effect_def.forced_move_mode))
				if int(effect_def.forced_move_distance) > 0:
					_set_if_missing(config, "forced_move_distance", int(effect_def.forced_move_distance))


static func _collect_level_effect_defs(skill_def: SkillDef, level: int) -> Array:
	var effect_defs: Array = []
	if skill_def == null or skill_def.combat_profile == null:
		return effect_defs
	_append_level_effect_defs(effect_defs, skill_def.combat_profile.effect_defs, level)
	for cast_variant in skill_def.combat_profile.get_unlocked_cast_variants(level):
		if cast_variant == null:
			continue
		_append_level_effect_defs(effect_defs, cast_variant.effect_defs, level)
	return effect_defs


static func _append_level_effect_defs(output: Array, effect_defs: Array, level: int) -> void:
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		if _effect_unlocked_at_level(effect_def, level):
			output.append(effect_def)


static func _effect_unlocked_at_level(effect_def, level: int) -> bool:
	if effect_def == null:
		return false
	var min_level := maxi(int(effect_def.min_skill_level), 0)
	if level < min_level:
		return false
	var max_level := int(effect_def.max_skill_level)
	return max_level < 0 or level <= max_level


static func _merge_damage_effect_typed_fields(config: Dictionary, effect_def) -> void:
	if int(effect_def.power) != 0:
		_set_if_missing(config, "damage_power", int(effect_def.power))
	if int(effect_def.damage_ratio_percent) != 100:
		_set_if_missing(config, "damage_ratio_percent", int(effect_def.damage_ratio_percent))
	if effect_def.damage_tag != &"":
		_set_if_missing(config, "damage_tag", String(effect_def.damage_tag))
	_merge_save_fields(config, "damage", effect_def)


static func _merge_status_effect_typed_fields(config: Dictionary, effect_def) -> void:
	var status_id := String(effect_def.status_id)
	if status_id.is_empty():
		return
	var status_label := _format_status_label(effect_def.status_id)
	_set_if_missing(config, "status_id", status_id)
	_set_if_missing(config, "status_display_name", status_label)
	if int(effect_def.duration_tu) > 0:
		_set_if_missing(config, "status_duration_tu", int(effect_def.duration_tu))
	if int(effect_def.power) != 0:
		_set_if_missing(config, "status_power", int(effect_def.power))
	_set_if_missing(config, "%s_status_id" % status_id, status_id)
	_set_if_missing(config, "%s_display_name" % status_id, status_label)
	if int(effect_def.duration_tu) > 0:
		_set_if_missing(config, "%s_duration_tu" % status_id, int(effect_def.duration_tu))
	if int(effect_def.power) != 0:
		_set_if_missing(config, "%s_power" % status_id, int(effect_def.power))
	_merge_save_fields(config, "status", effect_def)
	_merge_save_fields(config, status_id, effect_def)


static func _merge_save_fields(config: Dictionary, prefix: String, effect_def) -> void:
	if prefix.is_empty() or effect_def == null or effect_def.save_ability == &"":
		return
	var save_ability := String(effect_def.save_ability)
	var save_label := _format_attribute_label(effect_def.save_ability)
	_set_if_missing(config, "%s_save_ability" % prefix, save_ability)
	_set_if_missing(config, "%s_save_ability_label" % prefix, save_label)
	_set_if_missing(config, "%s_save_text" % prefix, _format_save_text(effect_def, save_label))


static func _format_save_text(effect_def, save_label: String) -> String:
	if effect_def == null:
		return ""
	if effect_def.effect_type == &"damage" and bool(effect_def.save_partial_on_success):
		return "%s豁免成功时伤害减半" % save_label
	if (effect_def.effect_type == &"status" or effect_def.effect_type == &"apply_status") and effect_def.status_id != &"":
		return "%s豁免失败时附加%s" % [save_label, _format_status_label(effect_def.status_id)]
	return "%s豁免" % save_label


static func _format_attribute_label(attribute_id: StringName) -> String:
	match attribute_id:
		&"strength":
			return "力量"
		&"agility":
			return "敏捷"
		&"constitution":
			return "体质"
		&"perception":
			return "感知"
		&"intelligence":
			return "智力"
		&"willpower":
			return "意志"
		_:
			return String(attribute_id)


static func _format_status_label(status_id: StringName) -> String:
	match status_id:
		&"shocked":
			return "感电"
		&"burning":
			return "燃烧"
		&"frozen":
			return "冻结"
		&"slow":
			return "迟缓"
		&"blind", &"blinded":
			return "失明"
		&"rooted":
			return "定身"
		&"staggered":
			return "踉跄"
		_:
			return String(status_id)


static func _set_if_missing(config: Dictionary, key: String, value) -> void:
	if config.has(key):
		return
	config[key] = value


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
