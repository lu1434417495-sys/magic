## 文件说明：该脚本属于技能书物品工厂相关的辅助脚本，集中把 book 来源技能转换为共享仓库可识别的物品定义。
## 审查重点：重点核对技能到物品的映射规则、生成字段稳定性以及与手写 ItemDef 的覆盖关系。
## 备注：该工厂只负责生成运行时 ItemDef，不负责把技能书放入仓库或结算使用效果。

class_name SkillBookItemFactory
extends RefCounted

const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")

const DEFAULT_ICON_PATH := "res://icon.svg"
const DEFAULT_MAX_STACK := 20


static func build_item_id_for_skill(skill_id: StringName) -> StringName:
	return ProgressionDataUtils.to_string_name("skill_book_%s" % String(skill_id))


func build_generated_item_defs(skill_defs: Dictionary, existing_item_defs: Dictionary = {}) -> Dictionary:
	var generated_defs: Dictionary = {}
	for skill_key in skill_defs.keys():
		var skill_def: SkillDef = skill_defs.get(skill_key) as SkillDef
		if skill_def == null:
			continue
		if skill_def.skill_id == &"" or skill_def.learn_source != &"book":
			continue
		if skill_def.display_name.strip_edges().is_empty():
			continue

		var item_id := build_item_id_for_skill(skill_def.skill_id)
		if existing_item_defs.has(item_id):
			continue

		var item_def := ITEM_DEF_SCRIPT.new()
		item_def.item_id = item_id
		item_def.display_name = _build_display_name(skill_def)
		item_def.description = _build_description(skill_def)
		item_def.icon = DEFAULT_ICON_PATH
		item_def.is_stackable = true
		item_def.max_stack = DEFAULT_MAX_STACK
		item_def.item_category = ITEM_DEF_SCRIPT.ITEM_CATEGORY_SKILL_BOOK
		item_def.granted_skill_id = skill_def.skill_id
		generated_defs[item_id] = item_def
	return generated_defs


func _build_display_name(skill_def: SkillDef) -> String:
	return "%s 技能书" % skill_def.display_name.strip_edges()


func _build_description(skill_def: SkillDef) -> String:
	var skill_name := skill_def.display_name.strip_edges()
	var lines := PackedStringArray([
		"阅读后使一名队员学会技能：%s。" % skill_name,
	])
	if not skill_def.description.is_empty():
		lines.append(skill_def.description)
	return "\n".join(lines)
