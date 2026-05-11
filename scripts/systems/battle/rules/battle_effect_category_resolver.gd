class_name BattleEffectCategoryResolver
extends RefCounted

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")


func resolve_categories(skill_def: SkillDef, effect_defs: Array) -> Array[StringName]:
	var categories: Array[StringName] = []
	var seen: Dictionary = {}
	if skill_def != null and skill_def.combat_profile != null:
		_append_categories(categories, seen, skill_def.combat_profile.delivery_categories)
	for effect_variant in effect_defs:
		if effect_variant == null or effect_variant is not Object:
			continue
		_append_categories(categories, seen, (effect_variant as Object).get("effect_categories"))
	return categories


func _append_categories(categories: Array[StringName], seen: Dictionary, values: Variant) -> void:
	if values is not Array:
		return
	for value in values:
		var category := _to_string_name(value)
		if category == &"" or seen.has(category):
			continue
		seen[category] = true
		categories.append(category)


func _to_string_name(value: Variant) -> StringName:
	if value is StringName:
		return value
	if value is String:
		return StringName(value)
	return &""
