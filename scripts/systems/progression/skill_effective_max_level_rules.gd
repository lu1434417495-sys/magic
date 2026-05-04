## 文件说明：该脚本属于技能有效等级上限规则，集中维护动态上限与非核心上限的组合计算。
## 审查重点：重点核对动态上限来源、非核心封顶顺序，以及调用方是否传入了当前 UnitProgress。
## 备注：后续如果新增动态上限技能，应优先扩展这里，避免各服务各自判断满级。

class_name SkillEffectiveMaxLevelRules
extends RefCounted


static func get_effective_max_level(
	skill_def: SkillDef,
	skill_progress: Variant,
	unit_progress: UnitProgress
) -> int:
	if skill_def == null:
		return 0
	var absolute_max := get_effective_absolute_max_level(skill_def, unit_progress)
	var configured_non_core_max := int(skill_def.non_core_max_level)
	if configured_non_core_max > 0 and (skill_progress == null or not bool(skill_progress.is_core)):
		return mini(absolute_max, configured_non_core_max)
	return absolute_max


static func get_effective_absolute_max_level(skill_def: SkillDef, unit_progress: UnitProgress) -> int:
	if skill_def == null:
		return 0
	if _uses_dynamic_max_level(skill_def):
		var stat_value := _get_dynamic_max_level_stat_value(skill_def.dynamic_max_level_stat_id, unit_progress)
		var base_level := maxi(int(skill_def.dynamic_max_level_base), 0)
		var level_per_stat := maxi(int(skill_def.dynamic_max_level_per_stat), 0)
		return base_level + stat_value * level_per_stat
	return maxi(int(skill_def.max_level), 0)


static func is_at_effective_max_level(
	skill_def: SkillDef,
	skill_progress: Variant,
	unit_progress: UnitProgress
) -> bool:
	if skill_def == null or skill_progress == null:
		return false
	return int(skill_progress.skill_level) >= get_effective_max_level(skill_def, skill_progress, unit_progress)


static func _uses_dynamic_max_level(skill_def: SkillDef) -> bool:
	return skill_def != null and skill_def.dynamic_max_level_stat_id != &""


static func _get_dynamic_max_level_stat_value(stat_id: StringName, unit_progress: UnitProgress) -> int:
	if unit_progress == null or unit_progress.unit_base_attributes == null:
		return 0
	return maxi(
		int(unit_progress.unit_base_attributes.get_attribute_value(stat_id)),
		0
	)
