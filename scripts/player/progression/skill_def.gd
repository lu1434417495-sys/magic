class_name SkillDef
extends Resource

@export var skill_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var skill_type: StringName = &"active"
@export var max_level := 1
@export var mastery_curve: PackedInt32Array = PackedInt32Array()
@export var tags: Array[StringName] = []
@export var learn_source: StringName = &"book"
@export var learn_requirements: Array[StringName] = []
@export var mastery_sources: Array[StringName] = []
@export var attribute_modifiers: Array[AttributeModifier] = []


func get_mastery_required_for_level(level: int) -> int:
	if level < 0 or level >= mastery_curve.size():
		return 0
	return mastery_curve[level]


func is_profession_skill() -> bool:
	return learn_source == &"profession"
