## 文件说明：该脚本集中维护技能等级描述模板与等级配置的静态内容契约。
## 审查重点：保持这里仅校验 schema 形状，不推导模板变量，也不承担 UI 渲染职责。
## 备注：当前只硬校验已经接入 level_description_template/configs 的技能；未迁移技能不阻断启动。

class_name SkillLevelDescriptionContentRules
extends RefCounted

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")


static func append_validation_errors(errors: Array[String], skill_id: StringName, skill_def: SkillDef) -> void:
	if skill_def == null:
		return
	var template := String(skill_def.level_description_template).strip_edges()
	var configs: Dictionary = skill_def.level_description_configs
	var has_template := not template.is_empty()
	var has_configs := not configs.is_empty()

	if not has_template and not has_configs:
		return
	if has_template and not has_configs:
		errors.append("Skill %s level_description_configs must be non-empty when level_description_template is set." % String(skill_id))
		return
	if not has_template and has_configs:
		errors.append("Skill %s level_description_template must be non-empty when level_description_configs is set." % String(skill_id))
		return

	var valid_levels: Array[int] = []
	var lowest_declared_level := -1
	var highest_declared_level := -1
	var has_dynamic_max_level := skill_def.dynamic_max_level_stat_id != &""
	for level_key in configs.keys():
		var parsed_level := _parse_level_key(level_key)
		if parsed_level < 0:
			errors.append(
				"Skill %s level_description_configs key %s must be a non-negative integer string." % [
					String(skill_id),
					str(level_key),
				]
			)
			continue

		lowest_declared_level = parsed_level if lowest_declared_level < 0 else mini(lowest_declared_level, parsed_level)
		highest_declared_level = maxi(highest_declared_level, parsed_level)
		valid_levels.append(parsed_level)
		var level_config: Variant = configs.get(level_key, null)
		if level_config is not Dictionary:
			errors.append(
				"Skill %s level_description_configs[%d] must be a Dictionary." % [
					String(skill_id),
					parsed_level,
				]
			)

		if not has_dynamic_max_level and skill_def.max_level >= 0 and parsed_level > int(skill_def.max_level):
			errors.append(
				"Skill %s level_description_configs[%d] must be <= max_level %d." % [
					String(skill_id),
					parsed_level,
					int(skill_def.max_level),
				]
			)

	if valid_levels.is_empty():
		return

	var declared_levels := {}
	for level in valid_levels:
		declared_levels[level] = true
	for expected_level in range(lowest_declared_level, highest_declared_level + 1):
		if not declared_levels.has(expected_level):
			errors.append(
				"Skill %s level_description_configs must include level %d." % [
					String(skill_id),
					expected_level,
				]
			)


static func _parse_level_key(level_key: Variant) -> int:
	if typeof(level_key) != TYPE_STRING:
		return -1
	var text := String(level_key).strip_edges()
	if text.is_empty() or not text.is_valid_int():
		return -1
	var parsed_level := int(text)
	if parsed_level < 0:
		return -1
	if str(parsed_level) != text:
		return -1
	return parsed_level
