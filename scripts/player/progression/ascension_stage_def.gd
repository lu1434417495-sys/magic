class_name AscensionStageDef
extends Resource

@export var stage_id: StringName = &""
@export var ascension_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""

@export var attribute_modifiers: Array[AttributeModifier] = []
@export var trait_ids: Array[StringName] = []
@export var racial_granted_skills: Array[RacialGrantedSkill] = []
@export var body_size_category_override: StringName = &""
@export var trait_summary: Array[String] = []
